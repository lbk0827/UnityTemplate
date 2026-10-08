# F2: 로컬 푸시 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sf LocalPushManager/LocalNotification 레이어의 **스케줄 모델**(백그라운드 진입 시 전부 취소 후 재예약, 포그라운드 복귀 시 OS 큐 비움, 하트 가득 참 알림, 30일치 일일 리텐션 푸시 팬아웃, 일정 진행 뒤 권한 요청)을 BKFramework에 재구현한다. 플랫폼 호출은 `INotificationPlatform` 뒤에 숨기고 Unity Mobile Notifications 2.5.0 구현을 `BK_MOBILE_NOTIFICATIONS` 정의로 감싼다.

**Architecture:** 새 레이어 `BK.Notifications`(Core, Save, UniTask, VContainer). `LocalPushService`는 등록된 `IPushProvider`들이 만든 `NotificationRequest`를 백그라운드 진입 시 플랫폼에 예약한다(권한 허용 시). 공급자: `DailyRetentionPushProvider`(Notifications 레이어, 문구 풀 셔플 팬아웃), `HeartFullPushProvider`(BK.Meta, `IWallet`의 FullAt 사용 → Meta가 Notifications를 참조). 레이어: `Core ← Save ← Notifications ← Meta`. 권한 플래그는 `PushData : SaveData`.

**sf 규칙(권위):**
- 모드 QueueClearAndReschedule: 보내기는 메모리 큐에 쌓고, pause 시 1초 미만 미래 항목 제거 → OS 예약, foreground 시 큐 비우고 OS 전부 취소. 즉 OS에는 백그라운드 동안만 알림이 있다.
- 하트 가득 참(id 1001): 충전형 재화가 최대 미만일 때 `GetFullRechargeDateTime()`에 1회성. 매 백그라운드마다 재등록.
- 일일 리텐션: 30개 1회성 슬롯 id 100200..100229, 매일 로컬 18:40(오늘 지났으면 내일부터), 111개 문구 풀을 피셔-예이츠 셔플해 슬롯별 본문. 야간 금지 규칙은 없음(고정 시각이 야간을 피함).
- 권한: 초기화 시 요청하지 않음. 스테이지 15 클리어에서 `MarkPendingPermissionRequest()`(이미 요청했으면 무시, 플래그 저장), 로비 리빌 후 `TryRequestPendingPermissionAsync()` → OS 요청, 결과와 무관하게 "요청함" 저장, 허용이면 전체 재등록. 에디터는 즉시 true.

---

### Task 1: BK.Notifications 서비스 + 테스트

**Files:** `Runtime/Notifications/BK.Notifications.asmdef`, `NotificationRequest.cs`, `INotificationPlatform.cs`, `NullNotificationPlatform.cs`, `IPushProvider.cs`, `PushData.cs`, `LocalPushService.cs`, `LocalPushDriver.cs`(MonoBehaviour: pause/foreground 중계), `DailyRetentionPushProvider.cs`; 테스트 `LocalPushServiceTests.cs`, `DailyRetentionPushProviderTests.cs`.

- `NotificationRequest { int Id; string Title; string Body; DateTime FireAtLocal; }` (반복은 팬아웃으로 대체하므로 없음).
- `INotificationPlatform { bool IsSupported; UniTask<bool> RequestPermissionAsync(); void Schedule(NotificationRequest); void CancelAll(); }`.
- `LocalPushService(ISaveService, IClock, INotificationPlatform, IEnumerable<IPushProvider>)`:
  - `PermissionRequested`, `PermissionGranted`(PushData), `IsPermissionPending`.
  - `MarkPendingPermissionRequest()`: 이미 요청했거나 pending이면 무시.
  - `TryRequestPendingPermissionAsync()`: pending일 때만 플랫폼 요청 → requested=true, granted=결과 저장 → 허용이면 `OnBackgrounded`와 같은 재예약은 하지 않는다(OS에는 백그라운드에만). 반환 bool.
  - `OnBackgrounded()`: `CancelAll` → granted면 공급자 요청을 모아 `FireAtLocal > now+1s`인 것만 `Schedule`. 반환: 예약 수.
  - `OnForegrounded()`: `CancelAll`.
  - `Providers`는 생성자 주입 + `Register(IPushProvider)`.
- `LocalPushDriver`(MonoBehaviour, 서비스가 Play 모드에서 생성): `OnApplicationPause(true)` → `OnBackgrounded`, `OnApplicationPause(false)`/`OnApplicationFocus(true)` → `OnForegrounded`(중복 호출 무해).
- `DailyRetentionPushProvider(DailyRetentionConfig cfg)`: `IdBase=100200`, `Days=30`, `Hour=18, Minute=40`, `Title`, `Bodies[]`. `Build(nowLocal)`: 첫 발사 = 오늘 18:40, 지났으면 내일; 슬롯 i는 +i일; 본문은 `Shuffle(bodies, seed)`를 순환(`bodies.Length < Days`면 반복). seed는 생성자 인자(기본 `nowLocal.Date` 기반)로 테스트 결정성 확보.

테스트:
- `LocalPushServiceTests`: 권한 미허용이면 백그라운드에 예약 0 + CancelAll 1회; 허용 후 백그라운드에 공급자 요청 예약, 1초 미만 미래는 제외; 포그라운드는 CancelAll; MarkPending → TryRequest 흐름(요청 1회만, 거부 시 granted=false 저장); 상태 영속.
- `DailyRetentionPushProviderTests`: 30개 id 고유·연속, 오늘 18:40 전이면 오늘부터, 후면 내일부터, 본문이 셔플된 풀을 빠짐없이 쓴다(30 ≥ 풀 크기 시), 같은 seed면 같은 순서.

- [x] 구현 → csc → 커밋 `feat(notifications): add LocalPushService with daily retention fan-out`

---

### Task 2: 하트 가득 참 공급자 (BK.Meta)

**Files:** `Runtime/Meta/Push/HeartFullPushProvider.cs`; BK.Meta asmdef에 `BK.Notifications` 추가; 테스트 `HeartFullPushProviderTests.cs`.

- `HeartFullPushProvider(IWallet wallet, string currencyId, int id, string title, string body)`: 재화가 Rechargeable이고 `Value < RechargeMax`이며 `Anchor`가 있으면 `RechargeLogic.FullAt` → 로컬 시각으로 1건. 무한 버프 중이거나 가득 찼으면 0건.
- 규약 테스트의 레이어 맵: `BK.Notifications: [Core, Save]`, `BK.Meta: [Core, Save, Notifications]`.

- [x] 구현 → 테스트 → 커밋 `feat(meta): add heart-full push provider`

---

### Task 3: Unity Mobile Notifications 플랫폼 + 설치

**Files:** `Packages/manifest.json`(`com.unity.mobile.notifications: 2.5.0`), `Runtime/Notifications/Unity/UnityMobileNotificationsPlatform.cs`(`#if BK_MOBILE_NOTIFICATIONS`), asmdef `versionDefines`(`com.unity.mobile.notifications` → `BK_MOBILE_NOTIFICATIONS`), 참조 `Unity.Notifications`(옵션: 패키지 없으면 경고만), `NotificationsInstaller.cs`.

- Android: 채널 `bk_default` 등록, `AndroidNotification{Title, Text, FireTime}` → `SendNotification(…, channel)`, `CancelAllScheduledNotifications`; 권한: `AndroidNotificationCenter.UserPermissionToPost`/`RequestUserPermission` 코루틴 대체는 `PermissionRequest` 폴링(UniTask).
- iOS: `iOSNotification{Identifier, Title, Body, Trigger=iOSNotificationCalendarTrigger}`, `iOSNotificationCenter.RemoveAllScheduledNotifications`, `RequestAuthorizationAsync`.
- 에디터/기타: `NullNotificationPlatform`(IsSupported=false, 권한 true, 로그만).
- `AppLifetimeScope`: `NotificationsInstaller.Install(builder)` → 플랫폼(정의 유무로 선택) + `LocalPushService` 싱글턴. 공급자는 게임이 `As<IPushProvider>()`로 등록.

- [x] 구현 → csc(정의 없이/있이 둘 다) → 커밋 `feat(notifications): add Unity Mobile Notifications platform and installer`

---

### Task 4: Bounce 배선 + 문서

- `IntegratedProjectScope.ConfigureProject`: `HeartFullPushProvider(Heart, 1001, "Hearts are full", "Your hearts are back. Ready for the next stage?")`, `DailyRetentionPushProvider`(18:40, 풀 5문구)를 `IPushProvider`로 등록.
- `KitApp.Complete(won)`: 클리어 스테이지 ≥ 3이면 `push.MarkPendingPermissionRequest()`; `GoToLobby` 로드 완료 후 `TryRequestPendingPermissionAsync().Forget()`.
- CLAUDE.md Layout/Rules, 로드맵 F2 완료.

- [x] 커밋 `feat(bounce): schedule heart and retention pushes` / `docs: record local push`

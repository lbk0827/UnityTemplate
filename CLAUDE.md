# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

# Project: BKFrameWork

## 공용 템플릿과 게임 전용 영역 관리 (2026-10-21)

사용자 결정: 공용 템플릿은 `main`, Bounce 게임은 `develop-bounce`에서 개발한다. 작업 시작 전에 현재 브랜치와 변경 범위를 확인한다.

| 구분 | 관리 브랜치 | 변경 범위 |
| --- | --- | --- |
| 공용 템플릿 | `main` | 여러 게임에서 재사용할 아웃게임, 로비, 상점, 팝업의 구조·동작·공통 연출·기본 리소스, SafeArea, 프레임워크와 공통 도구 |
| Bounce 게임 | `develop-bounce` | 인게임 조준·발사·물리·기믹·레벨·밸런스, 게임 전용 오브젝트·리소스, Bounce 전용 설정과 연결 코드 |
| 공용 화면의 게임별 구성 | `develop-bounce` | Bounce 상품·재화·보상·진입 정책, 게임 시작 연결, 테마·아이콘·안내 내용 등 게임별 차이 |

### 코드와 리소스의 경계

- `Assets/BKFramework/`는 게임에 독립적인 프레임워크다. `Assets/_Project/` 또는 Bounce 타입·에셋에 대한 의존성을 추가하지 않는다.
- `Assets/_Project/Bounce/`에는 현재 인게임뿐 아니라 로비·상점·팝업 구현도 함께 있다. 폴더 이름만으로 변경의 소유 브랜치를 결정하지 않는다. 다른 게임에도 적용할 개선이면 공용 작업, Bounce의 규칙·데이터·테마에 한정되면 게임 작업으로 분류한다.
- 현재 디렉터리 구조는 공용 화면과 게임 전용 연결이 완전히 분리된 상태가 아니다. 이 문서 추가만으로 코드나 에셋이 분리된 것으로 간주하지 않는다.
- 공용 화면은 재사용 가능한 구조·동작을 제공하고, 상품 목록·재화·보상·게임 시작 처리·스킨은 게임이 설정이나 인터페이스로 공급하도록 점진적으로 분리한다. 공용 UI를 분리한다는 이유로 게임 의존 코드를 `BKFramework`에 옮기지 않는다.
- 공용 씬·프리팹 원본은 `main`에서 관리한다. 게임별 차이는 가능하면 설정, 데이터, 프리팹 Variant로 표현한다. 양쪽 브랜치에서 공용 원본을 서로 다른 용도로 중복 수정하지 않는다.
- Unity 에셋을 실제로 이동하거나 분리할 때는 `.meta`와 GUID를 유지하고 씬·프리팹·Addressables 참조를 검증한다. 별도 요청이나 해당 작업에 필요한 이유 없이 대규모 구조 변경을 함께 수행하지 않는다.

### 브랜치와 반영 절차

1. 공용 변경은 `main`을 기준으로 작업하고, 검증 후 `main`에 반영한다. 임시 작업 브랜치를 쓰더라도 기준·반영 대상은 동일하다.
2. Bounce 전용 변경은 `develop-bounce`를 기준으로 작업하고 해당 브랜치에 반영한다.
3. `main`의 공용 변경은 `develop-bounce`로 주기적으로 병합한다. 충돌 시 공용 개선과 게임별 설정을 모두 보존하고, 영향받는 화면·게임 흐름을 다시 검증한다.
4. Bounce 개발 중 공통 수정이 필요하면 게임 변경과 별도 커밋으로 분리한다. 이미 `develop-bounce`에서 수정했다면 게임 전용 의존성이 없는 공통 커밋만 선별해 `main`에 반영한다. `develop-bounce` 전체를 공용 변경 반영 목적으로 `main`에 병합하지 않는다.
5. 양쪽에 필요한 운영 문서 같은 독립 변경은 해당 커밋만 선별 적용할 수 있다. 기존 미커밋 작업을 임의로 버리거나 덮어쓰면서 브랜치를 전환하지 않는다.
6. 완료한 변경은 성격별로 분리해 한글 제목·설명으로 커밋하고, 검증 후 해당 원격 브랜치에 푸시한다. 완료 보고에 반영 브랜치와 커밋을 적는다.

`HANDOVER.md`는 과거 작업 기록이다. 현재 운영 기준은 이 절을 따르며, 과거의 남은 작업이나 커밋 절차를 현재 요청으로 해석하지 않는다.

## Integrated Bounce project (2026-10-08)

- Active game: `Assets/_Project/Bounce/`, imported from BK_Kit and adapted to BKFramework.
- Open `BK > Integration > Open Bounce Bootstrap`, then Play. Entry scene: `Assets/_Project/Bounce/Scenes/VisualBootstrap.unity`.
- `IntegratedProjectScope` boots BKFramework and installs the Bounce meta loop (`MetaInstaller` with `BounceCurrencies`, `BounceContinueOffers`, `BounceEntry.Policy`, `BounceStepOffers`, `BounceWinStreak`). `KitApp` is a facade over `IWallet`, `IStageProgress`, `IOptionsService`, `ISaveService`, `PendingRewardQueue`/`CurrencyDisplayLock`, `ContinueOffers`, `StepOffers`/`WinStreak` and `IMessageService`; it owns no state and flushes the save after every mutation. The former sample project (`Project` namespace lobby) was removed on 2026-10-09; `Assets/_Project/` now holds only Bounce plus the shared `Settings/FrameworkSettings`, `Data/TableCatalog`, `Localization/{en,ko}` and `UI/Sequences`.
- Bounce save: `persistentDataPath/Bounce/Profiles/local/<Type>.json` (`WalletData`, `StageProgressData`, `OptionsData`, `BounceProfileData`, `StepOfferData`, `WinStreakData`); tests set `BK_KIT_TEST_SAVE_DIR` and read/seed through `KitTestSaves`. Hearts: stages 1-3 free, then one heart per entry (`KitApp.Play` → `IStageProgress.TryStart`), refunded on clear. Continue on loss: 150/300/450/600 gold for +5 balls (`KitApp.TryContinue`). A failed write keeps the data dirty and retries (no rollback).
- Bounce lobby (sf layout): home keeps the imported stage_slot path; the Play button opens `WinStreakPopup` (pre-play: streak gauge, milestones 3/5/10, next-round boosters) which calls `KitApp.Play`. Step offers run on BK.Meta `StepOffers` with `BounceStepOffers` (epoch 2026-01-05 UTC, unlock stage 2, one type per week: odd weeks vertical 4 steps, even weeks chain 7 steps; paid steps reference shop rows 1001+/1011+ and only show a price). Daily rewards were removed from the lobby on 2026-10-16 (BK.Meta `DailyRewards` stays in the framework, unused by Bounce). `WinStreakPopup` is a `BouncePopupView` prefab at `Assets/_Project/Bounce/Views/` with addresses `Bounce/UI/<Name>`; regenerate with `BK > Integration > Generate Bounce UI Prefabs`. Payments stay disconnected (`KitApp.PaymentsNotConnected`); `StepOffers.ConfirmPaid` is the hook for a future store.
- Keep the existing `BKFramework` independent of the game module. Imported presentation remains game content, with its existing provenance.
- Active Bounce fonts: Lilita One (Latin) with Jua (Korean fallback), SIL OFL 1.1. Preserve each original TTF and `Resources/FontLicenses/<family>/OFL.txt` under `Assets/_Project/ThirdParty/`. Resources packages the full copyright/license notices with players; Settings > Font Licenses displays both offline. Rebuild atlases with `BK > Integration > Rebuild OFL Fonts`. Historical Kenney CC0 source/provenance is retained; do not reimport DUG fonts.
- Bounce shop data lives in `Assets/_Project/Bounce/Content/Tables/`. Edit the JSON sources and run `BK > Integration > Import Shop Tables`. The two game tables are the only entries in `Assets/_Project/Data/TableCatalog.asset` and load through BK.Data at boot.
- Target editor remains 6000.3.21f1. See `docs/bk-kit-integration.json` for actual validation versions/results.

A reusable Unity template framework for personal side projects. Unity 6000.3.21f1, URP.

## Stack

Local-only by design — no backend, no analytics, no remote config.

| Concern | Package |
| --- | --- |
| DI / lifetime scopes | VContainer 1.19.0 |
| Async | UniTask 2.5.11 |
| Reactive | R3 1.3.1 |
| Content | Addressables |
| Tween / sequencing | LitMotion (코드 트윈), Animation Sequencer 0.5.5 + DOTween 1.3.030 free (프리팹 UI 연출) |
| String building | ZString |

## Layout

```
Assets/BKFramework/Runtime/   framework, one asmdef per layer (BK.*)
  Core/      lifecycle, boot sequence, DI helpers, logging
  Save/      typed save slots: atomic write, .bak recovery, .corrupt-* preservation, version migration
  Options/   reactive player options (music/sfx/haptics/language) persisted through Save
  Meta/      wallet (plain/rechargeable/buff currencies), stage progress + entry gate, pending rewards, continue ladder, stage preloader registry, weekly step offers, daily rewards, win streak
  Notifications/ local push: LocalPushService (clear-and-reschedule on background), IPushProvider, daily retention fan-out, Unity Mobile Notifications backend behind INotificationPlatform
  Assets/    Addressables abstraction, scope-bound asset lifetime
  Scene/     scene scopes tied to DI + asset scopes
  UI/        layered view stack, popup dim (IPopupDim), screen cover + ISceneFlow, message/toast (IMessageService), BackInputDriver
  Data/      table pipeline
  Localization/
  Presentation/
Assets/_Project/              game-side content; framework never depends on this
Assets/BKFramework/Editor/    BK/Framework (UI prefab generator), BK/Data (table importer), BK/Tools (asset dependency finder, nested prefab finder, component replace, prefab cleanup)
tools/tables/                 xlsx -> JSON + C# table exporter (python -I tools/tables/export_tables.py)
```

## Rules

- 메타 루프는 `BK.Meta.MetaInstaller.Install(builder, currencies, continues, entryPolicy)`로 게임 스코프에 설치한다(카탈로그는 게임이 공급). 하트 차감은 `IStageProgress.TryStart` 한 곳, 환불은 `Clear`. 시간은 `IClock`만 쓴다.
- 로컬 푸시: 공급자는 `IPushProvider`로 등록하고 요청은 백그라운드 진입 시에만 예약된다(포그라운드 복귀 시 전부 취소). 권한은 게임이 `LocalPushService.MarkPendingPermissionRequest()`로 표시한 뒤 로비에서 한 번만 묻는다. 실기기 확인 전까지 플랫폼 코드는 미검증.
- Framework layers depend downward only: `Core` ← `Save` ← `Options`, `Core` ← `Save` ← `Notifications` ← `Meta`, and `Core` ← `Assets` ← `Scene`/`Data` ← `UI`/`Localization` (`UI` may use `Scene`).
  Never introduce an upward or sideways reference between `BK.*` assemblies.
- Back/Escape는 `BackInputDriver` 한 곳에서만 읽는다. 뷰는 `IUIView.OnBackRequested`로 소비하고, 게임 코드는 `Input.GetKeyDown(KeyCode.Escape)`를 직접 폴링하지 않는다.
- 팝업은 `UIViewBase`의 Dim 레벨(인스펙터)로 딤을 자동 취득한다. 여러 팝업을 잇는 플로우는 `IPopupDim.HoldForTransition()`. 씬 전환은 `ISceneFlow.TransitionAsync`(커버→닫기→전환→리빌; 씬 쪽은 `IRevealReady` 등록으로 리빌 시점 지정). 메시지/토스트 프리팹은 `BK > Framework > Generate UI Prefabs`로 재생성한다(`Assets/BKFramework/Content/UI`, Addressables 그룹 `BK Framework`, 주소는 FrameworkSettings).
- 테이블: xlsx(헤더 문법은 `tools/tables/README.md`) → `python -I tools/tables/export_tables.py` → `BK > Data > Import Tables`. 생성된 `<Sheet>Row`/`<Sheet>Table`은 손으로 고치지 않는다.
- 테스트 관례: 프레임워크 테스트는 `Assets/BKFramework/Tests/{EditMode,PlayMode}` (asmdef는 `overrideReferences: true` + `precompiledReferences: nunit.framework.dll, R3.dll`). 에러 로그를 내는 경로는 `LogAssert.Expect`, 시간은 `FakeClock`, 세이브는 임시 폴더. 규약은 `ArchitectureConventionTests`가 소스를 grep해 지킨다(Escape 폴링 금지, `DateTime.Now` 금지, asmdef 하향 참조).
- 세이브는 `ISaveService.Get<T>()`로 슬롯을 받고 변경 후 `MarkDirty()`. 파일은 `persistentDataPath/Save/<Type>.json`. 암호화는 아직 없다.
- Asset and view lifetime is owned by scopes, never by callers. If you find yourself
  pairing a manual load with a manual release, use a scope instead.
- MCP: Unity is driven over `UnityMCP` (HTTP, `127.0.0.1:8080`). Prefer verifying package
  installs on disk under `Library/PackageCache/` — async job status does not survive
  Unity's domain reload and will report `running` forever.
  When checking for compile errors, read the console with `types: ["all"]` — filtering on
  `"error"` alone returned nothing for a batch of ~50 package compile errors that `"all"` showed.
- Animation Sequencer는 `DOTWEEN_ENABLED;TMP_ENABLED` define이 있어야 컴파일된다. DOTween은 `Assets/Plugins/Demigiant/DOTween`에 벤더링(출처 `docs/animation-sequencer.json`). 코드에서 트윈이 필요하면 LitMotion, 디자이너가 프리팹에서 조립하는 연출은 Animation Sequencer.
- Unity 배치모드로 검증한다(MCP 불통 시에도 가능, 에디터가 열려 있으면 실패): 컴파일 `Unity.exe -batchmode -nographics -quit -projectPath D:\UnityTemplate -logFile <log>`, 테스트 `-runTests -testPlatform EditMode -testResults <xml>` (`-quit` 없이).
- If a token usage limit interrupts work mid-task, resume that task where it stopped once the
  limit resets. Do not restart from scratch or drop the remaining scope; re-read the files
  touched so far and continue from the last unfinished step.

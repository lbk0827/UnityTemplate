# G1: Bounce를 BK.Save / BK.Meta / BK.Options 위로 이관 + 하트·클리어·컨티뉴 루프

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bounce의 자체 저장소(`LocalSaveStore`/`PlayerProgress`)와 KitApp 내부 경제 로직을 제거하고, 지금까지 만든 프레임워크 서비스(`ISaveService`, `IWallet`, `IStageProgress`, `IOptionsService`, `PendingRewardQueue`/`CurrencyDisplayLock`, `ContinueOffers`, `IMessageService`)로 돌아가게 한다. 그 위에 sf 규칙의 하트 입장 게이트·클리어 환불·실패 시 컨티뉴를 붙인다. 로비 화면 재구성(홈 stage_slot 유지, 상점·데일리·스텝 오퍼·윈 스트릭 UI)은 G2.

**Architecture:** `KitApp`은 그대로 "게임 허브" MonoBehaviour로 남되, 상태를 소유하지 않고 서비스를 감싸는 파사드가 된다. 뷰(ImportedSceneView, StagePathView, BoosterShopView, LobbyOffersView, KitDialog, KitAudioChannel, KitHaptics)는 `KitApp.Instance`를 계속 쓰므로 호출면을 최대한 유지하고, `Progress.*` 필드 접근만 파사드 프로퍼티로 바꾼다. 저장은 "변경 후 즉시 Flush"로 테스트의 동기 저장 계약을 유지한다. 저장 실패 시 롤백(이전 KitApp 동작)은 버리고 프레임워크 정책(더티 유지, 재시도)으로 통일한다.

**결정 사항:**
- 재화 카탈로그(Bounce): `Gold`(Plain, 0), `Heart`(Rechargeable 5/5, 1개/30분, 버프 `InfiniteHeart`), `InfiniteHeart`(Buff), `Missile`/`ExtraBall`/`Bomb`/`Laser`(Plain, 0). 부스터 id = `BoosterKind.ToString()`.
- 입장 정책: `StageEntryPolicy("Heart", freeUntilStage: 3)`. 1~3스테이지는 무료.
- 컨티뉴 오퍼(Bounce 골드 스케일에 맞춤): MovesZero 기본 150, +150, 최대 600, `AddMoves = 5`(공 5개). 클리어 보상은 50 그대로.
- 오퍼 진행(엔드리스 오퍼/기프트 스텝)은 G1에서는 `BounceOfferData : SaveData`에 종류별 step만 저장하고 `OfferClaim`이 지갑에 지급한다. 주간 `StepOffers` 전환은 G2(아이콘이 주차별 1개만 뜨는 sf 동작과 함께).
- 플레이어 이름: `BounceProfileData : SaveData { playerName }`.
- 테스트 저장 폴더: `BK_KIT_TEST_SAVE_DIR` 유지. `AppLifetimeScope`에 `protected virtual string SaveDirectory`를 두고 `IntegratedProjectScope`가 env를 반영.
- `TrySetSandboxProgress`는 유지(에디터/개발 빌드). `StageProgress`에 `SetCurrentStage(int)`를 추가(도구·테스트용, 문서화).
- `soundEnabled` 마스터 토글은 제거. 설정 팝업의 "sound/music" 버튼은 Music 토글로 매핑.

---

### Task 1: 프레임워크 보강 (작은 변경)

**Files:**
- `AppLifetimeScope.cs`: `protected virtual string SaveDirectory => SaveService.DefaultDirectory;` 추가, `ISaveService` 등록이 이를 사용.
- `StageProgress.cs` / `IStageProgress.cs`: `void SetCurrentStage(int stage)` 추가(1 이상, attempts 0, 이벤트 없음, MarkDirty). 주석: 디버그/도구 전용.
- `Wallet.cs`: 변경 없음. (`Set`은 이미 있음.)
- 테스트: `StageProgressTests`에 `SetCurrentStageIsForToolsOnly` 1개.

- [x] 구현 → csc → 커밋 `feat(core): allow projects to override the save directory; add StageProgress.SetCurrentStage`

---

### Task 2: Bounce 카탈로그와 스코프 배선

**Files:**
- Create: `Assets/_Project/Bounce/Runtime/BounceCatalogs.cs`
  - `BounceCurrencies : ICurrencyCatalog`, `BounceContinueOffers : IContinueOfferCatalog`, `BounceEntryPolicy` 상수, `BounceSaveData.cs`(`BounceProfileData`, `BounceOfferData { int endlessOfferStep, endlessGiftStep; Step/SetStep }`).
- Modify: `IntegratedProjectScope.cs`
  - `SaveDirectory` 오버라이드: `BK_KIT_TEST_SAVE_DIR` 있으면 그 폴더, 없으면 `persistentDataPath/Bounce/Profiles/local`.
  - `ConfigureProject`: `MetaInstaller.Install(builder, new BounceCurrencies(), new BounceContinueOffers(), new StageEntryPolicy("Heart", 3))`, 엔트리포인트 유지.
  - `IntegratedGameFlow` 생성자에 `ISaveService, IWallet, IStageProgress, IOptionsService, IMessageService, PendingRewardQueue, CurrencyDisplayLock, ContinueOffers, IClock` 추가해 `KitApp.Initialize(...)`로 전달.
- Modify: `BK.Kit.Runtime.asmdef` references에 `BK.Save`, `BK.Meta`, `BK.Options` 추가. 테스트/에디터 asmdef도 동일.

- [x] 구현 → csc → 커밋 `feat(bounce): register Bounce currency/continue catalogs and framework services`

---

### Task 3: KitApp 파사드 재작성

**Files:**
- Rewrite: `Assets/_Project/Bounce/Runtime/KitApp.cs`
- Delete: `Assets/_Project/Bounce/Runtime/LocalSaveStore.cs` (+ meta)

유지하는 공개 멤버(호출면): `Instance`, `Shop`, `Session`, `IsLoading`, `ClearGoldReward`, `LastGoldReward`, `HasPendingSave`, `Changed`, `ConsumeStageAdvance`, `ConfigureScenes`, `UsesFrameworkServices`, `Play`, `GoToLobby`, `Complete`, `SetMusic/SetEffects/SetHaptics`, `ToggleSound`(→Music), `TrySetPlayerName`, `TrySetSandboxProgress`, `RetryPendingSave`, `TryBuyBooster`, `TryClaimOfferStep`, `TryConsumeBooster`.

새 멤버(기존 `Progress.*` 대체):

```csharp
public int UnlockedLevel => progress.CurrentStage.CurrentValue;
public long Gold => wallet.ValueOf("Gold");
public int BoosterCount(BoosterKind kind) => (int)Math.Min(int.MaxValue, wallet.ValueOf(kind.ToString()));
public int OfferStep(OfferKind kind) => offers.Step(kind);
public string PlayerName => profile.playerName;
public bool MusicEnabled => options.Music.Value;  public bool EffectsEnabled => options.Sfx.Value;  public bool HapticsEnabled => options.Haptics.Value;
public long Hearts => wallet.ValueOf("Heart");  public bool HeartsInfinite => wallet.IsInfinite("Heart");  public TimeSpan TimeToNextHeart => wallet.TimeToNext("Heart");
public bool CanEnter(int level) => progress.CanEnter(level);
public IWallet Wallet, IStageProgress Progress /*이름 충돌 주의: 기존 Progress(PlayerProgress)는 제거*/, PendingRewardQueue Rewards, CurrencyDisplayLock DisplayLock, ContinueOffers Continues, IMessageService Messages;
```

핵심 동작:
- `Play(level)`: `level = Clamp(level, 1, UnlockedLevel)`; `if(!progress.TryStart(level)) { Messages.ShowAsync("No hearts", $"Next heart in {Format(TimeToNextHeart)}", "OK").Forget(); return; }` 그 다음 기존 로드 절차. (하트는 여기서 1회 차감.)
- `Complete(won)`: `Session.Finish(won)` 성공 시, 승리면 `LastGoldReward = 50`; `long from = Gold; wallet.Add("Gold", 50, "stage_clear"); DisplayLock.Engage("Gold", from); Rewards.Enqueue(new PendingReward("Gold", 50, from, from+50, alreadyCredited: true));` 그리고 `if(Session.Level == UnlockedLevel) pendingStageAdvance = Session.Level; progress.Clear(Session.Level);`(포인터 전진 + 환불). 패배면 `LastGoldReward = 0`, `Continues`는 뷰가 사용. `Flush()`.
- `ReleaseRewardDisplay()`: `DisplayLock.Release("Gold")` + 큐 비움. ClearRewardView 완료 콜백에서 호출.
- `TryBuyBooster`: `Session.State==Lobby` 검사, `wallet.CanAfford("Gold", price)` → `wallet.Add("Gold", -price)`, `wallet.Add(kind, +1)`, `Flush()`.
- `TryConsumeBooster`: Playing 검사, 수량 > 0 → `wallet.Add(kind, -1)`, `Flush()`.
- `TryClaimOfferStep`: Lobby 검사 → `OfferClaim.Apply(offers, wallet, kind, step, out message)` → `Flush()`.
- `TryContinue(out message)`: `Continues.TryGetOffer(StageFailReason.MovesZero, out offer)` + `TryPurchase` → true면 `Session`을 다시 Playing으로 되돌리고 `game.Continue(offer.Definition.AddMoves)` 호출(게임 모듈 API는 Task 5).
- `Flush()`: `saves.Flush()`; 실패하면 `HasPendingSave = true`, `LastSaveError`, 로그 `"BK_Kit: progress could not be saved"`(테스트 계약). `RetryPendingSave()` = Flush 재시도. 2초 주기/일시정지/종료 재시도 유지.
- `TrySetSandboxProgress(level, gold, boosters)`: `progress.SetCurrentStage(level)`, `wallet.Set("Gold", gold)`, 부스터 4종 `Set`, `Flush()`.
- `OfferClaim`: `PlayerProgress` 대신 `(BounceOfferData, IWallet)`를 받도록 수정. 골드 포화는 long이므로 제거.
- `LocalOfferSource.CurrentStep` → `app.OfferStep(kind)`.

- [x] 구현 → csc(BK.Kit.Runtime) → 커밋 `refactor(bounce): make KitApp a facade over wallet, stage progress, options and save`

---

### Task 4: 뷰 갱신

**Files:** `ImportedSceneView.cs`, `StagePathView.cs`, `BoosterShopView.cs`, `LobbyOffersView.cs`, `KitDialog.cs`, `KitAudioChannel.cs`, `KitHaptics.cs`, `PlayerSmokeCheck.cs`, `Editor/KitWorkbench.cs`

- `app.Progress.unlockedLevel` → `app.UnlockedLevel`, `app.Progress.gold` → `app.Gold`, `Count(kind)` → `app.BoosterCount(kind)`, `playerName` → `app.PlayerName`, `*Enabled` → `app.MusicEnabled` 등.
- HUD 골드 텍스트: `app.DisplayLock.GetDisplayValueOr("Gold", app.Gold)`로 읽는다(코인 도착 전까지 옛 값).
- HUD 하트(`UIHUDSub_Top`/`heart`): `currencyCountText` = `HeartsInfinite ? "∞" : Hearts`, `rechargeTimeText` = 가득 차면 숨기고 아니면 `mm:ss`(0.5초 갱신 코루틴), `fullText` = 가득 찼을 때 "Full". 버튼은 하트 정보 메시지(`Messages.ShowAsync`).
- `UIHUDSub_Currency`(인게임 HUD)도 같은 규칙.
- 클리어 팝업: `ClearRewardView.Initialize(..., app.Gold, app.LastGoldReward, () => { app.ReleaseRewardDisplay(); app.GoToLobby(); })`. 잔액 텍스트 초기값은 `DisplayLock` 값.
- 실패 팝업: `continuePriceText`에 컨티뉴 가격(`Continues.TryGetOffer`), `playOnLabels.0` 표시("+5"), `replayButton`(원래 Retry)은 `app.TryContinue`로, 잔액 부족이면 토스트. 다시하기(재시도)는 `lobbyButton`/`closeButton` 옆의 기존 버튼 매핑을 유지하되 `Play(Session.Level)`은 하트를 다시 차감한다(sf 규칙).
- `KitAudioChannel`: `app.Changed` 대신 `options.Music/Sfx` 구독(R3). `KitHaptics`: `app.HapticsEnabled`.
- `PlayerSmokeCheck`: `Progress.*` → 파사드; `new LocalSaveStore(folder).Load().gold` → `new SaveService(folder).Get<WalletData>()` 값.

- [x] 구현 → csc(BK.Kit.Runtime, BK.Kit.Editor) → 커밋 `refactor(bounce): read progress through the KitApp facade; show hearts and reward display lock in the HUD`

---

### Task 5: BounceModule 컨티뉴 훅

**Files:** `BounceModule.cs`, `GameModule.cs`

- `GameModule`에 `public abstract bool CanContinue { get; }`, `public abstract void Continue(int extraBalls);` 추가.
- `BounceModule.Resolve`: 패배 조건(공 0, 블록 남음)에서 즉시 `Report(false)` 하지 않고 `failed` 상태로 두고 `completed(false)`를 호출하되, `KitApp.Complete(false)`는 Session을 Lost로 만든다. 컨티뉴 시 `KitApp.TryContinue` → `Session.Resume()`(새 메서드: Lost → Playing) → `game.Continue(5)` → `BallsRemaining += 5; playing = true`. 즉 "Report 후 재개" 모델. 이미 `End()`가 호출되지 않았음을 보장(실패 팝업 표시 중 모듈은 살아 있음).
- `GameSession.Resume()` 추가: `State == Lost`일 때만 Playing으로.

- [x] 구현 → csc → 커밋 `feat(bounce): allow continuing a lost round with extra balls`

---

### Task 6: 테스트 재작성

**Files:**
- Rewrite: `Tests/EditMode/ProgressTests.cs` → `BounceSaveTests.cs`: 파사드 없이 `SaveService` + `Wallet` + `StageProgress`로 Bounce 카탈로그 동작 확인(초기값, 부스터 구매/소비는 KitApp 없이 지갑 수준), `GameSession` 테스트 유지(Resume 포함).
- Rewrite: `Tests/EditMode/OfferTests.cs`: `OfferClaim.Apply(BounceOfferData, Wallet, ...)` 기준.
- Modify: PlayMode 두 파일의 저장 검증을 `Saved(folder)` 헬퍼로 교체: `new SaveService(folder).Get<WalletData>().Find("Gold").value`, `Get<StageProgressData>().currentStage`, 부스터 수량, 옵션은 `Get<OptionsData>()`, 이름은 `Get<BounceProfileData>()`. 저장 차단 테스트는 `WalletData.json.tmp` 디렉터리 생성으로 바꾸고, 롤백 단언은 "메모리 값은 바뀌고 HasPendingSave=true, 파일은 옛 값" 으로 바꾼다. 시드는 `SaveService`로 슬롯을 써서 Flush.
- 하트 테스트 추가(PlayMode, VisualFlowTests에 1개): 4스테이지부터 입장 시 하트 5→4, 클리어 시 5로 환불, 하트 0으로 시드하면 Play가 막히고 메시지 팝업이 뜬다.
- 컨티뉴 테스트 추가: 공 0으로 패배 → 실패 팝업 가격 150 → 골드 1000 시드 상태에서 컨티뉴 → 공 +5, 골드 850, 다시 패배 → 가격 300.

- [x] 구현 → 에디터 닫힌 뒤 EditMode/PlayMode 전체 실행 → 커밋 `test(bounce): cover the framework-backed facade, hearts and continue`

---

### Task 7: 문서

- CLAUDE.md의 Bounce 섹션: "KitApp은 BK.Save/BK.Meta/BK.Options 파사드. 저장 파일은 `persistentDataPath/Bounce/Profiles/local/<Type>.json`(테스트는 `BK_KIT_TEST_SAVE_DIR`). 하트 1~3 무료, 이후 1개 차감·클리어 환불. 컨티뉴 150/300/450/600 골드에 공 5개."
- 로드맵 G1 완료, G2 범위 명시.

- [x] 커밋 `docs: record the Bounce migration`

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

## Integrated Bounce project (2026-10-08)

- Active game: `Assets/_Project/Bounce/`, imported from BK_Kit and adapted to BKFramework.
- Open `BK > Integration > Open Bounce Bootstrap`, then Play. Entry scene: `Assets/_Project/Bounce/Scenes/VisualBootstrap.unity`.
- `IntegratedProjectScope` boots BKFramework and installs the Bounce meta loop (`MetaInstaller` with `BounceCurrencies`, `BounceContinueOffers`, `BounceEntry.Policy`). `KitApp` is a facade over `IWallet`, `IStageProgress`, `IOptionsService`, `ISaveService`, `PendingRewardQueue`/`CurrencyDisplayLock`, `ContinueOffers` and `IMessageService`; it owns no state and flushes the save after every mutation. Do not start the sample `ProjectScope` alongside it.
- Bounce save: `persistentDataPath/Bounce/Profiles/local/<Type>.json` (`WalletData`, `StageProgressData`, `OptionsData`, `BounceProfileData`, `BounceOfferData`); tests set `BK_KIT_TEST_SAVE_DIR` and read/seed through `KitTestSaves`. Hearts: stages 1-3 free, then one heart per entry (`KitApp.Play` → `IStageProgress.TryStart`), refunded on clear. Continue on loss: 150/300/450/600 gold for +5 balls (`KitApp.TryContinue`). A failed write keeps the data dirty and retries (no rollback).
- Keep the existing `BKFramework` independent of the game module. Imported presentation remains game content, with its existing provenance.
- Active Bounce fonts: Lilita One (Latin) with Jua (Korean fallback), SIL OFL 1.1. Preserve each original TTF and `Resources/FontLicenses/<family>/OFL.txt` under `Assets/_Project/ThirdParty/`. Resources packages the full copyright/license notices with players; Settings > Font Licenses displays both offline. Rebuild atlases with `BK > Integration > Rebuild OFL Fonts`. Historical Kenney CC0 source/provenance is retained; do not reimport DUG fonts.
- Bounce shop data lives in `Assets/_Project/Bounce/Content/Tables/`. Edit the JSON sources and run `BK > Integration > Import Shop Tables`. The two game tables load through BK.Data at boot; the original Project sample tables remain separate.
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
- Framework layers depend downward only: `Core` ← `Save` ← `Options`/`Meta`, and `Core` ← `Assets` ← `Scene`/`Data` ← `UI`/`Localization` (`UI` may use `Scene`).
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

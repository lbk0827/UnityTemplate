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
- `IntegratedProjectScope` boots BKFramework; `KitApp` owns Bounce progress/save state and delegates scenes/UI to `ISceneService`/`IUIService`. Do not start the sample `ProjectScope` alongside it.
- Keep the existing `BKFramework` independent of the game module. Imported presentation remains game content, with its existing provenance.
- DUG-named fonts were replaced with Kenney Future Narrow (CC0), including TMP atlases. Preserve `Assets/_Project/ThirdParty/KenneyFonts/License.txt` and `source.json`; do not reimport the old fonts from the historical handover.
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
| Tween / sequencing | LitMotion |
| String building | ZString |

## Layout

```
Assets/BKFramework/Runtime/   framework, one asmdef per layer (BK.*)
  Core/      lifecycle, boot sequence, DI helpers, logging
  Assets/    Addressables abstraction, scope-bound asset lifetime
  Scene/     scene scopes tied to DI + asset scopes
  UI/        layered view stack, per-view scopes
  Data/      table pipeline
  Localization/
  Presentation/
Assets/_Project/              game-side content; framework never depends on this
```

## Rules

- Framework layers depend downward only: `Core` ← `Assets` ← `Scene`/`Data` ← `UI`/`Localization`.
  Never introduce an upward or sideways reference between `BK.*` assemblies.
- Asset and view lifetime is owned by scopes, never by callers. If you find yourself
  pairing a manual load with a manual release, use a scope instead.
- MCP: Unity is driven over `UnityMCP` (HTTP, `127.0.0.1:8080`). Prefer verifying package
  installs on disk under `Library/PackageCache/` — async job status does not survive
  Unity's domain reload and will report `running` forever.
  When checking for compile errors, read the console with `types: ["all"]` — filtering on
  `"error"` alone returned nothing for a batch of ~50 package compile errors that `"all"` showed.
- If a token usage limit interrupts work mid-task, resume that task where it stopped once the
  limit resets. Do not restart from scratch or drop the remaining scope; re-read the files
  touched so far and continue from the last unfinished step.

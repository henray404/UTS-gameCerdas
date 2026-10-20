# Campaign verification

## Pure campaign rules

Run from project root on Linux/macOS with Unity Mono installed:

```bash
Tests/run-campaign-rules.sh
# Or choose editor explicitly:
Tests/run-campaign-rules.sh /path/to/Editor/Unity
```

This compiles the real `CampaignLevels.cs` with the test executable. No Unity process, external test package, or game save is needed. Expected result: `PASS: 123 campaign assertions`.

## Unity integration

Create a disposable project copy at `/tmp/sneakhouse-level-validation` containing `Assets`, `Packages`, and `ProjectSettings`. In **the copy only**, set `companyName` to `SneakHouse Validation` and `productName` to `Campaign Validation` in `ProjectSettings/ProjectSettings.asset`. These exact isolation checks prevent deleting original campaign records.

```bash
/path/to/Editor/Unity -batchmode -nographics \
  -projectPath /tmp/sneakhouse-level-validation \
  -executeMethod SneakHouseEditor.CampaignValidation.Run \
  -logFile /tmp/sneakhouse-campaign-validation.log
```

Do not pass `-quit`: the runner enters Play Mode, survives reload, runs all six levels, and exits with 0 only after all assertions pass. Timeout or game runtime errors return 1. The helper rejects interactive editor invocation. It resets only campaign/score keys under the validation product identity.

On Windows, use an isolated folder under `%TEMP%` and update the path check **in the copy only** so it points at that folder; keep the source check unchanged.

## Verified run — 20 October 2026

- Unity 6000.6.0f1, Windows, isolated copy, batch mode, re-run after the difficulty-curve and target-highlight changes.
- 123 pure-rule assertions and 434 Play Mode assertions passed.
- Guard parameters now also verify that `SuspicionRate`, `ChaseSpeed`, `HearingSuspicion`, and `LoseSightSeconds` reach every guard.
- Physical interactions at validated target approaches and extraction succeeded on all six maps.
- Failure, replay, locked launch, sequential unlock, target activation, guard parameters, cans, save/reload, and distinct per-level records checked.
- Guards were disabled after roster/parameter assertions to isolate progression. Teleporting to target approaches tests interaction and NavMesh validation, not manual route completion or AI difficulty.
- Earlier GameView inspection covered the menus and level selection, including a locked level's details and disabled Play button: [level-selection.png](screenshots/level-selection.png). The new 3D target highlight has not yet been visually inspected, and human balance testing remains pending.
- The source project remains Unity 6000.2.10f1. That exact editor is not installed on this machine, so execution on it is still unverified.
- Unity 6000.6 emitted an editor-only `UnityEditor.Search.SearchDatabase` / `SearchInit` startup exception. The runner explicitly excludes that stack from game failures, logs its exclusion, and fails on other Play Mode errors. Existing API deprecation warnings were retained.

Measured walkable areas and complete balancing calculations are in [the level report](../LAPORAN-SISTEM-LEVEL.md).

## Level-card menu — 21 October 2026

- Unity6000.6.0f1 on Linux, isolated copy; current working-tree editor upgrade preserved.
- 123 pure-rule assertions and434 Play Mode assertions passed after the UI replacement.
- Six actual seeded map screenshots rendered at768x576 and loaded as runtime Resources. Upper floor/stairs visible in Loft preview. No image-generation service or new package used.
- Inspected GameView at1440x900 and960x540: all six cards fit, map images and guard counts visible, long mission names wrap, lock icons replace status words, and locked Play remains disabled.
- Fresh profile: [level-cards.png](screenshots/level-cards.png). Selected locked level: [level-cards-locked.png](screenshots/level-cards-locked.png). Earlier screenshots retained.
- The visual capture used private menu selection solely to inspect locked-card presentation; its launch was also rejected through GameSession.StartLevel. Full campaign regression checks the underlying unlock/save rules. Manual mouse/controller testing and human balance testing remain pending.

To regenerate previews, prepare the isolated copy described above, then run Unity with graphics enabled:

```bash
/path/to/Editor/Unity -batchmode -force-glcore \
  -projectPath /tmp/sneakhouse-level-validation \
  -executeMethod SneakHouseEditor.LevelMapPreviewBuilder.Run \
  -levelPreviewOutput /absolute/project/Assets/SneakHouse/Resources/SneakHouseUI/Levels \
  -logFile /tmp/sneakhouse-preview.log
```

The builder requires the isolated project identity, touches only that identity's progress, renders six fixed filenames under the explicit output directory, and exits itself. Existing texture import metadata is retained when regenerating PNGs.

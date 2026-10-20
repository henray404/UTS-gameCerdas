# Campaign verification

## Pure campaign rules

Run from project root on Linux/macOS with Unity Mono installed:

```bash
Tests/run-campaign-rules.sh
# Or choose editor explicitly:
Tests/run-campaign-rules.sh /path/to/Editor/Unity
```

This compiles the real `CampaignLevels.cs`, `ScoreRules.cs`, and `ScoreData.cs` into two test executables. No Unity process, external test package, or game save is needed. Expected result:

```
PASS: 123 campaign assertions
PASS: 55 score assertions
```

The score tests cover the formula bounds, top-five score and speed boards, tie-breaks, legacy migration (legacy entries never enter speed boards), corrupt-data normalization, profile name fallback, and score/speed totals.

`.gitattributes` keeps `*.sh` files LF so Windows Git Bash can run the script after a fresh checkout. An existing checkout may need `git rm --cached -r . && git reset --hard` (or re-cloning) to pick up the line-ending change.

## Unity integration

Create a disposable project copy at `/tmp/sneakhouse-level-validation` containing `Assets`, `Packages`, and `ProjectSettings`. In **the copy only**, set `companyName` to `SneakHouse Validation` and `productName` to `Campaign Validation` in `ProjectSettings/ProjectSettings.asset`. These exact isolation checks prevent deleting original campaign records.

```bash
/path/to/Editor/Unity -batchmode -nographics \
  -projectPath /tmp/sneakhouse-level-validation \
  -executeMethod SneakHouseEditor.CampaignValidation.Run \
  -logFile /tmp/sneakhouse-campaign-validation.log
```

Do not pass `-quit`: the runner enters Play Mode, survives reload, runs all six levels, and exits with 0 only after all assertions pass. Timeout or game runtime errors return 1. The helper rejects interactive editor invocation. It resets only campaign/score keys (`SneakHouse.Scores.v2` plus legacy per-level keys) under the validation product identity.

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

## Scoring / leaderboard update — 20 October 2026

- Pure rules: 123 campaign and 55 score assertions pass (Mono, Linux).
- Runtime scripts compiled against UnityEngine 2021.3 reference assemblies with a local shim for the Unity 6 `Rigidbody.linearVelocity` rename; `CampaignValidation.cs` compiled against a minimal `UnityEditor` stub. This proves the C# is consistent, not that it runs in Unity.
- `CampaignValidation` now reads the v2 store, checks that loss writes no record, that replays join the level leaderboard, and that each win stores a speed record. This updated harness has **not yet been run** in Unity.
- Main menu, Hall of Records, result breakdown, and level-detail layouts were checked only with offline mockups rendered from the game's TTF fonts at 1440x900 (no text overflow found). GameView inspection is still pending.

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

## Missing-preview recovery — 21 October 2026

- Recaptured all six actual maps at768x576, preserving their existing texture GUIDs and import settings.
- Existing PNGs were already tracked and valid. That run reproduced a separate null-cache recovery weakness; the user's later Console evidence identified the actual import blocker: a malformed folder GUID, covered below.
- RED: clearing six menu cache references while keeping the real Resources assets loaded failed with `Open must reload missing preview for level 1`.
- GREEN:161 assertions passed in `SneakHouseEditor.MapPreviewValidation.Run`, using isolated Unity6000.6.0f1 with graphics enabled. Real resources, repeated opening, a single missing slot, return/reopen, and locked launch rejection checked.
- The timed stage cleared all6cached references while the menu stayed open, then drove actual GameView repaint/player-loop updates. All six recovered after1.5real seconds at `Time.timeScale=0`, without calling Open or refresh from the test.
- Runtime now retries missing entries immediately when the menu opens and once per real second while displayed. Valid cached textures stay shared and unchanged.
- Fresh captures at1440x900 and960x540 were inspected with all six recaptured maps visible. Snapshot: [level-previews-recovered.png](screenshots/level-previews-recovered.png).

Run the regression in the isolated copy configured above:

```bash
/path/to/Editor/Unity -batchmode -force-glcore \
  -projectPath /tmp/sneakhouse-level-validation \
  -executeMethod SneakHouseEditor.MapPreviewValidation.Run \
  -logFile /tmp/sneakhouse-map-preview-green.log
```

## Actual import blocker: malformed folder metadata — 21 October 2026

- `Assets/SneakHouse/Resources/SneakHouseUI/Levels.meta` contained a33-character GUID. Unity reported that metadata invalid and ignored its corresponding asset. Restarting the editor could not repair that source file.
- The earlier validation clone already contained a different, valid Unity-generated folder GUID. Consequently its successful Resources tests did not validate the malformed source folder metadata.
- Replaced only the invalid folder GUID with the valid Unity-generated32-character ID and canonical DefaultImporter fields. The six texture GUIDs and import settings were preserved.
- Added `Tests/check-unity-metadata.sh`: RED flagged exactly the malformed folder among247metadata files. GREEN verified all247GUIDs are well-formed and unique. Separate temporary fixtures checked valid,33-character, duplicate, missing and empty metadata inventories.
- Preserved the previous validation clone as a temporary backup. Imported a new clone from exact corrected source files with no Library cache:161preview assertions passed, no invalid-GUID errors occurred, and the folder retained the exact source GUID after import.

Run this preflight **before** creating/opening a validation clone so editor repair cannot hide malformed source metadata:

```bash
Tests/check-unity-metadata.sh
```

The user subsequently reported that the original editor still showed missing previews after refreshing and reopening. The clean-clone result does not establish recovery in that editor.

## Original-editor import diagnosis — 21 October 2026

- The original project's report confirmed six valid TextureImporter assets: all six direct `AssetDatabase.LoadAssetAtPath<Texture2D>` calls succeeded, while all six `Resources.Load<Texture2D>` calls failed before and after targeted synchronous reimport. This narrows the failure to the original editor's Resources lookup; the underlying cache/index cause remains unproven.
- Added `Sneak House > Repair Map Previews`. It inspects direct AssetDatabase loads and Resources loads before and after a synchronous reimport of only the preview folder and six PNGs. It writes `Logs/MapPreviewImportReport.json`, without deleting caches, saving scenes, resetting progress, or closing the editor.
- The helper runs once after its scripts load in an interactive editor session. Import workers skip it. The menu allows a subsequent manual run.
- The helper compiled and reported six successful loads before/after import in the isolated validation copy. This validates the helper, not the unresolved original-editor failure.
- Added `LevelSelectionUI.LoadPreview`: retain Resources loading for players and healthy editors, with an `#if UNITY_EDITOR` direct AssetDatabase fallback when Resources returns null. The fallback returns the same shared imported PNG assets; no file decoding or duplicate textures are introduced.
- The v2 report records `menuLoaded` separately from `resourcesLoaded`; a menu recovery PASS does not claim the editor's Resources lookup recovered. The screenshot callback waits up to180 seconds for the user to open level selection without changing gameplay state.
- Updated isolated regression:167 assertions passed, including six checks that the loader preserves valid Resources assets, stale-menu recovery, timed Game View recovery, and locked-level rejection. Original-editor recovery with the new loader still requires its v2 report and menu inspection.
- Built a Linux Development player from the isolated project and ran a temporary player-only probe. All six previews loaded through `LevelSelectionUI.LoadPreview` as768x576 textures, then the probe exited with code0. `UnityEditor` is excluded in this build, so it verifies actual packaged Resources rather than editor fallback. The probe/build harness and player are temporary files under `/tmp/sneakhouse-level-validation` and `/tmp/sneakhouse-preview-player`; they are not part of the source project. Logs: `/tmp/sneakhouse-preview-player-build.log` and `/tmp/sneakhouse-preview-player-runtime.log`.

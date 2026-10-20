# Level menu cards

- Scope: six left-to-right level cards; real map preview for every level; play/lock/check status icons; guard count on every card.
- Keep existing wood/paper palette, pixel fonts, sequential unlock and level balance.
- Runtime ownership: `LevelSelectionUI.cs`, delegation from `MissionFlowUI.cs`.
- Assets: six rendered seeded maps in `Resources/SneakHouseUI/Levels`, generated in an isolated Unity copy.
- Preserve existing user edits to model imports, packages and Unity settings.
- Verify: Unity compile, real rendered menu at desktop/smaller aspect, locked-card inspection/launch rejection, existing campaign regression tests.
- Review: C# and general code review before completion. No commit/push requested.

## Result — 21 October 2026

- Six card strip, play/lock/check symbols, actual seeded map previews and guard counts implemented.
- Preview generator rendered all six maps at768x576; Loft upper office/stairs included.
- 123 rule assertions and434 Play Mode assertions passed in isolated Unity6000.6.0f1 on Linux.
- GameView captures inspected at1440x900 and960x540, including disabled Play on a selected locked card.
- C# and general reviews: no unresolved findings. Owned GUI textures disposed on scene reload; generator rejects interactive editor before any exit/prefs mutation.
- Existing model-import/package/project-settings changes preserved. No Git commit or push performed.

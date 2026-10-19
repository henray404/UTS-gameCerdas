# Exterior and living-world direction — v1

Date: 19 October 2026. Original design brief below. Implementation now applied to PrototypeHouse. Revision 2 adds a rotating pinwheel, sweeping sprinkler with traveling drops, and two flying butterflies with wing motion; vegetation and cloth sway are stronger. Actual preview: ../Verification/garden-motion-v2.gif, recorded while characters and mission simulation remain paused. 19 Blender assets integrated. 30 exterior checks and 43 gameplay checks passed. The concept image remains a proposal render, not an engine screenshot.

## Direction

Give Collector's House a maintained but inhabited residential garden. Keep warm afternoon light, faceted matte models, cream walls, olive vegetation, terracotta accents, pixel UI, and tilted top-down camera. Existing house remains the visual and gameplay focus. No additional characters, buildings, levels, objectives, or second floor in this pass.

Concept image: [exterior-garden-concept-v1.png](exterior-garden-concept-v1.png). Generated with built-in imagegen using the existing HUD screenshot only as a visual/layout reference. This image is an art-direction sketch, not engine output or a collision plan. Foliage density and framing require adjustment in Unity.

## Current evidence

PrototypeBuilder creates one 23 × 17 courtyard slab, a 16 × 12 house footprint, and four long rectangular border pieces. Exterior currently has two potted plants near the west entrance. RuntimeNavigation collects layer-8 physics colliders under the assigned geometry root, with bounds 24 × 7 × 18. New playable ground outside those bounds would need explicit navigation changes; this design does not require that expansion.

## Exterior composition

| Zone | Proposed details | Purpose |
| --- | --- | --- |
| Southwest approach | Low gate, mailbox, irregular stepping stones, porch lantern | Clear visual route to existing west entrance and extraction |
| West entrance strip | Open walking space, flower clusters at edges, low hedge segments | Frame entry without hiding player or extraction ring |
| North garden | Two small trees at distant corners, grouped flower beds, varied lawn patches | Depth, irregular silhouette, warm shadows |
| East service strip | Wooden crates, watering can, coiled hose, low bench, small hanging cloth | Signs of daily use; different identity from ornamental garden |
| South terrace | Small stone pad, two seats, round table, one planter | Foreground focal cluster; leave walking route around it |
| Outside boundary | Narrow pavement and simplified vegetation, visual backdrop only | Replace isolated board-like edge with a neighborhood context |

Do not distribute props uniformly. Alternate quiet open ground with compact detail clusters. Use subtle soil/grass color patches and inset stepping stones instead of an all-over noisy texture. Keep foreground foliage low. Avoid branches crossing target or guard silhouettes. Preserve the west approach and the three target routes.

## Motion and sound specification

All values below are starting art targets, not measured performance results.

| Element | Proposed motion | Constraint |
| --- | --- | --- |
| Tree canopy | Slow, slight rotation around branch/trunk pivot; phase varies by tree | Trunk and collision proxy remain fixed |
| Grass and flowers | Small tip sway, softer than tree motion | Only selected clusters animate; no synchronized whole-map wobble |
| Loose leaves | Sparse drift near trees | Stay away from objective markers and guard vision display |
| Birdbath | Gentle repeating water ripple | Small unlit effect; no simulated water or moving collider |
| Hanging cloth | Soft sway around upper attachment | Keep cloth out of walkways and house sightlines |
| Porch light | Steady warm emission | No flicker competing with alert feedback; daylight needs no expensive shadowed point light |
| Ambient audio | Quiet wind, occasional distant bird, faint water nearby | Separate ambient bus; never feeds GameSession.EmitNoise |

Environmental motion should be low amplitude and staggered. Keep interior and mission feedback visually louder. Offer an Ambient Motion toggle; reduced mode disables drifting leaves and lowers sway. Menu/briefing may keep gentle visual ambience running with an independent clock; explicit gameplay pause freezes it. Audio ambience stays quiet and ducks under alert cues.

## Gameplay contract

- Cosmetic foliage is not automatically stealth cover. Do not advertise hiding in shrubs until vision occlusion is explicitly implemented and tested.
- If crates or hedges occupy reachable ground, their stable colliders must match visible obstruction and be included in navigation/vision consistently.
- Animated leaves, cloth, and water have no moving physics colliders.
- Ambient sound is decorative. Any later sound that attracts guards must use a separate intentional gameplay stimulus with clear player feedback.
- Gate initially stays open. No gate puzzle or new extraction condition in this pass.
- Do not widen the camera simply to fit decorations until the player, guard expressions, target markers, and UI remain readable at the existing Game View size.

## Implementation order

1. Ground variation, stepping stones, segmented fence, gate. Review entry/extraction readability from actual camera.
2. Add north planting and east service cluster, then terrace. Keep density lower if paths or silhouettes become busy.
3. Add restrained canopy/plant/cloth motion and small water effect; then ambient audio.
4. Verify player access, all target approaches, extraction, guard routes and line of sight. Compare motion enabled/disabled. Check profiler on target machine and Console; no FPS claim until measured.

Use existing potted plant and furniture assets where suitable. New exterior-specific assets: segmented low fence/gate, mailbox, stepping-stone set, faceted tree, hedge, flower/grass clumps, crate, watering can/hose, hanging cloth and birdbath. Prefer reusable modular meshes; avoid generating separate assets for every placement.

Preserve manual scene edits. Add exterior as grouped scene objects/prefabs; do not run the destructive full-scene rebuild menu over the current scene.

## Visual references

- [Untitled Goose Game — official screenshots](https://untitled.goose.game/): reference candidate for readable domestic props and quiet village setting.
- [Animal Crossing: New Horizons — Nintendo island guide](https://play.nintendo.com/news-tips/tips-tricks/animal-crossing-new-horizons-discover-tips/): reference candidate for grouped planting and residential garden detail.

These are mood references, not asset sources. Generated concept uses the existing project's screenshot as its input reference.

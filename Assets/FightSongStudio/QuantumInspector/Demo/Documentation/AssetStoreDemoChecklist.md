# Living Laboratory — release checks

Validation environment: Unity **6000.3.7f1**, Universal Render Pipeline **17.3.0**, Windows Editor, 2026-10-13. Results apply to this environment. See `ValidationReport.json` for measured hierarchy data and `VerificationResults.json` for live checks.

- [x] Scene supplied at `Demo/Scenes/Demo.unity`; laboratory root and all seven numbered subsystems present.
- [x] 391 GameObjects; maximum depth 8; 32 parents with five or more children; seven groups with 25–50 descendants.
- [x] 307 objects have multiple non-Transform components; stress subtree contains 62 objects.
- [x] All eight named showcases present, including a real nested prefab and an intentionally empty/inactive example.
- [x] 1,196 components and 436 serialized cross-object reference entries measured, including the five explicitly saved URP light-data components.
- [x] No missing scripts, dangling serialized references, or unassigned demo object-reference fields.
- [x] Ten shared materials, one generated mesh, two prefab assets, and a local URP Volume profile included.
- [x] Five active lights; one animation Update; no ExecuteAlways animation or per-object Update loops.
- [x] Saved scene dependency audit found no assets elsewhere under `Assets/`.
- [x] Only engine primitives, generated geometry, shared materials, and Unity's built-in font used; no third-party art or downloads.
- [x] Separate runtime and Editor assemblies; demo does not depend on Quantum Inspector's implementation assembly.
- [x] Generator ownership check, overwrite confirmation, single prior-scene backup, and scoped asset saving implemented.
- [x] Repeated generation verified with matching semantic fingerprint, object/component counts, and asset inventory.
- [x] Current Editor compilation succeeds; current Console has no errors or warnings after verification.
- [x] Live Play Mode frame advancement, animated ring/arm movement, pause/resume, power cycling, and camera transition verified.
- [x] All five camera viewpoints reached their target position and orientation; measurements saved in CameraTourVerification.json.
- [x] Existing Quantum Inspector providers discovered seven subsystems, five serialized graph references on the cross-reference showcase, and the Demo scene in Project view.
- [x] Game-view overview and reactor screenshots captured and visually reviewed.
- [x] README describes supported tool features, navigation, references, regeneration, limitations, and demo removal.
- [ ] Import the intended package into a fresh Unity 6000.3.7f1 URP project and repeat compile, open, Play Mode, regeneration, and removal checks.
- [ ] Repeat in every additional Unity/URP version advertised on the store listing; do not claim untested render pipelines.
- [ ] Profile graph expansion and rendering on representative minimum-spec customer hardware; verify all five viewpoints and controls at target resolutions.
- [ ] Manually review button interaction, keyboard/accessibility expectations, and screenshot readability in the final package layout.
- [ ] Review the actual export dependency list. Include this folder and its `.meta` files, including the ownership stamp; exclude `Editor/Backups~` and `Editor/Verification~`, and avoid exporting unrelated project settings or development tools.
- [ ] Complete final product licensing, store copy, screenshot selection, and package-upload checks for the commercial release.

The previous-scene backup does not preserve previous versions of regenerated materials, mesh, prefabs, or the Volume profile. Keep custom work under version control or copy the full demo before regeneration.

## Cluster motion upgrade — 13 October 2026

- [x] Stable component identity, repeated-instance slots, Scripts/Transform families, truthful typed edges and explicit scoped reference discovery.
- [x] Investigation Lock queues additions/edits and safely invalidates deleted objects; explicit Apply Updates/Arrange preserve pins. Hierarchy GameObjects and Project selections still update the view unless Lock selection is enabled.
- [x] Full/Reduced/Off motion, interruptible elapsed-time transitions, collapse/reopen retention and explicit camera focus.
- [x] Mouse resize with readable wrapping, direct drag/pinning, semantic zoom, culling, search/reveal and preserved radial-wheel behavior.
- [x] Leaf folders and mixed assets, bounded previews with icons, separate dark/light graph palettes, unchanged link-thickness settings.
- [x] Dense prefab with 42 owner components / 32 repeated scripts; mixed assets and a leaf folder supplied separately from the saved Demo scene.
- [x] Core checks, existing regressions, live resize, Prefab Stage, Play Mode/recompile callback checks and 100/500/2,000-member measurements recorded in ClusterMotionVerification.json.
- [x] Actual dark/light/Project screenshots captured and visually reviewed; palette changes retained the same camera and positions.
- [x] Saved Demo independently rechecked: 391 GameObjects, 1,196 components, depth 8, seven subsystems, zero missing scripts and zero external Assets dependencies.
- [ ] Run a counterbalanced developer trial and record task time, wrong selections and disorientation; no usability-gain claim yet.
- [ ] Measure native UI painting, end-to-end frames and allocations with Unity Profiler; the available per-thread allocation counter failed its positive control.
- [ ] Complete the fresh-project, minimum-hardware and commercial release checks above. The 4 ms work-slice goal is not a hard guarantee.

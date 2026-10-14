# Quantum Inspector: stable constellations

## Cosmic Nebula pass — 13 October 2026

Open **Tools > Quantum Inspector > Open > Clustered Graph**. Graph Mode follows Hierarchy GameObjects, selected Component instances, Project folders and files by default. Entering Graph Mode does not silently turn on Investigation Lock. Leave the window's Lock selection off to follow Project assets, folders and Hierarchy GameObjects, including while Investigation Lock is enabled. Direct external component selections still require Investigation Lock to be off. An identity check in the existing Editor update loop also catches deferred selection notifications. Component selection keeps the exact instance selected in Unity.

**Cosmic Nebula** is the initial personal palette. The existing layout is retained: no alternate layout engines or scene effects were introduced. A cached seeded indigo/violet/teal field sits behind quiet measured family contours, typed paths and opaque instrument cards. The owner has a square silhouette, an OWNER label and three instrument arcs; families are counted pills; components retain individual icons. Selected, OFF, MISSING and PIN text/shape cues supplement family color. Real asynchronous prefab-owner/asset previews use reserved icon/thumbnail space; “preview pending” and “type icon” describe the fallback honestly. Scene owners use a type icon because requesting a rendered scene-owner preview caused a measured 1.56-second synchronous Editor stall; that request path was removed.

Graph Settings choices for palette, motion, quality, Investigation Lock, Ambient and auxiliary UI are personal EditorPrefs overrides. They do not serialize into the shared preset. Editable nebula color tokens remain in the settings preset. Cosmetic choices do not change graph IDs, family membership, positions, pins or camera targets.

Full motion uses 110 ms hover emphasis, 180 ms selection emphasis, 320 ms enclosure expansion, 240 ms collapse/ordinary layout transitions and 340 ms explicit focus. A bounded, one-shot 520 ms path accent acknowledges selection along discovered relationships. **It is selection feedback, never a runtime execution/traffic display.** Reduced and Off commit spatial and camera targets immediately and stop ambient/path motion. The in-tool preference is always available; automatic OS reduced-motion detection is not implemented.

Ambient is off initially. When enabled with Full motion, only the cached background moves (4 logical pixels horizontally, 3 vertically, periods 32/38 seconds). A separate **Ambient while locked** opt-in is required during Investigation Lock. Low uses a 128 × 64 cached field and simple accents; Balanced uses 256 × 128; Cinematic uses 384 × 192 and additional seeded stars. Dense views suppress extra glow and use clipped straight paths with the original relationship styles. No quality tier removes members. Hidden/detached windows suspend graph work; close/reload disposes textures and queues.

Current verification and measured limits are recorded in **CosmicNebulaVerification.txt** and **CosmicNebulaVerification.json**. The historical screenshots and performance table below predate this art pass. The initial capture attempt failed; the subsequent clean-view review successfully captured the actual running Cosmic Nebula renderer below.

## Clean graph view and default settings

The default graph view keeps Hierarchy, Project, Wheel, Fit, search and Settings in one compact row. Legends, relationship descriptions, research details, navigation, footer and secondary header controls are hidden. **Settings > Show auxiliary UI** restores those panels. Advanced appearance, motion, lock and graph actions are available in the scrolling Settings drawer, together with the full selected name and relationship explanation. Graph context menus remain available. Pending changes/loading display a compact indicator when needed.

**Settings > Restore clean defaults** resets the settings preset and personal graph preferences to Cosmic Nebula, Balanced quality, Full functional motion, Ambient off, 2× dots, unlocked selection and auxiliary UI off. It preserves the current graph root, node pins, saved arrangements and camera. The reference factory preset is `Assets/InspectorTool/Editor/QuantumInspectorCleanDefaults.asset`. **Open preset** exposes the existing shared settings asset for explicit editing.

![Actual Unity graph with clean defaults](Screenshots/CleanClusterView.png)

This capture uses the actual LivingLaboratory scene object `SHOWCASE_DenseComponentFixture`: 43 components, 49 graph nodes, with family counts derived from actual members. The isolated review uses the existing layout algorithm and default settings rather than overwriting the user's saved arrangement. At the same window size, the measured graph viewport grew from 949.33 × 315.33 to 1269.33 × 618.67 logical pixels, **2.62× the area**. Toggle/reset verification preserved positions and camera; restoring defaults preserved a pinned root. See `CleanViewVerification.txt` for the checks and limits.

Verified in **Unity 6000.3.7f1**, Windows, with the project's existing **URP 17.3.0** installation. No additional packages or third-party assets were added. These are implementation checks, not evidence of a general usability improvement or a broader Unity compatibility claim.

## Observed implementation before this upgrade

No applicable AGENTS.md files were found in the repository or its ancestor directories. The tool lives in `Assets/InspectorTool/Editor`, namespace `FightSong.InspectorTool`, in the Editor-only `FightSong.InspectorTool.Editor` assembly. `InspectorWheelWindow` is a UI Toolkit window; the radial wheel and clustered graph were already separate modes. The graph was already uncapped and supported explicit serialized-reference, direct-dependency and prefab-source discovery. No voice recognition implementation was found.

The previous graph stored presentation state in `Library/FightSong/QuantumGraphResearch.asset`. Family directions depended on the number of currently present families. Settings changes refreshed discovery/layout; selected edges had continuous moving particles. The previous Investigation Lock did not block external Unity selections. Those observations are separate from the requested design, which this upgrade implements below.

## Open and navigate

Choose **Tools > Quantum Inspector > Open**, inspect a GameObject, then choose **Clustered Graph**. For the dense fixture, open `Demo/GraphFixtures/DenseComponentFixture.prefab` in Prefab Mode and select its root. The owner has 42 components: Transform, rendering, physics and audio components, a disabled Light, and 32 repeated DemoProbe scripts. There are 48 graph nodes and 89 underlying ownership/grouping edges before reference discovery.

![Dense fixture overview, captured in Unity](Images/ConstellationDark.png)

| Control | Behavior |
| --- | --- |
| Investigation Lock | Explicit personal choice; off initially. External selections cannot change a locked investigation; additions/edits wait for Apply Updates. Deleted objects become unavailable immediately. |
| Lock selection | Existing window-level selection lock. Both locks must be off for Graph Mode to follow external Hierarchy/Project selections. |
| Apply Updates | Accept queued changes, preserving existing locked positions. New nodes use local slots; unresolved spacing is reported. |
| Arrange | Reconcile unpinned space explicitly. Does not fit the camera. Reset Layout also retains pinned nodes. |
| Empty-space or middle-button drag | Pan. Alt-drag is also available. Wheel/trackpad zooms about the pointer. |
| Node/header drag | Move and pin a node, or move the cluster's presentation together. Scene Transforms are not edited. |
| Lower-right resize corner | Resize a card. Titles wrap and impose a minimum readable height. Right-click to restore automatic sizing. |
| + / −, or Enter on the selected node | Explicit expansion/collapse. A component click selects it; double-click does not navigate the root. |
| Clusters menu | Expand all loaded families or collapse them. No wheel capacity limit applies to discovery. |
| Search / Next match | Find discovered names, types, aliases and paths, including collapsed members. Reveal their ancestors and focus deliberately. |
| Center selected / context Focus | Recover the selection. Fit Entire Graph or F frames the discovered graph explicitly. |
| Context menu | Pin/unpin, reveal verified references, list relationships, ping/open Unity's Inspector, or deliberately make a node the root when unlocked. |
| Escape | Cancel the active drag/resize and clear graph selection. |

Back, Forward, breadcrumbs and the separate Hierarchy/Project histories remain available. The compact radial wheel still supports capacities 5–8; that limit does not apply to the graph.

## What the connections mean

The legend distinguishes plain ownership lines, solid directional containment/field lines, dotted presentation grouping, dashed reference arrows, and dashed dependency diamonds. Selecting a node shows relationship types and property paths; the context menu lists the full observed neighborhood. Overview/collapsed proxies aggregate only matching direction and relation type. Counts come from underlying edges; crowded edge badges may be omitted, with exact relationships available through selection and the menu.

Ownership is GameObject-to-component membership. Family membership is only a presentation relationship. Serialized-reference edges come from actual object-reference properties; dependency discovery uses Unity's **direct** dependency query. Prefab-source and RequireComponent relationships retain their separate typed descriptions. Sharing a type or color does not imply execution order, subscription or causation. No transitive dependency scan or edge-bundling algorithm is claimed.

Saved scene objects use eligible GlobalObjectIds. Assets/sub-assets use GUID/local-file identity where applicable. Unsaved/runtime objects use session identity; missing scripts receive nonpersistent placeholder identities that are discarded when their ambiguous topology changes. A validated Unity-object-reference handoff preserves the window through domain reload/Play Mode without treating runtime IDs as persistent editor IDs. Moving a saved object between scenes changes its durable ID; an open window can transfer its current presentation only when the actual object reference still matches.

## Layout, motion and appearance

MonoBehaviours belong to **Scripts** by default, including UI scripts. Native families include Transform, Rendering, Physics, Audio and UI; existing Animation/Networking categories and explicit category rules remain supported. Repeated component instances stay separate. Stable family directions and saved local slots prevent insertions/removals from renumbering surviving nodes. Bounds include measured titles and card sizes. Growing unpinned families move outward; pins and locked anchors take priority over automatic spacing.

Full motion uses an interruptible elapsed-time ease-out: 320 ms for enclosure expansion and 240 ms otherwise; explicit camera focus uses 340 ms. Reduced and Off both commit spatial/camera changes immediately. Dragging is direct. There is no graph idle orbit or wobble. Optional background motion and a single selection path accent are described above. Collapse preserves expanded positions and selection; pinned members fade without moving. A stale root/snapshot preparation cannot overwrite a newer one.

Overview shows counts, family labels, simple member marks and typed aggregate edges. Inspection reveals individual labels; detail enables previews. Separate enter/exit thresholds prevent threshold flicker. Selected and pinned nodes keep useful labels. Title presentation targets at least 11 logical screen pixels. Culling affects drawing, not membership or search. Small Project folders retain readable asset cards.

Editor, Dark, Light and Cosmic Nebula are graph palettes. Personal toolbar choices override shared defaults without changing the preset. Switching palettes leaves IDs, membership, targets, camera and transition timing unchanged. Link thickness remains configurable in editor pixels, including at different zoom levels.

![Same arrangement and camera in the light palette](Images/ConstellationLight.png)

Project scope discovers immediate folders **and files**, including leaf files. Small folder branches use stable local rows. Visible/selected previews share a four-request work budget with bounded retries; icons are honest fallbacks. Thumbnail space is reserved before textures arrive, so completion does not change layout. Changing roots discards stale requests. Entire Project was already present as a progressive directory expansion option; it remains a legacy extension path, not a newly validated full-project index, preview-virtualization guarantee, or whole-project analysis engine.

![Mixed Project assets and actual previews](Images/ProjectFixtures.png)

## Implementation map

| Files under `Assets/InspectorTool/Editor` | Responsibility |
| --- | --- |
| InspectorWheelWindow / InspectorWheelSettings | Mode/selection integration, lifecycle, existing settings and graph preferences |
| QuantumGraphModel / QuantumGraphDataProvider | Typed discovery, stable identity, scoped jobs, truthful references, pending changes |
| QuantumClusterClassifier / QuantumGraphNodeSizing | Extensible existing rules, family directions, cached title/preview bounds |
| QuantumGraphLayoutEngine | Persistent slots, local packing, bounded neighborhood checks and spatial culling |
| QuantumGraphMotion / QuantumGraphWindowHandoff | Interruptible display poses, reload/Play Mode handoff |
| QuantumGraphView / QuantumGraphNodeView / QuantumGraphEdgeRenderer / InspectorWheel.uss | Input, labels, camera, recycled cards, palette and typed edge rendering |
| QuantumGraphPreviewCache / QuantumGraphStatePersistence | Bounded preview requests and personal saved presentation |
| Verification/QuantumGraphConstellationVerification / QuantumGraphPresentationVerification | Core acceptance checks and reproducible stress measurements |

The demo additions are `Runtime/DemoProbe.cs`, `Runtime/DemoExperimentPreset.cs`, `Editor/QuantumInspectorGraphFixtureBuilder.cs`, `GraphFixtures/`, this guide, screenshots and verification results. The existing scene generator and `Scenes/Demo.unity` remain intact. The saved scene was independently reopened as a preview for validation: **391 GameObjects, 1,196 components, depth 8, seven subsystems, zero missing scripts, and no dependencies elsewhere under Assets**. Unsaved user scene edits were not saved or discarded. The new prefab contains two GameObjects and 42 components on its owner; it is a separate component-density fixture.

## Verification and measured limits

### Zoom overview

Zooming out between 78% and 38% fades component and asset cards into round, selectable dots beneath their actual family or containing-folder header. Full motion settles this presentation in 180 ms and supports reversing mid-transition; Reduced and Off change it immediately. This is screen-space presentation only: node positions, expansion, pins, graph membership and Unity object state stay intact. Zoom back in for the full cards, or double-click a dot to focus its exact instance. Dots support selection, dragging, hover names and context actions. A ring marks selection, a lower bar marks a pin, a hollow dot marks disabled state, and a crossed hollow dot marks a missing component. Headers retain actual family counts and show the number of presented dots, including expanded fields.

The overview reserves space for both headers and their complete dot fields. Pan still moves the overview; Fit resets its framing. The zoom floor is 0.5%. See `ZoomOverviewVerification.txt` for the recorded live Editor checks and measurement limits.

Drag a category header or empty space inside its enclosure to move the category and its members together. Category placement survives zooming, reopening and the window's saved state. Drag a dot to move just that instance; Alt-drag or middle-drag pans over a category. The relationship summary has a fixed height so selection does not repack the viewport during a drag.

Dots now default to **2× the original diameter**. **Settings > Dot size** adjusts this between 1× and 6× and saves the personal preference. The Settings preset exposes **Graph Dot Size Default** for an explicitly edited shared default. Dots receive additional spacing when the family enclosure has room; dense stress views retain bounded packing without dropping members. Enable auxiliary UI to expose the resizable research pane. Floating windows also have resize handles on all four edges and corners; docked windows retain Unity's outer dock dividers. Window resizing respects the existing minimum size and the screen's usable area.

See [ClusterMotionVerification.json](ClusterMotionVerification.json). Checks include 50 identity/layout/lock/motion/preview assertions, the existing 10,026-element progressive-array regression, 84,000 radial-wheel samples, real Painter2D stroke geometry at four zoom levels, live resize/title wrapping, and Prefab Stage identity. Compile completed without errors. Live Play Mode advanced frames; camera `(83,49)` and zoom `0.72` survived entry/reload/exit. One update callback and one selection callback per tool window were observed. Idle graphs produced no view updates in the reproducible idle check.

Recorded hardware: i9-12900, RTX 3070, 65,222 MB system memory. Viewport: 1180 × 514 logical pixels; motion Off. Measurements cover main-thread preparation and UI binding, **not native painting, GPU time or end-to-end frame time**.

| Members | Nodes / edges | Visible cards / crossing edges at inspection | Maximum measured pan update | Maximum scheduled tick |
| --- | --- | --- | --- | --- |
| Dense demo: 42 | 48 / 89 | 20 / 65 | 0.224 ms | 10.091 ms |
| Synthetic: 100 | 108 / 224 | 15 / 51 | 0.189 ms | 0.151 ms |
| Synthetic: 500 | 508 / 1,124 | 40 / 220 | 1.037 ms | 5.950 ms |
| Synthetic: 2,000 | 2,008 / 4,499 | 45 / 733 | 1.859 ms | 10.373 ms |

The 2,000-member preparation took 22 slices, with a maximum text slice of 4.054 ms. The 500-member run included a 5.368 ms text slice: the 4 ms budget is a soft scheduling target, and a single Unity text/API call cannot be preempted. Final packing/binding can also exceed that target. An unsliced cold 2,000-member layout measured approximately 61–88 ms across recorded runs; large view updates therefore prepare text incrementally. These results are not a guaranteed node limit or frame rate. Unity's allocation counter returned zero for a deliberate 1 MB allocation, so allocation results are explicitly **unavailable**, not reported as zero.

Run the verification entry points in Edit Mode with the included demo installed. `QuantumGraphConstellationVerification.Run()`, `QuantumGraphVerification.Run()`, `InspectorWheelNavigationVerification.Run()` and `QuantumGraphEdgeVerification.Run()` return structured results. `QuantumGraphPresentationVerification.Begin(100)` opens a temporary window; use 500, 2000 or 42 for the other fixtures, allow an Editor frame, then call `Complete()`. The resize harness similarly uses `Begin()`, `Grow()`, `Shrink()`, `Complete()` with UI frames between calls. These routines are developer verification helpers, not player-build code.

## Six-task visual-understanding trial

Use a disposable instance of DenseComponentFixture, not the package prefab, for component edits. Compare this version with a separately checked-out prior version using matched fixtures; alternate which version is used first.

1. Search for AudioLowPassFilter, reveal it, and identify the Audio family.
2. Collapse Audio with its explicit toggle; reopen it and locate the same instance.
3. Add and remove one DemoProbe. While locked, identify the pending addition/removal; Apply Updates and find exactly the affected node. Check that surviving pins stay put.
4. Select a DemoProbe and reveal serialized references. Trace `upstream` to its actual component and `sampleTarget` to CalibrationTarget. Explain why the dotted family line is not either reference.
5. Select a component, pan/zoom away, then Center selected. Verify root, pins and history survive.
6. Switch Dark/Light and Full/Reduced/Off; repeat collapse/reopen and verify the same arrangement and readable titles. Unlock and test external Hierarchy/Project selections, including `GraphFixtures/Leaf`.

Record completion time, wrong selections, task completion and a 1–5 disorientation rating per task. **No counterbalanced human trial was performed in this implementation pass.** Fresh-package import/removal, minimum-spec profiling, end-to-end frame allocation/paint measurement, accessibility review and any additional advertised Unity versions still require release review. Entire-project indexing, automatic community detection, hierarchical edge bundling, voice expansion and 3D graphs remain outside this upgrade.

Remove the entire Demo directory to remove the laboratory and fixtures without removing the main tool. Personal graph presentation is stored under Library; panning/pinning does not dirty the scene or shared settings asset.

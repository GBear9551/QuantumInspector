# Quantum Inspector — Living Laboratory

A self-contained, stylized research facility built to teach hierarchy and component inspection. The cutaway laboratory combines a cyan quantum reactor, violet and green specimen chambers, articulated robots, holographic consoles, and a diagnostic memory array. Geometry uses Unity primitives and one generated torus mesh; no third-party models, textures, fonts, or downloads are required.

![Living Laboratory in Play Mode](Images/LaboratoryPlayMode.png)

## Open and explore

1. Open `Assets/FightSongStudio/QuantumInspector/Demo/Scenes/Demo.unity`, or choose **Tools > Quantum Inspector > Demo > Open Demo Scene**. The scene asset is named **Demo**; its single root is `QuantumInspector_LivingLaboratory`.
2. Choose **Tools > Quantum Inspector > Open**, then select the laboratory root or a numbered subsystem in Unity's Hierarchy.
3. Compare **Orbital Wheel** and **Clustered Graph**. Expand children, inspect attached components, and use Parent, Back, and history to retrace an investigation.
4. Leave **Lock selection** unchecked to follow Hierarchy GameObjects and Project assets or folders in either view. **Investigation Lock** holds automatic layout and queues content changes but does not block these selections. **Apply Updates** accepts pending changes; **Arrange** explicitly reconciles unpinned space. Unlocking investigation alone does not rearrange the graph.
5. Enter Play Mode in the Game view. **Pause systems** freezes machinery and telemetry; **Cycle power** cycles high, standby, and medium output; **Next viewpoint** visits the overview, reactor, research chambers, robotics, and control center. Camera transitions continue while systems are paused.

The included camera presents the complete facility. Use the Game view for the intended lighting and post-processing. In Scene view, enable scene lighting and effects; the Open Demo Scene menu frames the laboratory automatically. Motion runs only in Play Mode. For a clean screenshot, uncheck **Show Controls** on the root's `DemoLabAnimator` during Play Mode; exiting Play Mode restores the scene.

## Guided inspection examples

All showcases live below `06_DemoDocumentation`. Search their exact names in the Hierarchy. Seven are in `NavigationExamples`; `SHOWCASE_LargeCluster` is in `StressTestCluster`.

| Object | What to explore |
| --- | --- |
| `SHOWCASE_DeepHierarchy` | Four nested calibration stages contain visible housings and a working sensor probe. Follow the branch to depth 8, inspect the probe's reactor reference, and navigate back. |
| `SHOWCASE_ManySiblings` | Six named calibration cartridges share one parent. Compare wheel paging/recycling and graph child expansion. For more siblings, inspect `00_LaboratoryInfrastructure/Floors` (26 children). |
| `SHOWCASE_MultiComponentObject` | MeshFilter, MeshRenderer, BoxCollider, kinematic Rigidbody, DemoDevice, DemoSensor, and DemoGuide share one GameObject. Compare component categories and serialized parameters. |
| `SHOWCASE_CrossReferencedSystems` | Its DemoGuide points to the reactor, power grid, sensor network, control console, and a robotic arm. These are real serialized references, including intentional cycles between systems. |
| `SHOWCASE_LargeCluster` | Six diagnostic banks each contain nine physical memory cells, with serialized power connections. Expand incrementally, compare grouping, and follow a cell to its power distributor. |
| `SHOWCASE_EmptyGameObject` | Deliberately Transform-only, for observing an empty inspection state. It is the sole intentionally empty showcase. |
| `SHOWCASE_DisabledObject` | An inactive beacon retains its mesh and DemoDevice connection. Compare inactive objects with active siblings. |
| `SHOWCASE_NestedPrefab` | A DiagnosticRack prefab contains two SensorPod prefab instances. Inspect the hierarchy, prefab overrides, and original prefab assets. |

This installed Quantum Inspector supports **Clustered Graph**, component classification, and reference overlays. In the graph's **Overlays** menu, enable **SerializedReferences**, or right-click a component and choose **Reveal serialized references**. Use **PrefabSources** for the nested prefab and **AssetDependencies** when exploring Project assets. References are discovered from serialized Unity fields, not hand-drawn connections. Enable overlays selectively on the large cluster to keep the view readable. Voice navigation is not part of this demo or the inspected tool.

The [cluster motion guide](ClusterMotionGuide.md) covers stable families, motion preferences, pins, semantic zoom, previews, the exact verification results, and a reproducible six-task trial. A separate `GraphFixtures/DenseComponentFixture.prefab` has **42 attached components**, including **32 distinct DemoProbe instances**; the main laboratory scene is unchanged. Choose **Tools > Quantum Inspector > Demo > Create Graph Trial Fixtures** to create any missing fixture assets without replacing existing ones. Open the prefab in Prefab Mode, select its root, then enter Clustered Graph. `GraphFixtures` also contains a texture, material, ScriptableObject, source file, opaque text/binary fixture, and a leaf folder.

## Laboratory structure

```text
QuantumInspector_LivingLaboratory
├── 00_LaboratoryInfrastructure
│   ├── Floors
│   ├── Walls
│   ├── Ceiling
│   ├── LightingNetwork
│   └── StructuralSupports
├── 01_QuantumReactor
│   ├── ReactorCore
│   ├── EnergyRings
│   ├── ContainmentNodes
│   ├── CoolingSystem
│   └── MonitoringSensors
├── 02_RoboticsWorkshop
│   ├── RoboticArm_A
│   ├── RoboticArm_B
│   ├── AssemblyConveyor
│   ├── ToolStorage
│   └── DiagnosticStations
├── 03_ResearchChambers
│   ├── Chamber_01 … Chamber_03
│   └── SampleContainers
├── 04_HolographicControlCenter
│   ├── MainConsole
│   ├── DisplayPanels
│   ├── ProjectionNodes
│   └── DataTerminals
├── 05_EnvironmentalSystems
│   ├── Ventilation
│   ├── PowerDistribution
│   ├── SecurityCameras
│   └── EmergencyLighting
└── 06_DemoDocumentation
    ├── WelcomeMarker
    ├── NavigationExamples
    └── StressTestCluster
```

| Subsystem | Purpose and relationships |
| --- | --- |
| Infrastructure | Graphite floor tiles, a cutaway shell, structural supports, presentation camera, local atmosphere Volume, and the five-light network establish the environment. |
| Quantum reactor — center | An emissive core and three rotating rings are surrounded by containment pylons, coolers, and sensors. DemoReactorController holds serialized connections to its equipment. |
| Robotics workshop — east | Two articulated arms expose joints, tool mounts, assembly targets, and their power supplier. A conveyor, diagnostic equipment, and tool storage complete the work area. |
| Research chambers — west | Three containment frames hold different rotating specimens with environmental sensors and controllers. DemoSensorNetwork also aggregates sensors elsewhere in the lab. |
| Holographic control center — north | Six displays, control pads, a layered projection, and terminals surround the main console. DemoControlConsole references the reactor, grid, sensor network, cameras, and displays. |
| Environmental systems — south | Ventilation fans, six battery cells, camera housings, and emissive warning beacons provide practical consumers for DemoPowerDistributor. Security cameras are illustrative devices, not extra rendering cameras. |
| Demo documentation | In-scene DemoGuide notes, the eight targeted inspection examples, and the memory-bank stress subtree provide a learning route. |

## Generation and safe regeneration

Choose **Tools > Quantum Inspector > Demo > Generate or Regenerate Laboratory** in Edit Mode. The Editor-only `QuantumInspectorDemoSceneBuilder` creates the scene, shared materials, torus mesh, nested prefabs, atmosphere profile, and validation report at fixed paths under this Demo directory. It uses deterministic loops and fixed transforms; it does not use random placement.

Regeneration asks for confirmation and keeps one previous scene copy at `Editor/Backups~/Demo.previous.unity`. It refuses to replace an existing scene without the matching `Documentation/GeneratorOwnership.txt` stamp, and refuses to regenerate an open, modified Demo scene. Save or close that scene first. Unrelated open scenes are preserved.

Generated materials, mesh, prefabs, and the atmosphere profile are updated in place. Their manual edits are replaced. **The backup protects the previous scene only, not previous versions of these shared assets.** Copy the whole Demo folder or use version control before customizing generated assets. The `~` backup and verification folders are ignored by Unity and should be excluded from a release archive.

For automated authoring, call `FightSongStudio.QuantumInspector.Demo.Editor.QuantumInspectorDemoSceneBuilder.Build()` from an Editor context. This API uses the ownership check and scene backup but does not display the menu confirmation. It creates no duplicates on repeated runs. The generator requires URP to be the active render pipeline and does not change project render-pipeline or build settings.

Choose **Tools > Quantum Inspector > Demo > Validate Saved Demo** to recheck and write `Documentation/ValidationReport.json`. Validation requires Edit Mode and a saved, unmodified Demo scene.

## Verified contents

Verified on **Unity 6000.3.7f1**, with **Universal Render Pipeline 17.3.0** and this project's existing PC URP renderer. No pipeline packages were installed or upgraded for the demo. Other Unity versions, renderers, platforms, and hardware have not been certified.

| Measurement | Result |
| --- | ---: |
| GameObjects, including inactive objects and the scene root | 391 |
| Components, including Transforms | 1,196 |
| Non-Transform components | 805 |
| Direct subsystem groups under the laboratory root | 7 |
| Maximum depth, measured in edges from root at depth 0 | 8 (9 levels) |
| Parents with at least five direct children | 32 |
| Groups with 25–50 descendants | 7 |
| GameObjects with at least two non-Transform components | 307 |
| Stress subtree, including wrapper and showcase root | 62 |
| Serialized cross-object reference entries in demo components | 436 |
| Shared custom materials / mesh assets / prefab assets | 10 / 1 / 2 |
| Nested prefab instances inside the scene's rack | 2 |
| Active lights | 5 (one directional, four points) |
| Missing scripts / dangling references / null demo reference fields | 0 / 0 / 0 |
| Scene dependencies on assets elsewhere under Assets | 0 |

Dense groups are Floors (26 descendants), ContainmentNodes (25), RoboticsWorkshop (50), ResearchChambers (40), HolographicControlCenter (41), EnvironmentalSystems (43), and NavigationExamples (29). Reference counts count serialized slots, not unique graph edges. The material count excludes Unity's built-in font material. Package and engine dependencies are expected; the external-asset check covers other project assets under `Assets/`.

Two consecutive builds produced the same semantic hierarchy fingerprint, counts, and asset inventory. The fingerprint records named hierarchy paths, active states, transforms, component types, and demo reference paths; it is not a byte-for-byte scene-file hash. The five URP additional light-data components are included explicitly so counts remain stable on reload. Live Play Mode checks verified frame advancement, ring and arm movement, pause/resume, power cycling, and all five camera positions. See `ValidationReport.json`, `VerificationResults.json`, `CameraTourVerification.json`, and `AssetStoreDemoChecklist.md` for recorded evidence and remaining release checks.

## Implementation and performance

`Runtime/` contains nine small components in the `FightSongStudio.QuantumInspector.Demo` namespace and its own assembly. Only DemoLabAnimator has an Update method. It drives rotations, articulated joints, emissive material-property blocks, staggered warning beacons, five camera views, and 5 Hz telemetry. Shared material assets are never changed by the animation. DemoDevice, DemoSensor, DemoReactorController, DemoPowerDistributor, DemoSensorNetwork, DemoRoboticArm, DemoControlConsole, and DemoGuide expose ordinary serialized data for inspection.

`Editor/` has a separate Editor-only assembly referencing URP for procedural authoring. Runtime scripts have no UnityEditor dependency and no dependency on the Quantum Inspector implementation. The demo does not need the Unity CLI or AI packages to open, play, or regenerate through its menu. No main tool source was changed for this demo task.

The scene uses a small number of shared materials, GPU-instancing-compatible material settings, one actual rendering camera, five lights, and selective static colliders. The multi-component showcase's Rigidbody is kinematic. Emissive strips and warning lights are meshes rather than extra realtime lights. The controls use a small IMGUI overlay; there are no edit-time animation updates.

## Limitations and release review

- This is an illustrative laboratory, not a physical reactor, robot IK simulator, security-camera system, or playable character level. Telemetry is synthetic. The chamber frames imply containment rather than transparent glass simulation.
- Materials and the local Volume profile target the installed URP pipeline. Built-in RP and HDRP are not supported by this scene generator. Reduced quality settings can alter bloom, shadows, or contrast.
- The hierarchy is intentionally dense. Expanding every graph node and every reference overlay at once can be cluttered; performance will also depend on the tool and the host machine.
- A clean-project package import, target-device profiling, and final Asset Store marketing review remain release tasks. No player build or cross-platform certification is claimed.
- To manually repeat the runtime check: clear the Console, enter Play Mode, observe rings/arms/fans and pulsing displays, pause for several seconds, resume, cycle all three power levels, visit all five views, and confirm no new errors. Exit Play Mode and run Validate Saved Demo again. Compare its counts with the table above.

## Remove the demo

Close Demo, open another scene, then delete only `Assets/FightSongStudio/QuantumInspector/Demo` in Unity's Project window. No main Quantum Inspector assets, project settings, or build-profile entries depend on this folder. The demo was not added to a build profile. If you added it manually, remove that scene entry too. Empty parent folders may optionally be removed; preserve any siblings containing other assets.

# 全命令索引

0.8.0 包含 **277 个唯一命令名、310 处实现声明**。Cinemachine 2/3 是互斥适配，不重复计数。这里是完整静态候选；可选包、Editor 版本和编译条件决定当前工程是否注册。

取得当前参数：

```powershell
& '<Skill目录>/scripts/Start-UnityTask.ps1' -ProjectPath '<工程>' -Command '<命令名>' -DiscoverOnly
```

[机器可读完整清单](command-index.json) 包含每条命令的程序集、源文件/行号、defineConstraints、versionDefines、源码条件与说明。两个 UI Toolkit 元素截图命令需要 `UNITY_6000_7_OR_NEWER`。

## 按功能检索

### animation/animator

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `add_animator_layer` | Add a layer to an AnimatorController. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |
| `add_animator_parameter` | Add a parameter (Float \| Int \| Bool \| Trigger) to an AnimatorController. A duplicate name returns code 'duplicate_parameter'. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |
| `add_animator_state` | Add a state to a layer, optionally with a motion (AnimationClip or BlendTree) and as the layer default.  | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |
| `add_animator_transition` | Add a transition between two states (or from AnyState/Entry, to Exit) on a layer, with optional conditions.  | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |
| `create_animator_controller` | Create an .controller AnimatorController asset (with a default Base Layer) under the authoring root. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |
| `get_animator_controller` | Read an AnimatorController's full structure: parameters, layers, states (with motion / default), and transitions (with conditions). | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimatorControllerCommands.cs) |

### animation/clip

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_animation_clip` | Create an empty .anim AnimationClip asset under the authoring root, with an optional frame rate and loop flag. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimationClipCommands.cs) |
| `get_animation_clip` | Read an AnimationClip's metadata and all float curve bindings (optionally with keyframes). | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimationClipCommands.cs) |
| `remove_animation_curve` | Remove a float curve binding from an AnimationClip (SetEditorCurve(clip, binding, null)). Destructive: requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimationClipCommands.cs) |
| `set_animation_curve` | Add or replace a single float curve binding on an AnimationClip (via AnimationUtility.SetEditorCurve).  | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/AnimationClipCommands.cs) |

### animation/timeline

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `add_timeline_clip` | Add a clip to a named track on a TimelineAsset. For Animation tracks pass an AnimationClip asset; for Audio tracks an AudioClip. Requires the com.unity.timeline package. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/TimelineCommands.cs) |
| `add_timeline_track` | Add a track (Animation \| Audio \| Activation \| Control \| Playable \| Signal \| Marker) to a TimelineAsset, optionally nested under a parent group/track. Requires the com.unity.timeline package. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/TimelineCommands.cs) |
| `create_timeline` | Create a .playable TimelineAsset under the authoring root (optional frame rate). Requires the com.unity.timeline package. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/TimelineCommands.cs) |
| `get_timeline` | Read a TimelineAsset's structure: frame rate, duration, and its tracks with their clips. Requires the com.unity.timeline package. | [实现](../assets/com.unity.pipeline/Editor/Commands/Animation/TimelineCommands.cs) |

### assets

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `copy_asset` | Copy an asset to a new path under the authoring root. The copy gets a fresh GUID. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `create_asset` | Create a new ScriptableObject (or other UnityEngine.Object) asset of the given type at a path under the authoring root. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `create_folder` | Create a folder under the authoring root (creates intermediate folders). | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/FolderCommands.cs) |
| `delete_asset` | Delete an asset from the project. Destructive: requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `find_assets` | Find assets by type and/or name and/or label, returning their path, GUID and type. At least one filter is required. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `move_asset` | Move (or rename via a new path) an asset to a new location under the authoring root. Preserves the asset's GUID. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `rename_asset` | Rename an asset in place (keeps it in the same folder, keeps its GUID). | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |

### assets/import

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_import_settings` | Read an asset's import settings, structured by importer type (texture/model/audio), including the default-platform fields and (for textures/audio) one platform override block. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetImportCommands.cs) |
| `import_asset` | Import an external file (e.g. a texture, model, audio clip) into the project by copying it to a path under the authoring root, then importing it. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetCommands.cs) |
| `set_import_settings` | Set import settings on an asset's AssetImporter (default platform top-level properties, or a texture/audio per-platform override) and re-import it. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/AssetImportCommands.cs) |

### assets/text

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `read_text_file` | Read a UTF-8 text file under the authoring root and return its contents. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/TextFileCommands.cs) |
| `write_text_file` | Write UTF-8 text to a file under the authoring root, then import it. Overwriting an existing file requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Assets/TextFileCommands.cs) |

### authoring

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_authoring_root` | Get the base folder (under Assets/) that bare authoring paths resolve against. | [实现](../assets/com.unity.pipeline/Editor/Commands/Authoring/AuthoringConfigCommands.cs) |
| `set_authoring_root` | Set the base folder (under Assets/) that bare authoring paths resolve against and are confined to. Use 'Assets' for full project access. | [实现](../assets/com.unity.pipeline/Editor/Commands/Authoring/AuthoringConfigCommands.cs) |

### baking/lighting

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `bake_lighting` | Trigger an async lightmap bake of the open scene(s) via Lightmapping.BakeAsync(). Returns immediately; poll lighting_bake_status until completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |
| `cancel_lighting_bake` | Cancel an in-progress lighting bake (Lightmapping.Cancel()). | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |
| `clear_baked_lighting` | Clear baked lightmap data for the open scene(s). Destructive: requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |
| `get_lighting_settings` | Read the active LightingSettings (lightmapper, bounces, resolution, directional mode, AO, etc.). | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |
| `lighting_bake_status` | Get the status of the last lighting bake: idle \| baking \| completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |
| `set_lighting_settings` | Apply a subset of lighting settings to the active LightingSettings. Returns { applied[], unknown[] }. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/LightingBakeCommands.cs) |

### baking/navmesh

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `bake_navmesh` | Trigger an async legacy NavMesh bake of the open scene(s) via UnityEditor.AI.NavMeshBuilder. Returns immediately; poll navmesh_bake_status until completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `bake_navmesh_surfaces` | Bake NavMeshSurface components (AI Navigation package). v1 stub: returns package_not_found when the package is absent. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `cancel_navmesh_bake` | Cancel an in-progress NavMesh bake (NavMeshBuilder.Cancel()). | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `clear_navmesh` | Clear the baked NavMesh for the open scene(s). Destructive: requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `get_navmesh_settings` | Read the default agent's legacy NavMesh bake settings (agentRadius/Height/Slope/Climb, minRegionArea, voxelSize). | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `navmesh_bake_status` | Get the status of the last NavMesh bake: idle \| baking \| completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |
| `set_navmesh_settings` | Apply a subset of legacy NavMesh bake settings to the default agent. Returns { applied[], unknown[] }. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/NavMeshBakeCommands.cs) |

### baking/occlusion

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `bake_occlusion_culling` | Trigger an async occlusion-culling bake of the open scene(s) via StaticOcclusionCulling.GenerateInBackground(). Returns immediately; poll occlusion_bake_status until completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/OcclusionBakeCommands.cs) |
| `cancel_occlusion_bake` | Cancel an in-progress occlusion bake (StaticOcclusionCulling.Cancel()). | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/OcclusionBakeCommands.cs) |
| `clear_occlusion_culling` | Clear baked occlusion-culling data for the open scene(s). Destructive: requires confirm=true. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/OcclusionBakeCommands.cs) |
| `occlusion_bake_status` | Get the status of the last occlusion bake: idle \| baking \| completed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Baking/OcclusionBakeCommands.cs) |

### batch

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `batch` | Run multiple registered commands in one transactional request. Later ops can reference earlier  | [实现](../assets/com.unity.pipeline/Editor/Commands/Batch/BatchCommand.cs) |

### build

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `build` | Trigger an async Player build and report the full BuildReport. Returns immediately (queued);  | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildCommand.cs) |
| `build_status` | Status of the current/most recent build: idle \| queued \| building \| completed, with the full  | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildCommand.cs) |

### build/settings

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `add_scene_to_build` | Add a scene to the Build Settings scene list (idempotent). Optionally enable it. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `get_build_settings` | Read the current build configuration from EditorUserBuildSettings / EditorBuildSettings. | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildConfigCommands.cs) |
| `list_build_profiles` | List Build Profile assets in the project (Unity 6 only). Returns feature_unavailable on earlier versions. | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildConfigCommands.cs) |
| `remove_scene_from_build` | Remove a scene from the Build Settings scene list (idempotent). | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `set_build_settings` | Set mutable EditorUserBuildSettings fields. Does NOT manage scenes (use add_scene_to_build /  | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildConfigCommands.cs) |

### build/targets

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `list_build_targets` | List the known BuildTarget values with their group and whether build support is installed. | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/BuildConfigCommands.cs) |
| `switch_build_target` | Switch the active build target (destructive, long-running: triggers a full reimport + domain  | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/SwitchBuildTargetCommand.cs) |
| `switch_build_target_status` | Status of the last target switch: idle \| switching \| completed (with success + activeBuildTarget). | [实现](../assets/com.unity.pipeline/Editor/Commands/Build/SwitchBuildTargetCommand.cs) |

### capture

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `capture_editor_element` | Capture a UI Toolkit VisualElement (by selector) from an EditorWindow to a PNG; returns path + base64. | [实现](../assets/com.unity.pipeline/Editor/Commands/CaptureEditorElementCommand.cs) |
| `capture_game_view` | Capture the game view as a PNG. source=screen (default) captures what the Game window shows, incl. Screen Space - Overlay UI; source=camera renders a single camera instead and misses overlay UI. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both). | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs) |
| `capture_runtime_element` | Capture a UI Toolkit VisualElement (by selector) from a live runtime panel (UIDocument or PanelRenderer) to a PNG; returns path + base64. | [实现](../assets/com.unity.pipeline/Runtime/Commands/CaptureRuntimeElementCommand.cs) |
| `capture_scene_view` | Render the active Scene View to a PNG. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both). | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/CaptureCommands.cs) |
| `screenshot` | Capture the Scene or Game view as a PNG and return its file path | [实现](../assets/com.unity.pipeline/Editor/Commands/ScreenshotCommand.cs) |

### capture/observation

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_visual_observation_status` | Read persisted observation status, measured capture cost, sequence and ring eviction counters. | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/VisualObservationCommands.cs) |
| `mark_visual_observation` | Associate the next captures with a caller operation label without changing the game. | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/VisualObservationCommands.cs) |
| `read_visual_frames` | Incrementally read bounded frame paths after a sequence. latest=true skips backlog explicitly. Frames remain in the ring and may later expire. No base64 payload flood. | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/VisualObservationCommands.cs) |
| `start_visual_observation` | Start continuous bounded PNG capture without blocking the Editor. Default screen source includes Overlay UI. Poll read_visual_frames during operations; starting capture alone does not start a model. | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/VisualObservationCommands.cs) |
| `stop_visual_observation` | Stop the named observation; keep its bounded frame evidence. Does not stop Play Mode or alter scenes. | [实现](../assets/com.unity.pipeline/Editor/Commands/Capture/VisualObservationCommands.cs) |

### cinemachine/authoring

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_brain` | Set the existing Brain's default blend duration/style with Undo. Existing custom blend rules may override the default. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineCommands.cs) |
| `configure_cinemachine_camera` | Configure a Cinemachine 2.x basic camera with Undo. Follow/LookAt can be explicitly cleared. Does not replace existing pipeline components or save scenes. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineCommands.cs) |
| `create_cinemachine_camera` | Create a basic Cinemachine 2.x follow/look camera with Undo. Explicitly adds the standard follow and look-at pipeline. Does not create a Brain or save. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineCommands.cs) |

### cinemachine/blends

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `set_cinemachine_custom_blends` | Create a NEW blend-table asset and bind it to the selected Brain. Preserves old tables; asset creation is not Undoable. Name-based Cinemachine blend rules are explicit. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/catalog

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_cinemachine_catalog` | Discover the installed Cinemachine component types, obsolete markers, and supported body/aim/noise choices. Real type metadata, not a hardcoded claim that every package version works. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/composition

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_composition` | Configure aim composer screen point (0..1), dead zone and damping. CM3 centered coordinates are translated explicitly. Requires composer aim selected. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/confinement

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_confiner` | Assign an existing 2D boundary or 3D volume and invalidate real caches. Requires the matching confiner extension already installed. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/dolly

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_dolly` | Assign a compatible path and normalized position 0..1 to the explicitly selected dolly body, with Undo. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `create_cinemachine_path` | Create a scene-local smooth path (CM2) or SplineContainer (CM3) from explicit local points. Undoable; no asset overwrite. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/extensions

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `add_cinemachine_extension` | Add a selected Cinemachine behavior with Undo: collider, confiner2d, confiner3d, impulse_listener, camera_offset, group_framing. Version-unsupported choices are rejected explicitly. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/groups

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_cinemachine_target_group` | Create a weighted target group, used as Follow/LookAt by group framing cameras. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `set_cinemachine_group_members` | Replace a group's explicit member list with Undo; resolve every member first. Weights/radii are nonnegative and duplicate identities are refused. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/impulse

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_cinemachine_impulse_source` | Create a uniform non-legacy impulse source with defined duration/channel. Listener extension must be explicitly installed on receiving cameras. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `emit_cinemachine_impulse` | Emit one real impulse in Play Mode. This is a live side effect, not Undoable; no retry when the result is unknown. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/lens

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_lens` | Set projection mode, clipping planes, roll and lens size with Undo. Brain may also require lens mode overrides enabled in CM3. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/live

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `prioritize_cinemachine_camera` | Move a camera to the top of its equal-priority queue in Play Mode. Does not increase its numerical priority or promise immediate blend completion. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/managers

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_cinemachine_manager` | Create a clear_shot, mixing, state_driven or sequence manager. Child cameras are explicitly parented with existing set_parent commands; no existing cameras are reparented automatically. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `set_cinemachine_mixer_weights` | Set all direct-child weights of a mixing camera (max 8) with Undo. Requires at least one positive weight. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `set_cinemachine_sequence` | Replace a sequence manager's explicit camera/hold/blend instructions. Every referenced camera must be its direct child; all inputs validated before mutation. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `set_cinemachine_state_rules` | Bind an Animator and explicit full-path state hashes to child cameras with Undo. Does not guess state names. Hash existence must be checked against the intended Animator controller. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/noise

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_noise` | Configure Perlin profile, amplitude and frequency with Undo. Requires perlin noise stage. ReSeed can be triggered explicitly. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `create_cinemachine_noise_profile` | Create a new NoiseSettings asset through the typed API. Not undoable; no overwrite. Configure amplitudes/frequencies on the camera separately. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/obstacles

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_obstacles` | Configure actual occlusion avoidance layers/radius/damping. Requires collider/deoccluder extension already installed. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/orbit

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_orbit` | Set manual orbit axes and three-ring heights/radii. Vertical is normalized 0..1 across generations; horizontal is degrees. Does not install an input driver. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `create_cinemachine_freelook` | Create a CM2 FreeLook or CM3 three-ring OrbitalFollow+RotationComposer camera. Axis values are manual until an input provider/controller is explicitly connected. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/pipeline

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_cinemachine_pipeline` | Read the actual body/aim/noise pipeline including CM2 hidden pipeline children, stage, validity and attached extensions. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `remove_cinemachine_component` | Remove a pipeline component/extension with Undo. Camera/Brain/manager removal is refused. Explicit component identity is required. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `set_cinemachine_pipeline` | Explicitly select body/aim/noise algorithms with Undo. Existing different algorithms require replace=true; no silent pipeline replacement. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/properties

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_component` | Write discovered serialized property paths with the core SerializedObject/Undo path. No guessed member names or script replacement; unsupported property kinds fail. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |
| `inspect_cinemachine_component` | Read the actual serialized properties of a Cinemachine component. Supply its exact component handle; works with package components beyond the named presets. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### cinemachine/status

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_camera_blend` | Read one Cinemachine Brain's actual output, live camera and blend state. No priority-based guesses. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineCommands.cs) |
| `inspect_cinemachine` | Read Cinemachine 2.x cameras, Follow/LookAt, priorities, lens and actual Brain live/blend state in the current stage. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineCommands.cs) |

### cinemachine/third_person

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_cinemachine_third_person` | Configure the selected third-person body: shoulder, arm, distance, side and damping. Requires third_person already selected. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Cinemachine2/CinemachineAdvancedCommands.cs) |

### editor

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `editor_focus` | Bring the Unity Editor window to the foreground | [实现](../assets/com.unity.pipeline/Editor/Commands/FocusEditorCommand.cs) |
| `editor_status` | Get detailed Unity Editor status and state information | [实现](../assets/com.unity.pipeline/Editor/Commands/EditorStatusCommand.cs) |
| `menu` | Execute an Editor menu item by path, or list available items when no path is given | [实现](../assets/com.unity.pipeline/Editor/Commands/MenuItemCommand.cs) |
| `set_autotick` | Keep the editor ticking while unfocused by forcing EditorApplication.SignalTick at a throttled rate | [实现](../assets/com.unity.pipeline/Editor/Commands/AutoTickCommand.cs) |

### editor/extensions

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_cli_extensions` | List installed extension packages and actually registered command families. Missing packages do not imply commands are available. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ExtensionObjects.cs) |

### editor/playmode

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `editor_pause` | Toggle pause state of Unity Editor play mode | [实现](../assets/com.unity.pipeline/Editor/Commands/PlayModeCommands.cs) |
| `editor_play` | Enter Unity Editor play mode | [实现](../assets/com.unity.pipeline/Editor/Commands/PlayModeCommands.cs) |
| `editor_stop` | Exit Unity Editor play mode | [实现](../assets/com.unity.pipeline/Editor/Commands/PlayModeCommands.cs) |

### gameobjects

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_gameobject` | Create an empty GameObject or a built-in primitive (cube/sphere/capsule/cylinder/plane/quad) in the active scene. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `create_gameobjects` | Batch-create N empty GameObjects or primitives in one call. Optional positions/rotations/scales are arrays of [x,y,z] (length must equal count). Returns the created identities. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `delete_gameobject` | Delete a GameObject from the scene (reversible via Undo). | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `find_gameobjects` | Find GameObjects in loaded scenes by name, tag, component type, and/or hierarchy path (filters are combined). Returns structured identities. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `rename_gameobject` | Rename a GameObject. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `set_active` | Set a GameObject's active self-state (activeSelf). | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `set_layer` | Set a GameObject's layer by name or numeric index (0-31). | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `set_parent` | Reparent a GameObject under a new parent, or detach it to scene root when no parent is given. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `set_tag` | Set a GameObject's tag (the tag must already exist in the project). | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |
| `set_transform` | Set a GameObject's local position/rotation(euler)/scale. Omitted channels are left unchanged. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/GameObjectCommands.cs) |

### gameobjects/components

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `add_component` | Add a component (by type name) to a GameObject. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/ComponentCommands.cs) |
| `get_component_properties` | Get a component's serialized properties as a JSON map. Address the component by handle, or by GameObject handle + type. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/ComponentCommands.cs) |
| `get_serialized_fields` | Read serialized fields of a component/asset. Returns each top-level field's name, type and value  | [实现](../assets/com.unity.pipeline/Editor/Commands/Scripts/SerializedFieldCommands.cs) |
| `remove_component` | Remove a component from a GameObject. Provide either a component handle (target) or a GameObject handle (target) plus a type name. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/ComponentCommands.cs) |
| `set_component_properties` | Set serialized properties on a component (one Undo step). 'properties' maps property name -> value; object references accept an ObjectRef handle. | [实现](../assets/com.unity.pipeline/Editor/Commands/GameObjects/ComponentCommands.cs) |
| `set_serialized_field` | Set a serialized field on a component/asset. Supports primitives, enums, Vector/Color/Rect/Bounds,  | [实现](../assets/com.unity.pipeline/Editor/Commands/Scripts/SerializedFieldCommands.cs) |

### input/actions

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_input_binding` | Update one binding by its stable ID, preserving the asset's other IDs/references. Requires confirm; dry_run previews. Writes .inputactions through Input System JSON and synchronously imports. Not undoable. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |
| `create_input_actions` | Create a NEW .inputactions asset from Input System JSON, validate through InputActionAsset, then import and read back. Never overwrites an asset. Does not change Player settings. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |
| `inspect_input_actions` | Read .inputactions maps, actions, binding IDs, overrides and control schemes. Does not enable an asset. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |

### input/player

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_player_input` | Read PlayerInput's actual action map, control scheme, paired devices and notification behavior. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |
| `pair_player_input_devices` | Explicitly pair selected connected devices to a live PlayerInput without unpairing existing devices. Use virtual device IDs returned by start_input_sequence when PlayerInput restricts devices. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |
| `set_player_input_map` | Switch a live PlayerInput to an existing action map; Play Mode only. Does not change the asset or project input backend. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |

### input/simulation

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `cancel_input_sequence` | Cancel the named sequence and release only tool-owned virtual controls. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSequenceCommands.cs) |
| `get_input_sequence` | Read a sequence's progress and terminal status. Queued events are not a claim that gameplay callbacks succeeded. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSequenceCommands.cs) |
| `start_input_sequence` | Run a bounded, real-time key/pointer sequence across normal Input System updates in Play Mode. Own virtual devices; one active sequence. Steps use absolute milliseconds since start. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSequenceCommands.cs) |

### input/status

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_input_system_status` | Read actual Input System version, enabled input backend, update mode and connected devices. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |
| `read_input_controls` | Read controls matching an Input System path, including virtual devices. Does not inject events or enable actions. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Input/InputSystemCommands.cs) |

### materials

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_material_properties` | Read a material's shader, render queue, enabled keywords, and all shader properties with their current values (Color as [r,g,b,a], Vector as [x,y,z,w], Texture as an object reference). | [实现](../assets/com.unity.pipeline/Editor/Commands/Materials/MaterialCommands.cs) |
| `set_material_properties` | Set shader properties on a material (Float/Range/Int=number; Color=[r,g,b,a] or "#RRGGBBAA" hex; Vector=[x,y,z,w]; Texture=an object reference or null to clear), optionally reassign the shader, set the render queue, and toggle keywords. Unknown names / type mismatches are reported in unknown[]. | [实现](../assets/com.unity.pipeline/Editor/Commands/Materials/MaterialCommands.cs) |

### materials/shaders

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_shader_properties` | Introspect a shader's declared property list (name, description, type Color\|Vector\|Float\|Range\|TexEnv\|Int, range, textureDimension, flags). Provide 'shader' (by name) OR 'material' (read the shader off that material). | [实现](../assets/com.unity.pipeline/Editor/Commands/Materials/ShaderCommands.cs) |
| `list_shaders` | Discover available shaders so an agent can pick a valid name for set_material_properties / create_asset. Returns [{ name, assetPath\|null, isBuiltin, isSupported }]. | [实现](../assets/com.unity.pipeline/Editor/Commands/Materials/ShaderCommands.cs) |

### navigation

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_selection` | Read the current Editor selection as structured object identities. | [实现](../assets/com.unity.pipeline/Editor/Commands/Navigation/NavigationCommands.cs) |
| `search` | Run a Unity Search query and return structured results. | [实现](../assets/com.unity.pipeline/Editor/Commands/Navigation/NavigationCommands.cs) |
| `set_selection` | Set the Editor selection to the given assets/scene objects. | [实现](../assets/com.unity.pipeline/Editor/Commands/Navigation/NavigationCommands.cs) |

### observability/audit

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `audit` | Run a Project Auditor static-analysis scan. Returns immediately; poll audit_status until status is 'completed', then read the CSV. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectAudit/ProjectAuditCommands.cs) |
| `audit_status` | Get the status of the last audit: idle \| scanning \| completed \| failed \| interrupted \| unavailable. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectAudit/ProjectAuditCommands.cs) |

### observability/console

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `clear_console` | Clear the captured log buffer and the Unity Editor console. | [实现](../assets/com.unity.pipeline/Runtime/Commands/ConsoleCommand.cs) |
| `console` | Get captured Unity console output (Editor or Player; supports tail, level filtering, and follow via a cursor) | [实现](../assets/com.unity.pipeline/Runtime/Commands/ConsoleCommand.cs) |
| `console_status` | Console ground truth and buffer counters without pulling entries: compile-failure flag, Editor console counts, and the buffer's retained counts and cursor. | [实现](../assets/com.unity.pipeline/Runtime/Commands/ConsoleCommand.cs) |
| `log` | Write a message to Unity console | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeLogCommand.cs) |

### observability/eval_usage

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `report_evals` | Aggregate local eval-usage telemetry into a ranked report: API fingerprint frequency, one-liner percentage, error rate, and command-coverage suggestions. | [实现](../assets/com.unity.pipeline/Runtime/Commands/EvalTelemetryCommand.cs) |

### observability/performance

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_performance_stats` | Read render, memory, and frame-timing stats (structured, read-only). | [实现](../assets/com.unity.pipeline/Editor/Commands/Observability/PerformanceCommands.cs) |

### packages

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `package_add` | Add a UPM package by name@version, git URL, or 'file:' local path. Async by default (returns  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |
| `package_list` | List packages by scope: installed (default) \| available (registry) \| all (both). Returns the full  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |
| `package_remove` | Remove a UPM package by name. Async by default (returns in_progress; poll package_status); pass  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |
| `package_resolve` | Resolve/refresh packages from the manifest (re-fetch and re-link). May trigger a recompile/domain  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |
| `package_search` | Search packages available in the registry. Provide a name (e.g. com.unity.foo) or omit to list all.  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |
| `package_status` | Status of the last async package operation (add/remove/resolve): idle \| in_progress \| completed \|  | [实现](../assets/com.unity.pipeline/Editor/Commands/PackageManager/PackageManagerCommand.cs) |

### prefabs

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `apply_prefab_overrides` | Apply a prefab instance's overrides back to its source prefab asset. | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `create_prefab` | Save a GameObject as a prefab asset at a project path; the source becomes a connected instance. | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `create_prefab_variant` | Create a prefab variant asset that inherits from a base prefab. | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `instantiate_prefab` | Instantiate a prefab asset into a loaded scene and return the created instance. | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `revert_prefab_overrides` | Revert a prefab instance's overrides so it matches its source prefab asset. | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `save_prefab_contents` | Open a prefab asset in an isolated prefab stage, apply a declarative edit, and save it back (nested-prefab safe). | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |
| `unpack_prefab` | Unpack a prefab instance into plain GameObjects (outermost level or completely). | [实现](../assets/com.unity.pipeline/Editor/Commands/Prefabs/PrefabCommands.cs) |

### rendering/camera

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_render_camera` | Edit native Camera pose, physical/projection lens, clipping, viewport and output with one Undo. Omitted fields stay unchanged. Pipeline-specific settings use their adapter. | [实现](../assets/com.unity.pipeline/Editor/Extensions/RenderCameraCommands.cs) |
| `create_render_camera` | Create a native Camera with pose/lens/output settings in one Undo step. Does not add a Cinemachine Brain, choose an input backend or change render pipeline. | [实现](../assets/com.unity.pipeline/Editor/Extensions/RenderCameraCommands.cs) |
| `inspect_render_camera` | Read actual native Camera pose/lens/clip/output state and current render pipeline. Does not infer Cinemachine or URP settings from priorities. | [实现](../assets/com.unity.pipeline/Editor/Extensions/RenderCameraCommands.cs) |

### rendering/globals

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_shader_global_driver` | Add/edit a scene runtime global profile driver with Undo. Applies on Play enable, returns compatible keys on disable/destroy. Does not save the scene or apply Edit Mode globals. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `configure_shader_global_profile` | Replace one explicit profile's complete value list and save that asset. All entries validated first; affects drivers which next acquire this profile. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `control_shader_global_driver` | Explicitly apply or restore a runtime global driver in Play Mode. Reapply requires releasing the current lease first. Not Undo. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `create_shader_global_profile` | Create a new persistent typed global profile. No overwrite, no scene save; values are applied only by an explicit scope or Play Mode driver. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `get_shader_global_scope` | Read scope terminal/active state; recent 128 records survive assembly reload in Editor SessionState. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `get_shader_globals` | Read explicitly named, typed native Shader globals. Unity cannot enumerate all globals. This does not inspect material-local overrides. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `inspect_shader_global_driver` | Read persistent profile, runtime lease state and actual explicitly declared global values. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `inspect_shader_global_profile` | Read an explicit persistent profile's typed values without applying globals. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `restore_shader_globals` | End a temporary scope. Restore only keys still equal to this scope's native last write; report different external values as conflicts and preserve them. Same-value/ABA writes are not identifiable. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |
| `set_shader_globals` | Apply a temporary typed global scope after validating all entries. Returns scope ID; restore/Play transition/reload returns compatible keys to baseline. Observable different external writes are preserved. Not Undo or persistent. | [实现](../assets/com.unity.pipeline/Editor/Extensions/ShaderGlobalCommands.cs) |

### rendering/ssu

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `assign_ssu_renderer_material` | Assign or clear one explicit Renderer/SpriteRenderer shared material slot with Undo. Never calls Renderer.material or clones hidden materials; preserves other slots. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuRendererCommands.cs) |
| `assign_ssu_ui_material` | Assign or clear a GUI SSU material asset on an exact Graphic with Undo. Optional original SSU size bridge owns only a derived runtime material; labels/children and source assets stay unchanged. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/SsuUiCommands.cs) |
| `configure_ssu_effects` | Configure installed SSU effects by exact _Enable property or suffix and parameters. All edits validate before mutation; edits both Toggle float and actual shader keyword. Editor asset authoring only. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuMaterialCommands.cs) |
| `configure_ssu_material` | Edit explicit SSU material asset properties atomically after complete typed validation, synchronizing declared Toggle/KeywordEnum local keywords. Saves asset, Edit Mode only; no partial unknown-member acceptance. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuMaterialCommands.cs) |
| `configure_ssu_ui_bridge` | Configure original SSU Graphic bridge enable/Rect/atlas synchronization with Undo. Native material-modifier order and vendor-owner conflicts are validated before enabling. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/SsuUiCommands.cs) |
| `create_ssu_material` | Create a new material using one explicitly installed SSU shader, with fully validated typed properties and native Toggle/KeywordEnum sync. Edit Mode, no overwrite. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuMaterialCommands.cs) |
| `get_ssu_catalog` | Discover installed SSU shader variants and actual effects/properties/keywords. Does not ship/install vendor assets or guess SSU version from ASE. Missing SSU returns installed=false. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuMaterialCommands.cs) |
| `inspect_ssu_material` | Read actual SSU asset effect Toggle floats separately from enabled local keywords, including mismatches. Optional property names return actual typed values. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuMaterialCommands.cs) |
| `inspect_ssu_renderer` | Read actual Renderer/SpriteRenderer material slot identities and SSU keyword state without instantiating material copies. PropertyBlock presence is explicit; no claim it matches asset values. | [实现](../assets/com.unity.pipeline/Editor/Extensions/SsuRendererCommands.cs) |
| `inspect_ssu_ui` | Read source/direct material, already submitted Canvas material and this bridge's owned/borrowed instances separately. Does not invoke materialForRendering or allocate stencil materials. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/SsuUiCommands.cs) |
| `set_ssu_ui_parameters` | Set temporary typed parameters on an active bridge-owned material in Play Mode, preserving them across Rect/mask rebuilds. Does not edit assets or feature keywords; no save/Undo. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/SsuUiCommands.cs) |

### rendering/timeline

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `bind_timeline_track` | Bind one exact Timeline TrackAsset handle to its required scene object type, or explicitly clear it. Validates ownership/output type before one Undo edit. Duplicate names are never guessed. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |
| `configure_timeline_animation_clip` | Edit one exact native AnimationPlayableAsset's removeStartOffset, clip pose offsets and foot IK, then save its owning asset. removeStartOffset=true subtracts the source first-frame pose; Camera paths commonly require explicit false. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |
| `configure_timeline_animation_track` | Edit exact native AnimationTrack root offset mode/pose and persist its Timeline asset. Explicit mode avoids guessing initial camera/actor placement; no scene sampling. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |
| `configure_timeline_director` | Add/edit a native PlayableDirector on a loaded object, binding an explicit Timeline asset with Undo. Edit Mode only; does not start playback or save. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |
| `control_timeline_director` | Play/pause/stop/seek/evaluate a native Director in stable Play Mode only. Uses its native clock; seek evaluates at an explicit finite time. Not Undo; stop uses Timeline's native restoration semantics. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |
| `inspect_timeline_director` | Read actual Director time/state, Timeline tracks with exact handles, output binding types and scene bindings. Does not build/evaluate a graph. | [实现](../assets/com.unity.pipeline/Editor/Extensions/Timeline/DirectorCommands.cs) |

### rendering/urp

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_urp_camera` | Add/edit UniversalAdditionalCameraData with Undo. Typed post-processing, AA, textures, volume mask/trigger and validated Base-to-Overlay stack. Does not change project pipeline. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpCameraCommands.cs) |
| `configure_urp_effect` | Add/edit a typed native Volume override and save this explicit shared Profile. Bloom/color/vignette/tonemapping/DOF/chromatic/lens distortion/motion blur. All supplied fields validated; omitted parameters and override states preserved. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpVolumeCommands.cs) |
| `configure_urp_fullscreen_pass` | Create/edit the official FullScreenPassRendererFeature on one explicit renderer asset. Keeps subasset identity/map; uses native URP14 execution or URP17 RenderGraph. Saves only this renderer; material is shared, never cloned. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpFullScreenCommands.cs) |
| `configure_urp_pipeline` | Edit one explicit URP asset's public HDR/depth/opaque texture/MSAA/render scale/shadow-distance settings and save that asset. Does not install/select a pipeline. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpCameraCommands.cs) |
| `configure_urp_volume` | Add/edit a native Volume on a loaded object with an explicit shared Profile. Changes scene only, with Undo; does not call Volume.profile or save. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpVolumeCommands.cs) |
| `create_urp_fullscreen_shader` | Write/import a new editable URP Full Screen Pass shader template. Includes global tint/intensity and invert keyword, consumes official _BlitTexture. No overwrite; returns actual compiler messages. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpFullScreenCommands.cs) |
| `create_urp_pipeline` | Create a new URP asset and Universal Renderer from the installed package's native renderer template. Does not select the project/Quality pipeline or overwrite assets. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpCameraCommands.cs) |
| `create_urp_volume_profile` | Create a new empty native VolumeProfile asset. Effects are explicit subassets; no overwrite or implicit shared profile clone. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpVolumeCommands.cs) |
| `inspect_shader_compilation` | Read actual ShaderUtil compile errors/warnings and support state for an explicit Shader asset. Does not treat missing Shader Graph compiler state as success. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpFullScreenCommands.cs) |
| `inspect_urp` | Read current/default/Quality pipeline assets, configured renderer mapping and camera/Volume facts. Does not allocate renderers or Volume.profile instances; GPU use is verified separately. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpCameraCommands.cs) |
| `inspect_urp_fullscreen_pass` | Read exact native feature material/pass/injection/requirements, renderer list index and persisted feature localId. No renderer creation. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpFullScreenCommands.cs) |
| `inspect_urp_volume_profile` | Read typed Volume values, per-parameter overrideState and component.active without instantiating Volume.profile or changing blending state. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpVolumeCommands.cs) |
| `remove_urp_fullscreen_pass` | Remove one exact official FullScreenPass feature and its own renderer subasset. Preserve other features and shared pass material; saves renderer. Not Undo. | [实现](../assets/com.unity.pipeline/Editor/Extensions/URP/UrpFullScreenCommands.cs) |

### runtime

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `runtime_status` | Get comprehensive runtime application status | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeStatusCommand.cs) |

### runtime/application

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `quit` | Gracefully quit the Unity application | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeApplicationCommand.cs) |
| `set_target_framerate` | Set the target frame rate for the application | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeApplicationCommand.cs) |
| `set_timescale` | Set the time scale for the application | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeApplicationCommand.cs) |

### runtime/input

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `simulate_key` | Simulate a keyboard key event (Input System). Drives the running app. | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeInputCommand.cs) |
| `simulate_pointer` | Simulate a mouse/pointer event at screen coordinates (Input System). | [实现](../assets/com.unity.pipeline/Runtime/Commands/RuntimeInputCommand.cs) |

### scenes

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `create_scene` | Create a new scene and save it to the given path under the authoring root. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `get_scene_hierarchy` | Return the GameObject tree of an open scene (or the active scene). Each node carries instanceId + hierarchyPath usable by GameObject commands. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `list_open_scenes` | List all currently open scenes with their load/active/dirty state. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `open_scene` | Open an existing scene from the given path. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `save_all` | Save all open scenes that have unsaved changes. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `save_scene` | Save an open scene. Saves the active scene when no path is given. | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |
| `set_active_scene` | Set which open scene is the active scene (new objects are created in the active scene). | [实现](../assets/com.unity.pipeline/Editor/Commands/Scenes/SceneCommands.cs) |

### scripts

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `attach_script` | Add a MonoBehaviour to a GameObject by its (compiled) type name OR by its script asset path.  | [实现](../assets/com.unity.pipeline/Editor/Commands/Scripts/AttachScriptCommand.cs) |
| `create_script` | Create a new C# script (default base class MonoBehaviour) from a template under the authoring root.  | [实现](../assets/com.unity.pipeline/Editor/Commands/Scripts/CreateScriptCommand.cs) |

### scripts/codereload

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `cleanup_codereload` | Remove old code reload DLL versions and clear registry | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeReloadCommands.cs) |
| `codereload_status` | Show current code reload registry status and statistics | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeReloadCommands.cs) |
| `reload_file` | Compile and apply in-place [CodeReload] edits from a source file | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeReloadCommands.cs) |
| `reload_file_editor_interpreter` | Compile in-place [CodeReload] edits and run them through the IlInterpreter VM in this process instead of Assembly.Load (IL2CPP-safe; only a minimal host API + the target type are available) | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeReloadCommands.cs) |
| `reload_file_player_interpreter` | Compile a file's (or folder's) [CodeReload] method(s) and push the IL to a connected player (IL2CPP-safe) over PlayerConnection. Folders are walked recursively and pushed once. | [实现](../assets/com.unity.pipeline/Editor/Commands/PushReloadCommand.cs) |

### scripts/compile

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `recompile` | Force a script recompile (works while unfocused/minimized). Poll recompile_status for completion. | [实现](../assets/com.unity.pipeline/Editor/Commands/RecompileCommand.cs) |
| `recompile_status` | Get the status of the last recompile: idle \| triggered \| compiling \| completed \| up_to_date. | [实现](../assets/com.unity.pipeline/Editor/Commands/RecompileCommand.cs) |

### scripts/eval

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `eval` | Evaluate C# code dynamically using Roslyn compiler | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeEvalCommand.cs) |
| `eval_file` | Evaluate C# code read from a .cs file on disk | [实现](../assets/com.unity.pipeline/Runtime/Commands/CodeEvalCommand.cs) |
| `run_script` | Compile a project C# file in memory (no domain reload) and execute a named static entry point.  | [实现](../assets/com.unity.pipeline/Editor/Commands/Scripts/RunScriptCommand.cs) |

### settings/audio

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_audio_settings` | Read project Audio settings (volume, rolloff scale, doppler factor). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/AudioSettingsCommands.cs) |
| `set_audio_settings` | Change project Audio settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/AudioSettingsCommands.cs) |

### settings/graphics

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_graphics_settings` | Read GraphicsSettings (default render pipeline). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/GraphicsSettingsCommands.cs) |
| `set_graphics_settings` | Set the default render pipeline asset. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/GraphicsSettingsCommands.cs) |

### settings/input

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_input_settings` | Read the legacy Input Manager axes (names and count). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/InputSettingsCommands.cs) |
| `set_input_settings` | Tune a legacy Input Manager axis (sensitivity/gravity/dead) by name. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/InputSettingsCommands.cs) |

### settings/physics

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_physics_settings` | Read Physics settings (gravity, solver iterations, bounce threshold). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/PhysicsSettingsCommands.cs) |
| `set_physics_settings` | Change Physics settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/PhysicsSettingsCommands.cs) |

### settings/player

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_player_settings` | Read PlayerSettings (company/product/version, scripting backend, API level). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/PlayerSettingsCommands.cs) |
| `set_player_settings` | Change PlayerSettings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. Scripting backend / API level changes trigger a domain reload. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/PlayerSettingsCommands.cs) |

### settings/quality

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_quality_settings` | Read QualitySettings (current level, level names, vSync, anti-aliasing). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/QualitySettingsCommands.cs) |
| `set_quality_settings` | Change QualitySettings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/QualitySettingsCommands.cs) |

### settings/runtime_pipeline

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_runtime_pipeline_settings` | Read Pipeline Runtime settings (enableInBuilds, port, requestTimeoutMs, enableAuditLogging, autoStart, maxWorkItemsPerFrame). Reports the settings in effect, falling back to built-in defaults when none have been authored; never creates the settings file. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/RuntimePipelineSettingsCommands.cs) |
| `set_runtime_pipeline_settings` | Change Pipeline Runtime settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. Refused while in Play Mode: the running driver already loaded its own settings, so a change would silently not apply until the next Play session or build. port, requestTimeoutMs, and maxWorkItemsPerFrame are rejected outright if outside the same bounds the Project Settings page enforces. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/RuntimePipelineSettingsCommands.cs) |

### settings/tags_layers

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_tags_layers` | Read the project's tags and (named) layers. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/TagsLayersCommands.cs) |
| `set_tags_layers` | Add/remove tags and assign user layer names (index 8-31). Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/TagsLayersCommands.cs) |

### settings/time

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `get_time_settings` | Read Time settings (fixedDeltaTime, maximumDeltaTime, timeScale). | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/TimeSettingsCommands.cs) |
| `set_time_settings` | Change Time settings. Requires confirm=true; use dry_run to preview. Not undoable via Ctrl+Z. | [实现](../assets/com.unity.pipeline/Editor/Commands/ProjectSettings/TimeSettingsCommands.cs) |

### tests

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `cancel_tests` | Cancel running test execution | [实现](../assets/com.unity.pipeline/Editor/Commands/TestCommands.cs) |
| `list_tests` | List all available tests (EditMode and/or PlayMode) without running them | [实现](../assets/com.unity.pipeline/Editor/Commands/TestCommands.cs) |
| `run_tests` | Execute Unity tests with filtering options | [实现](../assets/com.unity.pipeline/Editor/Commands/TestCommands.cs) |
| `test_status` | Get status of running async test execution | [实现](../assets/com.unity.pipeline/Editor/Commands/TestCommands.cs) |

### ui/authoring

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_ui_canvas` | Configure a Canvas mode, explicit camera, plane distance and sorting atomically with Undo. Omitted values stay unchanged. No scene save or input-system mutation. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UiStructureCommands.cs) |
| `configure_ui_canvas_group` | Add/edit native CanvasGroup opacity, interaction and raycast propagation with Undo. No input backend or scene save. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UiStructureCommands.cs) |
| `configure_ui_layout` | Add/edit native horizontal, vertical or grid layout with Undo. Same type preserves unspecified settings; changing type requires replace=true. Does not force layout rebuild. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiBuilderCommands.cs) |
| `configure_ui_layout_element` | Add/edit LayoutElement size preferences and flexible weights with Undo. Omitted values stay unchanged; -1 means unspecified. Parent layout determines final geometry. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiBuilderCommands.cs) |
| `configure_ui_rect` | Edit RectTransform anchors, offsets and 3D local pose atomically with Undo. Do not mix offsets with size/position, or local with anchored position. No layout rebuild. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UiStructureCommands.cs) |
| `create_ui_canvas` | Create a Canvas/RectTransform in the current stage. Camera and WorldSpace modes require an explicit camera. Does not add or change EventSystem, scaler or raycaster. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UiStructureCommands.cs) |
| `create_ui_element` | Build empty/panel/image/button under an explicit RectTransform. Button creates its actual label child and requires an explicit Font. All creation is one Undo step; no EventSystem mutation. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiBuilderCommands.cs) |

### ui/components

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_ui_components` | Read uGUI scaler, layout, clipping, graphics and common controls. Can include the ancestor chain. No layout rebuild or state change. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiComponentCommands.cs) |
| `set_ui_control` | Set common uGUI control state with Undo. Callback notification is off by default; Button clicks require the input path. Does not save. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiAuthoringCommands.cs) |

### ui/hit_test

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_ui_point` | Run EventSystem.RaycastAll at a screen point and return its actual ordered hit stack. No click is sent. Draw visibility and event-handler consumption are separate facts. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiComponentCommands.cs) |

### ui/layout

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_ui_order` | Set sibling order and/or an explicit Canvas sorting layer/order with Undo. No inferred global draw ranking, no save. Sorting fields require a Canvas on the target itself. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UiOrderCommands.cs) |
| `configure_ui_scaler` | Configure an existing CanvasScaler using typed API with Undo. Does not resize Game View or save. Query geometry after Unity's next layout pass. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiAuthoringCommands.cs) |
| `explain_ui_layout` | Read a RectTransform and its complete parent chain, including non-UI parents, the recorded driver, Canvas sorting and CanvasGroups. Does not force layout or change objects. | [实现](../assets/com.unity.pipeline/Editor/Commands/UI/UiInspectionCommands.cs) |
| `find_ui_overlaps` | Find screen-space AABB intersection candidates in a bounded RectTransform subtree. Geometry only: not proof of pixel occlusion or blocked clicks. Ancestor/descendant pairs are excluded by default. | [实现](../assets/com.unity.pipeline/Editor/Commands/UI/UiInspectionCommands.cs) |
| `get_ui_snapshot` | Read RectTransform geometry and Canvas sorting facts in the current stage. Read-only; does not force layout. Bounds are geometric, not visibility or hit-test results. Coordinates use the screen bottom-left origin. | [实现](../assets/com.unity.pipeline/Editor/Commands/UI/UiInspectionCommands.cs) |

### ui/shape

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `configure_ui_shape` | Edit shape/gradient/border/texture/material with one Undo. Omitted settings stay unchanged. Invalid final geometry rolls the complete edit back. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiShapeCommands.cs) |
| `create_ui_shape` | Create a native convex MaskableGraphic or complete shape Button/Label tree. Geometry and raycasts share the same outline; text stays upright. No input-system changes. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiShapeCommands.cs) |
| `inspect_ui_shape` | Read native shape state, current local outline and generated Canvas mesh counts. No layout rebuild; includes maskability, bound Button and material facts. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiShapeCommands.cs) |

### ui/text

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `inspect_tmp_text` | Read TMP text, sizing, measured overflow, font and input field state without forcing a mesh or layout rebuild. | [实现](../assets/com.unity.pipeline/Editor/Extensions/TMP/TmpCommands.cs) |
| `inspect_ui_text` | Inspect legacy uGUI Text and InputField. TMP has inspect_tmp_text with its own schema. | [实现](../assets/com.unity.pipeline/Editor/Extensions/UGUI/UiComponentCommands.cs) |
| `set_tmp_input` | Set TMP_InputField text/read-only state with Undo. Value-change notification defaults off; does not emulate typing or submit. | [实现](../assets/com.unity.pipeline/Editor/Extensions/TMP/TmpCommands.cs) |
| `set_tmp_text` | Set TMP display text, size and autosizing with Undo; no save or forced layout. Use a subsequent inspect after layout updates. | [实现](../assets/com.unity.pipeline/Editor/Extensions/TMP/TmpCommands.cs) |

### wait

| 命令 | 说明 | 源码 |
| --- | --- | --- |
| `wait_cancel` | Cancel an async wait started with wait_for (async=true). | [实现](../assets/com.unity.pipeline/Editor/Commands/WaitForCommands.cs) |
| `wait_for` | Wait server-side until a member condition holds, then optionally act in the same frame. Sync waits hold the exec queue (every other command waits behind them, including one the condition may depend on) — keep them short and self-contained; set async=true and poll wait_status for anything else. | [实现](../assets/com.unity.pipeline/Editor/Commands/WaitForCommands.cs) |
| `wait_status` | Get the status/result of an async wait started with wait_for (async=true). | [实现](../assets/com.unity.pipeline/Editor/Commands/WaitForCommands.cs) |

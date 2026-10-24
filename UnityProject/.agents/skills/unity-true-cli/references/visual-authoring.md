# URP 视觉制作 CLI

本组命令为镜头演示、非矩形 UI 和全局效果提供正式制作入口。它们操作 Unity 原生 Camera、Timeline、MaskableGraphic、VolumeProfile 和官方 Full Screen Pass；通过 `get_cli_extensions` 查看工程实际已加载的包和命令，再查当前 schema。

依赖按工程现有安装启用。URP adapter 面向 URP14..17，实测精确版本见兼容矩阵；Timeline adapter 需要 Timeline1.7+；Shape 需要 uGUI。工具不自动升级包，也不为了运行效果暗中切换 URP RenderGraph／兼容模式。新视觉运行程序集独立于 CLI 调试协议开关。

## 快速制作入口

| 能力 | 命令 |
| --- | --- |
| 原生镜头 | `create_render_camera`、`configure_render_camera`、`inspect_render_camera` |
| 镜头时间线接线 | `configure_timeline_director`、`bind_timeline_track`、`inspect_timeline_director`、`control_timeline_director`、`configure_timeline_animation_track`、`configure_timeline_animation_clip` |
| 斜边与形状按钮 | `create_ui_shape`、`configure_ui_shape`、`inspect_ui_shape`、`configure_ui_canvas_group` |
| 全局参数／临时恢复 | `get_shader_globals`、`set_shader_globals`、`get_shader_global_scope`、`restore_shader_globals` |
| 持久全局配置 | `create_shader_global_profile`、`configure_shader_global_profile`、`inspect_shader_global_profile`、`configure_shader_global_driver`、`inspect_shader_global_driver`、`control_shader_global_driver` |
| URP 环境／相机 | `inspect_urp`、`create_urp_pipeline`、`configure_urp_pipeline`、`configure_urp_camera` |
| 原生后处理 | `create_urp_volume_profile`、`configure_urp_volume`、`configure_urp_effect`、`inspect_urp_volume_profile` |
| 官方全屏效果 | `configure_urp_fullscreen_pass`、`inspect_urp_fullscreen_pass`、`remove_urp_fullscreen_pass`、`create_urp_fullscreen_shader`、`inspect_shader_compilation` |

与 [UI 基础搭建](ui-authoring.md)、现有材质／动画资产命令以及 [Cinemachine 的 33 项命令](cinemachine.md) 组合使用。以下为传给官方 `command` 的参数片段；通过 `Invoke-Unity.ps1 -ProjectPath ... -UnityArgs @(...)` 执行，句柄替换为工程实际 ObjectRef。

## 斜边 UI

```text
command create_ui_shape --name Launch --parent '{"instanceId":1234}' --rect '{"size_delta":[280,100]}' --settings '{"shape":"chamfer","cuts":[20,8,20,8],"tint":[0.03,0.18,0.4,1],"gradient_end":[0.1,0.75,0.9,1],"gradient_angle":30,"border_width":2,"border_tint":[0.3,0.95,1,1]}' --button true --font '{"path":"Assets/Fonts/MyFont.ttf"}' --text LAUNCH --json
```

`shape` 支持 `chamfer`、`parallelogram`、`trapezoid`、`polygon`。`cuts` 为本地单位的 TL/TR/BR/BL 四角切角；`skew` 是 -.5.. .5（不含端点）的宽度比例；`insets` 为梯形顶部／底部的收进比例（0.. .5，不含 .5）。polygon 的 `points` 是 0..1 Rect 归一化坐标，3..64 个逆时针、严格凸、无自交顶点。所有点保持在 Rect 内，避免原生射线矩形预筛造成可见但不可点击的凸出区域。

运行组件从同一轮廓生成顶点和点击过滤，透明切角允许命中下层。父级 Mask、RectMask2D、CanvasGroup 沿 uGUI 原生机制生效。Label 为独立正常 Rect，不把文字网格斜扭。材质可显式传 `material`，纹理可传 `texture`，清除用 `clear_material/clear_texture=true`；自定义材质仍须遵守原生 UI stencil／clip／alpha 合同。

创建／修改时显式超大切角或无有效内轮廓的边框会拒绝。后续布局缩窄时切角同比缩小，过厚边框饱和为填满外轮廓；仍能编辑颜色等省略几何字段。`inspect_ui_shape` 返回实际轮廓、当前生成网格数量、饱和状态和 Button 绑定。读命令不强制布局刷新，应等 Unity 正常更新后再看。

## Shader 全局参数

```text
command set_shader_globals --entries '[{"name":"_WindStrength","type":"float","scalar":0.8},{"name":"_TrueCliGlobalTint","type":"color","color":[0.3,1,0.55,1]},{"name":"_TrueCliGlobalIntensity","type":"float","scalar":1}]' --json
command get_shader_globals --entries '[{"name":"_WindStrength","type":"float"}]' --json
command restore_shader_globals --id '<返回的scope ID>' --json
```

类型及对应值字段为 `float/scalar`、`integer/integer`、`vector/vector[4]`、`color/color[4]`、`matrix/matrix[16]`（行优先）、`texture/texture ObjectRef`（或 `clear_texture=true`）、`keyword/keyword bool`。Integer 使用真正的 SetGlobalInteger API，保持 32 位整数精度。颜色允许 HDR 值，所有数必须有限。一次数组在全部校验后写入，重复 property 名不能借不同 type 避开冲突；keyword 使用独立域。

全局变量不能完整枚举，查询必须提供名字和类型。材质 Properties 中同名的局部属性可以覆盖全局参数；全局 keyword 与 material local keyword 也有各自的 Unity 语义。设置 global 不自动产生全屏后处理，更不会替任意 Shader 添加消费者。

每次 set 返回临时 scope ID；Play 状态转换、程序集重载或显式 restore 会结束它。只归还本 scope 仍占有、且当前值等于原生末次写入的键；可观测的不同值外写保留并报告 `conflicted`。外部同值写入或 ABA 无法靠值比较识别，须显式协调所有权；恢复 keyword 启用位不表示删去 Unity 已注册的 keyword 表项。上限为32活跃scope、每scope128键，近期128条终态在当前 Editor SessionState 中保留。

持久配置先建 `GlobalShaderProfile`，用 `configure_shader_global_driver` 绑定场景对象并显式保存场景。driver 在 Play 启用时申请参数、禁用／销毁时归还；未激活／禁用 driver 不能申请。编辑时不自动改全局状态，预览使用临时 scope。同工具重叠活跃 owner 拒绝，避免多个配置互相抢写。profile 的替换影响下一次申请，不会偷偷夺走正在运行的 lease。

## URP Volume 与全屏 Pass

`create_urp_pipeline` 创建明确路径的 Pipeline＋Universal Renderer 资产，复用已安装官方模板；不会自动选为项目或 Quality 管线。需要选择时使用现有 `set_graphics_settings`／Quality 设置入口并回读 `inspect_urp`：Quality override 可以覆盖默认管线。`inspect_urp` 只读配置映射，不调用会初始化 renderer 的 getter，也不访问会复制 profile 的 `Volume.profile`。

`configure_urp_camera` 支持原生后处理、AA、纹理请求、volume mask/trigger、renderer index 和 Base→Overlay stack。输入的最终堆栈必须类型兼容，即使只换 renderer、没有重传 stack 也会检查现有消费者。`configure_urp_pipeline` 对显式资产配置 HDR、深度／颜色纹理、MSAA、render scale 和 shadow distance。

先创建 VolumeProfile，再将其绑定到显式 Volume 对象，最后编辑覆盖项：

```text
command configure_urp_effect --profile '{"path":"Assets/Look.asset"}' --settings '{"effect":"bloom","intensity":0.8,"threshold":0.9,"scatter":0.6,"active":true}' --json
command configure_urp_effect --profile '{"path":"Assets/Look.asset"}' --settings '{"effect":"color","saturation":-20,"post_exposure":0.25}' --json
```

支持 `bloom/color/vignette/tonemapping/dof/chromatic/lens_distortion/motion_blur`。每个提供值默认同时开启该参数 override，使用 `override_state=false` 可显式关闭提供字段的 override；省略字段保留。查询返回每参数 value/overrideState 与 component.active。效果还取决于 Volume 权重、局部 Collider、Camera mask 和 renderPostProcessing；数值写入成功不能代替画面生效。共享 profile 修改会影响所有使用它的 Volume，私有实例 profile 不被此入口默默替换。

`create_urp_fullscreen_shader` 将可编辑模板写到新的项目路径并同步导入，查询实际编译消息。模板消费 `_BlitTexture`、global `_TrueCliGlobalTint/_TrueCliGlobalIntensity` 和可选 `_TRUECLI_INVERT` keyword；默认全局 intensity=0 为无变化。用现有 `create_asset --type Material --shader ...` 创建材质，再通过 `configure_urp_fullscreen_pass` 传明确 rendererData、材质、pass index 和 injection point。它使用官方 URP14 Execute／URP17 RenderGraph 实现；不自造渲染循环。

已有 feature 更新用精确 ObjectRef，保 localId 和 list/map 对应。移除支持 `dry_run=true`，真正删除须 `confirm=true`，只删该 renderer 的 feature 子资产，保共享材质和其他 feature。Shader 编译成功、材质被引用和 GPU 实际效果是三项不同证据。

## 镜头与 Timeline

Camera `settings` 提供世界 position/rotation/look_at、FOV 或 physical focal_length、sensor/lens shift、near/far、viewport、HDR/MSAA、背景和 target RenderTexture。省略值保留；同次 FOV 与 focal_length 互斥。URP 专属部分另用 `configure_urp_camera`。

复用 `create_animation_clip/set_animation_curve/create_timeline/add_timeline_track/add_timeline_clip` 制作资产。`configure_timeline_director` 在场景对象上接 PlayableDirector；`inspect_timeline_director` 返回实际 TrackAsset 句柄和要求的绑定类型；`bind_timeline_track` 用精确轨道与目标组件句柄，GameObject 存在多个同类型组件时要求显式组件。原资产命令遇到同名轨道会拒歧义，创建返回 Unity 实际去重后的名称和句柄。

`control_timeline_director` 仅在稳定 Play Mode 支持 `play/pause/stop/seek/evaluate`，使用原生时钟；seek 同时 Evaluate，时间有限且处于 Timeline 时长内。stop 使用 Timeline 原生恢复语义，不承诺覆盖任意外部组件写入。Cinemachine Brain／Animator 都可能驱动真实相机，按项目实际驱动者接线并查最终 Camera／Brain 状态，不能只看时间值或优先级。

根运动／相机路径须明确偏移合同：`configure_timeline_animation_track` 编辑原生 track offset mode 与位姿，`configure_timeline_animation_clip` 编辑实际 AnimationPlayableAsset（不是源 .anim）的 remove_start_offset、位姿偏移与 foot IK。Timeline 的 removeStartOffset 会减去源动画第一帧位置，即使源曲线写了固定 z=-10 也可能被减为0；绝对镜头路径通常应显式关闭该项，并配置 TrackOffset 后核实际完整位姿。

资产保存、Shader globals 和播控不能由 Unity Undo 完整回滚，已纳入 batch 非事务清单；创建场景对象、Shape、镜头／Director 绑定等支持 Undo。当前完成用例、精确版本及边界见[本次记录](verification-0.8.0.json)；仓库 docs/verification 为不随包分发的历史材料。原始失败、修正及图像保留在本机构建工作区。未验证范围继续明确保留，不用本组命令宣称所有视觉风格或所有 Player 已通过。

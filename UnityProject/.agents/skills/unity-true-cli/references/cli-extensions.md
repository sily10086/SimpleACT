# CLI 扩展能力

URP 视觉制作、斜边 UI 与 Shader 全局参数见 [视觉制作 CLI](visual-authoring.md)。

所有入口使用官方 CLI 的 `command <命令>`，参数由当前 Editor 的 schema 发现，不需要重编 CLI。先确认工程已安装本版 Pipeline：

```powershell
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<工程>' -EditorPath '<精确Unity.exe>' `
  -UnityArgs @('command', 'get_cli_extensions', '--json')
```

可选包没安装时，对应适配程序集不编译，命令不会出现。`get_cli_extensions` 会列出实际包版本和已注册命令，不用手工推断。工具不会自动安装或升级 Cinemachine、uGUI、TMP，也不会自动切换项目输入后端。

旧工程更新 Skill 后，请先保存并关闭目标 Editor，再显式执行：

```powershell
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<工程>' -EditorPath '<精确Unity.exe>' `
  -UpgradePipeline -InstallOnly
```

升级保留旧包目录备份，重新导入并验证安装结果。不会自动关闭已有 Editor。若工程使用另一个来源的 Pipeline，应先核对包来源和目标路径，不能把本地文件复制当成当前服务已加载新版的证据。

## UI、TextMeshPro 与速查

复杂布局与 3D 面板的 Canvas/Rect、元素树和原生布局创建入口见 [UI 搭建](ui-authoring.md)。

| 命令 | 用途 |
| --- | --- |
| get_ui_snapshot / explain_ui_layout | RectTransform、父级缩放、Canvas/CanvasGroup 和排序事实，见 [UI 核心说明](ui-inspection.md)。 |
| find_ui_overlaps | 有界屏幕 AABB 相交候选；不直接下像素遮挡结论。 |
| inspect_ui_components | uGUI 的 CanvasScaler、LayoutGroup/Grid、Fitter、Mask、Graphic、Image、Selectable、Toggle、Slider、ScrollRect、GraphicRaycaster；可追到父级。 |
| inspect_ui_point | EventSystem 的实际有序射线命中栈与点击处理器候选；无事件发送，无点击成功承诺。多个 EventSystem 且无 current 时必须指定目标。 |
| inspect_ui_text / inspect_tmp_text | 旧 Text/InputField 与 TMP 的独立查询，包含字号、溢出、字体/材质与输入框状态。 |
| set_ui_control | 设置旧文本/输入框、Toggle、Slider、ScrollRect、interactable；默认不触发值变化通知。 |
| set_tmp_text / set_tmp_input | 修改 TMP 显示或输入框；支持 Undo；输入框默认不通知回调。 |
| configure_ui_scaler / configure_ui_order | 配置实际缩放参数、sibling 顺序或明确 Canvas 的排序；不猜跨 Canvas 的统一绘制排名。 |

读操作不强制刷新布局。刚修改后，应等 Unity 真实布局更新，再查询与截图。TMP 无字体或 Essentials 时返回真实状态，不替工程补字体；几何重叠、绘制遮挡、点击拦截分别核对。

## Input System

| 命令 | 用途 |
| --- | --- |
| get_input_system_status / read_input_controls | 实际输入后端、更新模式、设备、PlayerInput 和当前控件值。 |
| inspect_input_actions / inspect_player_input | Maps、Actions、绑定 ID/覆盖、Control Schemes、用户设备和当前 Map。 |
| create_input_actions | 用 Input System JSON/API 校验并创建新 .inputactions，禁止覆盖已有文件；含导入后回读。 |
| configure_input_binding | 按绑定 GUID 局部改 path/groups/interactions/processors，保留现有身份；支持 dry_run，写入需 confirm。 |
| set_player_input_map / pair_player_input_devices | 显式切换实时 Map、为 PlayerInput 追加指定设备配对，保留已有配对。 |
| start_input_sequence / get_input_sequence / cancel_input_sequence | 跨真实游戏帧的按键、长按和鼠标操作。 |

序列示例：按住 Space 半秒后释放。`at_ms` 是本次序列的绝对起始偏移，`keys` 表示工具当前完整按键集合；省略则保持前一步。

```text
command start_input_sequence --steps '[{"at_ms":0,"keys":["Space"]},{"at_ms":500,"keys":[]}]' --json
```

指针步骤支持 x/y（左下原点）和 left；例如先指定坐标并 left=true，后一步 left=false。一次输入更新最多应用一步，跨游戏帧推进；延迟会推迟操作，不把按下/释放压进同一帧。始终保留返回的操作 ID，结果不明时查原 ID。

这些操作只驱动 Play Mode。工具创建独立虚拟 Keyboard/Mouse，停止时释放自己的设备，不重置物理设备。受 PlayerInput/Control Scheme 限制的项目需使用返回的 device ID 显式配对；这不会假装所有玩家自动接收工具输入。游戏未泵送输入更新时会超时并释放；取消、退出 Play 或重载会终止。

## Cinemachine 2 / 3

0.3.0 已将本节的基础 5 项扩展为每代 33 个命令，涵盖管线、第三人称、构图、环绕、目标组、轨道、边界/避障、噪声/冲击、镜头管理及自定义混合。完整列表、两代差异和已知边界见 [Cinemachine CLI](cinemachine.md)。

| 命令 | 用途 |
| --- | --- |
| inspect_cinemachine | 实际包版本、相机、目标、优先级和 Brain 实际 Live/Blend。 |
| create_cinemachine_camera | 创建基本跟随/注视相机与对应代际的标准管线，不创建 Brain。 |
| configure_cinemachine_camera | Follow/LookAt、显式清空、优先级、镜头与已有基本 Follow 组件的偏移；不替换其他管线。 |
| inspect_camera_blend / configure_cinemachine_brain | 回读 Brain 的真实输出与混合，修改默认混合时长/曲线；自定义混合规则可能覆盖默认设置。 |

两代使用独立强类型适配，同名 CLI schema 保持一致。创建 Brain 可复用 `add_component`，类型分别为 `Cinemachine.CinemachineBrain` 和 `Unity.Cinemachine.CinemachineBrain`。高级能力使用新专用入口；其余配置可先发现组件真实属性路径再编辑，不承诺所有组合已经逐项验收。

优先级写入后，等待相机真实更新，再读 Brain 和画面。虚拟混合对象可能没有 Unity 对象句柄，此时保留 name，不能把缺句柄当作没有相机。

## 连续画面与操作中分析

| 命令 | 用途 |
| --- | --- |
| start_visual_observation | 开始有界持续采集，默认 screen 包含 Overlay UI；camera 源需明确句柄。 |
| get_visual_observation_status | 实际状态、采样计数/频率、采集耗时、缓冲区和淘汰计数。 |
| mark_visual_observation | 给随后帧关联操作标签。 |
| read_visual_frames | 按会话与游标增量读最新图片路径、时间、游戏/渲染帧号与像素哈希；明确返回跳过帧数。 |
| stop_visual_observation | 停止采集并保留有界证据，不修改 Play 状态。 |

默认 2 fps、960×540、32 帧、最长 120 秒。可配置 0.2..10 fps、2..120 帧；每会话同时受 64 MiB 上限约束。图片在 Temp，不触发 Assets 导入。ring 淘汰后旧路径会失效，需及时查看/复制证据。`latest=true` 偏向最新状态，`latest=false` 按顺序消费；两种都返回真实缺口。

宿主 AI 的实际流程：

1. 按用户授权启动 Play/操作，创建观察会话，保留 ID。
2. 写操作标签，执行一小段动作；按动作节奏查询最新帧，一次通常 2..4 帧。
3. **实际打开返回图片**，比较相邻时间的画面，同时查相关 UI 命中/布局、相机或 Console。仅轮询路径不算分析。
4. 在当前任务工作回合中继续操作与取帧。发现问题时记录帧序号/时间/对象和观察；需要暂停时按已有任务授权执行，不把采集 stop 等同于游戏暂停。
5. 正常结束使用同 ID 停止；异常/重载查原会话终态。不要因一次 CLI 超时重复创建观察会话或重发写操作。

CLI 负责采集和事实查询，画面语义分析由正在工作的宿主模型完成；开始采集不会启动额外模型服务，也不代表模型在用户结束当前工作回合后仍持续监看。采样频率不等于模型推理频率。静止/重复画面用 identicalPixels 标记，时间戳也不能证明每次捕获都对应一帧新渲染。

需要画面时使用有 GPU 的 Editor。普通 wrapper 冷启动的 nographics 实例无法截图；显式打开交互式 Editor。camera 源省略 Overlay UI，不能用它验证 HUD 遮挡。

## SSU 适配（0.6.0）

11 个正式命令通过 `rendering/ssu`、`ui/ssu` 标签发现，涵盖真实效果/keyword 查询、typed 材质编辑、Renderer slot、GUI 绑定与运行实例参数。可选 uGUI 桥独立于协议和供应商脚本；SSU 原资产由项目提供。安装与 URP 17 2D 本地修补入口见 [SSU 使用说明](ssu.md)。缺包查询返回实际未安装；GUI 效果发现不代表全部组合验证。

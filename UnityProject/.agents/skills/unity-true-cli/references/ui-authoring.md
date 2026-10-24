# UI 搭建与 3D 面板

本批提供 Canvas、RectTransform、基础 uGUI 元素和原生布局的制作入口。先用 `get_cli_extensions` 确认当前服务已加载新版，再查询命令 schema。旧工程按 [升级说明](cli-extensions.md) 显式升级 Pipeline；更新 Skill 文件夹不会让旧服务自动加载新代码。

| 命令 | 参数与行为 |
| --- | --- |
| create_ui_canvas | `name`、`mode=overlay/camera/world`、可选 `parent` 和 `rect`。camera/world 必须显式传 Camera ObjectRef。创建 Canvas 与 RectTransform。 |
| configure_ui_canvas | `target`、可选 `mode/camera/plane_distance/sorting_order/override_sorting`。Camera 模式的最终距离必须在选定相机 near/far 之间；换相机后旧距离不合法时拒绝整项修改。 |
| configure_ui_rect | `target` 和 `settings`：锚点、pivot、sizeDelta、offsets、三维位置/旋转/缩放。 |
| create_ui_element | `name/parent`，`kind=empty/panel/image/button`，可选 `rect/tint/sprite`。button 创建真实 Label 子节点并绑定 targetGraphic，必须提供项目 Font。 |
| configure_ui_layout | `target`、`kind=horizontal/vertical/grid`、可选 `settings` 和 `replace`。使用原生 LayoutGroup/ContentSizeFitter。 |
| configure_ui_layout_element | `target/settings`。设置子元素最小/期望尺寸、弹性权重、ignore_layout 和 priority。 |

Canvas/Rect 三个入口不依赖 uGUI。元素与布局入口需要工程已安装 `com.unity.ugui`；本批按钮 Label 使用旧 Text，TMP 编辑继续使用已有 TMP 入口。不会替工程安装包、选择字体、创建 EventSystem 或切换输入后端。

## 创建世界面板

以下是官方 `command` 的参数片段；通过 Skill 的 `Invoke-Unity.ps1 -ProjectPath ... -UnityArgs @(...)` 调用。对象参数是 ObjectRef JSON，可用当前 instanceId；保存重开后改用 GlobalObjectId。示例中的相机、字体和句柄必须换成项目实际对象。

```text
command create_ui_canvas --name Inventory --mode world --camera '{"instanceId":1234}' --rect '{"size_delta":[640,360],"local_position":[0,0,0],"rotation":[0,-12,0],"scale":[0.01,0.01,0.01]}' --json
command create_ui_element --name Cards --parent '{"instanceId":5678}' --kind panel --tint '[0.08,0.13,0.23,1]' --rect '{"size_delta":[600,280]}' --json
command create_ui_element --name Confirm --parent '{"instanceId":6789}' --kind button --font '{"path":"Assets/Fonts/MyFont.ttf"}' --text Confirm --json
command configure_ui_layout --target '{"instanceId":6789}' --kind horizontal --settings '{"padding":[15,15,30,30],"spacing":18,"alignment":"MiddleCenter","control_width":true,"control_height":true,"expand_width":false,"expand_height":false}' --json
command configure_ui_layout_element --target '{"instanceId":7890}' --settings '{"preferred_size":[180,110],"flexible_size":[0,0]}' --json
```

使用创建结果里的 `handle` 或 `root` 作为下一命令的引用。需要指针交互时显式给 Canvas 添加 `UnityEngine.UI.GraphicRaycaster`，并使用项目实际 EventSystem/InputModule；先 `inspect_ui_point`，再通过真实输入验证回调。命中结果只证明射线命中，不证明按钮业务成功。

`tint` 为 RGBA 数组，范围 0..1。官方 CLI 的全局 `--color` 参数控制终端颜色，因此元素颜色使用 `--tint`。

## Rect 与布局规则

Rect 数组：`anchor_min/anchor_max/pivot/size_delta/offset_min/offset_max` 为两个数；`anchored_position/local_position/rotation/scale` 为三个数。`rotation` 是本地欧拉角（度）。应用顺序为 anchors/pivot → size 或 offsets → 位置/旋转/缩放。锚点和 pivot 支持 Unity 允许的区间外数值；拉伸布局的负 sizeDelta 也合法。

同一次调用不能混用 offsets 与 size/position，也不能同时指定 local_position 和 anchored_position。所有数必须有限，未知 settings 字段拒绝；合法字段后混入拼写错误也不会部分采用。

布局同类型更新仅修改显式字段，保留其他参数和子节点。换类型需要 `replace=true`。padding 顺序为 `[left,right,top,bottom]`；linear 用 spacing、control_width/height、expand_width/height；grid 用 cell_size、grid_spacing、columns。`horizontal_fit/vertical_fit` 为 `Unconstrained/MinSize/PreferredSize`，只在显式设置时添加或修改 ContentSizeFitter。LayoutElement 的 `min_size/preferred_size/flexible_size` 为 `[width,height]`，`-1` 表示未指定。

所有新制作入口要求稳定 Edit Mode，支持一组 Undo；复合操作抛错时回滚本组。Canvas 模式变化由 Unity 驱动根节点 Rect，撤销同时恢复原尺寸与 3D 姿态。真正切入 Overlay 清除相机引用；已经是 Overlay 时，仅改排序或重复指定该模式保留已有序列化引用。

命令返回实际即时状态，Unity 的下一次正常布局可能改变受驱动属性。等布局更新后使用 `get_ui_snapshot/inspect_ui_components/explain_ui_layout` 再查，结合 GPU 截图核对。不会为了即时回显强制刷新布局，也不会自动保存场景。

首批覆盖上述基础制作链；ScrollView、高级控件完整树、TMP 创建、导航、嵌套响应式布局与弯曲网格仍为后续工作。实测版本和具体断言见[兼容矩阵](compatibility.json)与[本次记录](verification-0.8.0.json)；仓库 docs/verification 属于历史材料，不随此包分发，不将其扩展为所有组合或 Player 已验收。

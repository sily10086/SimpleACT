# UI 布局与排序事实查询

首批提供三个 Editor 只读命令：`get_ui_snapshot`、`explain_ui_layout` 和 `find_ui_overlaps`。先通过当前服务发现 schema，确认项目加载了包含它们的版本。

```powershell
& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<工程>' `
    -UnityArgs @('command', 'get_ui_snapshot', '--limit', '100', '--json')

& '<Skill目录>/scripts/Invoke-Unity.ps1' -ProjectPath '<工程>' `
    -UnityArgs @('command', 'explain_ui_layout', '--target', '{"instanceId":12345}', '--json')
```

`root`/`target` 接受现有 ObjectRef。优先使用本次查询返回的 instanceId 或可用的全局对象引用，不能仅凭同名对象猜目标。限定 root 后只返回该子树中的 RectTransform。

## get_ui_snapshot

参数：`root` 可选，`include_inactive=false`，`offset=0`，`limit=100`（1..500）。返回当前 stage 的场景对象，忽略持久资产，不把 Prefab 资产当成已显示 UI；Prefab Stage 与普通场景 stage 分开处理。

返回 total、offset、nextOffset、frame、screenSize 和 elements。每个元素包含：

- 对象及父级定位信息、scenePath、activeSelf/activeInHierarchy、组件类型。
- siblingIndex 与数值排序的完整 siblingPath；路径服务于局部结构解释，不构成跨 Canvas 的统一绘制排名。
- localScale/lossyScale，RectTransform 的锚点、pivot、anchoredPosition、sizeDelta、offset 和实际 rect。
- drivenBy：Unity 当前记录的尺寸/位置驱动者，不猜 LayoutGroup 私有字段。2022.3 没有公开 drivenProperties，首批不声称逐属性列出驱动标记。
- 最近 Canvas 与根 Canvas 身份、renderMode、sortingLayer ID/value、sortingOrder、overrideSorting、renderOrder、scaleFactor、referencePixelsPerUnit、targetDisplay 和 worldCamera。
- 屏幕投影和四角坐标，坐标原点为左下；矩形为 `[x,y,width,height]`。

投影状态：

- `projected`：screenCorners、screenAabb 与 viewport 可读；screenAabb 是四角投影的轴对齐包围盒。
- `no_canvas`：目标不在 Canvas 下，只返回世界/本地几何。
- `camera_required`：Camera/World Space 根 Canvas 没有显式 worldCamera；不自动选择 Camera.main。
- `near_plane_intersection`：至少一角在相机近裁剪面上或后方，不返回具有误导性的屏幕矩形。
- `unsupported_display`：当前只定义 display 0 的坐标。
- `camera_target_texture`：相机输出到 RenderTexture，不能把纹理坐标当成 Game View 屏幕坐标。
- `invalid_geometry`：投影不是有限数值。

分页基于当前状态，不冻结场景；跨帧层级变化后应从头重新取相关范围。查询不会强制刷新布局，因此在刚改完布局的同一帧可能读到待更新值，应等待真实布局更新后回读。

## explain_ui_layout

必须指定带 RectTransform 的场景 target，返回 targetToRoot 完整父级链，保留中间普通 Transform。链上每个 CanvasGroup 的 enabled、alpha、interactable、blocksRaycasts、ignoreParentGroups 单独返回。最多 256 级父链，超出则报告错误，不静默截断。

## 当前边界

`find_ui_overlaps` 接受 root、limit=30（1..100）、include_ancestors=false，最多扫描 200 个活动 RectTransform；超出要求缩小 root，不静默漏扫。返回候选相交矩形、各自 AABB 相交比例和对象句柄；默认排除祖先包含后代这类普通布局关系。totalCandidates 与 truncated 说明结果是否超过返回上限，unprojectableElements 说明哪些几何未进入比较。根节点同一层级下的几何关系不等于实际遮挡。

这三个核心命令提供几何、缩放和排序原始事实。0.2.0 已通过可选适配补上 inspect_ui_components（CanvasScaler/LayoutGroup/常见组件）、inspect_ui_point（EventSystem 命中栈）、inspect_tmp_text 与对应写命令，见 [CLI 扩展](cli-extensions.md)。不可凭 screenAabb 相交就断言像素遮挡、不可点击或排序错误。

排查时将对象与父链查询、实际截图及后续输入回读结合。数据查询不自动改分辨率、排序、布局、Play 状态或保存场景。

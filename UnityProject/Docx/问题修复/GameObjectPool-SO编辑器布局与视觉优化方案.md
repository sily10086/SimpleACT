# GameObjectPool SO 编辑器布局与视觉优化方案

- 日期：2026-10-25
- 等级：L3，现有 Inspector 跨文件布局调整。
- 状态：用户已确认执行，三个编辑器文件已完成修改；尚未运行验证。
- 用户已选择：优化现有 SO Inspector，采用更明显的卡片、配色和状态提示。

## 目标、范围与依据

提高 GameObjectPoolSetting 配置的可读性，解决强制横排导致的宽度竞争，明确每个池和两类回收策略的视觉边界。

修改范围限定为 Assets/TEngine/Editor/GameObjectPool 下的三个现有绘制文件：GameObjectPoolSettingInspector.cs、GameObjectPoolEntryDrawer.cs、GameObjectPoolInspectorContent.cs。
保留现有 11 个配置字段、序列化格式、默认值、过期规则、检查规则和编辑操作语义。

从代码确认的布局特征：

1. 身份字段固定并排，右侧另有定位按钮，没有根据面板宽度切换布局。
2. 字段标签统一为 76 点，包含“整池自动释放间隔”等较长文案。
3. 回收设置一行混合普通字段、76 点标签、86 点模式下拉和时间输入，空间分配不均。
4. 优先级字段与三个各 72 点的按钮同排，窄面板空间紧张。
5. 主标题和展开/折叠按钮同排；说明默认展开；卡片主要依靠 helpBox 和分隔线区分。

以上是源码事实。具体重叠或截断表现尚未在 Unity 中复现；若用户补充画面暴露其他问题，纳入布局修正，超出范围的交互变更另行确认。

## 参考方案与取舍

已在 GitHub 查询 Unity 官方 UnityCsReference 的 Inspector 和 FoldoutHeader 实现，参考标题、折叠及宽度分配的组织方式：

- https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/Editor.cs
- https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/GUI/FoldoutHeader.cs

工程版本为 Unity 2022.3.62f2。上述 master 源码只作设计参考，不直接复制或依赖其中内部 API。继续使用现有 IMGUI 与公开 Editor API，避免为局部视觉调整引入额外插件或 UI 框架迁移。

## 布局与视觉

- 顶部概览卡片：标题单独一行；配置数量、错误数、警告数放在独立摘要区域；帮助默认收起。
- 工具区：搜索与列表操作分行组织，宽度不足时操作按钮换行。
- 每池一卡：加强卡片内边距、标题背景和分隔，状态色带配合文字/图标，避免只靠颜色传达信息。
- 卡片标题：序号、池名和状态；窄宽度下操作按钮另行，长名称可截断并通过 Tooltip 查看完整内容。
- 身份字段：池名称、资源地址各占一行；定位操作与地址相邻，复制名称放在合适的操作区，避免重复入口。
- 预热与容量：宽面板采用等宽双列，窄面板自动单列；字段采用上方标签、下方输入，避免长标签挤占输入。
- 回收策略：分成“实例回收”和“整池回收”两个子区；回收方式、过期秒数、扫描间隔按纵向顺序呈现。宽面板可将子区并排，窄面板上下排列。
- 永不过期：保留原有哨兵值及切换回默认秒数的语义；整池扫描间隔继续显示不适用提示。实例扫描间隔的处理保持原逻辑，不能把关闭过期误当作关闭所有容量清理。
- 优先级独立成行；恢复默认等操作放卡片底部，与输入字段分开。
- 添加条目为主操作；全部恢复默认为次操作，保留原确认与撤销行为。
- 蓝色用于结构强调，红色表示错误、琥珀色表示警告，绿色表示当前配置检查无问题；分别适配深色和浅色主题。
- 问题提示支持换行；详细问题沿用现有检查来源，状态不代表运行时已验证。

取舍：窄面板下卡片会比当前更高，用折叠、搜索及宽屏双列控制浏览成本。初步以 320、480、720 点面板宽度作为后续验收参考；实际断点由内容最小宽度、内边距与列间距决定。

## 分步执行清单

### 1. 统一视觉与布局尺寸

- [x] 修改 GameObjectPoolInspectorContent.cs：完成卡片留白、主题配色、文字状态徽标、可换行错误、上标签字段以及最小列宽和间距；切换主题重建样式。
- 原因：消除散落的固定尺寸和不一致层级。
- 伪代码：EnsureStyles(theme) → ResolveColumns(contentWidth, minimumColumnWidth, gap) → DrawSection/DrawStatus。

### 2. 重排条目字段

- [x] 修改 GameObjectPoolEntryDrawer.cs：身份改为单列；容量和两类回收区域按宽度切换单双列；优先级与操作区分离；移除重复资源定位按钮；字段继续使用缓存的 SerializedProperty，时间标签明确秒单位。
- 原因：解除标签、输入和按钮之间的横向挤压，明确字段归属。
- 伪代码：DrawIdentity → DrawPrewarmAndCapacity(columns) → DrawInstanceAndPoolRelease(columns) → DrawPriority → DrawActions。
- 注意：保持 GUI 状态恢复、播放模式只读、过期哨兵值和默认值写入路径。

### 3. 重排主面板与状态

- [x] 修改 GameObjectPoolSettingInspector.cs：完成概览、默认收起帮助、工具区、彩色卡片标题与状态；窄面板操作分行；长标题附完整 Tooltip；测量内容宽度并在 Layout 阶段应用，复用原有诊断和结构操作路径。
- 原因：提升首屏有效信息量和多条目浏览效率。
- 伪代码：Update → RefreshExistingCachesIfNeeded → DrawOverview → DrawTools → DrawCards → DrawActions → ApplyModifiedProperties → DrawDiagnostics。
- 注意：增删复制仍在布局组收尾后执行；保持 Undo/Redo、缓存失效和编辑器事件订阅逻辑。

### 4. 交付记录

- [x] 按实际改动更新完成状态；交付说明明确未运行验证，并询问是否需要检查。
- [ ] 用户明确要求后，才执行编译或 Unity 视觉及交互验证；本项在未获要求时保持未完成。

## 验收标准（方案已确认，尚未执行）

1. 在约 320/480/720 点的 Inspector 宽度下，字段与按钮不重叠，标签完整可读，输入区域可用；长池名保留完整文本提示。
2. 深色与浅色主题下卡片、分区、输入和状态均可辨认，正常/警告/错误有文字或图标辅助。
3. 全部 11 个字段仍可按原语义编辑；搜索、问题过滤、折叠、增删复制、资源定位和恢复默认保留。
4. Undo/Redo、播放模式只读和过期模式切换行为保持一致。
5. 不更改运行时模块、SO 数据结构或现有资产配置。

## 当前执行结果

已完成：规则和相关源码阅读、GitHub 参考查询、方案确认、三个编辑器绘制文件修改及执行记录更新。

未完成：编译；Unity 中 320/480/720 点布局、深浅主题、Undo/Redo、过期模式切换及播放只读等视觉与交互验证。没有运行验证，不代表验证通过。

实现说明：双列以卡片内容宽度达到 412 点（200 × 2 + 12）作为切换条件；窗口缩放后，测量结果在下一次 Layout 应用。最终断点和视觉表现仍需实际 Unity 画面确认。

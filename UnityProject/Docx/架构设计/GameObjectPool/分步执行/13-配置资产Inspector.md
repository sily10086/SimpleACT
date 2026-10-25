# 第 13 步：配置资产 Inspector

前置：第 12 步主功能门槛满足。**从本步起才开始编辑器功能。**  
本步完成后：可在 Inspector 方便地编辑、复制、定位和检查对象池配置。  
状态：代码已落盘（2026-10-25，含一次性能与结构重构），存根环境编译 0 警告 0 错误；**未在 Unity 内编译、未目视确认**，`TEngine.Editor` 当前仍因第 14 步的两个文件编译不过。

## 1. 目标与文件改动

| 目标 | 文件 | 改动与功能 |
|---|---|---|
| 13-A | Assets/TEngine/Editor/GameObjectPoolSettingInspector.cs | 面板骨架：生命周期、缓存同步、过滤、条目壳、底部操作、诊断区 |
| 13-B | 同上 | 条目增删/复制/搜索/折叠、默认值与 Undo |
| 13-C | `GameObjectPoolEditorValidator.cs`（第 02 步新增）+ `GameObjectPoolInspectorContent.cs` | 复用编辑器诊断，补 YooAsset 地址定位 |
| 13-D | 该 Inspector 的启停与订阅 | 更新提示、释放 Editor 事件，恢复第 01 步必要隔离 |
| 13-E | `GameObjectPoolEntryDrawer.cs` | 四组分区 + 两列紧凑布局；两个过期字段各带「按时间回收 / 永不过期」枚举下拉，选永不过期后收起时间输入框；整池过期关闭时收起扫描间隔 |
| 13-F | `GameObjectPoolEntryDrawer.cs` + `GameObjectPoolEntryDefaults`（编辑器程序集） | 九个默认值集中到 `const`，唯一的 `ApplyDefaults` 写入点，条目级与全局"恢复默认" |
| 13-G | `GameObjectPoolEntryDrawer.cs` + `GameObjectPoolInspectorContent.cs` | 每个字段就地校验并标红，不静默钳制 |
| 13-H | `GameObjectPoolSettingInspector.cs` + `GameObjectPoolEditorValidator.cs` | 条目状态图标、"只看有问题"、"展开问题条目"、过期时序提示 |
| 13-I | 三个文件的分工与稳态成本 | 字段引用缓存、文案/颜色静态化、事件合并、绘制期零字符串拼接 |

三个文件的分工在 13-I 里说明。参考当前 GameFrameworkInspector、ProcedureSettingInspector 的生命周期和序列化方式；不另建编辑器 UI 框架。第 01 步若未隔离该文件，则无恢复操作。

## 2. 面板与功能

~~~text
TEngine 游戏对象池配置                        [展开全部][折叠全部]
  ▸ 这个资产怎么填（可折叠说明）
  搜索 [__________][清空]            共 N 个条目   [只看有问题]
  ┌─ ✖ 1. Coin ──────────────────────────────── [复制][删除] ─┐  ← helpBox 卡片
  │ 资源标识 ────────────────────────────────────────────────  │
  │   池名称 [Coin          ]  资源地址 [Coin          ][定位]  │
  │ 预热与容量 ──────────────────────────────────────────────  │
  │   预热数量 [0]             每帧预热数量 [4]                 │
  │   容量目标 [32]            实例上限     [0]                 │
  │ 清理与过期 ──────────────────────────────────────────────  │
  │   自动释放间隔 [60]        实例过期 [按时间回收▾] [60.0]    │
  │   整池过期 [永不过期▾][不回收]   整池扫描间隔 [5.0]         │
  │ 回收优先级 ──────────────────────────────────────────────  │
  │   优先级 [0]               [复制名称][定位资源][恢复默认]   │
  └────────────────────────────────────────────────────────────┘
  ┌─ 2. Enemy ───────────────────────────────────── ✖ ────────┐
  │  …… 同上，每个池一个独立框框，框间留 6px ……                │
  └────────────────────────────────────────────────────────────┘
  [＋ 添加对象池条目]        [全部恢复默认值]
  配置检查                                    [展开问题条目]
    （逐个条目的错误与提示，带"第 N 条"前缀）
~~~

显示中文含义并配 Tooltip：特别说明容量不是硬上限、预热不是最低保有量、资源地址是 YooAsset 定位地址（文件名去扩展名，不带路径前缀）、**实例过期与池级过期是两个不同概念**。

池级过期的提示文案要写清语义差异，避免被当成"实例过期"的别名：

| 字段 | Tooltip 要点 |
|---|---|
| 整池过期时间（PoolExpireTime） | 池内**所有对象都不在借出状态**时才开始倒计时；有人借出则本轮计时作废，全部归还后重新计时完整时长；预热期间不计时，预热完毕即刻开始计时。过期方式选「永不过期」等于关闭，默认关闭 |
| 整池自动释放间隔（PoolAutoReleaseInterval） | 逐池字段，各池独立；只是扫描频率，到期在下一个检查点生效，不是精确定时器。整池过期关闭时这一行不显示输入框 |

空配置显示信息，不显示红色错误。

第一版播放中禁用配置编辑；不提供运行参数修改写回资产或自动重新初始化模块。

## 3. 类型设计

Inspector 必须继承 Editor/GameFrameworkInspector，因此为 class。纯提示数据如 Severity、Index、Message 使用 readonly struct；需要改写的 UI 展开状态用现有基础集合（`List<bool>`）。

唯一的例外是 `GameObjectPoolEntryDrawer`：一条配置对应一个实例，它有状态（11 个 `SerializedProperty` 引用），且这个状态需要跨帧存活，用 struct 放进 `List` 反而会因为副本语义丢缓存。它不是"每行 UI 状态"的容器，而是"一条配置的字段视图"，因此用 class；每个实例常驻的内存是 11 个引用。

配置条目是普通 struct，SerializedProperty 直接用条目元素的相对字段定位；若使用直接 C# 修改数组元素，需整体取出/写回，不能修改临时副本后声称保存。

## 4. 逐目标伪代码

### 13-A：序列化绘制

~~~text
OnEnable:
  调用基类要求的初始化
  FindProperty("_pools")
  RebuildValidation()           // 诊断 + 条目状态 + 全部预格式化文案
  EnsureDrawers(arraySize)      // 每条目一次性绑定 11 个字段引用
  订阅 Undo、projectChanged

OnInspectorGUI:
  执行基类检查
  _readOnly = isPlayingOrWillChangePlaymode   // 本帧只查一次
  serializedObject.Update()
  若 _validationDirty -> RebuildValidation()   // projectChanged 合并到这里
  EnsureDrawers(arraySize)                     // 长度一致直接返回
  绘制标题、过滤、条目、底部
  先记录增删意图，GUI 栈收尾后应用
  changed = serializedObject.ApplyModifiedProperties()
  changed -> RebuildValidation()
  展示预格式化的错误/提示
~~~

字段缺失时显示明确“脚本/序列化字段不匹配”，不继续空引用绘制。

### 13-B：增删复制

~~~text
Add:
  增加一个数组元素
  显式赋全部默认值
  不依赖 Unity 扩展数组时复制上一项的默认行为

Duplicate(index):
  使用序列化属性复制该项全部字段
  生成未占用名称（Name_Copy / Name_Copy2）
  保留资源地址与数量，提示用户确认池名

Delete(index):
  对该元素执行正确的数组删除
  同步折叠/选中状态
  让 SerializedObject 记录 Undo
~~~

普通 struct 条目不存在“先置 null 再删除”的引用元素流程；实现按实际元素序列化类型处理，不无脑删两次。

### 13-C：编辑器诊断 + 资产存在性

~~~text
RefreshValidation:
  GameObjectPoolEditorValidator.Collect(setting, _errors, _warnings)
    -> 数量、重名、空名称、首尾空白、地址格式、目标矛盾、全局字段
  资源包已初始化:
    CheckLocationValid(assetName) / HasAsset -> 地址无法定位为编辑错误
    资源仅在线 -> 提示需要下载后才能建池
  资源包未初始化:
    只用 AssetDatabase 按文件名检索候选预制体，提示"运行时才能确认"
  同名文件多于一个 -> 提示应写相对路径地址去重
  结果缓存在 List<GameObjectPoolValidationMessage>，不在 OnGUI 里重建
  同一趟把"第 N 条："前缀拼好存入 _errorLines / _warningLines，
    并生成标题、条目计数、通过摘要、溢出提示 —— 绘制阶段只读取
  不逐次 OnGUI 全量反射/扫描工程
~~~

诊断器位于 TEngine.Editor 程序集，发布版不存在。`GameObjectPoolValidator.ValidateRuntime` 的 10 条单项规则也在 `#if UNITY_EDITOR` 内，发布版同样不存在；面板侧先调它、再补格式与跨条目检查，保证同一配置项只被判定一次、且面板与建字典的结论一致。

Duplicate 名称不自动写入业务代码。复制名称和 Ping 只是编辑操作，不注册池、不加载业务资源、不实例化场景对象。

### 13-D：释放订阅

~~~text
OnDisable:
  解除本 Inspector 添加的所有 Editor 事件订阅
  清理临时引用
  保留基类生命周期要求
~~~

Undo/Redo、资产变化只刷新相关提示；不持续无条件 Repaint。保存通过标准序列化系统，不调用全项目 SaveAssets 作为每帧操作。

### 13-E：分组、两列紧凑布局与过期方式下拉（2026-10-25 16:15 定稿）

~~~text
11 个字段分四组，组内两列并排：
  资源标识     PoolName / AssetName(+定位按钮)            ← 同一行
  预热与容量   PrewarmCount / PrewarmPerFrame             ← 第 1 行
               Capacity / MaxInstanceCount                 ← 第 2 行
  清理与过期   AutoReleaseInterval / ExpireTime(下拉+值)   ← 第 1 行
               PoolExpireTime(下拉+值) / PoolAutoReleaseInterval(条件显示) ← 第 2 行
  回收优先级   Priority + [复制名称][定位资源][恢复默认]   ← 同一行
~~~

**排布目标**：一个池从"每字段一行"的 14 行压到 6 个字段行 + 4 个分组标题，多个池时能在同一屏里扫完。压紧的代价是标签宽度只有 76px（`LABEL_WIDTH`），再加字段名就要重算列宽。

1. **分组不是装饰**：作者填参数的真实顺序是"先给它身份、再给数量、最后才轮到回收"。平铺 11 行时最容易把 ExpireTime 和 PoolExpireTime 看成一个东西——这正是第 2 节要反复强调的那件事。
2. 分组标题用 `GroupTitleStyle`（复制 `EditorStyles.boldLabel` 后 `fontSize + 1`）+ 一条跟随皮肤的分隔线（Pro 白 12%／亮色黑 18%），行高 18px，**不用嵌套 box**：box 套 box 在浅色皮肤下边界会糊在一起。字号不能写死也不能在静态构造里取——那时 `EditorStyles` 还是 null，只能在属性 getter 里惰性构造，基准值取 `ResolveFontSize()`（探不到时退回 11）。
2.1 正文与就地错误共用 `FieldLabel`（复制 `EditorStyles.label` 再 `fontSize + 1`）；资产标题用 `AssetTitleStyle`（`+2`）。三条样式都走惰性 getter，域重载后自动重建一次。
3. 条目容器用 `EditorStyles.helpBox`，形成"每个池一个框框"的观感；框内首行先 `Space(2f)` 不贴边，条目之间留 6px 把相邻框分开。两个间距不能共用一个值：2px 是框内留白，6px 是框间分离。
4. 字段标签宽度在条目内统一：进入时设 `EditorGUIUtility.labelWidth = 76`（`LABEL_WIDTH`），结束恢复；手动绘制的行（两个过期字段、优先级标签）用同一宽度，否则同一条目里两套标签明显错位。缩进与 labelWidth 都用 `try/finally` 还原，中途异常不会污染整个 Inspector。76px 是两列布局下的上限：再宽右侧输入框就没了。
5. **过期方式用枚举下拉，不用勾选框**：`EditorGUILayout.Popup` 两项「按时间回收 / 永不过期」，下标与 `GameObjectPoolExpireMode`（`Timeout = 0` / `Never = 1`）一一对应，文案表是 `ModeOptionNames`（文案顺序必须与枚举值顺序一致，改一个就得改另一个）。理由：同一字段有两种语义时，下拉框能把两个选项都写在脸上；勾选框只表达"关掉了什么"，而且"永不过期"四个字配一个勾选框在窄面板里会被截断成"永不过…"。
   选「永不过期」→ 写哨兵值（实例 `float.MaxValue`／池级 `0`），时间输入框**不再绘制**，原位置显示"不回收"三个字；选回「按时间回收」→ 时间框回来，且若当前值是哨兵值就填 `GameObjectPoolEntryDefaults.InstanceExpireTime` / `PoolExpireTimeOnEnable`。下拉状态与写入值只经过 `GameObjectPoolExpireRule` 的两个静态判定与两个常量，Inspector 里**不出现 `float.MaxValue` 字面量**。
6. 整池过期关闭时，"整池自动释放间隔"那一行也不画输入框，显示"整池不过期，无需扫描"。理由同 02-E：该值不参与判断，留着输入框等于骗人。

为什么不新增一个 `bool Forever` 序列化字段：见 02-E。哨兵值是底层本来就认的写法，新增 bool 会多出"bool 与数值不一致时以谁为准"的第二套规则。

**代价（明确接受）**：选「永不过期」再切回「按时间回收」会丢掉原来的秒数，退回推荐值。要做"记住原值"就得在 Inspector 里放一份非序列化草稿，而条目增删会打乱下标、池名重复时也没法做键，复杂度不值得——哨兵值 + 推荐值的语义是自洽的：选永不过期就真的是关闭，切回来就是启用一个能用的默认值。

### 13-F：默认值与恢复

1. 添加条目：`InsertArrayElementAtIndex` 后显式把名称/地址写空，再 `ApplyDefaultsTo(entry)`。不依赖"Unity 扩展数组时复制上一项"的行为。
2. `ApplyDefaultsTo` 是**唯一的默认值写入点**，九个值全部来自 `GameObjectPoolEntryDefaults` 的 `const`；条目级"恢复默认"与全局"全部恢复默认值"共用它。编辑器代码里没有魔数——默认值写两处必然漂移。
3. **恢复默认只覆盖参数，不覆盖池名与地址**：那两个是身份信息，覆盖了等于换了个池。
4. 全局恢复是批量覆盖手填值，先 `DisplayDialog` 确认。确认框在布局收尾（`EndHorizontal` / `EndDisabledGroup` 之后）弹出：在 `BeginHorizontal` 内部弹模态框会让 GUI 栈与布局错位。
5. 单条"恢复默认"不弹框（影响面就一条，且有 Undo）。

### 13-G：就地校验

1. 每个字段画完立刻判一次，非法时在字段下方用红色 `miniLabel` 打一行 `× 原因`。**不用 HelpBox**：11 个字段各挂一个 HelpBox 会把面板撑成长条，而这里要的只是"这一行填错了"的即时反馈。
2. 数值规则：预热数量／容量／实例上限 `>= 0`；每帧预热数量 `> 0`；自动释放间隔、整池自动释放间隔为有限正数；实例过期时间 `> 0` 或等于哨兵值；整池过期时间 `>= 0`；优先级不限（底层是排序权重，可以为负）。
3. 字符串规则：池名／地址不能为空，池名不能首尾带空白。地址的格式规则（带扩展名、反斜杠、前导斜杠）只在底部诊断里报，不在这里重复第二遍。
4. **不静默钳制**：输入 `-5` 就让它留在字段里变成红字，不改写成 `0`。这条沿用 02 文档"非法值一律交给校验器报出来，而不是悄悄改成默认值"的口径——静默钳制会让作者以为填的数生效了。
5. 就地校验是"即时反馈"，不是规则的第二份定义：阈值与默认值引用 `GameObjectPoolEntryDefaults`，哨兵值判定引用 `GameObjectPoolExpireRule`，最终结论仍以底部 `GameObjectPoolEditorValidator` 为准。

### 13-H：诊断区与过滤

1. 底部"配置检查"标题行右侧加"展开问题条目"，从诊断直接跳到问题条目，不用自己翻。
2. 每条诊断前拼"第 N 条："（`Index < 0` 的全局问题不拼）。
3. 条目标题行右侧画状态图标：有错误 `console.erroricon.sml`、只有提示 `console.warnicon.sml`、都没有则留 20px 空位（不留空位标题会左右跳动）。图标数据来自 `RefreshValidation` 时摊平到 `_entryHasError` / `_entryHasWarning` 的结果，**不在 OnGUI 里遍历诊断列表**。
4. "只看有问题"过滤 + 状态图标配合，条目多时能一屏看完需要动的条目。
5. 搜索：池名与地址都参与匹配，`OrdinalIgnoreCase`，与运行期 `Ordinal` 的字典键严格性区分开（搜索是给人用的，不需要那么严）。

### 13-I：性能与代码结构

#### 13-I-1 为什么编辑器代码也要算成本

IMGUI 的 Inspector 不是"打开一次画一次"：一次重绘至少走 `Layout` 与 `Repaint` 两遍，鼠标移动、键盘输入、窗口缩放还会各自触发。于是任何写在绘制路径上的小开销都要乘以「条目数 × 字段数 × 事件数」。作者把池配到几十条、面板一直开着调参时，这条乘积就会变成肉眼可见的输入延迟。

判断标准只有一条：**稳态（没有新操作、只是重绘）时，绘制路径上不应该有查找、不应该有堆分配、不应该有字符串拼接**。一次性开销（绑定、刷新诊断、点击按钮）不计较。

#### 13-I-2 三个文件的职责

| 文件 | 职责 | 规模 |
|---|---|---|
| `GameObjectPoolSettingInspector.cs` | 生命周期与订阅、缓存同步、标题/过滤/条目壳、底部操作、诊断区 | 约 870 行（含注释） |
| `GameObjectPoolEntryDrawer.cs` | 单条配置的四个分组、11 个字段的就地校验、字段引用缓存 | 约 450 行 |
| `GameObjectPoolInspectorContent.cs` | 字段名常量、全部文案与图标、颜色、绘制积木、资产定位 | 约 370 行 |

拆分的依据是**变更原因不同**：加一个配置字段只改 `EntryDrawer` 与两处常量；改文案只改 `Content`；改交互（折叠、搜索、增删）只改主面板。原先是 1093 行一个类同时承担这三类变更，改文案要在一堆布局代码里翻。

`GameObjectPoolFieldName` 把 11 个序列化字段名收成常量。这不是洁癖：`FindPropertyRelative` 找不到字段时返回 `null` 且**不抛异常**，字段名拼错的表现是"面板上某一行凭空消失"，靠肉眼极难定位。

#### 13-I-3 性能优化（按收益排序）

| # | 原来 | 现在 | 稳态收益 |
|---|---|---|---|
| 1 | 每个条目每次重绘做 12 次 `FindPropertyRelative`（子属性链表上的名字比较） | 字段引用缓存在 `GameObjectPoolEntryDrawer`，只在数组长度变化时 `Bind` 一次 | N 条目从 `12N` × 事件数 → **0** |
| 2 | 每个条目每次重绘 `new GUIContent` 约 12 次（标签、tooltip、过期方式下拉选项、状态图标包装） | 37 个 `static readonly GUIContent` + 2 个惰性图标 + 静态 `ModeOptionNames` | `12N` × 事件数次堆分配 → **0** |
| 3 | `EditorGUIUtility.IconContent("console.erroricon.sml")` 每条目每帧一次 native 调用 | 图标 `GUIContent` 缓存，域重载后重建一次 | N 次 native 调用 → **0** |
| 4 | `EditorApplication.isPlayingOrWillChangePlaymode` 每条目 2～3 次（两处 `BeginDisabledGroup` + 播放提示） | 每次绘制开头取一次存 `_readOnly` | `3N+1` 次 native 查询 → **1** |
| 5 | 诊断文案每帧拼 `"第 N 条：" + message`（最多 8×2 次），标题每帧拼序号与池名，条目计数/通过摘要/溢出提示每帧拼 | 全部在 `RebuildValidation` 时生成，绘制只读取 | 每帧数十次字符串分配 → **0** |
| 6 | 就地错误每帧 `"× " + message` | 文案常量直接带 `×` 前缀，`DrawInlineError` 只改 `GUI.color` | 每字段一次拼接 → **0** |
| 7 | `SeparatorColor` 是属性，每次调用 `new Color`（每个分组标题一次） | 两个 `static readonly Color`，按皮肤二选一 | `4N` 次分配 → **0** |
| 8 | `EditorApplication.projectChanged` 每次都全量校验 + `Repaint`；批量导入资源时该事件连发几十次 | 只置 `_validationDirty`，重算合并到下次绘制；重绘用一次 `delayCall` 合并 | 导入 N 个资源从 N 次全量校验 → **1 次** |
| 9 | `MakeUniqueCopyName` 每试一个候选名就全表扫一遍池名（平方级，且每次取值都走 `SerializedProperty`） | 先一次收进复用的 `HashSet<string>`，再逐个候选查 | O(n²) → **O(n)** |
| 10 | `_pools.arraySize` 出现在循环条件里，每次迭代一次属性访问 | 提到局部变量 `size` | 循环内 0 次属性访问 |

第 1、2 条是主体：它们决定"面板开着不动"时的每秒垃圾量与 CPU。

#### 13-I-4 代码优化

1. **绘制不再顺手改结构**：条目上的复制/删除原本在按钮处直接动手，再补一句 `EndVertical` 提前 `return`（GUI 栈配对是 1:2，靠注释说明"这是故意的"）。现在 `DrawEntry` 只返回 `GameObjectPoolEntryAction`，由调用方在 GUI 栈收尾后执行，配对恢复严格 1:1，也不用再记"这条路径要手工补 End"。
2. **消除布尔参数**：`DrawExpireRow(..., bool instanceLevel, ...)` 把两个字段的差异压进一个 `bool`，读调用点看不出是哪个层级。现在拆成 `DrawInstanceExpireRow` / `DrawPoolExpireRow`，共用 `DrawExpireInputRow`（只负责"输入框 + 开关"这一行），各自写自己的错误文案。同理 `DrawFloatField(..., bool allowZero, ...)` 的两个调用点都要求 `> 0`，直接收敛成 `DrawPositiveFloatField`。
3. **消除重复查找**：`DrawExpireSection` 原先对 `PoolExpireTime` 取两次（一次传给绘制、一次判断是否关闭扫描间隔），现在字段在缓存里，直接读。
4. **收敛状态列表的伸缩逻辑**：`SyncExpandedState` / `ResetFlags` 里两套"补长 + 截断 + 遍历"合并成 `EnsureSize` / `ResetFlags` 两个工具。
5. **`IsReadOnly` 从属性改为字段**：属性看起来无害，实际上每次读都做一次 native 查询，被藏在 `BeginDisabledGroup` 的参数里很难发现。
6. **`OnDisable` 撤掉已排上的 `delayCall`**：静态事件上留着指向已销毁 Inspector 的委托，面板无法被回收。

#### 13-I-5 字段缓存的失效边界（关键风险）

缓存的语义是**绑定路径**，不是绑定值：`_pools.Array.data[3]` 这个路径始终指向数组的第四个元素，元素被拖拽换序后自动指向新内容，因此重排不需要重建。

会失效的只有一种情况：**数组长度变化**（增、删、复制、`arraySize` 被外部脚本改写）。此时路径会越界或残留。处理方式是结构变化后立刻 `InvalidateDrawers()` 丢弃全部缓存，再按新长度重建。三个操作点的顺序都是「改结构 → 丢弃缓存 → 写值 → `ApplyModifiedProperties` → 重建诊断 → 重建缓存」。

快路径是 `size == _drawers.Count` 直接返回，所以稳态下连 `propertyPath` 比较都不做。`IsBoundTo` 里额外比了 `serializedObject` 引用，防止 Editor 实例被复用到另一个资产上。

`DrawFields` 开头还有一层兜底：`_poolName == null` 时画一条"缓存已失效，折叠再展开"的提示并返回，而不是让空引用把整个 Inspector 打空。

#### 13-I-6 明确不做的优化

| 不做 | 理由 |
|---|---|
| 列表虚拟滚动（只画视口内的条目） | 条目数在几十量级，折叠 + 过滤已经够用；虚拟化要求自己算行高，会破坏 `EditorGUILayout` 的自适应布局，收益与复杂度不成比例 |
| 诊断增量刷新（只重算改动的那一条） | 全量校验是纯比较的 O(N)，N 小；要维护脏集合就得让每个写入点都记得标脏，漏一个就是"改完不报错" |
| 缓存 `HelpBox` 的 `GUIStyle` / 自建样式 | `EditorStyles` 的属性访问本身很轻，而且自己复制样式会在皮肤切换时不同步 |
| 预建「地址 → 预制体」全表索引 | 那会让**打开面板**就变慢，而现在只在点"定位"时才查一次数据库 |
| 每个条目池化一个 `GUIContent` 复用实例 | 文案是静态的，复用实例省不下东西，反而让"某一行文案被上一行覆盖"成为可能的 bug |

#### 13-I-7 量化对比（估算，非实测）

| 项 | 重构前 | 重构后 |
|---|---|---|
| 稳态每帧 `FindPropertyRelative`（20 条，2 事件/帧） | 约 480 次 | **0** |
| 稳态每帧 `GUIContent` 分配 | 约 480 个 | **0** |
| 稳态每帧字符串拼接 | 约 50 次（标题 + 诊断 + 就地错误） | **0** |
| 稳态每帧 native 播放状态查询 | 约 60 次 | **1** |
| 单文件最大行数 | 1093（旧单文件） | 869（`GameObjectPoolSettingInspector.cs`）；六个文件合计 2243 行，单文件都在千行以下 |
| GUI 栈配对 | `BeginVertical/EndVertical` 1:2（依赖注释解释） | 全部 1:1 |

这些数字是按代码路径数出来的，**没有做过 Profiler 实测**——本机没有 Unity。重构的判据是"稳态路径上不再有查找与分配"，不是"某个具体毫秒数下降了"。

## 5. 完成标准

- [x] 13-A 所有配置字段可编辑，普通 struct 序列化路径正确。
- [x] 13-B 添加/复制/删除有明确默认值与 Undo 行为。
- [x] 13-C 提示区分错误与信息；完整诊断只存在于 Editor 程序集，Runtime 只保留必要校验，两侧对同一配置项判定一致。
- [x] 13-D 订阅对称释放，不因绘制创建运行模块。
- [x] 13-E 四个分组 + 两列紧凑布局；两个过期字段各带「按时间回收 / 永不过期」枚举下拉，选永不过期后收起时间输入框；整池过期关闭时收起扫描间隔输入框；Inspector 里不出现 `float.MaxValue` 字面量。
- [x] 13-F 九个默认值只有 `ApplyDefaultsTo` 一个写入点；恢复默认不动池名与地址；全局恢复先确认。
- [x] 13-G 每个字段就地校验，非法值标红但不静默钳制。
- [x] 13-H 条目状态图标、"只看有问题"、"展开问题条目"、搜索匹配池名与地址。
- [x] 13-I 稳态绘制路径上没有 `FindPropertyRelative`、没有 `GUIContent`/`Color` 分配、没有字符串拼接；`projectChanged` 连发合并为一次；字段缓存的失效判据是"数组长度变化"且有兜底。
- [x] 播放期间禁改配置，统计不写入 SO。

勾选含义是**代码已落盘、存根编译通过、逐符号核对过**，不等于已经在 Unity 里验证：存根只能证明类型与重载对得上，证明不了 `helpBox` 里两列并排的实际对齐、字号加一号后会不会被行高裁掉、下拉框在窄面板里会不会被挤成省略号（见 6.3）。在 Unity 里跑起来、亲眼看过面板之前，以上都应视为"未验证"。

## 6. 实施记录

### 6.1 复用与改动

`GameObjectPoolSettingInspector` 是重构而不是新建，沿用了原有骨架：`GameFrameworkInspector` 基类、`_pools` 的序列化访问入口、`_expanded` 折叠状态、`Undo.undoRedoPerformed` 与 `EditorApplication.projectChanged` 双订阅、`RefreshValidation` 缓存诊断、`PingAssetByName` 按文件名定位资源。条目内的 `FindPropertyRelative` 已改为一次性绑定缓存，见 13-I-5。

| 区域 | 改动 |
|---|---|
| 顶部 | 标题行加"展开全部/折叠全部"；说明改为可折叠，避免长期占据首屏 |
| 过滤 | 搜索从"只匹配池名"扩展为池名 + 地址；新增"只看有问题"；新增条目计数 |
| 条目结构 | 从 11 行平铺改为四个分组 + 分隔线 + **两列并排**（9 个字段行压到 6 行）；容器从 `"box"` 改为 `helpBox`，每池独立框、框间 6px |
| 过期字段 | 两个时间字段改成「枚举下拉 + 秒数」组合行（「按时间回收 / 永不过期」）；整池过期选永不过期时收起扫描间隔 |
| 校验 | 每个字段就地判一次并标红；新增四条过期时序提示在 EditorValidator 侧 |
| 默认值 | 从 Inspector 里的 9 个魔数改为引用 `GameObjectPoolEntryDefaults` 的 9 个常量；新增条目级"恢复默认"与全局"全部恢复默认值" |
| 诊断 | 诊断带"第 N 条"前缀；新增"展开问题条目"；标题行状态图标 |
| 命名 | 常量改为全大写下划线（`MAX_MESSAGE_COUNT` / `LABEL_WIDTH` / `EXPIRE_MODE_WIDTH` / `ICON_SLOT_WIDTH` / `ERROR_COLOR`），与既有 `GameObjectPoolConfigError` 的常量风格一致。原 `FOREVER_TOGGLE_WIDTH` 随勾选框一起删除 |

### 6.1b 二次重构：性能与代码结构（13-I）

单文件 1093 行拆成三个，`GameObjectPoolInspectorContent.cs` 与 `GameObjectPoolEntryDrawer.cs` 为新建。

| 项 | 改动 |
|---|---|
| 文件拆分 | 主面板 869 行／条目绘制 420 行／文案与积木 462 行（另有 `GameObjectPoolEditorValidator` 304、`GameObjectPoolExpireRule` 99、`GameObjectPoolEntryDefaults` 89）；`GameObjectPoolFieldName` 收 11 个序列化字段名 |
| 字段缓存 | 新增 `GameObjectPoolEntryDrawer.Bind/IsBoundTo/DrawFields/ApplyDefaults/ClearIdentity`；主面板用 `List<GameObjectPoolEntryDrawer>` 与 `arraySize` 同步 |
| 文案静态化 | 37 个 `static readonly GUIContent` + 2 个惰性图标；就地错误文案自带 `×` 前缀 |
| 事件合并 | `projectChanged` 只置 `_validationDirty`，重绘用一次 `delayCall`；`OnDisable` 撤掉未执行的回调 |
| 预格式化 | `_errorLines` / `_warningLines` / `_titles` / `_entryCountLabel` / `_passSummary` / 两个溢出提示都在 `RebuildValidation` 生成 |
| 结构操作 | `DrawEntry` 返回 `GameObjectPoolEntryAction`，结构改动一律在 GUI 栈收尾后执行；`MakeUniqueCopyName` 改为 `HashSet` 一次收表 |
| 简化 | 删掉 `DrawExpireRow` 的 `instanceLevel`、`DrawFloatField` 的 `allowZero`、`DrawEntryFooter` 的 `index`、`ContainsPoolName`、`SyncExpandedState`；`IsReadOnly` 属性改字段 |
| 新增 .meta（手写 GUID，已确认与 `Assets`、`Packages` 无冲突） | `GameObjectPoolInspectorContent.cs` = `21737b5f1cf24a638992315ebe1e90c3`；`GameObjectPoolEntryDrawer.cs` = `0db87bd6ddd84e1e9a06c2f24588c690` |

### 6.2 字段名与序列化路径

`FindProperty("_pools")` 与 11 个 `FindPropertyRelative` 的名字必须与 `GameObjectPoolConfig` 的公有字段完全一致，本次全部沿用未改：`PoolName` / `AssetName` / `PrewarmCount` / `PrewarmPerFrame` / `Capacity` / `MaxInstanceCount` / `AutoReleaseInterval` / `ExpireTime` / `PoolExpireTime` / `PoolAutoReleaseInterval` / `Priority`。资产 YAML 仍是 `_pools: []`，**没有新增序列化键**（永不过期用哨兵值，不加 bool 字段）。

### 6.3 验证

| 项 | 结果 |
|---|---|
| 编译（含 `GameFrameworkInspector`、两个校验器、三个 Runtime 文件、三个 Inspector 文件） | **通过**：`%TEMP%/gp_verify_editor`（netstandard2.1 + C#9 + UnityEngine/UnityEditor 存根，定义 `UNITY_EDITOR;UNITY_2018_3_OR_NEWER`，`TreatWarningsAsErrors=true`）**0 警告 0 错误**。存根只能证明"类型与重载对得上"，不能证明运行期布局与视觉 |
| Runtime 侧双模式（`%TEMP%/gp_verify`） | 定义与不定义 `UNITY_EDITOR` **均 0 警告 0 错误** |
| GUI 栈配对（脚本统计，跳过注释与字符串；16:20 六文件重测） | `BeginHorizontal/EndHorizontal` **12:12**、`BeginVertical/EndVertical` **1:1**、`BeginDisabledGroup/EndDisabledGroup` **6:6**、`BeginChangeCheck/EndChangeCheck` **2:2**（`try/finally` 各成对）。布局改造后 `BeginHorizontal` 由 10 增至 12——新增的两处两列并排行（身份行、清理与过期第 2 行）。二次重构前 `BeginVertical` 是 1:2（提前 return 的那条路径），现已消除 |
| 括号/引号平衡（脚本核对；16:20 六文件重测） | 6 个文件 `{}`/`()`/`[]` 全部相等。`gameObjectPoolInspectorContent.cs` 与 `GameObjectPoolSettingInspector.cs` 的原始 `{` 各比 `}` 多 1，来自字符串插值，剥掉字面量后平衡 |
| 稳态绘制路径的查找与分配 | 脚本核对：`FindPropertyRelative` 只出现在 `Bind`（结构变化时，11 次一次）与复制逻辑（点击时）；`new GUIContent` 39 处全部在静态字段初始化或惰性图标里，不在绘制路径上 |
| 常量与判定方法引用（15:58 目录重组后重新核对） | 逐符号核对：三个 Inspector 文件与两个校验器引用的成员全部存在（默认值 → `GameObjectPoolEntryDefaults`，哨兵值与判定 → `GameObjectPoolExpireRule`） |
| Inspector 对配置类型的新增依赖 | 无。只依赖 `GameObjectPoolSetting` / `GameObjectPoolConfig`（Runtime）与 `GameObjectPoolEntryDefaults` / `GameObjectPoolExpireRule` / `GameObjectPoolEditorValidator` / `GameObjectPoolValidationMessage`（均在本程序集 `TEngine.Editor.GameObjectPool`） |
| 存根需补的 API（均为真实 Unity API，不是为编译硬造的） | `SerializedProperty.propertyPath` / `.serializedObject`、`GUILayout.Button(GUIContent, GUIStyle, params)`、`EditorGUI.LabelField(Rect, GUIContent, GUIStyle)`、`EditorApplication.delayCall` |
| Unity 内编译 | **未执行**（未进入 Play Mode，也没有取到 Unity 的编译日志） |
| 面板目视效果 / Play Mode 只读 / 实际性能 | **未确认**。`GameObjectPoolMonitorInspector.cs` 已移出 `GameObjectPool/`，暂存为 `Docx/架构设计/GameObjectPool/待启用/GameObjectPoolMonitorInspector.cs.txt`：它依赖的 `GameObjectPoolMonitor`、`IGameObjectPoolModule`、`GameObjectPoolInfo`、`GameObjectPoolInstanceInfo` 全工程都还没有（分别属 03/10/14 步），留在源码目录会让 `TEngine.Editor` 直接编译失败。另外它显示的两套状态字段都已删除——`GameObjectPoolModuleState`（2026-10-25 17:00）与 `PooledInstanceState`（18:10），启用时要把状态显示与状态列一起去掉，实例侧只剩 `IsSpawned`。到第 14 步把这些类型落地后，改回 `.cs` 移入 `Assets/TEngine/Editor/GameObjectPool/` 即可，`.meta` 沿用 guid `7dcdf01fe4cb4fb3a8a75ef4347a45df` |

存根编译的局限要说清：它验证的是"这些 API 在 Unity 里存在且签名匹配"，验证不了 `helpBox` 里套 `foldoutHeader` 的实际观感、`EditorGUIUtility.labelWidth` 与手动 `GUILayout.Width` 是否真的对齐、`DrawRect` 的分隔线在两种皮肤下的对比度。这些只能等第一次在 Unity 里打开面板时看。

**性能优化同样没有实测**：13-I-7 的数字是按代码路径数出来的，不是 Profiler 采样结果。等面板能在 Unity 里打开后，用 Profiler 的 Hierarchy 视图对比"面板静止 5 秒"的 `GC Alloc`，才能确认 13-I-3 的第 1、2 条真的归零。

下一步：[14：Mono 可视化与只读面板](14-Mono可视化与只读面板.md)。


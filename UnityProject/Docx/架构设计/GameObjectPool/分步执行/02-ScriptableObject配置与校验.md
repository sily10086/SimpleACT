# 第 02 步：ScriptableObject 配置与校验

前置：第 01 步确定字段迁移与资源边界。  
本步完成后：配置可序列化、可校验、可装载为运行期字典；尚不创建对象池、不加载资源、不创建场景实例。  
状态：已实施（dotnet 静态编译通过；Unity 内未编译、未运行）。

## 1. 目标与文件改动

运行目录统一为 Assets/TEngine/Runtime/Module/GameObjectPoolModule/。

| 目标 | 文件 | 改动与功能 |
|---|---|---|
| 02-A | GameObjectPoolSetting.cs | 新增/恢复 SO，只有一个私有字段 `_pools`（`GameObjectPoolConfig[]`） |
| 02-B | GameObjectPoolConfig.cs | `GameObjectPoolConfig`（SO 的序列化条目，可写公有字段）+ `RuntimePoolConfig`（运行期只读副本，readonly 字段）；字段为 YooAsset 定位地址，不含运行状态 |
| 02-C | GameObjectPoolValidator.cs | **单项校验，编辑器独占**（整段 `#if UNITY_EDITOR`；失败返回 `const string` 文案，无枚举、无 `Describe`） |
| 02-C2 | Editor/GameObjectPoolEditorValidator.cs | **编辑器完整诊断**（只存在于 TEngine.Editor 程序集，发布版不带这段代码） |
| 02-D | `GameObjectPoolSetting.TryBuildConfigs()`（**SO 上的普通实例方法**）+ `GameObjectPoolConfigBuildResult`（与 02-B 同文件） | 全量校验通过后返回 `RuntimePoolConfig[]`，失败返回首个原因 |

命名统一为 Config 词根：原 GameObjectPoolDefinition 改名为 **GameObjectPoolConfig**，运行期容器与 API 同步改为 configs、ContainsConfig、TryBuildConfigs，避免文档与代码词根分裂。SO 只有一个私有字段 `_pools`，YAML 即 `_pools: []`，不再有模块级嵌套键。ScriptableObject 不使用 OnEnable 自动启动池。

**构建入口不设静态 Builder 类（用户 2026-10-25 14:59 定）**：`TryBuildConfigs` 直接写成 `GameObjectPoolSetting` 的实例方法，调用形式是 `setting.TryBuildConfigs()`，不再有 `GameObjectPoolConfigBuilder.TryBuildConfigs(setting)`。它只是「把这份资产翻译成只读配置数组」，数据来源就是自己，没有独立成工具类的必要；少一层类型，调用处也少一个中间概念。代价是资产为 null 时无法调用，缺配置的判空责任上移到模块初始化分支——那本来就属于模块的前置条件，不是配置内容错误。

**两个配置类型，各管一段（用户 2026-10-25 15:00 定稿，推翻 14:49 的"只保留一个类型"）**：不设模块级结构体，但保留运行期只读副本。

| 类型 | 角色 | 字段形态 | 是否序列化 |
|---|---|---|---|
| `GameObjectPoolConfig` | SO 里的序列化条目 | 公有可写字段（Unity 要求） | 是，`_pools[]` 的元素 |
| `RuntimePoolConfig` | 运行期字典的值 | `readonly struct` + 只读字段 | 否，只存在于内存 |

两者的字段一一对应，没有任何语义转换，构造函数一次按值复制。为什么值得多一个类型：序列化要求字段可写（Unity 不序列化 `readonly` 字段），而运行期希望"拿到就不能改"——一个类型无法同时满足这两点，硬合并只能靠调用纪律保证只读。分成两个之后，模块的字段类型直接就是只读的，写错会编译失败，而不是运行时被悄悄改掉。

## 2. 字段合同

配置只有逐池条目一种，每个条目自带全部参数，不存在"全模块共用"的配置项。

**`GameObjectPoolConfig`（每个池一份，`_pools[]` 的元素）**

| 字段 | 类型/约束 | 功能 |
|---|---|---|
| PoolName | 非空字符串，Ordinal 大小写敏感；拒绝首尾空白 | 稳定逻辑键，不随资源重命名 |
| AssetName | 非空字符串，YooAsset 定位地址；本工程采集规则为 AddressByFileName，**地址＝文件名去扩展名**（如 `Coin`）；不含扩展名、不含反斜杠 | 池的唯一资源来源，由 IResourceModule 加载；不保存 Prefab 引用 |
| PrewarmCount | int ≥ 0 | 启动总量目标 |
| PrewarmPerFrame | int > 0 | **本池**分帧预热时每帧最多发起的创建请求数；两个池各自预热互不占用对方的预算 |
| Capacity | int ≥ 0 | 底层软保留目标 |
| MaxInstanceCount | int ≥ 0，0 为不限 | 实例硬上限 |
| AutoReleaseInterval | 有限 float > 0 | 底层自动清理间隔（单实例层面） |
| ExpireTime | 有限 float > 0；或哨兵值 `float.MaxValue`（`GameObjectPoolExpireRule.NeverExpireInstance`）表示永不过期 | **实例级**空闲过期时间，底层按单对象 LastUseTime 判定 |
| PoolExpireTime | 有限 float >= 0，**0 为关闭**（`GameObjectPoolExpireRule.NeverExpirePool`） | **整池**过期时间：池内无借出、无预热时连续空闲多少秒后整池释放 |
| PoolAutoReleaseInterval | 有限 float > 0，**仅当 `PoolExpireTime > 0` 时校验** | **本池**多久检查一次整池是否该释放（秒），默认 5；**只是"多久看一次"，空闲多久释放由 PoolExpireTime 决定**；PoolExpireTime 为 0 时该值不参与判断，因此也不校验 |
| Priority | int | 底层池清理优先级 |

编辑器新增条目的建议默认值：预热 0、**每帧预热 4**、Capacity 32、硬上限 0、间隔 60、实例过期 60、**整池过期 0（关闭）**、**整池自动释放间隔 5**、Priority 0。**这九个值全部以常量形式定义在编辑器程序集的 `GameObjectPoolEntryDefaults` 上**（`PrewarmCount` / `PrewarmPerFrame` / `Capacity` / `MaxInstanceCount` / `AutoReleaseInterval` / `InstanceExpireTime` / `PoolExpireTime` / `PoolAutoReleaseInterval` / `Priority`），Inspector 的"添加条目"与"恢复默认值"共用同一个 `ApplyDefaults`，编辑器代码里没有魔数。这些只是起始值，不是性能结论。

**为什么默认值不放在 `GameObjectPoolConfig` 上**（2026-10-25 15:55 改）：这个 struct 是资产的数据契约，回答的是"存了什么"；默认值回答的是"作者点添加之后先看到什么"，是编辑器表单的事，运行期永远不会读。放进数据契约会让一个纯数据 struct 携带只对编辑器成立的常量，发布版也白白多出这些元数据（虽然 `const` 会被内联，但语义污染是实的）。

另有两个常量描述"永不过期"的写法，见 02-E：`GameObjectPoolExpireRule.NeverExpireInstance = float.MaxValue`、`NeverExpirePool = 0f`；以及开关从"永不过期"切回"按时间回收"时填入的 `GameObjectPoolEntryDefaults.PoolExpireTimeOnEnable = 60f`（它不是新增条目的默认值，默认值仍是关闭）。

**"哪些值代表关闭"也不放在 `GameObjectPoolConfig` 上**：它和默认值一样只对编辑期有意义，运行期只看字段里的数字本身。因此单独建 `GameObjectPoolExpireRule`，放在 **`TEngine.Editor` 程序集**（`Editor/GameObjectPool/`），和 `GameObjectPoolEntryDefaults`、`GameObjectPoolEditorValidator` 同居一处。

它**不需要** `#if UNITY_EDITOR`：`TEngine.Editor.asmdef` 的 `includePlatforms` 只有 `Editor`，发布版整体不编译这个程序集，类型自然不存在；再套一层宏只会多一个"这段到底编没编进去"的疑问。这与 `GameObjectPoolValidator` 不同——后者在 Runtime 程序集，只能靠宏把代码摘掉。

校验器不调用这里的判定（它连引用都做不到：Runtime 不能引用 Editor）。它要答的问题是"整池过期是否已开启"，而走到那一步时负数与 NaN / Infinity 已被上一条规则排除，写 `PoolExpireTime > 0f` 与这边的 `!= 0f` 在合法值域上等价。两边问的其实是两个问题，不是同一个判定的两份实现，见 02-E。

`PoolExpireTime` 默认为 0 是刻意的：不开启该字段的池行为与此前完全一致，需要回收内存的池逐条显式开启，避免一次性改变全部池的运行时生命周期。

**SO 结构（2026-10-25 14:49 定，15:00 复核维持）**：`GameObjectPoolSetting` 内部**只有 `_pools` 一个数组**，每个元素直接就是 `GameObjectPoolConfig`，字段平铺在元素里，没有嵌套的"模块级 / 条目级"分层。唯一被推翻的中间方案是把 `PrewarmPerFrame` / `PoolAutoReleaseInterval` 做成 SO 顶层的 `_moduleConfig`——错在把每池独立的参数当成了全模块共用。序列化条目 `GameObjectPoolConfig` 与运行期副本 `RuntimePoolConfig` 是两个类型但不是"两份配置来源"：前者只被 SO 与 Inspector 使用，后者只被模块使用，字段按值单向复制。

**命名规则（2026-10-25 14:15 定）**：公有字段与公有属性一律大驼峰；私有字段一律 `_` 前缀小驼峰；局部变量与参数仍是小驼峰。`GameObjectPoolConfig` 的 11 个公有字段是 `PoolName`、`AssetName`、`PrewarmCount`、`PrewarmPerFrame`、`Capacity`、`MaxInstanceCount`、`AutoReleaseInterval`、`ExpireTime`、`PoolExpireTime`、`PoolAutoReleaseInterval`、`Priority`；`RuntimePoolConfig` 的 11 个公有只读字段用**完全相同的名字与拼写**（只在语义上是 `readonly`），这样从配置项到运行副本的复制是逐字段同名赋值，不会出现两套命名需要对照；`GameObjectPoolSetting` 只有一个私有字段 `_pools`，另给大驼峰只读属性 `Pools` 供上层读。

这条规则会同时改变 Schema 路径，实施时必须一起改，否则 Inspector 静默返回 null：Inspector 读条目字段用 `FindPropertyRelative("PoolName")` 这类相对名，读条目数组用 `FindProperty("_pools")`；`GameObjectPoolSetting.asset` 的 YAML 只有 `_pools: []` 一个键，没有嵌套结构。

PoolName 与 AssetName 是两个独立键：池名是业务调用入口，地址是资源来源。同一 AssetName 可以被多个池引用（例如同一预制体配不同预热和过期参数），此时共享同一份资源引用；同一 PoolName 不可重复。

**本组细化建议：**PrewarmCount 不得超过 Capacity，也不得超过非零 MaxInstanceCount。因为复用的底层 Register 会在超容量时释放空闲对象，允许矛盾目标会产生反复创建却无法完成的问题。启动配置报错，动态预热请求返回明确失败；不默默修改数值。

## 3. 设计思路

SO 只表达作者意图。模块初始化时调用 `setting.TryBuildConfigs()`，逐条校验通过后复制为 `RuntimePoolConfig`，装进一个**数组**返回；模块再遍历这个数组，建自己的 `Dictionary<string, RuntimePoolConfig>(Ordinal)`。副本生成后模块不再回头读 SO。模块可保留只读资产引用供诊断，但不据此实时重载。

**构建结果给数组，字典由模块自己建（2026-10-25 17:15 定）**：字典是"按池名查"的运行期索引，属于模块；配置资产只负责把条目翻译成只读形态，没有理由替调用方决定用哪种容器。而且数组保留资产里的条目顺序——字典不保序，池列表的顺序会随哈希码变化，诊断和面板都不好对。每条 `RuntimePoolConfig` 自带 `PoolName`，建索引不需要额外的键值对结构。

`GameObjectPoolConfig` 与 `RuntimePoolConfig` 都是值类型：前者默认值是字段为零/null 的数据，不存在"条目对象为 null"的判断；后者用 `readonly struct`，通过构造函数一次赋值，参数按值传入，不以 in/out/ref 传递。SO 继承 ScriptableObject 才使用 class；不为纯配置额外引入引用对象。

配置与资源之间没有硬引用：SO 不会把预制体拉起进资源包，是否打包由采集目录与资源包配置决定。因此"配置里有地址"不等于"资源一定可用"，地址有效性属于建池时的运行期检查（第 04 步），不在本步的纯数据校验里加载资源。

加载与释放不归属本步：池创建时由 IResourceModule.LoadAsset<GameObject>(AssetName) 同步取一次预制体资产引用并保存在池内，实例用 Instantiate 生成，池销毁时把同一个引用交给 UnloadAsset 归还（第 04、07 步）。本步不引入 IResourceModule 调用、不引入 ConfigSystem / TbPoolMap 依赖。

校验只在编辑器下做，**发布版一行都不带**：

| 层 | 内容 | 编译范围 | 消费方 |
|---|---|---|---|
| 单项规则（02-C） | 10 条：池名空、地址空、数量负数、每帧预热 ≤0、清理间隔非有限或 ≤0、整池自动释放间隔非有限或 ≤0（**仅整池过期开启时**）、实例过期非有限或 ≤0、池级过期非有限或 <0、预热超 Capacity、预热超硬上限 | 文件整体包在 `#if UNITY_EDITOR`；失败直接返回 `const string` 文案，没有错误码枚举、没有 `Describe` 翻译层 | `TryBuildConfigs()`（编辑器下）、`RegisterPool` 动态注册 |
| 完整诊断（02-C2） | 上面全部 + 首尾空白、重名、扩展名/反斜杠/前导斜杠、同地址多池共享提示 | `TEngine.Editor` 程序集，**发布版连元数据都没有** | 第 13 步 Inspector |

**为什么发布版不校验（2026-10-25 15:04 定）**：配置是作者在编辑期填的，打包时内容已经定死；发布版跑的是同一份数据，每次启动再逐条算一遍不改变任何结果，只是把一份必然通过的判断又执行一次。省掉的不只是几条 `if`：常量文案、`IsPositiveFinite` 这些符号都不会进包。

代价是运行期不再兜底。负数 `Capacity` 会直接进入底层 `Register`，不会被拦下。这个代价被接受：SO 只有作者能改，构建前 Inspector 会把这些问题全报一遍，漏网的只可能是手改 YAML。

**但有一类问题不能放任**：空池名在发布版会一路进到字典索引并抛 `ArgumentNullException`——那是异常，不是"明确失败"，与失败合同冲突。所以 `TryBuildConfigs` 与 `RegisterPool` 各用一句话挡掉 `null` 与空串，只挡这一个，不复制任何字段规则。**重名不在构建阶段拦**（2026-10-25 17:30 用户决定）：判重是编辑器诊断的职责，只在 Inspector 侧报错标红；发布版真出现重复池名（手改 YAML、合并冲突、运行时重复登记），模块建索引时后者覆盖前者，结果仍是"每个池名一条配置"，只是条数少于条目数。

与 `TEngine.Editor` 的诊断不冲突：诊断负责"给作者看"，单项规则负责"构建配置时挡住非法条目"，两者都在编辑器下编译，发布版两边都不存在。

## 4. 逐目标伪代码

### 02-A、02-B：声明纯配置

~~~csharp
[CreateAssetMenu(menuName = "TEngine/GameObjectPoolSetting", fileName = "GameObjectPoolSetting")]
public sealed class GameObjectPoolSetting : ScriptableObject
{
    // 唯一的序列化字段：每个池一个条目，条目自带全部参数
    [SerializeField] private GameObjectPoolConfig[] _pools = null;

    public GameObjectPoolConfig[] Pools => _pools;

    // 不在此建立字典、根节点、任务或资源加载。
    // 私有字段一律 _ 前缀小驼峰 + 只读属性：SO 只表达作者意图，业务代码不能反写配置。
}

[Serializable]
public struct GameObjectPoolConfig
{
    // 只有字段。默认值在编辑器程序集的 GameObjectPoolEntryDefaults，
    // "关闭过期"的哨兵值与判定在 GameObjectPoolExpireRule（同一个编辑器程序集，
    // 目录 Assets/TEngine/Editor/GameObjectPool/）。数据契约不承载任何只对编辑期成立的常量。

    public string PoolName;
    public string AssetName;              // YooAsset 定位地址：文件名去扩展名，不带 Assets/ 前缀

    public int PrewarmCount;
    public int PrewarmPerFrame;           // 本池每帧预热预算，必须 > 0
    public int Capacity;
    public int MaxInstanceCount;          // 0 = 不限

    public float AutoReleaseInterval;
    public float ExpireTime;              // 实例级：必须 > 0，或 float.MaxValue 表示永不过期
    public float PoolExpireTime;          // 整池级：0 = 关闭整池过期
    public float PoolAutoReleaseInterval; // 本池整池自动释放检查间隔（秒），仅整池过期开启时要求 > 0

    public int Priority;
}

// ---- 下面两个类型不属于数据契约，都在 TEngine.Editor 程序集（发布版不编译该程序集，故无需宏）----
namespace TEngine.Editor.GameObjectPool
{
    public static class GameObjectPoolExpireRule
    {
        public const float NeverExpireInstance = float.MaxValue;  // 实例级：底层走不到过期分支
        public const float NeverExpirePool = 0f;                  // 池级：0 本来就是关闭

        public static bool IsInstanceExpireDisabled(float expireTime);
        public static bool IsPoolExpireDisabled(float poolExpireTime);
    }

    public static class GameObjectPoolEntryDefaults
    {
        public const int PrewarmCount = 0;
        public const int PrewarmPerFrame = 4;
        public const int Capacity = 32;
        public const int MaxInstanceCount = 0;
        public const float AutoReleaseInterval = 60f;
        public const float InstanceExpireTime = 60f;
        public const float PoolExpireTime = GameObjectPoolExpireRule.NeverExpirePool;
        public const float PoolExpireTimeOnEnable = 60f;          // 开关从"永不过期"切回时填的值
        public const float PoolAutoReleaseInterval = 5f;
        public const int Priority = 0;
    }
}
~~~

公有字段一律大驼峰：Unity 序列化对公有字段没有命名限制，Inspector 的 `FindPropertyRelative("PoolName")` 按这个名字取，改回小驼峰会让它静默返回 null。实际编码时每个字段补 XML 注释（完成标准要求），这里省略以突出结构。

`PrewarmPerFrame` 与 `PoolAutoReleaseInterval` 不再做属性归一化，也不再由 `OnValidate` 回写：非法值一律交给校验器报出来，而不是悄悄改成默认值。默认值只用于 Inspector 的"添加条目 / 恢复默认"，见 02-C2 与 13-F。

`PoolAutoReleaseInterval` 多一条：它只在整池过期开启时被校验（02-E）。关闭状态下这个字段没有意义，Inspector 也不画它的输入框（13-E）。

`PoolExpireTime` 是**配置**，不是运行状态：池的空闲起点 `IdleStartSeconds` 属于运行池对象，不得写进 SO，也不得通过 Inspector 反写。

### 02-C：单项校验（编辑器独占）

整段包在 `#if UNITY_EDITOR` 里：发布版连这个类都不存在，不是"编译进来但不调用"。失败直接把 `const string` 文案返回给调用方，不再走「错误码 → switch → 文案」两层。

~~~csharp
#if UNITY_EDITOR
namespace TEngine
{
    /// <summary>
    /// 配置校验失败的文案。用字符串常量而不是枚举：
    /// 这些文案只出现在编辑器诊断与初始化日志里，不需要再做一层翻译。
    /// </summary>
    public static class GameObjectPoolConfigError
    {
        public const string PoolNameEmpty = "池名为空";
        public const string AssetNameEmpty = "YooAsset 地址为空";
        public const string NegativeCount = "数量字段不能为负数";
        public const string InvalidPrewarmPerFrame = "每帧预热数量必须大于 0，否则分帧预热无法推进";
        public const string InvalidAutoReleaseInterval = "自动清理间隔必须是有限正数";
        public const string InvalidPoolAutoReleaseInterval = "整池自动释放间隔必须是有限正数";
        public const string InvalidInstanceExpireTime = "实例空闲过期必须大于 0 且为有限值（0 会让空闲实例一被清理就释放，等于不缓存）";
        public const string InvalidPoolExpireTime = "整池过期时间必须是有限非负数（0 表示关闭）";
        public const string PrewarmExceedsCapacity = "预热数量超过 Capacity，当前后端无法保有该目标";
        public const string PrewarmExceedsMaxInstance = "预热数量超过 MaxInstanceCount";
    }

    /// <summary>
    /// 单个配置项的校验，只在编辑器下参与编译。
    /// </summary>
    public static class GameObjectPoolValidator
    {
        /// <returns>首个命中的文案常量；全部通过时为 null。</returns>
        public static string ValidateRuntime(GameObjectPoolConfig config)
        {
            if (string.IsNullOrEmpty(config.PoolName))
            {
                return GameObjectPoolConfigError.PoolNameEmpty;
            }

            if (string.IsNullOrEmpty(config.AssetName))
            {
                return GameObjectPoolConfigError.AssetNameEmpty;
            }

            if (config.PrewarmCount < 0 || config.Capacity < 0 || config.MaxInstanceCount < 0)
            {
                return GameObjectPoolConfigError.NegativeCount;
            }

            // 每帧预算为 0 会让分帧预热永远停在原地：既不报错也不推进
            if (config.PrewarmPerFrame <= 0)
            {
                return GameObjectPoolConfigError.InvalidPrewarmPerFrame;
            }

            if (!IsPositiveFinite(config.AutoReleaseInterval))
            {
                return GameObjectPoolConfigError.InvalidAutoReleaseInterval;
            }

            // 实例级过期：必须 > 0。永不过期用 float.MaxValue（有限正数，天然通过这条）
            if (!IsPositiveFinite(config.ExpireTime))
            {
                return GameObjectPoolConfigError.InvalidInstanceExpireTime;
            }

            // 池级过期与实例级过期规则相反：0 是"关闭"，不能套用 >0 的判断。
            // 必须排在下面那条之前：下一句要看它是否开启才决定校不校验
            if (!IsNonNegativeFinite(config.PoolExpireTime))
            {
                return GameObjectPoolConfigError.InvalidPoolExpireTime;
            }

            // 检查间隔参与累加，0 或非有限值会让整池过期永远等不到检查点。
            // 但整池过期关闭时这个值不参与任何判断，就不该被强制要求填正数。
            // 用 > 0 判"开启了没"而不是调 GameObjectPoolExpireRule：那个类型在编辑器程序集，
            // 本程序集引用不到；且此处负数/NaN/Infinity 已被上一条排除，两者等价（见 02-E）
            if (config.PoolExpireTime > 0f && !IsPositiveFinite(config.PoolAutoReleaseInterval))
            {
                return GameObjectPoolConfigError.InvalidPoolAutoReleaseInterval;
            }

            // 预热超过 Capacity 会形成"创建→被容量淘汰→再创建"的无法收敛循环
            if (config.PrewarmCount > config.Capacity)
            {
                return GameObjectPoolConfigError.PrewarmExceedsCapacity;
            }

            if (config.MaxInstanceCount > 0 && config.PrewarmCount > config.MaxInstanceCount)
            {
                return GameObjectPoolConfigError.PrewarmExceedsMaxInstance;
            }

            return null;
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
#endif
~~~

调用方只有两处：`GameObjectPoolSetting.TryBuildConfigs()` 与编辑器诊断。前者的这段判断同样包在 `#if UNITY_EDITOR` 内，所以发布版的构建路径只有「复制 + 空名挡板」两条语句。

**判重不在这里**：重复池名只在编辑器诊断（`GameObjectPoolEditorValidator.CheckPoolName`）里报错并标红，那是唯一发现点。构建阶段不扫集合、不引入 `HashSet`，返回的数组与条目一一对应（长度相同，可能含同名条目）。

### 02-C2：编辑器完整诊断（发布版剔除）

放在 `Assets/TEngine/Editor/Inspector/`、`TEngine.Editor` 程序集里。02-C 用的是 `#if UNITY_EDITOR` 条件编译——它住在 Runtime 程序集（模块与配置类型都在那儿），只能是同一文件里按符号裁剪；完整诊断没有这个约束，直接进 Editor 程序集，Player 构建根本不加载该程序集，比条件编译更彻底：诊断代码、集合分配、跨条目检查的字符串一个字节都不进包。

~~~csharp
using System;
using System.Collections.Generic;

namespace TEngine.Editor.Inspector
{
    public enum GameObjectPoolValidationSeverity
    {
        Error = 0,
        Warning = 1,
    }

    /// <summary>
    /// 一条诊断。按值传递；Inspector 在 RefreshValidation 时缓存列表，不在 OnGUI 里重建。
    /// </summary>
    public readonly struct GameObjectPoolValidationMessage
    {
        public readonly GameObjectPoolValidationSeverity Severity;
        public readonly int Index;      // -1 = 全局问题（非条目）
        public readonly string Field;
        public readonly string Message;

        public GameObjectPoolValidationMessage(
            GameObjectPoolValidationSeverity severity, int index, string field, string message)
        {
            Severity = severity;
            Index = index;
            Field = field;
            Message = message;
        }
    }

    /// <summary>
    /// 配置资产的完整诊断。只存在于编辑器程序集。
    /// 先复用 02-C 的单项规则，再补格式与跨条目检查——同一个字段不能被两边各判一次，也不能两边判得不一样。
    /// </summary>
    public static class GameObjectPoolEditorValidator
    {
        public static void Collect(
            GameObjectPoolSetting setting,
            List<GameObjectPoolValidationMessage> errors,
            List<GameObjectPoolValidationMessage> warnings)
        {
            errors.Clear();
            warnings.Clear();

            if (setting == null)
            {
                errors.Add(Error(-1, "setting", "配置资产为空"));
                return;
            }

            // 所有字段都是逐池的，没有模块级参数需要单独检查：
            // 每帧预热数量与整池自动释放间隔已由 ValidateRuntime 在下面逐条覆盖，
            // 这里只在它之外补格式与跨条目检查，避免同一问题报两遍
            GameObjectPoolConfig[] pools = setting.Pools;
            if (pools == null)
            {
                return;     // _pools 为 null 按空数组合法处理
            }

            // 名字 -> 首条下标，用来指明"跟哪一条撞了"；只存名字的话同名三条时无法定位
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            var assetOwners = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < pools.Length; i++)
            {
                GameObjectPoolConfig config = pools[i];

                string runtime = GameObjectPoolValidator.ValidateRuntime(config);
                if (runtime != null)
                {
                    errors.Add(Error(i, "config", runtime));
                }

                CheckPoolName(config.PoolName, i, names, errors);
                CheckAssetName(config.AssetName, i, assetOwners, errors, warnings);
            }
        }

        private static void CheckPoolName(
            string poolName, int index, Dictionary<string, int> names,
            List<GameObjectPoolValidationMessage> errors)
        {
            if (string.IsNullOrEmpty(poolName))
            {
                return;     // 已由必要规则报过，不重复报
            }

            if (poolName != poolName.Trim())
            {
                errors.Add(Error(index, "PoolName", "池名首尾不能有空白"));
            }

            if (names.TryGetValue(poolName, out int firstIndex))
            {
                errors.Add(Error(index, "PoolName", "池名重复：与第 " + (firstIndex + 1) + " 条相同"));
            }
            else
            {
                names.Add(poolName, index);
            }
        }

        private static void CheckAssetName(
            string assetName, int index, Dictionary<string, int> assetOwners,
            List<GameObjectPoolValidationMessage> errors,
            List<GameObjectPoolValidationMessage> warnings)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return;     // 已由必要规则报过
            }

            if (assetName != assetName.Trim())
            {
                errors.Add(Error(index, "AssetName", "地址首尾不能有空白"));
            }

            if (assetName.IndexOf('\\') >= 0)
            {
                errors.Add(Error(index, "AssetName", "地址不能包含反斜杠"));
            }

            if (assetName.StartsWith("/", StringComparison.Ordinal))
            {
                errors.Add(Error(index, "AssetName", "地址不能以 / 开头"));
            }

            if (HasAssetExtension(assetName))
            {
                errors.Add(Error(index, "AssetName", "地址不带扩展名：AddressByFileName 规则是文件名去扩展名"));
            }

            if (assetOwners.TryGetValue(assetName, out int firstIndex))
            {
                warnings.Add(new GameObjectPoolValidationMessage(
                    GameObjectPoolValidationSeverity.Warning, index, "AssetName",
                    "与第 " + firstIndex + " 条共用同一地址；释放按引用计数，单个池释放不保证资源立即卸载"));
            }
            else
            {
                assetOwners.Add(assetName, index);
            }
        }

        private static bool HasAssetExtension(string assetName)
        {
            return assetName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                || assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || assetName.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)
                || assetName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static GameObjectPoolValidationMessage Error(int index, string field, string message)
        {
            return new GameObjectPoolValidationMessage(
                GameObjectPoolValidationSeverity.Error, index, field, message);
        }
    }
}
~~~

三个必须守住的点：

1. **单项规则与完整诊断共用同一份判定**：`Collect` 第一件事就是调 `ValidateRuntime`，再补格式与跨条目检查。若两边各写一套，必然漂移成"面板放行、建字典时失败"。`ValidateRuntime` 直接返回文案，Editor 侧不需要再做错误码翻译。
2. **`PoolExpireTime` 与 `ExpireTime` 的 0 含义相反**，不能合并成"所有时间字段必须 > 0"：实例 `ExpireTime` 为 0 会让空闲实例在下一次清理时全部释放（不是"失效"，是"全过期"）；池级 `PoolExpireTime` 为 0 是设计好的关闭语义。实例级要"永不过期"用 `float.MaxValue`，见 02-E。
3. **判重只在编辑器诊断里做**（2026-10-25 17:30 用户决定）。`CheckPoolName` 用 `Dictionary<string,int>` 记首条下标，重复的那条报「池名重复：与第 N 条相同」并标红——这是全项目唯一的重名发现点。构建阶段不再扫一遍集合：`TryBuildConfigs` 因此不引入 `HashSet`，返回的数组与条目一一对应；发布版若真出现重名，模块建索引时后者覆盖前者，仍是"每个池名一条配置"。理由是判重属于"给作者看的配置体检"，与"把条目复制成运行期结构"是两件事，塞进构建阶段会让构建承担一份它交付不了的保证（发布版没有诊断、也没有人看日志）。

动态 `RegisterPool` 复用 `ValidateRuntime` 并额外检查名称冲突；内核消费已校验字典时不再层层重复规则。

### 02-D：全量通过后返回只读配置数组

~~~text
setting.TryBuildConfigs() -> GameObjectPoolConfigBuildResult    // SO 的实例方法
  pools = _pools
  pools 为 null -> { Success=true, Configs=Array.Empty<RuntimePoolConfig>(), Failure="" }
  configs = new RuntimePoolConfig[pools.Length]
  for i in 0..pools.Length-1:
    error = GameObjectPoolValidator.ValidateRuntime(pools[i])
    error != null -> { Success=false, Configs=null, Failure="第 i+1 条（池名）：" + error }   // 仅编辑器编译
    pools[i].PoolName 为 null 或空 -> { Success=false, Configs=null, Failure="第 i+1 条配置：池名为空" }
    // 不判重：重名只在编辑器诊断里报，构建阶段照条目一一复制
    configs[i] = new RuntimePoolConfig(pools[i])         // 下标与资产一致
  返回 { Success=true, Configs=configs, Failure="" }
  // 失败时 Configs 为 null，绝不返回半成品
  // 空名挡板与 RegisterPool 里那条同性质：只防字典索引抛 ArgumentNullException，不是第二套规则

调用方（模块 OnInit）：
  built = setting.TryBuildConfigs()
  built 失败 -> 错误日志，返回（字典保持为空）
  建根
  for i in 0..built.Configs.Length-1:
    _configs[built.Configs[i].PoolName] = built.Configs[i]
      // 数组顺序即生效顺序：重名时后者覆盖前者（重名本该被编辑器诊断挡住）
~~~

结果用 `readonly struct GameObjectPoolConfigBuildResult` 按值返回，不使用 out 参数。`Failure` 只在失败分支拼接，成功路径不产生字符串。

**`Configs` 是数组不是字典**：字典属于模块（索引结构、查得快），配置资产只产出数据。数组还顺带保住了资产里的条目顺序，`Failure` 里的"第 N 条"与 Inspector 里的排列完全对得上；换成字典后这个顺序就没了。

资产为 null 不在方法内判断：实例方法本身就要求调用者先有实例。模块初始化写 `setting == null → 明确失败`，与"资产存在但内容非法"分开报告，前者是接线缺漏，后者是配置错误。

`new RuntimePoolConfig(pools[i])` 是唯一一次类型转换，只做逐字段同名赋值，不挑选字段、不做单位换算、不补默认值——补默认值是 Inspector 的职责，运行期不该悄悄改作者填的数。

### 02-E：永不过期怎么表达（2026-10-25 15:20 定）

配置层**不新增 bool 字段**，用值表达"关闭"。理由：Inspector 开关、校验器、运行池这三类消费方各自只需要一个数，多一个 bool 就多一处"两个字段互相矛盾"的状态需要解释，也就要多一条"以谁为准"的规则。

| 字段 | 关闭值 | 依据 |
|---|---|---|
| `ExpireTime` | `float.MaxValue`（`GameObjectPoolExpireRule.NeverExpireInstance`） | 底层 `ObjectPool.Release` 的分支是 `if (_expireTime < float.MaxValue) expireTime = DateTime.UtcNow.AddSeconds(-_expireTime);`。值达到 `float.MaxValue` 时过期基准取 `DateTime.MinValue`，`LastUseTime <= MinValue` 恒不成立，没有任何实例会被判过期。这是底层本来就有的能力，不改底层、不加字段就能用 |
| `PoolExpireTime` | `0`（`GameObjectPoolExpireRule.NeverExpirePool`） | 池级过期本来就是 0 = 关闭（第 07 步 `ReleasePool` 的前置判断），再引入一个哨兵只会让同一字段出现两种"不算"的写法 |

两个字段的 0 含义**相反**，所以实例级不能写 0 表示关闭：`ExpireTime = 0` 时过期基准等于"现在"，下一次清理会把全部空闲实例释放，语义是"不缓存"而不是"不回收"。这一点由 Inspector 在字段下方就地提示，不指望作者记住。

判定集中在 `GameObjectPoolExpireRule` 上（**不在** `GameObjectPoolConfig` 上：数据契约只声明字段；也不在 Runtime 程序集：它属于 `TEngine.Editor`，见第 4 节的落点说明），服务 Inspector 与编辑器诊断两处：

~~~csharp
namespace TEngine.Editor.GameObjectPool
{
    public static class GameObjectPoolExpireRule
    {
        public const float NeverExpireInstance = float.MaxValue;
        public const float NeverExpirePool = 0f;

        public static bool IsInstanceExpireDisabled(float expireTime)
        {
            // NaN 与任何值比较都为 false，因此非法值不会被误判成"永不过期"；
            // Infinity 另外排除——那是填错，该由校验器报"必须是有限值"
            return !float.IsInfinity(expireTime) && expireTime >= NeverExpireInstance;
        }

        public static bool IsPoolExpireDisabled(float poolExpireTime)
        {
            // 负数不是"关闭"而是填错：留在字段里由校验器报出来，不能在这里被当成合法状态吞掉
            return poolExpireTime == NeverExpirePool;
        }
    }
}
~~~

**校验器为什么不调它**：`GameObjectPoolValidator` 在 Runtime 程序集，`GameObjectPoolExpireRule` 在 Editor 程序集，前者引用不到后者（反向也禁止）。校验器也不会因此写错：它问的是"整池过期开启了没"，而那时负数、NaN、Infinity 已被上一条 `IsNonNegativeFinite` 排除，`PoolExpireTime > 0f` 与这边的 `!= 0f` 在整个合法值域上等价。这两个是**不同的问题**（"开启了没" vs "这个写法是不是表示关闭"），不是同一个判定的两份实现，所以不构成漂移风险。第 15:55 版本曾把判定留在 Runtime 复用，代价是让一个纯编辑器概念占着 Runtime 目录——现在换回按职责放，用一行等价比较换掉那个妥协。

**连带改了一条校验规则**：`PoolAutoReleaseInterval` 只在 `PoolExpireTime > 0` 时要求有限正数。整池过期关闭时它不参与任何判断，若仍强制 > 0，作者只会随手填个 60 把红字消掉，规则本身什么也没保证。为此 `ValidateRuntime` 的判断顺序固定为 `AutoReleaseInterval` → `ExpireTime` → `PoolExpireTime` → `PoolAutoReleaseInterval`：最后一条要看前一条是否开启，顺序不能颠倒。

`ExpireTime` 的文案也跟着修正：原文案写"0 会让底层过期过滤失效"，与底层实际行为相反（0 会让全部空闲实例立刻算过期），现在写的是"0 会让空闲实例一被清理就释放，等于不缓存"。

## 5. 边界与成本
空数组合法；缺 Setting 资产属于明确初始化失败。Editor 改动 SO 不动态更新已有运行池；第一版播放模式禁改资产。

`GameObjectPoolValidator` 是单项规则的**唯一定义处**，但它与完整诊断一样只在编辑器下参与编译：发布版既不校验、也不携带这些常量文案与判定函数。真实体积差异可以验证——在同一份源码上分别以"不定义 / 定义 `UNITY_EDITOR`"编译，前者的程序集里搜不到 `GameObjectPoolValidator` 与任何一条失败文案。

现存 GameObjectPoolSetting.asset 的 _pools 为空，条目字段由 Prefab 引用改为 AssetName 字符串没有历史数据迁移成本；若实施时发现存在非空条目，必须先记录原字段值再决定是保留旧字段还是重建条目，不能手改 YAML 冒充迁移成功。

地址语义与打包的边界：预制体必须位于 `Assets/AssetRaw/*` 采集目录内，否则地址无效；远端资源包未下载时，池创建失败属于正常失败路径，不回退到 Resources、场景对象或空对象。同一地址被多个池引用时各持有独立引用计数，单个池释放只归还自己那一份。

## 6. 完成标准

- [x] 02-A SO 可表达设置，字段兼容性决定已记录。
- [x] 02-B 配置项类型名为 GameObjectPoolConfig，AssetName 语义明确为 YooAsset 定位地址，所有字段具有 XML 注释。
- [x] 02-C 单项校验只有 10 条规则，整段在 `#if UNITY_EDITOR` 内，失败直接返回 `const string` 文案；发布版不包含该类型与文案。
- [x] 02-C2 完整诊断位于 `TEngine.Editor` 程序集（不是 `#if UNITY_EDITOR` 包在 Runtime 文件里），发布版不含该代码；编辑器侧先调 `ValidateRuntime` 再补格式与跨条目检查。
- [x] 02-D 数组在全量通过后生成（保留条目顺序），**空池名在构建阶段挡掉、重名只由编辑器诊断报**（2026-10-25 17:30），配置不保存运行态；入口是 SO 的实例方法 `setting.TryBuildConfigs()`，没有独立的静态 Builder 类。
- [x] 两个 struct 分工明确：`GameObjectPoolConfig` 是可序列化条目（公有可写字段），`RuntimePoolConfig` 是 `readonly struct` 只读副本（字典的值），字段一一对应、按值复制；不使用结构体 in/out/ref 参数。
- [x] `PoolExpireTime` 与 `PoolAutoReleaseInterval` 都是逐池字段，校验分支与实例 `ExpireTime` 分开：`PoolExpireTime` 的 0 合法（关闭），`PoolAutoReleaseInterval` 只在整池过期开启时要求 > 0。
- [x] "永不过期"用哨兵值表达，不新增 bool 序列化字段：实例级 `float.MaxValue`、池级 `0`；判定只有 `GameObjectPoolExpireRule.IsInstanceExpireDisabled` / `IsPoolExpireDisabled` 两处（02-E）。
- [x] `GameObjectPoolConfig` 只声明字段：默认值、哨兵值、判定、校验都不在上面（2026-10-25 15:55 起）。
- [x] 九个默认值全部以 `const` 定义在 `GameObjectPoolConfig` 上，编辑器侧统一由 `ApplyDefaultsTo` 写入，没有散落的魔数。
- [x] 运行态字段（`IdleStartSeconds`、`ActiveCount`、`_pendingPrewarmCount`）不出现在 SO 与配置结构体中。
- [x] 配置中不存在 Prefab 引用；未加入资源加载、ConfigSystem 或自动启动行为。

## 7. 实施记录

### 7.1 实际文件与 GUID

| 文件 | GUID | 说明 |
|---|---|---|
| `Assets/TEngine/Runtime/Module/GameObjectPoolModule/` | `b7f3c1a94d2e46f0a8c5e93b17d6a204` | 新建运行目录 |
| `GameObjectPoolSetting.cs` | **`3c52c1003ee9e2d43ab4b6452b70a779`** | 手工指定 GUID，与 `GameObjectPoolSetting.asset` 的 `m_Script` 一致，Missing Script 因此恢复绑定；另含实例方法 `TryBuildConfigs()` |
| `GameObjectPoolConfig.cs` | `5d8e2f0b7a3c4198b6e4d2c85f01a937` | 含 `GameObjectPoolConfig`、`RuntimePoolConfig`、`GameObjectPoolConfigBuildResult` |
| `GameObjectPoolValidator.cs` | `9c4a1e6f2b8d43c7a0e5f3b61d9c8472` | 10 条单项规则 + 文案常量，整段 `#if UNITY_EDITOR` |
| `Assets/TEngine/Editor/GameObjectPoolEditorValidator.cs` | `2f7b6d3e9a1c45b8a3e0c7d54f8b9126` | 完整诊断，仅存在于 `TEngine.Editor` |

四个 `.meta` 均为手写，GUID 已确认在 `Assets` 与 `Packages` 内无冲突。程序集无需改动：`TEngine.Editor` 已引用 `TEngine.Runtime`（`24c092ae…`）；`TEngine.Runtime` 已引用 UniTask，本步不需要它。

### 7.2 字段兼容性

`GameObjectPoolSetting.asset` 现在只有一个键：`_pools: []`，没有 `prefab` 字段、也没有非空条目，**没有历史数据需要迁移**。字段命名与结构的几次演进都未留下需要迁移的数据：14:15 从无前缀改为 `_pools`；14:30 短暂把 `PrewarmPerFrame` / `PoolAutoReleaseInterval` 收进 `_moduleConfig` 嵌套键；14:49 取消 `_moduleConfig`，两个参数改为逐池字段；14:59 构建方法从静态类搬进 SO；15:00 恢复 `RuntimePoolConfig` 运行期副本。因为 `_pools` 一直是空数组、且这些改动都不落在序列化键上，YAML 始终只需保持 `_pools: []` 一行。

### 7.3 演进记录与偏离

1. **配置类型从 1 个回到 2 个**：14:49 曾取消 `RuntimePoolConfig`，15:00 按决定恢复——序列化条目保持 `GameObjectPoolConfig`（Unity 要求字段可写，不能加 `readonly`），运行期容器改回 `Dictionary<string, RuntimePoolConfig>`（`readonly struct`，字段只读）。
2. **`GameObjectPoolConfigBuildResult.Configs` 是数组，不是字典（17:15）**：构建阶段只把条目翻译成只读形态，容器选择留给模块；模块在 `OnInit` 里 `new Dictionary<string, RuntimePoolConfig>(StringComparer.Ordinal)` 之后遍历数组填进去。理由见 §3——字典是运行期索引，数组保住资产里的条目顺序。同时补一条空池名挡板：没有 `TryAdd` 之后 `null` 键会一路进字典索引抛 `ArgumentNullException`，与"明确失败"的合同冲突。**判重随后（17:30）从构建阶段整体移除**，见第 6 条。
3. **`TryBuildConfigs` 是 SO 的实例方法**：14:59 取消 `GameObjectPoolConfigBuilder` 静态类，方法搬进 `GameObjectPoolSetting`，调用形式 `setting.TryBuildConfigs()`；资产判空的责任上移到模块初始化分支。
4. **校验改成编辑器独占 + 文案常量**（15:04）：`GameObjectPoolValidator.cs` 整段包进 `#if UNITY_EDITOR`，删掉 `GameObjectPoolConfigError` 枚举、`GameObjectPoolConfigValidation` 结构体与 `Describe` 的 switch，改为 `const string` 文案 + `ValidateRuntime` 直接返回文案（null = 通过）。`TryBuildConfigs` 里那段逐条校验同样包进 `#if UNITY_EDITOR`。当时"重名判重保留在两种模式下"，17:30 已推翻，见第 6 条。
5. **`GameObjectPoolSettingInspector.cs` 做了最小必要改造**：它原本引用 `GameObjectPoolDefinition`、`.Prefab`、`GameObjectPoolValidator.CollectErrors/CollectWarnings`、`FindPropertyRelative("prefab")`，全部已不存在，不改则 `TEngine.Editor` 必然编译失败。本步只做「能编译 + 与新配置类型一致」：
   - `_errors/_warnings` 改为 `List<GameObjectPoolValidationMessage>`，统一走 `GameObjectPoolEditorValidator.Collect`；
   - `prefab` 字段换成 `AssetName`，「定位预制体」改为按文件名在 `Assets/AssetRaw` 内检索的「定位资源」；
   - 条目面板补上 `PrewarmPerFrame` / `PoolAutoReleaseInterval` 两个字段，新增条目时用 `GameObjectPoolEntryDefaults.PrewarmPerFrame` / `PoolAutoReleaseInterval` 填默认值，并补 `PoolExpireTime = 0`；
   - 删掉 `PrefabUtility.IsPartOfPrefabAsset` 检查（配置里已无 Prefab）。
   - 搜索框现在只按池名过滤，按地址过滤、错误定位到条目等交互留给第 13 步。
6. **判重从构建阶段移除，只留在编辑器诊断里**（17:30 用户决定）。`TryBuildConfigs` 删掉临时 `HashSet<string>` 与重名判断，`using System.Collections.Generic` 一并去掉，发布版构建路径只剩「复制 + 空名挡板」。`GameObjectPoolEditorValidator.CheckPoolName` 改为 `Dictionary<string,int>` 记首条下标，重复时文案是「池名重复：与第 N 条相同」——能指明位置，比只报名字有用。`RegisterPool` 的运行时拒绝重名**不变**：那是"追加一条新配置"的 API 合同，与"批量读取资产"不是同一件事。
   代价（明确接受）：发布版若真出现重名（手改 YAML、合并冲突），`OnInit` 按索引赋值时后者覆盖前者，`ConfigCount` 少于条目数且没有日志。判据是"配置只有作者能改，构建前 Inspector 会把重名标红"，与"校验只在编辑器"（15:04）同一条口径。若将来需要运行期提示，加一条日志即可，但不该把判重塞回构建阶段——那会重新变成"发布版做一份没人看的检查"。

### 7.4 验证

| 项 | 结果 |
|---|---|
| 三个新增脚本的 C# 9 静态编译（dotnet + UnityEngine 存根） | **通过，0 警告 0 错误**；15:00 恢复 `RuntimePoolConfig`、把 `TryBuildConfigs` 移入 SO 后重新编译，仍 0 警告 0 错误 |
| 双模式编译（15:04 校验改 `#if UNITY_EDITOR` 后） | 不定义 `UNITY_EDITOR`（模拟 Player）与定义 `UNITY_EDITOR`（模拟编辑器）**均 0 警告 0 错误** |
| 发布版是否真的不含校验代码 | **已核对产物**：Player 模式 dll 中 `GameObjectPoolValidator`、`GameObjectPoolConfigError`、`ValidateRuntime` 与全部中文失败文案均 0 次出现；加上 `UNITY_EDITOR` 后同一 dll 均出现。这是"宏包住了"的实证，不是只看源码 |
| `HashSet<string>(comparer, capacity)` 与 `Array.Empty<T>()` 是否可用 | 可用：`ProjectSettings.asset` 的 `apiCompatibilityLevel: 6` = .NET Standard 2.1 |
| 构建结果改为数组后重新核对（17:15） | 双模式编译 **0 警告 0 错误**；行为断言新增 5 组共 18 项（顺序保持、返回新数组、空串/null 池名在构建阶段失败且 `Configs` 为 null、`Ordinal` 大小写敏感、`_pools` 为 null 视为空配置），全套 51 项全绿 |
| 判重移出构建后重新核对（17:30） | 双模式编译 **0 警告 0 错误**；`%TEMP%/gp_run` 全套 **54 项全绿**（第 2 组失败样本改为空池名，第 9 组改为「构建不判重 + 模块建索引时后者覆盖前者」）；新增 `%TEMP%/gp_editor_run`（`UNITY_EDITOR` + `TEngine.Editor` 源码）验证编辑器诊断 **15 项全绿**：两条同名报在第 2 条并指明"与第 1 条相同"、三条同名报两次都指向首条、`Coin`/`coin` 不算重名、首尾空白单独报、空池名不在这里重复报、正常不重名零诊断、同名两条构建成功 |
| `GameObjectPoolSettingInspector.cs` 编译 | **未验证**：依赖 UnityEditor API，本机无 Unity 可编译 |
| Unity 内编译 / Play Mode | **未执行** |
| 配置资产绑定恢复 | **未在编辑器确认**，仅核对了 `m_Script` 与脚本 `.meta` 的 GUID 一致 |

`TEngine.Runtime` 这一侧本步之后应当可编译（`Settings.cs` 对 `GameObjectPoolSetting` 的引用已成立）。`TEngine.Editor` 仍有 1 个文件编译不过，与本步无关，属后续步骤：

| 文件 | 缺失类型 | 归属 |
|---|---|---|
| `Editor/GameObjectPool/GameObjectPoolMonitorInspector.cs` | `GameObjectPoolMonitor`、`GameObjectPoolInfo`、`GameObjectPoolInstanceInfo`（`GameObjectPoolModule` 与 `IGameObjectPoolModule` 已由第 03 步建好；`GameObjectPoolModuleState` 已于 2026-10-25 17:00 删除、`PooledInstanceState` 已于 18:10 删除，启用该文件时要一并去掉状态显示与状态列） | 10 / 14 步 |

（原表中还有 `GameObjectPoolWiringTool.cs`，2026-10-25 15:58 复查时该文件已不在工程内，不再计入。）

因此 **Unity 目前仍不能进入 Play Mode**，需等这三步的类型齐备；旧 `GameLogic/Module/GameObjectPoolModule/` 的编译错误要到第 12 步删除旧目录才消失。

### 7.5 Inspector 可视化改造（2026-10-25 15:35，第 13 步提前落地）

用户要求：面板分组美化、给默认值、过期时间可勾"永不过期"并把时间框收起不给填、参数就地校验（例如过期时间不能为负数）。编辑器侧细节写在 [13：配置资产 Inspector](13-配置资产Inspector.md) 的 13-E ~ 13-H；本节只记与本步（配置层）交叉的部分：

| 改动 | 文件 | 原因 |
|---|---|---|
| 补 `public GameObjectPoolConfig[] Pools => _pools;` | GameObjectPoolSetting.cs | 02 文档 14:15 就写明了这个只读属性，但代码里一直没落地，`GameObjectPoolEditorValidator` 的 `setting.Pools` 因此编译不过。补上即修好，不需要改诊断器 |
| 九个默认值常量（`PrewarmCount` … `Priority`）与 `PoolExpireTimeOnEnable` | Editor/GameObjectPoolEntryDefaults.cs | 默认值只能有一处定义：Inspector 的"添加条目"与"恢复默认"必须写同一组数。**15:55 从 `GameObjectPoolConfig` 移到编辑器程序集**，理由见 7.6 |
| 两个哨兵值与 `IsInstanceExpireDisabled` / `IsPoolExpireDisabled` | Editor/GameObjectPool/GameObjectPoolExpireRule.cs（`TEngine.Editor`，无需宏） | 开关状态与编辑器诊断共用同一个判定。**15:55 从 `GameObjectPoolConfig` 移出，15:58 从 Runtime 移到编辑器程序集**，理由见 7.6 |
| `PoolAutoReleaseInterval` 改条件校验，判断顺序调整 | GameObjectPoolValidator.cs | 整池过期关闭时该值不参与判断，强制 > 0 只会逼作者填一个永远用不到的数（02-E） |
| `InvalidInstanceExpireTime` 文案改写 | 同上 | 原文案"0 会让底层过期过滤失效"与底层实际行为相反 |
| 四条过期时序提示（过长、小于扫描间隔、扫描间隔不小于过期、预热与整池过期并存） | GameObjectPoolEditorValidator.cs | 都是"数值合法但行为与直觉不同"，不报出来只表现为内存回收不如预期 |

验证：Runtime 3 个文件 + 编辑器 7 个文件分别在 `%TEMP%/gp_verify` 与 `%TEMP%/gp_verify_editor`（netstandard2.1 + C#9 + UnityEngine/UnityEditor 存根，`TreatWarningsAsErrors`）下编译，Runtime 定义与不定义 `UNITY_EDITOR` 两种模式**均 0 警告 0 错误**，编辑器工程同样 0 警告 0 错误。**但这不是 Unity 内编译**：没有进 Play Mode，也没有取到 Unity 的编译日志。`GameObjectPoolMonitorInspector.cs` 依赖的 14 步类型尚未创建，该文件已移出源码目录、暂存于 `Docx/架构设计/GameObjectPool/待启用/`，所以它不再阻塞 `TEngine.Editor`；但**面板效果仍无法在编辑器里目视确认**。

### 7.6 编辑器内容与数据契约分离，编辑器代码独立目录（2026-10-25 15:55，15:58 修正）

用户先指出 `GameObjectPoolConfig` 上的常量与判定「都是编辑器阶段做的事，不要污染数据契约」，随后指出「这个不是应该放进 TEngine.Editor 这边吗？然后关于对象池的编辑器 Inspector 应该需要单独一个文件夹」。两次都照做；中间那一版把 `GameObjectPoolExpireRule` 放在 Runtime 目录、用宏摘掉，**已作废**。

**最终落点**

| 内容 | 位置 | 程序集 | 发布版是否存在 | 依据 |
|---|---|---|---|---|
| 11 个公有字段 | `Runtime/Module/GameObjectPoolModule/GameObjectPoolConfig.cs` | TEngine.Runtime | 是 | 资产要序列化它们，这是这个类型存在的唯一理由 |
| 逐条校验 | `Runtime/Module/GameObjectPoolModule/GameObjectPoolValidator.cs`（整段 `#if UNITY_EDITOR`） | TEngine.Runtime | 否 | 要被同程序集的 `GameObjectPoolSetting.TryBuildConfigs` 调用，**只能**在 Runtime |
| 2 个哨兵值 + 2 个判定 | `Editor/GameObjectPool/GameObjectPoolExpireRule.cs` | TEngine.Editor | 否 | 只服务 Inspector 开关与编辑器诊断；编辑器程序集整体不进发布版，**无需宏** |
| 10 个默认值常量 | `Editor/GameObjectPool/GameObjectPoolEntryDefaults.cs` | TEngine.Editor | 否 | 只有 Inspector 的"添加条目 / 恢复默认"写它们 |
| 完整诊断 | `Editor/GameObjectPool/GameObjectPoolEditorValidator.cs` | TEngine.Editor | 否 | 面板红字与问题列表 |
| 面板绘制 | `Editor/GameObjectPool/` 下 `SettingInspector` + `EntryDrawer` + `InspectorContent` | TEngine.Editor | 否 | 见第 13 步 13-I |
| 运行期可视化面板 | `Editor/GameObjectPool/GameObjectPoolMonitorInspector.cs` | TEngine.Editor | 否 | 第 14 步 |

**校验器为什么不调 `GameObjectPoolExpireRule`**：Runtime 引用不到 Editor，这是程序集方向决定的，没有变通余地。但也不需要变通——校验器问的是"整池过期开启了没"，而那一步之前负数、NaN、Infinity 已被 `IsNonNegativeFinite` 排除，`PoolExpireTime > 0f` 与 `ExpireRule.IsPoolExpireDisabled(x)` 的 `!= 0f` 在整个合法值域上等价。这是两个不同的问题，不是同一个判定的两份实现，因此写两遍不构成漂移风险。代价是这行等价关系必须写在注释里（`GameObjectPoolValidator.cs` 该处已有），否则下一个人会以为可以直接调用、然后发现编译不过。

**编辑器目录与命名空间**：`Assets/TEngine/Editor/GameObjectPool/` 下 7 个文件，命名空间统一 `TEngine.Editor.GameObjectPool`，不再散落在 `Editor/Inspector/`。移动时 `.cs` 与 `.meta` 一起搬，GUID 未变（`SettingInspector` 的 GUID 就是资产 `m_Script` 的引用，丢了面板绑定就断）。两个 Inspector 的基类 `GameFrameworkInspector` 仍在 `Editor/Inspector/Core/`，因此各自加了 `using TEngine.Editor.Inspector;`。目录新增 `.meta`（`6826a2b0…`）。

**产物级验证**（两个工程都在编译前清空 `bin`/`obj`，逐个模式核对 dll 字节）：

| 符号 | Runtime / Player 模式 | Runtime / 编辑器模式 | Editor 程序集 |
|---|---|---|---|
| `GameObjectPoolExpireRule` / `NeverExpireInstance` / `NeverExpirePool` | 0 次 | **0 次** | 各有 |
| `IsInstanceExpireDisabled` / `IsPoolExpireDisabled` | 0 次 | **0 次** | 各有 |
| `GameObjectPoolValidator` / `ValidateRuntime` | 0 次 | 各 1 次 | — |
| `PrewarmPerFrame` / `PoolName` / `RuntimePoolConfig` | 存在 | 存在 | — |

关键点：**`GameObjectPoolExpireRule` 在 Runtime 程序集的两种模式下都是 0 次**，说明它已经彻底不属于 Runtime；`GameObjectPoolValidator` 仍只在编辑器模式出现，宏照旧生效。三个工程均 0 警告 0 错误。

**验证时踩到的坑（值得记住）**：`dotnet build -p:DefineConstants=...` **不会**因为该属性变化而重编，MSBuild 的 up-to-date 判定不看它。第一次做双模式核对时拿到的是上一次的 dll，`GameObjectPoolExpireRule` 显示 0 次，差点被误判成"宏写错了"。正确做法是每次切换模式前先把 `bin`/`obj` 删干净。7.4 表里的双模式结论也是同一隐患，本次已按新方式重新核对。

下一步：[03：模块契约与生命周期骨架](03-模块契约与生命周期骨架.md)。

using System;

namespace TEngine
{
    /// <summary>
    /// 单个对象池的配置项，是资产里可序列化的条目。
    /// <remarks>
    /// 普通 struct：默认值就是"字段为零或 null 的数据"，不存在"条目对象为 null"的判断。
    /// 公有字段一律大驼峰——Unity 序列化与 Inspector 的
    /// <c>FindPropertyRelative("PoolName")</c> 都按这个名字读取。
    /// 它只在编辑与构建阶段使用；通过校验后由 <see cref="RuntimePoolConfig"/> 复制成只读形态
    /// 交给模块，运行期不读这个类型。
    /// 不保存预制体引用，只保存 YooAsset 定位地址；也不保存任何运行状态。
    /// <b>这里只有字段</b>：默认值、"关闭过期"的哨兵值与判定、逐条校验都属于编辑器阶段的事，
    /// 分别放在编辑器工具与 <c>GameObjectPoolExpireRule</c> 里，
    /// 本类型不承载任何只在编辑器里才成立的东西。
    /// </remarks>
    /// </summary>
    [Serializable]
    public struct GameObjectPoolConfig
    {
        /// <summary>
        /// 稳定逻辑键，Ordinal 大小写敏感，不允许为空或首尾带空白。
        /// 业务按它借出实例，不随资源重命名变化。
        /// </summary>
        public string PoolName;

        /// <summary>
        /// YooAsset 定位地址。本工程采集规则为 AddressByFileName，地址等于文件名去扩展名
        /// （例如 <c>Coin</c>），不带 <c>Assets/</c> 前缀、不带扩展名、不含反斜杠。
        /// 池创建时用它加载预制体，因此它不是"资源一定可用"的保证。
        /// </summary>
        public string AssetName;

        /// <summary>
        /// 启动预热总量目标，必须 ≥ 0；不得超过 <see cref="Capacity"/>，
        /// 也不得超过非零的 <see cref="MaxInstanceCount"/>，否则预热无法收敛。
        /// </summary>
        public int PrewarmCount;

        /// <summary>
        /// 这个池在分帧预热时每帧最多发起的创建请求数，必须大于 0。
        /// 它只约束本池的节奏：两个池各自预热互不占用对方的每帧预算。
        /// </summary>
        public int PrewarmPerFrame;

        /// <summary>
        /// 底层软保留目标，必须 ≥ 0。总数超过该值时底层会尝试释放空闲实例，它不是实例硬上限。
        /// </summary>
        public int Capacity;

        /// <summary>
        /// 实例总量硬上限，必须 ≥ 0；0 表示不限制。
        /// </summary>
        public int MaxInstanceCount;

        /// <summary>
        /// 底层自动清理检查间隔（秒），必须为有限正数。
        /// </summary>
        public float AutoReleaseInterval;

        /// <summary>
        /// 实例级空闲过期时间（秒），必须为有限正数。
        /// <remarks>
        /// 0 不是"关闭"：底层会把过期基准算成"现在"，任何已归还实例的 <c>LastUseTime</c>
        /// 都满足 <c>&lt;= 基准</c>，下一次清理就全部释放，等于不缓存。
        /// 要永不过期请填 <c>float.MaxValue</c>，不要填 0 或负数。
        /// </remarks>
        /// </summary>
        public float ExpireTime;

        /// <summary>
        /// 整池过期时间（秒），必须为有限非负数，0 表示关闭整池过期。
        /// 与 <see cref="ExpireTime"/> 的 0 含义相反：池内无借出且无预热时才从归零那一刻开始计时，
        /// 达到该值后整池被释放。检查动作由同条目的
        /// <see cref="PoolAutoReleaseInterval"/> 节流。
        /// </summary>
        public float PoolExpireTime;

        /// <summary>
        /// 这个池的整池自动释放检查间隔（秒），必须为有限正数。
        /// <remarks>
        /// 每个池按自己的间隔判断"该不该检查一次整池过期"，互不影响：
        /// 池内既无借出、也无进行中的预热请求，且连续空闲时间达到本池的
        /// <see cref="PoolExpireTime"/> 时，释放这个池持有的实例与资源引用。
        /// 它只是"多久看一次"，不是"空闲多久释放"；池级过期关闭时该值不参与判断。
        /// </remarks>
        /// </summary>
        public float PoolAutoReleaseInterval;

        /// <summary>
        /// 底层池清理优先级，数值越大越晚被回收。不是"禁止销毁"标记。
        /// </summary>
        public int Priority;
    }

    /// <summary>
    /// 校验通过后的运行期只读配置。
    /// <remarks>
    /// 不参与 Unity 序列化，因此可以用 readonly struct 与只读字段：
    /// 模块建立运行池后只消费它，不再回读资产，资产后续被修改也不影响已运行的池。
    /// 与 <see cref="GameObjectPoolConfig"/> 字段一一对应，构造函数一次赋值，
    /// 两者之间只做按值复制，不引入额外语义转换。
    /// </remarks>
    /// </summary>
    public readonly struct RuntimePoolConfig
    {
        /// <summary>
        /// 池名，来自配置项 <see cref="GameObjectPoolConfig.PoolName"/>，已通过非空与重名校验。
        /// </summary>
        public readonly string PoolName;

        /// <summary>
        /// YooAsset 定位地址，来自配置项 <see cref="GameObjectPoolConfig.AssetName"/>。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 启动预热总量目标。
        /// </summary>
        public readonly int PrewarmCount;

        /// <summary>
        /// 本池分帧预热时每帧最多发起的创建请求数。
        /// </summary>
        public readonly int PrewarmPerFrame;

        /// <summary>
        /// 底层软保留目标。
        /// </summary>
        public readonly int Capacity;

        /// <summary>
        /// 实例总量硬上限，0 表示不限。
        /// </summary>
        public readonly int MaxInstanceCount;

        /// <summary>
        /// 底层自动清理检查间隔（秒）。
        /// </summary>
        public readonly float AutoReleaseInterval;

        /// <summary>
        /// 实例级空闲过期时间（秒）。
        /// </summary>
        public readonly float ExpireTime;

        /// <summary>
        /// 整池过期时间（秒），0 表示关闭整池过期。
        /// </summary>
        public readonly float PoolExpireTime;

        /// <summary>
        /// 本池整池自动释放的检查间隔（秒）。
        /// </summary>
        public readonly float PoolAutoReleaseInterval;

        /// <summary>
        /// 底层池清理优先级。
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 由配置项按值复制出运行期只读配置。
        /// </summary>
        /// <param name="config">已经通过 <c>GameObjectPoolValidator.ValidateRuntime</c> 的配置项。</param>
        public RuntimePoolConfig(GameObjectPoolConfig config)
        {
            PoolName = config.PoolName;
            AssetName = config.AssetName;
            PrewarmCount = config.PrewarmCount;
            PrewarmPerFrame = config.PrewarmPerFrame;
            Capacity = config.Capacity;
            MaxInstanceCount = config.MaxInstanceCount;
            AutoReleaseInterval = config.AutoReleaseInterval;
            ExpireTime = config.ExpireTime;
            PoolExpireTime = config.PoolExpireTime;
            PoolAutoReleaseInterval = config.PoolAutoReleaseInterval;
            Priority = config.Priority;
        }
    }

    /// <summary>
    /// 配置构建结果。按值返回，不使用 out/ref 参数。
    /// </summary>
    /// <remarks>
    /// 产出的是<b>数组而不是字典</b>：字典是"按池名查"的运行期索引，属于模块；
    /// 构建阶段只负责把资产条目翻译成只读形态，不需要预先决定调用方怎么存。
    /// 另外数组保留了资产里的条目顺序，字典不保序，池列表会随哈希码乱跳。
    /// </remarks>
    public readonly struct GameObjectPoolConfigBuildResult
    {
        /// <summary>
        /// 是否全部条目通过校验且无重名。
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// 成功时为完整数组，顺序与资产里的一致；失败时为 null，不会返回半成品。
        /// </summary>
        public readonly RuntimePoolConfig[] Configs;

        /// <summary>
        /// 失败原因；成功时为空字符串。
        /// </summary>
        public readonly string Failure;

        /// <summary>
        /// 构造构建结果。
        /// </summary>
        /// <param name="success">是否成功。</param>
        /// <param name="configs">成功时的完整数组。</param>
        /// <param name="failure">失败原因。</param>
        public GameObjectPoolConfigBuildResult(
            bool success, RuntimePoolConfig[] configs, string failure)
        {
            Success = success;
            Configs = configs;
            Failure = failure;
        }
    }

}

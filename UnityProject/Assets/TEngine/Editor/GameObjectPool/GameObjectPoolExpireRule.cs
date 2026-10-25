namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 过期字段在面板上的填写方式，只是表单语义，<b>不序列化</b>。
    /// <remarks>
    /// 资产里只有 <c>float</c> 一个字段，这个枚举负责把"永不过期"从一个勾选框
    /// 变成可读的选择项：作者看到的是"按时间回收 / 永不过期"，
    /// 写入资产时仍然是 <see cref="GameObjectPoolExpireRule.NeverExpireInstance"/>
    /// 或 <see cref="GameObjectPoolExpireRule.NeverExpirePool"/> 这两个哨兵值。
    /// 序号即下拉框下标，不要重排，也不要插入中间项。
    /// </remarks>
    /// </summary>
    public enum GameObjectPoolExpireMode
    {
        /// <summary>
        /// 按时间回收：字段里存的是秒数。
        /// </summary>
        Timeout = 0,

        /// <summary>
        /// 永不过期：字段里存的是该字段对应的关闭值。
        /// </summary>
        Never = 1,
    }

    /// <summary>
    /// 两个过期字段"关闭"语义用的哨兵值与判定，<b>只在编辑器下参与编译</b>。
    /// <remarks>
    /// 「哪些值代表关闭」是作者在编辑期表达意图的方式，不是运行期需要知道的事：
    /// 运行期只看字段里的数字本身，哨兵值到了底层就是一个普通的大数或 0。
    /// 因此它属于 <c>TEngine.Editor</c> 程序集——那个 asmdef 的 <c>includePlatforms</c> 只有
    /// <c>Editor</c>，发布版整体不编译它，连类型都不会生成，也就不必再套 <c>#if UNITY_EDITOR</c>
    /// （多一层宏只会在编辑器内调试时多一个"这段到底编没编进去"的疑问）。
    /// <para>
    /// <b>校验器不调用这里的判定</b>：<see cref="GameObjectPoolValidator"/> 在 Runtime 程序集，
    /// 不能反向引用编辑器程序集。它需要的判断只是"整池过期是否已开启"，
    /// 而走到那一步时负数与 NaN / Infinity 已被前一条规则排除，写 <c>PoolExpireTime &gt; 0f</c>
    /// 与这里的 <c>!= 0f</c> 在合法值域上等价，不存在"各判一套、可能判得不一样"的问题。
    /// 这个类型服务的是编辑器侧的两处：Inspector 的永不过期开关、编辑器诊断的时序提示。
    /// </para>
    /// </remarks>
    /// </summary>
    public static class GameObjectPoolExpireRule
    {
        /// <summary>
        /// "实例永不过期"的哨兵值，等于 <c>float.MaxValue</c>。
        /// <remarks>
        /// 和 <see cref="NeverExpirePool"/> 是<b>两个不同的值</b>，因为两个过期字段的 0 含义相反：
        /// 实例过期的 0 表示"空闲即刻过期"（等于不缓存），池级过期的 0 才是"关闭"。
        /// 底层 <c>ObjectPool.Release</c> 的判断是
        /// <c>if (_expireTime &lt; float.MaxValue) expireTime = UtcNow.AddSeconds(-_expireTime);</c>，
        /// 到达该值时过期基准取 <see cref="System.DateTime.MinValue"/>，
        /// <c>LastUseTime &lt;= MinValue</c> 恒不成立，于是没有实例会被判为过期——
        /// 这是底层本来就支持的能力，不需要改底层，也不需要新增序列化字段。
        /// </remarks>
        /// </summary>
        public const float NeverExpireInstance = float.MaxValue;

        /// <summary>
        /// "整池永不过期"的哨兵值，等于 0。
        /// <remarks>
        /// 池级过期关闭时整池计时器不存在，同条目的整池自动释放间隔也不再参与判断。
        /// 选 0 而不是 <c>float.MaxValue</c>：0 是池级过期本来就有的"关闭"语义，
        /// 再引入一个哨兵只会让同一个字段出现两种"不算"的写法。
        /// </remarks>
        /// </summary>
        public const float NeverExpirePool = 0f;

        /// <summary>
        /// 该实例过期时间是否表示"永不过期"。
        /// <remarks>
        /// 判定只写在这里：Inspector 的开关状态与校验器的提示都调它，
        /// 避免同一个哨兵值在编辑器代码与规则代码里各判一次、还可能判得不一样。
        /// 用 <c>&gt;=</c> 而不是 <c>==</c>：NaN 与任何值比较都为 false，非法值不会被误判成"永不过期"；
        /// Infinity 另外排除——那是填错，该由校验器报"必须是有限值"。
        /// </remarks>
        /// </summary>
        /// <param name="expireTime">实例过期时间（秒）。</param>
        /// <returns>该值表示"实例永不过期"时为 true。</returns>
        public static bool IsInstanceExpireDisabled(float expireTime)
        {
            return !float.IsInfinity(expireTime) && expireTime >= NeverExpireInstance;
        }

        /// <summary>
        /// 该整池过期时间是否表示"永不过期"（关闭整池过期）。
        /// <remarks>
        /// 精确等于 0：负数不是"关闭"而是填错，应当留在字段里由校验器报出来，
        /// 不能被这里当成合法状态悄悄吞掉。
        /// </remarks>
        /// </summary>
        /// <param name="poolExpireTime">整池过期时间（秒）。</param>
        /// <returns>该值表示"关闭整池过期"时为 true。</returns>
        public static bool IsPoolExpireDisabled(float poolExpireTime)
        {
            return poolExpireTime == NeverExpirePool;
        }
    }
}

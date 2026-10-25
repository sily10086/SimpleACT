namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 新增条目时填入的参数默认值，<b>只存在于编辑器程序集</b>。
    /// <remarks>
    /// 这些数字回答的是"作者点『添加条目』之后先看到什么"，
    /// 运行期不参与任何判断，因此不属于 <see cref="GameObjectPoolConfig"/>——
    /// 那个类型是资产的数据契约，只声明字段。
    /// 与 <see cref="GameObjectPoolExpireRule"/> 的分工：那个管"哪些值代表关闭"，
    /// 这个管"新条目从多少开始"。
    /// <para>
    /// 全项目只有这一处定义，<c>GameObjectPoolEntryDrawer.ApplyDefaults</c> 是唯一写入点。
    /// 编辑器代码里不允许再出现默认值字面量：同一组值写两处，改一处忘另一处必然漂移。
    /// </para>
    /// </remarks>
    /// </summary>
    public static class GameObjectPoolEntryDefaults
    {
        /// <summary>
        /// 新增条目时的"预热数量"：0，即不预热。
        /// </summary>
        public const int PrewarmCount = 0;

        /// <summary>
        /// 新增条目时的"每帧预热数量"。
        /// <remarks>
        /// 不能是 0：分帧预热每帧预算为 0 会永远推不动，校验器直接判错。
        /// </remarks>
        /// </summary>
        public const int PrewarmPerFrame = 4;

        /// <summary>
        /// 新增条目时的"容量目标"。底层软保留值，不是实例硬上限。
        /// </summary>
        public const int Capacity = 32;

        /// <summary>
        /// 新增条目时的"实例上限"：0，即不限。
        /// </summary>
        public const int MaxInstanceCount = 0;

        /// <summary>
        /// 新增条目时的"自动释放间隔"（秒）。
        /// </summary>
        public const float AutoReleaseInterval = 60f;

        /// <summary>
        /// 新增条目时的"实例过期时间"（秒）。
        /// <remarks>
        /// 这里给 0 不合法（0 等于"空闲即刻过期"，即不缓存），
        /// 所以默认给一个能真正起到缓存作用的有限正数。
        /// </remarks>
        /// </summary>
        public const float InstanceExpireTime = 60f;

        /// <summary>
        /// 新增条目时的"整池过期时间"：关闭。
        /// <remarks>
        /// 默认关闭是刻意的：新增的池不会因为填了默认值就突然开始整池回收，
        /// 需要回收内存的池逐条显式打开。
        /// </remarks>
        /// </summary>
        public const float PoolExpireTime = GameObjectPoolExpireRule.NeverExpirePool;

        /// <summary>
        /// 把"整池永不过期"开关拨回计时状态时填入的整池过期时间（秒）。
        /// <remarks>
        /// 与 <see cref="PoolExpireTime"/> 是两件事：那个是"新增条目时的默认状态"（关闭），
        /// 这个是"作者刚决定要启用整池回收时先给个能用的数"，
        /// 否则开关一拨字段还是 0，等于没启用。
        /// </remarks>
        /// </summary>
        public const float PoolExpireTimeOnEnable = 60f;

        /// <summary>
        /// 新增条目时的"整池自动释放间隔"（秒）。
        /// <remarks>
        /// 只在整池过期开启后才参与判断；默认条目整池过期是关闭的，这个值此时不被使用，
        /// 但先填好可以避免作者打开整池过期后再遇到一个 0 触发的校验红字。
        /// </remarks>
        /// </summary>
        public const float PoolAutoReleaseInterval = 5f;

        /// <summary>
        /// 新增条目时的"回收优先级"。
        /// </summary>
        public const int Priority = 0;
    }
}

#if UNITY_EDITOR
namespace TEngine
{
    /// <summary>
    /// 配置校验失败的文案。
    /// <remarks>
    /// 用字符串常量而不是枚举：这些文案只在编辑器诊断与初始化日志里出现，
    /// 不需要再做一层"错误码 → 文案"的翻译，也就不需要 switch。
    /// 整个类型与 <see cref="GameObjectPoolValidator"/> 一起位于 <c>#if UNITY_EDITOR</c> 内，
    /// 发布版连元数据都不会生成。
    /// </remarks>
    /// </summary>
    public static class GameObjectPoolConfigError
    {
        /// <summary>
        /// 池名为空或 null。
        /// </summary>
        public const string PoolNameEmpty = "池名为空";

        /// <summary>
        /// YooAsset 定位地址为空或 null。
        /// </summary>
        public const string AssetNameEmpty = "YooAsset 地址为空";

        /// <summary>
        /// 预热数量、Capacity 或实例上限为负数。
        /// </summary>
        public const string NegativeCount = "数量字段不能为负数";

        /// <summary>
        /// 每帧预热数量小于等于 0，分帧预热永远推不动。
        /// </summary>
        public const string InvalidPrewarmPerFrame = "每帧预热数量必须大于 0，否则分帧预热无法推进";

        /// <summary>
        /// 自动清理间隔不是有限正数。
        /// </summary>
        public const string InvalidAutoReleaseInterval = "自动清理间隔必须是有限正数";

        /// <summary>
        /// 整池自动释放检查间隔不是有限正数（仅在该池开启了整池过期时校验）。
        /// </summary>
        public const string InvalidPoolAutoReleaseInterval = "整池自动释放间隔必须是有限正数";

        /// <summary>
        /// 实例级空闲过期不是有限正数（0 会让空闲实例在下一次清理时全部释放，等于不缓存）。
        /// </summary>
        public const string InvalidInstanceExpireTime =
            "实例空闲过期必须大于 0 且为有限值（0 会让空闲实例一被清理就释放，等于不缓存）";

        /// <summary>
        /// 整池过期时间不是有限非负数（0 是合法的"关闭"值）。
        /// </summary>
        public const string InvalidPoolExpireTime = "整池过期时间必须是有限非负数（0 表示关闭）";

        /// <summary>
        /// 预热数量超过 Capacity，预热无法收敛。
        /// </summary>
        public const string PrewarmExceedsCapacity = "预热数量超过 Capacity，当前后端无法保有该目标";

        /// <summary>
        /// 预热数量超过非零的实例硬上限。
        /// </summary>
        public const string PrewarmExceedsMaxInstance = "预热数量超过 MaxInstanceCount";
    }

    /// <summary>
    /// 游戏对象池配置校验，<b>只在编辑器下参与编译</b>。
    /// <remarks>
    /// 整段位于 <c>#if UNITY_EDITOR</c> 内：配置是作者在编辑期填的，
    /// 发布版运行的是同一份已经过校验的数据，再逐条重算一遍没有收益，
    /// 因此发布包里连这段代码都不存在（而不是"编译进来但不调用"）。
    /// 单项规则的唯一定义处就在这里：编辑器诊断（<c>GameObjectPoolEditorValidator</c>）
    /// 必须先调用 <see cref="ValidateRuntime"/>，再补格式与跨条目检查，避免两侧规则漂移。
    /// 返回文案而不是错误码，是因为调用方只有编辑器建字典与诊断面板两处，
    /// 直接拿字符串更省一层翻译；字符串是常量，不会为每次校验分配。
    /// </remarks>
    /// </summary>
    public static class GameObjectPoolValidator
    {
        /// <summary>
        /// 校验单个配置项，返回首个命中的失败文案。
        /// </summary>
        /// <param name="config">待校验的配置项，按值传入。</param>
        /// <returns>失败时为首个命中的文案常量；全部通过时为 null。</returns>
        public static string ValidateRuntime(GameObjectPoolConfig config)
        {
            // 空池名会让字典索引失效，且业务永远取不到这个池
            if (string.IsNullOrEmpty(config.PoolName))
            {
                return GameObjectPoolConfigError.PoolNameEmpty;
            }

            // 没有地址就没有实例来源，属于配置失效而不是"等资源下载好再说"
            if (string.IsNullOrEmpty(config.AssetName))
            {
                return GameObjectPoolConfigError.AssetNameEmpty;
            }

            // 负数会进入底层 Register 的参数，行为不可预期
            if (config.PrewarmCount < 0 || config.Capacity < 0 || config.MaxInstanceCount < 0)
            {
                return GameObjectPoolConfigError.NegativeCount;
            }

            // 每帧预算为 0 会让分帧预热永远停在原地：既不报错也不推进，只能在加载界面干等
            if (config.PrewarmPerFrame <= 0)
            {
                return GameObjectPoolConfigError.InvalidPrewarmPerFrame;
            }

            if (!IsPositiveFinite(config.AutoReleaseInterval))
            {
                return GameObjectPoolConfigError.InvalidAutoReleaseInterval;
            }

            // 实例级过期必须是正数：0 会让底层把过期基准当成"现在"，下次清理时空闲实例全被释放。
            // 永不过期用的是 float.MaxValue，它是有限正数，天然通过这一条，不需要为它开分支。
            if (!IsPositiveFinite(config.ExpireTime))
            {
                return GameObjectPoolConfigError.InvalidInstanceExpireTime;
            }

            // 池级过期与实例级过期规则相反：0 是"关闭"，不能套用 > 0 的判断。
            // 这条必须排在 PoolAutoReleaseInterval 之前：下面那条要看它是否开启才决定校不校验。
            if (!IsNonNegativeFinite(config.PoolExpireTime))
            {
                return GameObjectPoolConfigError.InvalidPoolExpireTime;
            }

            // 检查间隔参与累加，0 或非有限值会让整池过期永远等不到检查点。
            // 但整池过期关闭时这个值不参与任何判断，"永不过期"的池不该被迫填一个永远用不到的
            // 正数——那样作者只会随手填个 60 来消掉红字，规则本身却什么也没保证。
            // 这里用 > 0 判"是否开启"而不是调 GameObjectPoolExpireRule：那个类型在
            // TEngine.Editor，本程序集不能引用它。两者在合法值域上等价（上一条已排除
            // 负数与 NaN / Infinity，"关闭"只可能是 0），所以没有各判一套的风险。
            if (config.PoolExpireTime > 0f && !IsPositiveFinite(config.PoolAutoReleaseInterval))
            {
                return GameObjectPoolConfigError.InvalidPoolAutoReleaseInterval;
            }

            // 预热超过 Capacity 会形成"创建 → 被容量淘汰 → 再创建"的无法收敛循环，表现为卡死
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

        /// <summary>
        /// 是否为有限正数（排除 NaN 与 ±Infinity）。
        /// </summary>
        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        /// <summary>
        /// 是否为有限非负数（排除 NaN 与 ±Infinity，允许 0）。
        /// </summary>
        private static bool IsNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
#endif

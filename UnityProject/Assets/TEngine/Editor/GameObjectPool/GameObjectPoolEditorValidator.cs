using System;
using System.Collections.Generic;
using System.Globalization;

namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 诊断级别。
    /// </summary>
    public enum GameObjectPoolValidationSeverity
    {
        /// <summary>
        /// 必须修正，否则运行期会失败。
        /// </summary>
        Error = 0,

        /// <summary>
        /// 可以保存，但有需要知情的行为。
        /// </summary>
        Warning = 1,
    }

    /// <summary>
    /// 一条诊断。按值传递；Inspector 在刷新时缓存列表，不在 OnGUI 中重建。
    /// </summary>
    public readonly struct GameObjectPoolValidationMessage
    {
        /// <summary>
        /// 级别。
        /// </summary>
        public readonly GameObjectPoolValidationSeverity Severity;

        /// <summary>
        /// 命中的配置条目下标；-1 表示全局问题（不属于某一条目）。
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// 相关字段的序列化名，用于定位；全局问题可为空字符串。
        /// </summary>
        public readonly string Field;

        /// <summary>
        /// 可读文案。
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 构造一条诊断。
        /// </summary>
        /// <param name="severity">级别。</param>
        /// <param name="index">条目下标，-1 表示全局。</param>
        /// <param name="field">字段名。</param>
        /// <param name="message">文案。</param>
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
    /// 配置资产的完整诊断。只存在于 <c>TEngine.Editor</c> 程序集，发布版连类型都不存在。
    /// <remarks>
    /// 先复用单项规则 <see cref="GameObjectPoolValidator.ValidateRuntime"/>，
    /// 再补格式与跨条目检查——同一个字段不能被两边各判一次，也不能两边判得不一样。
    /// 校验器本身也在 <c>#if UNITY_EDITOR</c> 内，与这里同属编辑器代码，
    /// 因此不存在"编辑器靠运行期规则兜底"的关系：发布版两边都不编译。
    /// 资源是否存在/已下载不在这里判断，那属于建池时的运行期检查。
    /// </remarks>
    /// </summary>
    public static class GameObjectPoolEditorValidator
    {
        /// <summary>
        /// "过长过期时间"的告警阈值（秒）：一天。
        /// <remarks>
        /// 超过一天的空闲过期在工程尺度上等价于"基本不会回收"，但它又不是底层的永不过期哨兵，
        /// 作者容易以为"设了个很大的数"。这里给一条提示而不是错误：数值合法，语义可疑。
        /// </remarks>
        /// </summary>
        private const float LongExpireThreshold = 86400f;

        /// <summary>
        /// 全量收集配置问题。
        /// </summary>
        /// <param name="setting">配置资产，可为 null。</param>
        /// <param name="errors">错误列表，调用时清空。</param>
        /// <param name="warnings">提示列表，调用时清空。</param>
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
            // 这里只在它之外补格式与跨条目检查，避免同一问题报两遍。
            GameObjectPoolConfig[] pools = setting.Pools;
            if (pools == null)
            {
                return; // _pools 为 null 按空数组合法处理
            }

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
                CheckExpireTiming(config, i, warnings);
            }
        }

        /// <summary>
        /// 过期判定的时序提示。
        /// <remarks>
        /// 这几条都不是配置错误，数值本身合法，只是行为与直觉不同：
        /// 过期时间设得过大等于不回收、过期比扫描间隔还短会延后释放、
        /// 预热与整池过期同时开启会让预热成果可能被立刻回收。
        /// 它们只会表现成"内存回收不如预期"，不报出来作者很难自己发现。
        /// </remarks>
        /// </summary>
        private static void CheckExpireTiming(
            GameObjectPoolConfig config, int index,
            List<GameObjectPoolValidationMessage> warnings)
        {
            // 只在"这个值真的会参与计时"时才谈时序：永不过期、0、负数、Infinity 都先排除，
            // 它们的非法性或特殊语义已经由 ValidateRuntime 与哨兵值判定处理过，
            // 这里再叠一条时序提示只会让同一条配置报两遍
            if (!GameObjectPoolExpireRule.IsInstanceExpireDisabled(config.ExpireTime)
                && IsUsableSeconds(config.ExpireTime))
            {
                if (config.ExpireTime >= LongExpireThreshold)
                {
                    warnings.Add(Warning(index, "ExpireTime",
                        "实例过期时间不小于 1 天，实际等价于不回收；如果本来就不打算回收实例，"
                        + "请直接勾选「永不过期」"));
                }
                else if (config.ExpireTime < config.AutoReleaseInterval)
                {
                    warnings.Add(Warning(index, "ExpireTime",
                        "实例过期时间小于自动释放间隔，空闲实例最早要等到下一次自动清理（"
                        + config.AutoReleaseInterval.ToString("0.##", CultureInfo.InvariantCulture)
                        + " 秒）才会被释放，实际回收比设定值更晚"));
                }
            }

            // 整池过期关闭时，检查间隔与预热都不会与它产生时序冲突
            if (!IsUsableSeconds(config.PoolExpireTime))
            {
                return;
            }

            if (config.PoolExpireTime >= LongExpireThreshold)
            {
                warnings.Add(Warning(index, "PoolExpireTime",
                    "整池过期时间不小于 1 天，实际等价于不过期；如果不需要整池回收，请勾选「永不过期」"));
            }
            else if (config.PoolExpireTime <= config.PoolAutoReleaseInterval)
            {
                warnings.Add(Warning(index, "PoolAutoReleaseInterval",
                    "整池自动释放间隔不小于整池过期时间，释放只能发生在检查点上，"
                    + "实际空闲时长最长接近两倍设定值"));
            }

            if (config.PrewarmCount > 0)
            {
                warnings.Add(Warning(index, "PrewarmCount",
                    "该池既做启动预热又开启整池过期：预热结束即开始倒计时，"
                    + "若此时停留在加载界面超过整池过期时间，预热成果会被白做一遍"));
            }
        }

        /// <summary>
        /// 该秒数是否是"会真正参与计时"的合法值。
        /// <remarks>
        /// 0 与负数在实例级是非法、在池级是关闭，Infinity 与 NaN 是填错，都不是时序问题，
        /// 各自的判定已经落在校验器与哨兵值判定里，这里只关心"能不能比较大小"。
        /// </remarks>
        /// </summary>
        private static bool IsUsableSeconds(float seconds)
        {
            return seconds > 0f && !float.IsInfinity(seconds) && !float.IsNaN(seconds);
        }

        /// <summary>
        /// 池名检查：首尾空白与重名。空名已由运行期规则报过，这里不重复。
        /// <remarks>
        /// 重名只在这里判：构建阶段不扫、运行期也不拦，所以这条诊断是唯一的发现点。
        /// 记录首条下标而不是只存名字，是为了让提示能指明"跟哪一条撞了"——
        /// 否则同名三条时作者只能逐条比对。比较器固定 <c>Ordinal</c>，
        /// 与模块建字典时的口径一致（Coin 与 coin 是两个池）。
        /// </remarks>
        /// </summary>
        private static void CheckPoolName(
            string poolName, int index, Dictionary<string, int> names,
            List<GameObjectPoolValidationMessage> errors)
        {
            if (string.IsNullOrEmpty(poolName))
            {
                return;
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

        /// <summary>
        /// 地址格式检查，并对同址多池给出共享引用提示。
        /// </summary>
        private static void CheckAssetName(
            string assetName, int index, Dictionary<string, int> assetOwners,
            List<GameObjectPoolValidationMessage> errors,
            List<GameObjectPoolValidationMessage> warnings)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return;
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
                warnings.Add(Warning(index, "AssetName",
                    "与第 " + (firstIndex + 1) + " 条共用同一地址；释放按引用计数，单个池释放不保证资源立即卸载"));
            }
            else
            {
                assetOwners.Add(assetName, index);
            }
        }

        /// <summary>
        /// 地址是否误带了资源扩展名。
        /// </summary>
        private static bool HasAssetExtension(string assetName)
        {
            return assetName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                   || assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                   || assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   || assetName.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)
                   || assetName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 构造一条错误诊断。
        /// </summary>
        private static GameObjectPoolValidationMessage Error(int index, string field, string message)
        {
            return new GameObjectPoolValidationMessage(
                GameObjectPoolValidationSeverity.Error, index, field, message);
        }

        /// <summary>
        /// 构造一条提示诊断。
        /// </summary>
        private static GameObjectPoolValidationMessage Warning(int index, string field, string message)
        {
            return new GameObjectPoolValidationMessage(
                GameObjectPoolValidationSeverity.Warning, index, field, message);
        }
    }
}

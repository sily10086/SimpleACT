using System;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 游戏对象池配置资产。
    /// <remarks>
    /// 只表达作者意图：保存池名与 YooAsset 定位地址，不持有预制体引用、不加载资源、
    /// 不创建对象池或场景根节点，也不在 OnEnable 中启动任何池。
    /// 内容只有一块：逐池条目 <c>_pools</c>。每个条目自带全部参数，
    /// 包括这个池自己的预热节奏（PrewarmPerFrame）与整池自动释放检查间隔
    /// （PoolAutoReleaseInterval），不存在"全模块共用"的配置项。
    /// 唯一的行为是 <see cref="TryBuildConfigs"/>：把条目复制成运行期只读配置并返回一份新数组，
    /// 结果不缓存为字段，资产本身仍是纯数据。池名字典由模块自己建——索引结构属于运行期。
    /// 逐条字段校验只在编辑器下执行（见方法内部的 <c>#if UNITY_EDITOR</c>）。
    /// 私有字段一律 _ 前缀小驼峰 + 只读属性，业务代码不能反写配置；运行期状态属于模块与运行池对象。
    /// </remarks>
    /// </summary>
    [CreateAssetMenu(menuName = "TEngine/GameObjectPoolSetting", fileName = "GameObjectPoolSetting")]
    public sealed class GameObjectPoolSetting : ScriptableObject
    {
        /// <summary>
        /// 全部对象池条目。null 与空数组都视为合法空配置。
        /// </summary>
        [SerializeField]
        private GameObjectPoolConfig[] _pools = null;

        /// <summary>
        /// 条目数组的只读入口，供编辑器诊断与建池前读取。
        /// <remarks>
        /// 只给读取用：这里是字段原样的数组引用，写元素仍会改到资产，
        /// 所以业务代码不该拿它反写配置——配置只有作者在 Inspector 里改。
        /// 返回 null 与空数组都表示"没有任何池"，调用方两者都要当成合法空配置。
        /// </remarks>
        /// </summary>
        public GameObjectPoolConfig[] Pools => _pools;

        /// <summary>
        /// 把条目复制为运行期只读配置，返回一份新数组。
        /// <remarks>
        /// <para>
        /// 条目复制为 <see cref="RuntimePoolConfig"/>（只读），产出只装只读形态：
        /// 模块建立运行池后不再回读资产，资产之后被修改也不影响已运行的池。
        /// 每次调用都返回新数组，不缓存为字段；只在初始化（或显式重建配置）时调用一次。
        /// 资产自身的 null 由调用方判断：那是"缺少配置"，不是配置内容错误。
        /// </para>
        /// <para>
        /// 逐条字段校验只在编辑器下做：发布版运行的是同一份已经校验过的数据，
        /// 再算一遍没有收益，因此那段判断连编译都不进。
        /// </para>
        /// <para>
        /// <b>构建阶段不判重</b>：池名重复只在编辑器侧报错并标红（<c>GameObjectPoolEditorValidator.Collect</c>），
        /// 那是唯一落点。这里只挡空池名——它防的是把 null 当字典键会抛 <c>ArgumentNullException</c>，
        /// 属于"别让公开路径抛异常"，不是"替编辑器再做一次校验"。
        /// 因此返回的数组与条目一一对应、长度相同：发布版真出现重复池名（资产被手改或合并冲突）、
        /// 或同一帧里登记过同名池时，模块建索引会由后者覆盖前者，结果仍是"每个池名一条配置"，
        /// 只是条数少于条目数。要发现这件事，看编辑器诊断，不要在构建里加一层检查。
        /// </para>
        /// </remarks>
        /// </summary>
        /// <returns>成功时携带与条目一一对应的数组，失败时携带首个失败原因。</returns>
        public GameObjectPoolConfigBuildResult TryBuildConfigs()
        {
            GameObjectPoolConfig[] pools = _pools;

            // null 与空数组都是合法的空配置：模块可以在没有任何池的情况下初始化
            if (pools == null)
            {
                return new GameObjectPoolConfigBuildResult(
                    true, Array.Empty<RuntimePoolConfig>(), string.Empty);
            }

            var configs = new RuntimePoolConfig[pools.Length];

            for (int i = 0; i < pools.Length; i++)
            {
                GameObjectPoolConfig config = pools[i];

#if UNITY_EDITOR
                // 逐条字段校验只在编辑器下存在：发布版跑的是同一份已校验过的数据。
                // 它只判单条，跨条目的重名不在这里——那条规则属于编辑器诊断
                string error = GameObjectPoolValidator.ValidateRuntime(config);
                if (error != null)
                {
                    string poolName = string.IsNullOrEmpty(config.PoolName) ? "<空>" : config.PoolName;
                    return new GameObjectPoolConfigBuildResult(false, null,
                        "第 " + (i + 1) + " 条配置（" + poolName + "）：" + error);
                }
#endif

                // 发布版没有上面那段校验，空池名会一路进到字典索引并抛 ArgumentNullException——
                // 那是异常，不是"明确失败"。这里只挡 null 与空串，不复制任何字段规则，
                // 与 RegisterPool 里那条挡板同性质。
                if (string.IsNullOrEmpty(config.PoolName))
                {
                    return new GameObjectPoolConfigBuildResult(false, null,
                        "第 " + (i + 1) + " 条配置：池名为空");
                }

                configs[i] = new RuntimePoolConfig(config);
            }

            return new GameObjectPoolConfigBuildResult(true, configs, string.Empty);
        }
    }
}

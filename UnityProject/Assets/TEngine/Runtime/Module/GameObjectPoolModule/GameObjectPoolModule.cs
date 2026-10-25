using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TEngine
{
    /// <summary>
    /// 游戏对象池模块：持有配置、根节点与池的运行期状态。
    /// <remarks>
    /// <para>
    /// 这一层负责"谁能用、什么时候能用"，以及"池从哪来、实例归谁"：
    /// 把配置资产变成运行期只读字典、建立承载池的根节点、按池名同步建池、
    /// 维护一份全量实例索引。借出、归还、预热、过期回收在后续步骤填充。
    /// </para>
    /// <para>
    /// <b>没有生命周期状态机</b>：初始化在 <see cref="OnInit"/> 里一次做完，
    /// 不做成"先创建对象、再等外部调 Initialize"的两段式，也不维护
    /// 未初始化 / 就绪 / 正在关闭这组对外状态。理由是这组状态除了让调用方
    /// 多一次"先问状态、再操作"的往返之外没有增加任何保证——
    /// 问完到真正操作之间状态照样可能变，各入口本来就得自己判一次。
    /// 现在统一的判据是"这个池有没有配置"，
    /// 它是内部细节，对外只体现为"各操作返回失败并打日志"。
    /// </para>
    /// <para>
    /// <b>不在模块初始化时预热</b>：预热属于"池被创建"这件事（第 09 步），
    /// 不是模块启动的一部分。模块初始化只读配置、建根，一个实例都不创建，
    /// 因为不是每个配置里的池都会在这一局用到，启动时全建一遍会把用不上的资源也拉进内存。
    /// 池被创建时若配了 <c>PrewarmCount</c>，由池自己在构造函数里登记，
    /// 本模块的 <see cref="Update"/> 每帧按该池的 <c>PrewarmPerFrame</c> 推进。
    /// </para>
    /// <para>
    /// <b>不复用底层 <c>ObjectPoolModule</c> 的公开接口做业务入口</b>：
    /// 底层按类型与名字管理对象池，业务侧要的是按池名取 GameObject 这一层语义，
    /// 两者的键空间与失败语义都不同，硬套会让调用方被迫理解两套规则。
    /// </para>
    /// <para>
    /// 类型名固定为 <c>TEngine.GameObjectPoolModule</c>，因为 <c>ModuleSystem</c> 靠
    /// <c>{命名空间}.{接口名去掉开头的 I}</c> 反射实现类型；改名会让 <c>GetModule</c> 直接抛异常。
    /// 类本身 internal：业务只能通过 <see cref="IGameObjectPoolModule"/> 使用它。
    /// </para>
    /// </remarks>
    /// </summary>
    internal sealed partial class GameObjectPoolModule : Module, IGameObjectPoolModule, IUpdateModule
    {
        /// <summary>
        /// 模块根节点名，也是场景里唯一标识"这个模块在运行时创建的根"的依据。
        /// </summary>
        /// <remarks>
        /// 根下按池直接分组：每个池建池时创建一个与池同名的子节点，
        /// 该池的所有实例都挂在这一个节点下，空闲实例只是它自己的 <c>SetActive(false)</c>。
        /// 不再额外分 Active / Inactive 两层容器——池自己的节点已经把实例归拢清楚了，
        /// 多套一层只会让层级更深，并让"空闲实例在哪个父节点下"变成一种需要同步的状态。
        /// </remarks>
        private const string MODULE_ROOT_NAME = "GameObjectPool";

        /// <summary>
        /// 底层对象池模块，<see cref="OnInit"/> 时取一次。
        /// </summary>
        /// <remarks>
        /// 建池与关闭池是稳态操作，没必要每次 <c>ModuleSystem.GetModule</c> 再查一遍字典。
        /// 放在 <see cref="OnInit"/> 而不是构造函数：<c>ModuleSystem</c> 用
        /// <c>Activator.CreateInstance</c> 造模块，构造期执行任何框架查询都可能撞上"模块表还在初始化"。
        /// <c>OnInit</c> 时调用是安全的——本模块已经先一步写进了模块表，取另一个模块只是把它也建出来。
        /// </remarks>
        private IObjectPoolModule _objectPoolModule;

        /// <summary>
        /// 资源模块，建池时加载预制体、池销毁时归还引用。
        /// </summary>
        /// <remarks>
        /// 建池与 <c>OnInit</c> 一样取一次，理由同 <see cref="_objectPoolModule"/>。
        /// </remarks>
        private IResourceModule _resourceModule;

        /// <summary>
        /// 池名到运行期只读配置的映射。
        /// </summary>
        /// <remarks>
        /// <b>始终非 null</b>：初始化失败也只是空字典，关闭后也只是清空，所以各入口不需要判空。
        /// 代价是"空配置"与"配置读取失败"在数量上都表现为 0 条，
        /// 后者只靠初始化时那一条错误日志暴露——这是有意接受的：
        /// 与其为它维护一个要处处传递的状态位，不如让各入口在"查不到这个池"时
        /// 直接返回失败，两者走的是同一条代码路径。
        /// 比较器固定 <c>Ordinal</c>（大小写敏感），与编辑器诊断判重、<c>RegisterPool</c> 判重同一口径；
        /// 两处口径不一致会出现"构建说没重名、登记时却当成两个池"这类极难排查的问题。
        /// </remarks>
        private readonly Dictionary<string, RuntimePoolConfig> _configs =
            new Dictionary<string, RuntimePoolConfig>(StringComparer.Ordinal);

        /// <summary>
        /// 池名到已建运行池的映射。只有建池完全成功后才写入。
        /// </summary>
        private readonly Dictionary<string, GameObjectPool> _pools =
            new Dictionary<string, GameObjectPool>(StringComparer.Ordinal);

        /// <summary>
        /// 全量实例索引：<c>GetInstanceID</c> 到实例记录。
        /// </summary>
        /// <remarks>
        /// <b>空闲实例也要登记</b>：只登记借出对象会让可视化和外部销毁清账失真——
        /// 前者数不出一个池里到底有多少实例，后者拿到一个空闲对象的销毁通知时查不到归属。
        /// 用实例号做键只是索引手段，命中后仍要复核记录引用与实例身份，
        /// 因为记录会被内存池复用，实例号本身不保证唯一到一个逻辑实例。
        /// </remarks>
        private readonly Dictionary<int, GameObjectInstance> _instances =
            new Dictionary<int, GameObjectInstance>();

        /// <summary>
        /// 还有预热没推完的池，按建池顺序排列。
        /// </summary>
        /// <remarks>
        /// 用 List 而不是字典：这里只登记"建池时配置了 <c>PrewarmCount</c>"的池，
        /// 一个池最多一条，推完就当场移出，没有按名查找的需求。
        /// 没有任何池配置预热时它一直是空的，<see cref="Update"/> 里连循环都不进。
        /// </remarks>
        private readonly List<GameObjectPool> _prewarmPools = new List<GameObjectPool>();

        /// <summary>
        /// 模块常驻根，跨场景存活；它的生命周期与模块一致，只由 OnInit 创建、Shutdown 销毁。
        /// </summary>
        /// <remarks>
        /// 每池一个子节点挂在这个根下，由建池逻辑创建。
        /// 不为此单独开公开属性：它是模块内部结构，业务与 Monitor 都不该改它的父子关系。
        /// <b>不拿它当"模块还开着吗"的判据</b>：建池入口不看它——关闭之后
        /// <c>_configs</c> 已经清空，"没有这个池的配置"会先一步返回，
        /// 再判一次 <c>_root == null</c> 只是在同一条路径上重复回答同一个问题。
        /// 代价是关闭后再用 <c>RegisterPool</c> 登记新配置并建池时，没有挂载点会抛空引用；
        /// 那是"先关模块再用模块"的调用顺序错误，不该靠一道长期判断替它兜底。
        /// </remarks>
        private GameObject _root;

        /// <summary>
        /// 单调累计的真实时间（秒），池级计时基准。
        /// </summary>
        private double _monotonicSeconds;

        /// <summary>
        /// 获取游戏框架模块优先级。
        /// </summary>
        /// <remarks>
        /// 低于底层对象池模块（6）。<c>ModuleSystem</c> 按优先级降序维护模块链，
        /// 关闭时从链尾往前遍历，所以优先级更低的先被关闭：本模块先释放手里的对象，
        /// 底层对象池随后再清理，避免"底层已经关了、上层还在往回还对象"。
        /// 轮询方向相反（高优先级先执行），底层先刷新、本模块后读，同样的顺序在这里也对。
        /// </remarks>
        public override int Priority => 5;

        /// <summary>
        /// 已登记的池配置数量。
        /// </summary>
        public int ConfigCount => _configs.Count;

        /// <summary>
        /// 单调累计的真实时间（秒）。
        /// </summary>
        /// <remarks>
        /// 只用真实时间：它不受 <c>Time.timeScale</c> 影响，暂停游戏不会让池
        /// "看起来空闲了很久"；也不受系统校时影响，不会出现负间隔或跳过一整天。
        /// </remarks>
        internal double MonotonicSeconds => _monotonicSeconds;

        /// <summary>
        /// 读配置、建根节点，一次完成模块初始化。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 配置来源是 <c>Settings.GameObjectPoolSetting</c>：<c>Settings</c> 是场景里常驻的
        /// MonoBehaviour，用 <c>FindObjectOfType</c> 懒查找，所以这里取它不需要额外的启动顺序保证。
        /// 拿不到资产或资产内容有错时只打错误日志，配置字典保持为空；
        /// 之后各入口按"查不到这个池"返回明确失败。这两条失败路径都不抛异常，也不留下半成品状态。
        /// </para>
        /// <para>
        /// 依赖模块（底层对象池、资源）在方法最前面取一次并缓存。<c>GetModule</c> 是创建式的，
        /// 所以这里取它们同时保证了两件事：底层的优先级更高、会被先建出来；以及它们与本模块的
        /// 配置成败无关——配置缺失时这两个引用照样在位，<c>Shutdown</c> 这类不读配置的入口不会失效。
        /// </para>
        /// <para>
        /// 建根不捕获异常：它只有一句 <c>new GameObject</c>，而配置在它之后才发布，
        /// 所以失败时没有"配置已发布、根却不在"的中间态需要回滚，交给框架处理更直接。
        /// </para>
        /// <para>
        /// 不加载任何资源、不建任何池、不预热任何实例。
        /// </para>
        /// </remarks>
        public override void OnInit()
        {
            _monotonicSeconds = 0d;

            _objectPoolModule = ModuleSystem.GetModule<IObjectPoolModule>();
            _resourceModule = ModuleSystem.GetModule<IResourceModule>();

            // 缺资产与配置内容有错是两种失败，文案要能区分，否则排查会走错方向
            GameObjectPoolSetting setting = Settings.GameObjectPoolSetting;
            if (setting == null)
            {
                Log.Error("[GameObjectPool] 初始化失败：Settings 上没有配置资产（GameObjectPoolSetting 为 null）。");
                return;
            }

            GameObjectPoolConfigBuildResult buildResult = setting.TryBuildConfigs();
            if (!buildResult.Success)
            {
                Log.Error("[GameObjectPool] 初始化失败：" + buildResult.Failure);
                return;
            }

            // 先建根再发布配置：建根之后才把配置对外可见，两步顺序固定
            CreateRoot();

            // 按索引逐个写入。构建阶段只挡空池名、不判重（重名只在编辑器诊断里报），
            // 所以这里允许同名覆盖：数组顺序即生效顺序，靠后的条目胜出，
            // 结果始终是"每个池名一条配置"，条数可能少于条目数。
            // 失败分支已经在上面提前 return，走到这里不会有半成品字典
            RuntimePoolConfig[] configs = buildResult.Configs;
            for (int i = 0; i < configs.Length; i++)
            {
                RuntimePoolConfig config = configs[i];
                _configs[config.PoolName] = config;
            }

            Log.Info("[GameObjectPool] 初始化完成：配置 " + _configs.Count + " 条。");
        }

        /// <summary>
        /// 追加一条池配置，只登记不建池。
        /// </summary>
        /// <remarks>
        /// 与资产加载结果无关：资产缺失或内容有错时配置字典为空，
        /// 但这个入口照样能登记成功——它不读资产，只写字典。
        /// </remarks>
        /// <param name="config">配置项。</param>
        /// <returns>登记是否成功。</returns>
        public bool RegisterPool(GameObjectPoolConfig config)
        {
            // 空池名先挡一次。发布版没有 ValidateRuntime 兜底，直接进 TryAdd 会抛
            // ArgumentNullException——那是异常，不是"明确失败"。
            // 这不是第二套校验：它只防公开 API 抛异常，完整规则仍只在编辑器里跑。
            if (string.IsNullOrEmpty(config.PoolName))
            {
                Log.Warning("[GameObjectPool] 登记配置被拒绝：池名为空。");
                return false;
            }

#if UNITY_EDITOR
            string error = GameObjectPoolValidator.ValidateRuntime(config);
            if (error != null)
            {
                Log.Warning("[GameObjectPool] 登记配置被拒绝：" + error);
                return false;
            }
#endif

            // TryAdd 同时完成判重与写入：既不用额外的 HashSet，也不会出现"判过重、写的时候覆盖了"
            if (!_configs.TryAdd(config.PoolName, new RuntimePoolConfig(config)))
            {
                Log.Warning("[GameObjectPool] 登记配置被拒绝：池名重复 " + config.PoolName + "。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 按池名取运行池：已经有了就直接给，没有就当场建一个。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>查询与创建合成一个方法</b>，因为对调用方它们是同一件事——"给我这个池"。
        /// 拆成"先查再建"两个入口，只会让每个调用点自己拼
        /// <c>if (查询 == null) 创建()</c>，而"什么时候建"本来就该由模块决定，
        /// 拆开等于把内部顺序复制到每个调用方手里，改一次顺序要改所有调用点。
        /// </para>
        /// <para>
        /// 建池是资源的加载时机：配置里只有定位地址，这里才把它变成预制体引用交给池。
        /// 加载走同步的 <c>LoadAsset</c>，所以建池不跨帧——返回时要么池已可用，要么已明确失败，
        /// 不存在"正在建"的中间状态，也就不需要为它维护在途登记或状态位。
        /// </para>
        /// <para>
        /// 顺序固定：空名 → 已建池快路径 → 配置 → 加载 → 建后端与容器 → 发布。
        /// <b>全成功才发布</b>，不留下半建池。
        /// </para>
        /// <para>
        /// <b>建池前不判底层重名</b>：底层已有同名池只可能是"上一次没清理干净"或别处直接动了
        /// <c>ObjectPoolModule</c>，属于程序错误而不是运行期状态。
        /// <c>CreateSingleSpawnObjectPool</c> 自己就会抛
        /// <c>GameFrameworkException("Already exist object pool ...")</c>，文案里带类型与池名，
        /// 比这里手写一份更全；前面再加一道 <c>HasObjectPool</c>，只是把同一个问题回答两次、
        /// 多养一份自己的错误文案。代价与"不重复校验配置"一致：真撞上时是异常而不是 null，
        /// 且此前已加载的预制体引用不会归还。
        /// </para>
        /// <para>
        /// 即便如此，<c>Spawn</c>/<c>TrySpawn</c> 也不隐式建池：首次建池要读 AB 包，
        /// 挂在借出路径上就是一次不可控的卡顿。建池点由调用方显式决定（启动流程或预热），
        /// 未建池时借出返回明确失败。
        /// </para>
        /// <para>
        /// <b>不重复校验配置字段</b>：空地址、负数容量这类规则只在
        /// <c>GameObjectPoolValidator.ValidateRuntime</c> 里，由 <c>TryBuildConfigs</c> 与
        /// <c>RegisterPool</c> 两处编辑器分支各调一次；发布版跑的就是那份数据。
        /// 代价是一致的：运行期真遇到空地址，<c>LoadAsset</c> 会抛
        /// <c>GameFrameworkException("Asset name is invalid.")</c>，不在本层转成 null。
        /// </para>
        /// <para>
        /// 返回 <c>false</c> 表示明确失败（池名为空、没有这个池的配置、预制体加载不到），
        /// 此时 <paramref name="pool"/> 为 null；返回 <c>true</c> 时它一定可用。
        /// 签名是 C# 常规的 <c>TryXxx(..., out T)</c>：<c>Try</c> 承诺"不抛异常、用返回值告诉你成没成"，
        /// 结果从 <c>out</c> 走，调用方一眼能看出这次调用有副作用、可能失败。
        /// <b>不判断"模块是否已关闭"</b>：关闭时配置字典已经清空，请求会在"没有这个池的配置"处返回，
        /// 在同一条路径上再判一次模块状态只是重复回答同一个问题；
        /// 关闭后又用 <c>RegisterPool</c> 登记新配置并建池会因为没有挂载点抛空引用，
        /// 那是调用顺序错误，不由这里长期兜底。
        /// </para>
        /// </remarks>
        /// <param name="poolName">池名。</param>
        /// <param name="pool">取到或建好的运行池；失败时为 null。</param>
        /// <returns>取到或建成功为 true；池名为空、没有配置、预制体加载不到为 false。</returns>
        internal bool TryGetPoolOrCreate(string poolName, out GameObjectPool pool)
        {
            pool = null;

            if (string.IsNullOrEmpty(poolName))
            {
                Log.Warning("[GameObjectPool] 建池被拒绝：池名为空。");
                return false;
            }

            // 快路径：建过就直接给，不读配置、不碰资源。稳态借出走的都是这一条
            if (_pools.TryGetValue(poolName, out GameObjectPool running))
            {
                pool = running;
                return true;
            }

            if (!_configs.TryGetValue(poolName, out RuntimePoolConfig config))
            {
                Log.Warning("[GameObjectPool] 建池失败：没有池 " + poolName + " 的配置。");
                return false;
            }

            // 返回的是预制体资产本体，也是后面归还引用时的凭据；禁 Destroy、禁改组件与变换
            GameObject prefab = _resourceModule.LoadAsset<GameObject>(config.AssetName);
            if (prefab == null)
            {
                Log.Error("[GameObjectPool] 建池失败：池 " + poolName + " 的预制体加载不到，定位地址 " + config.AssetName + "。");
                return false;
            }

            IObjectPool<GameObjectInstance> backend = _objectPoolModule.CreateSingleSpawnObjectPool<GameObjectInstance>(
                poolName, config.AutoReleaseInterval, config.Capacity, config.ExpireTime, config.Priority);

            GameObject container = new GameObject(poolName);
            container.transform.SetParent(_root.transform, false);

            pool = new GameObjectPool(this, config, prefab, container, backend);

            // 构造函数里已经把"什么时候开始计时"和"要不要预热"一次处理完了，
            // 这里只剩入表这一个动作——入表即发布，之后这个池就能被查到
            _pools[poolName] = pool;

            Log.Info("[GameObjectPool] 建池完成：池 " + poolName + "，定位地址 " + config.AssetName + "。");
            return true;
        }

        /// <summary>
        /// 登记一个实例到全量索引。
        /// </summary>
        /// <param name="instance">实例记录。</param>
        internal void RegisterInstance(GameObjectInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            _instances[instance.InstanceId] = instance;
        }

        /// <summary>
        /// 从全量索引注销一个实例。
        /// </summary>
        /// <remarks>
        /// 复核记录引用：记录被内存池复用后，同一个实例号可能已经属于另一个克隆体，
        /// 按实例号无脑删会把别人的登记一起删掉。
        /// </remarks>
        /// <param name="instance">实例记录。</param>
        internal void UnregisterInstance(GameObjectInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            int instanceId = instance.InstanceId;
            if (_instances.TryGetValue(instanceId, out GameObjectInstance current) && ReferenceEquals(current, instance))
            {
                _instances.Remove(instanceId);
            }
        }

        /// <summary>
        /// 按克隆体反查实例记录。
        /// </summary>
        /// <remarks>
        /// 归还的入参是 GameObject，池内部靠它换回记录。
        /// 只查索引，不猜归属：索引里没有就说明这个对象不是池里的、或者已经归还过了，
        /// 由调用方明确失败，而不是就地补一条记录。
        /// <b>查到后必须复核记录绑的就是这个克隆体</b>：实例号在对象销毁后会被新对象复用，
        /// 只认号不认对象，会把别人的记录当成本次归还的目标。
        /// </remarks>
        /// <param name="gameObject">克隆体。</param>
        /// <param name="instance">查到的记录；查不到时为 null。</param>
        /// <returns>索引里命中这条记录时为 true。</returns>
        internal bool TryGetInstance(GameObject gameObject, out GameObjectInstance instance)
        {
            instance = null;

            if (gameObject == null
                || !_instances.TryGetValue(gameObject.GetInstanceID(), out GameObjectInstance found)
                || !ReferenceEquals(found.GameObject, gameObject))
            {
                return false;
            }

            instance = found;
            return true;
        }

        /// <summary>
        /// 按实例号与身份版本复核索引里是否还是这条记录。
        /// </summary>
        /// <remarks>
        /// 用在"注册是提交点"之后：底层注册可能当场释放掉这条记录并把它还给内存池，
        /// 此时不能再把它当作创建成功的实例返回。
        /// </remarks>
        /// <param name="instanceId">实例编号。</param>
        /// <param name="identity">实例身份版本。</param>
        /// <returns>索引里仍是这条记录时返回 true。</returns>
        internal bool ContainsInstance(int instanceId, long identity)
        {
            return _instances.TryGetValue(instanceId, out GameObjectInstance instance)
                   && instance.Identity == identity;
        }

        /// <summary>
        /// 登记一个池的预热，从下一次 <see cref="Update"/> 开始按它自己的每帧预算推进。
        /// </summary>
        /// <remarks>
        /// 由 <see cref="GameObjectPool"/> 的构造函数调用。建池本身是同步的，
        /// 这里只把池排进队列、一个实例都不建：真正的创建发生在之后的帧里，
        /// 所以配了预热的池也不会在建池那一刻多停顿。
        /// </remarks>
        /// <param name="pool">刚创建好、配置了预热的池。</param>
        internal void EnqueuePrewarm(GameObjectPool pool)
        {
            if (pool == null)
            {
                return;
            }

            _prewarmPools.Add(pool);
        }

        /// <summary>
        /// 模块轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。本模块不使用它。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 时间基准只用 realElapseSeconds，理由见 MonotonicSeconds
            _monotonicSeconds += realElapseSeconds;

            // 后续两步插在这里，顺序不能换：
            //   ① 清账（第 07 步）：先让本帧被外部销毁的借出对象减掉计数，
            //      否则池会被误判成"仍有占用"而错过本轮过期；
            //   ② 推进预热（第 09 步）：必须早于过期扫描，
            //      否则刚预热完的池会在同一帧被判成"已空闲"却还带着未结算的阻碍项；
            //   ③ 池级过期扫描（第 07 步）：按每个池自己的 PoolAutoReleaseInterval 节流。
            // 底层 ObjectPoolModule 由框架自己轮询，这里不重复调用它的 Update。
            AdvancePrewarm();
        }

        /// <summary>
        /// 按每个池自己的每帧预算推进预热，推完的池当场移出队列。
        /// </summary>
        /// <remarks>
        /// 倒序遍历：正序边遍历边移出会漏掉紧挨着的下一个元素。
        /// 每池的预算各算各的、不共用全局额度，所以不需要轮转起点来防饿死。
        /// </remarks>
        private void AdvancePrewarm()
        {
            for (int i = _prewarmPools.Count - 1; i >= 0; i--)
            {
                if (_prewarmPools[i].AdvancePrewarm())
                {
                    _prewarmPools.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 关闭并清理模块，可重复调用。
        /// </summary>
        /// <remarks>
        /// 先释放运行池再销毁根：底层销毁池时会逐个释放记录，每条记录都要销毁自己的克隆体，
        /// 那时代码还要读克隆体的变换与父子关系，节点先没了就只剩一堆空引用。
        /// 预制体引用的归还在第 07 步补进来，本步只保证池对象与容器不再残留。
        /// </remarks>
        public override void Shutdown()
        {
            ReleasePools();

            // 池节点都是根的子节点，销毁根即整棵子树一起没了，不需要逐个池销毁容器
            DestroyObject(_root);

            // 这几步都不需要先判空：DestroyObject 已处理 null，空字典 Clear 是空操作，
            // 所以不必为幂等单独写一个提前返回。
            // 两个依赖引用也置 null：关闭后 _configs 已清空，再有人调 TryGetPoolOrCreate
            // 会在"没有这个池的配置"处返回，走不到对已经关掉的底层模块发指令的路径上
            _root = null;
            _objectPoolModule = null;
            _resourceModule = null;
            _instances.Clear();
            _prewarmPools.Clear();
            _configs.Clear();
            _monotonicSeconds = 0d;
        }

        /// <summary>
        /// 关闭并清空全部运行池。
        /// </summary>
        /// <remarks>
        /// 只做"把底层池从对象池模块里摘掉"，让下次初始化能重新建同名池；
        /// 业务回调、租约失效与资源引用归还都留给第 07 步。
        /// 按"类型 + 池名"销毁，不去池对象里取后端引用：建池用的就是这对键
        /// （<c>CreateSingleSpawnObjectPool&lt;GameObjectInstance&gt;(poolName, ...)</c>），
        /// 这样池内部持有的底层对象池不必为了关闭而对外暴露。
        /// </remarks>
        private void ReleasePools()
        {
            if (_pools.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, GameObjectPool> pair in _pools)
            {
                _objectPoolModule.DestroyObjectPool<GameObjectInstance>(pair.Key);
            }

            _pools.Clear();
        }

        /// <summary>
        /// 建立模块根节点。
        /// </summary>
        /// <remarks>
        /// 只建这一个节点。池各自的子节点由建池时创建：
        /// 一个池都没用到时，不该先在场景里摆一排空容器。
        /// 根由模块独占所有权，业务不持有，也不该销毁它。
        /// 本方法只在运行时被走到——编辑器面板查数据走 <c>TryGetModule</c>，
        /// 它不会创建模块，因此也不会在编辑模式下往场景里塞节点。
        /// </remarks>
        private void CreateRoot()
        {
            _root = new GameObject(MODULE_ROOT_NAME);

            // 编辑模式下 DontDestroyOnLoad 不生效且会打警告；
            // 正式初始化点在运行时启动流程里，这里只是防御
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(_root);
            }
        }

        /// <summary>
        /// 按运行模式选择销毁方式。
        /// </summary>
        /// <remarks>
        /// 池、实例记录与模块根都要销毁 GameObject，放在这里统一一份，
        /// 免得同一段分支在三个文件里各写一遍、哪天改了其中一个忘了另外两个。
        /// </remarks>
        /// <param name="target">要销毁的对象，可为 null。</param>
        internal static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            // Destroy 在编辑模式下不生效，会留下孤儿对象；
            // DestroyImmediate 在运行时会打断本帧，所以两者不能统一成一个
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}

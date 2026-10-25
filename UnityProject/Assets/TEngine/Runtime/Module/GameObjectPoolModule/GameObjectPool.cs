using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 一个运行池：一份只读配置、一个预制体资产引用、一个底层对象池、一个池节点。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 是 class 而不是 struct：它有稳定身份（配置里的池名）、持有需要在销毁时归还一次的
    /// 资源引用，还被底层对象池与自己创建出来的记录同时引用；复制一份就等于多出一个池。
    /// </para>
    /// <para>
    /// <b>建池即加载</b>：配置里只有定位地址，预制体由 <see cref="GameObjectPoolModule"/>
    /// 在建池时同步加载，加载结果交给本类保存。之后创建实例只 <c>Instantiate</c> 这个引用，
    /// 不再访问资源模块。
    /// </para>
    /// <para>
    /// <b>预制体引用只在本类手里</b>：克隆体不带 <c>AssetsReference</c>，销毁克隆体不会
    /// 归还引用计数；归还动作只在池被销毁时做一次（第 07 步）。所以这里既不能 <c>Destroy</c>
    /// 它，也不能改它的组件与变换。
    /// </para>
    /// <para>
    /// <b>三个池级计时字段放在这里</b>，不放进实例记录、不写进配置资产、不上移到底层
    /// <see cref="ObjectBase"/>：它们描述的是"这一池此刻有没有占用、有没有在途预热"，
    /// 池里任何一个实例都回答不了这个问题。
    /// </para>
    /// </remarks>
    internal sealed class GameObjectPool
    {
        /// <summary>
        /// 所属模块，提供单调时钟与实例索引。
        /// </summary>
        private readonly GameObjectPoolModule _module;

        /// <summary>
        /// 建池时复制下来的只读配置，之后资产被改动也不影响这个池。
        /// </summary>
        private readonly RuntimePoolConfig _config;

        /// <summary>
        /// 承载本池全部实例的节点，建池时创建在模块根下，池销毁时整棵销毁。
        /// </summary>
        private readonly GameObject _container;

        /// <summary>
        /// 底层对象池，单次借出。
        /// </summary>
        private readonly IObjectPool<GameObjectInstance> _backend;

        /// <summary>
        /// 预制体资产引用，归还凭据就是它本身。
        /// </summary>
        private readonly GameObject _prefab;

        /// <summary>
        /// 池内实例身份版本的计数器，每个新建实例取一次。
        /// </summary>
        private long _nextIdentity;

        /// <summary>
        /// 当前处于借出占用态的实例数。
        /// </summary>
        private int _activeCount;

        /// <summary>
        /// 本池在途预热请求数，0 表示没有预热在阻碍空闲计时。
        /// </summary>
        private int _pendingPrewarmCount;

        /// <summary>
        /// 本轮连续空闲的起点（单调时钟秒数），负数表示不可过期。
        /// </summary>
        /// <remarks>
        /// 用 float 而不是模块时钟的 double：这个值只参与"过了多久"的减法，
        /// 秒级的过期判定不需要 double 的精度。单调时钟累计到十万秒（约 27 小时）时，
        /// float 仍能分辨约 8 毫秒，远小于最短的过期时间。
        /// </remarks>
        private float _idleStartSeconds;

        /// <summary>
        /// 创建一个运行池。
        /// </summary>
        /// <param name="module">所属模块。</param>
        /// <param name="config">已校验的只读配置。</param>
        /// <param name="prefab">已加载的预制体资产引用。</param>
        /// <param name="container">本池的容器节点，已经挂在模块根下。</param>
        /// <param name="backend">已创建的底层对象池。</param>
        internal GameObjectPool(
            GameObjectPoolModule module,
            RuntimePoolConfig config,
            GameObject prefab,
            GameObject container,
            IObjectPool<GameObjectInstance> backend)
        {
            _module = module;
            _config = config;
            _prefab = prefab;
            _container = container;
            _backend = backend;

            _nextIdentity = 0L;
            _activeCount = 0;
            _pendingPrewarmCount = 0;
            _idleStartSeconds = -1f;

            // 建池就决定这一池"此刻算不算空闲"：没有预热就是空池，当场开始计时；
            // 有预热就先挂上阻碍项，等预热结束那一刻再开计时。
            // 起点必须是 -1，否则 TryBeginIdle 里"当前不在计时"那个条件不成立，空池永远不过期
            if (_config.PrewarmCount <= 0)
            {
                TryBeginIdle();
                return;
            }

            _pendingPrewarmCount = 1;
            _module.EnqueuePrewarm(this);
        }

        /// <summary>
        /// 当前处于借出占用态的实例数。
        /// </summary>
        /// <remarks>
        /// 从这里往下的四个读数只回答"这一池此刻什么状况"，不转发任何字段：
        /// 容器、预制体、底层池、配置、所属模块都只在本类内部使用。
        /// 保留读数的原因是模块的预热调度、监控面板与验证要观察池的进度，
        /// 但它们只能看，改状态仍然只能走 <see cref="Spawn"/>、<see cref="Despawn"/> 与预热。
        /// </remarks>
        internal int ActiveCount => _activeCount;

        /// <summary>
        /// 本池在途预热请求数。
        /// </summary>
        internal int PendingPrewarmCount => _pendingPrewarmCount;

        /// <summary>
        /// 本轮连续空闲的起点（单调时钟秒数），负数表示不可过期。
        /// </summary>
        internal float IdleStartSeconds => _idleStartSeconds;

        /// <summary>
        /// 本池当前受管实例总数，含空闲与借出。
        /// </summary>
        internal int TotalCount => _backend.Count;

        /// <summary>
        /// 尝试开启一轮空闲计时。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 三个条件都成立才计时：没有借出、没有在途预热、当前不在计时。
        /// 第三个条件是冗余守卫——已经在计时时重复调用不会把起点往后推，
        /// 将来若有人误在每帧调用，退化成空操作，而不是"池永不过期"这类隐蔽故障。
        /// </para>
        /// <para>
        /// 用负数当哨兵，不用"正数极大值"当永久标记、也不用 <c>bool + time</c> 双字段：
        /// "是否处于可过期空闲"和"空闲了多久"必须是同一个值的两种读法，
        /// 分开存就会出现标志位与时间戳互相矛盾的中间态。
        /// </para>
        /// </remarks>
        private void TryBeginIdle()
        {
            if (_activeCount == 0 && _pendingPrewarmCount == 0 && _idleStartSeconds < 0f)
            {
                _idleStartSeconds = (float)_module.MonotonicSeconds;
            }
        }

        /// <summary>
        /// 按本池自己的每帧预算推进一步预热。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 预算算的是<b>创建尝试次数</b>，不是成功个数：一次失败的尝试也消耗预算，
        /// 否则一个建不出实例的池会在同一帧里被重试到帧末，把主线程卡住。
        /// </para>
        /// <para>
        /// <b>总数不增长就结束</b>：底层注册在总量超过容量时会顺手释放空闲实例，
        /// <c>TotalCount</c> 可能原地不动。这时继续循环只会一直失败，
        /// 必须当作预热结束——否则池永远停在"预热中"，带着阻碍项永不过期。
        /// </para>
        /// <para>
        /// 预算为 0 或负数属配置错误，只由编辑器侧的校验报错，这里不兜底：
        /// 那种配置下预热不会推进，也就不会结束，与本模块"运行期不重复校验配置"的取舍一致。
        /// </para>
        /// </remarks>
        /// <returns>预热已结束（达标或无法继续）时为 true。</returns>
        internal bool AdvancePrewarm()
        {
            int target = _config.PrewarmCount;
            int budget = _config.PrewarmPerFrame;

            while (budget > 0 && TotalCount < target)
            {
                budget--;

                int before = TotalCount;
                CreateInstance(false);

                if (TotalCount <= before)
                {
                    Log.Warning("[GameObjectPool] 预热提前结束：池 " + _config.PoolName + " 无法继续创建实例，目标 " + target + "，当前 " + TotalCount + "。");
                    FinishPrewarm();
                    return true;
                }
            }

            if (TotalCount < target)
            {
                return false;
            }

            FinishPrewarm();
            return true;
        }

        /// <summary>
        /// 结算本次预热：摘掉阻碍项，条件满足就立刻开始计时。
        /// </summary>
        /// <remarks>
        /// 不等下一个检查点：预热结束的这一刻就是这一池重新变成"整池可过期空闲"的起点。
        /// </remarks>
        private void FinishPrewarm()
        {
            _pendingPrewarmCount = 0;
            TryBeginIdle();
        }

        /// <summary>
        /// 从池里借出一个克隆体：有空闲就复用，没有就新建。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 对外就是"给一个 GameObject"，与回收同一个形状：调用方借出、使用、归还，
        /// 全程只见克隆体本身，不需要知道实例记录、租约或底层登记——池名已经决定了这些。
        /// </para>
        /// <para>
        /// 借出即退出可过期空闲：计时期间被借出还接着计时，池会在有人用的时候被判成过期。
        /// </para>
        /// </remarks>
        /// <returns>借出的克隆体；取不到实例时返回 null。</returns>
        internal GameObject Spawn()
        {
            // 先找空闲；找不到就新建一条按借出态注册的记录
            if (!_backend.TrySpawn(_config.PoolName, out GameObjectInstance instance))
            {
                instance = CreateInstance(true);
            }
            else if (instance.GameObject == null)
            {
                // 克隆体已经被外部销毁，底层却还留着这条空闲记录（完整清账在第 07 步）。
                // 先还回去让它的获取计数归零，再当作没有空闲实例处理，避免把死引用交出去
                _backend.Unspawn(instance);
                instance = CreateInstance(true);
            }

            if (instance == null)
            {
                return null;
            }

            instance.IsSpawned = true;
            _activeCount++;
            _idleStartSeconds = -1f;

            return instance.GameObject;
        }

        /// <summary>
        /// 把一个克隆体还给池。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 参数是 GameObject，不是实例记录：借出和归还拿的是同一个东西，
        /// 池内部再用实例号把它换回记录。查不到就明确失败——那说明它不是这个池的对象，
        /// 或者已经归还过一次；静默当成归还成功会让占用计数越扣越少。
        /// </para>
        /// <para>
        /// 停用与坐标复位不在这里：底层 <c>Unspawn</c> 会调
        /// <see cref="GameObjectInstance.OnUnspawn"/>，那才是与获取计数配对的时机。
        /// 本方法只负责"退出占用、交还底层"，克隆体始终挂在池节点下。
        /// </para>
        /// <para>
        /// <b>清标志与扣计数必须早于 <c>backend.Unspawn</c></b>：底层在总数超过容量时
        /// 会顺手释放空闲记录，那条记录立刻走到 <see cref="GameObjectInstance.Release"/>。
        /// 第 06 步加归还回调后，回调要跑在 <c>Unspawn</c> 之前（业务在回调里仍应看到
        /// "借出中"），但这两句与 <c>Unspawn</c> 的先后不变。
        /// </para>
        /// </remarks>
        /// <param name="gameObject">要归还的克隆体。</param>
        /// <returns>归还成功为 true；对象不属于本池或已经归还过为 false。</returns>
        internal bool Despawn(GameObject gameObject)
        {
            // 三个条件缺一不可：索引里查得到、属于这个池、此刻确实在借出态。
            // 少了最后一条，重复归还会把底层的获取计数扣成负数（底层会当场抛异常），
            // 占用计数也会跟着多扣一次
            if (!_module.TryGetInstance(gameObject, out GameObjectInstance instance)
                || !ReferenceEquals(instance.Pool, this)
                || !instance.IsSpawned)
            {
                Log.Warning("[GameObjectPool] 归还未生效：对象不属于池 " + _config.PoolName + "，或已经归还过。");
                return false;
            }

            // 先退出占用再交还底层：底层在超容量时会当场释放这条记录
            instance.IsSpawned = false;
            _activeCount--;

            if (_activeCount == 0)
            {
                TryBeginIdle();
            }

            _backend.Unspawn(instance);
            return true;
        }

        /// <summary>
        /// 注销一个记录的索引。
        /// </summary>
        /// <remarks>
        /// 只有一条路径：底层释放记录（由 <see cref="GameObjectInstance.Release"/> 调），
        /// 底层保证每条记录只释放一次，所以这里不需要再判"是不是已经注销过"。
        /// 占用计数不在这里扣——它只在 <see cref="Spawn"/> 与 <see cref="Despawn"/> 里配对。
        /// </remarks>
        /// <param name="instance">要注销的记录。</param>
        internal void Unregister(GameObjectInstance instance)
        {
            _module.UnregisterInstance(instance);
        }

        /// <summary>
        /// 克隆预制体并登记一个记录。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 克隆体挂在池容器下，创建后立刻 <c>SetActive(false)</c>：容器的层级是常开的，
        /// 所以预制体本身是激活态时，这次实例化会让它跑一次 <c>Awake</c>/<c>OnEnable</c>。
        /// 这是"不要 Inactive 层容器"的已知代价——业务不能在 <c>Awake</c> 里假定
        /// 变换与父节点已经就位，准备逻辑应放进第 05 步的借出回调。
        /// </para>
        /// <para>
        /// <b>注册是提交点</b>：底层的 <c>Register</c> 在总数超过容量时会顺手
        /// <c>Release</c> 掉若干空闲实例，那些记录会立刻被销毁并还给内存池。
        /// 所以提交之后只用捕获下来的实例号与身份复核，不再访问未经核对的状态。
        /// </para>
        /// <para>
        /// <b>借出扩容必须直接按占用态注册</b>：先注册空闲再借出，在 <c>Capacity = 0</c>
        /// 或已达容量时，空闲注册会立刻把手里的实例释放掉，借出方拿到的就是死引用。
        /// 占用计数不在这里加，由 <see cref="Spawn"/> 在拿到能用的实例之后统一加一次。
        /// </para>
        /// <para>
        /// 之所以不是 <c>private</c>：同模块的预热与验证要分别造"空闲"与"借出"两条
        /// 路径的记录。运行期业务不该直接调它——按借出态建出来的记录并不计入占用，
        /// 等于绕过了 <see cref="Spawn"/> 的账，只有预热与测试需要这种半成品。
        /// </para>
        /// </remarks>
        /// <param name="spawned">是否按已借出注册。</param>
        /// <returns>创建好的记录；克隆失败或记录已被底层清掉时返回 null。</returns>
        internal GameObjectInstance CreateInstance(bool spawned)
        {
            if (_prefab == null)
            {
                Log.Error("[GameObjectPool] 创建实例失败：池 " + _config.PoolName + " 没有预制体引用。");
                return null;
            }

            GameObject clone = Object.Instantiate(_prefab, _container.transform);
            clone.SetActive(false);

            long identity = ++_nextIdentity;

            // 池名与优先级是底层 ObjectBase 的元数据，由池从配置里取出来交给记录，
            // 记录不反向读池的属性：池不对外暴露自己的字段
            GameObjectInstance instance = GameObjectInstance.Create(
                this, _config.PoolName, _config.Priority, clone, identity, spawned);
            int instanceId = instance.InstanceId;

            // 先登记模块索引再注册后端：后端注册可能触发释放，释放路径要能查到索引
            _module.RegisterInstance(instance);

            _backend.Register(instance, spawned);

            if (!_module.ContainsInstance(instanceId, identity))
            {
                // 已被底层在注册时释放，不能把这条记录当成功实例返回
                return null;
            }

            return instance;
        }
    }
}

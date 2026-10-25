using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 一个受管实例的记录：包装 <see cref="ObjectBase"/>，作为底层对象池的存储单位。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 必须是 class：它继承了 <see cref="ObjectBase"/>，是后端占用计数与
    /// <c>MemoryPool</c> 复用的行为对象，复制一份就多出一个假记录。
    /// </para>
    /// <para>
    /// <b><see cref="ObjectBase.Target"/> 指向记录自身</b>，不指向 GameObject：
    /// 底层 <c>ObjectPool&lt;T&gt;</c> 用 <c>Target</c> 做 <c>_objectMap</c> 的键，
    /// 而 UnityEngine.Object 在销毁后参与哈希会失效，键留在字典里就再也查不到也删不掉。
    /// 记录是纯 C# 对象，身份与哈希都不受 Unity 生命周期影响。
    /// </para>
    /// <para>
    /// <b>借出态就是 <see cref="ObjectBase.Locked"/></b>（2026-10-25 20:20 删掉独立的
    /// <c>_spawned</c> 字段）：底层用 <c>Locked</c> 表示"这条记录不许被自动释放"，
    /// 与"此刻正被业务占用"是同一件事，再存一个布尔只会让两处状态有机会不同步。
    /// <see cref="IsSpawned"/> 因此只是它的别名，不是第二个字段。
    /// </para>
    /// <para>
    /// <b>不用 GameObject 的 <c>activeSelf</c> 或 <c>activeInHierarchy</c> 反推占用</b>：
    /// 业务完全可能借出一个自己保持禁用的对象，那时它仍然是借出态。
    /// </para>
    /// <para>
    /// <b>没有归属标记组件、没有首次借出标志、没有初始缩放、没有实例级空闲起点</b>
    /// （2026-10-25 20:20 一起删）：激活/停用与坐标复位放进
    /// <see cref="OnSpawn"/> / <see cref="OnUnspawn"/>，这两个回调由底层保证与获取计数
    /// 严格配对；其余三项在真正要用之前不预置字段——首次与复用的差异按第 05 步的需求再加。
    /// </para>
    /// <para>
    /// 预制体资产引用不在这里：它是"池"这一级的东西，实例只认自己的克隆体。
    /// </para>
    /// </remarks>
    internal sealed class GameObjectInstance : ObjectBase
    {
        /// <summary>
        /// 所属运行池，销毁后为 null。
        /// </summary>
        private GameObjectPool _pool;

        /// <summary>
        /// 克隆体本身，销毁后为 null。
        /// </summary>
        private GameObject _gameObject;

        /// <summary>
        /// 创建时捕获的 <see cref="GameObject.GetInstanceID"/>，用于模块的实例索引。
        /// </summary>
        private int _instanceId;

        /// <summary>
        /// 实例身份版本，池内单调递增，用来识别"这条记录属于哪一次创建"。
        /// </summary>
        private long _identity;

        /// <summary>
        /// 所属运行池，销毁后为 null。
        /// </summary>
        internal GameObjectPool Pool => _pool;

        /// <summary>
        /// 克隆体本身，销毁后为 null。
        /// </summary>
        internal GameObject GameObject => _gameObject;

        /// <summary>
        /// 实例索引键，创建时捕获，销毁后仍然保留原值直到 <see cref="Clear"/>。
        /// </summary>
        internal int InstanceId => _instanceId;

        /// <summary>
        /// 实例身份版本。
        /// </summary>
        internal long Identity => _identity;

        /// <summary>
        /// 是否处于借出占用态，读写的就是 <see cref="ObjectBase.Locked"/>。
        /// </summary>
        internal bool IsSpawned
        {
            get => Locked;
            set => Locked = value;
        }

        /// <summary>
        /// 从内存池取出一个记录并绑定到指定克隆体。
        /// </summary>
        /// <param name="pool">所属运行池。</param>
        /// <param name="name">底层池名，同时是底层按名字分组时的组名，由池从配置里取出。</param>
        /// <param name="priority">底层清理优先级，由池从配置里取出。</param>
        /// <param name="clone">已经实例化的克隆体。</param>
        /// <param name="identity">实例身份版本，由池分配。</param>
        /// <param name="spawned">是否按借出态创建。</param>
        /// <returns>绑定完成的记录。</returns>
        internal static GameObjectInstance Create(
            GameObjectPool pool,
            string name,
            int priority,
            GameObject clone,
            long identity,
            bool spawned)
        {
            GameObjectInstance instance = MemoryPool.Acquire<GameObjectInstance>();

            instance._pool = pool;
            instance._gameObject = clone;
            instance._instanceId = clone.GetInstanceID();
            instance._identity = identity;

            // Target 传记录自身，见类注释；locked 与"是否已借出"是同一件事
            instance.Initialize(name, instance, spawned, priority);

            return instance;
        }

        /// <summary>
        /// 借出时的激活。
        /// </summary>
        /// <remarks>
        /// 底层在"按借出态注册"与"复用空闲记录"两条路径上都会调它，
        /// 所以池的 <see cref="GameObjectPool.Spawn"/> 不必自己再 <c>SetActive</c> 一次。
        /// </remarks>
        protected internal override void OnSpawn()
        {
            if (_gameObject != null)
            {
                _gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 归还时的停用与复位。
        /// </summary>
        /// <remarks>
        /// 底层保证它与获取计数成对调用，所以停用放在这里，不写在池的 <c>Despawn</c> 里。
        /// 克隆体挂在池容器下，<c>position</c> 就是相对容器的局部坐标，归零即回到池节点原点。
        /// </remarks>
        protected internal override void OnUnspawn()
        {
            if (_gameObject == null)
            {
                // 克隆体已被外部销毁，没有可复位的东西
                return;
            }

            _gameObject.SetActive(false);
            _gameObject.transform.position = Vector3.zero;
            _gameObject.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 释放记录：注销索引并销毁克隆体。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 调用者是底层的 <c>Object&lt;T&gt;.Release</c>，它在本方法返回后会立刻把记录
        /// 还给 <c>MemoryPool</c>（触发 <see cref="Clear"/>），所以本方法之后不能再被读。
        /// </para>
        /// <para>
        /// 不碰池的占用计数：底层只释放空闲记录（<c>ReleaseObject</c> 会挡掉
        /// <c>IsInUse</c> 与 <c>Locked</c>），借出中的记录只在关闭池时被强制释放，
        /// 那时整个池都不再使用，计数归零没有意义。占用计数只在
        /// <see cref="GameObjectPool.Spawn"/> 与 <see cref="GameObjectPool.Despawn"/> 里配对加减。
        /// </para>
        /// </remarks>
        /// <param name="isShutdown">是否是关闭对象池时触发。</param>
        protected internal override void Release(bool isShutdown)
        {
            GameObject gameObject = _gameObject;
            GameObjectPool pool = _pool;

            _pool = null;
            _gameObject = null;

            if (pool != null)
            {
                pool.Unregister(this);
            }

            if (gameObject != null)
            {
                GameObjectPoolModule.DestroyObject(gameObject);
            }
        }

        /// <summary>
        /// 清空记录，回到内存池复用前的空白状态。
        /// </summary>
        /// <remarks>
        /// 不作为业务清理的替代：销毁行为已经在 <see cref="Release"/> 里做完，
        /// 这里只负责让对象能被下一次 <c>Acquire</c> 安全重用。
        /// </remarks>
        public override void Clear()
        {
            base.Clear();

            _pool = null;
            _gameObject = null;
            _instanceId = 0;
            _identity = 0L;
        }
    }
}

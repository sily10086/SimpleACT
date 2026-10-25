using TEngine;
using UnityEngine;

namespace GameCommon.GameObjectPoolModule
{
    /// <summary>
    /// 游戏对象池组件
    /// 挂载在游戏对象上，管理特定类型游戏对象的池化操作
    /// </summary>
    public sealed class GameObjectPool : MonoBehaviour
    {
        /// <summary>
        /// 底层对象池实例，管理GameObjectInstance对象的存储和回收
        /// </summary>
        private IObjectPool<GameObjectInstance> _objectPool;
        
        /// <summary>
        /// 要池化的预制体引用
        /// </summary>
        private GameObject _prefab;

        /// <summary>
        /// 标记对象池是否已初始化
        /// </summary>
        private bool _initialized;
        
        /// <summary>
        /// 获取对象池名称
        /// 显示在Inspector面板中
        /// </summary>
        public string Name => _objectPool.Name;
        
        /// <summary>
        /// 获取对象池中对象的总数量
        /// 显示在Inspector面板中
        /// </summary>
        public int Count => _objectPool.Count;
        
        /// <summary>
        /// 获取或设置实例对象池自动释放可释放对象的间隔秒数
        /// 在Inspector面板中只读显示
        /// </summary>
        public float AutoReleaseInterval
        {
            get => _objectPool.AutoReleaseInterval;
            
            set => _objectPool.AutoReleaseInterval = value;
        }
        
        /// <summary>
        /// 获取或设置实例对象池的容量
        /// 在Inspector面板中只读显示
        /// </summary>
        public int Capacity
        {
            get => _objectPool.Capacity;
            
            set => _objectPool.Capacity = value;
        }
        
        /// <summary>
        /// 获取或设置实例对象池对象过期秒数
        /// 在Inspector面板中只读显示
        /// </summary>
        public float ExpireTime
        {
            get => _objectPool.ExpireTime;
            
            set => _objectPool.ExpireTime = value;
        }
        
        /// <summary>
        /// 获取或设置实例对象池的优先级
        /// 在Inspector面板中只读显示
        /// </summary>
        public int Priority
        {
            get => _objectPool.Priority;
            
            set => _objectPool.Priority = value;
        }

        /// <summary>
        /// 创建对象池，内部初始化方法
        /// </summary>
        /// <param name="poolConfig">对象池配置</param>
        internal void CreatePool(PoolConfig poolConfig)
        {
            // 检查对象池是否已经初始化
            if (_initialized)
            {
                // 如果已初始化，记录错误日志
                Log.Error("GameObject pool is already initialized.");
                
                return;
            }
            
            // 标记对象池已初始化
            _initialized = true;
            
            // 通过模块系统获取对象池模块
            var objectPoolModule = ModuleSystem.GetModule<IObjectPoolModule>();
            
            // 创建单生成对象池，管理GameObjectInstance类型对象
            _objectPool = objectPoolModule.CreateSingleSpawnObjectPool<
                GameObjectInstance>(name, poolConfig.AutoReleaseInterval, 
                poolConfig.Capacity, poolConfig.ExpireTime, poolConfig.Priority);
            
            // 保存预制体引用，用于后续动态创建
            _prefab = poolConfig.Prefab;
        }
        
        /// <summary>
        /// 释放对象池
        /// 清理所有池化对象并销毁对象池
        /// </summary>
        internal void ReleasePool()
        {
            // 检查对象池是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("Object pool is not initialized.");
                
                return;
            }
            
            // 标记对象池已释放，不再可用
            _initialized = false;
            
            // 释放对象池中所有未使用的对象
            _objectPool.ReleaseAllUnused();
            
            // 获取对象池模块
            var objectPoolModule = ModuleSystem.GetModule<IObjectPoolModule>();

            // 销毁底层对象池实例
            objectPoolModule.DestroyObjectPool(_objectPool);
        }

        /// <summary>
        /// 从对象池中生成一个游戏对象
        /// </summary>
        /// <param name="name">对象名称</param>
        /// <returns>生成的游戏对象，如果失败则返回null</returns>
        internal GameObject Spawn(string name)
        {
            // 检查对象池是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("Object pool is not initialized.");
                
                return null;
            }

            // 尝试从对象池中获取空闲的GameObjectInstance
            if (!_objectPool.TrySpawn(name, out var gameObjectInstance))
            {
                // 池中没有空闲对象，需要动态创建新的
                // 实例化新的游戏对象，并设置为当前对象池的子物体
                GameObject newObj = Instantiate(_prefab, transform);

                // 创建GameObjectInstance实例来包装新创建的游戏对象
                gameObjectInstance = GameObjectInstance.Create(name, newObj);

                // 将新创建的对象注册到对象池中，并标记为正在使用
                _objectPool.Register(gameObjectInstance, true);

                return newObj;
            }

            // 获取池中的游戏对象
            return gameObjectInstance.GameObject;
        }

        /// <summary>
        /// 将游戏对象回收到对象池
        /// </summary>
        /// <param name="gameObject">要回收的游戏对象</param>
        internal void Despawn(GameObject gameObject)
        {
            // 检查对象池是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("Object pool is not initialized.");

                return;
            }
            
            // 设置游戏对象的父物体为当前对象池
            gameObject.transform.SetParent(transform);
            
            // 将游戏对象回收到对象池中
            _objectPool.Unspawn(gameObject);
        }
    }
}
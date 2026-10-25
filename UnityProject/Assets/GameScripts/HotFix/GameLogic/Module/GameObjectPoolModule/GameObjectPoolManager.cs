using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using GameLogic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameCommon.GameObjectPoolModule
{
    /// <summary>
    /// 游戏对象池管理器
    /// 继承自单例模式，负责管理所有游戏对象池的创建、销毁和生命周期
    /// </summary>
    public sealed class GameObjectPoolManager : Singleton<GameObjectPoolManager>
    {
        /// <summary>
        /// 对象池根节点的名称常量
        /// </summary>
        private const string POOL_NAME = "[GameObjectPool]";
        
        /// <summary>
        /// 对象池根节点的Transform组件
        /// 所有对象池游戏对象都挂载在此节点下
        /// </summary>
        private Transform _poolRoot;
        
        /// <summary>
        /// 懒加载对象池配置字典
        /// </summary>
        private readonly Dictionary<string, PoolConfig> _poolConfigs 
            = new Dictionary<string, PoolConfig>(24);

        /// <summary>
        /// 按名称索引的对象池字典
        /// 键为对象池名称，值为对应的对象池组件
        /// </summary>
        private readonly Dictionary<string, GameObjectPool> _pools 
            = new Dictionary<string, GameObjectPool>(24);

        /// <summary>
        /// 按对象实例ID索引的对象池字典
        /// 键为对象实例的唯一ID，值为所属的对象池组件
        /// 用于快速查找对象所属的对象池
        /// </summary>
        private readonly Dictionary<int, GameObjectPool> _objectPools 
            = new Dictionary<int, GameObjectPool>(1024);
        
        private IResourceModule _resourceModule;

        /// <summary>
        /// 单例初始化方法
        /// 在单例首次创建时调用，创建对象池根节点
        /// </summary>
        protected override void OnInit()
        {
            // 创建名为POOL_NAME的空游戏对象，并获取其Transform组件作为根节点
            _poolRoot = new GameObject(POOL_NAME).transform;

            _resourceModule = ModuleSystem.GetModule<IResourceModule>();
            
            // 不要卸载对象池根节点
            Object.DontDestroyOnLoad(_poolRoot);
        }

        /// <summary>
        /// 单例释放方法
        /// 在单例被销毁时调用，释放所有对象池资源
        /// </summary>
        protected override void OnRelease()
        {
            // 遍历所有对象池的副本列表（避免遍历时修改集合）
            foreach (var gameObjectPool in _pools.Values.ToList())
            {
                // 检查对象池是否有效
                if (gameObjectPool != null)
                {
                    // 释放对象池内部的所有资源
                    gameObjectPool.ReleasePool();
                    
                    // 销毁对象池的游戏对象
                    Object.Destroy(gameObjectPool.gameObject);
                }
            }
            
            // 清空按名称索引的对象池字典
            _pools.Clear();
            
            // 清空按实例ID索引的对象池字典
            _objectPools.Clear();
            
            // 检查根节点是否存在
            if(_poolRoot != null)
                // 销毁根节点游戏对象
                Object.Destroy(_poolRoot.gameObject);
        }

        #region 创建、销毁

        /// <summary>
        /// 创建一个新的对象池
        /// </summary>
        /// <param name="poolName">对象池名称</param>
        /// <param name="prefab">要池化的预制体</param>
        /// <param name="capacity">对象池容量</param>
        /// <param name="autoReleaseInterval">自动释放间隔时间</param>
        /// <param name="expireTime">对象过期时间</param>
        /// <param name="priority">对象池优先级</param>
        public void CreatePool(string poolName, GameObject prefab, int capacity,
            float autoReleaseInterval, float expireTime, int priority)
        {
#if UNITY_EDITOR
            
            if (string.IsNullOrEmpty(poolName))
            {
                Log.Error("[GameObjectPoolManager] 创建对象池失败：对象池名称不能为空");
                
                return;
            }
            
            if (prefab == null)
            {
                Log.Error($"[GameObjectPoolManager] 创建对象池失败：{poolName} 的预制体不能为空");
                
                return;
            }
            
            if (capacity <= 0)
            {
                Log.Error($"[GameObjectPoolManager] 创建对象池失败：{poolName} 的容量必须大于0，当前值: {capacity}");
                
                return;
            }
            
#endif
            
            // 检查是否已存在同名的对象池
            if (_poolConfigs.ContainsKey(poolName))
            {
                // 记录错误日志，避免重复创建
                Log.Error($"[GameObjectPoolManager] 创建对象池失败：对象池配置 {poolName} 已存在，请勿重复创建");
                
                return;
            }
            
            _poolConfigs.Add(poolName, new PoolConfig()
            {
                PoolName = poolName,
                
                Prefab = prefab,
                
                Capacity = capacity,
                
                AutoReleaseInterval = autoReleaseInterval,
                
                ExpireTime = expireTime,
                
                Priority = priority,
                
                IsCreating = false,
            });
        }

        /// <summary>
        /// 释放指定的对象池
        /// </summary>
        /// <param name="poolName">要释放的对象池名称</param>
        /// <returns>是否成功释放</returns>
        public bool ReleasePool(string poolName)
        {
#if UNITY_EDITOR
            
            if (string.IsNullOrEmpty(poolName))
            {
                Log.Error("[GameObjectPoolManager] 释放对象池失败：对象池名称不能为空");
               
                return false;
            }
            
#endif
            
            if (!_poolConfigs.Remove(poolName, out var poolConfig))
            {
                Log.Error($"[GameObjectPoolManager] 释放对象池失败：未找到名为 {poolName} 的对象池配置");
               
                return false;
            }
            
            if (!poolConfig.IsCreating)
            {
                Log.Info($"[GameObjectPoolManager] 对象池配置已移除（对象池尚未创建）: {poolName}");
                
                return true;
            }
            
            // 尝试从字典中移除并获取对象池
            if (!_pools.Remove(poolName, out GameObjectPool pool))
            {
                Log.Error($"[GameObjectPoolManager] 释放对象池失败：对象池 {poolName} 状态异常（配置标记为已创建但实际未找到）");
                
                return false;
            }
            
            // 释放对象池内部的所有资源
            pool.ReleasePool();
            
            // 销毁对象池的游戏对象
            Object.Destroy(pool.gameObject);
            
            return true;
        }

        private bool TryGetPool(string poolName, out GameObjectPool pool)
        {
            if (!_poolConfigs.TryGetValue(poolName, out PoolConfig config))
            {
                // // 查询配置表（懒加载入口）
                // var mapConfig = ConfigSystem.Instance.Tables.TbPoolMap.GetOrDefault(poolName);
                //
                // if (mapConfig == null)
                // {
                //     pool = null;
                //     return false;
                // }
                //
                // // 同步加载预制体并注册对象池配置
                // var prefab = _resourceModule.LoadAsset<GameObject>(mapConfig.AssetName);
                //
                // CreatePool(mapConfig.PoolName, prefab, mapConfig.Capacity,
                //     mapConfig.AutoReleaseInterval, mapConfig.ExpireTime, mapConfig.Priority);
                //
                // config = _poolConfigs[poolName];
            }

            // // 如果对象池尚未创建，则立即创建
            // if (!config.IsCreating)
            // {
            //     pool = DefaultCreatePool(config);
            //
            //     return true;
            // }

            return _pools.TryGetValue(poolName, out pool);
        }

        /// <summary>
        /// 默认的对象池创建方法
        /// </summary>
        /// <param name="poolConfig">对象池配置</param>
        /// <returns>创建的对象池实例</returns>
        private GameObjectPool DefaultCreatePool(PoolConfig poolConfig)
        {
            // 创建新的对象池游戏对象，名称格式为"[poolNamePool]"
            GameObject poolObject = new GameObject("[" + poolConfig.PoolName + "Pool]");
            
            // 将新创建的对象池游戏对象设置为根节点的子物体
            poolObject.transform.SetParent(_poolRoot);
            
            // 为游戏对象添加GameObjectPool组件
            GameObjectPool pool = poolObject.AddComponent<GameObjectPool>();
            
            // 初始化对象池，传入所有参数
            pool.CreatePool(poolConfig);
            
            // 将创建的对象池添加到字典中，以便后续管理
            _pools.Add(poolConfig.PoolName, pool);
            
            // 标记对象池已创建
            poolConfig.IsCreating = true;

            return pool;
        }

        #endregion

        #region 取出（从池中取出对象）

        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（默认位置和旋转）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用原点位置、默认旋转和无父节点
                return DefaultSpawn(poolName, pool, Vector3.zero, Quaternion.identity, null);
            }
            
            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");
            
            // 返回空表示生成失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName) where T : Component
        {
            // 先调用基础生成方法获取游戏对象
            var obj = Spawn(poolName);
                
            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
                
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");
            
            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定父节点）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName, Transform parent)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用原点位置、默认旋转和指定父节点
                return DefaultSpawn(poolName, pool, Vector3.zero, Quaternion.identity, parent);
            }
            
            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");
            
            // 返回空表示生成失败
            return null;
        }

        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定父节点）并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName, Transform parent) where T : Component
        {
            // 先调用指定父节点的生成方法获取游戏对象
            var obj = Spawn(poolName, parent);
                
            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
                
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");
            
            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName, Vector3 position)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用指定位置、默认旋转和无父节点
                return DefaultSpawn(poolName, pool, position, Quaternion.identity, null);
            }
            
            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");
            
            // 返回空表示生成失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置）并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName, Vector3 position) where T : Component
        {
            // 先调用指定位置的生成方法获取游戏对象
            var obj = Spawn(poolName, position);
            
            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
                
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");
            
            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置和旋转）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="rotation">生成旋转角度</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName, Vector3 position, Quaternion rotation)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用指定位置、旋转和无父节点
                return DefaultSpawn(poolName, pool, position, rotation, null);
            }
            
            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");
            
            // 返回空表示生成失败
            return null;
        }

        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置和旋转）并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="rotation">生成旋转角度</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName, Vector3 position, Quaternion rotation) where T : Component
        {
            // 先调用指定位置和旋转的生成方法获取游戏对象
            var obj = Spawn(poolName, position, rotation);
                
            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
                
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");
            
            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置和父节点）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName, Vector3 position, Transform parent)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用指定位置、默认旋转和指定父节点
                return DefaultSpawn(poolName, pool, position, Quaternion.identity, parent);
            }

            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");

            // 返回空表示生成失败
            return null;
        }

        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置和父节点）并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName, Vector3 position, Transform parent) where T : Component
        {
            // 先调用指定位置和父节点的生成方法获取游戏对象
            var obj = Spawn(poolName, position, parent);
            
            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
            
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");

            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置、旋转和父节点）
        /// </summary>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="rotation">生成旋转角度</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>生成的游戏对象实例</returns>
        public GameObject Spawn(string poolName, Vector3 position, Quaternion rotation, Transform parent)
        {
            // 尝试从对象池字典中获取对应名称的对象池
            if (TryGetPool(poolName, out var pool))
            {
                // 调用默认生成方法，使用指定位置、旋转和父节点
                return DefaultSpawn(poolName, pool, position, rotation, parent);
            }
            
            // 配置表中未配置该对象池，属于合法的无池状态
            Log.Warning($"[GameObjectPoolManager] {poolName} 未配置对象池，返回 null。");
            
            // 返回空表示生成失败
            return null;
        }

        /// <summary>
        /// 通过预制体名称从对象池生成游戏对象（指定位置、旋转和父节点）并获取指定组件
        /// </summary>
        /// <typeparam name="T">要获取的组件类型</typeparam>
        /// <param name="poolName">预制体资源名称</param>
        /// <param name="position">生成位置坐标</param>
        /// <param name="rotation">生成旋转角度</param>
        /// <param name="parent">父节点Transform</param>
        /// <returns>找到的组件实例</returns>
        public T Spawn<T>(string poolName, Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            // 先调用完整参数的生成方法获取游戏对象
            var obj = Spawn(poolName, position, rotation, parent);

            if (obj == null) return null;

            // 尝试从游戏对象获取指定类型组件
            if (obj.TryGetComponent(out T component))
                return component;
            
            // 记录错误日志：组件未找到
            Log.Error($"该物体没有{typeof(T)}的脚本。");
            
            // 返回空表示获取失败
            return null;
        }
        
        /// <summary>
        /// 默认的对象生成实现方法
        /// </summary>
        /// <param name="pool">对象池实例</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <param name="parent">父节点</param>
        /// <returns>生成的游戏对象</returns>
        private GameObject DefaultSpawn(string poolName, GameObjectPool pool, 
            Vector3 position, Quaternion rotation, Transform parent)
        {
            // 从对象池中获取游戏对象
            var obj = pool.Spawn(poolName);
            
            // 设置对象父节点
            if(parent != null)
                obj.transform.SetParent(parent);
            
            // 设置对象位置
            obj.transform.localPosition = position;
            
            // 设置对象旋转
            obj.transform.localRotation = rotation;
            
            // 获取实例化的Id
            int id = obj.GetHashCode();
            
            // 将对象添加到对象池反向映射字典
            _objectPools.Add(id, pool);

            return obj;
        }

        #endregion
        
        #region 回收操作（回收对象）

        /// <summary>
        /// 回收游戏对象到对象池
        /// </summary>
        /// <param name="gameObject">要回收的游戏对象</param>
        public void Despawn(GameObject gameObject)
        {
            // 立即执行回收
            NormalDespawn(gameObject);
        }
        
        /// <summary>
        /// 回收游戏对象到对象池
        /// </summary>
        /// <param name="gameObject">要回收的游戏对象</param>
        /// <param name="delay">延迟回收时间(秒)，0表示立即回收</param>
        public void Despawn(GameObject gameObject, float delay, 
            Action<GameObject> callBack = null, CancellationToken token = default)
        {
            // 启动异步延迟回收任务
            DelayDespawn(gameObject, delay, callBack, token).Forget();
        }

        /// <summary>
        /// 通过组件回收所在游戏对象到对象池
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="component">要回收的组件</param>
        public void Despawn<T>(T component) where T : Component
        {
            // 立即执行回收
            NormalDespawn(component.gameObject);
        }
        
        /// <summary>
        /// 通过组件回收所在游戏对象到对象池
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="component">要回收的组件</param>
        /// <param name="delay">延迟回收时间(秒)，0表示立即回收</param>
        public void Despawn<T>(T component, float delay, 
            Action<GameObject> callBack = null, 
            CancellationToken token = default) where T : Component
        {
            // 启动异步延迟回收任务
            DelayDespawn(component.gameObject, delay, callBack, token).Forget();
        }

        /// <summary>
        /// 普通立即回收方法
        /// </summary>
        /// <param name="gameObject">要回收的游戏对象</param>
        private void NormalDespawn(GameObject gameObject)
        {
            // 获取实例化的Id
            int id = gameObject.GetHashCode();
            
            // 从对象池映射中查找并移除该对象
            if (!_objectPools.Remove(id, out var pool)) 
            {
                Log.Error("");
                
                // 如果对象不在任何池中则直接返回
                return;
            }
            
            // 调用对象池的回收方法
            pool.Despawn(gameObject);
        }

        /// <summary>
        /// 延迟回收协程方法
        /// </summary>
        /// <param name="gameObject">要回收的游戏对象</param>
        /// <param name="delay">延迟时间(秒)</param>
        private async UniTaskVoid DelayDespawn(GameObject gameObject, float delay, 
            Action<GameObject> callBack = null, CancellationToken token = default)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
        
                // 检查对象是否仍然有效
                if (gameObject == null) 
                    return;
        
                NormalDespawn(gameObject);
                
                callBack?.Invoke(gameObject);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，正常处理
                Log.Info($"延迟回收任务已被取消: {gameObject.name}");
            }
            catch (Exception ex)
            {
                Log.Error($"延迟回收过程中发生错误: {ex.Message}");
            }
        }

        #endregion
        
        #region 判断clone出来的
        
        /// <summary>
        /// 检查给定的 GameObject 是否是克隆对象。
        /// </summary>
        /// <param name="clone">要检查的 GameObject 对象。</param>
        /// <returns>如果该 GameObject 是克隆对象，返回 true，否则返回 false。</returns>
        public bool IsClone(GameObject clone)
        {
            // 获取实例化的Id
            int id = clone.GetInstanceID();
            
            // 检查 _objectPools 中是否包含该 GameObject，若包含则说明是克隆对象
            return _objectPools.ContainsKey(id);
        }

        /// <summary>
        /// 检查给定的 Component 是否是克隆对象。
        /// </summary>
        /// <param name="clone">要检查的 Component 对象。</param>
        /// <returns>如果该 Component 对应的 GameObject 是克隆对象，返回 true，否则返回 false。</returns>
        public bool IsClone(Component clone)
        {
            // 调用另一个重载方法，传入 Component 所在的 GameObject 进行检查
            return IsClone(clone.gameObject);
        }

        #endregion
        
        #region 判断是否有对象池

        /// <summary>
        /// 检查是否存在指定名称的对象池
        /// </summary>
        /// <param name="prefabName">预制体名称</param>
        /// <returns>是否存在该对象池</returns>
        public bool HasPool(string prefabName)
        {
            if (!_poolConfigs.TryGetValue(prefabName, out var poolConfig))
                return false;

            if (!poolConfig.IsCreating)
                return false;
            
            // 检查字典中是否存在该键
            return _pools.ContainsKey(prefabName);
        }

        #endregion
        
        #region 获取对象池

        /// <summary>
        /// 根据 prefab 名字获取对应的对象池（池池名）。
        /// </summary>
        /// <param name="prefabName">对象池对应的 prefab 名字。</param>
        /// <returns>如果找到对应的对象池，则返回该池，否则返回 null。</returns>
        public GameObjectPool GetPoolByName(string prefabName)
        {
            // 尝试根据 prefab 名字从池集合中获取对象池
            if (TryGetPool(prefabName, out var pool))
            {
                // 如果找到了对应的池，则返回该池
                return pool;
            }
    
            // 如果没有找到池，则记录错误日志
            Log.Error($"名字 '{prefabName}' 未找到对应的池!");
    
            // 返回 null，表示未找到对应的池
            return null;
        }

        /// <summary>
        /// 根据克隆的 GameObject 获取对应的对象池。
        /// </summary>
        /// <param name="clone">要查找的克隆 GameObject。</param>
        /// <returns>如果找到了对应的对象池，则返回该池，否则返回 null。</returns>
        public GameObjectPool GetPoolByClone(GameObject clone)
        {
            // 获取实例化的Id
            int id = clone.GetInstanceID();
            
            // 尝试根据克隆的 GameObject 从对象池字典中获取对象池
            if (_objectPools.TryGetValue(id, out var pool))
            {
                // 如果找到了对应的池，则返回该池
                return pool;
            }
    
            // 如果没有找到池，则记录错误日志
            Log.Error($"克隆 '{clone}' 未找到对应的池!");
    
            // 返回 null，表示未找到对应的池
            return null;
        }

        #endregion
    }
}
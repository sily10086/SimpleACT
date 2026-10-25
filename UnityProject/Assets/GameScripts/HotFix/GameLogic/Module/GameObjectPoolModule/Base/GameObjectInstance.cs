using TEngine;
using UnityEngine;

namespace GameCommon.GameObjectPoolModule
{
    /// <summary>
    /// 游戏对象实例包装类
    /// 继承自ObjectBase，用于在对象池中包装Unity游戏对象
    /// </summary>
    public sealed class GameObjectInstance : ObjectBase
    {
        /// <summary>
        /// 被包装的Unity游戏对象
        /// </summary>
        private GameObject _gameObject;
        
        /// <summary>
        /// 标记是否已初始化
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// 获取被包装的游戏对象
        /// </summary>
        public GameObject GameObject => _gameObject;

        /// <summary>
        /// 创建GameObjectInstance实例的静态工厂方法
        /// </summary>
        /// <param name="name">对象名称</param>
        /// <param name="gameObject">要包装的游戏对象</param>
        /// <returns>创建好的GameObjectInstance实例</returns>
        public static GameObjectInstance Create(string name, GameObject gameObject)
        {
            // 从内存池中获取一个GameObjectInstance实例
            GameObjectInstance gameObjectInstance = MemoryPool.Acquire<GameObjectInstance>();
            
            // 初始化对象实例，设置名称
            gameObjectInstance.Initialize(name, gameObject);
            
            // 保存游戏对象引用
            gameObjectInstance._gameObject = gameObject;
            
            // 标记为已初始化
            gameObjectInstance._initialized = true;
            
            return gameObjectInstance;
        }
        
        /// <summary>
        /// 对象被生成时的回调方法
        /// 当对象从池中取出时调用
        /// </summary>
        protected override void OnSpawn()
        {
            // 检查是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("GameObject Pool Module is not initialized.");

                return;
            }
            
            // 激活游戏对象，使其在场景中可见和可交互
            _gameObject.SetActive(true);
        }

        /// <summary>
        /// 对象被回收时的回调方法
        /// 当对象放回池中时调用
        /// </summary>
        protected override void OnUnspawn()
        {
            // 检查是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("GameObject Pool Module is not initialized.");

                return;
            }
            
            // 禁用游戏对象，使其在场景中不可见，节省性能
            _gameObject.SetActive(false);
            
            // 重置游戏对象的位置到原点
            _gameObject.transform.position = Vector3.zero;
            
            // 重置游戏对象的旋转角度到默认值
            _gameObject.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 释放对象资源
        /// 当对象池清理或销毁时调用
        /// </summary>
        /// <param name="isShutdown">是否是系统关闭时的释放</param>
        protected override void Release(bool isShutdown)
        {
            // 检查是否已初始化
            if (!_initialized)
            {
                // 如果未初始化，记录错误日志
                Log.Error("GameObject Pool Module is not initialized.");

                return;
            }
            
            // 销毁被包装的游戏对象，释放资源
            Object.Destroy(_gameObject);
        }
    }
}
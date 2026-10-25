using UnityEngine;

namespace GameCommon.GameObjectPoolModule
{
    public sealed class PoolConfig
    {
        public string PoolName;
        
        public GameObject Prefab;
        
        public int Capacity;
        
        public float AutoReleaseInterval;
        
        public float ExpireTime;
        
        public int Priority;
        
        public bool IsCreating;
    }
}
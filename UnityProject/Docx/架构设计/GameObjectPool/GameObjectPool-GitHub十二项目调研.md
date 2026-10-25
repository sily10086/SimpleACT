# GameObjectPool：GitHub 十二项目调研

日期：2026-10-24。范围：Unity GameObject/Prefab 对象池、ScriptableObject 配置、Mono 可视化、生命周期与回收策略。

本报告供[当前分步文档](分步执行/00-总览与设计基线.md)参考；[旧总方案](GameObjectPool模块改造方案.md)保留为历史资料。以下 **十二个独立仓库**均读取了 README 和至少一个关键源码文件；Unity 官方源码、TEngine、UnityGameFramework 与 Zenject 仅作补充，不计入十二个。本文保留原调研日期与观察，不表示本次重新运行或重新核验了这些仓库。

## 调研方法与结论可信度

- 通过 GitHub 搜索实际仓库，再读取仓库原文与源码；没有将二手介绍当作项目能力证据。
- 以下“能力”是文档/源码观察，“采用与限制”是针对当前 Parkour 项目的设计判断。
- 没有安装、编译或运行这些库，也没有复测其性能宣传；不按星数或 README 的“高性能”字样认定适用于本项目。
- 不把这十二个都称作“商业验证成熟方案”：其中包括轻量工具与历史实现。它们是不同设计的对照样本，直接导入前仍需许可证、兼容性与维护评估。
- 每项记录本次读取的源码树快照 ID，**不是发布版本，也不是测试通过的 commit 声明**。链接提供项目 README 和实际关键源码。

## 对照总表

| 项目 | 最相关主题 | 主要取舍 |
|---|---|---|
| thefuntastic/unity-object-pool | 双字典与预热 | 借鉴索引，不复制单例体系 |
| prime31/RecyclerKit | Inspector 配置、硬上限、周期清理 | 拆开容量概念 |
| Kink3d/kPooling | Processor 生命周期分离 | 学职责分离，不增加泛型扩展框架 |
| IntoTheDev/Object-Pooling-for-Unity | 扩展方法、回调、外部销毁 | 统一首次与复用合同 |
| annulusgames/uPools | 轻量接口、同步/异步分层 | 不引入资源加载分支 |
| mackysoft/XPool | Unity 对象失效与专用组件池 | 状态安全优先，组件适配后置 |
| MeeXaSiK/NightPool | 预设、监控、延迟重复策略 | 最贴近配置和运行诊断 |
| GalvanicGames/unity-pooler | 场景生命周期、数量补足 | 不默认抢占仍借出的对象 |
| jonagill/ObjectPool | 单次租约与生命周期 | 解决旧异步任务误操作复用对象 |
| gilzoide/unity-prefab-pool | SO/Mono 展示、实例列表 | 最贴近本次可视化用途 |
| coryleach/UnityPooling | SO 管理器与懒创建 | SO 仅取配置入口，不承载运行态 |
| QFSW/MasterObjectPooler2 | SO 池、多池管理、组件缓存 | 借鉴编辑体验，控制缓存复杂度 |

## 1. thefuntastic/unity-object-pool

来源：[README](https://github.com/thefuntastic/unity-object-pool/blob/master/README.md)、[关键源码](https://github.com/thefuntastic/unity-object-pool/blob/master/Assets/Scripts/PoolManager.cs)。源码树：`2d20e73abe2728633549f19e5d1ecb5ec6def00b`。

- **已观察能力**：按 Prefab 建池，Prefab→池与实例→池双字典；可选预热和状态输出。
- **源码依据**：PoolManager.cs 中 warmPool、spawnObject、releaseObject、PrintStatus；借出前设置位置后激活。
- **本项目采用**：借鉴双向索引和薄入口，配置名与显示名分离。
- **不直接照搬的原因**：借出映射只覆盖在用实例；静态单例入口不符合本项目模块访问方向。不能照搬为全量实例监控。

## 2. prime31/RecyclerKit

来源：[README](https://github.com/prime31/RecyclerKit/blob/master/README.md)、[关键源码](https://github.com/prime31/RecyclerKit/blob/master/Assets/RecyclerKit/TrashManRecycleBin.cs)。源码树：`3addc042692cc64bdd7a86521a14ddab42b1cfc5`。

- **已观察能力**：Inspector 配置 Prefab、预分配、空池扩展、硬上限、保留数量、周期清理；支持延迟回收。
- **源码依据**：TrashManRecycleBin.cs 明确区分 instancesToPreallocate、hardLimit、instancesToMaintainInPool；clearBin 注明不处理借出对象。
- **本项目采用**：将预热、硬上限、清理容量作为不同概念，编辑器给出清晰提示。
- **不直接照搬的原因**：老式 Unity 写法及粒子 duration/lifetime 估算不应原样复制；需以现有 TEngine 清理语义为准。

## 3. Kink3d/kPooling

来源：[README](https://github.com/Kink3d/kPooling/blob/master/README.md)、[关键源码](https://github.com/Kink3d/kPooling/blob/master/Runtime/Processors/GameObjectProcessor.cs)。源码树：`d6d4a3c7935e423d0474d0f1aff7201c3de24d28`。

- **已观察能力**：通用泛型池配合 Processor，把对象创建、销毁、启停等行为委托给类型处理器。
- **源码依据**：GameObjectProcessor.cs 提供 CreateInstance、DestroyInstance、OnEnableInstance、OnDisableInstance，并统一处理父节点/变换。
- **本项目采用**：学习将 GameObject 生命周期集中到单池适配层，不散落在 Manager 的重载中。
- **不直接照搬的原因**：当前只需要 GameObject 池，没有必要引入一套 Processor 泛型扩展体系。GameObjectProcessor 中 Editor 使用 DestroyImmediate 的方式也不照搬到 Play Mode。

## 4. IntoTheDev/Object-Pooling-for-Unity

来源：[README](https://github.com/IntoTheDev/Object-Pooling-for-Unity/blob/master/README.md)、[关键源码](https://github.com/IntoTheDev/Object-Pooling-for-Unity/blob/master/Runtime/Pool.cs)。源码树：`0c29114a1a29267e095913a49069508e298d4e40`。

- **已观察能力**：Prefab 扩展方法 Populate/Reuse/Release，IPoolable 回调、池安装组件和实例索引。
- **源码依据**：Pool.cs 的 GetInstance 清理空闲栈中的无效实例；Clear 可选择销毁借出对象；创建与复用调用路径存在区别。
- **本项目采用**：学习简洁调用入口、回调缓存/状态重置以及被外部销毁的空闲对象处理。
- **不直接照搬的原因**：README 区分首次 Awake 与第二次起 OnReuse；本方案要求首次与复用相同回调合同。源码部分重载先激活后改 Transform，不直接照搬。

## 5. annulusgames/uPools

来源：[README](https://github.com/annulusgames/uPools/blob/main/README.md)、[关键源码](https://github.com/annulusgames/uPools/blob/main/Assets/uPools/Runtime/GameObjectPool.cs)。源码树：`e11ce9c12c65c12f3bc35ae2ef7cacde6826e503`。

- **已观察能力**：独立 GameObjectPool、共享池、生命周期回调、预热；还提供独立的 UniTask/Addressables 扩展。
- **源码依据**：GameObjectPool.cs 的 Rent/Return/Prewarm/Clear；专用类只有一个空闲栈，无全量实例字典。
- **本项目采用**：学习将同步复用与异步能力分开、显式 Dispose 和紧凑接口。
- **不直接照搬的原因**：本任务不需要它的资源加载扩展；所读 GameObjectPool.cs 的 Clear/Dispose 仅遍历空闲栈，不能据 README 概述推定会追踪和销毁全部借出实例。

## 6. mackysoft/XPool

来源：[README](https://github.com/mackysoft/XPool/blob/main/README.md)、[关键源码](https://github.com/mackysoft/XPool/blob/main/Assets/MackySoft/MackySoft.XPool/Runtime/Unity/ObjectModel/UnityObjectPoolBase.cs)。源码树：`2e2bcc2fb721d2a9ce427273cc7e9ff6cb52bb30`。

- **已观察能力**：区分纯 C# 与 Unity 对象池，支持 ComponentPool、粒子池和自定义池基类。
- **源码依据**：UnityObjectPoolBase.cs 的 GetPooledInstance 跳过外部销毁项，Return 在达到容量时释放实例，ReleaseInstances 支持 keep。
- **本项目采用**：学习 Unity 假 null 处理、组件专用回收作为可插拔业务适配，以及清空空闲实例时保留指定数量。
- **不直接照搬的原因**：集合池与泛型工厂池不在本任务范围；容量是回收保留容量，不应直接当作生成硬上限。粒子适配列为后续功能。

## 7. MeeXaSiK/NightPool

来源：[README](https://github.com/MeeXaSiK/NightPool/blob/main/README.md)、[关键源码](https://github.com/MeeXaSiK/NightPool/blob/main/Code/Requests/DespawnRequest.cs)。源码树：`fb0218774b66a8e3d99505c5eefda3ff8b41ba2d`。

- **已观察能力**：PoolsPreset 资产、单池配置、运行计数、容量溢出策略、延迟回收重复请求策略和回调接口。
- **源码依据**：README 的 Pools Preset、ReadOnly Fields；DespawnRequest 保存对象与时间；另读 NightPool.cs 的 DespawnWithDelay 与重复请求处理。
- **本项目采用**：重点借鉴可视化统计字段、预设配置、明确的重复延迟回收政策。
- **不直接照搬的原因**：不引入强抢占借出对象、SendMessage/BroadcastMessage 等分支；源码与 README 的 DEBUG 安全策略不能替代发布包必要的状态一致性检查。

## 8. GalvanicGames/unity-pooler

来源：[README](https://github.com/GalvanicGames/unity-pooler/blob/master/README.md)、[关键源码](https://github.com/GalvanicGames/unity-pooler/blob/master/Assets/UnityPooler/Scripts/GameObjectPool.cs)。源码树：`a6d5fdafb6196df6a1130113ff7515a0bb3156af`。

- **已观察能力**：Prefab 上挂 PoolableGameObject，提供预热、场景持久化、容量限制与借出对象循环复用。
- **源码依据**：README 定义 PopulatePool 与 AddToPool；GameObjectPool.cs 为 PoolableGameObject 提供入口转发及分帧预热。
- **本项目采用**：学习把场景生命周期行为写成明确政策；区分补到目标总量与追加数量。
- **不直接照搬的原因**：容量满时重用仍活跃对象可能改变游戏行为，本方案默认拒绝新借出。无需要求美术为所有 Prefab 手工挂池配置组件。

## 9. jonagill/ObjectPool

来源：[README](https://github.com/jonagill/ObjectPool/blob/HEAD/README.md)、[关键源码](https://github.com/jonagill/ObjectPool/blob/HEAD/Packages/com.jonagill.objectpool/Runtime/Pooling/PooledInstance.cs)。源码树：`05302400970d15e98f53fe344967cecf8423e110`。

- **已观察能力**：PrefabPoolCollection 负责生命周期，PooledInstance 表示单次借出有效期，inactive 容器与父节点销毁前通知。
- **源码依据**：PooledInstance.cs 的 IsValid/MarkInvalid；README 描述父节点销毁通知必须提前、OnDestroy 中操作已太晚。
- **本项目采用**：重点借鉴单次租约有效性，解决旧延时任务操作复用对象；采用 inactive 创建与显式池生命周期。
- **不直接照搬的原因**：其 PooledInstance 为引用类型包装；本方案先在内部记录版本，不要求每次 Spawn 分配公开包装。父节点销毁后的补救不能代替提前归还。

## 10. gilzoide/unity-prefab-pool

来源：[README](https://github.com/gilzoide/unity-prefab-pool/blob/HEAD/README.md)、[关键源码](https://github.com/gilzoide/unity-prefab-pool/blob/HEAD/Editor/APrefabPoolEditor.cs)。源码树：`2bfc1a896ffd9712150ebf1e99120d6c9eab2276`。

- **已观察能力**：同一池支持普通可序列化类、Mono、SO 形式；分帧预热；Inspector 分组展示借出和空闲实例。
- **源码依据**：APrefabPoolEditor.cs 展示 CountAll/CountActive/CountInactive、折叠 ObjectField 列表、播放状态判断；本方案进一步改为可见面板限频刷新。
- **本项目采用**：最贴近可视化目标：只读 ObjectField 实例列表、运行期调试面板、按状态分组。借鉴分帧预热预算。
- **不直接照搬的原因**：不采用资产 OnEnable/OnDisable 自动拥有运行池的模式；本项目 SO 只保存定义，Mono 只观察。Addressables 分支不引入。

## 11. coryleach/UnityPooling

来源：[README](https://github.com/coryleach/UnityPooling/blob/HEAD/README.md)、[关键源码](https://github.com/coryleach/UnityPooling/blob/HEAD/Runtime/GameObjectPoolManager.cs)。源码树：`2b4b756a7f9e9b4704b26c5549f8e3b9f16de006`。

- **已观察能力**：GameObjectPoolManager 本身是 SO，用 Prefab→池字典懒创建，OnEnable 清除跨运行残留。
- **源码依据**：GameObjectPoolManager.cs 的 CreateAssetMenu、poolDictionary、OnEnable、GetPool。
- **本项目采用**：证明 SO 能提供拖拽配置入口，借鉴按 Prefab 检索的便利性。
- **不直接照搬的原因**：不采用 SO 同时承载配置与可变运行字典，避免运行状态污染资产及多会话生命周期歧义。其 README 很简略，本项结论主要来自源码。

## 12. QFSW/MasterObjectPooler2

来源：[README](https://github.com/QFSW/MasterObjectPooler2/blob/master/README.md)、[关键源码](https://github.com/QFSW/MasterObjectPooler2/blob/master/Source/ObjectPool.cs)。源码树：`5a9129f1f1a6a3c0031573ac9b0933ed64de811f`。

- **已观察能力**：ObjectPool 为 SO；有独立池名、Prefab、默认量、最大保留量、多池管理器、组件查询缓存和自定义 Inspector。
- **源码依据**：ObjectPool.cs 配置字段、_pooledObjects、_aliveObjects、_componentCache，以及初始化和借出路径；README 的 AutoPool/IPoolable。
- **本项目采用**：重点借鉴 SO 配置体验、字符串业务入口、借出/空闲分离和运行期控制面板。
- **不直接照搬的原因**：SO 内直接存运行实例的方式不采用；组件缓存会增加内存及失效维护成本，第一阶段只缓存回调列表，不无差别缓存所有 T。

## 跨项目综合后的设计选择

| 最终设计 | 主要依据 | 项目化调整 |
|---|---|---|
| Settings → GameObjectPoolSetting → Prefab 直接引用 | 本地 AudioSetting 模式；MOP2、Prefab Pool、UnityPooling | 配置与运行态分离，不使用 IResourceModule |
| Mono 只用于可视化 | Prefab Pool 的 Debug panel；NightPool 的运行字段 | 删除 Monitor 不影响运行；查看未初始化模块不创建它 |
| 保留 TEngine 通用池 | 本地已有过期、锁定、优先级与 Debugger | 不引入第二套泛型对象池；新增 GameObject 语义层 |
| 首次/复用走相同顺序 | 多个库存在激活时机和首次回调差异 | inactive 创建、先设置状态/变换再激活，明确回调合同 |
| 租约版本保护延迟回收 | ObjectPool 的 PooledInstance；NightPool 的重复延时政策 | 内部使用世代/租约/请求版本；旧计划不作用于新借出 |
| 容量、预热与硬上限分开 | RecyclerKit、MOP2、XPool、NightPool | Capacity 保持 TEngine 语义；上限默认无限，不抢占活跃对象 |
| 分帧预热到目标总量 | Prefab Pool、unity-pooler | UniTask 主线程分帧，不使用 Coroutine，不把循环借还当预热 |
| 完整实例清账与可视化 | MOP2、IntoTheDev、Prefab Pool | 统一维护全部实例与借出状态；不能只跟踪空闲或借出一侧 |

## 补充资料（不计入十二项）

- [Unity 2022.3 ObjectPool 源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Runtime/Export/ObjectPool/ObjectPools.cs)：Get 在无空闲对象时创建；maxSize 在回收时限制空闲保留量；不能将其当作并发实例上限。
- [UnityGameFramework ObjectPoolComponent](https://github.com/EllanJiang/UnityGameFramework/blob/master/Scripts/Runtime/ObjectPool/ObjectPoolComponent.cs) 和 [Inspector](https://github.com/EllanJiang/UnityGameFramework/blob/master/Scripts/Editor/Inspector/ObjectPoolComponentInspector.cs)：参考运行数据展开、清理命令和 CSV 导出；不因此推导本项目的 Mono 必须驱动模块。
- [Zenject Memory Pools](https://github.com/modesttree/Zenject/blob/master/Documentation/MemoryPools.md)：参考预热/重初始化/池监控思路，不引入 DI 容器。
- [TEngine 上游](https://github.com/Alex-Rachel/TEngine)：只作框架背景，本地 ModuleSystem、Settings、ObjectPoolModule 与 Inspector 代码才是本次集成的实际依据。

## 暂不采纳的扩展

自动抢占活跃对象、多种资源加载器、每种组件的通用复位器、反射式生命周期广播、多层泛型 Processor、全量组件缓存、独立监控窗口、自动生成大量业务示例，均不作为第一阶段默认工作。后续可根据实际场景再单独确认。


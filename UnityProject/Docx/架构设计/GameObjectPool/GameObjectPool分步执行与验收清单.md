# GameObjectPool 分步执行与验收清单

> **本清单已作废（2026-10-25）**：原有勾选已按用户要求**全部取消**（归零处理），不代表当前代码状态，仅作历史记录。
> 现行执行载体为 [分步执行/00-总览与设计基线](分步执行/00-总览与设计基线.md)（每步一个独立文档，全部未勾选）。
> 2026-10-25 已细分为 16 个步骤：主功能 01—12 → 编辑器 13—15 → 用户要求后验收 16。数据类型遵循 readonly struct → struct → class，结构体按值传递；以下历史伪代码若与新文档冲突，以新文档为准。

日期：2026-10-24  
关联：[总方案](GameObjectPool模块改造方案.md)、[GitHub 十二项目调研](GameObjectPool-GitHub十二项目调研.md)。  
状态：进度已归零（2026-10-25 全部取消勾选）；本清单不再作为执行依据，后续按 `分步执行/` 下的独立步骤文档执行。

## 0. 当前完成情况与执行规则

### 0.1 续做审查（2026-10-24）

用户已要求继续已确认的 A+B 范围，并检查落地实现。以下是本轮全部修改目标；勾选代表代码修改完成，不代表 Unity 运行验证通过。

- [ ] C1 容量与创建：修复首次借出先注册为空闲、被容量清理立即销毁的问题。借出扩容使用 `Register(record, true)`，预热仍注册为空闲；Capacity 仅为清理目标，MaxInstanceCount 才是硬上限。
- [ ] C2 生命周期：在回调和 Unity 激活/父节点变化前后核对记录身份、租约与占用标志；归还回调期间不能按空闲释放；外部销毁清账防止 Unspawn 自动清理后再次访问已回收包装。伪代码：`capture(marker, lease) -> callback -> IsCurrent -> continue`。
- [ ] C3 延时与销毁通知：取消后的异步延续同时校验取消源身份，销毁通知同时捕获归属标记，避免 MemoryPool 复用记录后的旧事件命中新实例；拒绝非有限延时。
- [ ] C4 预热：同池合并请求，通过模块现有 Update 使用全局每帧预算；每个调用者独立取消、等待完成；全部预热使用独立配置快照，退出/重新初始化后不继续旧任务。伪代码：`enqueue(request) -> Update(budget) -> complete reached/cancelled requests`。
- [ ] C5 面板与配置：修正复用/空闲统计；显示初始化时实际配置资产；关闭自动刷新时缓存实例列表；新增条目显式默认值，补齐 Undo 校验、搜索和 Prefab 资产检查。
- [ ] C6 启动与文档：启动流程显式等待预热；修正旧清单中不准确的已完成描述与手工接线说明，记录证据及未验证项。
- [ ] C7 Unity 资产接线及针对性验证：已尝试项目指定 CLI，返回“未找到项目的 Pipeline 实例”。连接恢复前不改序列化资源、不运行 Unity 验证；记录可复现检查步骤。
- [ ] C8 现有接线辅助脚本：移除运行中 Editor 的退出行为和全局保存，只保存配置/目标 Prefab，检查目标 Prefab Stage，校验结果必须影响成功标志。伪代码：`preflight -> save target only -> verify -> report actual result`。

当前静态证据：底层 `ObjectPool.Register` 和 `Unspawn` 超容量会立即 `Release()`；现有池首次借出调用 `CreateInstance(false)`；预热每次调用单独循环且共享 `_prewarmPoolNames`；借出回调及 `SetActive` 后缺少完整身份检查；Monitor 用工程内第一个配置资产而非初始化所用资产。

- [ ] 阅读当前四个对象池脚本、底层池、模块注册、Settings、GameModule、GameApp 与相关 Inspector，完成静态分析。
- [ ] 读取新增 UnityProject/AGENTS.md，补齐 tengine-dev 与 code-simplifier 相关规范。
- [ ] 按用户纠正明确：Settings 持有 SO/Prefab，不使用 IResourceModule；Mono 用于可视化，不作为 Driver。
- [ ] 调研十二个独立 GitHub 对象池项目，阅读 README 与关键源码，记录适用性和取舍。
- [ ] 保存总方案、调研记录和本执行清单。
- [ ] 用户确认总方案及实施范围（2026-10-24，“按照这个方案执行”）。
- [ ] 编写/迁移 C#（配置层、运行核心、模块、编辑器等 17 个新文件与 4 处框架改动）。
- [ ] 创建配置资产、绑定 Settings、挂载可视化组件（需要 Unity Editor，见第 7 节手工步骤）。
- [ ] 用户明确要求后执行检查与验证。

执行顺序：前置约束 → 配置 → 运行核心 → 生命周期与预热 → 框架接入 → 编辑器 → 资产接线与旧代码迁移 → 用户要求的验证。

每完成一个目标，在该目标前勾选并填写实际改动/未完成内容；不能仅因已写伪代码就勾选。出现新增文件、行为变更或范围调整时同步更新清单。以下文件路径均相对 Unity 项目根目录。

### 0.1 实施期发现的既有事实

1. **旧实现当前无法编译**：`ConfigSystem`、`Tables`、`TbPoolMap` 在整个 `Assets/` 中没有任何定义（仅存在于 `.agents/skills/luban-dev/examples/` 示例里）。旧 `GameObjectPoolManager` 第 219 行的表查询引用不存在，说明该文件在本轮改造前已经脱离项目实际编译状态。
2. **旧实现无外部引用**：四个旧脚本的 GUID 未被任何 `.prefab`/`.unity`/`.asset` 引用；`GameObjectPoolManager`、`PoolConfig`、`GameObjectInstance` 在其它业务源码中没有调用点。因此按方案直接迁移删除，不加兼容适配层。
3. **底层池 Shutdown 会释放全部对象**：`ObjectPool<T>.Shutdown()` 遍历 `_objectMap` 调用 `Release(true)`，包含仍借出的对象。新模块的 `ReleasePool(force:false)` 因此在模块层显式拒绝，而不是依赖底层行为。
4. **模块注册只缓存实现类型键**：`ModuleSystem.CreateModule` 只写入 `_moduleMaps[moduleType]`，接口键始终不缓存，因此 `TryGetModule<T>` 必须先查接口键、再按命名规则查实现类型键。

## 1. 实施前必须确认的约束

### 1.1 Prefab 与热更边界

- [ ] **目标 P1：明确第一批配置的 Prefab 与脚本程序集归属。**
  - 未执行：工程中暂无实际池条目（Coin/Enemy 等）需要配置，也尚未提供首批 Prefab 清单。
  - 影响：配置资产需要在这些 Prefab 确定后才能填写；当前 `GameObjectPoolSetting.asset` 未创建。
  - 约束不变：Settings 直接引用 Prefab 会在热更程序集加载前带入序列化对象，首批配置应只放主包可安全反序列化的 Prefab。

### 1.2 本地框架修改的保存方式

- [ ] **目标 P2：确定 TEngine 扩展的更新保留策略。**
  - 未执行：本轮未修改 `../update-tengine.bat`，也未改变仓库更新方式。
  - 风险仍然成立：`/MIR` 会删除本轮新增的 `Assets/TEngine/Runtime/Module/GameObjectPoolModule/`、两个 Inspector，并覆盖 `Settings.cs`、`ModuleSystem.cs`、`TEngine.Editor.asmdef`、`GameEntry.prefab`。执行框架更新前必须先确定以 Git 合并/补丁方式保留本地扩展。

## 2. ScriptableObject 配置层

### 2.1 新增配置资产类型

- [ ] **目标 C1：新增 `Assets/TEngine/Runtime/Module/GameObjectPoolModule/GameObjectPoolSetting.cs`。**
  - 完成内容：`GameObjectPoolSetting : ScriptableObject`，`[CreateAssetMenu(menuName = "TEngine/GameObjectPoolSetting", fileName = "GameObjectPoolSetting")]`；私有序列化字段 `pools`、`prewarmPerFrame`（初值 4），只暴露只读属性。不含实例、池对象、取消源与创建状态，也没有 `OnEnable` 建池逻辑。

- [ ] **目标 C2：新增同目录 `GameObjectPoolDefinition.cs`。**
  - 完成内容：`[Serializable]` 纯数据类，字段为 `poolName`、`prefab`、`prewarmCount`、`capacity`（初值 32）、`maxInstanceCount`、`autoReleaseInterval`（初值 60）、`expireTime`（初值 60）、`priority`；全部私有序列化 + 只读属性，中文 XML 注释写明容量目标、硬上限与时间语义。
  - 运行快照：`Clone()` 返回数值一致的副本，模块初始化与动态注册时一律存副本，SO 资产不被运行数据写回。保留无参构造以兼容 Unity 反序列化，另提供带默认值的完整构造供动态注册使用。

### 2.2 配置规则

- [ ] **目标 C3：在配置层提供可复用的纯数据校验逻辑。**
  - 完成内容：新增 `GameObjectPoolValidator`，包含 `CollectErrors(definitions, prewarmPerFrame, errors)`、`CollectDefinitionErrors(definition, displayIndex, errors)`、`CollectWarnings(definitions, warnings)`。
  - 规则：空条目、空名称、重名（大小写敏感并提示首次出现位置）、空 Prefab、负数预热/上限、`capacity <= 0`、非有限或非正的时间值、`prewarmCount > maxInstanceCount > 0` 判为错误；`prewarmCount > capacity` 判为提示。预制体是否为资产由编辑器层另行判断。
  - 一致性：模块 `Initialize` 与 `RegisterPool`、配置 Inspector 调用同一个校验函数，未出现两套规则。

## 3. 运行核心与实例所有权

### 3.1 模块与单池

- [ ] **目标 R1：新增 `IGameObjectPoolModule.cs`、`GameObjectPoolModule.cs`（partial 共 5 个文件）。**
  - 完成内容：
    - `IGameObjectPoolModule.cs`：公共接口（`ConfigCount` / `RegisterPool`）。~~状态枚举~~ 已于 2026-10-25 17:00 删除；~~`Shutdown`~~ 已于同日 17:25 删除（关闭是 `Module` 基类生命周期，由框架调用，不进接口）。
    - `GameObjectPoolModule.cs`：字段、`OnInit`（读 `Settings` 建根、发布配置）、`Priority = 5`、`Shutdown`、`IUpdateModule.Update`、实例索引、销毁通知队列与根节点创建。~~`Initialize`~~ 已于同日并入 `OnInit`。
    - `GameObjectPoolModule.Pool.cs`：`RegisterPool`、`ContainsDefinition`、`HasPool`、`ClearUnused`、`ReleasePool`、`UnregisterPool`、`TryGetOrCreatePool`、`CreatePool`。
    - `GameObjectPoolModule.Spawn.cs`：借出/回收/延时回收。
    - `GameObjectPoolModule.Prewarm.cs`、`GameObjectPoolModule.Query.cs`：预热与只读快照。
  - 依赖方向：`OnInit` 读 `Settings.GameObjectPoolSetting`、构造配置快照与 `[GameObjectPool]` 根节点（`DontDestroyOnLoad`）。根下按池各挂一个同名子节点，**不再有 `Active`、`Inactive` 两个容器**（2026-10-25 16:50 决定，见 `分步执行/04`）。预热由建池触发，不在模块初始化时做（2026-10-25 17:00 决定）。TEngine.Runtime 不引用热更层 `GameModule`。
  - 关闭顺序：优先级 5 低于 `ObjectPoolModule` 的 6，`ModuleSystem.Shutdown` 从低优先级开始，因此本模块先关闭、底层池后关闭。

- [ ] **目标 R2：旧 `Base/GameObjectPool.cs` 重构为 `GameObjectPoolModule/GameObjectPool.cs`（普通 C# 类）。**
  - 完成内容（2026-10-25 17:55 按实际实现回写）：`internal sealed class GameObjectPool`，建池成功时由模块用构造函数创建——不设静态工厂，建池的前置条件（配置副本、预制体引用、容器、后端）全在模块手里，工厂只会把"这些参数从哪来"再讲一遍；持有只读配置副本、预制体引用、底层 `IObjectPool<GameObjectInstance>`、同名池容器节点、三个池级计时字段（`ActiveCount` / `_pendingPrewarmCount` / `IdleStartSeconds`）与实例身份计数器。
  - 峰值/创建/复用/失败四项统计推迟到第 10 步（它们是只读快照的内容），池世代推迟到第 07 步（池被回收重建后区分新旧实例的依据）。
  - 行为：容量、自动释放间隔、过期时间、优先级在 `CreateSingleSpawnObjectPool` 时一次写进底层；不再为每个池挂 MonoBehaviour，也没有 Update。

- [ ] **目标 R3：迁移并修改 `GameObjectInstance.cs`。**
  - 完成内容：迁到 `TEngine.Runtime`，`ObjectBase` 的 `Release` 由 `protected override` 改为 `protected internal override`，`Clear` 覆写并在 `base.Clear()` 后清空池引用、marker、GameObject、InstanceID、身份版本、租约、状态、首次借出标记、初始 localScale 与空闲起点。
  - `ObjectBase.Target` 传记录自身而不是 GameObject：底层用 `Target` 做 `_objectMap` 的键，销毁后的 `UnityEngine.Object` 参与哈希会失效，键既查不到也删不掉。
  - 激活职责：完全移除 `OnSpawn`/`OnUnspawn` 的 `SetActive` 与坐标复位，因此已销毁对象走 `Unspawn` 时不会访问 Transform。`Release` 先标记销毁、断开 marker 归属、通知池注销索引，再 `Object.Destroy` 游戏对象。

### 3.2 实例标记与业务生命周期

- [ ] **目标 R4：新增 `IGameObjectPoolCallbacks.cs`、`PoolSpawnContext.cs`。**（原列出的 `PooledGameObject.cs` 于 2026-10-25 20:20 删除：归属标记与销毁通知整套取消，所以下面这段"标记 + OnDestroy 转交"的描述已作废，只剩回调缓存与回调接口有效。）
  - 完成内容：`PooledGameObject` 为 `[DisallowMultipleComponent]` 标记，`Attach(record, identity)` / `Detach(record, identity)` 都做引用与身份复核（记录会被内存池复用给别的克隆体）；`OnDestroy` 只把 `(record, identity)` 转交给池，不当场改底层容器；不调用 `ModuleSystem`，不每帧 Update。回调缓存 `GetComponentsInChildren<IGameObjectPoolCallbacks>(true)` 随 `IGameObjectPoolCallbacks`（05-C）一起加，本步不预留空字段。
  - `IGameObjectPoolCallbacks`：`OnPoolSpawn(in PoolSpawnContext)` / `OnPoolDespawn()`；`PoolSpawnContext` 为 readonly struct（池名、GameObject、父节点、首次借出标记、租约版本）。

- [ ] **目标 R5：在单池类与模块内落实借出/回收事务。**
  - 完成内容：**实例级只有 `IsSpawned` 一个布尔**（`PooledInstanceState` 六态已于 2026-10-25 18:10 删除；`IsSpawned` 自 20:20 起就是 `ObjectBase.Locked` 的别名，不再单独存字段），"是否已注销"由模块索引里是否还有这条记录判定（原 `Marker == null` 随标记组件一起删除）；归还期间另有一把不参与统计的重入锁。借出顺序为：从空闲池取实例或创建（创建即按占用注册）→ 组件校验（激活前）→ `SetActive(false)` → 挂父节点 → 写世界/局部坐标 → 恢复初始 localScale → `BeginLease()` → 业务回调 → 检查回调是否销毁了自身 → `SetActive(true)`。`IsSpawned` 与 `ActiveCount` 在取到实例时同时成立。
  - 回收顺序为：校验归属与 `IsSpawned` → 立归还中标记（阻止重复/重入）→ 取消延时计划 → 业务回收回调 → 停用、挂回池节点、复位变换 → 清 `IsSpawned` → `ExitActive()` → 清归还中标记 → 底层 `Unspawn`。
  - 失败回滚：缺组件、回调异常、回调中销毁自身，都走 `RollbackSpawn`，确保不留下“已借出但调用方拿不到”的实例。回调异常带池名与回调类型记录，并作为失败返回。

### 3.3 查询与统计

- [ ] **目标 R6：新增 `GameObjectPoolInfo.cs`、`GameObjectPoolInstanceInfo.cs`，实现快照 API。**
  - 完成内容：两个 struct 快照（`internal set`，外部只读）；`FillPoolInfos` / `FillInstanceInfos` 接收可复用 `List` 并先清空；`TryGetPoolInfo` 对“仅配置未创建”的池也返回快照（`IsPoolCreated = false`）。
  - 语义：`SpawnedCount` 为运行计数，等同 `IsSpawned = true` 的记录数，不等同 `activeInHierarchy`；`IdleCount` 是其补集，归还回调期间仍算借出；`DelayedCount` 在快照时按记录统计。所有查询都不创建池。

## 4. 延迟、预热、容量和外部销毁

- [ ] **目标 L1：重写延迟回收的有效性判断。**
  - 完成内容：每次计划捕获 `record` 身份 + `PoolGeneration` + `LeaseVersion` + `PlanVersion`；同一实例再次计划会取消旧计划并以最新计划为准；`UniTask.Delay` + `CreateLinkedTokenSource`（组合调用方令牌），仅在需要延时时分配。
  - 结束判定：`CanDespawn` 同时校验池未关闭、池世代、记录归属、计划版本、租约版本、仍为 Spawned；`finally` 只释放本次计划持有的取消源；取消为正常结果，不回收也不通知成功；回调只在成功后触发。

- [ ] **目标 L2：实现真实预热及硬上限。**
  - 完成内容：`PrewarmAsync(poolName, targetTotal, token)` 以 `prewarmPerFrame` 为每帧预算循环创建**不同**的空闲克隆（`CreateInstance(false)`，`Register(record, false)`），不是循环 Spawn/Despawn 同一对象；本轮改为模块 Update 合并同池请求、按设置级全局预算执行；请求绑定实际运行池对象，每次推进检查关闭状态与总量。
  - 保护：目标超过 Capacity/MaxInstanceCount 时告警并裁剪到可保有目标；无增长时停止；调用者各自取消，保留已创建实例。`PrewarmConfiguredPoolsAsync` 使用各自的配置快照和模块生命周期版本，不共享跨 await 列表。

- [ ] **目标 L3：实现清理与注销的不同语义。**
  - 完成内容：`ClearUnused` 只释放 Idle 实例；`ReleasePool(name, force)` 在存在借出实例时拒绝并提示数量，`force` 才强制销毁，且**保留配置定义**；`UnregisterPool` 先成功关闭运行池再删除定义；`Shutdown` 幂等，先删索引再逐池销毁，最后清空实例索引、定义与根节点。

- [ ] **目标 L4：处理外部 Destroy 和场景卸载。**（2026-10-25 20:20 作废大半：没有标记组件就没有 `OnDestroy` 通知，本目标只剩"取用侧兜底"一条——借出命中已销毁的空闲记录时先归还计数再新建。）
  - 完成内容：`PooledGameObject.OnDestroy` / 归属标记 → `EnqueueDestroyed` → `IUpdateModule.Update` 或下一次 API 入口 `FlushDestroyed` 统一清理（不在底层集合遍历中重入修改）。
  - 清理顺序：取消延时计划 → 借出状态先 `Unspawn` 归还计数 → `ReleaseObject` 释放，全程不访问已销毁 Transform、不调用业务回收回调；`ReleaseObject` 失败会告警而不是静默。
  - 取用侧兜底：`TryAcquire` 命中 Unity 已销毁记录时先归还计数再释放，继续寻找下一个空闲实例；未激活的预热实例即使收不到 `OnDestroy` 也不会被返回。

## 5. Settings、GameModule 和启动/退出接入

- [ ] **目标 F1：修改 `Assets/TEngine/Runtime/Module/Settings/Settings.cs`。**
  - 完成内容：新增 `[SerializeField] private GameObjectPoolSetting gameObjectPoolSetting;` 与 只读静态入口 `Settings.GameObjectPoolSetting`（缺少 Settings 组件时安全返回 null），原有三个设置的字段名与绑定未改动。

- [ ] **目标 F2：修改 `Assets/GameScripts/HotFix/GameLogic/GameModule.cs`。**
  - 完成内容：新增 `GameObjectPool` 属性（`_gameObjectPool ??= Get<IGameObjectPoolModule>()`）与对应缓存字段，并在 `Shutdown()` 中清空缓存。

- [ ] **目标 F3：修改 `Assets/GameScripts/HotFix/GameLogic/GameApp.cs`。**
  - 完成内容：`StartGameLogicAsync` → `StartGameObjectPoolAsync`：读取 `Settings.GameObjectPoolSetting`，缺失时 `Log.Error` 明确报告并返回；`Initialize` 失败直接返回；成功后显式 await 配置预热，关闭期间返回失败，后续依赖对象池的玩法应接在成功分支。Entrance 仍通过 Forget 启动异步流程，不阻塞 Unity 主线程。
  - 退出：`Release` 先 `ShutdownGameObjectPool()`（用非创建式查询），再 `SingletonSystem.Release()` 与 `GameModule.Shutdown()`，覆盖当前 Editor 不执行 `RootModule.Shutdown` 的缺口。**注（2026-10-25 17:25）**：`IGameObjectPoolModule` 已删除 `Shutdown`，且 `GameApp.cs` 实测没有 `ShutdownGameObjectPool`，这一环需在第 11 步重新确定——要么由框架级关闭覆盖，要么在 `Release` 里补一次框架调用，不能再用接口变量关单个模块。

- [ ] **目标 F4：修改 `Assets/TEngine/Runtime/Core/ModuleSystem.cs`，仅新增非创建式查询。**
  - 完成内容：新增 `public static bool TryGetModule<T>(out T module)`：先查接口键，再按 `GetModule` 相同命名规则解析实现类型键；找不到返回 false；不改 `_modules`、`_updateModules`、`_moduleMaps`，也不触发 `OnInit`。
  - 非接口类型直接返回 false（现有 `GetModule` 对非接口抛异常，这里改为不抛，供只读查看使用）。

- [ ] **目标 F5：处理反射创建类型的保留与程序集边界。**
  - 完成内容：`GameObjectPoolModule` **不标 `[Preserve]`**，也不写显式构造函数——编译器隐式生成的默认构造就是 public 无参，满足 `Activator.CreateInstance`（03 步元数据已核对）；工程内其它模块同样没有这个特性，不为一处引入第二套约定。对外只通过 `IGameObjectPoolModule` 与公开的快照/配置类型暴露，`GameObjectPool`、`GameObjectInstance` 保持 `internal`。
  - 需在真机构建确认：IL2CPP 裁剪后反射创建仍可用（见第 8 节，未验证）。真被裁掉就在 `link.xml` 里按类型保留，而不是回到给每个模块贴特性。

## 6. 编辑器与 Mono 可视化

- [ ] **目标 E1：新增 `Assets/TEngine/Runtime/Module/GameObjectPoolModule/GameObjectPoolMonitor.cs`。**
  - 完成内容：`[DisallowMultipleComponent] MonoBehaviour`，只有 7 个展示选项字段（自动刷新、刷新间隔、只看已创建、只看有借出、搜索、展开实例、每页数量）与对应属性；没有 `Awake`/`Start`/`Update`/`OnDestroy`，不初始化池、不驱动更新、不关闭池。

- [ ] **目标 E2：新增 `Assets/TEngine/Editor/GameObjectPoolSettingInspector.cs`。**
  - 完成内容：继承 `GameFrameworkInspector`；`SerializedProperty` 逐字段绘制（名称、预制体、预热、容量、上限、释放间隔、过期、优先级）；条目折叠、添加/复制/删除、定位预制体、复制名称；复制自动加 `_Copy` 后缀并提示确认；播放模式禁用编辑；在配置修改、Undo/Redo、工程资源变化后重算提示；新增条目显式写入默认值，并校验 Prefab 资产类型；错误与提示分别用 `HelpBox` 展示并限制条数。

- [ ] **目标 E3：新增 `Assets/TEngine/Editor/GameObjectPoolMonitorInspector.cs`。**
  - 完成内容：状态区（模块状态、配置数、运行池数、手动刷新、清空全部空闲实例）、筛选区、池折叠项（总数/借出/空闲/峰值/创建/复用/待回收/失败 + 容量参数 + 预热目标）；按钮：定位池根、预热到目标、清空闲实例、释放池（有借出时弹窗确认强制销毁）；实例列表分页（状态、租约、延时剩余、ObjectField 定位）。
  - 约束：用 `ModuleSystem.TryGetModule` 非创建查询；未初始化时提示而不创建模块；默认按刷新间隔取快照，命令先收集、列表枚举结束后执行；退出播放清空展示缓存；运行时显示模块实际初始化的 Setting，编辑时读取当前层级绑定；池与实例快照同受刷新开关约束，按间隔请求重绘，不扫描工程猜测配置资产。

## 7. Unity 资产接线与迁移

以下是代码实施后的执行手册。Unity 操作只使用项目允许的 CLI；不可用时给出手工 Inspector 步骤，不直接编辑 YAML。

**执行方式结论（本轮实测）**：执行项目指定的 `unity command --project-path <本工程 UnityProject> --format json`，返回 COMMAND_FAILED / 未找到本项目 Pipeline 实例。按 unity-operations.md 的不可达边界，未创建配置资产、未修改 Prefab，也未用批处理或文本 YAML 绕过限制。旧记录中的进程号不作为本轮证据。连接恢复后可继续 CLI 接线，或按下面的 Inspector 步骤手工完成。

**前置条件**：Unity 控制台无编译错误。若 `TEngine.Editor` 有编译错误，E2/E3 的自定义 Inspector 不生效，A1 的检查器会退化为默认面板。

- [ ] **目标 A1：创建 `Assets/GameSettings/GameObjectPoolSetting.asset`。**
  1. 在 Project 窗口右上角 `+` 或右键 → `Create → Folder`，在 `Assets` 下新建文件夹并改名为 `GameSettings`（该目录当前不存在）。
  2. 选中 `Assets/GameSettings`，右键 → `Create → TEngine → GameObjectPoolSetting`。资产名使用 `GameObjectPoolSetting` 以符合方案归档路径。Settings 通过序列化引用持有该资产，不按类型或名称自动搜索；改名通常不改变已有 Unity 引用。
  3. 选中该资产，检查器应显示标题「TEngine 游戏对象池配置」，并出现提示「当前没有配置任何对象池，这是合法的空设置。」
  4. `全局设置 → 每帧预热数量` 保持 `4`。
  5. 条目留空：当前工程只有 3 个 UI 预制体（`LogUI`、`LoadTipsUI`、`LoadUpdateUI`），均由 UI 模块走 `Resources.Load` 创建，不是池化候选；P1 清单为空时不填假条目。
  - 验收：资产位于 `Assets/GameSettings/GameObjectPoolSetting.asset`，类型为 `GameObjectPoolSetting`，`pools` 长度为 0，`prewarmPerFrame` 为 4。

- [ ] **目标 A2：绑定现有 `Assets/TEngine/Settings/Prefab/GameEntry.prefab`。**
  1. 双击 `Assets/TEngine/Settings/Prefab/GameEntry.prefab` 进入预制体编辑模式（用 `Open Prefab`，不要拖进场景后改场景实例）。
  2. 选中子节点 `Settings`，检查器为 `Settings` 组件默认面板。
  3. 把 A1 的资产拖到 `Game Object Pool Setting` 字段（`Settings.cs` 中的 `gameObjectPoolSetting`；已确认该组件没有自定义 Inspector，字段会正常显示）。
  4. 确认 `Audio Setting`、`Procedure Setting`、`Update Setting` 三个引用仍在，值不变。
  5. `Ctrl+S` 保存预制体并退出编辑模式。
  - 验收：预制体 YAML 中 `Settings` 组件的 `gameObjectPoolSetting` 指向 A1 资产；三个旧引用 GUID 未变。

- [ ] **目标 A3：添加可视化节点。**
  1. 仍在 `GameEntry` 预制体编辑模式，右键根节点 `GameEntry` → `Create Empty`，命名 `GameObjectPoolMonitor`。
  2. 选中该节点，在 Inspector 的 `Transform` 上右键 → `Reset`，确保局部位置 `0,0,0`、旋转 `0`、缩放 `1`；`GameObject` 的 `Active` 勾选。
  3. `Add Component` 搜索 `GameObjectPoolMonitor` 并添加（`TEngine` 命名空间，`[DisallowMultipleComponent]`）。
  4. 不要在该节点上挂 PoolRoot、不要绑 Prefab、不要保存第二份 Setting 资产。
  5. `Ctrl+S` 保存。
  - 验收：`GameEntry/GameObjectPoolMonitor` 存在且激活，只含 `Transform` + `GameObjectPoolMonitor` 两个组件；非播放模式下它的检查器显示「进入播放模式后可查看对象池的运行状态。」

- [ ] **A1–A3 完成后的联调预期（未验证，仅作对照标准）**
  - 播放时不再出现 `[GameApp] 未在 GameEntry/Settings 上配置 GameObjectPoolSetting，游戏对象池模块不可用`。
  - 空配置下 `Initialize` 通过（校验器对空数组不报错），不会出现 `[GameObjectPool] 初始化失败` 日志；预热任务因目标为 0 立即完成。
  - `GameEntry/GameObjectPoolMonitor` 的检查器在播放模式下显示模块状态「运行中」，配置数 0、运行池 0。
  - 若 A2 绑定失败或资产被删，启动期会明确报错而不是静默降级——这是预期行为。

- [ ] **目标 A4：迁移旧四个 C# 文件及其实际引用。**
  - 完成内容：确认四个脚本 GUID 无任何序列化引用、无业务调用点后，删除 `Assets/GameScripts/HotFix/GameLogic/Module/GameObjectPoolModule/`（含 `Base/`、`Data/` 与 `GameObjectPoolModule.meta`）。
  - 结果：全工程不再存在 `TbPoolMap`/`ConfigSystem` 访问；对象池入口统一为 `GameModule.GameObjectPool`。未删除 Luban 系统或其它配置表。

## 8. 仅在用户要求后执行的验收

用户本轮已要求检查落地代码，静态代码审查已完成；下列仍是 Unity 验证标准。Pipeline 不可达，**本轮未编译、未进入 Play Mode、未打包，下列运行结果均未验证。**

| 状态 | 场景 | 预期结果 |
|------|------|----------|
| [ ] | 编译及程序集依赖 | Runtime 不反向引用 GameLogic；Editor 代码不进 Player；迁移后的覆盖方法修饰符正确 |
| [ ] | 未挂 Monitor | 初始化、借出、回收、预热和关闭正常 |
| [ ] | Inspector 查看未初始化模块 | 显示未初始化，不创建模块/池/实例 |
| [ ] | Settings 缺资产、重名、空地址、非法数值 | 明确诊断，无部分初始化和隐式默认资源（重名只在编辑器诊断报，构建阶段与发布版都不拦；空地址等字段规则**只在编辑器侧校验**，发布版不拦、撞上时会由 `LoadAsset` 抛 `GameFrameworkException`） |
| [ ] | 首次 Spawn 与复用 | OnEnable 看到最终坐标；回调合同一致；缩放恢复 |
| [ ] | Spawn<T> 缺组件 | 返回失败，活跃计数不增加，无遗留激活对象 |
| [ ] | A 延迟回收 → 提前归还 → 同实例再借出 B | A 的旧任务不能回收 B |
| [ ] | 同租约重复设置延迟、外部取消、立即回收 | 最新计划生效；取消无成功回调；资源不重复释放 |
| [ ] | 借出对象隐藏 | 仍计为 Spawned，不误认为空闲 |
| [ ] | 重复回收与未知对象回收 | false/清晰诊断，底层计数不为负，不擅自销毁外部对象 |
| [ ] | 外部 Destroy、父节点 Destroy、场景卸载 | 失效实例清账，无幽灵索引，不在 OnDestroy 重设父节点 |
| [ ] | 从未激活的预热实例失效 | 不仅依赖 OnDestroy；后续借出不返回已销毁对象 |
| [ ] | ClearUnused 与 ReleasePool(force) | 行为不同；非强制释放不销毁仍借出实例；索引一致 |
| [ ] | 预热到 N、重复预热、同池并发预热 | 确有 N 个不同实例；不额外翻倍；等待后重新判断数量 |
| [ ] | 预热期间关闭池/取消 | 不恢复已关闭池，不在销毁根下创建实例 |
| [ ] | Capacity 与硬上限 | 清理目标不限制借出；硬上限达到且无闲置时失败，不抢占 |
| [ ] | Play Mode 配置/Undo | 运行统计不写回 SO；编辑态配置支持 Undo/Redo |
| [ ] | 连续进出 Play Mode（项目当前设置） | 无旧池、旧延时或旧展示缓存残留 |
| [ ] | Player/IL2CPP 及直接引用 Prefab 的脚本 | 模块反射创建保留、热更/主包反序列化兼容，真实构建确认 |
| [ ] | Profiler（仅在明确要求性能验证时） | 区分创建、复用、延迟任务和 Inspector 成本，不用第三方数据替代本项目结果 |

验证失败则记录实际证据、修改对应目标、只重跑受影响项目；不能把全部清单勾选来表示“代码写完”。

## 9. 交付约定

完成实施后报告：实际修改的类型与资产、行为兼容差异、是否完成 Inspector 接线、哪些验证已运行/未运行，以及剩余限制。

### 9.1 本轮交付清单

新增（运行时，`Assets/TEngine/Runtime/Module/GameObjectPoolModule/`）：`GameObjectPoolSetting.cs`、`GameObjectPoolDefinition.cs`、`GameObjectPoolValidator.cs`、`IGameObjectPoolModule.cs`、`GameObjectPoolModule.cs`、`GameObjectPoolModule.Pool.cs`、`GameObjectPoolModule.Spawn.cs`、`GameObjectPoolModule.Prewarm.cs`、`GameObjectPoolModule.Query.cs`、`GameObjectPool.cs`、`GameObjectInstance.cs`、`IGameObjectPoolCallbacks.cs`、`PoolSpawnContext.cs`、`GameObjectPoolInfo.cs`、`GameObjectPoolInstanceInfo.cs`、`GameObjectPoolMonitor.cs`。（`PooledGameObject.cs` 已于 2026-10-25 20:20 删除，不再交付。）

新增（编辑器）：`Assets/TEngine/Editor/GameObjectPoolSettingInspector.cs`、`GameObjectPoolMonitorInspector.cs`。

修改：`Settings.cs`（新增配置字段与静态入口）、`ModuleSystem.cs`（新增 `TryGetModule`）、`GameModule.cs`（新增 `GameObjectPool` 属性与缓存清理）、`GameApp.cs`（启动初始化/预热、退出关闭）、`TEngine.Editor.asmdef`（新增 UniTask 引用，供编辑器调用 `PrewarmAsync`）。

删除：`Assets/GameScripts/HotFix/GameLogic/Module/GameObjectPoolModule/`（旧 Manager/Pool/Instance/PoolConfig 及其目录 meta）。

### 9.2 需要后续确认的兼容差异

1. API 语义变化：`Spawn` 默认世界坐标、`SpawnLocal` 才是局部坐标；`ReleasePool` 不再删除定义（`UnregisterPool` 才删）；`IsClone` 拆分为 `IsManaged` / `IsSpawned`；`CreatePool` 改名 `RegisterPool`。原实现无外部调用，故未保留适配层。
2. 新增的可选池名：底层池名使用配置的 `PoolName`，不再用 MonoBehaviour 名称，因此 `Debugger` 通用面板中的名称与配置一致。
3. 未提供 `Spawn<T>(poolName)` 等历史重载，调用方按新的薄重载迁移；`Component` 版 `Despawn` 重载也未保留，传 `.gameObject`。
4. `TEngine.Editor.asmdef` 新增了 UniTask 引用：这是本轮唯一对框架程序集定义的改动，需要在 P2 的框架更新策略中一并保留。


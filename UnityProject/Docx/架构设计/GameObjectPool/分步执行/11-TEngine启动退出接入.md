# 第 11 步：TEngine 启动退出接入

前置：第 02—10 步主功能具备实际实现。  
本步完成后：对象池由框架自身创建与关闭，业务统一通过 GameModule 访问，`GameApp.cs` 零改动。  
状态：待实施。类型选择遵循总览中的纯数据结构体规则。

## 1. 目标与文件改动

| 目标 | 文件 | 关系 | 本步动作 |
|---|---|---|---|
| 11-A | Assets/TEngine/Runtime/Module/Settings/Settings.cs | **只读** | 核对现状：`gameObjectPoolSetting` 字段与 `GameObjectPoolSetting` 静态属性已写好；模块 `OnInit` 直接读它 |
| 11-B | Assets/GameScripts/HotFix/GameLogic/GameModule.cs | **只读** | 核对现状：`GameObjectPool` 属性与 `Shutdown` 里 `_gameObjectPool = null` 已就位 |
| 11-C | Assets/GameScripts/HotFix/GameLogic/GameApp.cs | **只读** | **确认零改动**：启动侧不需要任何接线（见 §2），退出侧已由 `Release()` 覆盖 |
| 11-D | 模块 `Shutdown` | 模块实现 | 保证无参、幂等，能在 `ModuleSystem.Shutdown` 的逆序遍历里安全执行 |
| 11-E | 模块类型命名与程序集归属 | **只读约束** | 必须 `TEngine.GameObjectPoolModule` + TEngine.Runtime + 无参构造；不改 asmdef、不改 ModuleSystem |

**本步的实质是「核对既有接线是否被模块满足」，不是修改接线。** 这几个文件属于第 01 步 §6.6 的只读清单：发现不匹配时改模块实现去适配，**不修改它们**。

## 2. 设计思路

TEngine.Runtime 不向上引用 GameLogic；Settings 提供配置，框架负责何时创建模块，模块只消费配置。

### 启动侧不需要接线（2026-10-25 17:00 起成立）

模块在 `OnInit` 里自己读 `Settings.GameObjectPoolSetting`、建根、发布配置（第 03 步），因此启动流程里**没有"初始化对象池"这一步可做**。`ModuleSystem.CreateModule` 在第一次 `GetModule<IGameObjectPoolModule>()` 时执行 `OnInit`，所以第一次通过 `GameModule.GameObjectPool` 取模块就是初始化时刻，不需要任何人显式驱动。

配合"预热属于建池"（第 03、09 步），`GameApp` 里的 `StartGameObjectPoolAsync`、`PrewarmConfiguredPoolsAsync`、`IsInitialized` 三样东西**一个都不需要**。第 01 步曾记录"GameApp 是空骨架、11 步文档的签名锁定前提失效、需要用户决策改不改"——这个决策现在自然消失了：**`GameApp.cs` 保持零改动，而且不是因为妥协，是因为没有要做的事。**

### 代价：没有"预热失败不降级进玩法"的兜底

原设计想用 `IsInitialized` 兜住"预热失败也照样进玩法"。去掉之后，这个保护转移到了调用点上：**建池失败就是借出失败**。`Spawn` / `TrySpawn` 在池未建时明确返回失败——业务必须自己处理失败（重试、跳过、报错），框架不再给一个"整局能不能进"的开关。

这个交换是明确的：用一个能被查询、却保证不了任何东西的状态值，换掉一次"问状态→执行操作"的往返，代价是失败处理下沉到真正知道该怎么处理的地方。如果将来确实需要启动期的整体成败判定，应该由**发起预热的那段业务流程**自己 await 并判断，而不是由池模块伪造一个全局状态。

### 退出侧已覆盖

`Release()` 走 `ModuleSystem` 的逆序关闭：模块链按优先级降序维护、关闭时从链尾往前，本模块 `Priority = 5` 低于底层 `ObjectPoolModule` 的 6，所以**游戏对象池先被关闭**，底层池随后清理，不会出现"底层已关、上层还在往回还对象"。模块只需保证 `Shutdown` 无参、幂等、自行取消内部任务。

**业务单例不能把"Release 时归还池对象"当作兜底**：那时池已经关闭，归还必然失败。玩法退出必须自己主动归还。

### 池级空闲计时与"白预热"

池在被创建并预热完成后开始计时（见[整池空闲过期需求讨论](../GameObjectPool-整池空闲过期需求讨论.md) §3.4）。除掉了启动预热之后，这个风险比原设计小得多：池只会在真正要用的时候才建，建完很快就被借出。仍然成立的场景是"建池后长时间不借出"，两种应对：

1. 常驻池把 `PoolExpireTime` 设为 0 关闭，只对确实低频的池开启。
2. 让建池时机尽量靠近首次使用（例如在场景切换之后建，而不是加载流程最前面建）。

**不引入"启动保护期"这类隐式豁免**：那等于加了一个没有配置入口的隐藏计时，使过期行为不可预测，也让排查"池为什么没释放"多出一条查不到的路径。

模块 Shutdown 与自动过期是两件事：Shutdown 清空配置与全部运行池，自动过期只释放某个池且保留配置。Shutdown 路径不读取 `PoolExpireTime`，也不参与池级计时判定。

## 3. 逐目标伪代码

### 11-A：核对 Settings 接线（现状即目标，不改）

现有代码已经是目标形态：

~~~csharp
[SerializeField]
private GameObjectPoolSetting gameObjectPoolSetting;

public static GameObjectPoolSetting GameObjectPoolSetting
{
    get
    {
        Settings settings = Instance;
        return settings != null ? settings.gameObjectPoolSetting : null;
    }
}
~~~

要点：不扫描工程找"第一个同类型资产"（`UpdateSetting` 里那种 `AssetDatabase.FindAssets` 回退不往本模块复制）；`Instance` 为 null 时返回 null 而不是抛异常，正好对应模块"缺少配置资产"的分支。Settings 继承 MonoBehaviour、持有配置，不属于纯数据载体——**本次零改动**。

### 11-B：业务入口（现状即目标，不改）

~~~text
GameModule.GameObjectPool:
  缓存为空 -> 通过既有 Get<IGameObjectPoolModule>()
  返回缓存；Get 内部走 ModuleSystem 的反射创建，首次访问即触发 OnInit

GameModule.Shutdown:
  清空 _gameObjectPool
  沿用其他模块缓存清理
~~~

模块类型名、命名空间、程序集、无参构造四者必须同时正确，否则 `Get<T>` 会断言失败报 null。

### 11-C：启动与退出（两处都不需要新增代码）

~~~text
启动：
  无任何显式接线
  首次 GameModule.GameObjectPool -> ModuleSystem.CreateModule -> OnInit
    -> 读 Settings、建根、发布配置（失败只打日志，各入口随后返回明确失败）
  首次 TryGetPoolOrCreate（或预热）-> 同步加载预制体、建池、按 PrewarmCount 预热（第 04、09 步）

退出：
Release():                      // GameApp.cs 现状（实测没有 ShutdownGameObjectPool）
  SingletonSystem.Release()
  GameModule.Shutdown()         // 只清 GameModule 自己的模块引用缓存
  // 不写 module.Shutdown()：IGameObjectPoolModule 里没有这个成员（2026-10-25 17:25 删除），
  //   关闭是 Module 基类的生命周期，由框架模块链负责
  // 缺口：Editor 下退出不走 RootModule.OnDestroy，模块 Shutdown 可能不被调到，
  //   是否在 Release 里补一次框架级关闭由本步决定，见 11-D
~~~

退出路径只用 `TryGetModule`，不得访问 `GameModule.GameObjectPool` 这类会创建模块的 getter——那会在关闭阶段反过来建一个没人关的模块。

`ModuleSystem.TryGetModule` 使用 `out` 引用参数属于既有框架合同，本模块沿用，不借"结构体传值"的要求去重构无关框架 API。

### 11-D：Shutdown 的硬约束

`Shutdown` 不进 `IGameObjectPoolModule`：它是 `Module` 基类的抽象方法，实现类用 `public override void Shutdown()` 覆盖，框架关闭模块时一定会调到。业务侧要关整个模块只能走框架级入口，不能拿接口变量去调单个模块。

~~~text
Shutdown 必须满足：
  无参                         // 与 Module 基类签名一致
  幂等                         // 重复调用不抛异常
  自行取消内部任务             // 预热与延迟回收
  不依赖外部先归还全部对象     // 强制释放，不因"还有人借出"而跳过
  销毁根节点即整棵子树         // 池节点都是它的子节点
~~~

### 11-E：程序集归属与保留

~~~text
模块/接口/配置/数据结构体 -> TEngine.Runtime
具体玩法调用与回调实现 -> GameLogic
Inspector -> TEngine.Editor
UniTask 已有引用就复用，不重复添加、不改 asmdef
不标 Preserve：反射创建需要的 public 无参构造由编译器隐式生成
裁剪风险留第 16 步用真实 IL2CPP 构建验证，真出问题走 link.xml
~~~

`TEngine.Editor.asmdef` 对 UniTask 的引用是既有现状（第 01 步已确认），本步只需确认它存在，不产生 asmdef 修改项。真实裁剪、预制体在目标资源包内的地址有效性，留在按需构建检查，不凭 C# 代码存在认定通过。

## 4. 完成标准

- [ ] 11-A Settings 绑定明确，无自动扫描默认值；`Settings.cs` 零改动。
- [ ] 11-B 统一业务入口存在，getter 内不做初始化；`GameModule.cs` 零改动。
- [ ] 11-C **确认启动侧无接线需求**：`GameApp.cs` 零改动，且不存在 `StartGameObjectPoolAsync` / `PrewarmConfiguredPoolsAsync` / `IsInitialized` 这类已经作废的调用点（第 01 步曾按"签名已锁定"记录过它们，实测 GameApp 从来没有这些调用）。
- [ ] 11-C 池未建时的借出返回明确失败，业务侧失败处理已就近实现；模块不提供全局"能否进玩法"开关。
- [ ] 11-D 退出按"池先关、单例后释放"执行，`Shutdown` 无参幂等，不访问会创建模块的 getter。
- [ ] 11-D 池级空闲计时从"池建好且预热完成"起算；常驻池的 `PoolExpireTime` 取舍已按真实流程确定，不存在隐式启动保护期。
- [ ] 11-E Runtime 无反向热更引用；`TEngine.Editor.asmdef` 与 `ModuleSystem.cs` 零改动。

## 5. 实施记录

复用的代码：待填写（预期为只读文件的既有入口，全部零改动）。  
实际修改：预期为空——本步只做核对与模块侧适配。  
退出顺序：`SingletonSystem.Release → GameModule.Shutdown`（现状，没有 `ShutdownGameObjectPool` 这一环）。`IGameObjectPoolModule` 里不声明 `Shutdown`，业务侧不再有它的调用点；模块关闭是否在 Editor 退出时被触发，属本步要确认的缺口。  
验证：未执行。

下一步：[12：配置接线与旧入口迁移](12-配置接线与旧入口迁移.md)。

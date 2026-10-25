# Parkour项目接入方案

日期：2026-10-25；v0.1。代码事实来自本次读取；所有新增脚本、目录与API为设计提案。本次仅保存文档。

## 1. 当前项目事实

路径相对 `UnityProject/`，仓库配置工程例外另标。

| 实际依据 | 当前事实 | 对接方案的影响 |
|---|---|---|
| ProjectSettings/ProjectVersion.txt | Unity2022.3.62f2 | 不使用Unity6 Graph Toolkit/RenderGraph假设 |
| Packages/manifest.json | URP14.0.12、HybridCLR、uGUI等；未声明InputSystem | 首版旧输入适配，不把新输入包当已安装 |
| ProjectSettings/ProjectSettings.asset | activeInputHandler=0 | 输入迁移需单独处理EventSystem和设置 |
| ProjectSettings/TimeManager.asset | Fixed Timestep=0.02 | 现有物理为50Hz，不能直接充当60Hz时钟 |
| Assets/GameScripts/HotFix/GameLogic/GameApp.cs | Entrance初始化事件；StartGameLogicAsync等待StartGameObjectPoolAsync；预热被await | 在现有就绪链后接战斗，保留原流程 |
| 同目录GameModule.cs | 已有Resource/UI/Scene/Audio/Timer/Fsm/GameObjectPool门面 | 新增Battle门面；基础设施直接复用 |
| Assets/TEngine/Runtime/Module/UpdataDriver/IUpdateDriver.cs | Add/Remove Update、FixedUpdate、LateUpdate | 只注册一个战斗驱动，不给每单位注册Update |
| Assets/TEngine/Runtime/Core/Utility/Utility.Unity.cs | AddUpdateListener内部先Yield后注册 | 快速创建/销毁存在延迟注册窗口，不能盲用成对Add/Remove假设 |
| Assets/TEngine/Runtime/Core/GameEvent/ | 泛型GameEvent、接口wrap和EventDispatcher | 不按原文假定它是字符串反射事件系统 |
| Assets/TEngine/Runtime/Module/GameObjectPoolModule/IGameObjectPoolModule.cs | Initialize、RegisterPool、TrySpawn、Despawn、Prewarm、ReleasePool | 池负责实例复用，不负责加载资源 |
| GameLogic/Module/UIModule/ | UIWindow/UIWidget和AddUIEvent | 新HUD只扩展业务UI，不重写UIModule |
| GameLogic.asmdef、GameProto.asmdef、GameEditor.asmdef | 热更业务、配置和Editor程序集存在；GameEditor references目前为空 | 编辑器引用新增schema需显式配置，不能假设自动可见 |
| GameLogic目录清单 | 当前读取范围未发现Battle/Combat/Action/Run业务实现 | 旧SimpleACT文档是计划，不是可直接调用的系统 |

旧 `SimpleACT/02` 文档曾描述对象池预热为Forget；当前代码已改为等待预热。因此本案不再列“补await预热”作为新增任务，也不修改旧文档。

## 2. 推荐接入位置

```text
Assets/GameScripts/HotFix/GameLogic/
  GameApp.cs                         扩展就绪后的玩法入口
  GameModule.cs                      新增Battle与必要UpdateDriver门面
  Module/BattleModule/
    BattleModule.cs                  会话、资源、驱动、退出
    Adapters/                       输入、资源、场景几何适配
    Bridges/                        TEngine事件和表现桥
  Core/Battle/
    Data/ Simulation/ Input/ State/ Action/ Motion/
    Hit/ Projectile/ Attribute/ Effect/ Part/ AI/ Event/ View/
  IEvent/IBattleEvent.cs             对外结果接口，生成器产物不手写
  UI/BattleDebugUI.cs                演示HUD，后续按正式UI设计替换
Assets/GameScripts/HotFix/GameProto/GameConfig/   Luban生成数据
Assets/GameScripts/HotFix/GameEditor/BattleAuthoring/ 编辑器与导出
Assets/GameData/BattleAuthoring/                  编辑源资产（建议）
Assets/AssetRaw/Configs/Battle/                   UAnim及manifest
Assets/AssetRaw/Actor/Battle/                     单位与弹丸表现
Assets/AssetRaw/Effects/Battle/                   效果资源
```

Core里是具体领域行为；BattleModule拥有管理与生命周期。首版不修改TEngine核心，也不新建一套ECS框架。将来RunModule接管关卡时，把BattleWorld作为其组成部分，BattleModule保留适配门面或收敛，禁止双驱动。

## 3. 初始化接入

```text
GameApp.Entrance（现有）
  GameEventHelper.Init（保留）
  StartGameLogicAsync
    await StartGameObjectPoolAsync（现有，保留）
    await BattleModule.LoadDefinitionSnapshotAsync（拟新增）
    await BattleModule.PrepareViewsAsync（拟新增）
    BattleModule.CreateWorld（拟新增）
    BattleModule.AttachInputAndResultBridges（拟新增）
    BattleModule.Start（拟新增，唯一Tick驱动）
```

启动正式游戏不应永久直接开战，首个切片可由专用Demo入口启动；后续菜单/关卡流程调用同一模块。失败停在可诊断状态，释放本次已取得资源，不吞异常继续Running。

驱动推荐在GameModule新增返回IUpdateDriver的门面，通过现有Get缓存实现，BattleModule经门面调用同步Add/Remove。这样避免Utility.Unity异步注册在Stop后补挂监听的竞态；不需要为了本功能改动公共Utility实现。此门面当前不存在，不能直接照抄调用。

## 4. 资源与对象池

池设置当前使用已加载Prefab定义；静态演示资源按现有GameObjectPoolSetting配置，再await预热后TrySpawn。使用`TrySpawn(poolName,position,rotation,parent,out instance)`；退出按本场实例清单Despawn，不调用全局池Shutdown。

动态YooAsset资源如果需要池化，必须先确定Prefab句柄由谁持有、池销毁何时释放；现有池不做资源加载。首版不绕过资源规范拼出随意的LoadAssetAsync<GameObject>+Instantiate链路，采用现有池设置或非池化LoadGameObjectAsync各自明确生命周期。后续动态池资源提供者单独设计。

TextAsset配置用GameModule.Resource加载，解析完成保留运行快照，并在适当时机UnloadAsset原资源。不要让GameProto反向引用GameLogic门面；Config加载由业务模块先异步取字节，再交给Tables/DTO转换。Luban模板若是同步懒加载，不能直接带进Tick。

## 5. 场景与Prefab具体制作目标

未来通过Unity CLI/编辑器完成，不直接手写序列化资产：

1. 新建BattleDemo场景：静态地面、PlayerSpawn、EnemySpawn、Camera、现有框架要求的入口；避免复制出两个GameEntry。
2. UnitView根：位置/旋转为默认、Scale=(1,1,1)；子节点Visual挂SpriteRenderer，VfxAnchor为表现挂点；绑定ActionPresentation与EntityHandle。
3. 战斗框由逻辑数据/Gizmo显示；若用Physics2D阻挡，在专用子节点放碰撞体并设置层，不能让OnTrigger负责扣血。
4. 将Player/Enemy/Projectile/VFX对应Prefab加入池配置；预热数由演示最大数量确定。View回池清状态；逻辑实体ID与GameObject实例ID分离。
5. 演示HUD显示HP、当前状态/动作/帧、最近拒绝原因；UI节点按项目生成绑定规范命名，不手改生成文件。
6. 必要资源未就绪不开始Tick；退出时先解绑驱动和输入，再取消任务、回池、卸载本场资产。

## 6. 与SimpleACT的接合

原玩法中的移动/耐力/速度档和挑战由RunWorld或对应模块拥有；本框架接收只读上下文，并输出Kill/Parry/PerfectDodge等事实。TimeStop从规则系统创建时间域请求，不能UI播放一个暂停动画就认为世界时停完成。

一击必杀与标准HP模式在开局选择RuleSet；同一战斗中不随意切换。当前SimpleACT关于确定性、精准闪避、落岛等未定事项仍需要玩法决策，本框架不擅自替它定稿。

## 7. 修改边界

本次创建文档与示意图；没有改C#、asmdef、Package、Prefab、Scene、Luban表或Unity设置。未来实施详见 [18](18-分步落地目标与验收计划.md)，每批执行前确认范围，不要求一次批准整个大框架。

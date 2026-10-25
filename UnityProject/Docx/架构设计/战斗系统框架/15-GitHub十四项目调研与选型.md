# GitHub十四项目调研与选型

调研日期：2026-10-25。范围：实际读取14个独立仓库的元数据与README，另读取4个仓库的相关源文件。**未克隆运行、未安装、未编译、未对这些项目作全面代码审计。** 下列取舍是基于本项目约束的设计判断，不把README的性能声明当成实测。

默认分支会变化；本表记录本次读取的分支与当时状态，正式依赖选型还需固定版本/提交。GitHub的pushed_at仅表示仓库推送时间，不足以说明稳定性，因此不以最近更新或Star数决定采用。

## 1. 战斗、技能与Buff：4项

### R01 — EGamePlay

来源：[仓库与README](https://github.com/m969/EGamePlay)、[AbilityExecution.cs](https://github.com/m969/EGamePlay/blob/main/Assets/App.Model/Game.Model/base-module.thirdparty.model/com.model.egameplay/AbilityExecute/AbilityExecution.cs)。读取main；GitHub标识MIT，未归档。

实际内容：3.0使用EcsNode，区分业务与View；能力定义、执行体和执行片段；执行体记录Owner、Ability、目标和ExecuteClips。README列出Odin、DOTween Pro、Animancer Pro等收费依赖及移植注意事项。

借鉴：ActionDefinition与ActionRuntime分离，技能效果与时间轴执行分开，配置与执行体有明确关系。应用于02/07/10。

不照搬：不把其ECS、配置生成及视图框架整套塞进TEngine，也不直接沿用其中OriginTime等字段作为本项目Tick语义。移植依赖和HotFix程序集成本高；推荐设计参考，而非首版直接引入。

### R02 — sjai013 Unity Gameplay Ability System

来源：[README](https://github.com/sjai013/unity-gameplay-ability-system)、[Gameplay Tags说明](https://github.com/sjai013/unity-gameplay-ability-system/wiki/Gameplay-Tags)。读取main；MIT，**已归档**。

实际内容：Ability、Attribute、GameplayEffect与Tag；效果区分瞬时、持续、永久，使用定义和运行实例表达施加效果。

借鉴：Buff定义与实例、临时属性修饰和标签需求，补全原文未展开的战斗数值层。应用于10/16。

不照搬：仓库不再开发；示例采用协程和秒制持续时间，与本项目UniTask边界及固定Tick不同。建议读结构，不作为新项目默认依赖。

### R03 — EX Gameplay Ability System For Unity

来源：[README](https://github.com/No78Vino/gameplay-ability-system-for-unity)。读取EX-GAS-1.0；MIT，未归档。

实际内容：Tag、AttributeSet、Modifier计算、GameplayCue、Effect、Ability及监控/生成工具。README明确依赖付费Odin，并说明测试和稳定性仍有限。

借鉴：把属性修改与表现Cue分开；为标签和属性生成可追踪标识；提供运行监视器。应用于10/12及编辑器诊断。

不照搬：其数值能力系统不能自动解决ACT取消窗口和同Tick仲裁；配置复杂度和依赖成本超过首个切片。优先采用其概念组织，不能将“GAS”直接等同完整动作战斗框架。

### R04 — Unity-Buff-System

来源：[README与生命周期](https://github.com/NoSLoofah/Unity-Buff-System)。读取main；MIT，未归档。

实际内容：Buff生命周期回调、层数变化、重复添加策略、Tag互斥和编辑器；README要求唯一BuffMgr及特定数据路径，示例有直接Instantiate效果。

借鉴：让施加、刷新、层数变化、周期触发、移除成为明确事件，便于内容制作。应用于10。

不照搬：不另立全局BuffMgr，不采用其示例表现实例化方式；生命周期中的“下一帧”改为本框架明确的阶段。Tag数量和互斥模型不强行套到本项目。

## 2. 状态与AI：2项

### R05 — UnityHFSM

来源：[README](https://github.com/Inspiaaa/UnityHFSM)、[StateMachine.cs](https://github.com/Inspiaaa/UnityHFSM/blob/master/src/StateMachine/StateMachine.cs)。读取master；MIT，未归档。

实际内容：层级状态机、需要退出时间的状态、延迟迁移、强制迁移；源码有PendingTransition、当前状态出边缓存及生命周期处理。

借鉴：迁移请求与迁移提交分开，显式退出时机和强制通道；对子状态和挂起请求建模。应用于05。

取舍：可作为后续轻量依赖候选，但战斗时间必须由注入Tick推进，不能照搬示例Time.deltaTime/秒制Timer。首版若UnitFSM仅四个状态，自有小实现更易固定规则，不必同时维护TEngine FSM和第二套完整库。

### R06 — Fluid Behavior Tree

来源：[README](https://github.com/ashblue/fluid-behavior-tree)。读取develop；MIT，未归档。

实际内容：代码Builder、组合/装饰节点、任务返回状态、跨帧继续和树可视化。其Continue语义与其他库的Running名称不同，不能只按名字拷贝接口。

借鉴：显式任务状态、运行游标、可重用任务与调试树。应用于11。

取舍：适合参考AI任务生命周期；不能用同一个执行器替代ActionGraph，也不能把默认每Update Tick当成AI降频策略。首版AI较小时先做有限节点库。

## 3. 图编辑器：2项

### R07 — xNode

来源：[README](https://github.com/Siccity/xNode)。读取master；MIT，未归档。

实际内容：节点、端口、可扩展Inspector，编辑器和运行时分离，README强调轻量和少依赖。

借鉴：节点画布、引用关系与自定义检查器的低成本制作方式。应用于07/11。

取舍：可作为编辑器候选；节点API不是战斗执行语义。运行时应使用本项目导出数据，不把GetValue的object模型搬到每Tick热路径。先完成数据导出再选画布库，避免编辑器先行锁定架构。

### R08 — NodeGraphProcessor

来源：[README](https://github.com/alelievr/NodeGraphProcessor)、[BaseGraph.cs](https://github.com/alelievr/NodeGraphProcessor/blob/master/Assets/com.alelievr.NodeGraphProcessor/Runtime/Graph/BaseGraph.cs)。读取master；MIT，未归档。

实际内容：UIElements/GraphView图编辑、分组、便签、错误消息；源码区分nodes/edges及GUID索引，计算顺序中有无效/循环标记。

借鉴：作者节点ID与引用分离、图错误显示、运行图导出；把“画布”和“解释器”拆开。应用于06/07。

不照搬：原处理器面向依赖/数据处理图，循环计算限制与合法跨Tick连招回边不同；不可直接用它的执行器跑动作图。GraphView及具体包版本需后续在Unity2022.3验证，当前未验证。

## 4. 通信和输入：2项

### R09 — MessagePipe

来源：[README](https://github.com/Cysharp/MessagePipe)。读取master；MIT，未归档。

实际内容：类型化Pub/Sub、同步/异步消息、作用域、订阅释放、诊断；Unity部分说明泛型注册与IL2CPP约束。

借鉴：明确订阅所有者、作用域和类型，结果桥只传播事实。应用于12。

不引入理由：当前TEngine已有事件设施；另外接入消息总线和DI增加重复职责。README性能比较不是本项目基准，不能由此宣称本战斗流零分配。

### R10 — Unity InputSystem

来源：[官方仓库README](https://github.com/Unity-Technologies/InputSystem)。读取develop；仓库license元数据为NOASSERTION，README说明Unity Companion License及第三方条款。

实际内容：动作、设备、绑定、重绑定样例、UI与游戏输入分流及输入记录示例。

借鉴：设备层与语义动作层分开；未来替换设备适配器时保持战斗Intent不变。应用于04/13。

取舍：后续多设备和重绑定需求明确时值得采用；当前项目仍为旧输入，不能为文档任务直接改依赖。使用兼容2022.3的正式包版本，而不是把develop当生产依赖。

## 5. 动画与后端：4项

### R11 — Spine Runtimes

来源：[README](https://github.com/EsotericSoftware/spine-runtimes)、[Skeleton.cs](https://github.com/EsotericSoftware/spine-runtimes/blob/4.3/spine-csharp/src/Skeleton.cs)。读取4.3；自定义Spine许可，不能当MIT。

实际内容：官方多引擎Runtime；Skeleton源码包含bones、slots、drawOrder、skin、constraints等，说明动画求值远多于pos/rot轨道。

借鉴：保留原生后端处理骨架和附件，UAnim只统一外层时间轴。应用于01问题A01、08。

取舍：有Spine资产时采用官方后端并锁编辑器/Runtime版本；首版Sprite项目不额外引入。许可遵循官方条款，本案未做法律或商业许可审查。

### R12 — Unity Animation C# Jobs Samples

来源：[README](https://github.com/Unity-Technologies/animation-jobs-samples)。读取master；元数据NOASSERTION。

实际内容：Playable图的SimpleMixer、WeightedMaskMixer、LookAt及IK示例，README说明面向Unity2019.3及以后。

借鉴：3D混合、遮罩和IK应使用对应动画能力，不应靠万能JSON播放器重新实现。应用于08。

取舍：作为3D Adapter的技术原型参考，仓库样例较旧；不能把README兼容说明当本项目2022.3真机验证。许可证与资源条款正式采用前按文件核查。

### R13 — Animancer 文档仓库（补充参考）

来源：[README](https://github.com/KybernetikGames/animancer)。读取master。

实际内容：README明确这只是Animancer文档仓库，**不包含Animancer产品源码**。

借鉴/取舍：可继续研究其代码驱动动画工作流；本次不声称审阅了实现，不把它计为可直接移植的开源动画库，也不为本项目购买/引入插件。此项是选型边界说明。

### R14 — Unity 2D Animation Samples（历史反例参考）

来源：[README](https://github.com/Unity-Technologies/2d-animation-samples)。读取master；未检测到标准许可证标识。

实际内容：早期2D Animation和IK预览样例，README明确提醒预览功能不宜用于正式生产。

取舍：仅帮助区分Sprite帧序列与骨骼动画；不采用这份历史预览工程。不能把它的警告泛化为“现代Unity2D动画不可用”。具体包选型应另查当前项目锁定版本。

## 6. 最终选择矩阵

| 能力 | 推荐 | 原因 |
|---|---|---|
| 战斗时序与权威核心 | 项目内小型专用实现 | 必须满足本文明确的阶段、取消、代次和规则集契约 |
| 基础设施 | 继续TEngine/YooAsset/UniTask/Luban/现有池 | 避免整框架替换与重复生命周期 |
| 技能/Buff数据结构 | 借鉴R01–R04，按所需子集实现 | 补齐原文空白，控制GAS复杂度 |
| 单位状态 | 借鉴R05，首版有限状态 | 与动作图、AI分工清晰 |
| AI | 借鉴R06；最小任务集 | 需要回执和取消，不只是画树 |
| 编辑画布 | 首版列表/Inspector；之后R07或R08二选一 | 先验证数据和运行时，再投资工具 |
| 输入 | 旧输入适配；需求明确后R10 | 不影响语义消费链 |
| 动画 | Sprite先行；3D/Spine按R11/R12扩展 | 原生后端不重复造轮子 |
| 事件 | 借鉴R09，保留TEngine对外桥 | 逻辑阶段与表现订阅分开 |

核心技术参考R01–R12共12个独立仓库，另有R13/R14补充边界参考，共14项；并非把同一仓库多个文件算成多个项目。所有“采用”均为方案建议，尚未安装或执行。

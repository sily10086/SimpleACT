# SimpleACT GitHub参考方案调研

调研日期：2026-10-24；分析稿 v0.1。范围：与策划案具体功能有关的14个独立GitHub项目，非14个网页或同仓库重复计数。依据当次可访问仓库README、目录、示例说明及许可证声明；未克隆、编译、集成或审计完整源码，也没有声称版本兼容测试已通过。

“成熟”不能由Star数量代替：以下区分官方能力、可借鉴库、教学例子和历史原型。每项的适配成本是本项目工程判断，非作者性能承诺。推荐引入不等于已获准安装；最终仍需锁tag/commit、核对许可证文件及第三方素材条款。

## 1. 统一评估表

| 编号与项目 | 对应功能 | 已读取证据/可借鉴点 | 本项目适配、成本与结论 | 许可/成熟度边界 |
|---|---|---|---|---|
| R01 [Unity-Technologies/InputSystem](https://github.com/Unity-Technologies/InputSystem) | 键鼠手柄、重绑定、输入缓冲前端 | 官方README列Input Recorder、Rebinding UI、UI vs Game Input等样例 | **建议候选引入，成本中**：增加兼容包、输入设置和EventSystem适配；封装成InputModule，不让InputAction直接改角色位置 | Unity Companion License并有样例第三方条款；官方能力，项目当前未安装 |
| R02 [Matthew-J-Spencer/Ultimate-2D-Controller](https://github.com/Matthew-J-Spencer/Ultimate-2D-Controller) | 跳跃、输入缓冲、角色手感 | README说明缓冲、土狼时间、顶点手感；免费与Extended版本区分 | **参考算法，成本中**：命令缓冲和运动分层可借鉴，动作时间改Tick；不自动加入土狼时间，不把收费扩展的二跳/闪避功能算进免费版 | MIT；可读控制器示例，不保证本游戏确定性 |
| R03 [SebLague/2DPlatformer-Tutorial](https://github.com/SebLague/2DPlatformer-Tutorial) | 平台碰撞、墙面、角色分层 | 分章节平台控制器，README明确玩家Layer与collision mask区分 | **参考结构，成本中高**：借鉴碰撞查询与运动职责，替换为本项目稳定几何和规则；教学版本不能直接当完整ACT底座 | MIT；教学工程，需要现代版本和高速碰撞适配 |
| R04 [Inspiaaa/UnityHFSM](https://github.com/Inspiaaa/UnityHFSM) | 动作互锁、敌人状态、流程 | README展示State/onEnter/onLogic/onExit、嵌套状态与转换 | **参考为主，成本低至中**：菜单已有TEngine FSM，不再装第二套；战斗若需HFSM，应由RunWorld显式Tick，禁用不可控秒计时 | MIT；通用HFSM库，不提供本项目判定规则 |
| R05 [asik/FixedMath.Net](https://github.com/asik/FixedMath.Net) | 严格数值确定性 | README为Fix64定点数学；仓库标记2021-04-23归档 | **数学候选，成本高**：可参考/适配数值原语，仍需碰撞、溢出、舍入和稳定排序；不建议无评估整库采纳 | 本次未完整审阅LICENSE.txt条款，实施前必须核对；历史归档库，不当持续维护方案 |
| R06 [Kimbatt/unity-deterministic-physics](https://github.com/Kimbatt/unity-deterministic-physics) | 确定性技术边界、复现风险 | README基于DOTS Physics 0.6.0-preview.3与soft floats，强调实体顺序和受控运算 | **概念参考、不引入，成本很高**：依赖体系与现有TEngine 2D差异大；用于说明固定步长不足，不能拿演示替代本项目验证 | Unity物理/数学部分为UCL，soft floats为MIT；历史技术演示 |
| R07 [OndrejNepozitek/Edgar-Unity](https://github.com/OndrejNepozitek/Edgar-Unity) | 关卡拓扑、片段模板、程序生成 | README用房间图约束连接、手工模板控制样式；平台生成列为PRO能力 | **参考图+模板，成本中高**：本游戏增加耐力/跳数/净空可达证据，不直接套地牢连通；采购PRO前仍需评估是否覆盖G1–G7 | 免费/PRO边界明确；本次未完整核阅各发行包许可，不默认可复制PRO内容 |
| R08 [Syomus/ProceduralToolkit](https://github.com/Syomus/ProceduralToolkit) | 几何辅助、关卡制作可视化 | README列几何、绘制、网格、生成策略等组件，要求Unity2022.3或更高 | **按需参考，成本中**：只在确有几何需求时选用；RandomE不能直接充当确定性RNG，需替换统一随机源 | 文档说明兼容下限不代表本项目已验证；含多个第三方组件，许可需逐组件核对 |
| R09 [mxgmn/WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse) | 片段邻接、装饰生成、失败约束 | README描述局部图案约束和求解；源码包含Model/SimpleTiledModel | **不作主路径生成器**：可参考邻接约束和失败处理，但局部合法不保证角色可达；核心路径建议受控图构造 | [LICENSE](https://github.com/mxgmn/WaveFunctionCollapse/blob/master/LICENSE)为MIT，示例图片/tiles不属于该软件许可范围 |
| R10 [sjai013/unity-gameplay-ability-system](https://github.com/sjai013/unity-gameplay-ability-system) | 模组效果、属性、冷却与条件 | README分静态AbilityDefinition与运行Spec，效果支持Add/Multiply/Override | **借鉴数据分离，成本中高**：使用Luban定义、Tick实例，去除Coroutine与秒制耦合；简单ACT不必引全套GAS | MIT；部分能力标WIP，不把README范围全当成熟现成功能 |
| R11 [Unity-Technologies/com.unity.cinemachine](https://github.com/Unity-Technologies/com.unity.cinemachine) | 镜头构图、跟随和边界 | 官方镜头仓库；新版说明API变化，输入与相机组件解耦 | **候选可选，成本中**：选Unity2022.3兼容版本；单矩形关卡可先薄控制器；镜头表现不决定权威威胁判定 | 最终按选择的包LICENSE核对；官方项目，2.x/3.x不可混用API |
| R12 [TeamSirenix/odin-serializer](https://github.com/TeamSirenix/odin-serializer) | 本地档案序列化、AOT边界 | README支持普通C#对象序列化，并区分JIT/AOT构建和支持代码生成 | **作为备选，成本中**：简单DTO可先用现有序列化路径；采用前核对HybridCLR类型与AOT；不提供奖励幂等/原子文件事务 | Apache-2.0；开源Serializer不是付费Inspector授权 |
| R13 [annulusgames/LitMotion](https://github.com/AnnulusGames/LitMotion) | UI动画、镜头脉冲、反馈生命周期 | README示例含调度器、MotionHandle取消和AddTo生命周期绑定 | **仅表现层候选，成本低至中**：不代替逻辑帧数；少量效果可不用新依赖；退出/回池必须取消旧动画 | 本次未完整审阅许可文件，实施前核对发行版本；性能宣传未在项目复测 |
| R14 [Unity-Technologies/Graphics](https://github.com/Unity-Technologies/Graphics) | URP水彩材质、世界褪色、渲染扩展 | 官方SRP源码仓库，含URP相关包 | **版本对应参考，成本中**：对齐URP14源码/样例；不克隆主分支替换项目包，不照搬Unity6 RenderGraph | Unity源码/第三方文件许可需依所用版本核对；官方源码不代表所有演示适合2D Renderer |

## 2. 采用建议

### 候选新增依赖

Input System最贴合已明确的键鼠/手柄与重绑需求；Cinemachine为可选。严格确定性如需要现成数学原语，R05仅为起点，必须先做专项选择，不能因为本次列了它就视为已经选型。

### 以项目现有框架完成

资源/场景加载、UI、事件、基础音频、对象池、普通流程FSM已有TEngine能力，优先复用。角色动作、时停、耐力与挑战采用小型领域系统。GAS/HFSM/Tween等参考用于理解职责，不默认新增整套框架。

### 不直接采用的方案

R06旧DOTS物理与项目不匹配；R09局部约束不足以证明平台可达；R07的PRO内容不能当免费资源；R12不能替代业务交易与崩溃恢复。代码量少不等于集成成本低，维护和规则可控性优先。

## 3. 引用落实到设计的位置

| 设计文档 | 对应参考 | 实际借鉴/取舍 |
|---|---|---|
| 03输入与模拟 | R01/R05/R06 | 设备适配、输入记录、数值与顺序约束 |
| 04动作 | R02/R03 | 缓冲与运动/碰撞分层，不加入原文外移动能力 |
| 05战斗 | R04 | 小状态与明确转换，动作时钟自己控制 |
| 06生成 | R07/R08/R09 | 拓扑/模板/约束分离；不以局部规则代替可达证明 |
| 07装备 | R10 | 静态定义、运行实例、成本与效果分离 |
| 08存档 | R12 | 序列化与业务事务分离，AOT选型边界 |
| 09镜头 | R11 | 构图与边界职责，版本兼容后再采用 |
| 10视听 | R13/R14 | 表现取消生命周期、URP版本对应实现 |

音频分层另以[Unity2022.3 PlayScheduled](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html)作为官方API依据；它是补充资料，不计入14个GitHub项目。没有找到足以直接覆盖本游戏挑战保留/奖励耐久全部规则的通用项目，因此该部分明确为本项目业务设计，未用无关仓库凑数。

## 4. 调研限制

本次已实时访问上述仓库页面，但没有锁定实施commit、完成完整许可证审计、移植或兼容测试。README与仓库默认分支会变化；实现前针对最终候选读取对应tag源码并记录版本。此限制不影响本次“功能对应、架构取舍、制作方案”的文档交付，但不能宣称14个项目都可以直接投入生产。

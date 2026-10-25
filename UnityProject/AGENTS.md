# AGENTS.md

请使用中文写提案和回答

这个文件为 Codex 提供指导，用于处理此代码库中的代码。

TEngine 基于 HybridCLR + YooAsset + UniTask + Luban 构建。

---

## ⚡ 强制工作流（所有任务必须遵守）

> **禁止跳过** — 无论任务大小，必须按此顺序执行：

### 第零步：判断任务等级

在执行任何操作前，先判断任务等级：

| 等级        | 判断标准                                                            | 知识查询策略                           |
| --------- | --------------------------------------------------------------- | -------------------------------- |
| **L1 简单** | typo 修正、注释修改、日志输出、单行变量改名（**前提：不涉及框架 API 名称、UI 节点前缀、事件定义或资源路径**） | ❌ 跳过查询，直接编码                      |
| **L2 调用** | 调用已知 API、单一模块的局部修改                                              | ✅ 触发 `tengine-dev` skill（只查该主题）  |
| **L3 功能** | 新功能开发、跨文件修改、新增 UI/资源/事件逻辑                                       | ✅ 触发 `tengine-dev` skill（全量相关主题） |
| **L4 架构** | 模块设计、系统重构、多模块协作、架构决策                                            | ✅ 触发 `tengine-dev` skill（并行多主题）  |

> **判断原则**：宁可高估等级，不可低估——不确定时上调一级。

---

### 第一步：按等级获取规范（使用 tengine-dev skill）

**L1 任务直接跳到第二步。L2-L4 必须先触发 `tengine-dev` skill。**

**知识源**：`.agents/skills/tengine-dev/references/`（AI 专用精炼文档，唯一权威来源）

#### 调用方式

```
调用 `tengine-dev` Skill
描述需要查询的技术问题或功能点
```

#### 会话内缓存（避免重复查询）

同一会话中已查询过的主题无需重复触发 skill：

- 直接引用本次会话已获取的规范摘要
- 仅当任务涉及**本次会话未覆盖的新主题**时才重新触发

#### 触发时机

| 场景       | 必须查询主题                                       |
| -------- | -------------------------------------------- |
| UI 开发    | ui-lifecycle.md — UIWindow 生命周期、UIWidget 规范  |
| 资源加载     | resource-api.md — LoadAssetAsync API、释放时机    |
| 热更代码     | hotfix-workflow.md — 程序集划分、GameApp 入口、热更边界   |
| 事件系统     | event-system.md — GameEvent 用法、AddUIEvent 规范 |
| 模块使用     | modules.md — GameModule.XXX API、模块生命周期       |
| Luban 配置 | luban-config.md — 配置表生成流程、访问方式               |
| 代码规范     | naming-rules.md — 命名约定、节点前缀、设计模式             |

---

### 第二步：输出代码/方案

基于 tengine-dev skill 返回的规范编写实现。

#### 方案文档归档

- 当任务需要设计或输出方案时，必须将方案文档保存到 Unity 项目根目录下的 `Docx/` 文件夹（即仓库内的 `UnityProject/Docx/`，与 `Assets/` 同级），禁止保存到 `UnityProject/Assets/Docx/`，也不得只在对话中输出而不落盘。
- `Docx/` 下必须按任务类型建立对应的分类子文件夹（例如 `架构设计/`、`功能开发/`、`重构优化/`、`问题修复/`）；已有合适分类时复用，不得为同类任务重复创建近义目录。
- 每个方案必须独立保存为一个文档，并放入对应的任务类型子文件夹；文件名应能清楚表达方案主题，避免使用“新建文档”等无意义名称。
- 若 `Docx/` 或所需的任务类型子文件夹不存在，应在写入方案前创建。

**当 references 规范与代码实际 API 冲突时**：

1. 使用 `rg` 搜索实际方法签名验证（例：`Grep "ForceUnloadUnusedAssets"` 确认参数名）
2. 优先信任代码中的实际实现
3. 在输出中标注冲突点，并记录到 `.agents/memory/` 供后续修正

---

## 核心原则（编码红线）

1. **异步优先**：IO 操作用 `UniTask`，禁止同步加载/Coroutine
2. **模块访问**：通过 `GameModule.XXX` 访问，而非 `ModuleSystem.GetModule<T>()`
3. **资源必须释放**：`LoadAssetAsync` 对应 `UnloadAsset`，GameObject 用 `LoadGameObjectAsync`
4. **热更边界**：`GameScripts/Main` 不热更，`GameScripts/HotFix/` 全部热更
5. **事件解耦**：模块间用 `GameEvent`，UI 内部用 `AddUIEvent`

---

## 📚 References 参考文档

> **AI 唯一权威来源：`.agents/skills/tengine-dev/references/`**

| 文档                    | 内容                                    | 层级       |
| --------------------- | ------------------------------------- | -------- |
| architecture.md       | 项目结构/启动流程                             | 核心       |
| modules.md            | 模块 API（Timer/Scene/Audio/Fsm）         | 核心       |
| ui-lifecycle.md       | UI 开发（生命周期/层级/属性）                     | 核心       |
| event-system.md       | 事件系统（两种模式/核心接口）                       | 核心       |
| resource-api.md       | 资源加载/卸载                               | 核心       |
| hotfix-workflow.md    | 热更代码（HybridCLR/程序集划分/热更包）             | 核心       |
| luban-config.md       | 配置表                                   | 核心       |
| naming-rules.md       | 代码规范/命名约定/节点前缀                        | 核心       |
| ui-patterns.md        | UI 进阶（Widget 模板/节点绑定）                 | 进阶       |
| event-antipatterns.md | 事件避坑（内存泄漏/接口无响应/风暴）                   | 进阶       |
| resource-patterns.md  | 资源管理模式/生命周期/泄漏根因                      | 进阶       |
| unity-operations.md   | Unity Editor/Player 操作边界与 Pipeline 规范 | Unity 操作 |
| troubleshooting.md    | 问题排查                                  | 排障       |

---

## 🔧 自我优化机制

### 问题记录

**触发条件**（满足任一即记录）：

1. 发现 references 文档描述与实际代码 API 不符（通过 Grep/Read 验证）
2. AI 生成的代码在编译/运行时报错，根因是知识库描述有误
3. 用户明确指出某文档描述有误

**记录规范**：

- 文件名：`problem_YYYY-MM-DD.md`（如 `problem_2026-04-21.md`）
- 必填字段：
  - **问题现象**：错误表现或报错信息
  - **文档位置**：哪篇 reference 文档哪一节
  - **正确 API**：经代码验证后的正确用法
  - **建议修正**：文档应改成什么表述

##  加载约定

- Codex 会读取当前目录及父目录中的 `AGENTS.md`，本文件是 UnityProject 的项目规则入口。
- 可复用工作流位于 `.agents/skills/`，按任务类型加载对应 `SKILL.md`。Unity Editor/Player 操作统一遵循 `.agents/skills/tengine-dev/references/unity-operations.md`，禁止使用 Unity MCP。
- 规范与实际代码冲突时，仍以代码实现为准，并按上文规则记录问题。

## 代码简化与重构

- 方法优先使用常规 `{ ... }` 块体，即使只有一行逻辑，也尽量少用 `=>` 表达式体方法（例如 `public void Apply(SimulatedBall ball) => ball.Growing = !ball.Growing;`）。新增或修改方法时遵循此约定；不为统一格式批量改写无关代码。该约定不限制 Lambda 表达式的使用，具体示例见 [代码规范](.agents/skills/tengine-dev/references/naming-rules.md)。
- 用户要求简化、重构或优化现有代码时，加载 [code-simplifier](.agents/skills/code-simplifier/SKILL.md)。
- 在用户指定的范围内保留原有行为，优先处理高收益、低风险的改动；普通功能开发和修复不默认追加一次完整简化。
- 使用该技能时仍遵循上文任务分级、tengine-dev 规范查询和验证边界。
- 简化过程中需要查询或保存长期经验时，统一遵循 [codex-memory](.agents/skills/codex-memory/SKILL.md) 的记忆规则。

## 记忆与历史上下文

- 涉及历史方案、已有经验、跨会话背景，或用户明确要求记住、回忆、遗忘信息时，加载 [codex-memory](.agents/skills/codex-memory/SKILL.md)；不要求每个任务都查询历史。
- 记忆插件是可选能力；仅在相关工具可用时调用，不可用时回退读取 `.agents/memory/` 的相关索引和条目。
- 插件历史查询遵循 search -> timeline -> get_observations 分层流程，只有摘要不足时才读取原始工具记录。
- 经过确认、对后续任务仍有价值的信息按 codex-memory 的记录边界和格式写入 `.agents/memory/<主题>/`，维护对应 `MEMORY.md` 索引；不得假定索引会自动注入会话。
- 当前代码、规则和 Git 历史可直接推导的信息无需重复记忆；上文要求的 references 与实际 API 冲突问题仍按“问题记录”规范保存。
- 不得把插件数据库、日志、密钥或机器绝对路径提交到仓库。

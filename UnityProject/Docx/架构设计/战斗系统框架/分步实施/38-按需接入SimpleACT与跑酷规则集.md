# 步骤 38｜按需接入SimpleACT与跑酷规则集

版本：v0.1｜阶段：S7 可选｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

把通用战斗框架接入已有 SimpleACT 玩法提案，通过规则集处理一击必杀、精准闪避、速度档与时停。

- **R1**：先确认 SimpleACT 尚未确定的玩法规则，不能把示例时停值当正式决定。
- **R2**：RunModule 与 BattleModule 共享一个 BattleWorld 和时钟。
- **R3**：跑酷移动/挑战归玩法模块，战斗输出明确事实供其处理。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 01、步骤 27、步骤 28、步骤 29、步骤 30、步骤 37。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：已确认的 SimpleACT 规则、基础框架、RunModule 实际存在状态。
- 交付输出：SimpleActRuleSet、Run/Combat 协作契约与时间域请求映射。
- 设计依据：

- [13-Parkour项目接入方案](../13-Parkour项目接入方案.md)
- [17-战斗系统图文应用案例](../17-战斗系统图文应用案例.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `G/Core/Battle/Effect/SimpleActRuleSet.cs` | 按需新增 | 一击必杀及特定裁决。 |
| `G/Module/BattleModule/Bridges/RunCombatBridge.cs` | 按需新增 | 战斗事实与跑酷规则协作。 |
| `未来 RunModule 所在路径` | 确认后修改 | 仅在模块实际建立后接入同一 World。 |
| `D/SimpleACT接入规则决策.md` | 新增文档 | 精准、速度档、时停等正式选择。 |

## 4. 具体执行任务

### 38.1 确认规则与边界

- [ ] **做什么**：确认规则与边界。
- **为什么**：精准闪避需要定义时间/空间判定，不是播特效即成立。
- **怎么做**：明确攻击威胁、成功时点、重复奖励、时停目标和持续时间，使用规则编号。
- **关键伪代码**：

```text
PerfectDodgeRule = {threatDefinition, timingWindow, rewardDedupKey, timeStopPolicy}
```

### 38.2 接入事实转换

- [ ] **做什么**：接入事实转换。
- **为什么**：UI 或动画回调不能决定精准成功。
- **怎么做**：P5/P6 产生 Dodge/Parry/Kill 事实，玩法规则生成速度/时域请求，下个许可阶段统一生效。
- **关键伪代码**：

```text
CombatFact -> ModeRule -> TimeDomainRequest
```

### 38.3 消除双驱动

- [ ] **做什么**：消除双驱动。
- **为什么**：RunWorld 和 BattleWorld 各自 Tick 会产生时间错位。
- **怎么做**：选择唯一宿主拥有者，另一模块只提供门面；移动权威仍由统一 Motor 协作。
- **关键伪代码**：

```text
RunModule.Tick -> sameBattleWorld.Step
BattleModule must not register another driver
```

## 5. 本步完整流程伪代码

```text
BeginMode:
  choose SimpleActRuleSet before snapshot freeze
  resolve single world owner
DuringTick:
  execute common action/hit pipeline
  produce authoritative mode facts
  apply approved reward and time-domain policies
  publish presentation results
```

## 6. 边界与失败处理

- 若要求跨平台可复现跑酷，步骤 41 的决策需要前移到步骤 01，不能做到此处才换物理。
- 未选择该玩法时本步跳过，不影响 StandardActDemo。

## 7. 完成标准与待执行检查

- [ ] 同一场只有一个单位集合与一个 Tick 驱动。
- [ ] 精准成功可追踪到明确规则和事实。
- [ ] 时停按 SimTick 结束，冻结单位不会永久停止。

这些是将来判断本步是否完成的条件，本次没有执行。用户要求检查时，再按上述场景记录输入、预期、实际结果和证据；没有检查结果不填写“通过”。

## 8. 实施记录

| 项目 | 当前记录 |
|---|---|
| 批次确认 | 未确认编码批次 |
| 实际修改文件 | 无，本次仅编写方案 |
| 已完成任务 | 无实现任务完成 |
| 待完成任务 | 第 4 节全部任务 |
| 规则调整及原因 | 无；与上层设计冲突时先记录并同步相关文档 |
| 检查结果 | 未执行 |
| 后续交接 | 提交本步输出及未解决项，按总索引进入依赖满足的下一步 |


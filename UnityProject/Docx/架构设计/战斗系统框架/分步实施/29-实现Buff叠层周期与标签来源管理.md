# 步骤 29｜实现Buff叠层周期与标签来源管理

版本：v0.1｜阶段：S5｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

支持可配置持续效果，并保证刷新、到期、死亡移除和多来源标签都能正确撤销。

- **R1**：每个 Buff 声明时间域、聚合键、叠层、周期和死亡策略。
- **R2**：灼烧示例有效 180 UnitTick、周期 60、首个周期后触发；到期边界不触发。
- **R3**：刷新保留原 nextPeriodTick，只刷新到期时点；标签采用来源计数。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 05、步骤 08、步骤 16、步骤 17、步骤 27。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：BuffDefinition、施加请求、时间域和 EffectSystem。
- 交付输出：BuffSystem、BuffRuntime、TagSourceSet 与 Buff 结果。
- 设计依据：

- [10-伤害属性Buff与部位破坏](../10-伤害属性Buff与部位破坏.md)
- [16-面向程序的战斗系统策划案](../16-面向程序的战斗系统策划案.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `G/Core/Battle/Effect/BuffSystem.cs` | 新增 | 添加、刷新、周期、移除。 |
| `G/Core/Battle/Effect/BuffRuntime.cs` | 新增 | 来源、层数、开始/结束/下周期。 |
| `G/Core/Battle/State/TagSourceSet.cs` | 新增 | 按来源持有标签。 |

## 4. 具体执行任务

### 29.1 实现聚合与刷新

- [ ] **做什么**：实现聚合与刷新。
- **为什么**：刷新不能重复添加修饰器或把 DOT 无限推后。
- **怎么做**：演示按 buffId 聚合一层、最近来源归因；Refresh 更新 expire 而不重置 nextPeriod。
- **关键伪代码**：

```text
existing.expire = now + duration
existing.source = newestSource
keep existing.nextPeriod
```

### 29.2 实现时点与周期

- [ ] **做什么**：实现时点与周期。
- **为什么**：同 Tick 先触发再到期会多出一跳伤害。
- **怎么做**：P0 优先移除 now>=expire；未到期且达到周期则排效果到 P5，伤害复用提交系统。
- **关键伪代码**：

```text
if now >= expire: Remove()
else if now >= nextPeriod: QueueEffect(); nextPeriod += period
```

### 29.3 建立来源化撤销

- [ ] **做什么**：建立来源化撤销。
- **为什么**：两个 Buff 都授予霸体时移除一个不能清掉另一个。
- **怎么做**：标签和属性修饰带来源 token；统一 RemoveOnce 撤销自己持有的项。
- **关键伪代码**：

```text
tags.Add(tag, buffInstance)
RemoveBuff -> tags.RemoveSource(buffInstance)
```

## 5. 本步完整流程伪代码

```text
ApplyOrRefresh(request):
  resolve aggregate key
  if exists: apply declared stack policy
  else: create instance and install owned modifiers/tags
P0:
  evaluate expiry using declared clock
  enqueue due periodic effects for P5
P6 death:
  remove death-cleared buffs once
Remove:
  revoke owned contributions and emit removal
```

## 6. 边界与失败处理

- 时停是否影响 Buff 由 clock 字段决定；不得偷用宿主时间。
- 周期≤0、无效叠层上限导入失败；周期同 Tick 死亡按阶段规则收尾。

## 7. 完成标准与待执行检查

- [ ] 首次施加后仅 +60/+120 触发，+180 到期。
- [ ] 连续刷新不复制修饰器且不无限推迟下一跳。
- [ ] 移除一个来源后其他来源持有的相同标签仍有效。

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


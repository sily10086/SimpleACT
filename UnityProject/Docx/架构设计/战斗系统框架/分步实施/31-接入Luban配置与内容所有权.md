# 步骤 31｜接入Luban配置与内容所有权

版本：v0.1｜阶段：S5｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

把演示构建器迁移为可维护内容数据，划清 Luban 标量配置与 UAnim 时间轴/图结构的归属。

- **R1**：同一规则只保留一个权威数据源，避免表格和 UAnim 同时修改窗口。
- **R2**：GameProto 不反向依赖 GameLogic 模块。
- **R3**：生成代码通过既有 Luban 流程产生，不手改生成 C#。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 03、步骤 04、步骤 28、步骤 29、步骤 30。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：现有 Luban 源工程位置、16 文档字段、完整演示定义。
- 交付输出：表结构、演示数据、Tables→BattleSnapshot 转换器。
- 设计依据：

- [07-复合动作编辑器与数据管线](../07-复合动作编辑器与数据管线.md)
- [13-Parkour项目接入方案](../13-Parkour项目接入方案.md)
- [16-面向程序的战斗系统策划案](../16-面向程序的战斗系统策划案.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `现有 Luban 源定义与数据目录` | 修改 | 实施时定位真实源工程；新增 Unit/Damage/Buff/Part 标量表。 |
| `P/GameConfig/` | 生成 | 通过现有生成流程产生数据类型。 |
| `G/Module/BattleModule/Adapters/BattleConfigAdapter.cs` | 新增 | 表配置与 UAnim 合成定义源。 |
| `A/AssetRaw/Configs/Battle/` | 新增内容 | 动作/图数据与 manifest。 |

## 4. 具体执行任务

### 31.1 冻结数据所有权表

- [ ] **做什么**：冻结数据所有权表。
- **为什么**：同名字段出现在两处会形成不同运行结果。
- **怎么做**：Unit/Damage/Buff/Part 标量归 Luban，动作窗口与图边归 UAnim；表中仅用稳定 ID 引用。
- **关键伪代码**：

```text
ownership = {scalarRules:Luban, timelines:UAnim, graphs:UAnim}
```

### 31.2 建立转换器

- [ ] **做什么**：建立转换器。
- **为什么**：运行时核心不应依赖生成表的加载机制。
- **怎么做**：业务先异步取得内容，再转换成核心定义；禁止在 Tick 触发同步懒加载。
- **关键伪代码**：

```text
tables + uanim -> BattleDefinitionSource -> CompileSnapshot
```

### 31.3 迁移样例并保留开发回退

- [ ] **做什么**：迁移样例并保留开发回退。
- **为什么**：编辑器未完成时仍应能开发运行时。
- **怎么做**：将演示参数逐项迁移；构建器标为 DevelopmentOnly，不能与正式源同时叠加。
- **关键伪代码**：

```text
source = profile.UseDevelopmentBuilder ? builder : configuredSource
```

## 5. 本步完整流程伪代码

```text
ContentPrepare:
  read generated table data through existing project loader
  read UAnim graph and timeline files
  resolve stable cross-references
  validate ownership and versions
  convert to unified definition source
  freeze session snapshot
Runtime:
  access snapshot only
```

## 6. 边界与失败处理

- 源表目录未在本次文档细化中定位，列为实施前必须定位项，不编造实际路径。
- Luban 生成属于实施操作，本文没有运行生成器。

## 7. 完成标准与待执行检查

- [ ] 正式配置与开发样例使用同一 Snapshot 契约。
- [ ] 所有跨表/动作引用可定位并校验。
- [ ] 修改动作有效帧只需修改唯一权威内容源。

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


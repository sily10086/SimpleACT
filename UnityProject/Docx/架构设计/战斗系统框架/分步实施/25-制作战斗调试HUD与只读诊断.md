# 步骤 25｜制作战斗调试HUD与只读诊断

版本：v0.1｜阶段：S4｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

在演示中直观看到 HP、状态、动作帧、最近命令与拒绝原因，帮助后续实现定位规则问题。

- **R1**：HUD 只读快照，不直接修改 HP/FSM。
- **R2**：遵循项目 UIWindow/UIWidget 与 AddUIEvent 生命周期。
- **R3**：信息刷新与渲染分开，避免每 Tick 分配大量文本。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 24。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：已提交结果、单位诊断快照、现有 UI 绑定规范。
- 交付输出：BattleDebugUI 脚本与后续 Prefab 制作清单。
- 设计依据：

- [13-Parkour项目接入方案](../13-Parkour项目接入方案.md)
- [16-面向程序的战斗系统策划案](../16-面向程序的战斗系统策划案.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `G/UI/BattleDebugUI.cs` | 新增 | 窗口生命周期与数据刷新。 |
| `G/Module/BattleModule/Bridges/BattleDebugSnapshot.cs` | 新增数据 | 必要的调试字段。 |
| `A/AssetRaw/UI/BattleDebugUI.prefab` | 新增序列化资产 | 用 Unity CLI/编辑器及现有生成绑定流程制作。 |

## 4. 具体执行任务

### 25.1 设计最小信息布局

- [ ] **做什么**：设计最小信息布局。
- **为什么**：只有 HP 无法区分窗口关闭与耐力不足。
- **怎么做**：显示双方 HP/耐力/主状态/ActionHandle/localFrame，以及最新 CommandId 和 rejection；节点名按项目规范。
- **关键伪代码**：

```text
debug = {hp, stamina, state, actionId, frame, commandId, rejectReason}
```

### 25.2 连接窗口生命周期

- [ ] **做什么**：连接窗口生命周期。
- **为什么**：重新打开窗口可能重复订阅。
- **怎么做**：在规定生命周期初始化/释放，使用 AddUIEvent；关闭后停止刷新，读取最新快照恢复。
- **关键伪代码**：

```text
Open -> BindUIEvents -> ReadLatestSnapshot
Close -> FrameworkOwnedCleanup
```

### 25.3 提供可定位状态

- [ ] **做什么**：提供可定位状态。
- **为什么**：日志刷屏无法观察一次连招。
- **怎么做**：最近结果使用有界列表，按值变化更新文本；后续单步控制单独通过命令入口。
- **关键伪代码**：

```text
if displayedValue != snapshotValue: UpdateText()
recentResults.PushBounded(result)
```

## 5. 本步完整流程伪代码

```text
OnWindowReady:
  acquire current battle read-only snapshot
  register framework-managed UI event handlers
OnBattleCommitted:
  cache newest relevant snapshot
RenderUI:
  update changed fields and bounded result list
OnClose:
  release UI-owned references and subscriptions
```

## 6. 边界与失败处理

- 本步 HUD 是开发工具，不作为正式战斗 UI 视觉设计。
- 生成绑定文件不手工修改；具体节点前缀实施时以现有 UI 规范为准。

## 7. 完成标准与待执行检查

- [ ] 关闭 HUD 不影响战斗结算。
- [ ] 反复打开关闭不重复收到结果。
- [ ] 窗口能明确显示 WindowClosed、InsufficientResource 等原因。

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


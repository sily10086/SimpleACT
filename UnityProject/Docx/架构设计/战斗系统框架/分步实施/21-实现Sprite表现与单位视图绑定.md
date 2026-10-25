# 步骤 21｜实现Sprite表现与单位视图绑定

版本：v0.1｜阶段：S4｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

把逻辑动作与位置快照显示成 Sprite 动画，同时保持表现对战斗结果的单向依赖。

- **R1**：动画按 ActionFrame 采样，不以 AnimationEvent 决定命中。
- **R2**：UnitView 绑定完整 EntityHandle，异步回调核对会话和代次。
- **R3**：逻辑切换立即生效，视觉混合不能延迟输入许可。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 12、步骤 14、步骤 19。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：ActionViewSnapshot、前后姿态、预载 SpriteBinding。
- 交付输出：UnitView、SpriteActionPresenter 和解绑清理契约。
- 设计依据：

- [08-动画后端与移动协作](../08-动画后端与移动协作.md)
- [12-事件结果流与表现生命周期](../12-事件结果流与表现生命周期.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `G/Core/Battle/View/UnitView.cs` | 新增 | 逻辑身份与 Unity 对象桥。 |
| `G/Core/Battle/View/SpriteActionPresenter.cs` | 新增 | Sprite 帧映射和采样。 |
| `G/Core/Battle/View/IActionPresentation.cs` | 新增接口 | 后端采样和释放端口。 |

## 4. 具体执行任务

### 21.1 完成帧映射

- [ ] **做什么**：完成帧映射。
- **为什么**：动画图片数量不必等于动作总帧。
- **怎么做**：按明确 exposure 帧段定位 Sprite；验证帧段覆盖，不用渲染 delta 自行累计第二时钟。
- **关键伪代码**：

```text
spriteIndex = exposureMap.Resolve(snapshot.actionFrame)
```

### 21.2 绑定姿态与表现

- [ ] **做什么**：绑定姿态与表现。
- **为什么**：逻辑位置不由 Transform 反推。
- **怎么做**：View 只插值 Motor 快照；朝向、颜色与图层从表现绑定设置。
- **关键伪代码**：

```text
transform.pose = Interpolate(previousPose,currentPose,alpha)
presenter.Sample(actionSnapshot)
```

### 21.3 管理绑定代次

- [ ] **做什么**：管理绑定代次。
- **为什么**：池复用后旧特效不能出现在新角色。
- **怎么做**：Bind 记录 handle/token；Unbind 清动作、材质临时状态和挂点；过期资源回调释放取得的资源。
- **关键伪代码**：

```text
if callback.binding != currentBinding: ReleaseAcquiredResource(); return
```

## 5. 本步完整流程伪代码

```text
Bind(view, entity, resources):
  reset prior presentation state
  save exact handle and binding token
Render(snapshot, alpha):
  if snapshot.entity != binding.entity: ignore
  interpolate logical poses
  sample sprite from ActionFrame
Unbind:
  invalidate token
  stop local effects and detach listeners
  clear references
```

## 6. 边界与失败处理

- 必要 Sprite 资源缺失在启动前失败；非必要 VFX 缺失只记诊断。
- 本步不制作 Prefab，序列化资源在步骤 26 制作。

## 7. 完成标准与待执行检查

- [ ] 低渲染帧率不改变攻击生效帧。
- [ ] 视图绑定代次不匹配时不应用旧请求。
- [ ] 逻辑动作结束不依赖 Sprite 播放回调。

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


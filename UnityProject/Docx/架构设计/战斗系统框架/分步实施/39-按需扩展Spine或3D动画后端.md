# 步骤 39｜按需扩展Spine或3D动画后端

版本：v0.1｜阶段：S7 可选｜状态：方案已编写，实施未开始。

> 本文属于已授权的文档细化。所有新增类型、接口及路径是拟实施方案；伪代码用于表达控制流，不是可直接编译的代码。执行本步前确认该批次；完成后填写实际记录。

## 1. 目标与需求

在保留原生动画能力的前提下复用动作时间轴，避免把所有素材强行转换成自研骨骼格式。

- **R1**：一次选择有真实素材需求的一个后端。
- **R2**：后端只采样表现，逻辑命中窗口仍归 ActionTimeline。
- **R3**：能力、资源生命周期和许可依赖在实施前确认。

## 2. 前置依赖与输入输出

- 前置步骤：步骤 21、步骤 22、步骤 32、步骤 33、步骤 35。完整跳转见 [执行总索引](00-执行总索引.md)。
- 输入：目标素材、Unity/Spine 版本、IActionPresentation 与资源绑定契约。
- 交付输出：一个后端适配器、能力描述、资源接线与示例动作。
- 设计依据：

- [08-动画后端与移动协作](../08-动画后端与移动协作.md)
- [15-GitHub十四项目调研与选型](../15-GitHub十四项目调研与选型.md)

## 3. 涉及范围与全部修改目标

路径前缀约定见 [总索引](00-执行总索引.md)：G 为 GameLogic，E 为 GameEditor，P 为 GameProto，A 为 Assets，D 为本套设计文档目录。下表是本步计划修改范围；外部系统仅通过列明契约协作，若发现必须扩展范围，应先同步本表。

| 路径或对象 | 操作 | 责任及本步修改内容 |
|---|---|---|
| `G/Core/Battle/View/SpineActionPresenter.cs 或 PlayablesActionPresenter.cs` | 择一新增 | 原生后端时间采样。 |
| `G/Core/Battle/Data/PresentationCapabilities.cs` | 按需新增 | 支持混合/遮罩/约束等能力。 |
| `E/BattleAuthoring/Preview/` | 扩展 | 目标后端预览。 |
| `Packages/相关依赖与资源绑定` | 确认选型后修改 | 引入确有需要的版本。 |

## 4. 具体执行任务

### 39.1 核对素材与能力

- [ ] **做什么**：核对素材与能力。
- **为什么**：Spine 皮肤/约束和 3D 混合不是简单骨骼轨道等价物。
- **怎么做**：列出实际需用特性及版本，选择原生 Runtime；许可和商业依赖沿用调研结论并核对具体版本。
- **关键伪代码**：

```text
requiredCapabilities = InspectActualAssets()
backend = SelectCompatibleNativeRuntime()
```

### 39.2 实现手动时间驱动

- [ ] **做什么**：实现手动时间驱动。
- **为什么**：后端自动播放会与逻辑时钟分离。
- **怎么做**：按 ActionFrame/表现插值换算采样时间；RootMotion 输出请求给 Motor，不直接移动根节点。
- **关键伪代码**：

```text
sampleTime = ConvertActionFrame(snapshot)
backend.Sample(sampleTime)
rootMotion -> MotionRequest
```

### 39.3 扩展导入与预览

- [ ] **做什么**：扩展导入与预览。
- **为什么**：运行支持而编辑预览不支持会误导策划。
- **怎么做**：在 Manifest 声明能力，校验不支持的轨道，预览和运行使用一致绑定映射。
- **关键伪代码**：

```text
if requiredFeature not in backendCapabilities: blockExport
```

## 5. 本步完整流程伪代码

```text
Prepare:
  load selected native runtime assets
  create backend adapter and bindings
Sample:
  resolve action view track
  drive backend time explicitly
  apply supported blending/masks
Release:
  destroy owned graph/runtime state
  release resources via session scope
```

## 6. 边界与失败处理

- 3D 与 Spine 是两个候选实现，不要求一次全部引入。
- 不能把 Animancer 文档仓库误当可直接复制的完整源码框架。

## 7. 完成标准与待执行检查

- [ ] 同一逻辑动作换后端不改变命中 Tick。
- [ ] RootMotion 不绕过 Motor 写逻辑位置。
- [ ] 退出能销毁后端图/状态并释放资源。

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


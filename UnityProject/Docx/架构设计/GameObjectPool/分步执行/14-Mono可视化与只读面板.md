# 第 14 步：Mono 可视化与只读面板

前置：第 10 步查询 API、第 12 步主功能、第 13 步配置编辑器。  
本步完成后：GameEntry 既有节点上的 Mono 恢复可用，提供运行可视化；没有该 Mono 时运行池仍完整工作。  
状态：待实施。本步只做查看，改变运行状态的按钮放第 15 步。

## 1. 目标与文件改动

| 目标 | 文件 | 改动与功能 |
|---|---|---|
| 14-A | Assets/TEngine/Runtime/Module/GameObjectPoolModule/GameObjectPoolMonitor.cs | 新增只存展示选项的 Mono；`.meta` 的 guid 必须写成 `944e081cd8179054ebf7c4fef15131e0`，让 GameEntry.prefab 既有节点上的 Missing 脚本自动复绑 |
| 14-B | Assets/TEngine/Editor/GameObjectPool/GameObjectPoolMonitorInspector.cs | 先把暂存的 `Docx/架构设计/GameObjectPool/待启用/GameObjectPoolMonitorInspector.cs.txt` 改回 `.cs` 落位（539 行骨架已含搜索、筛选、折叠、实例分页、定位、手动刷新），再接只读快照；`.meta` 沿用 guid `7dcdf01fe4cb4fb3a8a75ef4347a45df`。该文件是 `internal sealed class`，标了 `[CustomEditor(typeof(GameObjectPoolMonitor))]`，继承 `GameFrameworkInspector`（命名空间 `TEngine.Editor.Inspector`，要 `using`） |
| 14-C | 同 Inspector | 搜索、筛选、折叠、实例分页、定位 |
| 14-D | 同 Inspector | 有限刷新、播放切换/生命周期缓存清理 |

Monitor 必须是 MonoBehaviour class；它不承载池、字典或计时器。快照/展示命令数据使用 readonly struct，按值读取。

## 2. Monitor 上的展示设置

| 字段 | 建议初值 | 用途 |
|---|---|---|
| autoRefresh | true | 是否自动刷新快照 |
| refreshInterval | 0.5 秒 | 面板刷新间隔，不改变池 Update |
| showOnlyCreatedPools | false | 是否隐藏未建池的配置 |
| showOnlySpawnedPools | false | 是否仅显示有借出对象的池 |
| showOnlyIdleExpiringPools | false | 是否仅显示已开启整池过期且正在计时的池 |
| searchPoolName | 空 | 名称过滤 |
| showInstanceList | false | 是否显示实例明细 |
| instancePageSize | 50 | 控制可见实例行数 |

这些值只是显示设置。Monitor 不持有第二个可编辑 Setting，不绑预制体或 PoolRoot，也不提供初始化、Shutdown、Awake/Start/Update/OnDestroy 驱动逻辑。

## 3. 设计思路与布局

参考 ResourceModuleDriverInspector 的配置区/运行区分离、RootModuleInspector 的状态展示、现有通用对象池调试窗口的数据口径。只学习表现方式，不能因为名字有 Driver 就把 Monitor 设计成驱动器，也不照搬无限 Repaint。

~~~text
状态：
  编辑模式 / 模块未创建 / 未初始化 / 运行中 / 已关闭
  实际初始化配置资产 [定位]
  配置数 / 已建池数 / 上次刷新时间
筛选与刷新：
  名称、只看已建、只看借出、自动刷新、间隔、手动刷新
池列表：
  名称、是否创建、总数/借出/空闲/瞬态
  峰值/创建/复用/失败/待回收/预热目标
  池级过期：已空闲时长（或阻碍原因）/ 池级过期配置 / 预计释放时刻
  展开：当前运行参数、实例分页
实例：
  ObjectField / InstanceID / 状态 / lease / 延时剩余
~~~

池级过期的展示必须区分三种状态，不能只显示一个数字：

| 内部状态 | 面板显示 |
|---|---|
| `IdleExpireBlocked = Disabled` | `整池过期：关闭` |
| `IdleExpireBlocked = ActiveLease` | `整池过期：有 N 个借出，暂停计时` |
| `IdleExpireBlocked = PrewarmPending` | `整池过期：预热中，暂停计时` |
| `IdleExpireBlocked = None` | `已空闲 12.4s / 60s（预计约 47.6s 后释放）` |

只显示倒计时数字而在被阻碍时停住不动，用户会误判为"面板没刷新"或"池不会释放"。同时要在面板上标注"快照时间"，因为释放发生在检查点，面板显示的预计时刻允许有一个 `PoolAutoReleaseInterval` 的误差。

显示 InstanceID、lease 是面向开发者的诊断信息，放展开明细，不挤占总览。不给没有依据的内存估计。

## 4. 逐目标伪代码

### 14-A、14-B：无创建查询

~~~text
绘制面板:
  Edit Mode -> 提示进入播放后查看；只展示已明确绑定的配置
  Play Mode:
    从 ModuleSystem 查询已存在模块
    无模块/未初始化 -> 显示状态，不调用 GameModule getter
    已运行 -> 展示 module.Setting（实际初始化用的资产）
~~~

编辑模式查配置应沿选中 Monitor 所在 GameEntry 的 Settings 关系定位；关系不明确则显示未绑定，不全工程扫描第一个资产。

### 14-C：快照与分页

~~~text
RefreshSnapshot:
  清空并复用本面板 List<GameObjectPoolInfo>
  module.FillPoolInfos(list)
  对当前展开且需要实例显示的池:
    复用 List<GameObjectPoolInstanceInfo>
    填充快照
  保存模块对象身份 + 刷新时间
    // 身份用 ReferenceEquals 比较：模块被 Shutdown 后由 ModuleSystem 清掉缓存，
    // 下次 GetModule 会建新对象，身份变化即可覆盖"重开一局"这个场景

Draw:
  只遍历快照
  先按名字/状态过滤，再分页
  显示 readonly struct 的属性值，不修改内部运行数据
  GameObject 已销毁 -> 显示失效/等待刷新，不访问 transform
~~~

分页减少绘制行数，不代表已消除填充全池实例列表的成本。第一版只获取展开池的实例信息；大量实例时可后续扩充分页查询 API，而不是宣称分页已解决全部性能问题。

### 14-D：刷新与清理

~~~text
Editor 更新回调:
  目标失效/不在播放/自动刷新关闭 -> 不取快照
  面板需要刷新且达到 interval -> 标记刷新请求并有限 Repaint

OnInspectorGUI:
  有请求时才刷新快照
  否则使用缓存（包括实例列表和延时显示）
  手动刷新可显式触发

退出播放 / 模块身份改变 / OnDisable:
  解除订阅
  清空池/实例缓存
  不 Shutdown 模块
~~~

关闭自动刷新时不暗中每次 OnGUI 重新拉实例列表；显示“快照时间”，避免用户把冻结的延时数值当实时。

## 5. 完成标准

- [ ] 14-A Monitor 只有展示配置，不参与运行生命周期。
- [ ] 14-B 未初始化状态可查看且无创建副作用。
- [ ] 14-C 快照、过滤和分页口径正确，失效对象可安全显示。
- [ ] 14-D 刷新受控、退出清缓存，停用面板不关闭池。
- [ ] 池级过期按 `Disabled` / `ActiveLease` / `PrewarmPending` / 计时中 四种状态分别展示，不使用会停留不动的裸倒计时。
- [ ] 类型采用 readonly 数据快照，无结构体 in/out/ref 参数。

## 6. 实施记录

实际展示字段/刷新实现：待填写。  
保留的性能限制：待填写。  
验证：未执行。

下一步：[15：调试操作与可视化接线](15-调试操作与可视化接线.md)。


# GameObjectPool：整池空闲过期需求讨论

日期：2026-10-25  
状态：**计时语义已确认（2026-10-25），并已写入 02–16 步分步文档；实现未排期、未落码、未验证。** 本文是需求的边界与判定依据，步骤文档是可执行合同。  
命名说明（2026-10-25 15:00 最终）：字段名已随实施修订——`PoolIdleExpireTime` → `PoolExpireTime`，`PoolExpireCheckInterval` → `PoolAutoReleaseInterval`，且两者都是**逐池字段**（曾短暂作为模块级 `GameObjectPoolModuleConfig`，该结构体已删除）；运行期读的是 `RuntimePoolConfig` 只读副本，`GameObjectPoolConfig` 只作为 SO 序列化条目。下文出现的旧名按此对应阅读语义，不再单独改动措辞。  
关联：[分步总览](分步执行/00-总览与设计基线.md)、[第 07 步](分步执行/07-销毁清账与池释放.md)。

## 1. 新需求与当前实现的区别

用户需求原文（2026-10-25 第二轮补充）：**"要这个对象池内的所有对象都没有借出的情况下，才开始更新最后一次的使用时间，开始倒计时。"**

即：过期时间不按"最后一次借出"起算，而按"最后一次全部归还"起算。池里还有对象借出时不计时；全部归还的那一刻重置时间戳并开始倒计时。

本轮读取的本地源码事实：

- ObjectPoolModule.Object.cs 的 Spawn / Unspawn 更新**单个对象**的 LastUseTime，使用 DateTime.UtcNow。
- ObjectPoolModule.ObjectPool.cs 的 Update 累加 realElapseSeconds，达到 AutoReleaseInterval 后调用 Release。
- GetCanReleaseObjects 排除 IsInUse、Locked、CustomCanReleaseFlag=false 的对象。
- 默认过滤器按单个对象 LastUseTime 判断过期，再按优先级/时间处理超容量目标。
- 上述定期释放不会移除整个池。DestroyObjectPool 是另一条关闭入口，会调用池 Shutdown，释放其中全部对象，包括占用对象。

因此新增的是“池级过期”，不能直接把原 ExpireTime 重新解释成整个池寿命，也不能不检查占用就调用 DestroyObjectPool。

源码路径前缀：Assets/TEngine/Runtime/Module/ObjectPoolModule/。上述为静态阅读结论，未运行验证。

## 2. 产品行为（已确认）

一个池在所有对象都归还、没有进行中的操作后，连续闲置达到池级过期时间，自动释放该运行池和全部闲置实例。

| 过期检查时的情况 | 行为 | 理由 |
|---|---|---|
| 池未创建，只有定义 | 不处理 | 不为检查而创建池 |
| 运行池总数为 0 | 超时后销毁运行池与其容器 | 空池仍有容器和管理记录 |
| 池内仍有实例，但全部 Idle | 超时后一起销毁这些实例和池 | 空闲对象就是池缓存，无业务占用 |
| 仍有 Spawned | 跳过整池释放 | 不能把长时间使用误认为无人使用 |
| 存在 Preparing / Returning | 跳过 | 生命周期事务尚未结束，避免重入销毁 |
| 仍有有效预热请求 | 跳过 | 不能与显式创建需求对抗 |
| 借出对象挂了延迟回收 | 按借出对象处理，跳过 | 等它真正归还/销毁后再判断 |
| 已有外部销毁通知未清账 | 先走既有清账，再判断 | 不以失真计数决定释放 |
| 正在关闭 | 跳过 | 不重复关闭 |

池内"有对象"不是条件，关键是对象是否仍被借出。Idle 数量大于 0 不阻止整池释放。

自动过期仅走非强制释放，与用户主动"强制释放"、模块 Shutdown 的强制语义分开。

## 3. 计时语义（已确认：借出数量归零才开始倒计时）

### 3.1 用户确认的规则

**池内所有对象都没有借出时，才更新池级的"最后一次使用时间"，从那一刻开始倒计时。**

由此推出三条硬规则：

1. 只要还有任何一个对象处于借出状态，池级计时器**不存在**（不是暂停、不是继续走），判定恒为"不可过期"。
2. 借出数量从 `> 0` 变回 `0` 的**状态转换点**，写入新的空闲起点，开始完整倒计时。
3. 倒计时过程中一旦有人借出，本轮计时**作废**；下次归零时**重新计算完整时长**，不累积、不续接。

第 3 条同时确定了第 8.1 节第 2 项的答案：过期为 60 秒、t=0 借出、t=90 归还最后一个对象，则下次最早在 t=150 之后的检查点才释放。**不是**"归还瞬间发现已超时就立刻销毁"。

### 3.2 不能复用单对象的 LastUseTime

源码事实（`Assets/TEngine/Runtime/Module/ObjectPoolModule/`，静态阅读，未运行验证）：

- `ObjectBase.LastUseTime` 是**每个实例**的字段，`ObjectPoolModule.Object.cs:159/170` 在 Spawn / Unspawn 时写 `DateTime.UtcNow`。
- `ObjectPoolModule.ObjectPool.cs:585/602` 的默认过期过滤器按单对象 `LastUseTime` 排序与比较。

单对象字段无法表达"整池何时最后一次无人使用"：池里有 N 个实例就有 N 个互相独立的时间戳，且这些时间戳仍被底层按对象过期的逻辑读写。把池级语义塞进去会同时污染两条逻辑。

**结论：新增独立的池级字段，不复用、不改写 `LastUseTime`。**

| 字段 | 归属 | 语义 |
|---|---|---|
| `IdleStartSeconds` | GameObjectPool 行为类（不继承 ObjectBase） | 本轮连续空闲的起点。`< 0` 表示"当前存在阻碍项，不处于可过期空闲" |
| `ActiveCount` | 同上 | 当前借出中的实例数量，借出侧的唯一真相 |
| `_pendingPrewarmCount` | 同上 | 该池在途预热请求数，预热侧的阻碍项，见 3.4.3 |

两个阻碍项任一 `> 0` 即不计时。`IdleStartSeconds` 用负数当哨兵，不用"正数极大值"或 `bool + time` 双字段。这样"是否处于可过期空闲"与"空闲多久"是同一个值的两种读法，不会出现标志位与时间戳互相矛盾的中间态。

显示器上如需展示"最后一次使用时间"，把它显示为 `IdleStartSeconds` 对应的**最后归还时刻**，字段名不要叫 `LastUseTime`，避免和单对象字段混淆。

### 3.3 事件到计时的映射（确认版）

| 事件 | ActiveCount | IdleStartSeconds | 说明 |
|---|---|---|---|
| 池创建成功，实例数为 0 | 0 | 写入建池时刻 | 空池也参与过期，符合第 2 节 |
| 池创建成功并直接建满预热实例 | 0 | 预热结束时刻 | 预热期间不计时，见 3.4 |
| 借出成功（0 → 1） | +1 | 置 `-1` | 本轮计时作废 |
| 借出成功（N → N+1） | +1 | 保持 `-1` | 幂等，不重复写 |
| 归还成功（N → N-1，N>1） | -1 | 保持 `-1` | 仍有占用，不进空闲 |
| 归还成功（1 → 0） | -1 | **写入此时刻** | 计时起点，这是本次确认的核心 |
| 延迟回收（DespawnAfter）到期归还 | 按归还路径 | 同上 | 计时前对象一直是占用态 |
| 借出对象被外部 `Destroy` 并完成清账（1 → 0） | -1 | **写入此时刻** | 等价于归还，必须减计数，见 3.5 |
| 该池第一个预热请求入队 | 不变 | 置 `-1` | 预热期间不计时 |
| 该池已有预热在途时再入队 | 不变 | 保持 `-1` | 用计数而非布尔，避免提前判定"预热完毕" |
| 预热请求入队即达标、未真正创建 | 不变 | **写入此时刻** | 视为一次显式使用，见 3.4.4 |
| 该池最后一个预热请求结束（完成/取消/无法增长），且无借出 | 不变 | **写入此时刻** | 预热完毕即刻开始计时 |
| 该池最后一个预热请求结束，但仍有借出 | 不变 | 保持 `-1` | 借出优先，等归零时再写 |
| 查询快照、打开 Monitor、手动刷新 | 不变 | 不变 | 观察行为不续期 |
| 底层按单对象过期清理 Idle | 不变 | 不变 | 清理空闲对象不等于业务使用 |
| `ClearUnused` | 不变 | 不变 | 同上 |
| 池被释放 / 模块 Shutdown | — | 随池对象一起消失 | 不需要额外清理逻辑 |

### 3.4 预热：期间不计时，完毕即刻开始计时（已确认）

用户口径：**预热完毕之后就开始计时，预热期间不需要计时。**

#### 3.4.1 为什么必须这样

预热实例在借出维度上确实"没有借出"（`ActiveCount == 0`），但预热代表一次**显式的创建需求**。若严格按无借出计时会出两个问题：

- 池空闲 55 秒时发起预热，预热 8 秒完成，`now - IdleStartSeconds = 63 > 60`，刚预热完的池在下一个检查点就被销毁，等于白做。
- 分帧预热跨多帧，`ActiveCount` 全程为 0，可能在预热途中就被判过期并释放正在创建的池。

#### 3.4.2 "预热完毕"的精确定义

针对单个池，满足以下全部条件即为预热完毕：

1. 该池的**最后一个**预热请求已终结（`Completed` / `Rejected` / `Closed` / `NoProgress` 任一种，都算终结）。`NoProgress`（无法增长）也算结束，否则不增长的池会永远不过期。
2. 该池请求组为空，或在途请求数为 0。
3. 不存在"创建进行中"的中间窗口：`pool.CreateInstance(false)` 是同步调用（第 09 步 09-C），所以判定点可以放在 `UpdatePrewarm` 当帧结束处，不需要额外的异步等待。

判定时机固定在模块 Update 中 `UpdatePrewarm()` **之后**，与检查点的顺序为：`FlushDestroyed` → `UpdatePrewarm` → 预热完毕结算 → 池过期扫描。

#### 3.4.3 必须用计数，不能用布尔

同一池可能有多个并发预热请求，且第 09 步规定"一个取消不影响另一个"。若用 `bool hasPrewarm`，第一个请求结束时就会误判为"预热完毕"并把计时打开，而其余请求还在建实例。

因此维护 `_pendingPrewarmCount`，并且**只在 0↔1 的转换处动作**：

- `0 → 1`：`IdleStartSeconds = -1`
- `1 → 0`：若 `ActiveCount == 0`，`IdleStartSeconds = 当前时刻`；否则保持 `-1`，等最后一个对象归还时由 `ExitActive()` 写入

#### 3.4.4 入队即达标、从未真正创建的那条路径

第 09 步的 `PrewarmAsync` 在 `TryGetPoolOrCreate` 之后有一个提前返回：

~~~text
if (!TryGetPoolOrCreate(name, out pool)) -> 返回失败   // 同步建池，失败即返回
if pool.Total >= target -> 直接 Completed   // 不入队，不进入请求组
~~~

这条路径上没有"预热期间"，如果什么都不做，池的起点会停留在建池或上次归零的时刻。极端情况：池已空闲 58 秒（`PoolExpireTime` 为 60、检查间隔 5 秒），业务此时发起预热、因总量已达标立刻返回 `Completed`，下一次检查点（第 60 秒）正好命中过期，把一个刚刚被确认"够用"的池销毁了。

因此确认：**任何预热调用，只要它让池被访问，就刷新一次起点**。入队路径由 `0 → 1` 置 `-1` 覆盖；提前完成路径直接把起点写成当前时刻。

这不是"查询续期"——预热是显式创建需求，不是观察行为。查询、Monitor 刷新、`ClearUnused` 仍然不续期。

#### 3.4.5 只写一次，不要每帧写

最常见的错误实现是每帧在 `UpdatePrewarm` 后无条件调用一次"若空闲则写起点"，这样起点每帧都被刷新，池永远不会过期，而且监控上"已空闲时长"永远显示 0 附近的值。

写起点只能发生在**状态转换处**：`ActiveCount` 归零、`_pendingPrewarmCount` 归零、建池、预热提前完成。任何以"当前刚好空闲"为条件、不做转换判定的写入都是错的。

### 3.5 计数必须覆盖所有离开占用态的路径

这是本次设计最容易出错的地方，必须写清：

`ActiveCount` 只减不加会造成**静默泄漏**——业务 `Destroy` 掉一个借出对象后，若只做了底层 `Unspawn` 却没减计数，池会永远停在 "1 个占用"，整池过期永不触发，而且监控上看起来"池一直有人用"。

所有离开占用态的路径必须收敛到同一个方法：

~~~text
ExitActive():
  if 记录的当前状态不是占用态 -> 断言失败，直接返回（防止重复减）
  ActiveCount--
  TryBeginIdle()      // 归零时写空闲起点；带守卫，重复调用不会推后起点
~~~

必须接入的调用点：

1. 正常归还 `ReturnCore` 成功后。
2. 延迟回收到期后走 `ReturnCore` 的那条路径（不额外减，避免双减）。
3. `FlushDestroyed` 中，对象是**借出态**被外部销毁并完成清账后。
4. 借出过程中的失败回滚**不得**调用（`Preparing` 从未提交为占用，回滚直接移除即可）。

反向入口 `EnterActive()` 在借出提交成功后调用：`ActiveCount++` 并置 `IdleStartSeconds = -1`。

`Preparing` / `Returning` 都算占用态：事务未提交前对象既不能被池回收，也不能让池进入空闲。因为计数在提交点增减，这两个中间态天然被覆盖，不需要额外判断。

计数与状态必须能交叉校验：Debug 构建下断言 `ActiveCount == Spawned + Returning`。`Preparing` 刻意不计入——那个阶段池还没把对象交给业务，借出失败回滚直接移除即可；若在 `Preparing` 就加计数，05-E 的每条回滚路径都要记得减一次，漏一条就会让池永远停在"有占用"、整池过期永不触发。`Returning` 必须计入，因为业务此刻仍持有引用。任何一次不配对都要在开发期暴露，而不是等到线上发现池不释放。

### 3.6 时间基准

确认使用模块 Update 传入的 `realElapseSeconds` 累加出的单调秒数（`ObjectPoolModule.Update(float elapseSeconds, float realElapseSeconds)` 已提供非缩放真实时间），不使用 `DateTime.UtcNow`。

理由：`UtcNow` 会被系统时间调整、时区、NTP 校时影响，出现负数间隔或跳过一天；`realElapsedSeconds` 不受 `Time.timeScale = 0` 影响，暂停游戏不会让池"看起来空闲了很久"。

不需要每个池挂 Mono `Update`，也不由 Monitor 面板驱动计时；判定只在模块级检查点进行。

### 3.7 判定与检查点

~~~text
IsIdleExpired(pool, now):
  if pool.PoolExpireTime <= 0 -> false        // 该池关闭整池过期
  if pool.IdleStartSeconds < 0 -> false           // 存在阻碍项
  return (now - pool.IdleStartSeconds) >= pool.PoolExpireTime
~~~

过期时间到期**不等于**立刻销毁，而是在下一次模块检查点生效。检查点在模块 Update 中按 `PoolAutoReleaseInterval` 节流，顺序为：先 `FlushDestroyed` 清账，再推进预热，最后做过期扫描。先清账保证计数与空闲起点已反映本帧的真实归还。


## 4. 可选策略对比

| 策略 | 有借出对象时 | 优点 | 成本/风险 |
|---|---|---|---|
| 连续闲置后释放（推荐） | 保留，最后归还后重新计时 | 简单，缓存复用机会稳定，业务对象不被自动销毁 | 长期未归还对象会让池长期存在 |
| 到期后等待归还即释放 | 到期记为待关闭，清空占用后释放 | 能更积极释放已过期池 | 要定义新 Spawn 是否撤销过期、预热是否允许，增加状态 |
| 到期强制销毁 | 借出与空闲全部销毁 | 严格按时间清理 | 业务引用失效、特效/角色中断，不能用于通用池默认行为 |

建议第一版只实现一种明确的自动策略，不急着加入三个模式供每个池选择。长期未归还是归属/业务生命周期问题，后续可以通过监控暴露，不能靠自动销毁掩盖。

**已确认选择第 1 行（连续闲置后释放）**，第 2 行"到期后等归还即释放"被第 3 节的重新完整计时规则直接排除，第 3 行"到期强制销毁"明确不实现。

## 5. 配置（已确认默认值）

保留原有实例 `ExpireTime` 和 `AutoReleaseInterval`；它们是**单对象**语义，与本文的池级配置并存，互不替代。新增：

| 字段 | 位置/规则 | 默认值 | 含义 |
|---|---|---|---|
| `PoolExpireTime` | 单池配置（`GameObjectPoolConfig`）；有限值 >= 0 | **0（关闭）** | 连续空闲多少秒后自动释放运行池与全部 Idle 实例 |
| `PoolAutoReleaseInterval` | 每个池条目自己的字段；有限值 > 0 | 5 秒（建议起始值） | 该池扫描整池过期的节流间隔 |

**`PoolExpireTime` 默认 0 = 关闭**，是本次确认的结论：不写这条配置的池永远不整池过期，现有行为零变化；需要省内存的池逐条显式开启，避免一次性把已有池的生命周期全部改掉。

注意校验规则与实例 `ExpireTime` 不同：`ExpireTime` 必须 `> 0`（0 会让底层过滤条件失效），而 `PoolExpireTime` 的 `0` 是**合法且有意义**的关闭值，只有负数、NaN、Infinity 报错。第 02 步的校验器必须为这条字段单独写分支，不能直接套用"时间必须 > 0"的通用规则。

需要频繁复用的池可关闭池级过期；不以 `PrewarmCount > 0` 自动认为永久常驻。

时间使用模块 Update 的 `realElapseSeconds` 累加单调秒数，不用 `DateTime.UtcNow`。没有每池 Mono Update，也不由 Monitor 更新。到期意味着"达到阈值后的下一次模块检查"，不是精确定时器。

**自动释放保留配置与 SO 定义（已确认）**：只销毁运行池、容器与实例，下次 `Spawn` 通过 `TryGetPoolOrCreate` 懒建同名新运行池并分配新世代。不因为自动过期而删除配置，否则后续按名字借出会永久失败。

同名重建不自动重跑启动预热；正常 `Spawn` 按需创建，只有显式预热请求才补足目标。否则会出现"释放后又被后台预热建回来"的循环。

释放不销毁 SO 引用的源 Prefab，也不承诺卸载 Prefab 依赖资源。全模块公共根节点继续存在。

## 6. 实现思路（讨论用伪代码）

### 6.1 三个入口与两个阻碍项

池级计时由两个阻碍项共同决定：借出数量 `ActiveCount`、在途预热请求数 `_pendingPrewarmCount`。任一项 `> 0` 就不计时。三个写入点全部收敛到带守卫的 `TryBeginIdle()`。

~~~text
// 私有辅助：只在状态转换后调用，绝不每帧无条件调用
TryBeginIdle():
  if ActiveCount == 0 and _pendingPrewarmCount == 0 and IdleStartSeconds < 0:
    IdleStartSeconds = 单调当前秒
  // 第三个条件 IdleStartSeconds < 0 是冗余守卫：
  // 已经在计时时重复调用不会把起点往后推，误调用也不会变成"永不过期"

// 池内某对象进入借出占用态（Spawn 提交成功后）
EnterActive():
  ActiveCount++
  IdleStartSeconds = -1        // 幂等：已有占用时重复置 -1 无副作用

// 池内某对象离开借出占用态（归还成功 / 借出对象被外部销毁并完成清账）
ExitActive():
  断言 该记录当前是占用态        // 防止重复减造成计数偏低
  ActiveCount--
  TryBeginIdle()                // 归零这一刻才开始倒计时

// 预热请求入队（该池从无预热变为有预热）
OnPrewarmEnqueued():
  _pendingPrewarmCount++
  if _pendingPrewarmCount == 1:
    IdleStartSeconds = -1        // 只在 0→1 转换时写，避免重复

// 预热请求终结：Completed / Rejected / Closed / NoProgress 都算
OnPrewarmSettled():
  断言 _pendingPrewarmCount > 0
  _pendingPrewarmCount--
  TryBeginIdle()                // 预热完毕即刻开始计时

// 预热调用因 Total >= target 提前完成、从未入队的情况
OnPrewarmShortCircuited():
  断言 _pendingPrewarmCount == 0
  IdleStartSeconds = -1
  TryBeginIdle()                // 视为一次显式使用，续期一次

// 建池（2026-10-25 19:20 起并入构造函数末尾，不再有独立的 OnPoolCreated）
GameObjectPool 构造函数末尾:
  if PrewarmCount <= 0:
    TryBeginIdle()              // 空池也从建池时刻开始计时
  else:
    _pendingPrewarmCount = 1
    module.EnqueuePrewarm(this) // 预热结算时再 TryBeginIdle，见上
~~~

不变式：`IdleStartSeconds >= 0` ⇔ `ActiveCount == 0` 且 `_pendingPrewarmCount == 0`。

Debug 下断言这条，能一次性抓出三类错误：忘记减借出计数、忘记结束预热计数、在非转换点重复写起点（后者表现为"已空闲时长恒为 0、池永不过期"）。

### 6.2 模块检查点

~~~text
模块 Update(realElapseSeconds):
  monotonicNow += realElapseSeconds
  FlushDestroyed()                 // 先清账，保证计数与空闲起点反映本帧真实归还
  UpdatePrewarm()                  // 内部在请求终结时调用 OnPrewarmSettled
  if monotonicNow - lastCheck < PoolAutoReleaseInterval:
    return
  lastCheck = monotonicNow
  candidates.Clear()
  for 每个运行池:                   // 只读判定，不在遍历中移除字典元素
    if 池正在关闭 -> skip
    if not IsIdleExpired(pool, monotonicNow) -> skip
    candidates.Add(池身份 + 世代 + 池引用快照)
  for 每个候选:
    复核 池对象仍是同一实例、世代未变、ActiveCount 仍为 0、_pendingPrewarmCount 仍为 0
    复核 IsIdleExpired 仍然成立（检查点之间可能已有新借出）
    调用现有非强制释放入口 ReleasePool(name, force: false)
    失败 -> 记录诊断并保留该池，下次检查点重试，不在同帧重试
~~~

**空闲起点只在状态转换处写，不在扫描时写**。这一点是本次确认的直接结论：如果扫描时观察到 `ActiveCount == 0` 才发现空闲，那么"归零到现在"这段时间就丢了，倒计时会永远从检查点开始算，比配置值多等最多一个检查间隔。

候选用 readonly struct，按值保存池身份、世代等简单数据，不使用 in/out/ref。池的可变生命周期状态仍放现有 GameObjectPool 行为类，不为了结构体偏好复制管理对象。运行快照用 readonly struct 增加 `IdleSeconds`（已空闲多久）与 `BlockedReason`（有借出 / 预热中 / 已关闭过期）字段。

释放入口必须在同一主线程事务中重新检查上述条件，不能复用"只检查 Spawned"的不完整判断。复用正常关闭流程，使旧延时、旧销毁通知、旧预热请求无法操作新世代。

## 7. 确认后对分步文档的影响

不放在编辑器阶段才实现，按现有编号就地补充：

- **第 02 步**：`GameObjectPoolConfig` 增加 `PoolExpireTime`（默认 0）与 `PoolAutoReleaseInterval`（默认 5），两者都是逐池字段；校验器为 `PoolExpireTime` 单列"0 合法、负数/非有限报错"分支，不复用 `ExpireTime` 的 `> 0` 规则。
- **第 03 步**：模块字段 `monotonicSeconds`（由 `realElapseSeconds` 累加）、`lastPoolExpireCheck`；Update 顺序固定为 `FlushDestroyed` → `AdvancePrewarm` → `TryExpireIdlePools`。
- **第 04 步**：`GameObjectPool` 增加 `ActiveCount`、`_pendingPrewarmCount`、`IdleStartSeconds` 与私有辅助 `TryBeginIdle()`；建池成功后调用 `OnPoolCreated()`；记录里不新增池级时间字段。
- **第 05 步**：借出提交成功后 `EnterActive()`；失败回滚不触碰计数。
- **第 06 步**：归还成功后 `ExitActive()`，这是倒计时起点的主要来源。
- **第 07 步**：`FlushDestroyed` 中借出对象被外部销毁并完成清账后调用 `ExitActive()`；新增"自动整池释放"入口，内部走 `ReleasePool(force: false)` 并保留配置。
- **第 08 步**：延迟回收到期走正常归还路径，不额外减计数；计划取消不影响计数。
- **第 09 步**：池维护 `_pendingPrewarmCount`；`PrewarmAsync` 入队时 `0→1` 置起点为 `-1`，请求终结（含 `NoProgress`）时 `1→0` 且无借出则写起点；`Total >= target` 提前返回的分支单独调用一次 `OnPrewarmShortCircuited`；`UpdatePrewarm` 顺序在 `FlushDestroyed` 之后、过期扫描之前；同名重建不自动预热。
- **第 10 步**：快照增加 `IdleSeconds`、`IsIdleExpireEnabled`、`BlockedReason`（有借出 / 预热中 / 已关闭过期）。
- **第 11—12 步**：保持懒重建，Shutdown 不参与池级过期逻辑。
- **第 11 步**：启动预热结束后池级计时随即开始，常驻池的 `PoolExpireTime` 取舍必须按真实流程确定（否则停在加载界面时会白预热）；不引入隐式启动保护期；Shutdown 不读该配置。
- **第 13—15 步**：Inspector 与 Monitor 展示 `PoolExpireTime`、已空闲时长、预计释放时间，明确显示"有 1 个借出，暂停整池过期"这类原因，而不是只显示一个不动的数字。
- **第 16 步**：验收用例补：空池过期、全 Idle 过期、有借出不过期、长占用后归零重新完整计时、外部销毁借出对象后能归零、预热中不过期、预热完重新计时、同名懒重建、查询不续期、`PoolExpireTime = 0` 永不过期。

预计修改现有步骤即可，不必重排编号；若第 07 步篇幅过大，再把"自动整池释放"拆成独立运行功能文档，仍须位于编辑器阶段之前。

## 8. 已确认结论与剩余待定项

### 8.1 本轮确认（2026-10-25）

| 编号 | 问题 | 结论 |
|---|---|---|
| 1 | 过期时仍有借出对象 | 保留，不进过期判断，不实现强制销毁 |
| 2 | 最后一个对象归还后 | **重新等待完整空闲期**，不是"发现已超时就立刻释放" |
| 3 | 空闲起点何时写入 | **借出数量归零的那一刻**，由状态转换触发，不由扫描触发 |
| 4 | 预热期间 | **不计时**；预热完毕（最后一个请求终结）**即刻开始计时**，从这一刻起算完整时长 |
| 5 | `PoolExpireTime` 默认值 | **0 = 默认关闭**，逐池显式开启 |
| 6 | 过期后的配置 | 保留定义，允许下次 `Spawn` 懒建新池、分配新世代 |
| 7 | 其余细节 | 一起销毁全部 Idle；查询与 `ClearUnused` 不续期；实例级 `ExpireTime` 保持原语义不变 |

### 8.2 仍待定（不阻塞讨论，实施前需定）

1. `PoolAutoReleaseInterval` 的正式默认值（当前只给了 5 秒的建议起始值，未验证）。
2. 一次性销毁实例数量很大（例如数百个）时是否要分帧。第一版按一次性销毁实现，若实测有帧尖峰再拆。
3. Monitor 面板对"已空闲时长"的刷新是否与检查点同频。

当前仅保存讨论内容，没有修改已有实施步骤、功能代码或 Unity 资产，没有运行编译/测试/Play Mode；本文所有源码结论均为静态阅读。


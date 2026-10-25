# 伤害、属性、Buff与部位破坏

日期：2026-10-25；v0.1。此页主要是原文04中“数值系统”和“削韧/部位耐久”的必要补全，不冒称原文已经定义这些规则。

## 1. 战斗结果职责

HitResolver决定这次接触属于什么命中；DamageCalculator计算结果；EffectSystem提交HP/护盾/Buff等变化；ReactionResolver提交受击请求；UnitFSM决定最终状态。UI仅显示已提交值。

## 2. 建议最小属性集

HP/MaxHP、Stamina/MaxStamina、Attack、Defense、Poise/MaxPoise、MoveSpeed。冷却由AbilityRuntime维护，不作为浮点倒计时散落每个技能。属性有Base、Modifier集合、Current；HP与Stamina是可变资源，不每帧把它们重置成属性基值。

演示伤害规则：

```text
raw = max(0, Attack * motionValueBps / 10000 + flatBonus)
mitigated = raw * 100 / (100 + max(0, Defense))
damage = floor(mitigated * partMultiplierBps / 10000)
shieldTaken = min(Shield, damage)
hpTaken = min(HP, damage - shieldTaken)
```

倍率以基点表达仅为便于配置和减少歧义，不意味着所有运算已采用确定性整数。实际实现须规定中间精度、溢出和取整位置。上式为**自拟演示公式**，不是必须采用的最终数值体系；首版不加入暴击、元素和随机伤害。需要时使用IDamageRule替换，不在所有系统里复制公式。

## 3. 不同游戏用规则集适配

`StandardActRuleSet`：HP、格挡、削韧、部位倍率生效。`SimpleActRuleSet`：按既有玩法提案执行有效攻击一击必杀、弹刀/砍弹/精准闪避奖励、速度档门槛与时停；具体争议仍沿用待定标记。两者共享命中管线，不能让通用公式先扣一滴再由另一个监听器杀死目标。

耐力消费与回复有稳定顺序：P2动作开始扣费，P5命中奖励回复；不能用本帧尚未提交的未来回复支付已经拒绝的动作。数值最终clamp到合法区间。

## 4. Buff 数据与生命周期

| 字段 | 内容 |
|---|---|
| buffId/sourceId | 定义及施加来源 |
| durationTicks/clockDomain | 持续时间及计时域 |
| stackKey/maxStacks | 按技能、来源或全局同名聚合 |
| stackingPolicy | Reject / Refresh / AddStack / Independent |
| modifierList | 属性加法/乘法/覆盖；覆盖同级按稳定优先级 |
| grantedTags | 添加的权限/状态标签，引用计数按来源管理 |
| periodTicks/firstTickPolicy | 周期效果，首次立即或满周期后 |
| removeOnDeath/dispelTags | 移除与驱散规则 |

`Apply → Active → Expire/Dispel/Death → Remove`，Remove只执行一次。Refresh只刷新到期，不重加同一属性修饰器。两个Buff都授予Silence时，移除一个不能把另一个的禁言也取消；使用来源句柄/计数解决。

示例：灼烧duration=180、period=60、首次满周期后、区间 `[start,start+180)`；在+60、+120各触发一次，+180先到期不触发。若想三次，明确选择首次立即或含末次Tick策略，不能凭实现顺序碰运气。

新增Buff在P5提交后立即影响其后的阶段和下一Tick；不回溯改变同一P5批次已采样的伤害。链式效果生成下一Tick命令，设最大传播深度和来源链ID，防反伤互递归。

## 5. 削韧和反应

普通伤害与PoiseDamage独立。霸体可以免普通HitStun但仍扣HP和韧性；韧性归零生成BreakReaction，提交一次破韧事件，然后按恢复策略重置或冷却恢复。击退方向取命中时快照，避免攻击者稍后转身导致受击方向改变。

死亡优先级高于破韧和普通硬直；死亡后不再接受普通伤害或Buff，复活是显式命令并分配新代次/重置规则。已有结果仍可用于战斗日志与奖励。

## 6. 部位破坏的完整闭环

PartDefinition：id、命中优先级、HP倍率、初始耐久、部位伤害倍率、破坏后倍率、可否继续命中、一次性反应、表现变体。

PartRuntime：remainingDurability、broken、breakEventEmitted。一次命中只对选中部位扣耐久；从未破坏跨到破坏时发PartBroken一次。破坏可请求倒地或Boss阶段变化，但仍通过UnitFSM/阶段系统仲裁。掉落属于奖励系统的事务，不在破坏监听里无限生成。

默认同一伤害先按破坏前倍率计算，再提交部位破坏；后续命中用新倍率。死亡与部位破坏同Tick可同时记录事实，但表现最终为死亡，奖励按一份结算事务去重。

## 7. 参考、落点和验收

参考 [EGamePlay](https://github.com/m969/EGamePlay) 的能力/执行分离、[Unity GAS](https://github.com/sjai013/unity-gameplay-ability-system) 的Definition/Spec与效果持续、[Unity-Buff-System](https://github.com/NoSLoofah/Unity-Buff-System) 的生命周期和叠层接口。借鉴职责，不导入其独立时钟、全局管理器或完整依赖。

拟新增 `Core/Battle/Effect/`、`Attribute/`、`Part/` 和相应Luban定义。未来用例：护盾溢出、Refresh不叠乘、同源/异源叠层、末帧DOT、死亡与破部位同帧、Tag来源计数、负伤害边界；本次未执行。

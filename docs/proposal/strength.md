# Predict & Reduce Strength

未裁决候选。不是当前规范，不得直接据此修改生产代码。

## Current baseline

主 Work Session、Companion、Prompt Authority、Fallback、Projection 与持久化行为由现有
`COMPANION-`、`PROMPT-`、`FALLBACK-`、`PROJ-`、`PERSIST-` 条款定义。当前系统没有
Strength Replica、投机只读请求、候选 delegation frame 或训练控制器。

Projection Algebra 的正式迁移仍有活跃差距，见 `status/projection-algebra-gap.md`。
本 Proposal 依赖该正式基线，不复制或修订 PROJ-008。

## Proposed delta

增加一个默认关闭的、有限深度的只读投机执行功能：主模型即将进行机械性调查时，独立的
低成本 Replica Session 可提前执行最多两个 provider 请求；其结果只有在主 attempt 确认
消费后才进入主会话的活动投影。

### Session 与权限

- 每个主 Work Session 至多关联一个叶子 Replica；Replica 没有自己的 Companion。
- Replica 使用内部 fast/deep tier，仅用于模型绑定；不出现在 provider 可见的 agent enum、
  fork schema、list 或 join 结果中。
- Replica 的 provider-visible schema 和 runtime execution gate 双重限制为已裁决的只读工具。
  任一边界无法证明权限时 fail closed 到正常主模型流程。
- 主模型与 Replica 都不接收 Strength 来源标记或预算提示；Replica 的自然文本输出丢弃。

### 预算与预测

- 预测器只输出 `K ∈ {0,1,2}`，按 provider 请求计数，不按工具调用计数。
- 一次请求内并发的只读调用作为一个批次；预算耗尽后不再发送 Replica provider 请求。
- 预测状态只按主会话的 CanonicalRole 分桶。策略参数集中为代码常量，不新增运行时配置面。
- 训练采样使用确定性选择和慢速负反馈；目标是稳定、成本合理的工作点，不声称找到反事实最优策略。
- Replica 失败、超时、取消或任何不确定结局均丢弃候选并回到正常主请求；不推进 Fallback，
  不改变 SelectedAgent、EffectiveAgent 或 PrefixEpoch。

### Candidate → consume → promote

1. Replica 的只读调用及结果先形成候选 frame，持久化身份与内容，但不进入活动历史。
2. 候选只对绑定的首次主 attempt 可见。
3. 主 provider run 的 input seal 必须证明消费了该候选 digest。
4. 证明成立后 promote；已提升 frame 才可进入后续主投影和 Companion ingestion。
5. 失败、未消费或身份不匹配的候选退休，不回滚主历史。

提升后的工具批次在主投影中与普通历史工具调用具有相同 canonical wire；来源只存在于内部事实，
不得泄漏给模型。Event identity 必须支持字节级去重，并防止 candidate 经 Companion 反射回主输入。

### Persistence and recovery

- Replica 关联、候选、消费证明、提升和退休使用 append-only facts；外部创建/发送遵循
  PERSIST-009 的 requested/accepted 纪律。
- Journal fold 产出唯一活动关联和 frame 集，不从 Session 列表、路径或 transcript 猜测。
- 重启时只恢复可证明的关联和已提交事实；未决效果先 reconcile，不能盲目重发。
- 所有 provider-visible projection 必须使用正式 Projection Algebra；本候选不建立旁路 renderer。

### Safety and circuit breaking

- 最大损失由 `K≤2`、只读双门、候选后提升和确定性输入界共同限制。
- 质量/成本反馈超过裁决阈值时自动关闭新候选；已有主流程不受影响。
- Host 不支持安全的独立挂起、取消、权限收窄或 input seal 绑定时，功能保持关闭。

## Impact map

- what：Agent 可见性、Companion 资格、Prompt attempt、Projection frame、持久化与恢复行为。
- shape：内部 Replica session、只读权限双门、candidate writer、promotion writer、projection ownership。
- how：预测器、请求预算、候选生命周期、recovery fold、控制反馈。
- proof：Host capability canaries、权限反例、投影性质、故障注入、K1/K2 灰度与熔断。
- code/resources：Agent 配置、Session/Journal/Projection/Prompt 适配与内部 prompt assets。

## Alternatives

1. 同一主 Session 临时切换低成本模型：拒绝；会混合身份、权限、前缀和 transcript。
2. 让 Replica 写入或执行命令：拒绝；错误投机损失不可接受。
3. 无限只读连读：拒绝；成本和调查偏移无界。
4. 在主输入标记“由 Replica 产生”：拒绝；会改变主模型策略并破坏无感知目标。
5. 直接提交 Replica 结果：拒绝；没有主 attempt 消费证明。
6. 只做 shadow prediction、不执行 Replica：可作为迁移阶段，但不能验证投影和权限闭环。

## Migration / cutover

1. 先完成正式 Projection Algebra 的现有活跃迁移，不借本 Proposal 改写 PROJ-008。
2. 用当前 Host 源码/发布产物验证 Session 隔离、schema 过滤、runtime gate、transform 生命周期、
   取消和 seal 绑定；建立 canary。
3. 先运行 shadow predictor，只记录确定性决策与成本，不创建 Replica。
4. 接入 Replica dry-run，结果不进入主投影。
5. 接入 candidate/consume/promote，先启用 K1；通过独立质量和成本门后才允许 K2。
6. 任一阶段无法证明安全边界时回到默认关闭；不保留半启用兼容路径。

## Compatibility disposition

Compatible when disabled。启用前必须对新增内部 Agent 配置、Journal schema 与投影版本作出
ExplicitMigration 或 ExplicitReset 的独立裁决。

## Proof plan

- Host canary：Session 隔离、不可见性、只读 schema/runtime 双门、挂起不阻塞其它 Session、
  取消/超时可收敛、provider run 与 input seal 可绑定。
- 纯性质：`K` 只取有限集合；采样和反馈同输入同输出；candidate 不进活动投影；promotion 幂等；
  canonical order、digest 与 event 去重稳定。
- 故障注入：创建/发送/append 的 committed、unknown、重启和重复回调均得到唯一 fold。
- 安全反例：伪造写工具、来源标记泄漏、跨 Authority Root 复用、未消费 promotion 必须判红。
- 灰度：K1 和 K2 分开门禁；成本、输入字节和质量指标触发自动关闭。

## Decision owner

Wanxiangshu 项目 Owner。

## Admission blockers

- 正式 Projection Algebra 活跃差距尚未关闭。
- 必须用当前 Host 证据确认独立 Session 的权限过滤、transform 生命周期、取消和 seal 绑定。
- 需要裁决 Replica 的内部角色/Agent 配置、Journal schema 兼容策略和精确策略常量。
- 需要先定义可审计的质量与成本指标及熔断判据，不能以“总体更强”作为验收。

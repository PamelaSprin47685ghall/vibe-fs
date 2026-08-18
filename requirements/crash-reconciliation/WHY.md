# crash-reconciliation — 存在理由

## 一句话 WHY

进程/插件中断会丢失 process-local 状态，却**不会自动撤销已经发生的外部事实**；恢复必须从
durable facts + 可信物理 observation 重新进入普通程序，不能从缓存、时间或「上次大概做到哪」猜状态。

## 为什么这个 WHY 不可替代

崩溃不是「一次 attempt 失败」。attempt 失败（`provider-attempt-recovery`）是业务层面已经确认的
失败——Host snapshot 说清了结局；崩溃是进程失忆——结局未知、在途效果未知、临时状态全部蒸发。
两者 failure meaning 完全不同（HANDOFF §6.5/§7.7）：

- attempt 失败：世界知道「这次没成功」，问题是下一步换谁继续；
- 崩溃：世界只知道「曾经想做什么」，问题是「哪些事真的发生了」——只有 durable facts 与可信物理
  观察能回答，任何内存/日志/时间猜测都是编造。

把两者合并成一个 recovery 概念，会让失败计数与恢复预算互相污染，也会让「重启后凭印象继续」的
假恢复混进普通程序。

## 世界什么时候 RED

- 重启后必须相信临时内存 / 日志 / 时间猜测才能继续（armed 标志、permit、waiter、sensor 被当
  成恢复权威）；
- outcome unknown 的外部 effect 被当作「未发生」而重放（requested≠accepted 的分型被绕过）；
- 恢复发明永久 `RecoveryStage` 程序计数器，而不是重入普通 workflow 入口；
- 证据 ambiguous / multiple / missing 时不是 fail closed，而是挑一个最像的继续；
- 没有 fresh evidence 却自动发出 effect（恢复期间自动 continuation、自动 join）；
- 用户显式 `/continue` 后，系统声称有 restart briefing，却没有把该 briefing 放进本次真实 provider-visible user material；LLM 因而只能从旧 transcript 猜「上次大概做到哪」，把已经成功的新 tool result 再解释成仍未完成并原地重试。

## 与相邻包的边界

| 看似邻近的事实 | 归属 | 为什么 |
|---|---|---|
| 单次 attempt 失败后换 binding | `provider-attempt-recovery` | 已确认失败 ≠ 进程失忆 |
| 病态重复输出的止损 | `degeneration-guard` | 是「中止当前 attempt」，不是恢复 |
| effect 的 Requested/Accepted 分型 | `effect-accounting` | 本包消费「unknown 不重放」的 guarantee，不定义 effect law |
| durable events 的 append/fold 法律 | `durable-events` | 本包只把 projection 当恢复输入 |
| 流程由语言结构表达 / 无程序计数器 | `structured-workflow` | 本包消费「恢复重入普通流程」的形态，FLOW-005/DSL-004 的规范 owner |
| Host 的最小物理能力与观察边界 | `host-boundary` | snapshot 是可信物理观察的来源 |
| managed session 的创建/复用/替换 | `managed-session-lifecycle` | Attached restore 的 domain 规则局部应用，生命周期合同 owner 是它 |
| 单条 recovery 决策的 domain 语义 | 各 domain owner（ORCH-007 → `change-integration` 等） | 本包只拥有「恢复从 durable+physical 重入」的通用纪律 |

## 本包不拥有的具体恢复规则（DOES NOT OWN）

各 domain 的具体 durable facts、effect Requested/Accepted law、provider-attempt retry、
managed-session replacement / publish reconcile 等 domain-specific 恢复规则——它们归各自的
domain owner；本包保证这些恢复规则运行在**正确的恢复地基**上：durable + 可信物理观察 + fail
closed + 无程序计数器。

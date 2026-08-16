# output-distillation — 可观察合同

本文件是 `output-distillation` 包的唯一 normative 语义合同。证据指针 → `PROOF.md`。

## DISTILL-001：大输出有损但诚实地压成 bounded observation

当执行输出大到不能原样进入 participant horizon 时，系统把它压成 bounded observation；**不得**静默
截断成「成功空结果」（EXEC-012）。压缩是有损的，但每个损失都必须是诚实的选择——保留改变后续判断
的事实，丢弃重复、progress noise、横幅、spinner（`resources/provider/role/distiller/`）。

含义/动机：截断冒充成功是 RED 的第一形态；「诚实」= 摘要必须携带它声称携带的信息，且不假装看见了
超出其获的东西。

证据：NEW `tests/distiller-fragment-humility.test.mjs`（失败时承认不完整而非报成功）；anchors
`distinguishing`（双语言命中）。

## DISTILL-002：保留会改变后续 judgment 的事实

压缩保留：error type、带行号的 path、失败的 assertion、panic/exception、无法同时为真的矛盾行、
约束主张的 counts、仍携带伤口的相关 raw tail（Role Law）。不按惯例整类保留（不是每条 stack/path/
count 都留）；保留那些把这次 failure/conflict/未决 condition 与泛泛失败故事区分开的具体印记。

含义/动机：下一动作由这些区分性事实决定；丢一个关键 condition = 下一动作走错方向。

证据：anchor `distinguishing`；REUSE `requirements/output-distillation/tests/large-gate.test.mjs`（预算合同，见 DISTILL-011）。

## DISTILL-003：fragment 谦逊——沉默的 fragment ≠ 整体成功

一个 fragment 里没有 failure 文本 ≠ 全局成功；截断正文上方的绿色 header 不是裁决；当 fragment
看不见整体时，说出它所能看见的并让边界保持可见（Role Law「保持 fragment 的谦逊」）。蒸馏结果
**必须**承认 fragment 的视野边界，不得把局部当整体。

含义/动机：fragment 的视野边界是认识论事实；伪装成整体成功会污染下游全部判断。

证据：NEW `tests/distiller-fragment-humility.test.mjs`（summary 命中
`/Condensation incomplete|Most recent raw output/`、不含 `summary-for-<failedId>`）；
anchor `fragment-humility`（`Do not manufacture success`）。

## DISTILL-004：合并多个 fragments 不发明因果或成功率

合并按实质性 failures 的并集进行；保留冲突、不调和成更光滑的故事；一个具体 failure 不会被许多
安静的 chunk 投票否决，许多安静 chunk 也不构成「那次 failure 并不真实」的证据（Role Law
`merge-conflicts`：`not outvoted`）。不得猜测 cause、不得补全缺失 evidence、不得把听起来合理的解释
升级成 finding（`no-invented-causality`）。

含义/动机：因果与成功率是发明物；合并的唯一合法运算 = 保留冲突的并集。

证据：anchors `merge-conflicts` / `no-invented-causality`（双语言命中）；NEW
`tests/distiller-fragment-humility.test.mjs`（失败者被诚实保留而非投票消除）。

## DISTILL-005：蒸馏结果对未见过原始输出的 reader 仍可用

一份蒸馏结果必须对从未见过原始大体量文本的读者仍然可用（Role Law）；它不该假装看见了超出其所获
的东西。可定位性 = 读者能凭摘要中的 path/行号/失败痕迹回到现场。

含义/动机：蒸馏的下游读者只看到摘要；不可定位的摘要 = 没有观察。

证据：anchor `locatable-to-unseen-reader`；NEW `tests/distiller-fragment-humility.test.mjs`
（verbatim 含 marker——原始 chunk 的定位痕迹必须存活）。

## DISTILL-006：失败路径 = partial account + 最后 chunk 原始 tail

map/reduce 任一 agent 失败（NotFound 硬失败 / 真超时）：不 throw、不冒充完整成功；产出 partial
account + 最后一个 chunk 的原始字节（`raw_tail`）作为「最近的原始输出」（`Distillation.partialWithTail`：
`CondensationIncomplete` 带 account、`CondensationUnavailable` 无 account，二者都带 raw_tail）。
失败 agent 的 work record 不得出现在 summary 中（不虚构成功）。

含义/动机：失败时保留最近原始输出 = 对未见过原文的读者诚实交代「我们只压缩到这一点」；
虚构失败者 summary = 把 fragment 当整体。

证据：NEW `tests/distiller-fragment-humility.test.mjs`；MOVE
`tests/executor-summarize.test.mjs`（`EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`、
`EXEC_distill_spool_await_not_found_hard_fail_collects_failure`、`EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`）。

## DISTILL-007：蒸馏输入是 spool；chunked map + online reduce

蒸馏消费 `ProcessOutcome.Spooled` 的 spool 文件（`Spool.readChunks` 按 204800 B 分块）；每 chunk 一个
map agent（`distillFragment`），结果按 chunk index 顺序等待；在线 reduce（`rippleInsert`，扇入 8）
把片段逐级归并成单一 account；任一 map 失败 → `cancelOwned()` 取消全部 owned map/reduce agents，
同时已成功的 siblings 仍各自 await 完成（不 skip）。

含义/动机：并行 map 收敛成确定性单账户；失败立即止损（cancelOwned），但已发起的观察不被丢弃。

证据：MOVE `tests/executor-summarize.test.mjs`（`EXEC_distill_spool_targeted_await_one_call_per_agent_no_stash`、
`EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent`、`EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`）。

## DISTILL-008：每 chunk 定向 await 一次；permit 门分型

每个 chunk 的 map agent 恰好一次 `AwaitAgentWithPermit`（无 stash skip、无跨 agent await）；
`RECOVERY_WAITING` → `ForkError.TimedOut`（等 journal readiness 信号后再一次 fresh permit check，
无 timer 驱动重探）；`FamilyBlocked` / 真 join 超时 → `ForkError.NotFound`（hard fail，不重试）。
纯 ForkRuntime 无 journal → fail closed（不铸造 synthetic permit）。

含义/动机：等待必须每次过 permit 门（恢复线性序），且失败分型决定「等一等」还是「认栽」；
synthetic permit 会伪造恢复就绪。

边界：permit 门与恢复线性序的完整法则 → `crash-reconciliation`；本包只拥有蒸馏侧的等待契约。

证据：MOVE `tests/executor-summarize.test.mjs`（`EXEC_distill_spool_family_waiting_waits_for_readiness_before_one_fresh_permit_check`——
call order `[permit, readiness, permit]`；`EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent`）。

## DISTILL-009：Distiller 是私有 runtime，不进公开 fork/horizon

Distiller 映射子会话是私有 runtime，不暴露为公开 `fork` / `horizon` 目标（EXEC-014、AGENT-008）；
map/reduce、chunk、session id 属机器 Assignment，不进 provider 工具面；durable handle =
`HostOwnedHidden`（对父 list/join/horizon/background guard/父恢复不可见，记录仍持久）；`run` 工具
同步掌控 Distiller 生命周期（fork → permit-gated await → 摘要 → 返回），调用方不 join。

含义/动机：蒸馏是 Host-owned workflow 的私有机制；暴露给 provider 会泄漏机器 Assignment 并把
内部 worker 变成可委托目标。

边界：隐藏 handle 的生命周期管理 → `managed-session-lifecycle`；Assignment 字段的 horizon 过滤 →
`participant-horizon`。

证据：REUSE `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`（`EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible`）；`tests/distiller-role-contract.test.mjs` 通过 `OpenCode/Tools/DistillationSurface.fs` 观察私有角色合同。

## DISTILL-010：Distiller 不执行、不改变世界、不裁决

蒸馏 role 不执行命令、不改变世界、不判断 implementation 是否值得 acceptance（Role Law）；
职责是「在长度压力下做选择」。`run`（执行）与 distill（压缩）是两个不同 office 的工具面。

含义/动机：执行证据的产生（process-execution）与执行证据的压缩（本包）必须由不同 authority 承担；
把二者合一 = 蒸馏者自己制造自己总结的证据。

证据：anchor `no-execution`（distiller Role Law 双语命中，与 `process-execution` 边界互证）；
`tests/distiller-role-contract.test.mjs` 通过 `OpenCode/Tools/DistillationSurface.fs` 断言零权限与唯一 `run` 执行面；REUSE
`requirements/process-execution/tests/executor-tool.test.mjs`（`RUN_*` 断言 run ≠ distill，见
`process-execution` PROOF.md）。

## DISTILL-011：Large Gate 与输出预算合同一致；禁无界缓冲

大输出进程的并发由单持有者 gate 约束（`Process/LargeGate.fs`：FIFO cancelable waiters、first holder
wins、release 泵队）；输出预算合同（estimate → threshold）与 gate 行为一致；禁止无界缓冲
（EXEC-013）。内存积压封顶（`MemoryBufferBudget`）在到达 OutputLimit 前提前流式落盘。

含义/动机：「输出预算」不是建议——gate 与 collector 必须共同保证一次只允许一个有界的大输出，
否则预算合同可被并发或积压绕过。

边界：gate 的物理位置在 `Process/`，但其语义 owner 是输出预算合同（本包）；spool 采集本身 →
`process-execution`。

证据：REUSE `requirements/output-distillation/tests/large-gate.test.mjs`（`VERIFY_009_large_gate_first_acquire_succeeds_immediately`、
`VERIFY_009_large_gate_second_acquire_waits_until_release`、cancelable waiter 组）；
`requirements/process-execution/tests/process-runner.test.mjs`（`EXEC_011_large_estimate_acquires_and_releases_the_gate`）。

## DISTILL-012：自定义 tool 文本结果确定性留尾截断

插件返回给 Host 的自定义 tool 文本结果必须在 Host 默认 head truncation 之前完成**确定性留尾截断**
（ARCH-012 / `Domain/ToolResultBound.fs`）：≤2000 行且 ≤51200 字节时逐字返回；超限时固定 marker
（`...head truncated (tail kept)...`）+ 确定性尾部（优先最新完整行；单行超限按 UTF-8 scalar 安全
保留后缀），使最终结果同时满足两项上限、Host 不再二次截断。计量只认 UTF-8 字节与换行。

含义/动机：Host head truncation 丢尾部（最新信息）；确定性留尾 = 压缩方向可预测、可测试。
边界只限制 tool 返回 wire，不改变内部完整结果的事实来源。

证据：REUSE `requirements/output-distillation/tests/tool-host-codec-full.test.mjs`（ToolResultBound 面，SPLIT 注记见
PROOF.md：wire 渲染 owner → `provider-projection`）。

## DISTILL-013：蒸馏不返回 chunk 统计仪表盘

蒸馏输出不包含 chunk 统计仪表盘、不叙述 map-reduce 机械过程、不报告 success ratio、不用「文本
如何被切开」的清单装饰返回（Role Law）。被保留的伤口、冲突、count、未决印记才是公开事实。

含义/动机：机械过程是私务；把 success ratio 报告成蒸馏事实 = 用发明物冒充 observation
（与 DISTILL-004 同源）。

证据：MOVE `tests/executor-summarize.test.mjs`（`DISTILLATION_prompts_carry_no_chunk_index_or_level`——
prompt 层面 chunk/level 不可见）；anchor `distinguishing`（区分价值是唯一选择标准）。

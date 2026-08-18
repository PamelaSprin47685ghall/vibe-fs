# WHAT：intra-participant-parallelism 必须成立的规则

本文件是 `intra-participant-parallelism` 的唯一 normative 合同。WHY/HOW/PROOF 非 normative。

## INTRA-PARTICIPANT-PARALLELISM-001：一人多 present，不增加 participant

Fission 只增加同一 logical participant 的并发 execution presents。所有 lanes 共享同一 logical identity、CanonicalRole、authority/responsibility owner、logical parent relation 与 logical child set；不得因 physical lane session 数量增加 provider-visible AgentId、handle 或 parent join obligation。

## INTRA-PARTICIPANT-PARALLELISM-002：canonical lane parser

`fission(prompts: String)` 先把 CRLF/CR 规范化为 LF，最多移除一个最终 LF，再按 LF 分行；必须 N≥2，且每行至少含一个非空白字符。除 newline normalization 外，每行文字 byte-for-byte 保留；不得 trim、猜 markdown/list/JSON 或 silently drop empty lane。

## INTRA-PARTICIPANT-PARALLELISM-003：fresh sibling replacement transport

V1 不使用 OpenCode session fork，也不在同一 physical SessionId 上并发 provider streams。Fission caller 必须是已有 physical Host parent 的 subsession；user-facing/root session 不允许 Fission。每条 lane 使用 fresh Host session；其 physical Host parent 必须等于 old caller 的 physical Host parent：`parent(lane[k]) = parent(oldCaller)`。lane 的首个 assignment 由 old caller 当时的 canonical Lifecycle Work Record 与该 lane 的 exact fission input 组成；lane physical session 不因此成为新的 delegation identity。

## INTRA-PARTICIPANT-PARALLELISM-004：all-or-none admission

一次 Fission admission 必须原子地建立全部 N 条 lanes。任一 lane create/bind/send 失败时，已建立 lanes 全部回滚，old caller 保持正常执行；禁止 partial group、silent lane count shrink 或“先中断 caller 再尝试补 lane”。

## INTRA-PARTICIPANT-PARALLELISM-005：old caller silent interrupt

只有全部 lanes 已成功建立后，old caller 才发生 Fission-owned silent interrupt。该 interrupt 不得发布 logical-owner `Aborted` completion、不触发 provider-failure recovery、不 abort owner 已有 children/PTYs、不关闭 parent completion cell；它只退休被 lanes 替代的 physical present。

## INTRA-PARTICIPANT-PARALLELISM-006：pre-fission outstanding completion 广播

Fission admission 时已 outstanding 的 subagent run 与 PTY 属于 logical owner 的共享既有债权。其每个 completion 使用一个 canonical completion payload，向每条 Fission lane exactly once delivery；这是一个事实的多 present delivery，不得制造 N 份 canonical WorkRecord 或 N 个 logical completion。lane 提前关闭不能使未投递 completion 消失。

## INTRA-PARTICIPANT-PARALLELISM-007：post-fission completion lane affinity

Fission admission 后由 lane `k` 新发起的 subagent run/PTY completion 绑定 initiating lane `k`。shared logical child inventory 不等于 shared completion drain：其它 lane 不得偷走该 active run 的 completion；busy existing child nudge 不创建新 run，也不改写原 run affinity。

## INTRA-PARTICIPANT-PARALLELISM-008：keyed work convergence

每 lane 的 canonical own work record 在 group 中以 lane index 为唯一 key。merge 同 key 同 ref/digest 幂等，同 key 不同 ref/digest fail closed。handoff/forwarding 可改变运输路径，但最终集合语义只由 keyed union 决定，不能由 arrival order 或字符串 append 决定。

## INTRA-PARTICIPANT-PARALLELISM-009：single logical completion

只有全部 lane own records、required pre-fission broadcasts 与 lane-affined completion obligations 均 accounted for 后，group 才能收敛。一个 Fission group 对 logical parent 最多产生一次 ordinary terminal completion；final completion 必须填回 old logical participant 的原 completion cell，而不是以任一 fresh lane session 建立新 handle。

## INTRA-PARTICIPANT-PARALLELISM-010：durable replay，不猜 lane

一旦 Fission 已造成真实 lane/effect，active group identity、lane membership、replacement relation、work/broadcast delivery 与 convergence terminal 必须有足以审计 crash 前因果的 durable facts；不得扫描相似 sessions 猜“哪些可能是 twins”。**Fission tool crash 后不自动恢复**：plugin init 与普通后续 tool/hook 都不得重建 lane runtime、重新 abort old owner、补 convergence 或替旧 tool 收尾。旧 Open group 表示上一 Fission 执行中断；未来仅显式 session `/continue` 可把该事实公开给 LLM，由新意图决定是否复用 surviving lane sessions。

## INTRA-PARTICIPANT-PARALLELISM-011：V1 单 active group

一个 logical participant 同时最多一个 active Fission group。active lane 再调用 `fission` 必须 fail closed 为 already-fissioned；V1 不递归裂变。

## INTRA-PARTICIPANT-PARALLELISM-012：eligibility 单一 consequence source

Fission 的 role entitlement 必须从 office consequence/capability 的单一 production source 同时投影到 provider-visible schema 与 runtime role gate；fast/deep 同 office 不得分叉。当前 role vocabulary 中 V1 entitlement 为 Manager、Coder、Inspector、Browser、Inquiry；Orchestrator、DevOps、Reviewer、Blogger、Distiller 不具备该 consequence。该 role consequence 不替代 INTRA-PARTICIPANT-PARALLELISM-013 的 subsession origin gate。

## INTRA-PARTICIPANT-PARALLELISM-013：subsession-only origin

Fission 的 origin gate 与 role entitlement 正交：caller 必须能证明自己是 physical subsession（`parent(oldCaller)=Some _`）。`parent(oldCaller)=None` 的 user-facing/root session 必须在任何 lane create、LWR materialization、durable admission 或 owner interrupt 之前 fail closed；不得把 root 替换成 sibling roots。该拒绝不得占用 active-group slot。

同一个 origin 事实还必须在 provider request 形成前收窄 tool surface：managed user-facing/root session 的每条物理 user request 都必须显式投影 `tools.fission=false`，因此模型不得在 provider-visible tool list 中看到 Fission；有 physical parent 的 subsession 不得被该 origin projection 误伤，继续由 INTRA-PARTICIPANT-PARALLELISM-012 的 role entitlement 决定是否可见。该 request-local deny 只收窄 entitlement，不修改 `Roles.permissions`，也不得把 root/subsession 做成两套 office。

即使 Host/客户端绕过 provider schema 强行调用 `fission`，tool adapter 也必须在读取/解析 `prompts` 之前先验证 physical parent。root 必须返回明确的 invalid-origin consequence，不得先返回 `TooFewLanes`、capacity、active-group 等与 origin 无关的错误。Domain admission 内的 parent check 继续保留为第二道 authoritative gate，不能因 adapter precheck 而删除。

## DEPENDS ON

`participant-identity`, `session-ontology`, `managed-session-lifecycle`, `office-capability`, `capability-enforcement`, `participant-horizon`, `work-record`, `process-execution`, `durable-events`, `crash-reconciliation`.

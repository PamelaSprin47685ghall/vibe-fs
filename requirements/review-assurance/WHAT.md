# WHAT — review-assurance

> 本文是 `review-assurance` 包的**唯一 normative 合同**。每条命题都是当前世界必须同时成立的事实。
> 证据指针（→ PROOF.md 行号）指向本包 PROOF.md 的落点表。

## 术语（本包内定义）

- **judgement**：Reviewer 的 `PERFECT | REVISE` 裁决（语义归 `review-judgement`；本包只处理它的消费资格）。
- **attempt identity**：`(ReviewBarrierId, GitTreeHash, ReviewerSessionId, ProviderRunIdentity, ToolCallId)` 五元组——一次可计数的 verdict 尝试。
- **challenge**：第一次 PERFECT 作为工具结果发出的 skeptical 句（`resources/provider/review/challenge/`），其 `contentDigest` 必须出现在第二次输入 seal 中才构成因果消费。
- **ProviderInputSeal**：`messages.transform` 时刻对 canonical provider input 的摘要，绑定到即将消费它的 ProviderRunIdentity（HOST-010）。
- **witness**：`ReviewWitness` 代数状态（NoReview / RevisionWitness / PerfectPending / Confirmed）；confirmed 只能从证据派生，不是存储标志。
- **ConsumableReview**：`TodoReviewConcluded(k)` —— VerdictKnown ∧ 同 snapshot 的 canonical ProcessReviewLWR record-ready ∧ 已 append Concluded；下一 TodoWrite / suicide drain 才能消费。
- **record-ready**：同一 Journal snapshot 下，以全量 origin coverage 物化含 `Chronicle`（及必要 Recent work）的 canonical LWR（request-range bounded，`includeOpening=false`）；frontier 为排他（lastPart+1）。

---

## REVIEW-ASSURANCE-001：第二次 PERFECT 必须满足九条件；禁止 same-root 确认

**规范**：第二次 PERFECT 成立当且仅当同时满足：（1）同一 Reviewer Session；（2）同一 ReviewBarrier；（3）同一 Git tree；（4）不同 ProviderRunIdentity；（5）不同 ToolCallId；（6）第二次 provider input seal **包含**第一次 challenge result；（7）中间没有 REVISE；（8）中间没有 tree 变化；（9）`judge` 工具确实成功执行。禁止仅凭 AuthorityRoot 或 PhysicalMessageId 确认。

**含义/动机**：确认必须是因果证明，不是身份猜测。任一条件缺失，第二次 PERFECT 都不构成确认；REVISE 在投影中清除 pending challenge，正是条件 7 的机制。

**边界**：REVISE 的 cohort 关闭语义（REVIEW-002）归 `finality`；本命题只冻结确认条件的代数与「REVISE 中断确认链」。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-001`。

## REVIEW-ASSURANCE-002：单次 PERFECT 不足；challenge 必须被因果消费

**规范**：第一次 PERFECT 只产生 `PerfectChallengeIssued`（pending witness），**不构成确认**。确认要求 seal 的 `IncludedToolResultDigests` 含 challenge digest——即第二次输入的 canonical 视图真实携带了第一次 challenge 的工具结果。ReviewConfirmation prompt 只让 Host 启动下一次 provider request，**不是**确认事实本身。

**含义/动机**：单 PERFECT 可被模型随口同意；只有「第二次输入里真的含有 skeptical challenge 的字节」才把确认从口头变成因果消费证据（历史 why/review 条款）。challenge 是工具结果，seal 必然携带其 digest——第二条哈希路径只会漂移并伪装成 fail-closed。

**边界**：challenge 的文案世代（TextVersion）与装载 → HOW；「ToolResultDigest 怎么算」→ `provider-projection`。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-002`。

## REVIEW-ASSURANCE-003：attempt identity 五元组；同 run 额外 PERFECT 不计数

**规范**：`ReviewAttemptIdentity = { ReviewBarrierId; GitTreeHash; ReviewerSessionId; ProviderRun; ToolCallId }`。同一 ProviderRunIdentity（含同 assistant message 内并行/重复 tool call）中的额外 PERFECT **不计数、不写 Journal**。attempt 窗口与 seal 窗口都有界（PERSIST-008），不随历史增长。

**含义/动机**：同一 run 里的两次 PERFECT 是同一次 provider 决策的重复表达，不是第二次独立判断；只有 run 与 call 都不同才可能是独立尝试。窗口有界保证投影不因历史增长退化。

**边界**：Finality 用完整五元组；TodoProcessReview 只以 run/call 标识 terminal judge（不分配 barrier witness 语义）→ REVIEW-ASSURANCE-011。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-003`。

## REVIEW-ASSURANCE-004：confirmed 是派生谓词，禁止存储布尔

**规范**：`confirmed` 只能从 witness 派生（`ReviewWitness.confirmedReviewer`/`IsConfirmed`），禁止旁置「已确认」布尔标志或旁置 reviewer id。记录 PERFECT verdict 本身不产生 pending；challenge 是独立事实。

**含义/动机**：存储布尔会与 witness 脱节——一个已确认布尔可以指向一个 NoReview witness。confirmed 是证据的属性，不是旁边的标志；旁置 reviewer id 是同一错误的一步之遥。

**边界**：`ConfirmedReviewWitness` 只服务 FinalityReview 与 Orchestrator 复审；process 评审不产生、不消费它（REVIEW-ASSURANCE-011）。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-004`。

## REVIEW-ASSURANCE-005：witness 自包含；Guard 不依赖外围 Map

**规范**：`ConfirmedReviewWitness` 独立回答：谁审的、为哪个 Job、哪棵 tree、哪两次 provider run、第二次是否真的看过 challenge、属于哪个 barrier。Guard 不得依赖外围 Map 补身份；witness 无 AuthorityRoot 字段（REVIEW-003 禁止 same-root 确认，「携带字段」是它回归的一步之遥）。

**含义/动机**：外围 Map 在恢复与并发 Job 下会静默读到别人的确认或空确认。自包含保证 witness 在任意上下文可独立验证。

**边界**：Manager 面是否可见 Guard（REVIEW-007）→ `finality`/`participant-horizon`；本命题只管 witness 本身的自包含性。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-005`。

## REVIEW-ASSURANCE-006：tree 变化使 witness 失效；不删除历史；新 barrier 需全新双 PERFECT

**规范**：任意 Git tree 变化：pending challenge → 拒绝；confirmed witness → 仍可审计但不再满足 Guard。**不删除**历史 witness；`witness.IsValid(currentBarrier, currentTree)` 是派生谓词。新 barrier（即使 tree hash 碰巧相同）要求全新双 PERFECT；post-rebase 必须重新双 PERFECT 才允许发布（REVIEW-009）。

**含义/动机**：审的是代码状态，不是 Session 情绪；tree 变即 witness 失效。barrier 是 request 身份，跨 request 复用旧确认等于把一次终末证明借给另一次终末。重复进入同一 barrier 幂等；晚到确认不能回卷当前 barrier。

**边界**：rebase 的发布门（ff publish）→ `change-integration`；本命题只冻结 witness 失效代数。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-006`。

## REVIEW-ASSURANCE-007：ProviderInputSeal fail-closed；无法绑定则不确认

**规范**：`ProviderInputSeal` 在 `messages.transform` 时刻生成，绑定到即将消费它的 ProviderRunIdentity。若 Host 无法把 transform 输出可靠绑定到 `ProviderRunIdentity`（无候选 / 多个候选 / 非最新 run / compaction 路径 / 无 physical user message），**必须 fail closed**——不写 seal，第二次 PERFECT 不确认。禁止退回 same-root 或 physical-message 猜测。

**含义/动机**：HOST-010 因果读：唯一未完成的 assistant、parent 匹配、最新 id、非 compaction。猜测在 Host 重排消息时会假绿；`bindableRun` 四条件合取，缺一即 admit 错误答案。

**边界**：因果读的传输实现（怎么从 Host 读）→ `host-boundary`；seal 的 fail-closed 语义与「只有 reviewer session 被 seal」是本包。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-007`。

## REVIEW-ASSURANCE-008：VerdictKnown 与 ConsumableReview 两段式；禁止提前 Concluded

**规范**：`VerdictKnown(k)` = Reviewer 域已有、针对 `TodoProcessReview(k)` 的 durable verdict（PERFECT|REVISE）→ 立即决定业务 outcome/settlement，**不携带 WorkRecordRef，不单独构成可消费报告，不进入 Finality dual-PERFECT witness**。该 verdict 的 `ProviderRunIdentity + ToolCallId` 必须由唯一 Journal 积分器随 `ReviewVerdictRecorded` 一次 fold 为 bounded typed projection evidence；任何消费方禁止扫描/重放 Journal、解析 dedupe string、或从 XTrace `tool_call` 反推 verdict identity。`ConsumableReview(k) ≡ TodoReviewConcluded(k)` = VerdictKnown ∧ 该 verdict frontier 的 canonical ProcessReviewLWR 已 record-ready ∧ 同 snapshot 已 append Concluded。顺序冻结：VerdictKnown → await record-ready → append Concluded → T(k+1)/suicide 可消费。

**含义/动机**：把「只有判断、尚无 report」挤进同一个 Concluded，恢复路径无法区分「已可 settle」与「已可展示报告」，并诱导提前 append 空壳。两段式让 verdict 先 settle 业务，report 就绪后才解锁消费。

**边界**：Rk 派生与节拍 → `obligation-ledger`；LWR 的表示 → `work-record`；本命题冻结两段式分型与消费门槛。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-008`。

## REVIEW-ASSURANCE-009：record-ready 同 snapshot、排他 frontier、事件驱动、无轮询、waiter 可恢复

**规范**：record-ready 判定是「能否在**同一 Journal snapshot** 物化有效 canonical LWR」，不是 `coverage >= frontier.Sequence`（frontier 排他 = lastPart+1，真实 coverage 上限只达 lastPart，旧门禁会永远悬挂）。coverage 判定与 LWR materialization 不同 revision 即不成立。等待只经 `AgentJournal.awaitChangeFrom` 事件唤醒；禁止 timer/sleep/wall-clock 轮询。Direct CE 必须先采样 revision，再执行 `tryConclude`/producer 判定，最后 `awaitChangeFrom sampledRevision`，形成 check→subscribe/recheck 等价握手，禁止在判定后才采 revision 造成 lost wakeup。waiter 取消/dispose/崩溃不是 durable abandonment：从 durable assignment / VerdictKnown / 冻结 frontier 重建同一等待；`TodoReviewConcluded` 已在则直接消费。若 process verdict 已 durable，则当前 Host work-unit 的 `CompletedAwaitingJoin` / `Retired` 只说明 reviewer 已结束生成，不证明 record-ready producer 丢失；必须继续等待 XTrace/LWR 收敛。只有尚无 durable verdict 且 reviewer work-unit 已不可继续时才可 fail closed。

**含义/动机**：轮询把 Journal 因果等待退化成运气；用较晚 head 替换冻结 frontier 会改变原 REVISE 的记录目标。同 snapshot 保证「判定时就绪」与「物化同一份」不可分。

**边界**：等待机制的因果可观测性 → `causal-wait`；「process-local waiter 消失即放弃」禁令的本包侧即本命题。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-009`。

## REVIEW-ASSURANCE-010：基础设施失败永远不是 PERFECT/REVISE

**规范**：下列失败**永远不是**过程/终末业务 PERFECT 或 REVISE，不伪造 settlement / semantic merge，不推进 ConsumableReview：dedicated create/resume、process assignment、Y/LWR materialization、Host contract 破坏、其它 infrastructure failure。处理：Accepted 派生的 Rk obligation 保持 outstanding；可证明可恢复 → event-driven ensureReview/ensureAssignment；不可证明/契约破坏 → typed infrastructure failure，Finality 不得越过该 outstanding Rk，下一 TodoWrite 继续阻塞。

**含义/动机**：伪装成 REVISE 会触发错误 semantic merge、推进虚假 ConsumableReview，并让 Manager 去「修复」系统故障。三态分离：tool 语法红 → `capability-enforcement`；语义 REVISE → `review-judgement`；infra fatal → `host-boundary`/`crash-reconciliation`。本包只拥有 review-side 的「不把 infra 伪装成 REVISE」负边界。

**边界**：undecidable 的 crash 恢复细节 → `crash-reconciliation`；本命题只冻结「不伪 REVISE、不伪 Concluded、义务 outstanding」。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-010`。

## REVIEW-ASSURANCE-011：process verdict 与 terminal witness 代数分离，互不计数

**规范**：`process PERFECT ≠ terminal first/second PERFECT`；`process REVISE ≠ FinalityRejected 事实本身`。ConfirmedReviewWitness / dual-PERFECT 代数仅服务 FinalityReview（与 Orchestrator 复审）。Dedicated 被 enlist 进某次 FinalityRequest 时，即使刚完成 process PERFECT，仍须 fresh FinalityRequestId / BarrierId / GitTreeHash / Authority Root + fresh dual-PERFECT chain；可复用同一 physical session，不可复用过程因果证明。

**含义/动机**：过程一次判断是 checkpoint 的判断（语义归 `review-judgement`），它的计数资格是终末证明的稀释点。代数分离保证终末 2N 只由终末 fresh evidence 满足。

**边界**：过程判断的语义本身 → `review-judgement`；enlist/graduate 生命周期 → `finality`/`managed-session-lifecycle`。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-011`。

## REVIEW-ASSURANCE-012：可消费证据 request-range bounded；session head 不能冒充

**规范**：过程/终末审查的可消费证据唯一表示是 request-range bounded canonical LWR（`includeOpening=false`）。三个用途各用冻结 range：ManagerCheckpointLWR(k) = Life work cursor..ReviewFrontier(k)；ProcessReviewLWR(k) = ReviewWorkStartCursor..ReviewerRecordFrontier(k)；Finality reviewer LWR = FinalityReviewWorkStartCursor..FinalityVerdictFrontier。**禁止取 session 当前 head 冒充**任何一条有界 LWR；历史 process turns 不得整段塞进终末 LWR。

**含义/动机**：Dedicated session 跨多个 Rk 复用后，若取 session head，R4 报告会吞入 R1–R3。request 绑定（GLORY-051）要求每份 record 绑定 current request/checkpoint、member session、barrier（若有）、tree（若有）与 digest；不一致 fail closed。

**边界**：LWR 的表示/物化/三标题 → `work-record`；本命题只冻结「可消费证据必须 request-bounded」与 frontier 冻结。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-012`。

## REVIEW-ASSURANCE-013：review requirement 以 Authority Root 标识；确认只清除其覆盖的 requirements

**规范**：等待 review 的 human prompt requirement 以 **Authority Root**（PROMPT-002：任务身份）为 key 并去重，不以 physical message 为 key。确认（`clearOnConfirmation`）只清除该确认覆盖的 requirements；重放同一 run 的确认不得清除其后到达的新 requirement。

**含义/动机**：requirement 是「人类要求了什么任务」，Authority Root 是任务身份；按 wire message key 会强迫身份转换并错配。幂等清除保证重放不回卷。

**边界**：Authority Root 语义 → `participant-identity`/`interaction-authority`；Manager 面可见性（REVIEW-007）→ `finality`/`participant-horizon`；本命题只冻结 requirement 的身份绑定与覆盖清除。

**证据**：→ PROOF.md `REVIEW-ASSURANCE-013`。

---

## 反向覆盖

本包消费的源材料及其落点：

| 源 Clause / 资源 | 落点 |
|---|---|
| REVIEW-003（九条件、challenge 因果、禁 same-root） | REVIEW-ASSURANCE-001/002 |
| REVIEW-004（attempt identity、同 run 不计数、窗口） | REVIEW-ASSURANCE-003 |
| REVIEW-005（两条因果链、confirmation 派生） | REVIEW-ASSURANCE-002/004 |
| REVIEW-006（自包含 witness、禁外围 Map、无 AuthorityRoot） | REVIEW-ASSURANCE-005 |
| REVIEW-008/009（tree 变化失效、不删除、新 barrier 新双 PERFECT） | REVIEW-ASSURANCE-006 |
| REVIEW-010 + HOST-010（seal fail-closed、bindableRun） | REVIEW-ASSURANCE-007 |
| REVIEW-014 + TODO-006（VerdictKnown vs ConsumableReview） | REVIEW-ASSURANCE-008 |
| REVIEW-017 + GLORY-072/073（同 snapshot、排他 frontier、无轮询、waiter 恢复） | REVIEW-ASSURANCE-009 |
| REVIEW-018 + GLORY-056/057（infra ≠ REVISE） | REVIEW-ASSURANCE-010 |
| REVIEW-020 + GLORY-058（process ≠ terminal witness） | REVIEW-ASSURANCE-011 |
| REVIEW-016 + GLORY-051（request 绑定、禁 session head） | REVIEW-ASSURANCE-012（表示侧 → work-record） |
| REVIEW-007 传输侧 + PROMPT-002（requirement 身份绑定） | REVIEW-ASSURANCE-013（clause owner 注明 finality/horizon 交叉） |
| REVIEW-001/011（judge 形态、判断语义） | 显式驳斥：→ `review-judgement`（不复制） |
| REVIEW-002/007（cohort 关闭、Manager 面） | 显式驳斥：→ `finality` |
| REVIEW-013/015（节拍、dedicated 生命周期） | 显式驳斥：→ `obligation-ledger`/`managed-session-lifecycle` |
| 历史 change（magic-todo）两段式与 ensureReview | REVIEW-ASSURANCE-008/009（考古见 WHY） |
| 历史 change（fix-revise）（GARBAGE transcript） | REVIEW-ASSURANCE-009/010 考古（弃权记录见 HOW） |

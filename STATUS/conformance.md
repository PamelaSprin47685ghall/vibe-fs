# STATUS/conformance — SSOT 条款合规表

状态允许值：`CONFORMANT` | `PARTIAL` | `CONTRADICTS` | `UNVERIFIED` | `NOT_IMPLEMENTED` | `PURE_CORE_ONLY`

`PURE_CORE_ONLY`：条款的纯领域内核已实现并测试（第 1 层），但生产接线被 Host canary 门禁阻断（如 SSOT/14-16 的 STRENGTH-078 / ENFORCER-180 / LEARN-082…088）——不是"零实现"，也不是"可发布"。

绑定源码 commit：`38cc1882`（休克—退火迁移收口）。运行验证不属于 commit 内容，命令与结果见 `docs/evidence/`；历史机器证据见 `docs/archive/shock-anneal-2026/evidence/`。本表只记录截至该提交的源码状态。

## 架构 DNA

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| ARCH-001: 结构化程序替代状态机 | PARTIAL | 全仓 | 无 Stage/Phase/Lease 字段；但 Orchestrator 恢复投影仍是 stage-like 事实序列（见 ORCH-006） |
| ARCH-002: 事件是信号不是数据 | CONFORMANT | `HostSignalAdapter.fs` `HostEventCodec.fs` | 碎片事件在 codec 边界丢弃 |
| ARCH-003: 不修改 OpenCode 本体 | CONFORMANT | 全仓 | 只用现有 Hook 与 SDK API |
| ARCH-004: LLM 前缀缓存保护 | PARTIAL | `CompanionProjection.fs` | FrozenB/LatestB 已分离；冷边界靠 mock 嗅探而非显式声明（见 VERIFY-003） |
| ARCH-007: 不同语义不同工具名 | CONFORMANT | `ForkTool.fs` | `fork-agent` / `fork-manager` / `fork-pty` 已分离 |
| ARCH-009: 有界并发与共享原语契约 | CONFORMANT | `Kernel/Flow.fs` 的 `Parallel.mapBounded` | 唯一并发原语；上限为正且拒绝非正值、结果按输入位置排列、取消在获取许可处观察且 token 传达到 action、许可在 action 失败时归还。`guide-contract.test.mjs` 断言不存在无界的 `Parallel.map*` 兄弟。第 1 层测试 12 项 |
| ARCH-010: 运行时 LLM 可见合成 prompt 的 TOML 形态 | CONFORMANT | `Domain/SyntheticToml.fs` | 指令只写为最前方顶层 `#` comment、数据只写为字段/表/value；三种合法形态、无统一 envelope、单向表示不反向解析；subagent 自然语言全文按 instruction 处理。证据：`scripts/surface-inventory.mjs`（工具返回审计）、`tests-mjs/Context/synthetic-toml.test.mjs`、`tests-mjs/Execution/fork-child-payload.test.mjs`、`tests-mjs/Execution/executor-summarize.test.mjs`、`src/Wanxiangshu.Next/Domain/SyntheticToml.fs` |
| ARCH-010: 插件工具返回体 TOML（无 JSON） | CONFORMANT | `Infrastructure/OpenCode/Codec/ToolHostCodec.fs` | 全部插件工具返回体经 `tomlObject`/`tomlTable` 渲染为 data-only TOML（snake_case 字段），LLM 可见 JSON 工具返回禁止，工具返回体已纳入 inventory。证据：`src/Wanxiangshu.Next/Infrastructure/OpenCode/Codec/ToolHostCodec.fs`、`tests-mjs/Plugin/manager-tool-contract.test.mjs`、`testkit/opencode/tests/reviewer-verdict-canary.mjs` |

## Prompt Authority 与 Dispatcher

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| PROMPT-002: Authority Root 固定执行画像 | CONFORMANT | `PromptAuthorityRun.createAuthorityRoot` | 唯一构造入口；`AuthorityExecutionProfile` 无 model 字段，故「root 覆盖 model」不可表达。第 1 层测试：`PROMPT_002_authority_root_profile_cannot_express_a_model`、`PROMPT_002_root_derives_peer_role_and_tier_from_the_selected_agent_alone`、`PROMPT_002_a_new_root_replaces_the_profile_and_clears_everything_run_scoped`（`tests-mjs/Prompt/authority.test.mjs`） |
| PROMPT-003: Continuation | CONFORMANT | `PromptDispatcherSend.SendContinuation` | 继承 run 与 root，EffectiveAgent 参与 PromptKey。第 1 层测试：`PROMPT_003_a_continuation_never_replaces_the_authority_root`、`PROMPT_003_every_continuation_kind_is_representable_and_none_is_a_root`（`tests-mjs/Prompt/authority.test.mjs`） |
| PROMPT-004: 来源类型 | UNVERIFIED | `PromptIngress.fs` `Fact.AuthorityRootAccepted` | 包 A/0b：`HumanPromptAccepted` 已替换；解析顺序为「journal 已知 → 显式 managed agent」，已知来源不得被 agent 字段改判。判据待补：无直接第 1 层测试，来源解析路径需 canary 全绿后补断言 |
| PROMPT-005: 四阶段协议 | CONFORMANT | `PromptDispatcher.fs` `PromptDispatcherSend.fs` | 四事实齐备；`AdmittedWithReceipt` 止于 Submitted，物理受理仅由 `chat.message` 产生。第 1 层测试：`PROMPT_005_submit_records_the_receipt_without_resolving_the_claim`、`PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone`；fallback canary 实测 journal 四事实（PluginPromptClaimed/Submitted/PhysicalAccepted） |
| PROMPT-006: 发送格式 | UNVERIFIED | `PromptDispatcherSend.fs` | 包 A：两处发送点均 `Model = None`，Agent 由 EffectiveAgent 绑定。判据待补：需第 1 层测试断言发送 payload 的 model/agent 字段 |
| PROMPT-007: Fire-and-forget 定义 | UNVERIFIED | `HostSessionNudge.sendContinuation` | 包 A：`prompt_async` 在 src/Wanxiangshu.Next/` 仅 1 处（唯一 Host adapter）；五条绕过 Dispatcher 的直发分支已删。判据待补：architecture-gate 的直发分支检查 + canary 覆盖 |
| PROMPT-008: 原子 AttemptExecutionProfile | CONFORMANT | `Domain/AttemptPlanner.fs` `OpenCode/XWire.fs` `OpenCode/CompanionTransform.fs` | `buildAttemptExecutionProfile` 唯一调用点 `AttemptPlanner.plan`（`AttemptPlanner.fs:65`），被 `XWire.applyTransform` 与 `CompanionTransform` 真实调用；`RequestKind` / `ProjectionChoice` 作为 profile 不可变字段进入 transform 边界，`XWire.reconcileAttempt` 从同一 profile 判 promote。`single-constructor` 双向检查（无旁路 + 有调用）由 `architecture-gate` 守护。第 1 层测试 18 项（`Context/attempt-plan.test.mjs`） |
| PROMPT-009: 来源解析顺序 | UNVERIFIED | `PromptAuthorityRun.resolveKnownOrigin` | 包 A：按 session 读投影（PERSIST-008），未知来源 fail closed。判据待补：PERSIST-008 索引已落地，来源解析顺序需第 1 层测试补断言 |
| PROMPT-011: 未决发送恢复 | CONFORMANT | `OpenCode/PromptRecovery.fs` `OpenCode/SpikePlugin.fs` | `PromptRecovery.reconcile` 在插件启动时（`SpikePlugin.fs:57`，早于任何 hook 派发）扫描 tail window（`RecoveryTailWindow = 50`）找物理消息；`RecoveryAttemptBudget = 3` 由 fold 记账（`recoveryBudgetSpent`），耗尽即 `Abandoned(UnresolvedAfterRecovery)`；只证明接受或放弃，绝不重发 |

## Fallback

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| FALLBACK-001: Fallback 属于 Logical Run | CONFORMANT | `FallbackProjection.forAuthority` `Fold.fs:229` | cursor 由 `AuthorityRootAccepted` 创建，无 `empty` 构造函数；无 cursor 的 advance 以 `NoCursor` 拒绝并停止 replay。第 1 层测试 |
| FALLBACK-002: Modulo-4 Cursor 与 ConsecutiveFailureCount | CONFORMANT | `Domain/AgentPairCursor.fs` | 两个量已分离为独立字段；Offset 模 4 无终态，count 为有界预算；offset 越界 `invalidOp` 而非默认 SideA。第 1 层测试 |
| FALLBACK-003: 统一 FallbackController | CONFORMANT | `Session/FallbackController.fs` | `shock-audit` 实测 ok (1)。两个旧 writer（`ProviderFailureWakeup` / `RetrySignalHandler` 经 `recordFallbackFailure`）已删；去重按 `FallbackAttemptIdentity` 四元组，窗口上限 32（PERSIST-008） |
| FALLBACK-004: 不变量 | CONFORMANT | `AgentPairCursor.recordSuccess` | 成功清零 count 且不动 Offset，并清空去重窗口；失败推进 Offset 且消耗一格预算。第 1 层测试含「成功后停放在奇数 Offset」这一 FALLBACK-012 前提 |
| FALLBACK-005: 有限 Circuit Breaker | CONFORMANT | `AgentPairCursor.recoveryVerdict` `Kernel/Fact.fs:127` | `DefaultAutoRecoveryBudget = 12`（非 `[<Literal>]`，故第 1 层可断言）；`FallbackExhausted` 事实存在；判决在失败记录之后，第 12 次立即终局，无自动第 13 次；`Exhausted` 为持久状态而非由 count 重新派生 |
| FALLBACK-006: 完整序列示例 | CONFORMANT | `AgentPairCursor.sideSequence` | 表以函数表达且无上界。第 1 层测试断言 100 项 |
| FALLBACK-007: 持久事实 | CONFORMANT | `Kernel/Fact.fs:115` `FallbackProjection.applyAdvance` | 六个字段齐备；Fold 验证 modulo-4 后继与 count 恰好 +1；四种拒绝理由可区分（`AlreadyObserved` / `AlreadyExhausted` / `DifferentRun` / `NoCursor` / `InvalidTransition`），前三者吸收、后两者停止 replay。成功不写任何事实 |
| FALLBACK-008: 空/XML-only terminal | CONFORMANT | `Domain/TerminalValidity.fs` `PromptAuthority.repairAlreadyClaimed` | 唯一内容级校验；预算由 `ClaimSequences` 派生故重启后仍在，abandon 不解锁。第 1 层测试 |
| FALLBACK-010: Host Attempt ≠ ConsecutiveFailureCount | CONFORMANT | `Domain/AgentPairCursor.fs` | 结构性保证：`recordFailure` 只接受 cursor 一个入参，Host `Attempt` 无路可入；推进按 `ProviderRunIdentity` 去重，故一次 provider run 的多次 Host 重试只消耗一格预算 |
| FALLBACK-011: 一个槽可含维护子请求 | CONFORMANT | `Domain/RecoverySlot.fs` | 见 Companion 段；第 1 层测试在 `Context/recovery-slot.test.mjs` |
| FALLBACK-012: armed 需要紧邻失败推进与 primed 槽位 | CONFORMANT | `RecoverySlot.mayRecover` | 两条件合取；facade 不提供由 Offset 奇偶单独派生 arming 的出口。第 1 层测试 |

### Fallback 段此前的记录全部失效（包 T-3 更正）

本段在包 T-3 之前的内容描述的是迁移前状态，七行里没有一行仍然成立（详细对照见
`docs/archive/shock-anneal-2026/FINAL-REPORT.md` §8）。失效的根因值得记档：这些行是包 C
完成时该更新而没更新的。休克期关闭了测试反馈，而 `conformance.md` 的更新一直依赖手工，
于是「代码前进、状态表留在原地」这个方向的偏移没有任何机器会发现——`ssot-lint` 只检查
条款 ID 与实现状态词的分离，不检查状态词是否真实。状态往乐观方向偏移更危险：一个标着
`CONTRADICTS` 的合规项只是噪音，一个标着 `CONFORMANT` 的违规项会让人跳过检查。本次更正后
Fallback 段的每一行都绑定到一个第 1 层测试或一次 `shock-audit` 实测输出。

## Review

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| REVIEW-002: REVISE | CONFORMANT | `ReviewProjection.applyVerdict` | 任何 REVISE 清除未完成的 PERFECT（`PendingChallenge` 置空、witness 转 `RevisionWitness`）。第 1 层测试 |
| REVIEW-003: 因果证明 | CONFORMANT | `ReviewController.provenSeal` `ReviewWitness.isDistinctAttempt` | 条件 1–5 为两个 witness 的纯比较；条件 6 由 seal 判定——`Set.contains ChallengeContentDigest seal.IncludedToolResultDigests`，无 seal 即 fail closed。四种弱代理在 src/Wanxiangshu.Next/` 全为 0（`AcceptedContinuationRoots`、`samePhysicalRootReevaluation`、`GuardPromptAccepted`、`RecentProviderRunIds`）。第 1 层测试逐条移除证据成分并断言确认消失 |
| REVIEW-004: ReviewAttemptIdentity | CONFORMANT | `Kernel/Identity.fs:348` | 五元组类型存在，`dedupeKey` 以 `\u001f` 连接；同一 provider run 内的额外 PERFECT 由 `PendingChallenge.FirstProviderRun` 比对拦下，不计数不写 journal；窗口上限 8（PERSIST-008） |
| REVIEW-005: 因果单调状态 | CONFORMANT | `VerdictDecision` `PromptClaim` `ProviderInputSeal` | 第二次 PERFECT 只有三种答案（`Confirmed` / `ChallengeUnproven` / `AlreadyCounted`），无 `Confirmed of bool` 形态。两条链各归其主：链 A 在 `PromptClaim`（`Receipt` = Submitted，`acceptClaim` = PhysicalAccepted），链 B 在 seal。确认只读链 B——`provenSeal` 是唯一路径，admission ID 无从参与 |
| REVIEW-006: 自包含 Witness | CONFORMANT | `Domain/ReviewWitness.fs` | `confirm` 接收两个摘要而非 bool，故 witness 自带证据；不依赖外围 Map。第 1 层测试直接断言生产 record 的键集合（而非 facade 投影），确保无 authority root / physical message 字段 |
| REVIEW-007: Manager Guard | PARTIAL | `HostReviewGuard.fs` `TerminalPolicy.isTopLevelManager` | 纯侧已有判据：requirement 按 Authority Root 键入并去重、确认后清除且对同一 run 幂等（第 1 层测试）。Host 侧 terminal 钩子接线属第 3 层；退火三进行中，尚待取得该轨迹证据 |
| REVIEW-008: Git tree 变化使 witness 无效 | CONFORMANT | `ReviewWitness.isValidForTree` | 有效性是对当前 tree 的派生问题而非 mutation：witness 历史保留且仍报 `Confirmed`，但 `satisfiesGuard` 对新 tree 为 false。新 barrier 清 pending 而保留 witness；同 barrier 重入幂等。第 1 层测试 |
| REVIEW-010: ProviderInputSeal | CONFORMANT | `Application/Reconciliation/ReviewSeal.fs` `Journal/ReviewProjection.fs` | `shock-audit` 实测单一写入口 ok (1)。seal 记录 `IncludedToolResultDigests`，Fold 由 list 转 `Set<string>`；窗口上限 8。绑定只发生在工具执行路径（`ReviewSeal.bindToRun`）：onTurn 绑定已删——Host 1.18.10 上 reconcile run 与 `context.ProviderRunId` 对 challenge response 不一致，onTurn 写的 seal 是死数据（曾逼出 VerdictTool 第二写者）。可实现性见 `docs/archive/shock-anneal-2026/evidence/host-transform-run-binding.md` |

### Review 段此前的记录同样失效（包 T-3 更正）

与 Fallback 段同一成因，六行里五行描述迁移前状态（详细对照见
`docs/archive/shock-anneal-2026/FINAL-REPORT.md` §8）。

`GuardPromptAccepted` 在 src/Wanxiangshu.Next/` 仍有 1 处，但那是 `FactCodec` 的 pre-0.5.0 标记名单——
它必须留着，否则旧 journal 会以晦涩的 union 错误失败而不是给出迁移提示。这类「旧符号
作为拒绝清单条目」的残留与真正的旧调用点不同，`shock-audit` 的计数看不出区别，故在此
记名。

## Orchestrator

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| ORCH-001: `fork-manager` 命名 | CONFORMANT | `ForkTool.fs` | — |
| ORCH-002: Clean Gate | CONFORMANT | `OrchestratorGit.fs` | — |
| ORCH-003: 一个 Job 一个 worktree 一个 Manager | CONFORMANT | `Journal/OrchestratorProjection.fs` | `ManagerAgent` 持久化为精确 agent 名（非裸角色）；创建后只有 `Progress` 会变；未知 job 的进展是 no-op 而非新建条目；第二次 create 不改 Manager 与 worktree。第 1 层测试 |
| ORCH-004: 并行 Job | CONFORMANT | `OrchestratorProjection.activeJobs` | 多 job 同时 active，终态 job 退出 active 集合但留在 map 中。第 1 层测试 |
| ORCH-005: 短 CAS Integration Gate | CONFORMANT | `OrchestratorProgram.publishUnderGate` | gate 只包住 `claimAndFf`，不再跨 rebase 与 LLM review；gate 内二次读 head 即 CAS 的 compare；`claimAndFf` 包在 try 内以免泄漏 publish 锁。第 3 层轨迹尚待在进行中的退火三取得 |
| ORCH-006: 持久事实 | CONFORMANT | `Journal/OrchestratorProjection.fs` `Kernel/Fact.fs` `Orchestrator.WorktreeResource.fs` | `JobProgress` 是单值而非五个独立可选字段；`ManagerAgent` / `WorktreeIdentity` / `TargetBranchFrozen` / 两个 review barrier id 全部就位；`ManagerJobCreated` append 成功后 worktree 转为 durable，`NeedsReview` 与普通作用域退出均不删除；终态 job 留在 map 中使重放的 `Published` 被识别为重复。第 1 层测试；生命周期证据 `docs/archive/shock-anneal-2026/evidence/manager-worktree-durable-ownership.md` |
| ORCH-007: 恢复逻辑 | PARTIAL | `OrchestratorProjection.recoveryAction` `Orchestrator.RecoverManagerJob` | 八个 progress 分支各产出恰好一个动作，`PublishClaimed` 三分支顺序符合条款；`Adopt` 不再隐式释放 durable worktree。`orchestrator-restart-publish` 已越过 deep-reviewer/TOML 缺口，但 restart 后仍观测 `OrchestratorPublished=0`，恢复链尚未取得第 4 层绿证据 |
| ORCH-008: target ref 安全 | CONFORMANT | `OrchestratorGit.fs` `OrchestratorProjection.recoveryAction` | `GetTargetHead` 失败时两个依赖 head 的分支均 `FailClosed`，理由文字点明禁止回落 HEAD |

### ORCH-006 的一处实测缺陷：`createJob` 无条件覆盖（包 T-4 修正）

`createJob` 原先无条件 `Map.add`，于是重放 `ManagerJobCreated` 会把 `Progress` 重置为
`ManagerStarted`。

这不是理论问题：PERSIST-009 的 durable-effect 协议在 `CommitUnknown` 后重试，所以一份
journal 合法地可能带两条同一 `ManagerJobCreated`；重启恢复也会从头重读。一个已经
`Published` 的 job 因此会以「刚创建」的样子交给 ORCH-007，recovery 于是为已经落到
target ref 的工作再拉起一个 Manager。

修正是已存在则原样返回。与 `recordProgress` 同一规则：worktree 与 Manager 在 job 整个
生命周期内固定，所以第二次 create 没有新信息可说——包括它与首次不一致时。

`recordProgress` 早就有终态保护（`isTerminal` 分支），所以缺口只在 create 一侧。两个
函数之中只有一个做了幂等，这正是同一份不变量分两处表达时的典型失败。


## Agent

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| AGENT-007: 工具权限双层 fail-closed | UNVERIFIED | `ToolRuntimeScope.RoleFor` `ToolRegistry.fs` | 包 B：Role 唯一来源为 `ActiveLogicalRun.CanonicalRole`；`sessionRoles` 三来源链与 Role 未解析时放行 `inspector` 的豁免均已删除。判据待补：manager-tool-contract 的权限矩阵用例覆盖声明层，`RoleFor` 的 fail-closed 分支需第 1 层测试 |

## Companion

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| COMPANION-001: 每个 Work Session 都有 Companion | CONFORMANT | `Journal/SessionAssociation.fs` | 角色白名单已删除。关联 API 不接受 role 参数，因此 role 无法影响它不是输入的决定 |
| COMPANION-002: Companion 是叶子 | CONFORMANT | `SessionAssociationProjection.link` | 一次 `link` 写双向条目，`isCompanion` O(1)；递归、重复 Y、抢占 Y、自链四种非法态由 fold fail closed |
| COMPANION-003: A(X) 与 B(X) | CONFORMANT | `Domain/CompanionProjectionBuilder.fs` `Session/CompanionHostBlogger.fs` | LatestB / CoverableB / FrozenB 三分：probe 只用 CoverableB（CTX-011），`join().workRecord` = LatestB，FrozenB 只在 rebase 时变。`Context/blog-projection.test.mjs` 锁 Coverage 与 frame 语义 |
| COMPANION-004: Y 的 System Prompt | CONFORMANT | `Session/CompanionPrompt.fs` `Session/CompanionTransform.fs` | system prompt 只建立投影形状三事实，不携带 token/预算（CTX-001 负向断言）；Y 的 system prompt 与 X 无共享可变文本。第 1 层测试 `COMPANION_004_system_prompt_establishes_the_three_facts_the_shape_needs` |
| COMPANION-005: BlogFrame 增量投影 | CONFORMANT | `Session/BloggerDelta.fs` `OpenCode/CompanionTransform.fs` | delta 物理最后（理由改为「让物理 delta 同时是 provider 看到的最后一条」）；忙时跳过不排队；累积与发送分离。第 1 层测试 `COMPANION_005_normal_instruction_forbids_rewriting_prior_frames` |
| COMPANION-008: 忙时跳过 | CONFORMANT | `Companion.Submit` | 忙时返回 `SkippedBusy` 并原样退出，不推进 coverage、不排队、不计数。「三次 busy skip」计数已删 |
| COMPANION-009: PrefixEpoch | CONFORMANT | `Journal/PrefixEpochProjection.fs` `Domain/XPrefixProjection.fs` `OpenCode/XWire.fs` | 单轨：epoch 递增、snapshot 退役、X 前缀计划全在新投影。X-wire 已接进 transform 边界：`XWire.applyTransform` 在 armed 恢复槽经 `AttemptPlanner.plan` 构造 `XPrefixPlan` 并 `replaceMessagesInPlace`；`reconcileAttempt` 提升 `PrefixRebaseCommitted`。无 snapshot / 无探针时 X 看到原始历史（SSOT/12 正确中间态，非降级）。第 1 层测试覆盖 `Context/prefix-epoch.test.mjs` 与 `Context/probe-selection.test.mjs` |
| COMPANION-010: 低信任 Companion Memory 注入 | CONFORMANT | `Domain/CompanionIdentity.fs` `OpenCode/XWire.fs` | FrozenB 以低信任 context block 注入（`rawWithPrefix`），不伪装 human/system instruction，无随机 ID / 当前时间。第 1 层测试 `COMPANION_010_the_memory_is_wrapped_as_low_trust_context` |
| COMPANION-011: Cutoff 证明 | CONFORMANT | `Domain/PrefixProbeSelection.fs` `OpenCode/XWire.fs` | 投影前重算 `hash(messages[0..cutoff])`，失配 fail closed；digest 失配不作 compaction 处置（归 HOST-006 重锚）。第 1 层测试 `COMPANION_011_a_digest_mismatch_fails_closed` 等 |
| COMPANION-013: Synthetic 稳定身份 | PARTIAL | `Domain/CompanionIdentity.fs` | 四个公式已实现且有第 1 层测试（可见 sha256 断言字段组合）。旧 `CompanionDelta.bHeadDigest` 已删，`companionMemoryMessageId` 成为唯一 synthetic 头部身份。仍缺门禁证明无 GUID / random / 当前时间 |

## Execution

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| EXEC-004: Join 语义 | CONFORMANT | `Journal/LinkageProjection.fs` `HandleController.retire` `HostForkRuntime.Join` | single-assignment cell：首个完成者唯一生效，后到者报 `AlreadyCompleted` 而非覆盖；`Join()` 消费后写 `HandleRetired`，且退休失败时不交出 completion（否则调用方以为已消费而 journal 仍在提供）。第 1 层测试覆盖三种 completion kind 与四种拒绝理由 |
| EXEC-005: List 语义 | CONFORMANT | `OpenCode/ListTool.fs` `Journal/LinkageProjection.fs` | `list` 按 durable `HandleProjection.listable` 枚举，`CompletedAwaitingJoin` 输出专用状态，`Retired` 被排除。第 1 层测试：`EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired`（`tests-mjs/Execution/handle.test.mjs`） |
| EXEC-009: Handle 持久化 + tombstone | PARTIAL | `Journal/LinkageProjection.fs` `Session/HandleController.fs` `OpenCode/HostForkRuntime.fs` `OpenCode/HostForkChildDispatch.fs` | 三事实、三态投影、typed handle、永久 tombstone 与 `ChildSessionId` 已接线；fork 前置读取 `isRetired`，父取消读取 `activeHandles`，但 `join` 仍走运行期 mailbox，`HandleProjection.joinable` 无生产调用点。本次仅有静态证据，休克期不重跑编译/行为测试 |
| EXEC-011: Process Deadline | CONFORMANT | `Process/ProcessRequest.fs` `Process/Deadline.fs` | `min(3 × estimate, hardLimit)`，`DefaultHardLimit = 1h` 有限；估算为 0/负/NaN/∞ 时回落到硬上限而非「无 deadline」；deadline 存为时刻故 `remaining` 每次由时钟派生、不递减；`nextWaitMs` 钳到 `2^31-1`（JS 定时器超过即立刻触发，会把长 deadline 变成忙循环）。第 1 层测试 |
| EXEC-015: PTY 行为 | CONFORMANT | `Process/Pty*.fs` | onExit-only completion 已验证 |

### EXEC-009 读侧接线复核（b48e38bd）

前一版记录的「四个 durable HandleProjection 读侧零调用点」已过时。当前静态调用点为：

```text
HandleProjection.listable        OpenCode/ListTool.fs
HandleProjection.joinable        0（JoinTool 仍走运行期 mailbox）
HandleProjection.activeHandles   OpenCode/HostForkChildDispatch.fs
HandleProjection.isRetired       OpenCode/HostForkRuntime.fs
```

这与包 X8 之前的 `buildAttemptExecutionProfile` 是同一形态：函数正确、有测试、
且没有任何生产路径走它。差别在于那一个是唯一写入口（门禁能问「谁在绕过」），
这四个是唯一读入口——而「没有人读」不构成任何现有门禁的违规。

当前调用点覆盖 `list`、父取消和 fork 前置 tombstone；`join` 仍未消费 durable
`joinable`，因此 `CompletedAwaitingJoin` 的 durable 消费链尚未闭合。
该结论只绑定静态源码与 `shock-audit`；`EXEC-005` 可在退火三取得 Host 轨迹后升格，
`EXEC-009` 还需先迁移 JoinTool 的 durable 读侧。

## Host 集成

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| HOST-002: 唯一允许进入业务层的信号 | CONFORMANT | `HostSignalAdapter.fs` | — |
| HOST-003: Transport 与 Domain 分离 | CONFORMANT | `HostSignal.fs` | — |
| HOST-004: Reconciler | CONFORMANT | `SessionReconciler.fs` | single-flight + dirty latch 已实现 |
| HOST-005: A 版分段 | PARTIAL | `TerminalSessionA.fs` | ARecord 未按 ProviderRun 分段 |
| HOST-006: Compaction 预防与收容 | CONFORMANT | `OpenCode/HostCompactionGate.fs` `OpenCode/HostSignalBootstrap.fs` | 预防层四项（auto/overflow/autocontinue/prune）关闭，写不进配置则启动失败；收容层把任何观察到的 compaction 转成一次 `ContextReanchored`（永远 armed，幂等，不分类来源）。第 1 层测试 14 项（`Context/host-compaction-policy.test.mjs`） |
| HOST-008: Session 关联 | CONFORMANT | `Journal/SessionAssociation.fs` `Domain/ManagedSessionKind.fs` | `ManagedSessionKind` 持久化，`isCompanion` O(1)；递归/重复 Y/自链/一 Y 两主均 fail closed；role 不影响关联。第 1 层测试 16 项（`Context/session-association.test.mjs`） |
| HOST-009: Host 生命周期 | UNVERIFIED | `SpikePlugin.fs` `PluginRuntimeScope.fs` | 当前仅注册 `experimental.chat.messages.transform` 一次；dispose 通过 `PluginRuntimeScope` 收束资源。判据在退火三进行中：host-restart / host-nudge canary 覆盖生命周期（HOST-012 已实测多实例加载——`initSpikePlugin` 每 project 一次） |
| HOST-010: Transform → ProviderRunIdentity 绑定 | PARTIAL | `OpenCode/ReviewSeal.fs` `OpenCode/SessionSnapshotPort.fs` | transform 已通过 session snapshot 绑定唯一最新未完成 assistant；缺 snapshot、user、候选或最新性时 fail closed。仍缺 HOST 版本升级 canary 对 transform id 与同 run `ToolContext.messageID` 的直接断言 |
| HOST-011: Tool 身份两个半边 | PARTIAL | `OpenCode/ToolHostCodec.fs` | `messageID` / `callID` 在 adapter 边界直接构造 typed identities，缺失时 VerdictTool fail closed；`userMessageID` 不存在且不读取。仍缺 HOST 版本升级 canary |
| HOST-012: 多实例共享边界 | CONFORMANT | `OpenCode/SharedState.fs` `OpenCode/PluginRuntimeScope.fs` | Host `InstanceStore` 按 directory 实例化插件（worktree = 独立实例），fork→verdict 跨实例。SessionParents/VerdictSessions/SessionDirectories 为 `SharedState` 模块级单例（所有插件实例同一引用）；每实例独有 AgentJournal/Companions/OwnedSessions/订阅。实测 deep-reviewer 的 verdict 不再 "manager session is unknown" |

## 验证

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| VERIFY-001: 测试金字塔 | PARTIAL | `scripts/` `tests-mjs/` `testkit/` | 第 0 层静态检查器已齐备，本次 `gate:static` 六门通过；第 1–2 层在 `tests-mjs/`，第 3 层 Fake Host 轨迹与第 4–5 层 canary/发布门禁正在退火三逐级恢复，尚未全绿 |
| VERIFY-003: Canary Mock 剧本 | PARTIAL | `testkit/opencode/scenario-runtime.js` `testkit/opencode/strict-mock-provider.js` | K9 已物理删除旧匹配路径（`strict-mock-forest.js` / `strict-mock-satisfy.js` 删除，`strict-mock-matches.js` 仅剩 request-kind 分类与诊断 extractor，provider 无 scenario 时一律记未匹配）。六项旧违规机制随删除消失；静态森林由 gate-testkit 的 VERIFY-003 系列守护。canary 运行期证据正在退火三逐条取得 |
| VERIFY-004: Stability Gate | CONFORMANT | `run-canary-staggered.mjs` `stability-checker.js` | 三轮 + leak check 已实现。原理与实现的偏差（含「断言心跳从未接线」）记于 `shock-anneal.md` 包 W |
| VERIFY-005: Architecture Gates | CONFORMANT | `scripts/architecture-gate.mjs` | 已是静态检查器而非测试，故不需先编译即可运行；单一写入口门禁实现为 `SINGLE_WRITER_FACTS`，八个事实实测 ok (1)；`single-constructor` 双向检查（无人绕过 + 有人调用）。行数门禁按条款保持删除 |
| VERIFY-007: 两种 Provider Projection | CONFORMANT | `Domain/ProviderProjection.fs` `testkit/opencode/provider-wire.js` | 生产侧两投影分离；testkit 仅解码 OpenAI wire 再调生产 projection。判据：`gate-projection-cases.mjs` 的 `semanticallyEqual` / `isAppendOnlyPrefix` / `sealDigest` 用例，gate-testkit 278 全绿实测 |
| VERIFY-008: 测试语言边界 | CONFORMANT | `tests-mjs/` `scripts/architecture-gate.mjs` | 生产保持 `.fs`，第 1–3 层测试保持 `.mjs` 并直接 import `build/next`；`tests-next/` 已删除，Fable 约定集中在 `domain.mjs`（`domain.meta.test.mjs` 锁三个静默陷阱）。判据：test:mjs 438 全绿 + architecture-gate 实测 + `domain.meta.test.mjs` 契约 |

### VERIFY 段此前四行失效（包 T-5 更正）

与 Fallback / Review 两段同一成因，四行里三行仍指向已删除的 `tests-next/`（详细对照见
`docs/archive/shock-anneal-2026/FINAL-REPORT.md` §8）。

VERIFY-005 这一行尤其值得记档。 它声称「单一写入口门禁未实现」，而门禁不仅实现了、
还在包 X8 抓出了 `buildAttemptExecutionProfile` 零调用点。一个标着「未实现」的门禁
没人会去看它的输出——状态表往乐观方向偏移会让人跳过检查，往悲观方向偏移会让人
忽略已有的保护。两个方向都在削弱同一份判据。

## 持久化

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| PERSIST-001: Envelope 结构 | CONFORMANT | `Journal/Envelope.fs` | 单行自包含、跨时区字节稳定（`ObservedAt` 编码前钉到 offset 0）、全序排序键。第 1 层测试在三个 TZ 下运行 |
| PERSIST-002: Append 原子性 | CONFORMANT | `Journal/Writer.fs` | 两结局；`wx` 拒绝同 RuntimeId 第二个 writer；已释放 writer 返回 `CommitUnknown` 而非抛异常。第 2 层测试 |
| PERSIST-004: 尾部损坏 | CONFORMANT | `Journal/Boot.fs` | 尾部截断静默丢弃；中间损坏 / LocalSeq 跳号 / RuntimeId 不匹配三者均在损坏处截断并报诊断；单个流损坏不牵连健康流。第 2 层测试 |
| PERSIST-005: 旧 Schema | CONFORMANT | `FactCodec.containsLegacyFallbackFields` | 发现旧 schema 直接失败；退役事实名按名诊断而非报编解码错误 |
| PERSIST-006: 文件权限 | CONFORMANT | `Journal/Writer.fs` | `mkdirSync` 传 `mode = 0o700`、`openSync` 传 `0o600`，创建时设定而非事后 chmod。第 2 层测试在 `umask 000` 下断言 |
| PERSIST-008: Projection 查询 | CONFORMANT | `Journal/Fold.fs` `OpenCode/TerminalPolicy.fs` | 多数 projection 是 O(1) 积分；`TerminalPolicy.tryLinkedChild` 现经 Fold 维护的 `HandleByChildSession` 全局索引（child → record，retired 保留）单键查询，不再跨 session 扫描 |
| PERSIST-009: Durable Effect 协议 | PARTIAL | `EffectProjection.fs` `Session/Companion.fs` | Companion 无 Journal 时现在返回 `DurableJournalUnavailable` 并不发送；Prompt 发送与 Git publish 的 durable effect 仍未闭合 |

### PERSIST-001 与 PERSIST-006 的两处实测缺陷（包 T-2 修正）

两条都标着 `CONFORMANT` 或未核验，但都不成立。共同点是缺陷只在测试真的观察物理产物时才可见。

`serialize` 渲染读者的本地时区偏移。 `Encode.Auto` 直接编码 `DateTimeOffset`，而 `Decode.Auto` 解码时挂上读者的本地 offset。写入侧永远传 `DateTimeOffset.UtcNow` 所以新写的行没问题，但「读一行再写回」在 `TZ=Asia/Shanghai` 上把同一时刻渲染成 `+08:00`。两台不同时区的机器会对同一份历史产出不同字节，副本的字节比对报出并不存在的差异。修正是编码前 `ToOffset TimeSpan.Zero`。

不用 `ToUniversalTime()`：Fable 的 `toUniversalTime` 让产出值的 `offset` 字段为 `undefined`，编码器于是靠巧合渲染出裸 `Z` 而非按契约渲染。

权限位从未设定。 `mkdirSync` 只传 `recursive`，`openSync` 只传 flags，所以实际权限是 umask 的结果——默认 umask 022 下是 755/644，而 PERSIST-006 要求 700/600。journal 行里有 session id、Git tree hash、prompt payload 摘要。修正是在创建时传 mode，不是事后 chmod：mkdir 与 chmod 之间目录是全局可读的。

测试必须让生产自己创建目录。 `mkdtemp` 本身就产出 0700，所以在 `mkdtemp` 的结果上直接断言会在生产完全不设 mode 的情况下通过。facade 因此把 journal 目录放在临时目录内的一个子路径，由 `JournalWriter.create` 创建。

## 零调用点的唯一构造函数（PROMPT-008，已解决）

包 X5 补 `RequestKind` / `ProjectionChoice` 时发现 `buildAttemptExecutionProfile`
全仓调用点为 0。包 X8 落地 `Domain/AttemptPlanner.plan` 作为唯一调用点后解决。

值得留档的是这个缺陷为何能存在这么久。包 0d 的记录写「profile 本身尚未作为参数
贯通全链（各处仍分别读 `ActiveLogicalRun`）」，措辞把它说成覆盖不足；实际是
没有任何一次 provider request 从这个 profile 出发构造，而那正是 PROMPT-008
禁止的状态。措辞的温和程度掩盖了状态的严重程度。

门禁也没抓住它，因为它只问「谁在手工构造这个类型」。答案是没有人——包括本该
构造它的那个函数的调用方。一个零调用点的构造函数通过所有「不得绕过」检查，
因为无路可绕。

`single-constructor` 门禁现在检查两侧：无人手工拼装，且 builder 至少有一个调用点。
当前唯一调用点是 `AttemptPlanner.plan` 内部对 builder 的调用；门禁不等价于检查
`AttemptPlanner.plan` 自身是否被 provider 发送链调用。

X-wire 接线后（`c6ac0eb1…5ff3c53a`）该缺口闭合：`AttemptPlanner.plan` 被
`XWire.applyTransform` 与 `CompanionTransform` 真实调用，profile 携带
`RequestKind` / `ProjectionChoice` 进入 transform 边界的投影决策，`reconcileAttempt`
从同一 profile 判 promote。本表 PROMPT-008 行相应从 `CONTRADICTS` 升为 `CONFORMANT`。

## 失败驱动上下文恢复（SSOT/12）

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| CTX-001: 不观察上下文容量 | CONFORMANT | 全仓灭绝表 | 包 X9 灭绝 `estimateTokens` / `shouldSwitchEpoch` / `CompanionBudgetStore` 等全部容量观察；唯一字节计量是 CTX-003 的 `BloggerDeltaLimitBytes`（输入合同，不与窗口比较）。第 1 层测试 `CTX_001_no_prompt_carries_a_token_count_or_output_budget` |
| CTX-002: 不主动预测溢出 | CONFORMANT | `OpenCode/CompanionTransform.fs` | 主动压缩层整体删除（注释逐条列明旧机制）；恢复只由真实失败驱动——`HostSignalBootstrap.onTurn` 失败后 `ArmRecovery`，下一次 transform 才规划探针。第 1 层测试在 `Context/` 各文件 |
| CTX-003: 最低上下文环境合同 | CONFORMANT | `Domain/BloggerDelta.fs:41` | `DeltaLimitBytes = 200 * 1024`，约束渲染后 UTF-8 字节数；与模型窗口无关。第 1 层测试 `CTX_003_delta_limit_is_200_KiB` 等 |
| CTX-004: 输出预算属于 provider | CONFORMANT | `Domain/TerminalValidity.fs` | 唯一内容级校验（非空 + 非 XML-only），无供应商依赖；repair 预算由 `ClaimSequences` 派生。第 1 层测试 5 项 |
| CTX-005: 失败不分类 | CONFORMANT | `Domain/RecoverySlot.fs` | `AttemptOutcome` 只有 `Completed` / `CompletedInvalid` / `Failed` / `Aborted`，无 Overflow 分支；Failed/Aborted 同路径；HOST-006 收容不分类来源。第 1 层测试 `CTX_005_Failed_and_Aborted_take_the_identical_path` 等 |
| CTX-006: 恢复槽的两种动作 | CONFORMANT | `Domain/RecoverySlot.fs` `OpenCode/XWire.fs` `Session/CompanionHostBlogger.fs` | `mayRecover` 三条件合取（armed + primed + material）；X 走 prefix probe（无额外 LLM），Y 走 squash（一次额外 LLM）。两条生产链均已接线。第 1 层测试含 parked-cursor 反例 |
| CTX-007: Attempt 三结局按 RequestKind 分派 | CONFORMANT | `Domain/RecoverySlot.fs` `Session/CompanionHostBlogger.fs` | `onSquashOutcome` / `onMainOutcome` 覆盖六行分派表；`CommitSquashThenMain` 是 `BlogSquashCommitted` 唯一提交路径（`AppendSquash`）；repair 一次预算。第 1 层测试 9 项 |
| CTX-008: 恢复槽的失败计数 | CONFORMANT | `RecoverySlot.advancesCursor` | 恰好一个决策推进 cursor；squash 成功不清零 count。第 1 层测试 `CTX_008_only_a_failed_slot_advances_the_cursor` |
| CTX-009: X 不发压缩请求 | CONFORMANT | `OpenCode/XWire.fs` | X 的恢复操作只有本地投影变换（前缀替换），无摘要/压缩 LLM 请求路径 |
| CTX-010: X 前缀替换是 attempt-local probe | CONFORMANT | `Domain/XPrefixProjection.fs` `Domain/AttemptPlanner.fs` | probe 是不可变 `AttemptExecutionProfile.ProjectionChoice` 的一部分（PROMPT-008），非 session 状态；`promotableProbe` 只接受 Completed+valid；失败 probe 无任何事实。第 1 层测试 13 项 |
| CTX-011: 覆盖游标与候选选择 | CONFORMANT | `Domain/PrefixProbeSelection.fs` `Domain/BloggerDelta.fs` | `SemanticCursor` / `Coverage` 两量分离；候选选择 9 步含 cutoff proof、digest fail-closed、squash 让 B 更紧凑仍为新候选。第 1 层测试 14 项 |
| CTX-012: 提交语义 | CONFORMANT | `OpenCode/XWire.fs` `Journal/BlogProjection.fs` | `PrefixRebaseCommitted` 唯一 writer 是 `XWire.reconcileAttempt`；probe SealRoot 被 promote 原样继承；squash 永久提交不回滚、级联成立、宽度 ceil(m/2)。第 1 层测试跨 4 文件 |
| CTX-013: BloggerDeltaProjection 与 TOML 编码 | CONFORMANT | `Session/BloggerDelta.fs` `Session/BloggerToml.fs` `Session/SyntheticToml.fs` | 三级切块 + 硬截断 + 图片 omitted marker + 确定性 TOML（固定键序、`'''` 字面量、closing 独占一行、无时间/随机/Host ID）；instruction header 计入 200 KiB。第 1 层测试 55 项跨 3 文件 |
| CTX-014: 诊断可观测性边界 | CONFORMANT | `OpenCode/Diagnostic.fs` `OpenCode/HostCompactionGate.fs` | 统一 schema：`Diagnostic.emit` 校验字段白名单（CTX-014 清单），白名单外字段 fail closed；禁止字段名在 src/Wanxiangshu.Next/**/*.fs` 的负向扫描 + 白名单外字段拒绝测试（`tests-mjs/Context/ctx014.test.mjs`） |

## 未列入本表的条款

`AGENT-*` 中除 AGENT-007 外的条款（20 个 Agent、能力矩阵、内部 Agent 不可见）与 `REVIEW-001` `REVIEW-002`
`REVIEW-008` `REVIEW-009`、`ORCH-004`、`EXEC-001` ~ `EXEC-003`
`EXEC-006` ~ `EXEC-008` `EXEC-010` `EXEC-012` ~ `EXEC-014`、`COMPANION-006`
`COMPANION-007` `COMPANION-012`、`HOST-001` `HOST-007`
`ARCH-005` `ARCH-006` `ARCH-008`、`VERIFY-002` `VERIFY-006`、
`PERSIST-003` `PERSIST-007` 当前未逐条核验，
状态为 `UNVERIFIED`。

这不是「大概符合」——是尚未产生判据。休克—退火迁移收口时未逐条补齐，属已知未闭合项
（记入 `docs/archive/shock-anneal-2026/FINAL-REPORT.md` §15）；发布前本表不得存在
`UNVERIFIED`。

## 反向缺口：有生产契约但无条款（包 T-5e 发现，已由 ARCH-009 补齐）

本表的形状假设每份判据都对应一条条款。写 `Parallel.mapBounded` 的测试时出现了相反的
情况：一个跨领域共享的原语，行为契约真实存在，而 `SSOT/` 没有任何条款管它。

```text
有条款可借   并发上限、取消传播        VERIFY-004（无界扇出使「慢」与「死」不可区分）
无条款覆盖   结果保序、空输入短路、
             拒绝非正上限、拒绝后
             siblings 继续运行         —
```

测试一度分两种命名：前者 `VERIFY_004_`，后者 `mapBounded_`。不给后者硬套条款 ID 是对的
——那会在测试名里放一个 `SSOT/` 并不背书的断言，而 `ssot-lint` 只读 `SSOT/` 目录，
测试名里的伪造引用没有任何机器会发现。

但「不硬套」不等于「就这样放着」。 缺口的正确处置是补写规范，不是给它起个中性名字然后
记档。`ARCH-009` 已写入 `SSOT/01.md`，本文件全部 12 个测试改名为 `ARCH_009_`，
`VERIFY-004` 的借用一并撤回——并发上限与取消现在是 ARCH-009 自己的小节，VERIFY-004
只提供理由（无界扇出使失败取决于机器负载）。

条款把最反直觉的一条写成了明文。 拒绝后 siblings 不被取消。`mapBounded` 用
`Promise.all`，所以一个 action 抛出时调用立即 reject，但已获得许可的 action 会在后台
跑到完成。把 reject 当成「不会再发生任何事」的调用方是错的，而这里没有取消机制能纠正它
——token 传给了每个 action，停止 siblings 是 action 的责任而非组合子的。

这不是缺陷，是 `Promise.all` 的语义。而恰恰因为它不是缺陷，它才必须进规范：只靠一个
测试锁住的反直觉行为，下一个读到 reject 的人有权认为语义是相反的。

留档的方法论结论。 `UNVERIFIED`（判据未产生）与「规范未表达」是两类缺口，本表原先只有
表达前者的位置。后者更隐蔽，因为它不会在任何一列里显示成非 `CONFORMANT`——代码正确、
测试全绿、状态表满分，而约束根本不存在于规范里。发现途径也只有一条：写测试时问「这条
断言的权威在哪」，答不出来就是缺口。

## SSOT/14 — Predict & Reduce Strength（`STRENGTH-`）

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| STRENGTH-006…025/079（类型/预测器/控制器/价值/策略） | PURE_CORE_ONLY | `Domain/StrengthTypes.fs` `StrengthPolicy.fs` `StrengthPredictor.fs` `StrengthController.fs` `StrengthValue.fs` + `tests-mjs/Strength/` | 纯领域内核已实现（28 测试）；生产接线（Replica session/transform 挂起/候选帧投影）受 STRENGTH-078 Host canary（C-01…C-21）与阶段 A（Projection DSL 迁移）门禁，未接线 |
| STRENGTH-001…005/026…135（其余） | NOT_IMPLEMENTED | — | 依赖 Host canary 与生产接线，未实现 |

## SSOT/15 — Blogger as Enforcer（`ENFORCER-`）

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| ENFORCER-020…043/080…102/170…172（codec/throttle/nudge/cycle/catalog） | PURE_CORE_ONLY | `Domain/EnforcerCatalog.gen.fs` `EnforcerCodec.fs` `EnforcerThrottle.fs` `EnforcerNudge.fs` `EnforcerCycle.fs` + `tests-mjs/Enforcer/` | 纯领域内核已实现（39 测试，120 项规则目录生成）；生产接线（blog 工具注册/BlogEntryCommitted 扩展/Nudge 事实/transform 挂起）受 ENFORCER-180 第 0 步九条 Host canary 阻断门，未接线 |
| ENFORCER-001…019/044…079/103…201（其余） | NOT_IMPLEMENTED | — | 依赖 Host canary 与生产接线，未实现 |

## SSOT/16 — Student & Teacher（`LEARN-`）

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| LEARN-015…017/024/032…037/050/051/075（角色/tier/工具面/QA/单飞/return） | PURE_CORE_ONLY | `Domain/StudentTeacher.fs` `Domain/PrefixCandidate.fs` + `tests-mjs/StudentTeacher/` | 纯领域内核已实现（15 测试，ProviderRequestKind 扩展 StudentLearn/StudentCompile）；生产接线（teacher/return 工具/Teacher session/QA 落盘）受 Host canary（LEARN-082…088）门禁，未接线 |
| LEARN-001…014/018…023/025…049/052…074/076…114（其余） | NOT_IMPLEMENTED | — | 依赖 Host canary 与生产接线，未实现 |

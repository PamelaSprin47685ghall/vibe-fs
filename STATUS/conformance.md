# STATUS/conformance — SSOT 条款合规表

状态允许值：`CONFORMANT` | `PARTIAL` | `CONTRADICTS` | `UNVERIFIED` | `NOT_IMPLEMENTED`

绑定 commit：`274a30aa`（pre-shock baseline）。休克期开始后本表随工作包推进更新。

休克期内已迁移的条款一律记 `UNVERIFIED`，不记 `CONFORMANT`：编译与测试关闭，代码符合条款只是静态阅读的结论，尚未产生判据。判据在退火一/二恢复后补齐。

## 架构 DNA

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| ARCH-001: 结构化程序替代状态机 | PARTIAL | 全仓 | 无 Stage/Phase/Lease 字段；但 Orchestrator 恢复投影仍是 stage-like 事实序列（见 ORCH-006） |
| ARCH-002: 事件是信号不是数据 | CONFORMANT | `HostSignalAdapter.fs` `HostEventCodec.fs` | 碎片事件在 codec 边界丢弃 |
| ARCH-003: 不修改 OpenCode 本体 | CONFORMANT | 全仓 | 只用现有 Hook 与 SDK API |
| ARCH-004: LLM 前缀缓存保护 | PARTIAL | `CompanionProjection.fs` | FrozenB/LatestB 已分离；冷边界靠 mock 嗅探而非显式声明（见 VERIFY-003） |
| ARCH-007: 不同语义不同工具名 | CONFORMANT | `ForkTool.fs` | `fork-agent` / `fork-manager` / `fork-pty` 已分离 |

## Prompt Authority 与 Dispatcher

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| PROMPT-002: Authority Root 固定执行画像 | UNVERIFIED | `PromptAuthorityRun.createAuthorityRoot` | 包 A：唯一构造入口；`AuthorityExecutionProfile` 无 model 字段，故「root 覆盖 model」不可表达 |
| PROMPT-003: Continuation | UNVERIFIED | `PromptDispatcherSend.SendContinuation` | 包 A：继承 run 与 root，EffectiveAgent 参与 PromptKey |
| PROMPT-004: 来源类型 | UNVERIFIED | `PromptIngress.fs` `Fact.AuthorityRootAccepted` | 包 A/0b：`HumanPromptAccepted` 已替换；解析顺序为「journal 已知 → 显式 managed agent」，已知来源不得被 agent 字段改判 |
| PROMPT-005: 四阶段协议 | UNVERIFIED | `PromptDispatcher.fs` `PromptDispatcherSend.fs` | 包 A：四事实齐备；`AdmittedWithReceipt` 止于 Submitted，物理受理仅由 `chat.message` 产生 |
| PROMPT-006: 发送格式 | UNVERIFIED | `PromptDispatcherSend.fs` | 包 A：两处发送点均 `Model = None`，Agent 由 EffectiveAgent 绑定 |
| PROMPT-007: Fire-and-forget 定义 | UNVERIFIED | `HostSessionNudge.sendContinuation` | 包 A：`prompt_async` 在 `next/` 仅 1 处（唯一 Host adapter）；五条绕过 Dispatcher 的直发分支已删 |
| PROMPT-008: 原子 AttemptExecutionProfile | CONFORMANT | `Domain/AttemptPlanner.plan` | 唯一构造函数现有唯一调用点；`single-constructor` 门禁同时检查「无人绕过」与「有人调用」两侧 |
| PROMPT-009: 来源解析顺序 | UNVERIFIED | `PromptAuthorityRun.resolveKnownOrigin` | 包 A：按 session 读投影（PERSIST-008），未知来源 fail closed |
| PROMPT-011: 未决发送恢复 | PARTIAL | `Domain/PromptAuthority.derivePromptKey` | 包 A：PromptKey 已按条款派生并写入 Host metadata，`ClaimSequence` 由 fold 推进。仍缺启动期 tail window 查找与 `RecoveryAttemptBudget = 3`（属清场期） |

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

本段在包 T-3 之前的内容描述的是迁移前状态，七行里没有一行仍然成立：

```text
FALLBACK-002 「无 ConsecutiveFailureCount 字段（当前是 LastProviderAttempt）」
             → 字段自包 0b 起就存在，LastProviderAttempt 已不在仓库中
FALLBACK-003 「两个 writer 经 recordFallbackFailure 写同一事实，实测 3 writers」
             → 包 C 已合一，recordFallbackFailure 全仓 0 处，shock-audit ok (1)
FALLBACK-004 「成功清零 count 未实现（无 count）」            → 已实现
FALLBACK-005 「无 AutoRecoveryBudget；无 FallbackExhausted 事实」→ 两者都有
FALLBACK-007 「缺三个字段；无 Fold 验证」                     → 六字段齐备，Fold 验证在
FALLBACK-010 「当前无 count 概念，故无从混淆」                 → 有 count，且已有结构性保证
```

失效的原因值得记档。 这些行是包 C 完成时该更新而没更新的。休克期关闭了测试反馈，而
`conformance.md` 的更新一直依赖手工，于是「代码前进、状态表留在原地」这个方向的偏移没有
任何机器会发现——`ssot-lint` 只检查条款 ID 与实现状态词的分离，不检查状态词是否真实。

这正是 AGENTS.md 第 1 节警告的第二种失败形态的镜像：那条讲的是「写完才看文档」，这里是
「改完没回写状态」。两者的共同后果一样——`conformance.md` 与代码的偏离多一处，而且偏在
乐观方向时更危险：一个标着 `CONTRADICTS` 的合规项只是噪音，一个标着 `CONFORMANT` 的
违规项会让人跳过检查。

本次更正后 Fallback 段的每一行都绑定到一个第 1 层测试或一次 `shock-audit` 实测输出。

## Review

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| REVIEW-002: REVISE | CONFORMANT | `ReviewProjection.applyVerdict` | 任何 REVISE 清除未完成的 PERFECT（`PendingChallenge` 置空、witness 转 `RevisionWitness`）。第 1 层测试 |
| REVIEW-003: 因果证明 | CONFORMANT | `ReviewController.provenSeal` `ReviewWitness.isDistinctAttempt` | 条件 1–5 为两个 witness 的纯比较；条件 6 由 seal 判定——`Set.contains ChallengeContentDigest seal.IncludedToolResultDigests`，无 seal 即 fail closed。四种弱代理在 `next/` 全为 0（`AcceptedContinuationRoots`、`samePhysicalRootReevaluation`、`GuardPromptAccepted`、`RecentProviderRunIds`）。第 1 层测试逐条移除证据成分并断言确认消失 |
| REVIEW-004: ReviewAttemptIdentity | CONFORMANT | `Kernel/Identity.fs:348` | 五元组类型存在，`dedupeKey` 以 `\u001f` 连接；同一 provider run 内的额外 PERFECT 由 `PendingChallenge.FirstProviderRun` 比对拦下，不计数不写 journal；窗口上限 8（PERSIST-008） |
| REVIEW-005: 因果单调状态 | CONFORMANT | `VerdictDecision` `PromptClaim` `ProviderInputSeal` | 第二次 PERFECT 只有三种答案（`Confirmed` / `ChallengeUnproven` / `AlreadyCounted`），无 `Confirmed of bool` 形态。两条链各归其主：链 A 在 `PromptClaim`（`Receipt` = Submitted，`acceptClaim` = PhysicalAccepted），链 B 在 seal。确认只读链 B——`provenSeal` 是唯一路径，admission ID 无从参与 |
| REVIEW-006: 自包含 Witness | CONFORMANT | `Domain/ReviewWitness.fs` | `confirm` 接收两个摘要而非 bool，故 witness 自带证据；不依赖外围 Map。第 1 层测试直接断言生产 record 的键集合（而非 facade 投影），确保无 authority root / physical message 字段 |
| REVIEW-007: Manager Guard | PARTIAL | `HostReviewGuard.fs` `TerminalPolicy.isTopLevelManager` | 纯侧已有判据：requirement 按 Authority Root 键入并去重、确认后清除且对同一 run 幂等（第 1 层测试）。Host 侧 terminal 钩子接线属第 3 层，退火三补 |
| REVIEW-008: Git tree 变化使 witness 无效 | CONFORMANT | `ReviewWitness.isValidForTree` | 有效性是对当前 tree 的派生问题而非 mutation：witness 历史保留且仍报 `Confirmed`，但 `satisfiesGuard` 对新 tree 为 false。新 barrier 清 pending 而保留 witness；同 barrier 重入幂等。第 1 层测试 |
| REVIEW-010: ProviderInputSeal | CONFORMANT | `OpenCode/ReviewSeal.fs` `Journal/ReviewProjection.fs` | `shock-audit` 实测单一写入口 ok (1)。seal 记录 `IncludedToolResultDigests`，Fold 由 list 转 `Set<string>`；窗口上限 8。可实现性见 `evidence/host-transform-run-binding.md` |

### Review 段此前的记录同样失效（包 T-3 更正）

与 Fallback 段同一成因，六行里五行描述迁移前状态：

```text
REVIEW-004 「类型不存在；去重只靠 RecentProviderRunIds 列表」
           → 类型在 Kernel/Identity.fs:348，RecentProviderRunIds 在 next/ 为 0
REVIEW-005 「无链 A / 链 B 分离；admission ID 仍可参与确认」
           → VerdictDecision 三答案已就位；provenSeal 是确认的唯一路径
REVIEW-006 「Witness 依赖外围 Map 补齐身份」→ confirm 接收摘要，witness 自带证据
REVIEW-003 「seal 因果判定本体属包 D」    → 包 D 已完成，判定在 provenSeal
REVIEW-010 「transform 侧封装与因果校验属包 D」→ 同上
```

`GuardPromptAccepted` 在 `next/` 仍有 1 处，但那是 `FactCodec` 的 pre-0.5.0 标记名单——
它必须留着，否则旧 journal 会以晦涩的 union 错误失败而不是给出迁移提示。这类「旧符号
作为拒绝清单条目」的残留与真正的旧调用点不同，`shock-audit` 的计数看不出区别，故在此
记名。

## Orchestrator

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| ORCH-001: `fork-manager` 命名 | CONFORMANT | `ForkTool.fs` | — |
| ORCH-002: Clean Gate | CONFORMANT | `OrchestratorGit.fs` | — |
| ORCH-003: 一个 Job 一个 worktree 一个 Manager | PARTIAL | `Journal/OrchestratorProjection.fs` `OrchestratorHost.runManager` | 包 0c 持久化 `ManagerAgent`；包 B 删除了未命中时回落 `fast-manager` 的分支（改报错）。冲突路径复用同一 Manager 属包 G |
| ORCH-005: 短 CAS Integration Gate | CONTRADICTS | `Orchestrator.IntegrationGate.fs` | 锁持有跨 review 期间 |
| ORCH-006: 持久事实 | CONTRADICTS | `Kernel/Fact.fs:111-151` | 事实集是 stage-like（`CandidateRegistered` / `Rebased` / `Pre-` `PostRebaseReviewConfirmed`）；缺 `ManagerAgent`、`WorktreeIdentity`、`TargetBranchFrozen`、witness ID |
| ORCH-007: 恢复逻辑 | PARTIAL | `Orchestrator.Recovery.fs` | `PublishClaimed` 恢复无三分支固定顺序判断 |
| ORCH-008: target ref 安全 | CONFORMANT | `OrchestratorGit.fs` | `GetTargetHead` 失败 fail closed 已验证 |

## Agent

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| AGENT-007: 工具权限双层 fail-closed | UNVERIFIED | `ToolRuntimeScope.RoleFor` `ToolRegistry.fs` | 包 B：Role 唯一来源为 `ActiveLogicalRun.CanonicalRole`；`sessionRoles` 三来源链与 Role 未解析时放行 `inspector` 的豁免均已删除 |

## Companion

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| COMPANION-001: 每个 Work Session 都有 Companion | CONFORMANT | `Journal/SessionAssociation.fs` | 角色白名单已删除。关联 API 不接受 role 参数，因此 role 无法影响它不是输入的决定 |
| COMPANION-002: Companion 是叶子 | CONFORMANT | `SessionAssociationProjection.link` | 一次 `link` 写双向条目，`isCompanion` O(1)；递归、重复 Y、抢占 Y、自链四种非法态由 fold fail closed |
| COMPANION-008: 忙时跳过 | CONFORMANT | `Companion.Submit` | 忙时返回 `SkippedBusy` 并原样退出，不推进 coverage、不排队、不计数。「三次 busy skip」计数已删 |
| COMPANION-009: PrefixEpoch | PARTIAL | `Journal/PrefixEpochProjection.fs` `Domain/XPrefixProjection.fs` | 单轨：epoch 递增、snapshot 退役、X 前缀计划全在新投影。旧 `switchEpoch` / `ReplacementActive` / `ActivePrefixEpoch` 双轨已删。差距是 `XPrefixProjection` 尚未接进 transform 边界，当前 X 一律发原始历史 |
| COMPANION-013: Synthetic 稳定身份 | PARTIAL | `Domain/CompanionIdentity.fs` | 四个公式已实现且有第 1 层测试（可见 sha256 断言字段组合）。旧 `CompanionDelta.bHeadDigest` 已删，`companionMemoryMessageId` 成为唯一 synthetic 头部身份。仍缺门禁证明无 GUID / random / 当前时间 |

## Execution

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| EXEC-004: Join 语义 | PARTIAL | `JoinTool.fs` `CompletionMailbox.fs` | single-assignment cell 已实现；join 后不写 `HandleRetired` tombstone |
| EXEC-005: List 语义 | PARTIAL | `ListTool.fs` | 无 CompletedAwaitingJoin 状态 |
| EXEC-009: Handle 持久化 + tombstone | PARTIAL | `Journal/LinkageProjection.fs` | 包 0b/0c：三事实与三态投影已就位。读侧全部悬空——`HandleLinked` 不含 child SessionId，8 处消费者无法由 handle 找到 child。包 B 已删占位 `ChildDispatch.tryCancel`；其余属包 F，含一处 SSOT 例外决策 |
| EXEC-011: Process Deadline | PARTIAL | `Process/Deadline.fs` | 3× estimate 已实现；无管理员 hard limit |
| EXEC-015: PTY 行为 | CONFORMANT | `Process/Pty*.fs` | onExit-only completion 已验证 |

## Host 集成

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| HOST-002: 唯一允许进入业务层的信号 | CONFORMANT | `HostSignalAdapter.fs` | — |
| HOST-003: Transport 与 Domain 分离 | CONFORMANT | `HostSignal.fs` | — |
| HOST-004: Reconciler | CONFORMANT | `SessionReconciler.fs` | single-flight + dirty latch 已实现 |
| HOST-005: A 版分段 | PARTIAL | `TerminalSessionA.fs` | ARecord 未按 ProviderRun 分段 |
| HOST-009: Host 生命周期 | PARTIAL | `SpikePlugin.fs:120,127` | 双 transform hook 注册 + 幂等 guard |
| HOST-010: Transform → ProviderRunIdentity 绑定 | NOT_IMPLEMENTED | — | 绑定判据与 fail-closed 条件均不存在。可实现性已证明 |
| HOST-011: Tool 身份两个半边 | PARTIAL | `ToolHostCodec.fs:149-151` | `messageID` / `callID` 读取正确；`userMessageID` 是死字段（Host 源码中不存在该字段） |

## 验证

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| VERIFY-001: 测试金字塔 | PARTIAL | `tests-next/` `testkit/` | 第 0 层已建（`ssot-lint` / `shock-audit`）；`architecture-gate.mjs` 未建，Gates 仍是测试 |
| VERIFY-003: Canary Mock 剧本 | CONTRADICTS | `strict-mock-forest.js` `strict-mock-matches.js` | 六项违规：`specificity` 打分消歧、`pathCursor` 游标、失败时删 seal 缓存、`requestRoleOf` 反推角色、`loadScripts` 运行期换剧本、`epochCold` / `modelSideCold` 嗅探式冷边界豁免。分析见 `design-script-forest.md` |
| VERIFY-004: Stability Gate | CONFORMANT | `run-canary-staggered.mjs` `stability-checker.js` | 三轮 + leak check 已实现 |
| VERIFY-005: Architecture Gates | PARTIAL | `tests-next/Gates/` | 行数门禁已删除；单一写入口门禁未实现（FALLBACK-003 双写未被拦住）；Gates 仍实现为测试而非静态检查器 |
| VERIFY-007: 两种 Provider Projection | NOT_IMPLEMENTED | `strict-mock-matches.js` 的 `sealProviderVisible` | 只有一个投影，同时承担字节相等与语义相等 |
| VERIFY-008: 测试语言边界 | NOT_IMPLEMENTED | `tests-next/**/*.fs` | 测试仍是 `.fs` + Fable 编译 + 手写 Assert shim |

## 持久化

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| PERSIST-001: Envelope 结构 | CONFORMANT | `Journal/Envelope.fs` | 单行自包含、跨时区字节稳定（`ObservedAt` 编码前钉到 offset 0）、全序排序键。第 1 层测试在三个 TZ 下运行 |
| PERSIST-002: Append 原子性 | CONFORMANT | `Journal/Writer.fs` | 两结局；`wx` 拒绝同 RuntimeId 第二个 writer；已释放 writer 返回 `CommitUnknown` 而非抛异常。第 2 层测试 |
| PERSIST-004: 尾部损坏 | CONFORMANT | `Journal/Boot.fs` | 尾部截断静默丢弃；中间损坏 / LocalSeq 跳号 / RuntimeId 不匹配三者均在损坏处截断并报诊断；单个流损坏不牵连健康流。第 2 层测试 |
| PERSIST-005: 旧 Schema | CONFORMANT | `FactCodec.containsLegacyFallbackFields` | 发现旧 schema 直接失败；退役事实名按名诊断而非报编解码错误 |
| PERSIST-006: 文件权限 | CONFORMANT | `Journal/Writer.fs` | `mkdirSync` 传 `mode = 0o700`、`openSync` 传 `0o600`，创建时设定而非事后 chmod。第 2 层测试在 `umask 000` 下断言 |
| PERSIST-008: Projection 查询 | PARTIAL | `Journal/Fold.fs` | 多数 projection 是 O(1) 积分；`Fold.reviewOwner` 用 `Map.tryPick` 扫描全部 session |
| PERSIST-009: Durable Effect 协议 | PARTIAL | `EffectProjection.fs` | 事实存在；未覆盖 Prompt 发送与 Git publish |

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

`single-constructor` 门禁现在检查两侧：无人手工拼装，且至少有一个调用点。
两条都红过一次以确认门禁真的会响。

## 未列入本表的条款

`AGENT-*` 中除 AGENT-007 外的条款（20 个 Agent、能力矩阵、内部 Agent 不可见）与 `REVIEW-001` `REVIEW-002`
`REVIEW-008` `REVIEW-009`、`ORCH-004`、`EXEC-001` ~ `EXEC-003`
`EXEC-006` ~ `EXEC-008` `EXEC-010` `EXEC-012` ~ `EXEC-014`、`COMPANION-003` ~
`COMPANION-007` `COMPANION-010` ~ `COMPANION-012`、`HOST-001` `HOST-006` `HOST-007`
`HOST-008`、`ARCH-005` `ARCH-006` `ARCH-008`、`VERIFY-002` `VERIFY-006`、
`PERSIST-003` `PERSIST-007` 当前未逐条核验，
状态为 `UNVERIFIED`。

这不是「大概符合」——是尚未产生判据。退火三之后逐条补齐；发布前本表不得存在
`UNVERIFIED`。

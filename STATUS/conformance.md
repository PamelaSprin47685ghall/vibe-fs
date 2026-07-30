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
| PROMPT-008: 原子 AttemptExecutionProfile | CONTRADICTS | `Domain/PromptAuthority.buildAttemptExecutionProfile` | 唯一构造函数存在且已补 `RequestKind` / `ProjectionChoice`（包 X5），但全仓零调用点 —— 见下方「零调用点的唯一构造函数」 |
| PROMPT-009: 来源解析顺序 | UNVERIFIED | `PromptAuthorityRun.resolveKnownOrigin` | 包 A：按 session 读投影（PERSIST-008），未知来源 fail closed |
| PROMPT-011: 未决发送恢复 | PARTIAL | `Domain/PromptAuthority.derivePromptKey` | 包 A：PromptKey 已按条款派生并写入 Host metadata，`ClaimSequence` 由 fold 推进。仍缺启动期 tail window 查找与 `RecoveryAttemptBudget = 3`（属清场期） |

## Fallback

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| FALLBACK-002: Modulo-4 Cursor 与 ConsecutiveFailureCount | PARTIAL | `AgentPairCursor.fs` | Offset 正确；`FallbackCursor` 无 `ConsecutiveFailureCount` 字段（当前是 `LastProviderAttempt`） |
| FALLBACK-003: 统一 FallbackController | CONTRADICTS | `ProviderFailureWakeup.fs:50` + `RetrySignalHandler.fs:84` | 两个 writer 经 `recordFallbackFailure` 写同一事实。`shock-audit` 实测 3 writers |
| FALLBACK-004: 不变量 | PARTIAL | `FallbackProjection.fs` | Authority profile 不变已实现；成功清零 count 未实现（无 count） |
| FALLBACK-005: 有限 Circuit Breaker | NOT_IMPLEMENTED | — | 无 `AutoRecoveryBudget`；无 `FallbackExhausted` journal 事实（`Kernel/Outcome.fs:44` 同名 case 是无关的 terminal outcome） |
| FALLBACK-007: 持久事实 | PARTIAL | `Kernel/Fact.fs:74` | 缺 `PreviousOffset` / `NextOffset` / `ConsecutiveFailureCount` 字段；无 Fold 验证 |
| FALLBACK-008: 空/XML-only terminal | UNVERIFIED | `HostSessionNudge.trySendInteractionRepair` | 包 A：预算改由 `ClaimSequences` 派生（PROMPT-005 `Claimed` 已写），不再依赖无事实支撑的 `RepairClaims` |
| FALLBACK-010: Host Attempt ≠ ConsecutiveFailureCount | UNVERIFIED | `HostSignal.fs` | 当前无 count 概念，故无从混淆；建立 count 后需门禁 |

## Review

| 条款 | 状态 | 当前代码位置 | 差距 |
|------|------|-------------|------|
| REVIEW-003: 因果证明 | PARTIAL | `Domain/ReviewWitness.fs` | 包 0c/A：四种弱代理已清除（`AcceptedContinuationRoots`、`samePhysicalRootReevaluation`、`GuardPromptAccepted`、`Review/Guard.fs`）。seal 因果判定本体属包 D |
| REVIEW-004: ReviewAttemptIdentity | NOT_IMPLEMENTED | `ReviewProjection.fs` | 类型不存在；去重只靠 `RecentProviderRunIds` 列表 |
| REVIEW-005: 因果单调状态 | PARTIAL | `ReviewProjection.fs` | 无链 A / 链 B 分离；admission ID 仍可参与确认 |
| REVIEW-006: 自包含 Witness | PARTIAL | `ReviewWitness.fs` | Witness 依赖外围 Map 补齐身份 |
| REVIEW-007: Manager Guard | UNVERIFIED | `HostReviewGuard.fs` | 包 A：nudge 去重改读 PROMPT-005 `PendingClaims`；`AcceptedGuardKey` 与 `GuardPromptAccepted` 已删 |
| REVIEW-010: ProviderInputSeal | PARTIAL | `Journal/ReviewProjection.ProviderInputSeal` | 包 0b/0c：事实与投影类型已存在。`messages.transform` 侧封装与因果校验属包 D。可实现性见 `evidence/host-transform-run-binding.md` |

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
| COMPANION-001: 服务角色 | PARTIAL | `MessageTransform.shouldCreateCompanion` | 仍从 Agent 字符串解析角色；改读 profile 属包 E |
| COMPANION-002: Eligibility 唯一来源 | PARTIAL | `CompanionTransform.fs` | 判定已只读 `ActiveLogicalRun.SelectedAgent`（包 A/B），且包 B 删除了回写 `sessionRoles` 缓存的两处。六角色判据本体属包 E |
| COMPANION-008: 忙时跳过 | PARTIAL | `CompanionHost.fs` | 存在「三次 busy skip」计数，规范只要求不推进 BlogBase |
| COMPANION-009: PrefixEpoch | PARTIAL | `CompanionProjection.fs` | FrozenB/LatestB 分离已实现；epoch 切换未创建新 SealRoot |
| COMPANION-013: Synthetic 稳定身份 | UNVERIFIED | `CompanionProjection.fs` | 需门禁证明无 GUID / random / 当前时间 |

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
| PERSIST-001: Envelope 结构 | CONFORMANT | `Journal/Envelope.fs` | — |
| PERSIST-002: Append 原子性 | CONFORMANT | `Journal/Writer.fs` | — |
| PERSIST-005: 旧 Schema | CONFORMANT | `FactCodec.containsLegacyFallbackFields` | 发现旧 schema 直接失败 |
| PERSIST-008: Projection 查询 | PARTIAL | `Journal/Fold.fs` | 多数 projection 是 O(1) 积分；`Fold.reviewOwner` 用 `Map.tryPick` 扫描全部 session |
| PERSIST-009: Durable Effect 协议 | PARTIAL | `EffectProjection.fs` | 事实存在；未覆盖 Prompt 发送与 Git publish |

## 零调用点的唯一构造函数（PROMPT-008）

`buildAttemptExecutionProfile` 存在、被 `single-constructor` 门禁保护、字段完整，
且在包 X5 补齐了 `RequestKind` 与 `ProjectionChoice`。全仓调用点为 0。

```text
grep -rn "buildAttemptExecutionProfile" next/ | grep -v Domain/PromptAuthority.fs
→ 无输出
```

包 0d 的记录写的是「profile 本身尚未作为参数贯通全链（各处仍分别读
`ActiveLogicalRun`）」，措辞把它说成覆盖不足。实际状态更严重：没有任何一次
provider request 是从这个 profile 出发构造的。 每个发送路径仍各自读
`ActiveLogicalRun` 拼出自己需要的字段，而 PROMPT-008 禁止的正是这件事。

因此状态从 `PARTIAL` 改为 `CONTRADICTS`。前者暗示部分路径已合规，而事实是零条。

门禁没有抓住这一点，因为它检查的是「谁在手工构造这个类型」，答案是没有人——
包括本该构造它的那个函数的调用方。一个零调用点的构造函数通过所有「不得绕过」
类型的检查，因为无路可绕。

贯通归包 X8：probe 候选必须进入 profile（CTX-010 要求候选只对一次 attempt 有效），
而那正是第一个真正需要完整 profile 的调用点。在此之前先补一条第 0 层门禁：
`buildAttemptExecutionProfile` 调用点数为 0 时报错。

## 未列入本表的条款

`AGENT-*` 中除 AGENT-007 外的条款（20 个 Agent、能力矩阵、内部 Agent 不可见）与 `REVIEW-001` `REVIEW-002`
`REVIEW-008` `REVIEW-009`、`ORCH-004`、`EXEC-001` ~ `EXEC-003`
`EXEC-006` ~ `EXEC-008` `EXEC-010` `EXEC-012` ~ `EXEC-014`、`COMPANION-003` ~
`COMPANION-007` `COMPANION-010` ~ `COMPANION-012`、`HOST-001` `HOST-006` `HOST-007`
`HOST-008`、`ARCH-005` `ARCH-006` `ARCH-008`、`VERIFY-002` `VERIFY-006`、
`PERSIST-003` `PERSIST-004` `PERSIST-006` `PERSIST-007` 当前未逐条核验，
状态为 `UNVERIFIED`。

这不是「大概符合」——是尚未产生判据。退火三之后逐条补齐；发布前本表不得存在
`UNVERIFIED`。

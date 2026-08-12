# 词汇表

词汇表只做导航，不形成独立条款；一个术语可指向定义其不同侧面的多个正式条款。冲突时以被指向的正式定义为准。

## A

| 术语 | 指向 |
|------|------|
| AABBAABB | FALLBACK-002：SelectedAgent/PeerAgent 永久循环 |
| ActivePrefixEpoch | COMPANION-009：冻结的 record prefix 快照 |
| AgentFinality | EXEC-020：Completed \| Failed \| Abandoned；ABORTED 不是 agent 终态 |
| AgentOwnerRoot | PROMPT-004：插件创建新工作的 Authority Root |
| AgentTier | AGENT-001：Fast / Deep |
| armedByFailure | FALLBACK-012：局部控制流事实，非持久状态 |
| AttemptExecutionProfile | PROMPT-008：一次 provider request 的原子档案 |
| Authority Root | PROMPT-002：有权改变执行档案的消息来源 |
| AttachmentKind | HOST-008：Companion / SyncInspector / SyncCoder / Bookkeeper / StrengthReplica；Attached 所有权的附着种类 |

## B

| 术语 | 指向 |
|------|------|
| BlindPlan | GLORY-074 / TODO-015：OpeningPolicy；Manager 至 first accepted `todowrite`（T1）关闭 Opening |
| BlogFrame | COMPANION-005：Y 历史的唯一表示（Entry/Squash） |
| BlogSquash | COMPANION-006：恢复槽对前半 Frames 的永久重写 |
| BloggerDeltaProjection | CTX-013：XTrace 降级后的 TOML delta，≤200 KiB |
| Blogger | AGENT-008 / ENFORCER-010：内部工作记录 Agent，工具面仅 `chronicle` |
| `blog`（工具） | （历史）非法旧名，无 alias；现行 `chronicle`（ENFORCER-011） |
| Byname | EXEC-002 / EXEC-005：在场人名；fork/commission/horizon 可见，无 AgentId |

## C

| 术语 | 指向 |
|------|------|
| Canonical Projection | COMPANION-007：Semantic 投影是 canonical digest 的唯一来源 |
| Canonical Answer | SPHINX-001/004：Kernel 写出的终局答案；非 LLM 自由文 |
| Canonical Role | AGENT-001：不变角色，与 fast/deep 无关，不决定 Companion |
| Chronicle | COMPANION-003：WorkRecord 段；Y 已沉淀的工作叙事 |
| Circuit Breaker | FALLBACK-005：达到有限正整数自动恢复预算时熔断；默认预算 12 |
| Clean Gate | ORCH-002：工作区 dirty 拒绝用户消息 |
| Closing report | COMPANION-003 / ARCH-015：WorkRecord 末段 prose claim；非固定字段 schema |
| `commission` | EXEC-029 / AGENT-015：Orchestrator 委托独立 Manager 之路；旧名 `fork-manager` 非法 |
| commissioner_record | COMPANION-003：commissioner 历史以 canonical WorkRecord 段落渲染；旧名 `parent_work_record` 非法 |
| CommitmentContract | GLORY-074 / TODO-015：BlindPlan 关闭 Opening 的承诺合同（Manager = T1 `todowrite`） |
| Companion | COMPANION-001：每个 Work Session 恰好一个叶子 Y |
| CompanionSession | HOST-008：InternalLeaf + Attached(Companion)；Work+Root 恰好一个 |
| Completion | EXEC-004：single-assignment completion cell；join 消费为有界 WorkRecord 批次 |
| ChildRecoveryResult | EXEC-023：RecoveredActive\|Terminal\|Abandoned\|RecoveryIncomplete\|RecoveryBlocked |
| ConsumableReview | TODO-006：≡ `TodoReviewConcluded`；VerdictKnown ∧ record-ready ProcessReviewLWR；下一 checkpoint / suicide 才可消费 |
| ControlHoldout | STRENGTH-010：restart-stable deterministic K0 bucket；predictor 训练 label 只来自此与 Shadow |
| Continuation | PROMPT-003：无权改变执行档案 |
| ContextReanchored | HOST-006：观察到 Host compaction 后退役 epoch 并归零 PrefixCoverage（保留 RecordCoverage/Frames） |
| CoverableRecordPrefix | COMPANION-003：Opening + 可证明覆盖完整 turn 的 Y frame prefix，probe 唯一合法输入 |
| CoverableTurnCutoff | CTX-011：已完整消化的最后一个 semantic turn 边界 |
| CoveredPrefixDigest | COMPANION-011：cutoff 处 provider-visible 前缀的 digest |
| Completion blob v2 | EXEC-021：schemaVersion=2，finality completed\|failed；LegacyFalseAbort 永不 RunCompletion |
| `chronicle` | ENFORCER-011 / AGENT-006：Blogger 工具；旧名 `blog` 非法 |

## D

| 术语 | 指向 |
|------|------|
| Dedicated process reviewer | TODO-008 / TODO-010：每 Manager Life 一个 process-review session；Finality ordinary enlist/graduate 与 process duty 拆开，duty 至 `LifeCompleted` |
| Distiller | AGENT-001 / AGENT-008：内部输出蒸馏角色；无工具；旧 Role `Executor` 非法，无 alias |

## E

| 术语 | 指向 |
|------|------|
| EffectiveAgent | FALLBACK-002：由 Fallback cursor 决定的当前 Agent |
| EpistemicState | SPHINX-004：`S=(R,B,E,D,A,C)`；handle 绑定的认识状态充分统计量 |
| Epoch | COMPANION-009：PrefixEpoch 内 append-only |
| `edit-qa` | （历史）非法旧名，无 alias；现行 `js-bookkeeper`（AGENT-006） |
| `establish-behavior` | AGENT-006 / EXEC-031：DevOps→Coder SyncDelegate；旧 TDD red 面非法 |
| ExecutionBinding | AGENT-029：物理模型 / tier / config；可随 Peer Fallback / Strength 变；≠ Persona |
| Executor（Role） | （历史）非法旧 Role，无 alias；现行 Distiller（AGENT-002/004） |
| `executor`（工具） | （历史）非法旧工具名，无 alias；现行 `run`（AGENT-006）；不得与 Distiller 角色混淆 |
| external_directory | AGENT-019：Host 路径边界元权限；managed agent 固定 allow，非角色工具 |

## F

| 术语 | 指向 |
|------|------|
| FallbackController | FALLBACK-003：统一 cursor advance 入口 |
| FallbackCursor | FALLBACK-002：modulo-4，Offset ∈ {0,1,2,3} |
| FallbackExhausted | FALLBACK-005：达到当前 AutoRecoveryBudget 后的终局 |
| FamilyRecoveryPermit | EXEC-023：恢复闭合后签发；Join / JoinWithPermit 唯一凭据 |
| Finality 三种经验 | GLORY-076：not accepted / accepted but not at rest / at rest；删除 status enum 投影 |
| Fire-and-forget | PROMPT-007：调用方不等待 PhysicalAccepted |
| `fork-manager` | （历史）非法旧名，无 alias；现行 `commission`（EXEC-029） |
| `fork-pty` | （历史）非法旧名，无 alias；现行 open/send/read/signal-terminal（EXEC-003） |
| FrameEpochId | COMPANION-006：只在 squash 提交时变化 |
| FrozenRecordPrefix | COMPANION-009：Opening + 可覆盖 Y frame prefix 的 epoch 冻结快照 |

## G

| 术语 | 指向 |
|------|------|
| Gates A–D | ARCH-016：Tool Referential Integrity / Provider Leak / Language Parity / Prompt Stability |

## H

| 术语 | 指向 |
|------|------|
| HandleId | EXEC-009：持久化，retired tombstone |
| HandleFalseCompletionRejected | EXEC-022：假 abort blob 驳回；CompletedAwaitingJoin→Active |
| `horizon` | EXEC-005：在场名册朝向（Byname / TerminalName）；旧名 `list` 非法 |
| HostSignal | HOST-003：typed SessionIdle/ProviderRetry/SessionDeleted |
| HumanRoot | PROMPT-004：真实用户新任务的 Authority Root |

## I

| 术语 | 指向 |
|------|------|
| IngestCursor | CTX-011：Y 实际已消化到哪个 part（RecordCoverage.IngestedThrough） |
| Inquiry | AGENT-001 / AGENT-025 / AGENT-030：`inspect` + Sphinx MCP 认识角色；旧 Role `Meditator` 非法，无 alias |
| `inspect` | AGENT-006 / EXEC-031：同步委派 Inspector → bounded WorkRecord；旧工具名 `inspector` 非法 |
| InsertStrengthFrames | STRENGTH-009/016 / PROJ-005：Candidate / Promoted / Replica-local frame insertion intent |
| Integration Gate | ORCH-005：短 CAS，只保护 ref mutation |
| Inspector | AGENT-006：证据获取角色（`query-shell` 等）；非「执行」角色 |
| `inspector`（工具） | （历史）非法旧同步委派工具名，无 alias；现行 `inspect` |
| isValidTerminal | CTX-004：非空且非 XML-only，唯一内容级校验 |
| includeOpening | COMPANION-003 / EXEC-006：LWR 是否渲染 Opening；父→子 true，子→父 false |
| Instruction comment header | ARCH-010：Synthetic TOML payload 最前方连续出现的 comments |

## J

| 术语 | 指向 |
|------|------|
| JoinGuard | EXEC-016：outstanding 后台资源时 terminal 先 join |
| JoinableCompletion | EXEC-021：fromDecoded / tryFromProvenTerminal 唯一构造；禁止 kind+body 弱证明 |
| JoinWaitOutcome | EXEC-017：`ResultsAvailable` \| `Interrupted of JoinInterruptReason`；user wake 只打断 wait，不授予 authority |
| JoinInterruptReason | EXEC-017：`OperatorAbort` \| `UserMessageArrived` \| `DeadlineExpired`；wire `operator_abort` / `user_message`；wake ≠ Prompt authority |
| `js-bookkeeper` | AGENT-006 / AGENT-008：Bookkeeper 工具；旧名 `edit-qa` 非法 |
| `judge` | REVIEW-001：Reviewer 工具；旧工具名 `verdict` 非法；参数字段 `verdict` 保留 |

## L

| 术语 | 指向 |
|------|------|
| Large Gate | EXEC-013 的大输出并发门 |
| LegacyFalseAbort | EXEC-021 / EXEC-022：历史 status/finality=aborted blob；永不 agent RunCompletion |
| LegacyTodoSeedAdopted | TODO-011：仅升级瞬间 legacy open Life 的一次 seed；正常新 Life 不从 Host TodoTable adopt |
| `list` | （历史）非法旧名，无 alias；现行 `horizon`（EXEC-005） |
| MaxJoinBatch | EXEC-018 定义的单次 join 批次上限 |
| LifecycleWorkRecord | COMPANION-003 / COMPANION-015：跨 Session 工作记录；段 = Opening / Chronicle / Recent work / Closing report；raw tool 禁止；`includeOpening` 父→子 true、子→父 false |
| Logical Run | PROMPT-002：一个 Authority Root 引发的完整对话序列 |
| LoopDetector | LOOP-005：滑动 4-gram + 慢指数核 + 代码先验，O(1)/字符 |
| LoopKillArmed | LOOP-006：进程内局部强杀标记，崩溃丢失 |
| LoopSensor | LOOP-002：边沿传感器，只读文本增量，不进业务层 |
| LOOP_HHI | LOOP-004 定义的 HHI 检测阈值 |
| LOOP_EFFECTIVE_COUNT | LOOP-004 定义的等效 4-gram 数阈值 |

## M

| 术语 | 指向 |
|------|------|
| ManagedSessionKind | （历史）旧 HOST-008 二元模型；现由 SessionExecutionClass × SessionOwnership 取代 |
| Manager Guard | GLORY-070：已删除的旧 review 门禁；仅历史行解析保留（PROMPT-003） |
| Managed Agent | AGENT-002 定义的受管 Agent 身份 |
| Magic Todo | TODO-001..015（`what/todo.md` 唯一语义 owner）：Manager `todowrite` 生命周期 checkpoint |
| MagicTodoManagerGuideline | TODO-013：Manager-only guideline fragment；不得并入全局 HOST-013 pair 正文 |
| MagicTodoProjection | TODO-007：Journal fold 的 canonical todo 真相；Host TodoTable 仅为 sink |
| manual compaction | HOST-006：官方支持的用户动作，效果 best effort |
| Meditator | （历史）非法旧 Role，无 alias；现行 Inquiry（AGENT-002/004/025） |

## N

| 术语 | 指向 |
|------|------|
| Native system instruction channel | ARCH-010：system prompt / developer prompt，本记法不约束 |
| N_eff | LOOP-003：inverse Simpson 等效 4-gram 数（物理量；阈值在此空间取中点） |

## O

| 术语 | 指向 |
|------|------|
| Office Library | PROMPT-016：角色继承技术书集合；≠ Common Law，不定义 authority |
| OpeningMaterial | COMPANION-014：preserved XTrace `[work start, OpeningBoundary)`；禁止重建；旧 `OpeningPromptRaw` 拼接已删 |
| OpeningPolicy | GLORY-074：`Immediate` \| `BlindPlan of CommitmentContract` |
| OpeningPromptRaw | （历史）已删除拼接重建；现由 OpeningMaterial（COMPANION-014）取代 |
| `original_user_requirement` | （历史）非法旧名，无 alias；现行 `root_requirement`（ARCH-015） |
| OwnerReuseScopeId | HOST-008 / EXEC-026：dedicated SyncDelegate 绑定键的 scope 半边；键为 `(OwnerReuseScopeId, role)` |
| `open-terminal` / `send-terminal` / `read-terminal` / `signal-terminal` | EXEC-003：四终端动词、四 contract；旧 `fork-pty` 非法 |

## P

| 术语 | 指向 |
|------|------|
| PeerAgent | AGENT-003：同角色相反 tier |
| Persona / SessionPersona | AGENT-028 / PROMPT-014 / FALLBACK-014：session 创建一次绑定，不可变自我模型 |
| Persona Registry | AGENT-028：Role × initial tier → SessionPersona |
| PrefixProbe | CTX-010：attempt-local 候选前缀，失败不成为事实 |
| PrefixRebaseCommitted | CTX-012：probe 提升的唯一持久事实；TODO-009：`EvidenceKind=TodoCheckpoint` 的 lag-1 rebase commit |
| ProcessReviewLWR | TODO-008：process / Finality 复用的 request-range bounded canonical LWR（`includeOpening=false`） |
| Prompt Composition Protocol | PROMPT-015：World / Role / Library / Runtime / Mission 分层；层可告知，不得冒充 |
| PromptDispatcher | PROMPT-005：四阶段（Claimed/Submitted/PhysicalAccepted/Abandoned） |
| Provider Horizon | ARCH-014：provider 可见面决策滤镜；无状态机 / UUID / 机器 DTO |
| Provider leak | EXEC-030 / ARCH-016 Gate B：SessionId/AgentId/PtyId/worktree/fallback offset 等不得穿 horizon |
| ProviderLanguage | PROMPT-017：`English` \| `SimplifiedChinese`；见 SessionProviderLanguage |
| ProviderRequestKind | PROMPT-008：WorkMain / BloggerMain / BloggerSquash / InteractionRepair / StrengthReplica |
| Provider-visible projection | COMPANION-012：正进入模型的字段排除非模型 metadata |
| ParentJoinCorrectionRequested | EXEC-022：已退休假 abort 的确定性 replacement 后通知父侧作废 |
| `parent_work_record` | （历史）非法旧名，无 alias；现行 commissioner WorkRecord 渲染（COMPANION-003） |
| PulseAgentHandle | EXEC-024：agent mailbox 仅唤醒信号；结果只读 Journal |
| PTY | EXEC-015：仅 DevOps 可操作，onExit-only completion |
| PtyAborted | EXEC-015 / EXEC-020：PTY 物理中断；agent 路径禁止 aborted |
| PublishPtyCompletion | EXEC-024：PTY mailbox 物理结果通道 |

## R

| 术语 | 指向 |
|------|------|
| RawGap | COMPANION-003：Y 尚未覆盖的 X suffix，经 LWR 投影补洞；剔除 raw tool |
| Recent work | COMPANION-003 / COMPANION-015：Y 未覆盖的 X-derived suffix；表示边界，非 receiver-relative 新近 |
| RecordCoverage | COMPANION-003：IngestedThrough，决定 LWR gap 起点 |
| ReconciledTurn | HOST-004：SDK API 读取的完整 typed turn；其 `Outcome` 仅为可 publish 的 `TurnOutcome`，不含 `TurnUnknown` |
| `repair-behavior` | AGENT-006 / EXEC-031：DevOps→Coder SyncDelegate；旧 TDD green 面非法 |
| `return`（工具） | （历史）已删除，无 alias；SyncDelegate ordinary completion → bounded WorkRecord（EXEC-031） |
| Review Attempt Identity | REVIEW-004：Barrier + Tree + Session + Run + ToolCall |
| Review Witness | REVIEW-006：自包含证据 |
| Reviewer Guard | REVIEW-003：terminal 无 `judge` 时自动 nudge |
| ReuseScope | HOST-008 / EXEC-026：语义上仍允许复用同一上下文的最大生命周期；非 Session dispose 瞬时 |
| Role / Persona / ExecutionBinding | AGENT-029：职责 / 自我模型 / 物理绑定三分离；Fallback 只改 Binding |
| `root_requirement` | ARCH-015：Reviewer authoritative scope 结构化 operand；旧名 `original_user_requirement` 非法 |
| Runtime Synthetic TOML | ARCH-010：运行时构造、包装或注入并进入 LLM 上下文的合成文本 |
| `run` | AGENT-006：DevOps 有界执行；旧工具名 `executor` 非法 |

## S

| 术语 | 指向 |
|------|------|
| Seal Barrier | COMPANION-009：provider-visible bytes 一旦发出永久 sealed |
| SealRoot | COMPANION-013：probe 生成后由 committed epoch 原样继承 |
| SelectedAgent | PROMPT-002：由 Authority Root 冻结、Fallback 不得改写的 Agent |
| Semble MCP | AGENT-027：内部 stdio 语义搜索；非 Host MCP；Strength 当前不消费 |
| SessionExecutionClass | HOST-008：Work / InternalLeaf |
| SessionOwnership | HOST-008：Root / Attached(ownerSessionId, AttachmentKind) |
| SessionProviderLanguage | HOST-026 / PROMPT-017 / FALLBACK-014：session 创建绑定不可变；child 继承 owner/commissioner |
| Sphinx | SPHINX-001：生成式认识状态求解器；Kernel↔LLM co-yield |
| Sphinx handle | SPHINX-002：不透明 inquiry 钥匙；进程内绑定 EpistemicState；非 EXEC HandleId |
| Sphinx MCP | AGENT-030 / SPHINX-003：Host MCP `sphinx`；schema 键 `sphinx_*`；域能力 `ToolPermission.Sphinx`；仅 Inquiry |
| stealth-browser MCP | AGENT-026：Browser 专用 Host MCP；schema 键 `stealth-browser-mcp_*`；域能力 `ToolPermission.Network` |
| Steward | （未来预留）V1 不创建、不进现行能力（AGENT-028） |
| StrengthCandidate | STRENGTH-006：已 durable 准备且仅绑定一个 TargetProviderRun、尚未成为语义历史的只读 frame bundle |
| StrengthCandidatePrepared | STRENGTH-006/017：Candidate 已 commit 到 EventStore 并绑定 TargetProviderRun 的 durable 事实 |
| StrengthCandidatePromoted | STRENGTH-007：TargetProviderRun 已产生消费证据后的不可删除语义历史 |
| StrengthDecisionId | STRENGTH-005/006：一次 decision 的稳定身份；frame wire identity 与 durable Candidate 的幂等键 |
| StrengthFramesTraced | STRENGTH-008：Promoted frame 已进入明确 XTrace cursor range |
| StrengthOpportunity | STRENGTH-002/010：冻结的 eligible evidence；不含 Stage/Phase |
| StrengthReplica | STRENGTH-004/014：`InternalLeaf × Attached(StrengthReplica)` 的 same-role fast decision-local leaf |
| StrengthBudget | STRENGTH-003：K0/K1/K2；单位为 Replica provider request |
| SyncCoder | HOST-008：AttachmentKind；EXEC-026 / EXEC-028：dedicated Coder SyncDelegate |
| SyncDelegate | EXEC-026 / EXEC-028 / EXEC-031：同步委派；ordinary completion → bounded WorkRecord；无 `return` |
| SyncInspector | HOST-008：AttachmentKind；EXEC-026 / EXEC-028：dedicated Inspector SyncDelegate（工具面 `inspect`） |
| Synthetic identity | COMPANION-013：由 SealRoot / frameEpoch / ordinal 等领域事实确定性派生 |
| System prompt 稳定性 | GLORY-075 / PROMPT-014 / FALLBACK-014：同 Life 内 office system prompt byte-identical；T1/Fallback/review/reanchor 不改写 |

## T

| 术语 | 指向 |
|------|------|
| TargetProviderRun | STRENGTH-006/007：Candidate 唯一绑定、唯一可 Promotion 的 ProviderRunIdentity |
| TipDeliveryFrontier | ENFORCER-071：哪些 TipOccurrence 已交付该 Main；durable、monotonic；reanchor 不重置 |
| TipSemanticCoverage | ENFORCER-071：哪些 TipName 全文仍可从当前 provider horizon 恢复；reanchor 可丢；⊥ TipDeliveryFrontier |
| TodoCheckpoint | TODO-001 / TODO-004：一次成功受理的 `TodoWriteAccepted`；持续节拍唯一来源 |
| TodoReviewConcluded | TODO-006：ConsumableReview 的 durable fact；禁止表达「仅有 verdict、尚无 report」 |
| TodoStatus | TODO-003：`pending \| in_progress \| reviewing \| completed \| cancelled`；completed 仅自 reviewing |
| TodoWriteAccepted | TODO-004 / TODO-006：checkpoint + 派生 Rk obligation 的 SSOT fact |
| T1 commitment | TODO-015 / GLORY-074：本 Life 第一次 accepted `todowrite`；关闭 BlindPlan Opening |
| ToolResultBound | ARCH-012：自定义 tool result 抢先留尾截断；≤2000 行 / ≤51200 字节 |
| TurnUnknown | HOST-004：reconciliation 私有 `SnapshotObservation`（finish=None），不是 `TurnOutcome` case |

## U

| 术语 | 指向 |
|------|------|
| UseStrengthMirror | STRENGTH-009/016 / PROJ-005：StrengthReplica-only base timeline selection |

## V

| 术语 | 指向 |
|------|------|
| VerdictKnown | TODO-006 / REVIEW-*：Reviewer 域 durable process verdict；单独不构成 ConsumableReview |
| `verdict`（工具） | （历史）非法旧工具名，无 alias；现行 `judge`（REVIEW-001）；参数字段 `verdict` 与 VerdictKnown 仍合法 |

## W

| 术语 | 指向 |
|------|------|
| Witness | REVIEW-006：双 PERFECT 的自包含证据 |
| WorkRecord | COMPANION-003 / COMPANION-015：跨 Session 唯一工作记录协议；Opening + Chronicle + Recent work + Closing report |
| WorkRecordStart | TODO-001 / CTX-016 / GLORY-074：Opening exclusive end；BlindPlan 下含 T1 commitment call/result；纯推导 floor，非 Stage |
| WorkSession | HOST-008：SessionExecutionClass.Work（Root 或 Attached Sync*）；Root 恰好一个 Companion |
| Worktree | ORCH-003：一个 Job 一个 worktree |

## X

| 术语 | 指向 |
|------|------|
| XTrace | COMPANION-003：X 唯一原始语义轨迹，含 host-visible reasoning |
| XTraceCursor | HOST-005：lifecycle 内严格单调、独立于 Host 数组下标的语义游标 |

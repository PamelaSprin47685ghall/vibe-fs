# STATUS/shock-anneal — 休克-退火迁移总账

分支 `refactor/ssot-shock-anneal`。封炉基线 `STATUS/evidence/pre-shock/`。

## 阶段

| 期 | 名称 | 机器反馈 | 当前 |
|----|------|---------|------|
| 0 | 封炉：冻结 SSOT、基线、迁移地图、验证层工装 | 静态检查 + 最后一次完整编译测试 | 完成 |
| 1 | 休克一：领域内核与持久事实（包 0） | 关闭 | 完成（包 0a–0e） |
| 2 | 休克二：生产代码全部调用链（包 A–H） | 关闭 | 进行中（包 A 完成） |
| 3 | 清场：删除旧语义与临时标记 | 静态检查 | 未开始 |
| 4 | 休克三：按条款写 `tests-mjs`，删除 `tests-next`（包 T） | 关闭 | 未开始 |
| 5 | 退火一：恢复生产编译 | dotnet build → npm run build | 未开始 |
| 6 | 退火二：恢复 mjs 单元套件 | test:next | 未开始 |
| 6.5 | 剧本森林重建（包 K） | 载入期校验 + 森林自检 | 未开始 |
| 7 | 退火三：恢复 Host / E2E / Release | gate-testkit → canary → P0×3 → release | 未开始 |

包 K 排在退火二之后、退火三之前：剧本的 lane 划分与 step 序列反映迁移后的生产行为，先重写会锁定旧语义；而它必须早于任何 canary 运行，因为旧剧本无法匹配新语义的请求。

休克期只允许第 0 层反馈：`ssot-lint.mjs`、`shock-audit.mjs`、`architecture-gate.mjs`、`git diff --check`、`git status --short`、`rg`。

### 测试语言迁移（VERIFY-008）

休克三不再是「重写 F# 测试」，而是换语言重建：生产保持 `.fs`，第 1–3 层测试全部改为 `.mjs`，直接消费 `build/next` 发布产物。

理由不是省编译时间，而是让语言边界物理性地阻止测试触碰实现内部——能从 mjs 干净进入的恰好是 SSOT 认定为事实的契约面，碰不到的恰好是实现自由部分。

因为休克三本来就要徒手重建全部测试，换语言的边际成本≈0，但删掉了一整层：

```text
迁移前：F# 测试 → Fable 编译 → 自制 Assert shim → 自制导出发现 runner → node
迁移后：mjs → node:test
```

连带删除：`tests-next/Wanxiangshu.Next.Tests.fsproj`、`tests-next/Assert.fs`（手写 xunit 替身）、`npm run test:compile`、`build/tests-next`、runner 的 Fable 导出发现逻辑。

退火期形态因此改变：

| 原计划 | 现计划 |
|--------|--------|
| 退火一 4 波（Domain/Journal、callers、Host/Fable、tests） | 退火一 2 波（Domain+Journal、callers+Host/Fable）。测试不参与编译 |
| 退火二先 `test:compile` 再 `test:next` | 退火二直接 `test:next` |

语义升级：生产入口 `build/next/OpenCode/Plugin.js` 与测试入口同为 `build/next`，测的是同一份字节。当前 F# 测试走 `--precompiledLib` 链接，测的是另一次编译结果。

新增的两条 mjs 专属风险与对策（`.mjs` 无编译期重命名保护的对价）：

| 风险 | 对策 |
|------|------|
| 字段改名后断言静默读到 `undefined` | 禁止只断言真值；必须比对完整结构或完整序列化文本 |
| Fable 命名约定与容器形状泄漏进测试 | 全部隔离在 `tests-mjs/domain.mjs` 单一 facade，等价于生产侧 Adapter/Codec 门禁 |
| `DateTimeOffset` 用裸 `new Date()` 导致时间比较反向错误且不报错（已实证） | facade 提供构造器；facade 自身有元测试断言值携带 offset |
| `build/next` 陈旧时给出假绿灯 | runner 比对产物与 `next/**/*.fs` 时间戳，陈旧则拒绝运行 |

### Architecture Gates 迁出测试套件

`tests-next/Gates/ArchitectureGates.fs`、`ArchitectureGates17.fs`、`ArchitectureGateSupport.fs` 做的是文件系统加正则判断，与 `ssot-lint.mjs` / `shock-audit.mjs` 同类。迁入 `scripts/architecture-gate.mjs`，成为 VERIFY-001 第 0 层。

净收益：门禁不再需要先编译才能检查源码；门禁红灯与行为红灯分离，退火期可以分层打开反馈。

## 工作包状态

状态取值：`UNTOUCHED` `DOMAIN_MIGRATED` `CALLERS_MIGRATED` `LEGACY_REMOVED` `TESTS_REWRITTEN` `COMPILES` `UNIT_GREEN` `E2E_GREEN`。禁止「差不多完成」。

### 包 0：Identity 与基础类型

| 项 | 值 |
|----|----|
| 条款 | PROMPT-001 PROMPT-002 PROMPT-005 PROMPT-008 FALLBACK-002 FALLBACK-004 FALLBACK-005 REVIEW-003 REVIEW-004 REVIEW-006 REVIEW-008 EXEC-009 ORCH-006 ORCH-008 HOST-010 HOST-011 ARCH-006 |
| 目标模块 | `Kernel/Identity.fs` `Kernel/Outcome.fs` `Domain/AgentPairCursor.fs` `Domain/PromptAuthority.fs` `Domain/PromptAuthorityRun.fs` `Domain/ReviewWitness.fs` |
| 旧入口 | 裸 `string` 承载 LogicalRunId / ProviderRunId / ToolCallId / GitTreeHash / ManagerId；单一 `MessageId` 同时表示物理消息与 Authority Root |
| 新入口 | 26 个 typed identity + 2 个复合身份（`FallbackAttemptIdentity` `ReviewAttemptIdentity`） |
| 生产 | DOMAIN_MIGRATED（Kernel + Domain 完成；Journal 与调用方属包 1b/1c 与 A–H） |
| 测试 | UNTOUCHED |

先做，破坏面最大。

#### 用类型表达的条款

不是「加一层包装」，而是把之前只能靠注释维持的条款变成编译期事实：

| 条款 | 之前 | 之后 |
|------|------|------|
| PROMPT-001 `PhysicalUserMessage ≠ AuthorityTurn` | 两者都是 `MessageId` | 两个类型；`promoteToAuthorityRoot` 是唯一单向通道，且无逆函数 |
| PROMPT-005 `accepted-*` 不是 Authority | 都是 `MessageId`，靠前缀字符串判别 | `TransportReceipt` 独立类型，不存在到任何消息身份的函数 |
| PROMPT-002 Authority Root 禁止覆盖 model | 靠纪律 | `AuthorityExecutionProfile` 无 model 字段，不可表达 |
| FALLBACK-004 成功不重置 Offset | `LastProviderAttempt: int64 option` 混合语义 | `Offset` 与 `ConsecutiveFailureCount` 两个字段；`recordSuccess` 只动后者 |
| FALLBACK-005 循环无界预算有界 | 无 count 概念 | `RecoveryVerdict = MayContinue \| Exhausted`；判定在推进之后 |
| REVIEW-003 需因果证明 | `canConfirm` 接受 same-root 猜测 | `confirm` 必须传入 `challengeConsumed`，无法自行伪造 |
| REVIEW-004 两次 PERFECT 必须 run 与 call 皆不同 | `ProviderRunId`/`ToolCallId` 同为 string | 两个类型，`isDistinctAttempt` 的检查有意义 |
| REVIEW-008 witness 永久，有效性是派生谓词 | `invalidateByTreeChange` 返回 `NoReview`（销毁历史） | `isValidForTree` 只回答问题，不改状态 |
| ORCH-006 恢复按 identity 不按 path | 只有 `WorktreePath` | `WorktreeIdentity` 与 `WorktreePath` 分离 |
| ORCH-007 head 比较不是同义反复 | `TargetRef` 与 commit 同类型 | `TargetRef` 与 `CommitHash` 分离 |
| EXEC-009 retired 不回退成 Agent 名 | handle 是 string | `HandleId` 三 case 联合；只有 `describe`，无 parse |
| PERSIST-002 append 只有两种结果 | — | `CommitResult` 恰好两 case（保持） |

#### 命名统一：ProviderAttemptIdentity = ProviderRunIdentity

SSOT 对同一概念用了两个名字：PROMPT-008 与 FALLBACK-007 写 `ProviderAttemptIdentity`，HOST-010 与 REVIEW-004 写 `ProviderRunIdentity`。

依据 `evidence/host-transform-run-binding.md`：一条 Host assistant message = 一次 provider request = 一次 attempt。Host 每个 provider step 新建一条 assistant message（`prompt.ts:1186`），并把同一 id 交给该 run 内所有 tool call（`ToolContext.messageID`）。因此实现只有 `ProviderRunIdentity` 一个类型。

这不是降低条款，是拒绝双模型：两个类型会让「同一 attempt 的两个身份是否相等」成为一个可提问但无意义的问题。

登记为待办的规范文字统一，不属 SSOT 例外协议（无语义矛盾，仅命名冗余）。退火三前把两处 SSOT 文字统一为 `ProviderRunIdentity`。

#### 顺带修正

`Kernel/Roles.fs` 前移到 `Kernel/Flow.fs` 之前。`Domain/PromptAuthority.fs` 需要 `Role` 与 `AgentTier`，F# 按声明顺序编译，原顺序把 Roles 排在 Outcome 之后属于偶然可用。

`Outcome.SessionError.FallbackExhausted` 改名 `AutoRecoveryExhausted`：同名的 journal 事实（FALLBACK-005）是另一回事，一个名字两个概念正是双模型的起点。

### 包 0b：Journal 事实集

| 项 | 值 |
|----|----|
| 条款 | PROMPT-002 PROMPT-005 FALLBACK-007 REVIEW-003 REVIEW-006 REVIEW-010 EXEC-009 ORCH-005 ORCH-006 ORCH-007 PERSIST-009 |
| 目标模块 | `Kernel/Fact.fs` |
| 生产 | DOMAIN_MIGRATED（事实集完成；codec / fold / 写入方属 0c 与 A–H） |
| 测试 | UNTOUCHED |

旧 union 33 case → 新 union 32 case。数量接近，但语义分布完全不同。

#### 事实级替换

| 旧 case | 新事实 | 为什么不是改名 |
|---------|--------|--------------|
| `PluginPromptAccepted` | `PluginPromptSubmitted` + `PluginPromptPhysicalAccepted` | PROMPT-005 的核心区分。单一 accepted 事实无法表达「Host 收下了但只给了 receipt」与「真实物理消息已存在」，而 Authority 只在后者生效 |
| `HumanPromptAccepted` | `AuthorityRootAccepted` | 前者按来源命名，后者按效力命名。PROMPT-004 的 AgentOwnerRoot 同样是 Authority Root，旧名字容纳不下 |
| `GuardPromptAccepted` | Dispatcher 四事实 | Guard 不是特殊的发送通道，它是一种 Continuation（PROMPT-003）。独立事实等于承认存在第二个 sender |
| `InteractionRepairClaimed` | `PluginPromptClaimed` + Origin | 同上 |
| `AgentLinked` / `AgentUnlinked` | `HandleLinked` / `HandleCompleted` / `HandleRetired` | 两事实只有两态，无法表达 completed-awaiting-join。EXEC-005 要求 `list` 显示该状态，旧模型下它与 running 不可区分 |
| `AgentForked` | `HandleLinked` | 「fork 的」与「link 的」区别是 join mailbox 归属，属 projection 判断，不是两个事实 |
| `ReviewConfirmedIdle` | `ConfirmedReviewWitness` | 前者记录「reviewer 空闲了」——这是信号不是事实（ARCH-002）。后者记录证据 |
| `OrchestratorCandidateRegistered` | `CandidateReady` | ORCH-006 禁止「注册后等 review 或走 publish」这种恢复动作不确定的分支 |
| `OrchestratorRebased` | `RebasedCandidateReady` | 同上，且必须携带 post-rebase barrier |
| `OrchestratorRejected` | `JobFailed` | Rejected 描述 review 结果，Job 失败是另一层 |
| `OrchestratorPre`/`PostRebaseReviewConfirmed` | `CandidateReady.PreRebaseReviewBarrierId` / `RebasedCandidateReady.PostRebaseReviewBarrierId` | 两个独立的「已确认」事实是 stage 标记。barrier id 内联进候选事实后，恢复动作由候选事实本身决定 |

#### 新增事实

| 事实 | 条款 | 空缺后果 |
|------|------|---------|
| `FallbackExhausted` | FALLBACK-005 | 预算耗尽无终局记录，重启后无法区分「还能重试」与「已放弃」 |
| `PerfectChallengeIssued` | REVIEW-003 | 第一次 PERFECT 的 challenge digest 无处存放，第二次无从校验 |
| `ProviderInputSealed` | REVIEW-010 | 因果证明的载体。缺它则只能退回 same-root 猜测 |
| `ConfirmedReviewWitness` | REVIEW-006 | 自包含 witness |
| `ConflictDetected` | ORCH-007 | 恢复时无法区分「Manager 正在解冲突」与「尚未产出 candidate」 |
| `JobAbandoned` | ORCH-006 | 主动放弃与失败混为一谈 |

#### 字段级修正

`FallbackCursorAdvanced` 补 `PreviousOffset` / `NextOffset` / `ConsecutiveFailureCount`，使 FALLBACK-007 的 fold 校验可执行——旧形状只有 `ProviderAttempt: string`，fold 无从验证模四后继关系。

`AuthorityRootAccepted` 确认无 model 字段。VERIFY-006 把「Authority journal 仍保存 model ID」列为 No-Go，缺字段即是执行。

`ManagerJobCreated` 补 `ManagerAgent`（ORCH-003 防降级）、`WorktreeIdentity`（ORCH-006 恢复按身份）、`TargetBranchFrozen`（ORCH-008）。

`PromptAbandonReason` 与 `HandleCompletionKind` 是判别联合而非字符串：前者决定运维是否需要排查双效果，后者是 EXEC-004 的三方竞争结果。

#### shock-audit 单一写入口检测的缺陷（本包发现）

新事实加入后，检测器对 `FallbackExhausted` 报 `ok (1)`，指向 `next/Domain/AgentPairCursor.fs`。那里只有一句文档注释提到该名字。

根因：检测器统计的是符号提及，不是构造器应用。因此

```text
0 个写入口 → 匹配到注释 → 报 ok (1)
1 个写入口 → 报 ok (1)
```

两种状态输出相同。一个对 0 和 1 都回答「正常」的门禁，无法察觉它唯一要保护的那次跃变。

修正：改为匹配 `AgentFact.<Name>` 构造器应用，并把 0 与 1 分开报告。`--gate` 下「已声明但无写入口」判失败——迁移声明了类型却没接线，与双写同样是缺陷。

同时把八个单一写入口事实全部纳入检测（原先只有两个）。当前实测：

```text
FallbackCursorAdvanced        3 writers（FALLBACK-003 违规，包 C 修）
PluginPromptClaimed           ok (1)
其余六个                       declared, no writer yet（包 A/C/D/F/G 接线）
```

### 包 0c：Journal 投影与 Fold

| 项 | 值 |
|----|----|
| 条款 | PERSIST-001 PERSIST-002 PERSIST-004 PERSIST-008 FALLBACK-001 FALLBACK-003 FALLBACK-007 REVIEW-003 REVIEW-004 REVIEW-007 REVIEW-008 REVIEW-010 EXEC-004 EXEC-005 EXEC-009 ORCH-003 ORCH-006 ORCH-007 ORCH-008 HOST-010 |
| 目标模块 | `Journal/` 全部 17 文件 |
| 生产 | DOMAIN_MIGRATED（投影与 fold 完成；OpenCode/Session/Review/Orchestrator 侧调用方属包 A–H） |
| 测试 | UNTOUCHED |

#### 拒绝必须是值，不是「原样返回」

旧 fold 在每条拒绝路径上都返回未修改的投影。后果是四种情形无法区分：

```text
重复事实（幂等，正常）
不同 Logical Run（无关，正常）
模四后继关系错误（FALLBACK-007 违规，必须 fail closed）
第二次 PERFECT 无 seal 证明（REVIEW-003 违规，必须 fail closed）
```

后两种是「没有正确 writer 能写出这条线」，把它们当成 no-op 吸收，等于把 journal 重放进一个领域禁止的状态。

现在三个投影各自返回 `Result<_, Rejection>`：

```text
FallbackAdvanceRejection  = AlreadyObserved | AlreadyExhausted | DifferentRun | InvalidTransition
VerdictRejection          = DuplicateAttempt | SameProviderRun | ChallengeNotProven
HandleTransitionRejection = UnknownHandle | HandleIsRetired | AlreadyCompleted | NotCompleted
```

`Fold` 把每种拒绝分类为「继续」或 `FoldRejection`。`FoldRejection` 携带事实名与原因，`Fold.apply` 遇到即停止并上报（PERSIST-004）。

#### 删除两个文件

`Journal/AuthorityProjection.fs`：五个函数全是 `PromptAuthorityLedger.foldXxx` 的纯转发，每个还各自重新声明一遍匿名记录形状。它是一层只增加拼写错误机会的间接。

`Journal/ReviewConfirmation.fs`：REVIEW-003 禁止的三种弱代理的实现体本身——`AcceptedContinuationIds`、`AcceptedContinuationRoots`、`ConfirmationPhysicalMessageId`，加上 `samePhysicalRootReevaluationMatched`。因果证明改由 seal 承担后，它没有任何合法用途。

灭绝表随之下降：`samePhysicalRootReevaluation` 2→0，`RecentProviderRunIds` 5→0，`AcceptedContinuationIds`（scoped）1→0。

#### PERSIST-008：三处全量扫描

| 位置 | 旧行为 | 修正 |
|------|--------|------|
| `Fold.reviewOwner` | `Map.tryPick` 遍历所有 session 找 reviewer 的父级 | 删除。review 事实携带 `ReviewerSessionId`，ReviewGuard 按它键控——review 对话发生在 reviewer 的 session 里，状态就该在那里 |
| `PromptAuthorityLedger.acceptedContinuation` | `Map.tryPick` 遍历所有 session 找 message id | 要求 `SessionId` 参数。一个 message id 只属于一个 session，跨 session 命中本身就是 bug，扫描只是把它静默容忍了 |
| `AgentJournal.reviewRequirementScope` | 递归走父链，每步 `Map.tryPick` 全扫 | 删除。requirement 由 fold 在收到 HumanRoot 的 session 上创建，由 `ConfirmedReviewWitness` 在 Manager session 上清除，无需搜索发现归属 |

#### AgentJournal 不再自己去重

旧 append 边界重新实现了一遍去重键：

```fsharp
let fallbackIdentity logicalRunId authorityRootUserMessageId providerAttempt =
    sprintf "%s|%s|%s" logicalRunId authorityRootUserMessageId providerAttempt
```

journal 与 fold 因此各持一份「什么算同一次 attempt」的定义。FALLBACK-003 把这个判断给了 FallbackController，REVIEW-004 把 review 去重给了投影；append 边界的第二份去重是同一知识的第二处实现。

删除后重放重复事实依然安全：fold 返回未变的投影。

#### 其他修正

`Envelope.TurnId` → `ProviderRun`。HOST-010 已证明一条 assistant message = 一次 provider request = 一个 turn，所以独立的 turn 身份只能是 run id 的副本，或者与它不一致。

`FactCodec` 的 pre-0.5.0 标记从 7 个字段名扩到 24 项，纳入本次替换掉的全部旧 case 名。否则解码器只会报一个不透明的 union 错误，运维看到的是「第 3 行解析失败」而不是「这个 journal 早于 0.5.0」——精确诊断决定了是归档文件还是去调试 codec。

`AgentJournal.createFromBoot` 与 `SharedAgentJournal.acquire` 返回 `Result`。PERSIST-004 要求无法折叠的 journal 停止启动；「尽可能折叠一部分」会把 runtime 建立在没有任何 writer 产生过的前缀上。

`OrchestratorProjection` 的五个独立可选字段（`PreRebaseReviewCommit` / `RebasedCommit` / `ConflictFiles` / `PostRebaseReviewCommit` / `PublishClaimHead`）合为一个 `JobProgress` 判别联合。这不是状态机（ARCH-001）：每个 case 携带物理证据（commit、head 快照、review barrier、冲突文件），没有一个说「程序接下来去哪」。ORCH-007 的恢复动作由 case 匹配得出，而不是给五个字段排优先级。

`SessionAgentProjection.Linkage` 改名 `Handles`，类型从两张 map（linked / unlinked）变为一张 `Map<HandleId, HandleRecord>` 加三态生命周期。旧模型无法表达 completed-awaiting-join，而 EXEC-005 要求 `list` 显示该状态。

### 包 0d：AttemptExecutionProfile 与两种 Provider Projection

| 项 | 值 |
|----|----|
| 条款 | PROMPT-008 AGENT-001 AGENT-007 AGENT-010 COMPANION-002 COMPANION-012 ARCH-004 REVIEW-010 VERIFY-003 VERIFY-005 VERIFY-007 |
| 目标模块 | `Domain/ProviderProjection.fs`（新建）`Domain/PromptAuthority.fs` `OpenCode/Projection.fs` `scripts/architecture-gate.mjs` |
| 生产 | DOMAIN_MIGRATED（类型与唯一构造函数完成；贯通全链属包 B） |
| 测试 | UNTOUCHED |

#### 唯一构造函数

`buildAttemptExecutionProfile` 只接收两个不可推导的输入——Authority Root 固定的 authority profile，与选边的 fallback cursor——其余全部在函数内派生：

```text
EffectiveAgent    ← effectiveAgentFor authority cursor
SystemPromptId    ← systemPromptIdFor authority.CanonicalRole
ToolCapabilitySet ← Roles.permissions authority.CanonicalRole
```

调用方因此无法传入与 agent 名不一致的 CanonicalRole，也无法传入与 role 不一致的工具集。PROMPT-008 禁止的「从 mutable session cache、最后一条 user message、Role map 和 fallback projection 临时拼装」在签名层面不可表达。

`SystemPromptId` 只由 CanonicalRole 决定，tier 不参与：AGENT-010 要求 `permissions(fast-coder) = permissions(deep-coder)`，让 tier 参与会使这一等式失去结构保证。

同时把两个判断移入 profile：

```text
hasCompanion  COMPANION-002：eligibility 只读 CanonicalRole，
              从 profile 出发无法访问 agent 字符串或 session cache
allowsTool    AGENT-007 第二层：runtime gate 与 provider schema 读同一个集合
```

#### 新增门禁：single-constructor

F# record 构造是结构化的，没有类型名可搜。判据改为「同时赋值 `SystemPromptId =` 与 `ToolCapabilitySet =`」——这两个字段名在全仓不属于任何其它类型，因此同时出现即为在构造该 profile。

已用探针验证门禁真的会红：临时写入一个手工拼装 profile 的文件，`architecture-gate` 报

```text
single-constructor (1)
  next/Session/GateProbe.fs: assembles AttemptExecutionProfile field by field;
  PROMPT-008 requires construction through next/Domain/PromptAuthority.fs
```

删除探针后恢复绿。声明了但没验证过会红的门禁，与包 W 记录的空 heartbeat 是同一类缺陷。

#### 两种 Provider Projection 拆分

旧实现只有一个 `ProviderVisibleMessage`，同时承担字节相等与语义相等。代价可见：canary 匹配器为此在 `strict-mock-matches.js` 里另写了一份 `sealProviderVisible` 规范化函数——同一知识的第二处实现，且两者对「什么算可见」的定义并不一致。

现在是两个类型：

| | 含 ID | 判断标准 | 可比范围 | 用途 |
|---|---|---|---|---|
| `ProviderWireProjection` | 是 | 字节相等 | 同一 Session 时间线内 | 前缀缓存（ARCH-004）、Seal（REVIEW-010） |
| `ProviderSemanticProjection` | 否 | 语义相等 | 跨 Session、跨重启 | fixture 键（VERIFY-003）、Blogger delta（COMPANION-012） |

`toSemantic` 是唯一的降级函数。全仓不存在返回 `ProviderWireProjection` 的函数（除类型定义本身），所以 `Semantic → Wire` 在签名层面不存在——丢弃的 ID 无法恢复，这一点由「没有那个函数」表达，而不是由注释表达。

provider 与 model 保留在 Semantic 中：它们是配置而非身份，且 FALLBACK-002 的 A/B 切换会改变 model，看不见它的 fixture 无法区分两侧。

#### 手写规范化而非反射序列化

两个 `render*` 函数手工拼 JSON。序列化库的字段顺序与 optional 处理会随版本变化，而这两个输出都是持久判据：Wire digest 会成为 durable seal，Semantic 字符串会成为 fixture 键。依赖升级不得使任何一方失效。

#### OpenCode/Projection.fs 降为纯 adapter

它现在只做 Host raw object → Wire，不再定义自己的投影类型。语义投影只能经 `toSemantic` 得到，因此不存在第二条解码路径对「这条消息是什么意思」给出不同答案。

顺带修正两处：tool call 缺 `callID` 时整个 part 被丢弃，而不是补一个空 id——REVIEW-004 需要 call id，空字符串会让「没有身份」看起来像一个真实身份。`system` 保持独立列表而不折进 messages，因为 Host 就是这样发的（`experimental.chat.system.transform` 收 `system: string[]`），折进去会让 wire projection 与它要镜像的字节不一致。

### 包 0e：单一 Host 摘要适配器与读侧修正

| 项 | 值 |
|----|----|
| 条款 | FALLBACK-001 FALLBACK-002 FALLBACK-005 PROMPT-011 VERIFY-005 VERIFY-008 ARCH-003 COMPANION-011 REVIEW-010 |
| 目标模块 | `Host/HostDigest.fs`（新建）、`Journal/RuntimePath.fs`、`OpenCode/{GitTree,ExecutorSummarize,HostSessionNudge,RetrySignalHandler,PromptDispatcher,PromptMetadataCodec,PromptAuthority}.fs`、`Orchestrator.IntegrationGate.fs`、`Session/{CompanionDelta,DurableFallback}.fs`、`scripts/architecture-gate.mjs` |
| 生产 | DOMAIN_MIGRATED |
| 测试 | UNTOUCHED |

#### 六个 crypto 适配器合一

六个模块各自 `[<Import("createHash", "node:crypto")>]` 并写三行 sha256-hex 包装：prompt 身份、runtime 路径、Git tree、executor id、publish lock key、Companion 前缀 digest。同一知识六份，四个不同局部名（`sha256`、`sha256Hex`、`digest`、`createHashImport`）。

摘要出现在持久事实里（REVIEW-010 的 seal、COMPANION-011 的 CoveredPrefixDigest）与派生身份里。第二份实现若在编码上有差异，不是「与另一份不一致」，而是让已存储的证据失效。

现在 `node:crypto` 在全仓恰好一个导入点。纯领域不调用它，而是接收 `sha256: string -> string` 参数——这就是 `Domain/` 没有 `node:crypto` 且可以不带 Host 测试的原因（VERIFY-008）。

新目录 `next/Host/` 而非放进 `OpenCode/`：摘要不是 OpenCode 概念，`Journal/RuntimePath.fs` 与 `Orchestrator.IntegrationGate.fs` 都要用它，让它们依赖 `OpenCode` 会造成反向依赖。fsproj 中排在 `Kernel/Identity.fs` 之前。

`ExecutorSummarize.fs` 因此不再需要任何 Fable 符号，`open Fable.Core` 与 `open Fable.Core.JsInterop` 一并删除，并从 host-boundary allowlist 移除——allowlist 条目留着不删，就会在下一个人往该文件加动态访问时静默放行。

#### duplicate-algorithm 门禁漏检单处误置

旧判据是 `hits.length > 1`，因此「只定义一次但在错误文件里」静默通过。`sha256Hex` 正处于这个状态：`Domain/PromptAuthority.fs` 被声明为 owner，实际那里根本没有定义，唯一定义在 `OpenCode/PromptAuthority.fs`。门禁看到一处命中就放过了。

三处修正：

```text
单处误置也判失败（strays 非空即报）
定义正则接受修饰符——let private sha256Hex 仍是第二份实现，
    private 改变谁能调用，不改变知识是否存在两份
只匹配模块级定义（缩进 ≤ 4）——函数体内的 let peerAgent 是恰好同名的
    局部变量，把它算作违规会逼作者为讨好门禁而扭曲局部命名
```

修正后立刻暴露两处真实违规：`HostSessionNudge.effectiveAgent` 与三处 `sha256Hex`。

`HostSessionNudge.effectiveAgent` 改名 `agentForActiveCursor`：它只是取 cursor 再问 `AgentPairCursor`，按查询命名而非按算法命名，否则读者会以为存在第二个 side-selection 实现。

#### DurableFallback 读侧两处缺陷

其一，逐字段重建 cursor：

```fsharp
{ Offset = fb.Offset
  LastProviderAttempt = fb.LastProviderAttempt }
```

包 0a 给 cursor 加了 `ConsecutiveFailureCount` 后，这里会静默漏抄，每次读出的 count 都是 0——FALLBACK-005 的预算永远满格。改为直接返回 `fallback.Cursor`。

其二，未知 session 返回 `AgentPairCursor.initial`。FALLBACK-001 规定 cursor 由 Authority Root 创建，缺失意味着没有被接受的 root；返回 `initial` 会让「无已证明的 authority」看起来像「一个刚开始的 run」。改为返回 `option`，并新增 `mayContinue`，未知 session 一律 `false`：没有已证明的 authority 就没有自动物理请求。

同时删除 `nextDecision`——它是 `currentState` 的同义转发，两个名字指向同一次读取，等于让读者猜哪个是权威。

#### PromptMetadataCodec

字段改 typed：`PromptKey`、`LogicalRunId option`。PROMPT-011 要求 PromptKey 进入 Host metadata，因为它是崩溃后调和未决 claim 的唯一锚点。

`LogicalRunId` 用 `null` 而非 `""` 表示不存在：Authority Root claim 尚无 run（run id 由 Host 还未创建的物理消息派生），`""` 会让「无 run」与「名为空串的 run」在 wire 上同形。

删除 `wanxiangshu_authority_root` 字段——它的唯一用途是让 review 从共享 authority root 推断确认，REVIEW-003 已禁止。

### 包 A：PromptDispatcher

| 项 | 值 |
|----|----|
| 条款 | PROMPT-001 PROMPT-002 PROMPT-003 PROMPT-004 PROMPT-005 PROMPT-006 PROMPT-007 PROMPT-009 PROMPT-011 FALLBACK-008 REVIEW-003 REVIEW-007 |
| 目标模块 | `OpenCode/{PromptDispatcher,PromptDispatcherSend,PromptIngress,PromptIngressCodec,PromptMetadataCodec,Sessions,OpenCodePort,HostSessionNudge,HostReviewGuard,HostSignalBootstrap,OneShotAgentTool,TurnReconcile}.fs`、`Session/{HostForkAgentOwner,HostForkRunLifecycle,CompanionHostBlogger}.fs`、`Domain/{PromptAuthority,PromptAuthorityRun}.fs`、`Kernel/DomainFlow.fs`、删除 `Review/{Guard,ReviewProgram}.fs` |
| 旧入口 | `PluginPromptAccepted`（单一 accepted 事实，混淆 receipt 与物理落地）；`OpenCodePort` 裸 `prompt_async` 多点调用 |
| 新入口 | 四事实 `PluginPromptClaimed` / `PluginPromptSubmitted` / `PluginPromptPhysicalAccepted` / `PluginPromptAbandoned`；单一 sender |
| 必须删除 | `PluginPromptAccepted`；`accepted-*` 参与 Authority 的所有路径；生产模块中除唯一 Host adapter 外的 `prompt_async` |
| 静态验收 | `prompt_async` 在 `next/` 只出现在唯一 Host adapter（已达成：1） |
| 生产 | DOMAIN_MIGRATED（发送与受理链完成；`TurnCompletionProgram` / `PluginFallbackRetry` / `TerminalPolicies` / `ProviderFailureWakeup` 侧调用方按其所属包 C/D/F 迁移） |
| 测试 | 删除 `tests-next/Review/Guard{,Durable}Tests.fs`（被测模块已删），其余 UNTOUCHED |

#### SendOutcome 必须穿透传输层

`ISessionHostPort.SendPrompt` 原返回 `Task<Result<MessageId, string>>`。两处擦除：成功分支必须凭空造一个 message id，失败分支把四种结局压成一个字符串。

`AcceptanceUnknown` 因此与 `Retryable` 同形。这不是精度问题——PROMPT-011 规定未决发送不得自动重发，而压平后调用方唯一能做的判断就是「失败了，重试」，正好是产生第二个物理效果的路径。

现在端口原样传递 `SendOutcome`，`AdmittedWithReceipt` 与 `AdmittedWithPhysicalMessage` 是两个 case：receipt 是传输令牌，物理消息才可成为 Authority Root，类型本身阻止混用。

#### 生产代码里的测试替身

`InjectedSessionPort` 在 `underlyingPort = None` 时伪造一个 `AgentRunResult { FinalText = "test output" }` 并直接 `NotifyTerminal Completed`。`CreateChildSession` 同样在无端口时自造 GUID SessionId。

后果不是「测试用的假数据」，而是配置错误的运行时与一个真正完成的 agent 不可区分：没有解析出 Host 传输的插件会立刻报告一次成功完成。两处改为 `Fatal` / `Error`（VERIFY-005）。

#### 五条绕过 Dispatcher 的直发路径

`OneShotAgentTool`、`CompanionHostBlogger`、`HostForkAgentOwner`、`HostForkRunLifecycle.sendChildPrompt` 各自带一条 `journal = None → sessions.SendPrompt(..., Metadata = None)` 兜底分支。

这些分支发出的是真实 prompt，但没有 PromptKey：PROMPT-011 没有锚点可恢复，PromptIngress 只能判为 `UnknownOrigin`。全部改为 `Error`——PROMPT-005 使插件 prompt 成为持久行为，无处记录时没有合法的可发内容。

`PromptDispatcher.ephemeral` / `ephemeralNamed` / `forRuntime` / `Dispatcher` 随之删除，`Runtime` 构造函数不再接受 `AgentJournal option`：一个无处持久化的 dispatcher 会为它静默丢弃的事实返回 `Ok`。

#### Runtime 不再持有 authority 状态

旧 `Runtime` 维护 `mutable authority`，并由 `fromJournal` 把每个 session 的投影 fold 成一个值作为种子。两个后果：一个 session 里的 claim 在另一个 session 可见（PERSIST-008 违规），且内存副本可与它本应镜像的 journal 分歧。

现在每次读取走 `ProjectionFor sessionId`，fold 是唯一写者。`ActiveProfile` 同时删除了回退到 `LastAuthorityProfile` 的分支——PROMPT-004 把 continuation 限定在 active run，用陈旧 profile 顶替正是必须禁止的。

#### PromptKey 派生而非生成

`newPromptKey()` 是 GUID。恢复因此不可能：重启后无法重新导出同一个 key，Host metadata 里的锚点就成了一次性随机数。

现在 `derivePromptKey` 取 (SessionId, LogicalRunId, AuthorityRoot, Origin, EffectiveAgent, PayloadDigest, ClaimSequence)，`ClaimSequence` 由 `registerClaim` 在 fold 中推进——无论该 claim 后来是否成立。只在成功时推进会让一次被放弃的发送与它的重试派生出同一个 key，恢复时就会为两个不同的逻辑行为找到同一个锚点。

`AuthorityRoot` 的 LogicalRunId 与 AuthorityRootUserMessageId 传 `None` 而非空串：这次发送正是创造它们的行为，空串会让「尚无 run」与「名为空串的 run」派生同一个 key。

#### FALLBACK-008 的预算此前只活在内存里

`tryClaimRepair` 写 `PromptAuthorityProjection.RepairClaims`，而 32 个事实里没有任何一个写它。at-most-once 保证随进程消失。

不新增第五个事实，而是让 repair 的 payload digest 命名场合（terminal ProviderRun + repairKind）而非 prompt 文本——repair 文本按 kind 固定，摘要文本会使同 kind 的每次 repair 成为同一个逻辑行为，per-terminal 预算退化成 per-session 预算。

digest 进入 claim scope 后，PROMPT-005 `Claimed` 已经写入的 `ClaimSequences` 就是计数器，`repairAlreadyClaimed` 读回 `> 1` 即已用尽。`RepairClaims` 字段与 `tryClaimRepair` 删除。

#### GuardPromptAccepted 是「插件 prompt 落地」的第二个名字

Guard nudge 去重原先读 `ReviewGuardProjection.AcceptedGuardKey`，由 `GuardPromptAccepted` 事实写入；`PromptIngress` 另有一条 ReviewConfirmation 分支补写同一事实，用途是让第二次 PERFECT 匹配 `ConfirmationPhysicalMessageId`。

这个用途 REVIEW-003 已禁止：continuation 落地不说明模型消费了 challenge。而「插件 prompt 落地」PROMPT-005 已经拥有，第二个名字只能与第一个分歧。

去重改读 `PendingClaims` 中是否已有同 `ContinuationKind` 的未决 claim。`nudgeKeys` 内存集合保留但只抑制同进程内重复发送，且仅在发送成功后写入——失败必须保持可重试，被去重的是受理，失败不是受理。

#### Review/Guard.fs 与 ReviewProgram.fs 是孤儿

两文件的生产消费者为 0。`Guard.recordVerdict` 构造 `ReviewVerdictRecorded` 时使用的 `ProviderRunId` / `UserPromptText` / `UserMessageId` 字段在包 0b 后已不存在，`guardMissingVerdict` 读的 `AcceptedGuardKey` 亦已不存在；`ReviewProgram.confirmPerfect` 比较 `ctx.BarrierId = newTreeHash` 两个字符串，与 REVIEW-003 要求的 seal 因果证明无关。

一并删除后 `ReviewFlow` / `ReviewContext` / `ReviewError` 与 `review {}` builder 无任何消费者，从 `Kernel/DomainFlow.fs` 移除；`architecture-gate` 的 `review` DSL 条目删除，`confirmPerfect` 的 owner 改为 `Domain/ReviewWitness.confirm`——确认判定是两个 verdict witness 与 seal 的纯函数，不是 Flow 程序。

#### FactCodec 的旧事实名不是残留

`shock-audit` 把 `FactCodec.pre050Markers` 里的 13 个旧 case 名字面量计为残留。但 PERSIST-004 要求 pre-0.5.0 journal 停止启动并给出精确诊断，识别它的唯一方式就是这些 case 名。

按残留计数会让每个已迁移事实永久非零，门禁最终会逼出一次错误的删除——删掉那个告诉运维「归档旧文件」的检查本身。`shock-audit` 因此对该单一文件豁免残留计数（`LEGACY_NAME_SENTINEL`），代价是该文件内的真实违规在此项看不见；可接受，因为它只含 codec 与这份拒绝清单，且架构与 ssot 门禁仍读它。

### 包 B：AttemptExecutionProfile

| 项 | 值 |
|----|----|
| 条款 | PROMPT-008 AGENT-001 AGENT-007 AGENT-010 ORCH-003 COMPANION-002 EXEC-009 |
| 目标模块 | `OpenCode/{ToolRuntimeScope,ToolRegistry,CompanionTransform,PluginRuntimeScope,PluginHost,PluginHostInterop,SpikePlugin,HostSignalBootstrap,HostSessionContext,PromptIngress,OrchestratorHost,ExecutorSummarizeRuntime,ForkTool,TurnCompletionProgram}.fs`、`Session/{AgentRoleIdentity,ForkRuntime,HostForkRuntimeFork,HostForkChildDispatch,HostForkRestart}.fs`、`Process/Pty.fs`、删除 `Session/ChildDispatch.fs` |
| 旧入口 | 各模块自行从 Agent 字符串解析 Role；`sessionRoles: Dictionary<string,string>`；`RoleFor context` |
| 新入口 | 唯一 `buildAttemptExecutionProfile`，所有模块接收 profile |
| 必须删除 | 任何在 profile 构造之外解析 `fast-`/`deep-` 前缀得出 Role 的代码 |
| 允许出现 Agent 字符串 | 配置解析、Authority Root 创建、profile 构造、Host 发送边界 |
| 静态验收 | `sessionRoles` / `SessionRoles` / `defaultFastManagedName` 在 `next/` 为 0 |
| 生产 | DOMAIN_MIGRATED（Role 读侧与 fork 发送侧完成；两处 EXEC-009 阻塞见下） |
| 测试 | UNTOUCHED |

#### Role 有三个来源，AGENT-007 只允许一个

`ToolRuntimeScope.roleName` 依次尝试 Authority、`sessionRoles` Dictionary、Host context 的 `agent` 字段。三者任一命中即返回，因此一个 Authority 说 Coder 的 session 仍可能因为缓存条目或消息字段而被当作 DevOps 授权。

AGENT-007 要求两层权限门读同一个 `CanonicalRole`，且 Role 无法确定时工具集为空。现在只剩 `ActiveLogicalRun.CanonicalRole`，返回 `Role option`；`sessionRoles` 整个 Dictionary 连同 `PluginRuntimeScope.SessionRoles`、`restoreSessionRoles`、四个写入点一并删除。

条款还点名了要删的东西：ToolRegistry 在 Role 未解析时放行 `inspector`，理由是它只读且多角色可用。只读与否不改变「在未知 Role 下执行」这件事——该豁免删除。

#### fork 的 managed agent 名不得由 Role 反推

`ForkRuntime.Fork` / `Restore` 与 `HostForkRuntime.Fork` 的 `?agent` 缺省走 `defaultFastManagedName role`，即凭空造出 Fast 层。造出来的名字随后进入 completion 记录与 Host 发送边界，看起来与真正被选中的名字无异。

三处签名改为必填 `agent: string`。`defaultFastManagedName` 删除。

`HostForkRuntime.Reuse` 原先也会在记录里没有名字时回落到该函数，等于一次 reuse 就把 `deep-coder` 降级为 `fast-coder`；现在读 `AgentRecord.Agent`，空则报错。

`OrchestratorHost.runManager` 同类：`managerAgents` 内存 map 未命中时回落 `fast-manager`。未命中正是重启场景，而 ORCH-003 持久化 `ManagerJobProjection.ManagerAgent` 就是为了这个场景——回落使 `deep-manager` 作业以 `fast-manager` 恢复。改为报错。

`ExecutorSummarizeRuntime` 与 `CompanionHost` 的固定名保留：Executor 与 Blogger 是 AGENT-008 的内部 Agent，其角色是常量而非推断。`OrchestratorHost.runReviewerOnce` 改为复用 `OrchestratorHostReview.DeepReviewerAgent`，不再第二次拼写同一策略。

#### COMPANION-002 的缓存回写

`CompanionTransform` 从 `ActiveLogicalRun.SelectedAgent` 判定 eligibility（正确），但随后把推得的 role 写回 `sessionRoles`，并在 Blogger 创建时写入 `"blogger"`。写入本身就是那条被禁止的第二来源的供给方。两处删除，`handleCompanionTransform` 不再接收该参数。

#### PromptIngress.onAuthorityResolved 回调

其唯一实现体是往 `sessionRoles` 写 CanonicalRole。`AuthorityRootAccepted` 事实已经是该 Role 的记录，每个消费者都从投影读回。回调连同参数删除。

#### Session/ChildDispatch.fs 是孤儿

12 个函数无任何生产或测试消费者，其中 `tryCancel` 是注释为 P6 占位、恒返回 `false` 的假实现。整文件删除（包 F 的「`ChildDispatch.tryCancel` 占位」条目随之作废）。

#### 两处 EXEC-009 阻塞

`HostForkRestart.restoreLinkedChildren` 与 `TurnCompletionProgram` 的 linked-child authority 都需要「由 handle 找到 child session 与其 managed agent 名」。

包 0c 把 `AgentLinkageProjection` 从 `LinkedChildren`/`LinkedRoles`/`ForkedChildren` 换成按 `HandleId` 键入的 `Handles`，而 `HandleLinked` 只携带 `{ ParentSessionId; Handle; TargetAgent; CanonicalRole }`——没有 child SessionId。

child session id 由 Host 签发，从 handle id 派生只会造出一个此后每次操作都静默空转的身份，因此不能就地补。两处写 `SHOCK-UNMIGRATED[EXEC-009]`，留给包 F：要么 `HandleLinked` 增加该字段（需 SSOT 例外协议），要么 EXEC-009 明确恢复期以别的方式重新解析 children。

`TerminalPolicy.isLinkedChild`、`VerdictTool`、`ReviewerGuardState`、`OrchestratorSessionDirectories`、`PluginHost.restoreSessionParents`、`CompanionTransform` 的 blogger 恢复也都还在读旧 `Linkage` 字段，同属包 F 的连带面。

#### Process/Pty.fs 的 `fast-%s`

PTY completion 的 `AgentName` 由 `AgentRole` 拼出 `fast-*`，无角色时得到 `fast-executor`。未标记 unmigrated：该名字只进入 completion 记录的诊断字段，而 EXEC-015 要求 PTY completion 只由 backend `onExit` 触发，在此处失败会破坏该条款。改为注释说明并留给包 F 让 `PtyHandle` 携带 managed 名。

### 包 C：FallbackController

| 项 | 值 |
|----|----|
| 条款 | FALLBACK-002 FALLBACK-003 FALLBACK-004 FALLBACK-005 FALLBACK-007 FALLBACK-010 |
| 目标模块 | `FallbackDetect.fs` `RetrySignalHandler.fs` `ProviderFailureWakeup.fs` `Session/DurableFallback.fs` `Journal/FallbackProjection.fs` |
| 旧入口 | 两个 writer：`RetrySignalHandler:84` 与 `ProviderFailureWakeup:51` 都调 `recordFallbackFailure` |
| 新入口 | 唯一 `FallbackController`；Host 信号只 MarkDirty |
| 必须删除 | `ProviderFailureContinuation.recordDurableAdvance`；`FallbackCursorAdvanced` 事实中缺 `PreviousOffset`/`NextOffset`/`ConsecutiveFailureCount` 的旧形状 |
| 必须新增 | `FallbackExhausted`；`AutoRecoveryBudget = 12`；Offset 与 ConsecutiveFailureCount 分离 |
| 静态验收 | `FallbackCursorAdvanced` 在 `next/` 除类型/codec/fold 外恰好一个 append site |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

### 包 D：Review

| 项 | 值 |
|----|----|
| 条款 | REVIEW-003 REVIEW-004 REVIEW-005 REVIEW-006 REVIEW-008 REVIEW-010 HOST-010 HOST-011 |
| 目标模块 | `Journal/ReviewConfirmation.fs` `Journal/ReviewProjection.fs` `Domain/ReviewWitness.fs` `VerdictTool.fs` `Review/Guard.fs` `Session/ReviewerHost.fs` `CompanionTransform.fs` |
| 旧入口 | `physicalConfirmationMatched`（三种弱代理：AcceptedContinuationIds / AcceptedContinuationRoots / ConfirmationPhysicalMessageId）；`samePhysicalRootReevaluationMatched` |
| 新入口 | `PerfectChallengeIssued` → `ProviderInputSealed` → `ConfirmedReviewWitness`；seal 按 HOST-010 判据绑定 ProviderRunIdentity |
| 必须删除 | same-root matcher；physical-message-as-proof matcher；`ConfirmationPhysicalMessageId` 字段；`RecentProviderRunIds` 作为唯一去重依据 |
| 必须新增 | 固定版本化 challenge text（`ChallengeTextVersion = 1`）；transform 内 seal 生成；fail-closed 三条件 |
| Host 前提 | 已证明可实现，见 `STATUS/evidence/host-transform-run-binding.md`。不需 SSOT 例外 |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

整体迁移，不能分半。

### 包 E：Companion

| 项 | 值 |
|----|----|
| 条款 | COMPANION-001 COMPANION-002 COMPANION-008 COMPANION-009 COMPANION-010 COMPANION-013 EXEC-008 HOST-005 HOST-006 |
| 目标模块 | `Tools/MessageTransform.fs` `Session/Companion*.fs` `CompanionTransform.fs` `Journal/CompanionProjection.fs` |
| 旧入口 | `MessageTransform.shouldCreateCompanion(agent)` 从 Agent 字符串判定 |
| 新入口 | eligibility 只读 ActiveLogicalRun 的 `AttemptExecutionProfile.CanonicalRole` |
| 必须删除 | `shouldCreateCompanion(agent)`；三次 busy skip 计数；child background 使用 FrozenB 的路径 |
| 必须修正 | 六角色开启 Companion（含 Reviewer、Meditator、DevOps）；ARecord 按 ProviderRun 分段；child background 用最新 durable LatestB |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

### 包 F：Execution / Handle

| 项 | 值 |
|----|----|
| 条款 | EXEC-004 EXEC-005 EXEC-009 EXEC-011 EXEC-015 |
| 目标模块 | `Session/{ChildRun*,ForkRuntime,HostForkRestart}.fs` `OpenCode/{JoinTool,ListTool,TerminalPolicy,VerdictTool,ReviewerGuardState,OrchestratorSessionDirectories,PluginHost,CompanionTransform,TurnCompletionProgram}.fs` `Process/{Deadline,Pty}.fs` |
| 旧入口 | `AgentLinked` / `AgentForked` / `AgentUnlinked` 三事实无 completed/retired 区分 |
| 新入口 | `HandleLinked` / `HandleCompleted` / `HandleRetired`；active / completed-awaiting-join / retired 三态分离 |
| 必须删除 | retired handle 回退成 Agent 名称重新 fork 的路径 |
| 必须新增 | join 消费后写 tombstone；真实单 child cancel；parent abort 逐项取消；process 管理员 hard limit |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

包 B 已删除 `Session/ChildDispatch.fs`（12 个函数无消费者，`tryCancel` 是恒返回 `false` 的 P6 占位），故原「`ChildDispatch.tryCancel` 占位」条目作废；真实单 child cancel 仍需新建。

#### 先决：`HandleLinked` 缺 child SessionId

包 0c 把 `AgentLinkageProjection` 换成按 `HandleId` 键入的 `Handles` 后，`LinkedChildren` / `LinkedRoles` / `ForkedChildren` 全部消失，而 `HandleLinked` 只携带 `{ ParentSessionId; Handle; TargetAgent; CanonicalRole }`。

以下读侧全部悬空，且都需要「handle → child session」这一步：

```text
Session/HostForkRestart.restoreLinkedChildren    重启恢复 join mailbox
OpenCode/TurnCompletionProgram                   linked-child AgentOwner authority
OpenCode/TerminalPolicy.isLinkedChild
OpenCode/VerdictTool
OpenCode/ReviewerGuardState
OpenCode/OrchestratorSessionDirectories
OpenCode/PluginHost.restoreSessionParents
OpenCode/CompanionTransform                      blogger child 恢复
```

child session id 由 Host 签发。从 handle id 派生会造出一个此后每次操作都静默空转的身份，因此不能就地补。本包开始时先决定二者之一：`HandleLinked` 增加 `ChildSessionId`（走 SSOT 例外协议），或 EXEC-009 明确恢复期以别的方式重新解析 children。前两处已标 `SHOCK-UNMIGRATED[EXEC-009]`。

`Process/Pty.fs` 的 `PtyHandle` 同样只记 `AgentRole`，故 completion 的 `AgentName` 只能拼 `fast-*`；本包应让它携带 forking profile 选定的 managed 名。

### 包 G：Orchestrator

| 项 | 值 |
|----|----|
| 条款 | ORCH-003 ORCH-004 ORCH-005 ORCH-006 ORCH-007 ORCH-008 REVIEW-009 |
| 目标模块 | `Orchestrator*.fs` `next/OpenCode/Orchestrator*.fs` `Journal/OrchestratorProjection.fs` |
| 旧入口 | `OrchestratorCandidateRegistered` / `OrchestratorRebased` / `OrchestratorRejected` / `OrchestratorPre|PostRebaseReviewConfirmed` — stage-like 恢复投影 |
| 新入口 | `ManagerJobCreated`（含 `ManagerAgent` `WorktreeIdentity` `TargetBranchFrozen`）/ `CandidateReady` / `ConflictDetected` / `RebasedCandidateReady` / `PublishClaimed` / `Published` / `JobFailed` / `JobAbandoned` |
| 必须删除 | 跨 review 持有 Integration Gate；旧 stage-like recovery projection |
| 必须新增 | 短 CAS 发布窗口；`PublishClaimed` 三分支恢复（ORCH-007 固定顺序）；barrier 事实携带 witness ID |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

最后迁移，依赖前面几乎全部协议。

### 包 H：Plugin Composition Root

| 项 | 值 |
|----|----|
| 条款 | HOST-009 VERIFY-005 |
| 目标模块 | `SpikePlugin.fs` `PluginRuntimeScope.fs` `PluginHost.fs` `HostSignalBootstrap.fs` |
| 旧入口 | 双 transform hook 注册 + 幂等 guard；多处 Dictionary 充当 session 状态 |
| 新入口 | 单 Journal / 单 PromptDispatcher / 单 FallbackController / 单 profile builder / 单 HostSignal adapter / 单 ToolRegistry；显式 dispose |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

### 包 T：测试语言迁移

| 项 | 值 |
|----|----|
| 条款 | VERIFY-001 VERIFY-005 VERIFY-008 |
| 目标模块 | `tests-mjs/`（新建）、`tests-next/`（删除）、`scripts/architecture-gate.mjs`（新建）、`package.json` |
| 旧入口 | `tests-next/**/*.fs` + `Wanxiangshu.Next.Tests.fsproj` + `Assert.fs` 手写 xunit 替身 + `npm run test:compile` + runner 的 Fable 导出发现 |
| 新入口 | `tests-mjs/**/*.mjs` 走 `node:test`，import `build/next`；Fable 约定隔离在 `tests-mjs/domain.mjs` |
| 必须删除 | `tests-next/` 全部 `.fs`、tests `.fsproj`、`Assert.fs`、`EventDrivenHarness.fs`、`MockOpenCode/*.fs`、`Gates/*.fs`、`npm run test:compile`、`build/tests-next` 相关清理 |
| 必须新增 | `domain.mjs` facade + 其元测试；runner 陈旧产物 fail-closed；`architecture-gate.mjs` |
| 生产 | 不涉及（本包不改 `next/`） |
| 测试 | UNTOUCHED |

本包与包 0–H 正交：它换的是验证层的语言与入口，不改任何生产语义。因此工装部分（facade、runner 门禁、architecture-gate）在封炉期即可完成并验证；测试内容本身在休克三按条款重建。

#### 必须在 mjs 侧重建覆盖的 15 个静默失效测试

`architecture-gate.mjs` 新增的 `fsproj-drift` 门禁发现 5 个测试文件在 `c3c35756`（`refactor: unify authority fallback and process flows`）从 `.fsproj` 移除但文件留在磁盘上。它们从那时起就没有运行过，`test:next` 的绿灯不覆盖这 15 个断言。

其中 2 个断言的正是 SSOT 现在禁止的语义，属于旧世界的保护罩，不得重建：

```text
PromptAuthoritySendTests
  SendAgentOwnerRoot with accepted-* still accepts authority and sets ActiveLogicalRun
  AcceptAgentOwnerRoot is idempotent after SendAgentOwnerRoot accepted-*
      ↑ 与 PROMPT-005 直接冲突：accepted-* 不是 Authority Root
```

其余 13 个是真实语义，必须在 `tests-mjs` 按条款重建：

| 原测试 | 条款 |
|--------|------|
| Prompt authority continuation never replaces authority root | PROMPT-003 |
| Chat_message_recovers_prompt_key_from_text_part_metadata | PROMPT-011 |
| Chat_message_accepts_single_pending_claim_without_transport_metadata | PROMPT-011 |
| AcceptHumanRoot builds managed agent profile without model fields | PROMPT-002 PROMPT-006 |
| SendAgentOwnerRoot sends EffectiveAgent with Model None | PROMPT-006 |
| Stable logical run id is deterministic for same host message | PROMPT-011 |
| Interaction repair identity is claimed only once | PROMPT-005 |
| Continuation acceptance preserves last authority profile | PROMPT-003 |
| Dispatcher claims continuation and preserves authority metadata | PROMPT-005 |
| Unknown physical user message never becomes authority | PROMPT-004 |
| New authority root replaces profile while continuation does not | PROMPT-002 PROMPT-003 |
| createAuthorityRoot rejects bare legacy agent names | AGENT-004 AGENT-005 |
| Chat_message_maps_keyed_continuation_without_becoming_human_root | PROMPT-009 |

`tests-next/Agent/ProgramsTests.fs` 只剩一行注释（`// DELETED - Tests for dead Programs module`），无需重建。

文件本身在封炉期删除；内容由 Git 历史保存（`c3c35756^`）。

### 包 W：因果推进门禁重建

| 项 | 值 |
|----|----|
| 条款 | VERIFY-004 VERIFY-002 |
| 目标模块 | `testkit/opencode/watchdog.js`、`watchdog-constants.js`、`stability-checker.js`、`scripts/run-canary-staggered.mjs`、`tests-next/runner.js` |
| 原理状态 | 保留。没有进展就杀死、watchdog 按语义事件投喂、因果 bark 交错启动，三者是既有设计中最有价值的部分，必须继承发扬 |
| 实现状态 | 不合格。见下方实测缺陷 |
| 重建方式 | 第一性原理瀑布流，与包 K 同期。禁止在现有实现上逐点修补 |
| 生产 | 不涉及 |

原理与实现必须分开评价。VERIFY-004 的文字是判据，现有代码不因先存在而获得权威。

#### 实测缺陷

| 缺陷 | 位置 | 后果 |
|------|------|------|
| 声明了断言心跳但未接线 | `tests-next/Assert.fs:13` `resetHeartbeat () : unit = ()` 空实现；`runner.js` 的 `globalThis.__resetAssertionTimeout` 无人调用 | 单测的「断言投喂心跳」根本不存在。1000ms 是纯 wall-clock 超时，读者却以为有因果保护 |
| 数量常量与清单各自维护 | `run-canary-staggered.mjs`：`CANARY_COUNT = 17`，`CANARY_TESTS` 实际 19 条 | 已经漂移。日志里的 `expected ~17` 是错的 |
| 静态门禁指向不存在的目录 | `stability-checker.js:32` 判 `e2e/opencode/specs/`，该目录不存在 | `containsTool` 检查恒为通过，伪门禁 |
| 超时值散落为字面量 | `run-canary-staggered.mjs:120` 就绪窗口 `10000`；`:35` canary 兜底 `90000`；`watchdog.js:84` 诊断竞速 `3000` | 只有 `WATCHDOG_TIMEOUT_MS` 是集中的。其余三个无法统一调整，也无法被门禁检查 |
| 启动阶段只有 wall-clock 覆盖 | canary 进程拉起到 `[setupScenario] ready` 之间只有 10s 硬窗口 | 存在一段无因果判据的时间窗，违反 VERIFY-004「覆盖必须无缝」 |

#### 重建顺序

```text
W1  集中所有时间常量，建立单一来源；门禁禁止字面量超时
W2  canary 清单单一事实来源，数量从清单派生
W3  删除伪门禁，静态检查路径判据与实际目录对齐
W4  重建单测运行器的因果心跳：断言真实投喂，并有测试证明未接线会红
W5  启动阶段因果判据，消除只有 wall-clock 的时间窗
W6  watchdog 重写：语义投喂、背景不续期、诊断完整、不持有事件循环
W7  gate-testkit 增加门禁自检：每条「禁止退化清单」都有对应失败测试
```

W4 与 W7 是本包的重点：现在的门禁声明了自己有能力，但没有测试证明能力真实存在。`Assert.fs` 的空 `resetHeartbeat` 能存在这么久，正是因为没有任何测试断言心跳被投喂。

### 包 K：剧本森林重建

| 项 | 值 |
|----|----|
| 条款 | VERIFY-003 VERIFY-004 VERIFY-007 ARCH-004 |
| 目标模块 | `testkit/opencode/strict-mock-*.js`、`script-loader.js`、`scripts/*.json` → `*.toml` |
| 旧入口 | 谓词合取匹配 + `specificity` 打分消歧 + `pathCursor` 游标 + `lane.turn` 排序 + 四标志（`reusable`/`pathless`/`blocking`/`neverEnd`）+ 对 error 删除 seal 缓存 + `requestRoleOf` 从 wire 反推角色 + `loadScripts` 运行期换剧本 + `epochCold`/`modelSideCold` 嗅探式冷边界豁免 |
| 新入口 | 静态单 TOML 文件；运行时键 `(lane, turn, step)` 皆为请求纯函数；内容/故障/冷边界/断言四份独立声明；载入期六项校验 |
| 必须删除 | `specificity`、`pathCursor`、`sealToEdgeId` 删除逻辑、`templateFingerprint`、`aliasToEdge`、`claimCount`/`matchCount` 双计数、`requestRoleOf`、`NUDGE_MARKERS`、`__testkitHeaders` 参与内容匹配、`epochCold`/`modelSideCold`、`loadScripts`、`extractLastUserMsg` 2000 字符截断 |
| 必须新增 | TOML schema + 载入期编译器 + 根键顺序硬检查 + formatter + 旧字段拒绝器 + 森林纯函数性自检 |
| 设计定稿 | `STATUS/design-script-forest.md`（第一性原理分析 + TOML schema） |
| 生产 | 不涉及（本包不改 `next/`，唯一例外是删除生产 prompt 中的 `Role canary` 类测试标记） |
| 测试 | UNTOUCHED |

子步骤 K1–K10 见设计文档第十二节。K8（22 个 canary 手工重写为 TOML）是唯一大量手工劳动，禁止脚本批量转换。

本包依赖包 A–H 完成：剧本的 lane 划分与 step 序列反映的是迁移后的生产行为，先重写剧本会锁定旧语义。因此排在休克三之后、退火三之前。

## 旧符号灭绝表

残留数由 `node scripts/shock-audit.mjs` 测量。休克结束时全部为 0（除标注「允许 1」的唯一入口）。

基线测量：commit `274a30aa`，2026-07-29。包 0 完成后的复测标注在行内。

### 作用域计数

少数符号在 SSOT 授权的位置合法、在别处非法。这类行只统计非法路径，`shock-audit` 标 `(scoped)`。

全仓计数对它们是错的：目标永远到不了 0，最终会逼出一次错误的删除。违规是出现的位置，不是符号本身。

| 符号 | 合法用途 | 非法路径（计数范围） |
|------|---------|-------------------|
| `AcceptedContinuationIds` | PROMPT-003 / PROMPT-009：判定某消息是否为 continuation 及其种类 | `Journal/ReviewConfirmation` `Journal/ReviewProjection` `Review/` `Session/ReviewerHost` |

`AcceptedContinuationRoots` 不在此列：它存在的唯一目的是让 witness 从共享 authority root 推断确认，无任何授权用途，整仓清零。

### 生产与测试侧

| 旧符号 / 行为 | 新语义 | 条款 | next | tests | testkit | 目标 |
|--------------|--------|------|------|-------|---------|------|
| `PostPromptFireAndForget` | `Dispatch(_, Detached)` | PROMPT-007 | 0 | 0 | 0 | 0（已达成） |
| 裸 `prompt_async` 调用 | 唯一 Host adapter sender | PROMPT-005 | 5→1 | 2 | 12 | next 允许 1（已达成） |
| `PluginPromptAccepted` | Submitted + PhysicalAccepted 两事实 | PROMPT-005 | 7→0 | 5 | 0 | 0 |
| `recordDurableAdvance` | `FallbackController` | FALLBACK-003 | 2 | 0 | 0 | 0 |
| `ProviderFailureContinuation` | Host 信号只 MarkDirty | FALLBACK-003 | 2 | 0 | 0 | 0 |
| `ProviderFailureWakeup` | reconcile 从 snapshot 识别失败 | FALLBACK-003 | 5 | 0 | 0 | 0 |
| `FallbackCursorAdvanced` 旧形状 | 含 Previous/Next Offset + count | FALLBACK-007 | 5 | 17 | 7 | next 允许 1 append site |
| `ConfirmationPhysicalMessageId` | `ProviderInputSeal` | REVIEW-010 | 11 | 0 | 0 | 0 |
| `samePhysicalRootReevaluation` | seal 含 challenge digest | REVIEW-003 | 2 | 0 | 0 | 0 |
| `AcceptedContinuationIds` 出现在 review 路径 | seal 因果证明 | REVIEW-003 | 1 | 0 | 0 | 0 |
| `AcceptedContinuationRoots` | seal 因果证明 | REVIEW-003 | 3 | 0 | 0 | 0 |
| `RecentProviderRunIds` 作唯一去重 | `ReviewAttemptIdentity` | REVIEW-004 | 5 | 0 | 0 | 0 |
| `ReviewConfirmedIdle` | `ConfirmedReviewWitness` | REVIEW-006 | 6 | 1 | 1 | 0 |
| `GuardPromptAccepted` | Dispatcher 四事实 | PROMPT-005 | 6→0 | 8 | 0 | 0 |
| `InteractionRepairClaimed` | Dispatcher `Claimed` + Origin | PROMPT-005 | 5→0 | 0 | 0 | 0 |
| `HumanPromptAccepted` | `AuthorityRootAccepted` | PROMPT-004 | 5→0 | 0 | 0 | 0 |
| `shouldCreateCompanion(agent)` | ActiveLogicalRun CanonicalRole | COMPANION-002 | 2 | 3 | 0 | 0 |
| `ToolContext.userMessageID` 读取 | `CurrentPhysicalUserMessage` | HOST-011 | 1 | 0 | 0 | 0 |
| `AgentLinked`/`AgentForked`/`AgentUnlinked` | Handle 三事实 | EXEC-009 | 12 | 26 | 0 | 0 |
| `OrchestratorCandidateRegistered` | `CandidateReady` | ORCH-006 | 3 | 8 | 0 | 0 |
| `OrchestratorRebased` | `RebasedCandidateReady` | ORCH-006 | 3 | 3 | 0 | 0 |
| `OrchestratorRejected` | `JobFailed` | ORCH-006 | 2 | 1 | 0 | 0 |
| `OrchestratorPreRebaseReviewConfirmed` | `CandidateReady.PreRebaseReviewWitnessId` | ORCH-006 | 3 | 4 | 3 | 0 |
| `OrchestratorPostRebaseReviewConfirmed` | `RebasedCandidateReady.PostRebaseReviewWitnessId` | ORCH-006 | 3 | 2 | 2 | 0 |
| 双 transform hook 注册 | 单 hook | HOST-009 | 2 | 0 | 0 | 1 |

### 剧本森林侧（包 K）

`testkit` 列统计 `.js`/`.mjs`，`剧本` 列统计 `testkit/opencode/scripts/`。

| 旧符号 / 行为 | 新语义 | 条款 | testkit | 剧本 | 目标 |
|--------------|--------|------|---------|------|------|
| `specificity` 打分消歧 | 最长前缀唯一命中 | VERIFY-003 | 6 | 0 | 0 |
| `pathCursor` 游标推进 | `step` = assistant 消息条数 | VERIFY-003 | 8 | 0 | 0 |
| `lane.turn` 排序键 | `turn` = user 语义内容 | VERIFY-003 | — | 254 | 0 |
| `reusable` 标志 | 纯函数天然可复用 | VERIFY-003 | 17 | 46 | 0 |
| `pathless` 标志 | 无游标可豁免 | VERIFY-003 | 18 | 8 | 0 |
| `blocking` 参与匹配 | 迁到 `must` 断言 | VERIFY-003 | 53 | 36 | 0 |
| `sealToEdgeId` 失败时删除 | 故障轴独立于内容 | VERIFY-003 | 6 | 0 | 0 |
| `templateFingerprint` 模板去重 | 无需去重 | VERIFY-003 | 3 | 0 | 0 |
| `aliasToEdge` 别名映射 | 两次 PERFECT 是两个 step | REVIEW-003 | 5 | 0 | 0 |
| `claimCount`/`matchCount` 双计数 | 每个 step 独立可 wait | VERIFY-003 | 8 | 0 | 0 |
| `requestRoleOf` 反推角色 | 角色由 profile 决定 | PROMPT-008 | 9 | 0 | 0 |
| `NUDGE_MARKERS` prose 常量 | 无（跨产品死启发式） | VERIFY-003 | 1 | 0 | 0 |
| `__testkitHeaders` 参与匹配 | harness 记账单向 | VERIFY-003 | 4 | 0 | 0 |
| `epochCold` 嗅探豁免 | `[[epoch]]` 显式声明 | COMPANION-009 | 3 | 0 | 0 |
| `modelSideCold` 嗅探豁免 | `[[epoch]]` 显式声明 | FALLBACK-004 | 2 | 0 | 0 |
| `loadScripts` 运行期换剧本 | 静态单文件 | VERIFY-003 | 8 | 3 | 0 |
| `.json` 剧本 | `.toml` 剧本 | VERIFY-003 | — | 22 文件 | 0 |

`blocking` 的 53 处含大量非剧本用途（HTTP、进程），实施时需按用途分辨；剧本侧 36 处必须清零。

### 单一写入口实测

`shock-audit` 不只数事实名，还追 append helper 的调用方——否则一个 helper 就能把多个 writer 藏在一次调用后面。这正是当前 FALLBACK-003 的违规形态：

```text
FallbackCursorAdvanced   FALLBACK-003   3 writers
  next/OpenCode/FallbackDetect.fs                          （helper 定义 + append）
  next/OpenCode/ProviderFailureWakeup.fs:50  via recordFallbackFailure
  next/OpenCode/RetrySignalHandler.fs:84     via recordFallbackFailure

FallbackExhausted        FALLBACK-005   absent (fact not defined yet)
  next/Kernel/Outcome.fs 有同名但无关的 terminal-outcome case
```

包 C 完成时这两行必须变成 `ok (1)` 与 `ok (1)`。

## 熔断条件

出现任一项立即暂停新增迁移，回到本总账重新切分工作包：

1. SSOT 同一概念出现第二种解释
2. 新生产代码添加兼容 shim
3. 同一领域出现两个 writer
4. `SHOCK-UNMIGRATED` 数量连续两个工作包不下降
5. 一个工作包改动超过三个不相关领域
6. 无法解释某条物理副作用的 crash window
7. 为绕过 Host 限制依赖未公开 API
8. 测试迁移开始复制生产实现
9. 为通过编译重新引入旧 union case
10. STATUS 与实际 commit 不再对应

## SSOT 例外协议

休克期原则上不改 SSOT。确认无法实现时：

1. 停止相关代码迁移
2. 写 `STATUS/blocker-<条款>.md`
3. 证明是 Host 能力或逻辑矛盾，不是实现困难（必须引用 `../opencode` 源码行号）
4. 修改 SSOT
5. 在本文件追加 supersedes 记录
6. 重新冻结

禁止一边改代码一边悄悄降低条款。

已触发次数：0。

## 封炉期已完成

| 项 | 结果 |
|----|------|
| 基线保存 | `STATUS/evidence/pre-shock/`。prod/tests build 绿，test:next 290/3，gate-testkit 29/0 |
| SSOT 矛盾修复 | FALLBACK-005 循环无界 vs 预算有界；新增 FALLBACK-010（Host Attempt ≠ count）；FALLBACK-007 成功不写事实；VERIFY-006 重写；PROMPT-005 四事实 + Abandon reason；PROMPT-011 PromptKey 定义 + 恢复边界；ORCH-006 补 ManagerAgent/WorktreeIdentity/TargetBranchFrozen；ORCH-007 三分支固定顺序；VERIFY-003 改用 Semantic projection；VERIFY-007 单向有损关系；新增 HOST-010、HOST-011 |
| Host 能力证明 | REVIEW-010 seal→run 绑定可实现，不需例外。证据 `STATUS/evidence/host-transform-run-binding.md` |
| 悬空引用清理 | README 指向已删除的 `next/Doc/SSOT.md`、`AGENTS.md`、`MIGRATION.md`；已改指 `SSOT/00.md` 与 `STATUS/` |
| 行数门禁废除 | 删除 `Next_source_files_do_not_exceed_300_lines` 与 §17.7 行数带；VERIFY-005 改为只阻断语义。机械后缀 allowlist 保留 |
| 测试语言边界确立 | 新增 VERIFY-008：生产 `.fs`，第 1–3 层测试 `.mjs` 消费 `build/next`；VERIFY-001 增设第 0 层静态检查；VERIFY-005 规定 Gate 不得实现为测试 |
| Fable 边界实证 | 探针验证契约面可从 mjs 干净进入：JS 对象字面量可作 F# record 参数、Envelope 序列化/反序列化/fold 全链路可驱动、纯函数直接可调。同时实证 `DateTimeOffset` 裸 `new Date()` 会让 `isExpired` 反向错误且静默 |
| 静态检查工具 | `scripts/ssot-lint.mjs`（条款唯一性、悬空引用、前缀归属、规范/状态分离）；`scripts/shock-audit.mjs`（灭绝表残留 + 单一写入口 + SHOCK 标记）；`scripts/repo-scan.mjs`（共享遍历） |

行数门禁删除后重测：`291 passed / 1 failed / 292 total`。变化可完全解释——删掉 1 个行数测试（293→292），§17 语义门禁不再因行数失败而转绿（3 failed→1 failed）。剩余 1 个失败是 `ReviewRequirementBoundaryTests`，属 Review 语义，由包 D 覆盖。

## 封炉期工装（已完成）

不改生产语义，因此允许在休克开始前运行编译与测试验证其自身正确。

| 项 | 结果 |
|----|------|
| tag `ssot-freeze-0.5.0` | 已打，指向 `0e2e4239` |
| `scripts/architecture-gate.mjs` | 12 个门禁全部迁出测试套件；新增 `fsproj-drift`；Kernel/Domain 的 host-boundary 不再接受文件名豁免 |
| `tests-next/Gates/*.fs` | 已删除，`.fsproj` 条目已移除 |
| `fsproj-drift` 首次运行的产出 | 发现 6 个死文件并删除；其中 5 个是 `c3c35756` 起就未运行的 Prompt Authority 测试，15 个断言登记入包 T |
| `tests-mjs/domain.mjs` | Fable 约定 facade。38 导出，封死三个静默陷阱 |
| `tests-mjs/domain.meta.test.mjs` | facade 契约，20 测试全绿 |
| `tests-mjs/runner.mjs` | 陈旧产物 fail closed + 每测试 1000ms 硬超时 + 300s 套件上限 |
| `package.json` | 新增 `gate:static` / `gate:shock` / `test:mjs` / `test:unit` / `test:harness`；删除 `test:e2e:p0:parallel`、`test:e2e:p0:full`、`test:release:full`、`test:full` 四个重复别名 |

### facade 封死的三个静默陷阱

三者共同点：不抛异常、不报类型错误，只是答案错。

| 陷阱 | 后果 | 出口 |
|------|------|------|
| `new Date(iso)` 无 `offset` 属性 | Fable `compareDates` 走 DateTime 分支加本地时区偏移；`Deadline.isExpired` 对未到期的 deadline 返回 true | `utcOffset()` / `clockAt()` |
| JS 数组的 `tail` 是 `undefined` | `FSharpList__get_IsEmpty` 判其为空，`List.fold` 直接返回种子；投影全空而断言全过 | `toList()`，`fold.apply` 自动转换并拒绝单值 |
| union tag 是位置序数 | 中间插入新 case 后按序数构造会静默造出另一个事实 | `fact(caseName, payload)`，名字从 `cases()` 解析，未知即抛错 |

实测第二项：`List.fold((a,x)=>a+x, 0, [1,2,3])` 返回 `0`；换成 `ofArray` 返回 `6`。

### 陈旧产物门禁实测

```text
build/next 落后 1465s → runner 拒绝运行，指出最新源与最新产物
npm run build 之后    → runner: build is current (166 sources, 164 artifacts)
```

166 源 = 165 `.fs` + 1 `.fsproj`；`AssemblyInfo.fs` 不产出 JS，故 164 产物。差额可完全解释。

## 封炉期剩余

无。休克期可以开始，入口是包 0（Identity）。

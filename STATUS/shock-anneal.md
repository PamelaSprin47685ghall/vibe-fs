# STATUS/shock-anneal — 休克-退火迁移总账

分支 `refactor/ssot-shock-anneal`。封炉基线 `STATUS/evidence/pre-shock/`。

## 阶段

| 期 | 名称 | 机器反馈 | 当前 |
|----|------|---------|------|
| 0 | 封炉：冻结 SSOT、基线、迁移地图、验证层工装 | 静态检查 + 最后一次完整编译测试 | 完成 |
| 1 | 休克一：领域内核与持久事实（包 0） | 关闭 | 未开始 |
| 2 | 休克二：生产代码全部调用链（包 A–H） | 关闭 | 未开始 |
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

### 包 A：PromptDispatcher

| 项 | 值 |
|----|----|
| 条款 | PROMPT-001 PROMPT-005 PROMPT-006 PROMPT-007 PROMPT-009 PROMPT-011 |
| 目标模块 | `PromptDispatcher.fs` `PromptDispatcherSend.fs` `PromptIngress.fs` `PromptMetadataCodec.fs` |
| 旧入口 | `PluginPromptAccepted`（单一 accepted 事实，混淆 receipt 与物理落地）；`OpenCodePort` 裸 `prompt_async` 多点调用 |
| 新入口 | 四事实 `PluginPromptClaimed` / `PluginPromptSubmitted` / `PluginPromptPhysicalAccepted` / `PluginPromptAbandoned`；单一 sender |
| 必须删除 | `PluginPromptAccepted`；`accepted-*` 参与 Authority 的所有路径；生产模块中除唯一 Host adapter 外的 `prompt_async` |
| 静态验收 | `prompt_async` 在 `next/` 只出现在唯一 Host adapter |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

### 包 B：AttemptExecutionProfile

| 项 | 值 |
|----|----|
| 条款 | PROMPT-008 AGENT-001 AGENT-007 AGENT-010 |
| 目标模块 | `Domain/PromptAuthority.fs` `Session/AgentRoleIdentity.fs` `ToolRuntimeScope.fs` `ToolRegistry.fs` |
| 旧入口 | 各模块自行从 Agent 字符串解析 Role；`sessionRoles: Dictionary<string,string>`；`RoleFor context` |
| 新入口 | 唯一 `buildAttemptExecutionProfile`，所有模块接收 profile |
| 必须删除 | 任何在 profile 构造之外解析 `fast-`/`deep-` 前缀得出 Role 的代码 |
| 允许出现 Agent 字符串 | 配置解析、Authority Root 创建、profile 构造、Host 发送边界 |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

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
| 目标模块 | `Session/ChildDispatch.fs` `Session/ChildRun*.fs` `Session/ForkRuntime.fs` `JoinTool.fs` `ListTool.fs` `Process/Deadline.fs` |
| 旧入口 | `AgentLinked` / `AgentForked` / `AgentUnlinked` 三事实无 completed/retired 区分；`ChildDispatch.tryCancel` 占位 |
| 新入口 | `HandleLinked` / `HandleCompleted` / `HandleRetired`；active / completed-awaiting-join / retired 三态分离 |
| 必须删除 | retired handle 回退成 Agent 名称重新 fork 的路径 |
| 必须新增 | join 消费后写 tombstone；真实单 child cancel；parent abort 逐项取消；process 管理员 hard limit |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

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
| 裸 `prompt_async` 调用 | 唯一 Host adapter sender | PROMPT-005 | 5 | 2 | 12 | next 允许 1 |
| `PluginPromptAccepted` | Submitted + PhysicalAccepted 两事实 | PROMPT-005 | 7 | 5 | 0 | 0 |
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
| `GuardPromptAccepted` | Dispatcher 四事实 | PROMPT-005 | 6 | 12 | 1 | 0 |
| `InteractionRepairClaimed` | Dispatcher `Claimed` + Origin | PROMPT-005 | 5 | 0 | 0 | 0 |
| `HumanPromptAccepted` | `AuthorityRootAccepted` | PROMPT-004 | 5 | 0 | 0 | 0 |
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

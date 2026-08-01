# STATUS/shock-anneal — 休克-退火迁移总账

分支 `refactor/ssot-shock-anneal`。封炉基线 `STATUS/evidence/pre-shock/`。

## 阶段

| 期 | 名称 | 机器反馈 | 当前 |
|----|------|---------|------|
| 0 | 封炉：冻结 SSOT、基线、迁移地图、验证层工装 | 静态检查 + 最后一次完整编译测试 | 完成 |
| 1 | 休克一：领域内核与持久事实（包 0） | 关闭 | 完成（包 0a–0e） |
| 2 | 休克二：生产代码全部调用链（包 A–H） | 关闭 | 完成 |
| 3 | 清场：删除旧语义与临时标记 | 静态检查 | 完成（清场阶段 `SHOCK-UNMIGRATED` = 0，八个单一写入口 ok(1)；后续 X4 仍有一处 PERSIST-009 阻断） |
| 3.5 | SSOT/12 并入规范（`CTX-` 前缀 + 六个受影响文件） | ssot-lint | 完成（`c95429b3`） |
| 4 | 退火一：恢复生产编译 | dotnet build → npm run build | 完成（Build succeeded；Fable 157 产物新鲜） |
| 5 | 失败驱动上下文恢复（包 X，X0–X9） | 编译 + 第 0–3 层 | X0–X9 的删除与领域实现完成；X-wire 探针链已接线（`SpikePlugin.transform → XWire.applyTransform`，`HostSignalBootstrap.onTurn → ArmRecovery + reconcileAttempt`，提交 `c6ac0eb1…5ff3c53a`）；BlogSquash 生产链已接线（d5c49125：`AppendSquash` 唯一构造点 + armed 槽触发 + SHOCK-UNMIGRATED[CTX-006] 清零），剩余缺口为第 1 层测试与 K8f 端到端剧本，见「包 K8f 摸底」 |
| 6 | 休克三 + 退火二：按条款写 `tests-mjs`，删除 `tests-next`（包 T） | 关闭 → test:mjs | 完成（T-2…T-5e；386 测试三时区全绿，证据 `evidence/post-anneal2/`） |
| 6.5 | 剧本森林重建（包 K） | 载入期校验 + 森林自检 | 核心静态森林已落地；K9 仅剩物理删除：canary 已全部走 `ScenarioRuntime`（14 条经 `canary-driver`，2 条直接 `attachScenario`），`strict-mock-forest/matches` 仅被 provider 内部旧 expect 路径引用。K8f 另因 BlogSquash 生产链未接线阻断，见「包 K8f 摸底」 |
| 6.6 | 因果推进门禁重建（包 W） | gate-testkit + test:mjs | W1–W7 完成（W7 为 13 项 VERIFY-004 禁止退化清单的双向覆盖与注册完整性门禁；休克期未运行编译或测试） |
| 6.7 | 运行时合成文本 TOML 记法（包 N，SSOT/13 → ARCH-010） | gate:static + canary | N0–N5b 完成；N6 与退火三待办。canary 仍有行为债，X-wire 仍是 K8f 前置 |
| 7 | 退火三：恢复 Host / E2E / Release | gate-testkit → canary → P0×3 → release | 未开始 |

阶段 3.5 与退火一并行：SSOT/12 只改规范文件，不产生编译依赖，而它规定的三个新事实与 `ProviderRequestKind` 必须在写测试前定稿。

包 X 先于包 T，这是顺序上最关键的一条。 原计划把包 X 排在包 T 之后，那是错的：包 T 要为 COMPANION-001…013 与 PROMPT-008 写第 1–3 层测试，而这些条款的实现正是包 X 的产出。先写测试就只能对着旧语义写——旧的角色白名单 eligibility、旧的 JSON delta、旧的主动 PrefixEpoch 更新——然后包 X 落地时同一批测试要整体重写一遍。两次编写之间没有任何信息增益，只有一次把旧语义固化成断言的机会。

包 X 依赖退火一而非退火二：它新增大量类型与 fold 校验，需要编译反馈，但不需要既有 mjs 套件先全绿。`domain.mjs` facade 必须先能加载（它是所有 mjs 测试的唯一入口），这就是 facade 修复排在包 X 之前的原因。

包 X 的第 1–3 层测试随各子步骤同时写，不推迟到包 T。 包 T 因此收缩为「CTX/COMPANION 以外的条款测试 + 删除 `tests-next`」。

包 K 排在包 T 之后、退火三之前：剧本的 lane 划分与 step 序列反映迁移后的生产行为，先重写会锁定旧语义；而它必须早于任何 canary 运行，因为旧剧本无法匹配新语义的请求。X-A 至 X-D 四条与包 K 的 22 条一起手工写成 TOML。

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

登记为待办的规范文字统一，不属 SSOT 例外协议（无语义矛盾，仅命名冗余）。已完成：`SSOT/03.md` 与 `SSOT/04.md` 共 4 处改为 `ProviderRunIdentity`，全仓库该概念只剩一个名字。

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
| 新入口 | 无 eligibility 判定。Companion 由 `ManagedSessionKind` 决定（HOST-008），每个 Work Session 恰好一个叶子 Y |
| 必须删除 | `shouldCreateCompanion(agent)` 及一切角色白名单；三次 busy skip 计数；child background 使用 FrozenB 的路径 |
| 必须修正 | 全部工作角色开启 Companion（含 Inspector、Browser、Executor）；ARecord 按 ProviderRun 分段；child background 用最新 durable LatestB |
| 生产 | UNTOUCHED |
| 测试 | UNTOUCHED |

本包在阶段 3.5 被 SSOT/12 修订：原「六角色开启 Companion」的白名单整体作废，改为 Session 种类不变量。实现时不得保留任何以 CanonicalRole 为输入的 Companion 判定函数——那正是被删除的语义。

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

child session id 由 Host 签发。从 handle id 派生会造出一个此后每次操作都静默空转的身份，因此不能就地补。

决定（包 F-1）：`HandleLinked` 增加 `ChildSessionId: SessionId`，不走 SSOT 例外协议。

EXEC-009 只列出三个裸事实名，没有给出任何字段表：

```fsharp
HandleLinked
HandleCompleted
HandleRetired
```

条款正文规定的是行为——「HandleId 创建一次并持久化，重启恢复同一个 ID」。要满足「重启恢复同一个 ID」就必须能把该 ID 重新绑回它代表的 child session，否则恢复出的 handle 指向不了任何东西。加这个字段是实现该句的必要条件，不是降低条款，因此没有可供例外协议修改的对象。`STATUS/blocker-EXEC-009.md` 不需要创建，例外次数保持 0。

反向验证：另一条路（EXEC-009 明确恢复期以别的方式重新解析 children）要求从 Host transcript 反推 handle↔session 对应关系，而 handle id 是插件自己签发的、Host 侧不存在，反推无据可依。该路不成立。

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

#### 包 G-1 清点结果

包 0b 已删除全部九个旧 `Orchestrator*` 事实，包 0c 已把投影换成 `JobProgress` 判别联合并写好 `recoveryAction`。因此新协议的读侧已完备，写侧全部悬空——`Orchestrator/OrchestratorProgram.fs` 与 `Orchestrator.fs` 仍在构造九个已不存在的事实。

五处结构性偏离，按迁移顺序：

1. Integration Gate 跨 review 持有（ORCH-005 熔断项）。`OrchestratorProgram.program:246` 在进入 `publishLoop` 前 `use! _gate = IntegrationGate.acquire`，而 `publishLoop` 内含 `rebase` 与 `postReview`——即 LLM review 与冲突修复全程持锁。条款要求 lock 只保护 ref mutation，多个 Job 可并行 rebase 与 review。锁必须下移到 `publish` 内部、`FfMerge` 前后。

2. `OrchestratorRecovery.currentJob` 是 stage-like 重建。它在投影缺失时用 `Option.defaultValue` 造一个五字段全 `None` 的记录，`preReview` / `postReview` / `rebase` / `publish` 再按「哪个字段被设过」推断该做什么。这正是 ORCH-006 禁止的形状，且 0c 之后该记录类型已不存在。整个 `Orchestrator.Recovery.fs`（29 行，两个函数）应删除——`recoveryAction` 已经用一次匹配代替了对五个字段排优先级。

3. `CandidateId` 已灭绝但九处仍在构造它。旧事实用 `candidate-<managerId>` 合成 id 作为 barrier 身份；ORCH-006 改为 barrier 事实携带 `ReviewBarrierId` 并指向已持久化的 `ConfirmedReviewWitness`。`OrchestratorRecovery.candidateId` 一并删除。

4. `ManagerId: string` vs `ManagerJobId`。运行期全链路（`ManagerJob.ManagerId`、`OrchestratorHandle`、`ManagerPort` 三个函数、`VerdictMailbox`、`OrchestratorVerdict` 五个 case）用裸 string，而新事实要求 `ManagerJobId` + `ManagerSessionId` + `WorktreeIdentity` 三个 typed 身份。`OrchestratorHost.runReviewerOnce` 已经在用 `sprintf "%s-reviewer" managerId` 从 manager id 派生 reviewer 的 agent id，这是把 id 当字符串拼装的既有入口。

5. `ManagerJobCreated` 六字段无处可取。`Orchestrator.forkManagerCore` 目前只有 `managerId` / `worktreePath` / `branch` / `prompt`；新事实需要 `ManagerSessionId`（Host 签发，需从 fork 结果取）、`ManagerAgent`（PROMPT-008，不可从 role 重建）、`WorktreeIdentity`（稳定身份，非可变路径）、`TargetBranchFrozen`（ORCH-008 的 `symbolic-ref` 冻结值）。`GitPort` 无 symbolic-ref 冻结动作，需新增。

同时确认的 REVIEW-008 次序矛盾（包 D 遗留的两处 `SHOCK-UNMIGRATED`）：`emitReviewBarrier` 在 `runReviewerOnce` fork reviewer 之前调用，此时 reviewer session 尚不存在。本包按「barrier 从 reviewer fork 路径发出」解决——一个 barrier 对应一次 reviewer fork，新 reviewer session 的 guard 起始为空，REVIEW-008 的「全新双 PERFECT」因此自动成立。

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

#### 包 T-2 实测结果

13 个必须重建的断言中，Prompt 侧全部落地在 `tests-mjs/Prompt/authority.test.mjs`（21 个测试）。
Journal 侧另开两个文件：`tests-mjs/Journal/envelope.test.mjs`（第 1 层，纯编解码与
fold）与 `tests-mjs/Journal/boot.test.mjs`（第 2 层，真实文件）。

`test:mjs` 228 → 263。三个时区（UTC / Asia-Shanghai / America-New_York）下均全绿。

不重建的 2 个断言：它们断言 `accepted-*` 运输回执可以承载 authority，PROMPT-005
现在明确禁止。替代物是同一情形的反向断言
（`PROMPT_001_a_transport_receipt_can_never_become_an_authority_root`）。

##### 三处只有真测试才能发现的缺陷

`domain.mjs` 的 `claimScopeDigest` facade 签名多了个 `sha256` 形参。 生产签名不接受
它——该函数返回 `\u001f` 连接的明文，不做哈希。旧 facade 会把 `sessionId` 当
`logicalRunId` 传进去并抛 `TypeError`。此前零调用点，所以 facade 写错了半个包都没人知道。
这正是 facade 层需要元测试的理由：facade 本身也是代码。

`Envelope.serialize` 渲染读者的本地时区偏移（PERSIST-001）。 `Encode.Auto` 直接编码
`DateTimeOffset`，`Decode.Auto` 解码时挂上读者本地 offset。写入侧永远传 `UtcNow` 所以
新写的行没问题，但「读一行再写回」在 `TZ=Asia/Shanghai` 上把同一时刻渲染成 `+08:00`。
两台不同时区的机器对同一份历史产出不同字节。修正是编码前 `ToOffset TimeSpan.Zero`。

不用 `ToUniversalTime()`：Fable 的 `toUniversalTime` 让产出值的 `offset` 字段为
`undefined`，编码器于是靠巧合渲染出裸 `Z` 而非按契约渲染。这条也解释了为什么该断言
必须比对完整行文本——只断言「能解回来」会全绿。

Journal 权限位从未设定（PERSIST-006）。 `mkdirSync` 只传 `recursive`，`openSync` 只传
flags，实际权限是 umask 的结果：默认 umask 022 下 755/644，条款要求 700/600。修正是
创建时传 mode 而非事后 chmod——mkdir 与 chmod 之间目录是全局可读的，而 journal 行里有
session id、Git tree hash 与 prompt payload 摘要。

测试自身的陷阱一并记账。 `mkdtemp` 本身就产出 0700，所以在 `mkdtemp` 结果上直接断言会
在生产完全不设 mode 的情况下通过。facade 因此把 journal 目录放在临时目录内的子路径，
由 `JournalWriter.create` 自己创建；`PERSIST_006_permissions_hold_regardless_of_the_process_umask`
额外把 umask 设为 `000` 再断言。

##### 第 2 层进入 tests-mjs 的边界

`JournalWriter` 与 `Boot` 是唯一触碰真实文件系统的领域模块，因此也是唯一能写第 2 层
资源契约测试的位置。PERSIST-004 问的是「半写的文件在启动时怎么办」，用内存替身断言
只会断言替身。

facade 出口 `journalStore()` 独占一个临时目录并负责清理（VERIFY-004）。`writeRaw` 是
表达「损坏 journal」的唯一手段——损坏 fixture 必须只在测试意图的那一点上与健康文件
不同，所以其余部分仍由生产的 `Envelope.serialize` 生成。

#### 包 T-5 前置：`tests-next` 清点与一处不可直迁的覆盖

删除前逐项清点。`tests-next` 现状：75 个 `.fs`、11654 行、234 个 `[<Fact>]`/`[<Theory>]`，
且自 X9 起已无法编译（`test:compile` 报 200 个 error，Fable 编译失败）。它已经不产生
任何反馈，所以「删除」不损失现有判据——但其中一部分覆盖尚未在 mjs 侧重建。

按目录清点：

| 目录 | 文件 | 测试 | 处置 |
|------|------|------|------|
| `Flow/` | 2 | 21 | 部分不可直迁，见下 |
| `Domain/` | 1 | 3 | 已由 `Fallback/cursor.test.mjs` 覆盖 |
| `Journal/` | 10 | 37 | 已由 `Journal/{envelope,boot}.test.mjs` 覆盖 |
| `Process/` | 5 | 26 | Deadline 已覆盖；PTY 生命周期属第 3 层，归退火三 |
| `Session/` | 16 | 56 | Companion/Fallback/Handle 已覆盖；fork runtime 属第 3 层 |
| `OpenCode/` | 26 | 82 | 全部依赖 Host 替身，属第 3 层，归包 K/W 与退火三 |
| `MockOpenCode/` | 7 | 1 | Host 替身工装，由包 K 的剧本森林替换 |
| `Integration/` | 5 | 7 | 第 3–4 层，归退火三 |
| `GuideContract/` | 1 | 1 | 已由 `tests-mjs/guide-contract.test.mjs` 替换 |

`Flow/` 的 21 个测试是唯一需要单独裁决的一组。 实测生产四个 flow 程序的控制流用量：

```text
let! / do!   18 处    ← 主体
use!          1 处    OrchestratorProgram.fs:372（worktree 释放）
while         0 处
for           2 处    ProcessRunner，且只遍历 8KiB 定长分块
try           0 处    （11 处命中全是注释文字）
```

由此分三类：

`mapBounded` 的 3 个测试必须重建，且可以重建。 它是生产在用的并发原语，
`Parallel_mapBounded` 是普通导出，从 mjs 可直接调用。`guide-contract.test.mjs` 已断言
它存在且不存在无界的 `Parallel.map*` 兄弟；行为测试（保序、异常传播、空输入）待补。

`use!` 的 4 个 disposal 测试必须重建。 它们锁的是「body 失败且 dispose 也失败时，
body 的异常胜出」，而唯一的 `use!` 调用点是 publish 路径上的 worktree 释放——泄漏一个
worktree 会阻塞整个仓库。这是真实载荷，不是 builder 能力展示。

其余约 14 个测试（`TryFinally`、`TryWith`、`While` 短路、10000 步栈安全）覆盖的是
生产 flow 从不使用的 builder 能力。 `while` 在四个程序里零处，`for` 只遍历定长分块，
故「10000 步不栈溢出」描述的是生产到不了的场景。这些不重建，理由记档而非静默丢弃。

不可直迁的原因：builder 方法名带 Fable 签名哈希。

```text
FlowBuilder$2__Bind_Z40B88B2D    FlowBuilder$2__Using_Z25CD278
FlowBuilder$2__TryFinally_74403B28   FlowBuilder$2__While_31AC1067
```

后缀由签名派生，正是 VERIFY-008 要求关在 `domain.mjs` 里的那种 Fable 约定。但把它们
关进 facade 也换不来什么：`use!` 的语义由编译器把 `companion { use! x = ... }` desugar
成 `Using(...)` 得到，而从 mjs 只能手工串 `Using` 调用——那样断言的是我拼的链，不是
编译器的 desugaring。测试会绿，而真正要保护的翻译过程未被覆盖。

因此 disposal 契约的重建路径不是第 1 层 mjs，而是让生产的 `use!` 调用点在第 3 层轨迹里
真的走一遍失败释放。登记为退火三的必须项，与 `OpenCode/` 那 82 个测试同批。

包 T-5 的执行顺序（下一步）：

```text
T-5a  本节（清点与裁决）                                    完成
T-5b  architecture-gate: TESTS_ROOT → tests-mjs；删 fsproj-drift 的测试项；
      GUIDE_CONTRACT_PATH → tests-mjs/guide-contract.test.mjs；
      RUNNER_CANDIDATES 去掉 tests-next/runner.js
T-5c  git rm tests-next 全部；package.json 删 test:compile / test:next，
      test:unit 收缩为 test:mjs；清理 build/tests-next
T-5d  shock-audit: tests 根 tests-next → tests-mjs（否则旧符号计数永远为 0，
      看起来像已灭绝）
T-5e  mapBounded 三个行为测试补进 tests-mjs
```

T-5b 必须先于 T-5c：门禁当前对 `tests-next/GuideContract/Signatures.fs` 的缺失是 fail，
先删文件会让门禁红着，而红门禁下无法验证删除本身是否干净。

#### 包 T-5 实测结果

`tests-next/` 已删：81 个文件、11654 行、234 个断言。`test:unit` 收缩为 `test:mjs`。
`test:mjs` 374 测试，在 UTC / Asia-Shanghai / America-New_York 三个时区下全绿；
`gate:static` 全绿（174 生产 + 23 测试文件）。

门禁侧的四处改动，每处都有一个「删完之后会静默失效」的理由：

`TESTS_ROOT` → `tests-mjs`，并新增 `TEST_EXTENSIONS = ['.mjs']`。 测试树与生产树
现在用不同的扩展名列表扫描。沿用 `SOURCE_EXTENSIONS`（`.fs`/`.fsproj`）会让
`testFiles` 变成空数组，而所有跨 `allFiles` 的门禁（禁用词汇、`src` 引用、依赖方向）
都会继续报 OK——扫描面静默缩小，门禁不会说任何话。

新增测试侧 scanner witness。 原有 witness 只覆盖生产四个文件。补上
`tests-mjs/{runner.mjs,domain.mjs,guide-contract.test.mjs}` 与 `testFiles.length < 5`
下限，是因为上一条的失效模式恰恰是「扫描返回空」。负向验证：临时移走
`guide-contract.test.mjs` 与 `runner.mjs`，门禁各报 2 项违规（`dsl-program` /
`test-runner` 加 `scanner`）。

`fsproj-drift` 去掉测试项。 测试树没有项目文件，所以「声明与磁盘不一致」这个违规类
变成不可表达而非未检查。留着空跑的检查项会让人以为它还在保护什么——而它当初存在的
理由（`c3c35756` 把五个测试文件从 `.fsproj` 移除、它们作为死代码继续「通过」了数月）
在 mjs 侧由 `node:test` 的文件发现天然消解。

测试项目引用门禁整体删除。 同上：没有 `.fsproj` 可以引用任何东西。通用形态仍在——
`referencesLegacySrc` 跑在 `allFiles` 上，含每个 `.mjs` 测试，所以测试 import `../src`
照样红。

`shock-audit` 的 tests 作用域改指 `tests-mjs` + `.mjs`。 这是本包最危险的一处：
留在已删除的 `tests-next` 上，该列会对每个符号返回 0——对一张灭绝审计表来说，
「查不到」和「已灭绝」显示成同一个值是最坏的失效方向。

连带发现一处豁免名单需要扩容。 改指之后十个旧事实名在 tests 列报出残留，全部来自
`tests-mjs/Journal/envelope.test.mjs`——那是 PERSIST-005 的测试，断言每个退役事实名
仍产生迁移提示而非晦涩的 union 错误。断言本身就是那个字符串字面量，所以
`LEGACY_NAME_SENTINEL` 从单个文件扩为 `LEGACY_NAME_SENTINELS` 两项（codec 与它的
测试）。只豁免 codec 会让这个测试无法存在。

其余跨仓引用一并清理：`README.md` 布局与构建段、`SSOT/10.md` 的 VERIFY-004 实现清单、
`testkit/reaper.mjs` 的孤儿进程标记、`next/package.json` 的三个脚本、`build/tests-next`
产物目录（1.4M，git 已忽略）。

`conformance.md` VERIFY 段四行同步更正，成因与 Fallback / Review 两段相同。

### 包 W：因果推进门禁重建

| 项 | 值 |
|----|----|
| 条款 | VERIFY-004 VERIFY-002 |
| 目标模块 | `testkit/opencode/watchdog.js`、`time-budget.js`（W1 建立，取代已删的 `watchdog-constants.js`）、`stability-checker.js`、`scripts/run-canary-staggered.mjs`、`scripts/budget-gate.mjs`（W1 建立）、`tests-mjs/runner.mjs`（原写 `tests-next/runner.js`，该目录已随 T-5 删除） |
| 原理状态 | 保留。没有进展就杀死、watchdog 按语义事件投喂、因果 bark 交错启动，三者是既有设计中最有价值的部分，必须继承发扬 |
| 实现状态 | 不合格。见下方实测缺陷 |
| 重建方式 | 第一性原理瀑布流，与包 K 同期。禁止在现有实现上逐点修补 |
| 生产 | 不涉及 |

原理与实现必须分开评价。VERIFY-004 的文字是判据，现有代码不因先存在而获得权威。

#### 实测缺陷

| 缺陷 | 位置 | 后果 |
|------|------|------|
| 一个测试超时导致整个套件停摆 | `tests-mjs/runner.mjs:111` 把 `PER_TEST_TIMEOUT_MS` 交给 `node:test` 的 `run({ timeout })` | 实测：`run({timeout})` 只判决不中止。挂死且持句柄的测试在超时点被判失败，但源流永不发 `end`，唯一终止者是 `SUITE_TIMEOUT_MS = 300000`。同时违反「禁止一个测试超时导致整个套件停摆」与「运行器必须有测试覆盖这一点」，且使 `runner.mjs:9-10` 的注释成为假声明 |
| watchdog 被墙钟无条件续期 | `canary-driver.mjs:126` 的 `waitFact` 轮询循环 | 每 500ms 切片无条件 `advance({blocking:true})`，与 fact 计数是否推进无关，最长可把一个错误的 watchdog 续到 120s。正是 VERIFY-004「一个反复重连的 SSE 读者能永久续期一个错误的 watchdog」点名的形态。宪章缺陷表原未记录，归入 W6 |
| 数量常量与清单各自维护 | `run-canary-staggered.mjs:36`：`CANARY_COUNT = 17`，`CANARY_TESTS` 实测 16 条 | 已经漂移两次：宪章原记「实际 19 条」，包 K 删三条 canary 后成 16。唯一用途是 `:203` 的日志，而该行同时打印两侧，字面输出 `Concurrency: 16 / 16 (expected ~17)`。漂移方向都变了，证明它必须派生而非校正 |
| 静态门禁指向不存在的目录 | `stability-checker.js:30` 判 `e2e/opencode/specs/`，该目录不存在 | `containsTool` 检查在全部 28 个 `runStaticGate([__filename])` 调用点恒不可达，伪门禁。同文件的 `fixed-sleep` 检查（`:43-63`）是活的 |
| ~~超时值散落为字面量~~ | ~~`run-canary-staggered.mjs:35`（90000 兜底）、`:111`（10000 就绪窗口，且同值重复进 `:244-245` 两处用户可见字符串）、`watchdog.js:84`（3000 诊断竞速）、`scenario-parallel.js:162`（3000 host.stop 竞速）、`canary-driver.mjs:127`（120000 waitFact 总窗）~~ | W1 已闭合。`budget-gate` 对未迁移树报 54 行，即宪章记的 6 处加 15 处同类（`process-host*` 五处、`scenario-http` 三处、`scenario-runner` 三处、`stability-checker` 三处、`event-probe-awaits` 两处、`spawn-ledger` 的 `30 * 60 * 1000` 等），另有 26 个常量当时无调用点。全部值逐字节移入 `time-budget.js`，`SUITE_TIMEOUT_MS` 与 `PER_TEST_TIMEOUT_MS` 保名保值待 W4 |
| 启动阶段只有 wall-clock 覆盖 | canary 进程拉起（`run-canary-staggered.mjs:86`）到 `[setupScenario] ready` 之间只有 `:111` 的 10s 硬窗口 | 存在一段无因果判据的时间窗，违反 VERIFY-004「覆盖必须无缝」。无任何递进就绪证据（端口已绑、插件已载、provider 已起）投喂任何东西 |
| ~~声明了断言心跳但未接线~~ | ~~`tests-next/Assert.fs:13`~~ | 已随 `tests-next/` 在 T-5（`952be9e3`）整体删除而灭绝。`resetHeartbeat` 与 `__resetAssertionTimeout` 全仓非散文引用为 0。故禁止退化清单第 7 条当前无任何活代码违反，W4 是全新实现而非修复 |

#### 重建顺序

```text
W1  集中所有时间常量，建立单一来源；门禁禁止字面量超时                       已完成
W2  canary 清单单一事实来源，数量从清单派生                                   已完成
W3  删除伪门禁，静态检查路径判据与实际目录对齐                               已完成
W4  重建单测运行器的因果推进门禁：以「距上次判决的静默时长」为主判据，
    verdict 投喂、stdout/stderr 不续期、静默即 SIGKILL 子进程组并转储诊断。
    并有测试证明：(a) 未接线或错误接线（噪声续期）会红；(b) 超时测试被遗忘
    而不污染下一个测试的归因；(c) 干净结束不等满静默窗口                     已完成
W5  启动阶段因果判据，消除只有 wall-clock 的时间窗                           已完成
W6  watchdog 重写：语义投喂、背景不续期、诊断完整、不持有事件循环           已完成
W7  gate-testkit 增加门禁自检：每条「禁止退化清单」都有对应失败测试         已完成
```

W1 落地记要。26 个常量进 `testkit/opencode/time-budget.js`，`scripts/budget-gate.mjs`
四条规则进 `gate:static`（现 5 条门禁），`gate-budget-cases.mjs` 14 条自检进 `test:harness`
（183 → 197）。门禁先红后绿：对未迁移树报 54 行，逐字转录在会话记事本。

判据取「量级即语义线」：轮询切片必须比它所受的界更快（`canary-driver` 500ms 在 2000ms
静默预算下、监听轮询 50ms、socket 重试 30ms），故合法切片按构造 < 1000ms；≥ 1000ms 者
本身即预算，必须进表。这避开了「该字面量是否受某个导入界约束」这类无法静态判定的问题。
门禁不设豁免注释——本包要替换的四个伪门禁都是从豁免通道腐烂的。

宪章第 4 条原设想的字符串反重复判据（表值或表值除以 1000 的数字串）在真实树上产出 935
条命中：1000/1000 = 1，而 harness 里几乎每个字符串都含孤立的 `1`（`'http://127.0.0.1:9999/v1'`、
`'$1'`、`'2025-01-01'`）。改为要求带单位（`10s` / `3000ms` / `5 minutes`）；残留缺口
（无单位裸整数重述）写在门禁头部注释里，不隐藏。

顺带发现并修正 `architecture-gate.mjs` 的 test-runner 判据是 `/\b\d{3,5}\b/`——「文件里有
3-5 位数字」。集中化之后它会在正确的树上变红，而在任何提到 1024 的文件上放行：它匹配的是
数字存在，不是界存在。改为要求 runner 命名 `PER_TEST_TIMEOUT_MS`。这是本仓库第五个
「判据与意图不符」的实例。

`SUITE_TIMEOUT_MS` 与 `PER_TEST_TIMEOUT_MS` 保名保值。W4 改变前者含义时才改名——此刻改名
等于宣称一个尚未发生的修复。值钉断言诚实标注为 DETECTION 而非 PREVENTION：调大数字永远是
合法代码，静态不可阻止；能安排的只是「调大必然可见」。

W5 落地记要。启动窗口拆成 6 级因果阶梯（`testkit/opencode/readiness.js`），每级独立预算，
到达即重新计时；总启动时长因此无界，被界住的是静默。`CANARY_READY_MS` 保留为总兜底，即
条款允许 wall-clock 值承担的角色。阶梯读子进程本来就在打的计时行，不新增门禁专用证据。

两处实测缺陷，各有红证：

```text
阶段顺序押反    第一版按「先备工作区、再起 provider」的自然读法排，而
                scenario-parallel.js 实际是 provider.start@88 先于 prepareWorkspace@94。
                observe 只在下一个期待标记上前进，押反即停在 1/6，每条 canary 都在
                阶段预算上失败而宿主完好。门禁因此从两个源文件数出各标记的打印行号
                再比顺序，不拿顺序与自身副本对拍。倒置实测 3 红
喂 chunk 而非   管道读边界落在哪里由缓冲区决定，被切断的标记在两次读里都不出现，
累积缓冲        症状同样是健康启动耗尽阶段预算。observe 单调，重放整段缓冲无代价。
                断开实测 1 红
```

`CANARY_MAX_PARALLEL` 从 `time-budget.js` 迁至 `canary-manifest.js`：它是并发计数不是时长。
W5 先放进了预算表，被预算表自己的整表钉死判红——契约为「全部 wall-clock 兜底的单一来源」
的表若能装并发计数，该契约就退化成「W5 需要的常量」。

W4 与 W7 是本包的重点：现在的门禁声明了自己有能力，但没有测试证明能力真实存在。`Assert.fs` 的空 `resetHeartbeat` 能存在这么久，正是因为没有任何测试断言心跳被投喂。

#### 六项裁决（实施前定案）

原 W4 措辞已按第一条改写。六项都不动 SSOT：VERIFY-004 的文字正确，要改的是本总账。

##### 一：W4 不重建断言级心跳，改为判决级

条款是条件句而非强制：「若运行器声称『断言投喂心跳』，则该心跳必须真实连通并有测试证明。」SSOT 从未要求单测运行器必须有断言级心跳。而那个「声明了但未接线」的心跳已随 `tests-next/` 删除而灭绝（见缺陷表末行）。

断言级投喂是语义错误，不只是难实现。条款自带判据「该事件是否证明被测因果链前进了一步」，并排除「任何『有字节在动』的证据」。纯 fold 的因果链是一次函数调用，断言在它返回之后执行，是已完成计算的下游观测，不是未完成调用的中间检查点。更糟：`for (c of cases) assert(f(c)); await neverResolves()` 会续期 300 次，断言写在非终止循环内则永久续期——正是条款点名的「反复重连的 SSE 读者」形态。选它会新增一条退化项，而非清除一条。

正确的因果信号在上一层：每个完成的 verdict 证明套件因果链前进一步，由运行时产生而非作者产生，挂死的测试无法伪造。这也让 `blocking` 区分在单测运行器里第一次有真实工作可做——`test:pass`/`test:fail`/`test:complete` 续期，`test:stdout`/`test:stderr`/`test:diagnostic` 只记录。

实现为两层：子进程保持 `run({ files, timeout, concurrency })` 语义不变（保住「超时即遗忘」与进程内并行），每个事件 `process.send` 一条；父进程复用 `testkit/opencode/watchdog.js` 的 `Watchdog`（不重造），在 spawn 之前武装（顺带覆盖单测侧的启动窗口），静默则转储诊断并 SIGKILL 子进程组。

已否决的三条：进程级隔离（每文件一进程）付 40 次 `build/next` 模块加载与 40 次 spawn 且失去 `concurrency`，换来的抢占一个哨兵进程已经买到——父进程不被子进程的事件循环阻塞，故 CPU 密集挂死也能杀；断言级投喂见上；仅改名不修停摆，把主违反留给「另行修复」。

`PER_TEST_TIMEOUT_MS` 保名保值 1000：固定单测硬界是「每个测试有独立的硬超时」明确要求的，只有 `runner.mjs:9-10` 的注释在说谎，删注释而非改常量。

##### 二：W2 拥有数量单一来源，修订三的 K8 归属被取代

修订三把 `CANARY_COUNT` 漂移的修复派给 K8 的 `data-driven.manifest.toml`。包 K8 已完成且未创建该清单，把此刻新建的文件归属于一个已完结的包是虚构。

两项义务本可分离却被并在一起：修订三想要的是「数据驱动 canary 运行器」，W2 的条款义务只是「数量从清单派生」。故 W2 履行数量义务；数据驱动运行器登记为取消——8 条 stub canary 仍是 8 个 9 行文件，在没有任何需求要求合并它们之前，多一层清单间接不产生收益。

##### 三：`Promise.all` 不违反 ARCH-009，但仍要有界

`run-canary-staggered.mjs:233` 对全部输入集 `Promise.all`。ARCH-009 的适用域是业务层（`next/**`），harness 工具按适配器内部例外不受此限，故不启动例外协议、不改条款。

但该条款的理由归属于 VERIFY-004，而对 16 个 OpenCode 进程的无界扇出恰好制造 VERIFY-004 禁止用「延长窗口」掩盖的资源竞争。故在 W5 落地时以 `CANARY_MAX_PARALLEL` 有界化，并在此记录适用域判断。

##### 四：manager-tool-contract 的三组 `.execute` 断言不随迁移带走

实测该测试当前是红的，红在第 242 行：`reviewerInspector.agent` 为 `undefined`，期望 `'fast-inspector'`。故 1–236 行是真实拥有的覆盖，238–288 行的三组共 31 条断言在当前树里从未通过过。

带走它们只有两种下场，都被禁止：让套件长红（破坏绿基线），或把断言放宽到承认 `undefined` 正确（正是 `design-script-forest.md:630` 判为「比没有验证装置更危险」的假绿）。故迁移只带 1–236 行，三组连同实测失败一并另记待办，与删除原文件同一提交落地。

###### 待办：三组 `.execute` 断言从未通过（迁移时另记）

原文件 `testkit/opencode/tests/manager-tool-contract.mjs` 已删除，迁移后的绿色部分在
`tests-mjs/Plugin/manager-tool-contract.test.mjs`。下列三组共 31 条断言在删除时点从未通过过，
逐字记录以免覆盖面被误认为存在。

实测失败点（`node --test` 于删除前最后一次运行）：

```text
manager-tool-contract.mjs:242
  actual: undefined   expected: 'fast-inspector'   operator: strictEqual
```

即 `hooks.tool.inspector.execute(...)` 返回的 JSON 里没有 `agent` 字段。断言原文：

```js
const reviewerInspector = JSON.parse(await hooks.tool.inspector.execute(
  { agent: 'fast-inspector', prompts: ['git status'] },
  { sessionID: 'reviewer-contract', agent: 'fast-reviewer' },
));
assert.equal(reviewerInspector.agent, 'fast-inspector');
assert.equal(reviewerInspector.output, 'test output');
```

三组的原始行号与内容：

| 原行号 | 断言组 | 条数 |
|--------|--------|------|
| 238–250 | `inspector` / `coder` 一次性 execute 返回 `agent` 与 `output` | 4 |
| 252–283 | `fork` 未知 agent 报错；`fork`/`join`/`list` 邮箱路径的 agentId/role/tier/fallbackPeer/finalText | 25 |
| 284–288 | journal 落在 Git common directory、工作区不出现 `.wanxiangshu-next` | 2 |

`output` 期望的 `'test output'` 在生产代码里已不存在——`next/Session/Sessions.fs` 的注释
记载先前实现是在伪造该字符串，随后删除。所以这三组要能通过，需要先确定
`inspector`/`coder` 一次性工具的返回契约究竟是什么，属生产行为问题而非测试迁移问题。

不带走的理由：带走只有两种下场，都被禁止——让套件长红（破坏绿基线），或把断言放宽到
承认 `undefined` 正确（正是 `design-script-forest.md:630` 判为「比没有验证装置更危险」的
假绿）。

##### 五：K10 收缩为一条森林级性质加一份在册清点

K10 的四项里三项已实现且已有门禁覆盖：无死边（`scenario-schema.js` `deadEdges`）、索引无冲突（`duplicateDeclarations` + `runtime-key.js` `ambiguousTurn`）、fault 有限（`validateFault` + `conflictingFaults`）。重新实现它们会造出第二事实源——正是 W1 与 W2 要消灭的缺陷。

故 K10 = 设计文档 `:581` 那条真正未实现的森林级性质「同请求序列 → 同内容序列」（跨全部 15 条真实剧本），加一份把其余三项映射到 `file:symbol` 与执行它的门禁用例名的在册清点，任一映射消失即门禁失败。形状仿 `scripts/shock-audit.mjs` 的符号灭绝表，方向取反。

##### 六：K11 第四类变异重新锚定

`loadScripts` 已在 K8c（`d01386dc`）删除，故「重启后本该命中的边消失」不能再对着它写。重新锚定到 `ScenarioRuntime` 的 `bind` + `clearSeals` + `select`：一条静态剧本的边在模拟重启后必须仍然命中——这正是「静态剧本」的定义。

##### 附：reporter 少报只作注释处理

实测同一次运行 `spec` 打印 `ℹ fail 1` 而源流发出 2 次 `test:fail`。`runner.mjs` 自己数源流故退出码正确，但读汇总的人看到错数字。这是上游 bug，不改写也不打补丁 `spec`；由父进程用 IPC 收到的全部 verdict 打印权威汇总，并加注释记录该少报。

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

包 X 新增四条剧本（X-A 至 X-D，见下），一并纳入 K8 的手工重写范围。

#### 包 K 清点与拆解修订（退火二后实测）

设计文档第十二节的 K1–K10 写于封炉期。退火二后逐项实测，四处需要偏离：三处比原计划更彻底，一处原计划缺失。

现状实测：

```text
剧本      22 文件 / 7583 行 / 258 条边
testkit   65 个 .js/.mjs
canary    19 条（run-canary-staggered 的 CANARY_TESTS）
           其中 8 条是 9 行 stub，除文件名外逐字节相同
           其中 4 条是内联轨迹（228/230/406/86 行，共 26 处 expect*）
```

##### 修订一：K1 从「两侧对拍」改为「删除 testkit 那一侧」

设计文档要求 testkit 的规范化函数与生产 VERIFY-007 同一定义并两侧对拍。实测生产侧已具备完整出口：

```text
build/next/Domain/ProviderProjection.js
  toSemantic  renderSemantic  fixtureKey  semanticallyEqual
  sealDigest  toolResultDigest(s)  isAppendOnlyPrefix  CanonicalVersion
```

而 `testkit/opencode/tests/manager-tool-contract.mjs:7` 已经直接 `import ... from '../../../build/next/OpenCode/SpikePlugin.js'`——testkit 引用生产产物是既有模式，不是本包引入的新风险。

保留一处 testkit 私有：`estimatePromptTokens`。它给 mock 的 usage 字段编造 token 数，而 mock 扮演的是 provider，provider 报告 token 用量是真实行为。CTX-001 禁止的是插件观察上下文容量，不是 provider 报告它。此处登记为必须守住的边界：该函数不得出现在 `next/` 任何路径上（当前实测 0 处）。

###### 修订一的修订：直接复用会让门禁变成恒真（实测）

上面那段推理漏了一件事，实测才发现。把 OpenAI wire body 喂给生产的 `Projection.decodeRequest`：

```text
两条完全不同的 user 内容           渲染结果相同
renderWire 输出                    messages 全部 "parts":[]
semanticallyEqual(A, B)            true
```

原因是两种 wire 格式不同，而生产解码器只认其中一种：

```text
Host raw（生产 transform 边界）   parts: [{ type: "text", text: "..." }]
OpenAI HTTP（mock 收到的）        content: "...", tool_calls: [{ id, function }]
```

`decodePart` 按 `type` 字段分派（`text` / `reasoning` / `tool-call` / `tool-result` / `file`）。OpenAI 消息没有 `parts` 数组，于是每条消息解出零个 part，只剩 `Role`。角色序列相同的任意两个请求因此语义相等。

如果 K1 按原计划落地，剧本匹配与前缀封印会同时变成恒真：每条边命中每个角色序列相同的请求。这正是设计文档第十四节判定为最坏结果的那件事——「一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险」。而它不会以失败的形式出现，canary 会全绿。

正确的切分是第三种，两个原方案都不对：

```text
原设计     两套完整规范化 + 对拍          比较逻辑可漂移
修订一     删掉 testkit 那套，直接复用     格式不同，门禁恒真
修订一'    testkit 只保留 wire 解码器，
           所有提问复用生产投影            ← 采用
```

即：`OpenAI wire body → ProviderWireProjection` 是一个 adapter，属 VERIFY-005 明确允许 testkit 拥有的那类代码（外来 wire 格式的解码）；而 `toSemantic` / `renderSemantic` / `semanticallyEqual` / `isAppendOnlyPrefix` / `sealDigest` 一律来自生产。类型与提问单一来源，解码器按 wire 格式各有一个——因为它们解码的确实是两种不同的字节格式。

`WirePart` 的五个构造子（`WireText` / `WireReasoning` / `WireToolCall` / `WireToolResult` / `WireMedia`）可从 mjs 按 case 名构造，与 `tests-mjs/domain.mjs` 的 `unionCase` 同一手法，所以 adapter 不需要生产侧新增任何出口。

K1 因此追加一项验收，且这一项是本步的主要判据：

```text
K1-a  testkit adapter：OpenAI wire body → ProviderWireProjection
K1-b  删除 sealProviderVisible / isProviderVisiblePrefix，改用生产投影提问
K1-c  反恒真测试：两条不同 user 内容必须语义不等；两条相同必须相等
      并覆盖 tool_calls / tool 结果 / 多模态，各自内容不同则不等
```

K1-c 不是补充测试，是 K1 存在的理由。 没有它，K1 的「成功」与「把门禁变成恒真」在所有现有反馈下不可区分。这也修正了修订四（K11 变异自检）的排期判断：变异自检不能全部推到最后，涉及投影替换的那一类必须与替换同一步落地。

##### K1 实测结果与两处连带缺陷

`gate-testkit` 29 → 45 项全绿；`test:mjs` 386 → 389；三时区全绿。

删除：`sealProviderVisible`、`isProviderVisiblePrefix`、`normalizeVisibleContent`（83 行），
以及只为 `modelSideCold` 存在的 `lastModelBySession`。新增 `testkit/opencode/provider-wire.js`
（OpenAI wire → `ProviderWireProjection` 的 adapter，全部提问 re-export 生产函数）。

两个嗅探式冷边界豁免整体删除而非移植：

```text
epochCold      tools + 首条 system 未变 → 无论 body 怎么改都重新封印
modelSideCold  tools 未变且 model id 变了 → 同上
```

`epochCold` 放行的正是它要抓的那类变异——错误的前缀替换绝大多数就长成「tools 与
system 不变、body 被改写」。`modelSideCold` 则已过期：它存在的前提是 Host system
prompt 内嵌 model id，而生产早已不这么做（PROMPT-006 发送时 `Model = None`，
prompt 资产内插 model id 实测 0 处）。

连带修正一处 fixture 键错误。 `selectExpectation` 用 wire 投影当缓存键，而 wire 含
tool call id——同一段对话第二次运行 id 不同，键因此不同，缓存永不命中（实测）。
VERIFY-007 把剧本匹配指派给语义投影，缓存键改为 `fixtureKey`。wire 相等只服务封印。

###### 连带发现：三个 Host hook 在真实调用形态下抛异常

修 `manager-tool-contract` 的 hook 名之后露出的。按 Host 的调用方式
（`../opencode/packages/opencode/src/plugin/index.ts:290` 的 `fn(input, output)`）
逐个调用五个 hook：

```text
chat.message                           ok
chat.params                            ok
experimental.chat.messages.transform   Cannot read properties of undefined (reading 'messages')
experimental.session.compacting        (intermediate value)(...) is not a function
experimental.compaction.autocontinue   Cannot set properties of undefined (setting 'enabled')
```

根因在 emit 模板与 Fable 发射元数不匹配：

```fsharp
[<Emit("(args, context) => $0(args)(context)")>]   // 假设内层是柯里链
```

Fable 对 `obj` 类型记录字段与偏应用保留柯里链，对普通双参 `let` 发射二参箭头。
柯里模板作用于二参箭头时只传一个参数，函数体于是拿到 `output = undefined`。

`prompt.ts:1255` 每个 provider step 都触发 transform，所以插件在真实 Host 上每一轮
都抛。`dotnet build` 看不见 emit 模板内部，`domain.mjs` 又从不 import `OpenCode/*`
——两侧都没有理由调用一个 hook。

修法是两个具名 emit（`curriedHook` / `pairedHook`），调用点显式选择，不做运行时
`fn.length` 嗅探——那种「双保险」只会把下一次不匹配藏起来而不是让它失败。

新增 `tests-mjs/Plugin/host-hooks.test.mjs`（4 测试）关掉这个缺口。它的形态值得留档：
每个 hook 一份 fixture，外加一条完整性门禁断言「注册的 hook 集合 == fixture 的键集合」。
最初写成单一最小输入遍历所有 hook，实测两个问题——太空的输入无法触发丢参数那条路径，
而 `chat.message` 会真的去创建子会话并把异步工作泄漏到测试结束之后。共同分母输入比
没有测试更糟。

###### `manager-tool-contract` 的定位错误（登记，不在 K1 修）

清点它的依赖面：`initSpikePlugin` + `mkdtemp` + `git init`，无 `setupScenario`、
无 HTTP、无 mock provider。`events: { listen: () => () => {} }` 是三行假端口而非
harness。它是第 2 层资源契约测试，与 `tests-mjs/Journal/boot.test.mjs` 同类，
在 `testkit/` 里的唯一理由是写它时 `tests-mjs` 还不存在。

它当前是红的，且早于本轮：包 H（`af384b77`）把双 transform 注册收敛为单注册后，
测试仍调 `hooks['chat.transform']`。改对 hook 名后露出下一层——184 个断言里 6 个走
`.execute`，而工具执行需要一个已受理的 Authority Root（AGENT-007 双层 fail-closed），
测试从未建立。其余 178 个断言（registry 形状、schema 键、config prompt、journal 落位）
都是第 2 层，本来就该在 `tests-mjs`。

处置分两步，均不在 K1：

```text
178 个第 2 层断言   迁入 tests-mjs/Plugin/，与 host-hooks.test.mjs 同目录
6 个 .execute 断言  需要 Authority Root fixture，属第 3 层，归退火三
package.json        test:manager-tools 随迁移删除；test/test:release 各去掉一处引用
```

`test:release` 单列 `test:manager-tools` 这一行本不该存在——它是同一段历史的残留。

##### 修订二：删除 `loadScripts` 顺带消灭三个文件，不是「合并」

设计文档说 22 个文件合并后预计降至 19。实测这三个文件的存在理由只有一个——作为 `loadScripts` 的运行期换入目标：

```text
host-restart-after.json                              3 边   无任何 canary 引用
orchestrator-restart-publish-recovery.json           5 边   无任何 canary 引用
orchestrator-restart-publish-conflict-recovery.json  0 边   {"scenario":..., "scripts": []}
```

第三个是空文件。它唯一的作用是在 flow 中途把匹配空间换成空集，即「重启后什么都不该再命中」——而这正是设计文档第八节判定为错误的那件事：重启不改变剧本，重启后的对话步本来就该写在同一个文件里。

所以这不是把三个文件的内容并进主文件，而是删掉一个不表达任何内容的文件、把另两个的 8 条边接回它们本来的对话位置。22 → 19 由此达成，且 `loadScripts` 在剧本侧的 3 处引用同时归零。

##### 修订三：8 条 stub canary 合并为清单驱动，`CANARY_COUNT` 漂移随之消失

实测这 8 个文件（agent-dsl / executor / process-stress / pty-stress / orchestrator / orchestrator-publish / inspector-oneshot / manager-full-loop）除剧本文件名与错误消息外逐字节相同，每个 9 行：静态门禁 + `runCanary('X.json')`。

它们不承载任何信息。8 个文件 72 行仪式表达的是「有 8 个纯数据驱动的 scenario」——那是清单，不是代码。改为单一清单驱动后：

```text
testkit/opencode/tests/data-driven.manifest.toml   8 行声明
testkit/opencode/tests/run-data-driven.mjs         一个入口
```

包 W 的实测缺陷之一「`CANARY_COUNT = 17` 而 `CANARY_TESTS` 实际 19 条，日志里的 `expected ~17` 是错的」在此顺带解决：清单成为唯一来源，数量由它派生，不再有第二处可漂移。这条跨包收益是把 K 与 W 排在同期的理由之一。

余下 2 条内联 canary（companion-projection / manager-companion）不合并。它们是第 3 层轨迹：`expect*` 之外还有真实断言、文件读写、`execFileSync`。K8 对它们只做一件事——把内容声明搬进 TOML，把轨迹与断言留在 `.mjs`。

原文写「4 条」，含 companion-cache 与 companion-replacement。包 K8d 实测两条都已随 X9 失效，予以淘汰，理由记在下方。

##### 修订五：K8d 实测 —— 三条 companion 剧本的 JSON 文件零载入

计入 19 条剧本的三个文件 `companion.json` / `companion-cache.json` / `companion-replacement.json` 都没有任何载入者。三个 canary 全部用 `provider.expect(...)` 在代码里注册期望（8 / 4 / 11 处），从不读 `scripts/`。把三个文件内容替换成 `NOT JSON` 后 `companion-cache` 与 `companion` 照常通过。

在此之上，两条剧本的核心主张已随 X9 死亡：

| 剧本 | 主张 | 实测 |
|------|------|------|
| `companion-cache` | `companion-b-head` 在各轮位置与内容不变 | 生产写入点 0。`CompanionTransform.fs:38` 只有一处 `StartsWith` 读取判断，无人写出该标记 |
| `companion-replacement` 轮 3 与 resetfail | Blogger 收到 `Condense the following FULL companion context…` | `selfRebaseBlog` 调用点 0。触发者 `bloggerSelfRebaseDue` 已按 CTX-001 + CTX-002 删除 |

`companion-cache` 因此落进 canary 的 `else` 分支并打印 `ℹ Prefix replacement did not activate within test rounds`，真正执行的断言退化为「消息数单调不减」——追加式对话永真。`companion-replacement` 则直接是红的（`[MOCK-FATAL] no-prefix-matched role=blogger`，等 `manager-blogger-3`），且工作区干净时即红，非本包引入。

处置：

```text
companion.json                 删除文件（零载入）；角色无 sidecar 与两次投影两次 Blogger
                               请求的内容由 companion-canary.mjs 完整承载
companion-cache.*              整条淘汰。唯一主张无生产写入点，断言永真
companion-replacement.*        整条淘汰。B′/self-rebase 半边无调用点；重锚半边是活的
                               （bloggerNeedsReset 有 2 个写入点），债记在 K8f
selfRebaseBlog                 删除生产函数（39 行）。与 applySquash 的 frame 序列形状
                               不兼容，复用价值低于重写
```

被淘汰的两半正是 K8f 要按 SSOT/12 新建的题目：真实失败驱动的探针提升（CTX-012）与恢复槽内 squash（CTX-006）。把按旧语义写的剧本翻译成 TOML，只会得到一条检验不了新语义的剧本。

K8f 因此多欠一条：重启后 FULL re-anchor 帧（`CompanionHost.fs:64,73` 的 `bloggerNeedsReset`）必须在 X 系列剧本里重新获得覆盖。这是本次淘汰唯一真正丢失的覆盖面。

##### 修订四：新增 K11 —— 森林的反向自检

设计文档第十四节的结论是「一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险」，并列出四种既有的绿灯误判（`epochCold` / `specificity` / `requestRoleOf` / `loadScripts`）。但 K1–K10 里没有任何一步验证这件事本身。

K10 的森林自检查的是纯函数性、索引无冲突、fault 有限、无死边——全部是「森林自身结构正确」。结构正确不蕴含「错误实现会被拒绝」：`epochCold` 那条豁免在结构上完全合法，它的问题是放过了不该发生的 epoch 切换。

因此新增：

```text
K11  变异自检：对每条关键 canary，注入一个确定的错误响应序列，断言森林拒绝
     覆盖至少四类，对应第十四节的四种历史误判：
       前缀在未声明处断裂          → 必须 fail closed，不得靠 tools+system 相同放行
       同长度冲突前缀              → 载入期拒绝，不得打分取一
       角色与 AttemptExecutionProfile 不一致 → 必须由 profile 决定，不得由 wire 反推
       重启后本该命中的边消失      → 必须仍然命中（静态剧本的定义）
```

这是唯一能证明门禁在起作用的测试类别，与包 T-5b 对 `architecture-gate` 做的负向验证（移走 `guide-contract.test.mjs` 与 `runner.mjs` 各报 2 项违规）同一方法论：门禁必须红过一次才算存在。

##### 修订后的执行顺序

```text
K1   删除 sealProviderVisible / isProviderVisiblePrefix，testkit import 生产投影
K2   运行时键提取：(lane, turn, step) 三个纯函数 + 最长前缀唯一命中
K3   delivery 与 fault 计划求值（纯函数）
K4   epoch 冷边界显式声明与前缀封印验证
K5   TOML schema + 载入期编译器 + 六项载入期校验 + 根键顺序硬检查
K6   TOML formatter（幂等）
K7   旧字段拒绝器：turn 编号 / reusable / pathless / blocking / loadScripts / specificity
K8   19 个 scenario 手工重写为 TOML（含 X-A–X-D 四条新增），4 条内联轨迹只搬内容
K9   删除 strict-mock-forest.js / strict-mock-matches.js 旧匹配路径
K10  森林结构自检：纯函数性、索引无冲突、fault 有限、无死边
K11  森林变异自检：四类错误响应必须被拒绝（新增）
```

K1 提前到第一步的理由：它决定 K2 的前缀比较用哪个投影。先做 K2 会写出一个基于 testkit 私有规范化的前缀索引，K1 完成后整体重写。

##### K9 状态修正（b48e38bd 静态复核，后于本提交完成）

K9 的「删除 strict-mock-forest.js / strict-mock-matches.js 旧匹配路径」已完成。
旧路径判定为「明确淘汰」而非「迁入静态森林」：全仓 grep 确认无任何 canary /
gate 仍调用 `provider.expect*`，旧 expect 路径无调用方可迁。已删除
`strict-mock-forest.js`（selectExpectation / consumeExpectation / edgeLabel /
edgeWaitIds / pendingExpectations / normalizeLane / laneLabel / indexPathEdge /
templateFingerprint）与 `strict-mock-satisfy.js`（checkSatisfied）；provider 内
`expect*` 方法、`_dispatchMatched`、无 ScenarioRuntime 时的 `selectExpectation`
路径与 reseal 分支一并移除，无 scenario 的 chat 请求一律记 `no-scenario-attached`
未匹配。`strict-mock-matches.js` 仅保留 request-kind 分类（title/synthetic/chat）
与两条诊断 extractor（`matchesExpectation` / `requestRoleOf` / `NUDGE_MARKERS` /
`requestSessionOf` / `requestParentSessionOf` 已删）；`strict-mock-state.js`
仅保留请求簿记与 alias 绑定（edges / templateIndex / aliasToEdge / pathEdges /
pathCursor / sealToEdgeId / observedEdgeIds 已删）。`gate-mutation-cases.mjs`
的 PROMPT-008 案例注释同步。至此 VERIFY-003 的 CONTRADICTS 记录随本提交失效。

历史复核原文（保留备查）：`strict-mock-provider.js` 曾在无 `ScenarioRuntime`
时走 `selectExpectation`，`companion-canary.mjs` 等旧内联轨迹曾调用
`provider.expect*`；当时只能称「ScenarioRuntime 核心已接入、旧路径共存」。

##### K10 落地与红过证据（4924905a）

K10 本体由 4b312cf6 落地：森林级性质「同请求序列 → 同内容序列」实测 15 条剧本全可
派生（182 请求，mismatched=0 unanswered=0），其余三项改为在册清点（任一映射消失
即失败）。4924905a 修通当前森林的两处红：重启两条剧本的 blogger/blogger-reanchor
在 31d4958b 后声明了相同片段序列被 duplicateDeclarations 拒绝，取生产重启 prompt
的真实区分语作 reanchor 中间锚片段；gate-source-cases 的 Companion 探针同步跟随
31d4958b 的实际 header。红过一次证据：把在册清点中「无死边」行登记的用例名改一个
字符，门禁立即红（`lost case "VERIFY-003 a turn no flow can reach is rejected":
expected 0, got 1`，272 passed 1 failed），恢复后 273 passed 0 failed。

### 包 N：运行时合成文本的 TOML Instruction/Data 记法（ARCH-010）

| 项 | 值 |
|----|----|
| 条款 | 新增 ARCH-010；连带修订 CTX-013、PROMPT-001 交叉引用、SSOT/99 三术语 |
| 设计定稿 | `STATUS/design-synthetic-toml.md`（动议审阅稿归档原文，含选型推理与 17 条禁止实现） |
| 核心原则 | instruction 用 comment，data 用 field；instruction 永远在前 |
| 纳入判据 | 四条同时成立：由 LLM 按文本 token 阅读、非原生 system/developer prompt、非未重新包装的人类原始消息、由运行时/Host/插件/工具/协作层/projection 构造或包装或复制或重新投影 |
| 明确排除 | system prompt、developer prompt、角色 prompt assets、provider 原生 instruction channel、人类原始消息、模型原始输出、provider 原生结构、不进入模型上下文的内部数据 |
| 生产 | 将改动（prompt 组合面，不改 transport） |

选择 TOML 不是因为系统需要 parse TOML，而是因为 `# …` 与 `key = value` 对 LLM 有稳定熟悉的视觉语义，且不需要 XML closing tag、JSON envelope 或额外 sentinel。完整选型推理见归档 §1.2。

#### 排期裁决：N 拆两段，fork surface 段先于 canary 修红

包 K 把 canary 剧本的 TOML 声明写定；包 N 改写这些声明所匹配的生产合成文本字节。两者触碰同一批 scenario 声明，故顺序是：

```text
N0–N4  规范 + 字符串 owner + inventory + fork surface + 门禁     先于 canary 修红
canary 11 红 → 16/16                                            随后
N5–N6  其余 surface 迁移 + fixture/golden/byte-limit 更新        canary 全绿之后
```

若先修红再迁 surface，同一批 turn 声明要按旧字节写一遍、再按新字节重写一遍。而当前多数红灯的同一根因——`HostForkRuntimeFork.fs:196` 的条件信封两形态无公共前缀——正是 N3 要迁的 fork/child-instruction surface：ARCH-010 的 instruction-first 保证恰给条件信封一个它现在没有的稳定前缀（instruction comment 恒在最前，有无 parent work record 都命中同一片段声明）。先修红等于先做一个会被 N3 重写的修法。

N5 排在 canary 全绿之后而非之前：动议 M5 要求「更新所有依赖最终文本 bytes 或前缀的 strict mock、scenario、golden snapshot、payload digest expectation、canary、byte-limit test」，而全绿的 canary 是做这件事时唯一可信的回归底座。

#### 子步骤（对应动议 M0–M5）

```text
N0  规范先行（M0）：ARCH-010 进 SSOT/01；CTX-013 修订；PROMPT-001 交叉引用；     已完成
    SSOT/99 三术语。不得先批量改生产 prompt 再让实现反向定义规范
N1  唯一字符串 owner（M2）：canonical TOML 字符串 writer 收敛为一处；            已完成
    多行定为 ''' + 零加工内容 + closing 独占一行；含 ''' 或裸控制字符者
    回退单行 basic string。裁决与实测见下方 N1 记要
N2  surface inventory（M1）：列出全部最终进入 LLM 的文本生产点并四分类            已完成
    NativeSystemPrompt / HumanRaw / ModelNative / RuntimeSyntheticToml。
    该分类只用于实现审计，不构成新的运行时 envelope。门禁固定该清单
N3  fork/child-instruction surface 迁到 ARCH-010 形态；fork 信封条件包裹在此定案   已完成
N4  ARCH-010 门禁并红过一次：instruction 不得为字段、data 不得为顶层 comment、      已完成
    instruction-first、data 开始后无顶层 comment、无 """、closing 独占一行、
    渲染结果 parse 成功且 value == 原文 + 尾换行、system prompt 未被纳入、
    human raw 未被包装、provider/tool 原生 binding 未改变
─── canary 修红（11 → 16/16）与退火三在此之后 ───
N5a 不依赖 delta 货币的 surface（M4）：continuation / interaction repair /            已完成
    review guard nudge / executor summary input。输入都是运行时自己的固定文本或
    命令输出，与 Companion delta 无关。不得迁移 system prompt assets
N5b Blogger delta surface（M3）——前置已解除。X4 后半已完成换币；正常 delta surface 已迁入 typed TOML。
    恢复侧仍待 X-wire；迁移前的阻断证据见下方「N5 拆分：M3 的前提在本仓不成立」
N6  更新依赖最终 bytes 的 fixture / golden / payload digest / byte-limit / canary（M5）。
    某固定文本若有自己的 version/digest 合同，由该文本的 SSOT owner 按既有规则决定是否 bump；
    本包不为各领域预先发明统一 versioning
```

#### N0 落地记要

`d4112f62`。ssot-lint 136 → 137 条款，396 处引用，14 个文件。

ARCH-010 承载动议的全部规范内容：纳入四判据、记法三条（instruction=comment / data=field / instruction-first）、语义分类（历史祈使句是 data、解释规则是 instruction、截断事实与截断规则分开）、三种合法形态、字符串九项不变量、data containment、无统一 envelope、局部 schema 字段设计、单向表示、排除范围、transport 边界、门禁清单。

CTX-013 三处修订：字符串写法改为引用 ARCH-010（删掉原「有换行且不含 `"""` 用三引号；含 `"""` 但不含 `'''` 用字面多行字符串」的按内容选 delimiter 规则）；新增「Blogger delta 的 instruction 与 data」分野；新增「instruction header 计入 chunk 限额」——header bytes 必须计入 200 KiB，chunker 以最终实际发送 bytes 计算，header 不得中间截断，data-only chunk 无额外开销。

`PENDING/13-Toml方案.md` → `STATUS/design-synthetic-toml.md`，按 `design-context-recovery.md` 的归档惯例加性质声明头（规范位置对照表 + 保留原因 + 编号说明 + 机械改动说明）。

#### N1 裁决：多行 delimiter 定为 `'''`，无缩进，closing 独占一行

动议原稿 §6.3 要求 `"""` + 四空格内容缩进 + closing 紧随最后一个内容行，并禁止 `'''`。该形态经实测否决，按最终裁量权改定，动议原稿已同步修订（`design-synthetic-toml.md` §6.3 全节重写，新增 §6.3.1／§6.3.2／§6.3.3）。

否决理由是原形态无法同时满足动议自己的两条要求：

```text
§6.4 + §7   工具输出、文件内容、diff、编译日志必须原样进入 value
§6.3 不变量  原始内容自身的缩进必须保留
```

`"""` 是 basic 多行字符串，处理转义序列。含反斜杠的正文——每个 regex、每条 Windows 路径、每个非平凡 tool-call args——只有两种下场，且两种都违反上面某一条：

```text
不转义反斜杠   \d 不是合法 TOML 转义 → 文档根本不 parse
转义反斜杠     模型看到 \\d+ 而工具输出的是 \d+ → data 失真
```

`'''` 是字面多行字符串，不处理任何转义，反斜杠原样通过，两难消失。这也正是 `BloggerToml.fs:96-112` 原注释刻意选 `'''`、刻意从不发射 `"""` 的理由——那段实测结论是对的，动议原稿写反了。

四空格格式缩进同样否决：TOML 不对字面多行字符串去缩进，那四个空格会成为 value 的一部分，即 renderer 篡改了它承诺原样转发的 data，与不变量 4 直接冲突。

closing delimiter 改独占一行有两个理由。其一是可读性：`second line'''` 把内容与结构挤在同一行，而多行形态存在的意义就是让模型看清结构。其二是它消掉一整类边界情况——内容以单引号结尾时 `ends with '''` 会与 closing 连成四引号，独占一行后该问题不存在。代价是 value 多一个尾换行，对一份只供阅读的投影无影响。

##### 可解析性是硬要求，不是可选项

「只供 LLM 阅读」不等于「可以只做个样子」。动议 §12 禁止的是让业务逻辑依赖反向解析，不是允许发射不合法的 TOML。渲染结果必须真的能被 parser 读回，因为这是本记法唯一可机械检验的性质——门禁、golden test 与 containment 断言都建立在「这是一份合法 TOML 文档」之上。一份只是长得像 TOML 的文本没有任何可断言的不变量。

因此上一版记在本节的裁决（`"""` + 反斜杠原样，接受非法转义序列）作废。

##### smol-toml 实测（`smol-toml@1.7.0`，即 `scenario-schema.js` 已在用的 parser）

```text
形态                                    结果
'''\nfoo'''            closing 紧随     OK   value = "foo"
'''\nfoo\n'''          closing 独占     OK   value = "foo\n"      ← 采用
反斜杠 regex / Windows 路径              OK   原样通过，无需转义
原始前导缩进 / tab / 内部空行             OK   逐字保留
结尾单引号 + closing 独占                OK   边界情况消失
内容含两个引号                           OK
内容含三个单引号                         FAIL 提前闭合，必须回退
内容含裸 NUL                            FAIL 控制字符，必须回退
```

往返验证：上表全部 OK 项的 parse 结果均等于「输入 + 一个尾换行」，逐项核对无差异。

##### 因此实现规则

```text
无换行                        单行 basic string + canonical 转义
有换行                        '''\n 内容 \n'''，内容零加工
内容含 ''' 或裸控制字符        回退单行 basic string 并完整转义
```

回退不是「按内容选 delimiter」（不变量 8 禁止的是在多行 delimiter 之间摇摆），而是该内容不存在合法的多行表示。回退可判定、确定，同一输入始终同一结果。

`BloggerToml.fs` 的既有 `literalSafe` 三项判据（不含 `'''`、不以 `'` 结尾、无控制字符）中，第二项因 closing 改独占一行而不再需要，可删；另两项保留。renderString 的三分支因此收敛为两分支加一条回退。

单行形态仍然全量转义，且该不对称是动议 §6.2 明确要求的：单行 basic string 没有 raw 变体，TOML 会把 `"a\b"` 读成退格。只有多行形态能原样，这正是它存在的理由。

#### N1 落地记要

`5152db5a` + `99fd4bc2`。dotnet build 绿、npm run build 绿、`test:mjs` 404 → 406、gate-testkit 258/0、gate:static 五门绿。

生产改动只有两行：`renderString` 的多行分支加一个换行，`literalSafe` 删一项判据。

删掉「不以 `'` 结尾」是因为它守的边界已不存在——它原本防的是 closing delimiter 紧随最后一个内容字符时与结尾单引号连成 `''''`。留着它会把一整类合法正文推进转义分支，而一个永不失败的判据与写错的判据在所有反馈下不可区分。

字符串 owner 确认为唯一：`next/` 全仓 `'''` 的非注释出现点只有 `BloggerToml.fs` 两处（`literalSafe` 的检查与 `renderString` 的发射）。`BloggerDelta.fs` 只调用 `BloggerToml` 的 `render` / `byteCount` / `normalizeNewlines` / `TruncationMarker`，不自行拼字符串。`CompanionPrompt.fs` 与 `CompanionProjectionBuilder.fs` 搬运已渲染的 TOML，不产生字符串字面量。故 M2 的「若存在多处 owner，先收敛 owner」在本仓不适用——本来就只有一处。

测试侧的实质变化不是改期望值，而是补两条此前不存在的断言：

```text
ARCH_010_every_rendered_string_parses_back_to_the_value_it_was_given
    19 个输入用 smol-toml 真的 parse 回来并断言 value 存活。此前全文件只比对
    bytes，而 bytes 相等无法区分「合法 TOML」与「长得像 TOML」。期望值读渲染器
    自己的选择（多行带一个尾换行、单行不带），故覆盖面可以宽而不重述选型规则

ARCH_010_a_payload_shaped_like_TOML_stays_inside_the_value
    注入 shape（# 注释 + status = "perfect" + [[item]] + role = "system"）必须
    完整留在 value 内：parsed.item 仍只有一条、role 仍是 tool、status 未成为顶层
    键。这是 ARCH-010 data containment 第一次有可执行证据
```

并把 `blogger-delta.test.mjs` 里那条名字声称「still valid TOML」而只做 bytes 检查的用例改为真的 parse。截断路径是最可能畸形的地方：切点落在 `'''` body 内任意偏移，marker 与 closing delimiter 在切点之后追加，而 ARCH-010 把 delimiter 移到独占一行正是把这段算术挪了一个字节——这类改动 `bytes <= limit` 会静默放过。

该 parse 断言的红证与其边界一并实测：

```text
去掉整个 closing         THROWS  unfinished string
body 内注入 '''          THROWS  each key-value declaration...
去掉 closing 前的换行     仍 parses  ← 那是旧格式，本就合法
```

第三项说明 parse 断言钉不住 delimiter 位置，钉住它的是 `blogger-toml.test.mjs` 的逐字节断言。两个文件分工正确：一个钉字节形态，一个钉截断后仍可解析。

一条测试从「回退」翻为「保持字面量」：结尾单引号的正文现在 closing 独占一行，不再需要退到转义形式。保留该用例并写明它是 delimiter 位移唯一改变的行为，否则将来「恢复结尾引号保护」会静默把这些正文推回转义分支。

##### N1 收口：抽出 `SyntheticToml`

`de08822d`。`test:mjs` 406 → 418、gate-testkit 258/0、gate:static 六门绿、`build/next/OpenCode/Plugin.js` 可 import。

上面那句「本来就只有一处 owner」是当时的事实，但它是巧合而非结构：`BloggerToml` 恰好是唯一生产者，故规则私藏其内也不违反条款。N3 与 N5 会让第二个 surface 渲染 value，那一刻「只有一个 owner」要么成为结构，要么第二个 surface 复制逻辑、条款禁止的局部方言就地诞生。

故按条款自己的两条边界切分：

```text
SyntheticToml   字符串规则 + 文档布局      「字符串写法只有一个 owner」
BloggerToml     part 种类 + 固定键序 + marker  「不引入统一 envelope」
```

`SyntheticToml` 的三个新成员承载此前无处安放的规则：

```text
comment   多行 instruction 按行拆成多条 comment。裸 \n 会终止注释并把余下内容以
          语法形式留在顶层——这正是 instruction 变成畸形文档（或字段）的路径。
          空行渲染为裸 # 以保持 header 为单一连续块；真空行会终止 header，令其后
          一切成为第二个非法 header
field     name = 已渲染值，值必须先经 renderString
document  instruction header → 恰好一个空行 → data body。三种合法形态由参数为空
          自然得到；「data body 不输出 comment」变为不可表达，因为 body 块只能来自
          field 与表构造器
```

`BloggerToml` 相应收缩，新增 `renderWith` 承接可选 instruction header，`render` 成为 `renderWith []` 的别名。facade 拆为两个命名空间，且 `bloggerToml` 不再导出 `renderString` / `byteCount` / `normalizeNewlines`——留着它们能工作会告诉下一个读者 Blogger 拥有字符串渲染，即条款禁止的那件事。

测试按同一边界重分：`synthetic-toml.test.mjs` 20 条（字符串形态、comment 拆行与空行、三种文档形态、data body 无顶层 comment、19 输入往返 parse、字节计数与平台编码器一致），`blogger-toml.test.mjs` 缩为 13 条只剩 schema。原文件八条字符串形态测试是删除而非复制：第二份断言同一规则就是局部方言上移一层。

并补三条此前不存在的 CTX-013 断言：data-only delta 不得出现任何 comment（原绝对规则恰好在此存活，且「data-only chunk 必须人为添加 instruction」被明确禁止）；instruction header 在 supply 时位于最前且与 body 隔一个空行；header bytes 计入渲染总量而 data-only 不付额外开销——后者是 chunker 200 KiB 限额测试所依赖的算术。

#### N2 落地记要

`19a9a976`。`gate:surface` 进 `gate:static`（现 6 条门禁）。

动议 M1 要求列出全部最终进入 LLM 的文本生产点并四分类。手写一份「构造 prompt 文本的地方」的清单正是本包 W1 与 W2 已经删过两次的缺陷形态：一份没人更新的镜像。而生产者无法机械枚举——任何 `sprintf` 都是候选。

故 ground truth 取 sink 侧。PROMPT-005 使这个闭集为真：插件发出的每条 user-shaped prompt 都经 `PromptDispatcher`，故其三个 send 成员加 `sendFirstPrompt` 是插件文本到达 `SendPrompt` 进而到达 provider 的唯一通路。扫描其调用点得到 surface，注册表说明每个 surface 承载什么。

实测 8 个 surface，全部 `RuntimeSyntheticToml`：

```text
OneShotAgentTool           one-shot agent 指派
CompanionHostBlogger       Blogger delta（正常 + 重启后 re-anchor）
HostForkRunLifecycle       fork 子会话首 prompt（lifecycle 路径）
HostForkAgentOwner         fork 子会话首 prompt（共享助手）
HostForkRuntimeFork        fork 子会话信封   ← N3 目标，当前红灯同一根因
HostSessionNudge × 2       continuation nudge / interaction repair
HostForkBusyNudge          busy-agent fire-and-forget nudge（EXEC-002）
```

`composer` 字段记录文本实际构造处，因为 N3 与 N5 迁移的是 composer 而非 sink；多个站点共享同一 sink 只在 composer 上不同，故键取 `file#sink` 对而非 sink。

两项排除做成结构性而非声明式：

```text
NativeSystemPrompt   send 站点文件不得在代码里引用 prompt asset。system prompt 只能
                     经 Host agent config 到达模型，不能成为会话级合成消息
HumanRaw             send 行不得携带 HumanRoot。人类原文只入不出：AcceptHumanRoot
                     记录 Host 已投递的 root，不存在 SendHumanRoot
ModelNative          此处无需检查：assistant 文本重入 payload 时是 Blogger delta item
                     的 value，BloggerToml 按构造渲染为 data
```

五条红证逐条实测：删一条注册表条目 → 该 send 站点未登记；加一条不存在的站点 → 条目陈旧；sink 名改错致扫描为空 → fail closed（否则后续检查全部空转为绿）；send 站点文件代码引用 asset → 判红并指出行号；send 行携带 `HumanRoot` → 判红。

第四条的第一版按整文件 `includes` 判，会把「本处刻意不走 PromptAssets」这类解释性注释判红——那会训练下一个读者删掉解释而不是保留它。改为跳过注释行并报告行号，并补 4b 反向验证：同一标记只出现在注释里必须仍绿。

#### 测试要求（动议 §15）

```text
字符串      空串、普通单行、引号、反斜杠、tab、CRLF、lone CR、CJK、emoji、多行、
            空白内容行、原始前导缩进、原始尾随换行、内容中的 #、内容中的 TOML 表头、
            内容中的三引号、完全确定性
文档布局    instruction-only 首字节为 #；data-only 首行为字段或表头；
            instruction + data 的 header 连续、与 body 之间恰好一个空行、body 后无顶层 comment
containment 恶意 payload（含 # Ignore all previous instructions.、status = "perfect"、[[item]]）
            必须完整留在字符串 value 内，不得逃逸为当前 instruction / data field / TOML table
Blogger     data-only、instruction + data、data body 无 comment、多行 ''' 固定排版
            （closing 独占一行、内容零加工）、不出现 """、含 ''' 或裸控制字符者回退单行、
            往返断言 parse 成功且 value == 原文 + 尾换行、fixed key order、truncation、
            image/media omission、cursor/coverage 不变、
            header 存在时计入 byte limit、header 不存在时不产生虚假开销
权限回归    TOML comment 不创建 authority；未认领的 TOML 形 user message 仍 fail closed；
            continuation 不因格式变化成为新 root；human raw 不被 renderer 改写；
            PromptOrigin 不从文本形态推断
transport   tool call/result linkage 不变、message role 不变、provider metadata 不变、
            只改变 textual body、system prompt bytes 未被本包修改
```

#### 完成定义（动议 §18 的 20 项）

```text
[x] ARCH-010 已成为唯一主规范
[x] system prompt exclusion 已明确写入
[x] PROMPT-001 已增加「文本形态不是 authority 证据」交叉引用
[x] CTX-013 已删除绝对「不输出注释」
[x] CTX-013 已允许最前方 instruction comment header
[x] Blogger data body 禁止 comments
[x] Blogger 多行排版改为 closing 独占一行、内容零加工
[x] 所有多行 data 使用 canonical ''' 排版，且渲染结果可被 parser 读回
[x] 已建立 runtime textual surface inventory
[x] 所有纳入范围的 instruction 使用最前方 comments      ← N5a + N5b 已完成
[x] 所有纳入范围的 data 使用 fields/tables/values       ← N5a + N5b 已完成
[x] data body 开始后不存在顶层 comment                  ← SyntheticToml.document + N5b 已完成
[x] human raw message 未被包装
[ ] model-native transcript 未被重写                    ← 与权限/transport 回归共同验收
[x] system/developer prompt 未被本包迁移
[ ] provider tool binding 未变化                        ← 与权限/transport 回归共同验收
[x] 不存在统一 envelope
[x] 不存在 TOML 反向 parser
[ ] fixtures、golden tests 和 canary 已更新             ← 与退火三共用验收
[ ] 完整 release gate 通过                              ← 与退火三共用验收
```

勾验依据，逐项可复核：

```text
Blogger data body 禁止 comments     CTX_013_a_data_only_delta_emits_no_comment_at_all，
                                    且 document 使 body 内 comment 不可表达（body 块只能
                                    来自 field 与表构造器）
已建立 surface inventory            scripts/surface-inventory.mjs + gate:surface，8 个 surface
                                    双向检查，五条红证
human raw 未被包装                  N2 结构性判据：send 行不得携带 HumanRoot。已红过
system/developer prompt 未被迁移     N2 结构性判据：send 站点文件不得在代码里引用 prompt
                                    asset。已红过
不存在统一 envelope                  SyntheticToml.document 签名只有 instructions 与 body，
                                    无 envelope 参数；两个渲染器均无 schema/kind/origin/
                                    authority/content_type/message_id 字段
不存在 TOML 反向 parser              next/ 全仓零 TOML parser。smol-toml 的消费者只有
                                    scenario-schema.js（harness 读测试 fixture，非合成
                                    payload）与三个测试文件
```

余下 6 项分三组：N3 与 N5 迁移 surface 后可勾 3 项；权限与 transport 回归勾 2 项；退火三勾 2 项（其中 fixtures/canary 一项与退火三共用）。

#### 编号说明与遗留裁决

归档文件标题自称「SSOT/13 修正动议」，但其 §13.1 要求主规范落为 SSOT/01 的 ARCH-010，故未创建 `SSOT/13.md`，SSOT/13 编号仍空缺。

`PENDING/14-Predict方案.md` 引用的「SSOT/13 — Projection Algebra（所有 provider-visible projection 的唯一生产路径）」是另一份文档，不在 PENDING 中。14 与 15 合入前必须先裁决：Projection Algebra 是否独立成 SSOT/13，还是同样落为 SSOT/01 条款；若前者，14 的全部 projection 条款引用需重新指向。

#### N3 落地记要

`d87a5fcd` + `202682ee`。`test:mjs` 421 → 432、gate-testkit 259、gate:static 六门绿。

替换两个各自独立、各自条件的信封（`HostForkRuntimeFork.fs:196` 与 `:98`），故子会话首 prompt 从四种无公共前缀的形态收敛为单一 payload。可选部分变为可选字段，位于两个稳定锚点之间，片段声明可跨过。

`gate-runtime-key-cases.mjs` 的 `WRAP` 常量删除，改为从生产 import `BaseInstructions[0]` 并经 `comment()` 渲染。原常量是镜像：N3 删掉信封后它会继续描述不再发送的文本而用例保持绿——对着生产已停止产出的形态验证匹配器。

八个剧本、15 条 turn 声明改为有序片段。一处不能照抄原文：`pty-stress` 的 assignment 含 `agent="pty"`，单行 basic string 会转义引号，故作者原文不再是子串，片段截到引号之前。这类地方按渲染后的 bytes 取片段，不按作者写的原文。

写测试时自己踩中一次：声明锚点写成 instruction 原文而非渲染后的 comment 行，表现为 unmatched 而非任何指出成因的信息。已把 `headerOf` 助手与该陷阱写进测试文件头。

#### N4 落地记要

`6aec5e93`。gate-testkit 259 → 273。

`arch010.js` 校验输出而非渲染器：writer 测试证明 `SyntheticToml` 行为正确，条款要求的是「每个到达模型的 payload 遵守 ARCH-010」。一个手工拼装文档、或调用 writer 后再追加内容的 producer，能通过全部 writer 测试而违反条款。

三处设计要点各由实测逼出：

```text
splitDocument 必须区分结构行与字面量内容   payload 的 value 合法地含 #、[[table]]、
                                          key = value。不跟踪字面量块的扫描会对正确
                                          payload 报违规，而「自然的修法」是弱化规则
                                          直到它们通过，得到同时接受真实违规的门禁
delimiter 检查必须先抹掉单行字符串 value   注入 payload 含 ''' 时 renderString 回退单行，
                                          文档合法地在一行内含 assignment = "… ''' …"。
                                          第一版对完全正确的 payload 报了三条违规
删掉不可达的 unterminated 分支             实测三种形态（无 closing、EOF 截断、closing 后
                                          有尾随文本）全部 parse 失败，parse 守卫先返回。
                                          改为一条用例钉住「由 parse 守卫捕获」，因为读
                                          代码的自然结论是「缺少该规则」——它不缺，在上游
```

`testkit/opencode/production.js`：testkit 侧生产 facade。存在理由是一个实测陷阱——`ForkChildPayload.render` 接到普通 JS 数组不抛错，而是读成空 F# 列表。`gate-runtime-key-cases.mjs` 正中此坑：reviewer 用例传 `['Ship it.']` 却收到无 requirements 的 payload，且因断言的声明是 `[anchor, assignment]`（两片段在四形态中都存在）而依然全绿——证明了匹配器工作，却从未走过它为之而写的路径。修法不是「记得调 `ofArray`」，而是任何调用方都不直接碰生产函数。另有一条用例断言四种 fork 形态必须是四份不同文档：输出相同即意味着可选输入根本没到达 renderer。

同时修掉 fable-library 版本硬编码：第一版写死 `4.30.0` 而本仓在 `5.13.0`，import 即抛。改为扫描目录，与 `tests-mjs/domain.mjs` 同一手法。

#### canary 现状与红灯分类（N4 后实测）

```text
6 / 16 绿    manager-companion  inspector-oneshot  process-stress
             agent-dsl（新绿）   host-nudge（新绿）  reviewer-restart
```

基线是 5 绿。N3 落地后先跌到 3 绿——生产文本已改而声明未跟，这是 N3 与剧本更新必须同属一个工作单元的直接证据，不是回归。剧本跟上后到 6 绿。

余下 10 条红按成因分三类，每类的处置不同：

```text
类一  N5 目标（prompt 文本尚未迁移）
      companion            reason=no-prefix-matched，role=blogger。
                           CompanionHostBlogger.fs:72,77 的散文外壳
      executor             expectation map.0。ExecutorSummarize.fs:95 的 summary input

类二  既有行为红（与 ARCH-010 无关，改动前后同一签名）
      fallback             expectation round1
      fallback-aabb-trace  expectation prove
      reviewer-verdict     AssertionError ReviewVerdictRecorded count
      pty-stress           expectation devops.5，第二个 PTY 的 trap TERM 行为
      host-restart         expectation=none
      orchestrator-publish             expectation manager.0
      orchestrator-restart-publish     expectation manager.3

类三  前进但未到底
      manager-full-loop    首轮停在 role=inspector（no-declared-turn，从未到达任何
                           expectation），现推进到 coder-edit.0，即已通过 inspector /
                           browser / meditator 三条。属类一与类二的混合，需在 N5 后复测
```

分类依据是可复核的：类二六个签名在剧本改动前的首轮日志中逐字出现，故非本轮引入。`coder-edit.0` 是唯一首轮不存在的签名，因为首轮 manager-full-loop 更早就停了。

推论：fork 信封这一根因已在结构上消除，它此前掩盖了类二的六条。类二不属包 N——它们是 fallback、review verdict、PTY 与 orchestrator 的行为债，须各自定位。包 N 只欠类一两条，且都在 N5 的既定清单上。

##### Executor durable Journal 修复（PERSIST-009 / PROMPT-005，2e5fc0c8）

静态追踪 `executor` 的 `map.0` 停止在 `ToolRuntimeScope.ExecutorRuntimeFor`：该路径曾以
`?journal = None` 构造 `HostForkRuntime`，随后 `ExecutorSummarize` 的 child prompt 进入
`HostForkRunLifecycle.sendAgentOwnerRoot`，因无 Journal 被 PromptDispatcher fail closed，
provider 永远收不到 map child。已改为复用 `ToolRuntimeScope` 的 durable `journal`，与普通
runtime 使用同一 Journal 入口；本次休克复核只证明静态接线，canary 仍待退火三复测。

#### N5 拆分：M3 的前提在本仓不成立（迁移前实测，已由 X4 解除）

动议 M3 说「优先迁移 Blogger，因为已有 typed semantic parts、deterministic renderer、byte limit、现成测试」。这四项对 `BloggerToml` / `BloggerDelta` 全部为真，但它们都不在活路径上。实测：

```text
迁移前活路径    Companion.Submit → Companion.jsonDelta → blogFn delta
                           → CompanionHostBlogger.blog:69-78 散文外壳 + 该字符串
                 即 delta 仍是 JSON 字符串（CompanionDelta.fs:93 的 jsonDelta）

迁移前零生产调用点
                 BloggerDelta.nextChunk                 0
                 CompanionProjectionBuilder.build       0
                 BloggerToml.*                          仅由 BloggerDelta 调用，而它本身零调用点
```

整条 TOML delta 链是一个自洽但悬空的岛，与阻断 K8f 的 X 恢复链同一形态。

若按 M3 就地把散文外壳改成 ARCH-010，产出的是：

```toml
# Write one dense work-log entry for the delta below.

delta = '''
{"messages":[…]}     ← JSON，不是 TOML
'''
```

该文档形态上完全合规：`gate:surface` 的 standing 会翻成 `CanonicalPayload`，`arch010.js` 全部规则通过。而 CTX-013 的 delta 契约——固定键序、三级切块、200 KiB、图片 omission marker——一条都没有到达。那是一个「绿灯描述的不是它所声称的东西」，本次迁移已实测四次的同一形态，且这次是门禁自己宣布的。

故 N5 拆两段：

```text
N5a  不依赖 delta 货币的 surface，现在可迁
     continuation nudge / interaction repair   TurnCompletionProgram.fs:92,158,227
     review guard nudge                        HostReviewGuard.fs:147,164
     executor summary input                    ExecutorSummarize.fs:95,113
     三者的输入都是运行时自己的固定文本或命令输出，与 Companion delta 无关

N5b  Blogger delta surface，迁移前阻断，现已由 X4 后半解除
     前置不是文本迁移，而是当时尚未完成的换币：
       ICompanionDurablePort.AppendSuccessful 的签名
       ProjectionSnapshot = string → SemanticMessage list + SemanticCursor
       BlogEntryCommitted 的 cursor 推进
     此前置早已登记在「X9 未清零的一行：jsonDelta」；X4 已完成该货币切换。
```

推论与 K8f 一致：迁移前欠的不是剧本也不是文本，而是接线。包 N 不代包 X 做换币——那会把一个架构级改动塞进一个记法包；X4 完成后，N5b 的验收（header bytes 计入 200 KiB、图片 marker、三级切块）才有真实 TOML delta 可验证。

`companion` canary 的 `no-prefix-matched`（role=blogger）因此归入 N5b 而非 N5a，与 `executor` 的 `map.0` 分属两段。

#### N5a 落地记要

`a5ce0d40` + `3e49242f`。`test:mjs` 432/0、gate-testkit 273/0、gate:static 六门绿。standing 分布 1 canonical → 3 canonical，1 awaiting N5。

#### N5b 落地记要

N5b 合并提交 `6f78de2f`，其三个前置动作可由提交链逐项定位：

```text
81e0b9e6  CTX-013  Blogger normal surface 改用 typed BloggerDeltaChunk/TOML
7d1d5ea5  ARCH-010 surface inventory 门禁锁定 typed Blogger payload route
253f5b87  CTX-013  header bytes 计入 Blogger chunk limit
```

当前源码证据：`CompanionHostBlogger.blog` 只把 `chunk.Toml` 送入 Blogger，
`BloggerDelta` 负责 chunk/cursor，`surface-inventory` 报告 8 个 surface、4 canonical、
4 verbatim-forward、0 awaiting N5。上述提交的编译与第 1–3 层数字沿用提交时记录；本轮
休克复核只运行静态门禁，未重新宣称 canary 全绿。X-wire 仍未接线。


##### executor summary：已在 ARCH-010 内，欠的是 fork 签名链

试着迁移时发现该项已由 N3 结构性满足。`ExecutorSummarize` 唯一的送出路径是 `runtime.Fork`（`ExecutorSummarize.fs:76` → `ExecutorSummarizeRuntime.fs:23` → `HostForkRuntime.Fork`），而 N3 已让该入口把每条 fork prompt 交给 `ForkChildPayload.relay`。map/reduce prompt 因此早已以 `assignment` 字段抵达子会话。

强行再迁会造出嵌套 payload。实测让 `ExecutorSummarize` 自行渲染文档再作为 assignment 传入：

```text
# Complete the assignment in `assignment`.
# Report back with exactly these fields: …

assignment = '''
# Complete the assignment in `assignment`.      ← 记法出现两次
# Report back with exactly these fields: …
# Summarize command output chunk 0, …           ← 内层 instruction 落在外层 data 之下

assignment = "raw spool bytes"
'''
```

另一条路——给 `ForkChildAssignment` 加 `TaskInstructions` 字段让调用方传 typed instruction——试过并回退。F# record 的位置构造使新字段插在第二位，`(assignment, parentWorkRecord, requirements)` 的全部调用点静默变成「把 work record 当 instruction 传」且不抛错。要修得改整条 fork 签名链，而收益仅是把「Summarize chunk N」从 assignment 值提到 comment 里——ARCH-010 不要求这个层级的拆分，assignment 对 renderer 而言就是 data。

结论写进 `ForkChildPayload.render` 的注释，含上述两条实测，防止下一个人重做一遍。

##### 四条 instruction-only nudge

`next/Domain/RuntimeNudge.fs`：provider retry、manager review guard、reviewer verdict guard、missing final report。四者都是条款所称 instruction-only，故渲染为纯 comment header 无 body。

两处文本刻意不迁，理由写在模块头：

```text
ReviewChallenge.Text     REVIEW-003 把其 digest 记入 PerfectChallengeIssued 并在第二轮
                         input seal 里搜同一值。bytes 是领域事实而非渲染选择；包裹会改变
                         digest，令每次确认失败而外观上仍像正确的 fail-closed——正是该
                         文件自身注释警告的形态
零宽 "\u200B"            它的空即其含义。CompanionDelta.isBareContinuationMessage 靠剥掉
                         U+200B 后判空来把 continuation 归为 transport 而非语义 delta；
                         加 "# " 前缀会把一个传输 nudge 提升进 Companion 的语义历史
```

`missing-final-report` 的字段清单从 Markdown 项目符号改为单行，与 `ForkChildPayload.BaseInstructions` 一致：comment 里的项目符号渲染成 `# - result`，读起来像「关于一个列表的注释」而非列表。

##### surface-inventory 补 composerFiles

standing↔代码校验必须读「合成」的文件，而两条 nudge surface 的合成不在 send site：`HostSessionNudge` 只负责发送，`TurnCompletionProgram` 与 `HostReviewGuard` 才决定文本。只读 send site 会把这两条永久报为未迁移，而顺手的反应是改标签而不是去看散文实际在哪。两条红证：删掉 `composerFiles` 判红；把生产改回散文字面量判红并指出是哪个 composer 文件。

##### canary 未变绿，且这是预期的

N5a 后仍 6/16 绿。四条 nudge 都在流程后段才到达，而余下十条红各自在更早处停住，故迁移不改变结局。唯一位移是 pty-stress：由 `no-declared-turn`（devops turn 文本失配）前进到 `expectation devops.5`（第二个 PTY 的 trap TERM 行为），即从「文本类」跨到「行为类」。

`no-prefix-matched`（role=blogger）仍在，属 N5b。余下 `no-declared-turn` 一条为 `role=orchestrator`、`msgs=6`、候选仅 `manager-guard.2`——该签名在剧本改动前的首轮日志中即已存在，非本轮引入。

推论：包 N 对 canary 的贡献已经出尽。fork 信封那一根因消除后，剩下的十条都不是文本问题：

```text
0 条  N5b 阻断（X4 后半换币已完成；恢复侧 X-wire 仍未接线）
9 条  类二行为债，各自定位：fallback / fallback-aabb / reviewer-verdict 计数 /
      pty-stress PTY 行为 / host-restart / 两个 orchestrator / manager-full-loop /
      executor spool→summarizeSpool 未 fork
```

### 包 X：失败驱动上下文恢复

| 项 | 值 |
|----|----|
| 条款 | CTX-001…CTX-014 COMPANION-001…COMPANION-013 HOST-006 HOST-008 FALLBACK-011 FALLBACK-012 PROMPT-008 VERIFY-007 PERSIST-010 |
| 目标模块 | `Session/Companion*.fs` `OpenCode/CompanionTransform.fs` `Journal/CompanionProjection.fs` `Session/FallbackController.fs` `OpenCode/Projection.fs` + 新增 delta/TOML/probe 模块 |
| 设计定稿 | `STATUS/design-context-recovery.md`（归档原文，含设计演化与代价推理） |
| 依赖 | 退火一（生产编译恢复）。本包新增大量类型与 fold，不能在无编译反馈下写 |
| 生产 | 将改动 |
| 测试 | 将改动 |

本包替换的是既有机制的触发条件与投影内容，不是另建一套系统。modulo-4 cursor、PrefixEpoch、cutoff digest、synthetic 稳定身份、projection 分层、append-only journal 全部沿用。

#### 子步骤

```text
X0  Host 源码确认（先于任何实现，见下方清单）
X1  BlogFrame 数据模型 + BlogProjectionState + 三事实 fold（PERSIST-010）+ isValidTerminal
    第 1 层纯函数测试；第 0 层静态门禁
X2  静态灭绝旧机制（见灭绝表新增行）
X3  delta 链路：SemanticCursor、CoverableTurnCutoff 推进、200 KiB 三级切块器、
    确定性 TOML 发射器（CTX-013）+ 第 1 层测试
X4  Companion 正常投影 + system/normal/squash prompt + synthetic ID 公式（COMPANION-013）
    + 第 3 层 Fake Host 轨迹（busy skip、失败轮零帧、纯图片 turn）
X5  Y squash + armed-by-advance 控制流 + 三结局分派（CTX-007）
    + 第 3 层轨迹（squash 成功/失败、级联、预算耗尽）
X6  ManagedSessionKind + SessionAssociation（HOST-008）；全部工作角色懒创建 Y；
    Y 不递归；重启复用；X 删除时 Y 收敛
X7  Host compaction 预防层 + 收容层（HOST-006）：
    预防 = 关 auto/overflow/autocontinue/prune + 运行时启动探测
    收容 = 观察 pseudo-run → ContextReanchored 重锚（永远武装）
    此步未完成前不启用新 PrefixEpoch 逻辑
X8  X probe：候选选择（CTX-011）、cutoff proof、FrozenB blob、attempt profile、
    probe projection、promote/discard（CTX-012）、崩溃恢复
X9  删除旧实现：主动 PrefixEpoch 更新、Host compaction rebase、角色 eligibility、
    JSON Blogger delta、旧 Y transcript replay、错误字符串分类器、X 压缩请求
X10 Canary 验收（X-A 至 X-D，随包 K 一并落地）
```

X7 必须早于 X8：两套压缩系统同时活着时，无法判断 PrefixEpoch 的变化来自 probe 还是 Host。

#### X9 实测结果

编译绿、`test:mjs` 207/207、`gate:static` 全绿。生产文件 175 → 174（删 `Tools/MessageTransform.fs`）。

已清零的旧机制符号（`next/` 全仓计数为 0）：

```text
estimateTokens estimateTokensUtf8 shouldSwitchEpoch bloggerSelfRebaseDue
CompanionBudgetStore BudgetFacts SessionBudgets SessionOutputLimits
ActivePrefixEpoch(Session 侧) ReplacementActive TryEnableReplacement
FreezeEpoch SwitchEpoch TrySelfRebase TryRebase SelfRebase
shouldReplacePrefix compressPrefix compressPrefixText replacePrefix
bHeadDigest prefixDigest prefixLength latestBFor frozenBForProjection
```

连带删除的三处：

`systemTransformHook` 整体删除。 它的唯一职责是把 provider 的 `model.limit.context`
与 `.output` 抄进两个 Dictionary，即 CTX-001 禁止的观察本身。`experimental.chat.system.transform`
注册点随之删除——插件对 system prompt 没有其他要说的。

`Tools/MessageTransform.fs` 整体删除。 `replacePrefix` 的唯一消费者是
`Companion.compressPrefix`，`sanitize` 零调用点，`HostMessage` / `MessageWatermark`
是这两个函数的专用类型。新的前缀替换以消息位置计数表达（`XPrefixPlan.DropLeading`），
不再需要一个 `Index` 包装类型。`GuideContract/Signatures.fs` 与
`tests-next/Tools/MessageTransformTests.fs` 同步删除。

`SpikePlugin` 的 `backgroundBFor` 改为只读 `LatestB`。 原逻辑优先取 epoch 的
FrozenB。子会话的背景简报要的是当前记忆，更旧的冻结副本在这里从来不是更好的答案——
它出现在这里只因为 epoch 存在。

`domain.meta.test.mjs` 两处 fixture 换事实。 原本用 `CompanionReplacementActiveSet`
当任意事实样本，该事实已随双轨删除。换为 `CompanionBloggerClosed`。这暴露了 meta
测试的一个性质：它锁的是 facade 机制，不是某个事实，因此样本事实必须选长期存在的。

#### X4 后半已换币：旧 `jsonDelta` 入口灭绝

灭绝表把 `CompanionDelta.jsonDelta` 记为 X3（发射器就位）+ X9（删旧路径）。X4
后半已完成接线：`Companion.Submit` 接收 `ProviderSemanticProjection`，由
`BloggerDelta.nextChunk` 产生 `BloggerDeltaChunk`，成功结果携带完整
`BloggerCompletion`。

`ICompanionDurablePort.AppendSuccessful` 现在只接受 `BloggerCompletion`。唯一 durable
writer 先写 blob，再追加 `BlogEntryCommitted`，由 journal fold 同时推进 frame、cursor
与 coverage；不得用旧字符串或默认事实替代。

adapter 侧不缺东西：`OpenCode/Projection.decodeMessageView` 已能把 Host raw obj 变成
`ProviderWireProjection`，`ProviderProjection.toSemantic` 已能继续变成语义投影。缺的
只是 recovery-side projection 与 squash writer 尚未接线；这不属于本次 currency switch。
journal-less Companion producer 现在返回 `CompanionOutcome.DurableJournalUnavailable`，不调用
Blogger、不制造 `ProviderRunIdentity`、cursor、digest 或 blob 事实；PERSIST-009 的
`SHOCK-UNMIGRATED` 标记已在 `b48e38bd` 清零。

#### X9 留下的一个功能空洞

`CompanionHost.TransformRaw` 现在只做 COMPANION-005 累积并原样返回 `messages`，
X 前缀替换不生效。`Domain/XPrefixProjection.fs` 与 `Domain/AttemptPlanner.fs` 已实现
并有第 1 层测试，但没有接进 transform 边界；`b48e38bd` 只清除了 journal-less
Companion 的 PERSIST-009 休克标记，未声称 X-wire 已接线。

这不是降级而是 SSOT/12 的正确中间态：CTX-002 要求前缀替换在一次真实失败之后发生，
而 transform hook 看不到 attempt 结局，所以这个位置本来就不该做这个决定。没有已提交
的探针时，X 看到原始历史（SSOT/12「无 snapshot → 原始历史」）。

接线点属 X10 之前的一步，需要 attempt 结局能到达 transform 边界。

##### 包 K8f 摸底：X10 被这个空洞阻断（实测）

##### HOST-012 决策记录：多实例共享（2026-08-01 实测）

orchestrator-publish canary 修复推进中，deep-reviewer 的 verdict 必拒
（"manager session is unknown"）。DIAG 实测定位根因：Host `InstanceStore`
（`../opencode/packages/opencode/src/project/instance-store.ts` 的 `load`：
`cache.get(directory)`）按 directory 缓存实例——orchestrator 的 manager
worktree（`Path.GetTempPath()/wanxiangshu-{jobId}`，独立 git worktree project）
触发第二个插件实例（`initSpikePlugin` 实测两次，`input.directory` 分别为主
workspace 与 worktree；`ToolRuntimeScope` 构造两次，uid 不同）。主实例的
reverify fork 的 deep-reviewer 由 worktree 实例的工具处理，per-instance
`SessionParents` 读不到主实例的注册 → REVIEW-008 fail closed。

决策（用户确认）：插件状态跨实例共享——SessionParents / VerdictSessions /
SessionDirectories 改为模块级单例（HOST-012）。每实例独有：AgentJournal
（独立 runtimeId 文件）、Companions、OwnedSessions、订阅。

##### 包 K8f 摸底：X10 被这个空洞阻断（实测）

包 K8f 计划写 X-A 至 X-D 四条 canary 验收剧本。摸底测量后判定不可写，原因不是剧本
难写，而是没有可观察的生产行为：

```text
模块 / 事实                生产调用点或写入方    第 1 层 mjs 测试
XPrefixProjection          XWire.applyTransform  有
AttemptPlanner             XWire.applyTransform  有
PrefixProbeSelection       XWire.applyTransform  有
PrefixProbeSubmitted       无此事实              有
PrefixProbePromoted        无此事实              有
BlogSquashCommitted        0                     有
```

X 恢复模块已有 `XWire` 生产调用点：失败/中止在 reconcile 边界 arm，下一次 transform
调用 `AttemptPlanner.plan`，成功 reconcile 从同一 `ProviderRunIdentity` 提升
`PrefixRebaseCommitted`。`PrefixProbeSubmitted` / `PrefixProbePromoted` 不是当前
事实类型；提交是 attempt profile 的内存事实，提升由唯一的 `PrefixRebaseCommitted`
writer 记录。`BlogSquashCommitted` 仍无生产 writer。
`SHOCK-UNMIGRATED[CTX-006]: Blogger squash producer is absent; do not fabricate a writer.`

canary 是第 4 层：驱动真实 Host 并断言生产行为。链条未接线时，X-A–X-D 只有两种下场：
立刻红（什么都不发生），或者写成不断言任何东西——即包 K8d 刚淘汰的 `companion-cache`
同类物。在淘汰它的下一个包里重造一个，是本次迁移最该避免的事。

因此 K8f 的前置不是剧本，而是接线，属包 X 而非包 K：

```text
X4 后半   Submit 链路换币 ProviderSemanticProjection → BloggerDeltaChunk
          → BloggerCompletion → blob writer → BlogEntryCommitted
X-wire    attempt 结局到达恢复决策点：失败 attempt → AttemptPlanner.plan
           → attempt profile 携带探针 → Host 接受后提升（PrefixRebaseCommitted）
          → 恢复槽内 squash（BlogSquashCommitted）
```

在此之前 K8f 保持 pending 并标注阻断原因，不以「写了四条剧本」充作完成。包 K 的其余
部分（K9–K11、manager-tool-contract）不依赖这条链，先行。

这也修正了总表第 5 行的措辞：X0–X9 完成指的是删除与领域实现完成，不含接线。

#### X8 必须落地的门禁：零调用点的唯一构造函数

包 X5 补 `RequestKind` / `ProjectionChoice` 时发现 `buildAttemptExecutionProfile`
全仓调用点为 0。 包 0d 记录的措辞是「profile 尚未作为参数贯通全链（各处仍分别读
`ActiveLogicalRun`）」，听起来像覆盖不足；实际是没有任何一次 provider request
从这个 profile 出发构造。`conformance.md` 的 PROMPT-008 已从 `PARTIAL` 降为
`CONTRADICTS`。

`single-constructor` 门禁没抓住它，因为它问的是「谁在手工构造这个类型」，
而答案是没有人——包括本该构造它的那个函数的调用方。一个零调用点的构造函数
通过所有「不得绕过」检查，因为无路可绕。

门禁要加的三处改动，X8 落地时一并合入：

```text
SINGLE_CONSTRUCTOR_TYPES 每项增加 builder: 'buildAttemptExecutionProfile'
门禁在 owner 之外统计该标识符出现的生产文件数，为 0 则 fail
实测：加上后 architecture-gate 报 1 violation，指名该函数无调用点
```

已在本地写出并验证会红，然后回滚。现在不合入，因为门禁一旦为红就会阻塞 X6/X7
的每次提交，而它要求的贯通正是 X8 的第一个真实调用点（CTX-010 要求候选只对一次
attempt 有效，那是第一个真正需要完整 profile 的位置）。X8 完成时同时合入门禁与
调用点，一次转绿。

X6 与 X7 期间该函数保持零调用点是已知且已登记的状态，不是遗忘。

#### X2 的实测状态与一处未列入表的残留

灭绝表 13 行中的 17 个符号在包 A–H 完成时已全部为 0，无需再删：

```text
isCompanionEligible CompanionEligibility contextWindow maxContextTokens
remainingTokens contextRatio headroom nearLimit shouldCompact ensureCapacity
LatestBBytes OverflowPatterns OverflowDetected CompressionThreshold
SquashReason PrefixProbeRolledBack RestoreOldEpoch
```

但角色白名单本身仍在，以另一个名字：`Domain.PromptAuthority.hasCompanion`（`next/Domain/PromptAuthority.fs:528`）按 `CanonicalRole` 返回 bool，六个角色 true、四个 false。这正是 COMPANION-001 删除的东西——它没有进灭绝表，因为表是按旧符号名列的，而这个函数是包 B 期间新写的，当时的 COMPANION-001 还是白名单语义。

唯一消费者：`next/OpenCode/CompanionTransform.fs:104`。

它的删除归入 X6，不归 X2。 原因是替换物不是「删掉判断」而是「换一个问题」：transform 边界要问的不再是「这个角色配不配有 Companion」，而是「这个 Session 本身是不是 Companion Session」。后者需要 `ManagedSessionKind` 这个持久事实（HOST-008）才能 O(1) 回答；在没有该事实的情况下删掉 `hasCompanion`，只剩两条路——扫描全部 session 的 `Companion.BloggerSessionId` 找反向指针（违反 PERSIST-008），或在运行时内存里猜（违反 ARCH-002）。两条都是半状态。

因此 X6 的必须删除项追加：

```text
Domain.PromptAuthority.hasCompanion            及其 CompanionTransform 调用点
Kernel/Roles.fs:96-100 的 RoleDefinition 注释   （现在指向 hasCompanion 作为权威）
```

迁移前记录：`Session/CompanionDelta.fs` 的 `jsonDelta` 归 X3（TOML 发射器就位）与 X9（删除旧路径），不归 X2；X4 后半已完成该删除与接线。

#### X0：Host 源码确认清单（已完成）

结论全文：`STATUS/evidence/host-context-recovery.md`。绑定 Host `1.18.9`、本仓库 `cd1f8f09`。

| # | 待确认 | 结论 | 影响 |
|---|-------|------|------|
| 1 | transform 是否允许输出与物理 transcript 不同的消息集 | 可以，但只能就地修改数组 | frame 投影可行，有约定陷阱 |
| 2 | transform 是否允许输出连续 user 消息 | 允许，Host 侧无校验 | COMPANION-005 投影形状成立 |
| 3 | synthetic user ID 如何影响 assistant `parentID` | 完全无影响，且无 id 存在性校验 | COMPANION-013 公式安全 |
| 4 | 输出末条消息 id 是否必须等于物理末条 | 不必须 | 「delta 最后」的理由需改写 |
| 5 | transform 输入能否读 prompt metadata / request kind | 不能，输入是空对象 | `RequestKind` 由插件自己回答 |
| 6 | automatic compaction 的真实关闭位置 | `compaction.auto = false`，实例级 | 可关闭 |
| 7 | overflow compaction 是否经可拒绝 Hook | 无 hook，但 `auto=false` 转为终局错误 | 与 CTX-002 对齐 |
| 8 | manual compaction 是否能被全局阻断 | 不能 | 触发 SSOT 例外 1 |
| 9 | autocontinue 的真实调用路径与条件 | hook 可否决；`auto=false` 下 replay 分支不可达 | 可关闭 |
| 10 | 被投影省略的历史是否仍影响 Host 内部 | 触发阈值不受影响；`prune` 受影响 | `prune` 进必须关闭清单 |

第 8 项触发 SSOT 例外协议第 1 次，见 `STATUS/blocker-HOST-006.md`。HOST-006 由单层「全部禁止」改为预防层 + 收容层，manual `/compact` 成为官方支持用法。

X0 发现的两条实现约束，写代码前必读。

第一条会静默失效。 `experimental.chat.messages.transform` 的返回值在调用点被丢弃（`packages/opencode/src/plugin/index.ts:284-293`），Host 随后读的是原数组绑定（`prompt.ts:1262`）。因此 frame 投影只能就地修改数组（`splice` / `push` / `length = 0`）；写成 `output.messages = frames` 不报错、不抛异常，只是 provider 收到未修改的原始 transcript，而所有断言都会通过。这与 `tests-mjs/domain.mjs` 封死的三个陷阱同类，X4 必须有一个第 3 层轨迹专门证明「替换数组引用不生效」。Host 仓库内无该 hook 的测试或文档，因此不能假定它在未来版本保持不变。

第二条改变了一处设计理由。 COMPANION-005 的「delta 必须最后」原本的理由是「更容易保持 HOST-010 零例外绑定」，源码显示这不成立：`parentID` 与传给 processor 的 `user` 都来自 transform 之前算出的 `lastUser`（`prompt.ts:1096`、`1188`、`1273`），与输出顺序无关。该顺序仍应保留，但理由改为「让物理 delta 同时是 provider 看到的最后一条，避免 Host 与 provider 对本轮新内容产生两种答案」。

未解决项两条，登记在 evidence 文件末尾：`packages/core/src/session/runner/llm.ts:215` 的第二个 compaction 实现是否可达（处置：X7 的启动探测必须是运行时的，不能只读源码结论）；非 Anthropic provider adapter 是否合并相邻同角色消息（处置：包 K 待确认）。

#### 测试矩阵

第 0 层静态门禁（并入 `architecture-gate`）：

```text
无第二个 Fallback cursor writer     无 context window 查询
无 token 比例                       无主动 compact 判定
无角色 Companion 白名单             无 Host compaction fallback
无 PrefixProbe rollback 事实        无随机 synthetic ID
```

第 1 层纯函数：

| 组 | 断言 |
|----|------|
| TOML | 同一输入逐字节相同；CRLF→LF；三引号选择正确；args 递归排序；末尾一个 LF；图片无内容；200 KiB 上限精确；UTF-8 截断不破字符；marker 后仍可解析 |
| Fold | entry append 同时推进 coverage；squash 替换前 k 帧；squash 不改 coverage；PrefixRebase 只接受成功 probe；stale PreviousEpoch 拒绝；digest 不匹配拒绝；duplicate solving attempt 幂等 |
| Candidate | cutoff 非完整 turn → 无候选；digest 不匹配 → 无候选；coverage 不增长 → 无候选；候选不覆盖当前 physical user；image identity 差异导致 digest 差异 |

第 2 层资源合同：blob 先写后 event；CommitUnknown fail closed；Y session 创建幂等；X/Y dispose；PromptClaim 持久化 projection descriptor；orphan candidate blob 可清理。

第 3 层 Fake Host 轨迹：

```text
X  A 失败 → A′ probe 成功 → commit new epoch
X  A 失败 → A′ probe 失败 → B 必须使用旧 epoch
X  A′ 失败 → B 失败 → B′ 用等价候选成功
X  A 失败 → Y 无新 coverage → A′ 普通重试，不创建 epoch
X  probe Completed 但空 → repair 失败 → 不 commit
X  probe 成功后 crash → restart reconcile → 幂等 commit

Y  A 成功 → entry append
Y  A 失败 → A′ squash 成功 → main 成功 → squash + entry
Y  A 失败 → A′ squash 成功 → main 失败 → squash 保留，B 使用新 frames
Y  A 失败 → A′ squash 失败 → 不发 main，cursor 推进
Y  squash invalid → repair 仍 invalid → 用原 frames 发 main
Y  busy 跳过三个 turn → 下一 offer 覆盖全部未消化内容
Y  一个 turn 分三 chunk → 前两块只推进 IngestCursor，最后一块推进 CoverableTurnCutoff
Y  纯图片 turn → image_omitted → 正常推进 coverage

Session      全部工作角色创建 Y；Y 不递归；重启复用同一 Y；
             fallback Agent 改变不创建新 Y；X 删除时 Y 正确收敛
Compaction   预防：auto/overflow/autocontinue/prune 关闭后不再自行触发；
             启动探测在首轮出现 pseudo-run 时报 HostContractUnsupported
             收容：手动 /compact → 一次 ContextReanchored，epoch 退役、
             coverage 归零、Frames 全保留；同一 pseudo-run 重复观察幂等；
             重锚后新轮次重新累积 coverage，probe 能力恢复；
             compaction pseudo-run 不推进 cursor、不成为 Authority Root
```

第 4 层 canary（四条，随包 K 写成 TOML）：

| 剧本 | 构造 | 断言 |
|------|------|------|
| X-A：Y 级联 squash | mock 连续返回大 entry 直到普通 Y 请求失败 | 下一 armed 槽先收到前半 frames；squash response 成为新 frame；后续 projection 不再含被覆盖 frames |
| X-B：X probe 成功 | A 对原前缀失败，A′ 对 Y prefix 成功 | 下一请求沿用完全相同 SealRoot |
| X-C：X probe 失败 | A′ 使用候选 P 后失败 | B 请求中不得出现 P |
| X-D：图片 | 发送图片 + 文本 | X wire 中有图片；Semantic digest 可区分图片；Y TOML 无图片内容，只有 `image_omitted` |

#### armed-by-advance 验收 trace

本 trace 是 FALLBACK-012 的行为判据，X5 完成时必须逐行可复现：

```text
turn 1–3 成功：Frames=[R1,R2,R3]，Epoch=0，Offset=0

turn 4：delta4=180KB，1 块 → slot0(fast) Completed → commit R4
        Frames=[R1,R2,R3,R4]，Offset=0，count=0

turn 5：slot0(fast) Failed → Offset 0→1（armed 激活）
        slot1 armed → squash [R1,R2] 为 S1，Epoch=1，Frames=[S1,R3,R4]
        main wire=[S1][R3][R4][normal-instruction][delta5] → Completed
        → commit R5：Frames=[S1,R3,R4,R5]，count=0
        Offset 停放 1（成功不清零）

turn 6：从 Offset=1 起步，未武装
        → 首槽直接 main，不 squash        ← 关键：停放不触发压缩
        → Completed → commit R6，Offset 仍停放 1

后续某 chunk：从 Offset=1 未武装起步
        → slot1(fast) Failed → Offset 1→2
        → slot2(deep) 普通请求 Failed → Offset 2→3（armed 激活）
        → slot3(deep) armed → squash [S1,R3] 为 S2
          Epoch=2，Frames=[S2,R4,R5,R6] → 级联成立
```

turn 6 是本包最容易实现错的一行：只看 Offset 奇偶会让它 squash，从而每轮碾压一半帧。

#### 发布验收清单

规范（阶段 3.5 已完成，此处为回归检查）：

```text
[x] SSOT 不再出现主动上下文阈值
[x] SSOT 不再定义 Companion eligibility
[x] SSOT 明确所有 Work Session 有 Companion
[x] SSOT 明确 Companion 不递归
[x] SSOT 明确 Host compaction 预防层四项 + 收容层重锚
[x] SSOT 明确 X probe 成功后才 promote
[x] SSOT 明确 Y squash 有效后立即提交
[x] SSOT 明确图片内容不进入 Companion
```

实现：

```text
[ ] 无 context-window API 调用          [ ] 无 tokenizer 依赖
[ ] 无 provider 错误分类器              [ ] 无 X 摘要请求
[ ] Y delta TOML 不超过 200 KiB         [ ] A′ 失败后 B 使用旧 epoch
[ ] B′ 可独立重试等价候选               [ ] squash 成功 + main 失败后 squash 仍存在
[ ] Host compaction 不产生任何领域事实  [ ] 全部工作角色均创建 Y
[ ] 手动 /compact 触发一次重锚而非静默失效
[ ] 重锚后 coverage 重新累积，probe 能力恢复
[ ] compaction.prune 关闭已断言
[ ] Y 不创建 Y                          [ ] 图片二进制/URL/hash 不进入 TOML
[ ] Prompt 全部经 PromptDispatcher      [ ] Fallback cursor 只有一个写入口
```

恢复：

```text
[ ] completed entry 可在重启后补提交    [ ] completed squash 可在重启后补提交
[ ] successful probe 可在重启后 promote [ ] failed probe 不产生 rollback
[ ] CommitUnknown fail closed           [ ] unresolved prompt 不自动重发
```

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

### 上下文恢复侧（包 X，X2 步测量；X9 步复测）

基线未测量：这些符号在包 X 开始时才第一次全仓统计。`shouldCreateCompanion` 已在包 E 清零，此处不重复。

`next` 列为 X9 完成后的实测值。

| 旧符号 / 行为 | 新语义 | 条款 | next | 目标 |
|--------------|--------|------|------|------|
| `CompanionEligibility` / `isCompanionEligible` | Session 种类不变量 | COMPANION-001 | 0 | 0 |
| 角色 Companion 白名单（任何以 Role 为输入的 Companion 判定） | `ManagedSessionKind` | HOST-008 | 0 | 0 |
| `contextWindow` / `maxContextTokens` / `remainingTokens` | 无（不观察容量） | CTX-001 | 0 | 0 |
| `contextRatio` / `headroom` / `nearLimit` | 无 | CTX-001 | 0 | 0 |
| `shouldCompact` / `ensureCapacity` | 无（失败驱动） | CTX-002 | 0 | 0 |
| `estimateTokens` / `estimateTokensUtf8` / `shouldSwitchEpoch` | 无（X9 新增行） | CTX-001 | 0 | 0 |
| `CompanionBudgetStore` / `BudgetFacts` / `systemTransformHook` | 无（X9 新增行） | CTX-001 | 0 | 0 |
| `bloggerSelfRebaseDue` / `TrySelfRebase` / `SelfRebase` / `selfRebaseBlog` | 恢复槽内 squash | CTX-006 | 0 | 0 |
| `LatestBBytes` 阈值判定 | 200 KiB 输入合同 | CTX-003 | 0 | 0 |
| `OverflowPatterns` / `OverflowDetected` | 无（失败不分类） | CTX-005 | 0 | 0 |
| `CompressionThreshold` / `SquashReason` | 无 | CTX-005 | 0 | 0 |
| X 侧摘要/压缩请求 | Companion 工作日志本地替换 | CTX-009 | 0 | 0 |
| `PrefixProbeRolledBack` / `PrefixProbeCleared` / `RestoreOldEpoch` | 无（失败 probe 非事实） | CTX-010 | 0 | 0 |
| Host compaction → PrefixEpoch rebase 路径 | 收容层重锚（ContextReanchored） | HOST-006 | 0 | 0 |
| `switchEpoch` / `ReplacementActive` / Session 侧 `ActivePrefixEpoch` | 单轨 `PrefixEpochProjection` | COMPANION-009 | 0 | 0 |
| `bHeadDigest` / `prefixDigest` / `prefixLength` / `compressPrefix` | `XPrefixPlan` + `companionMemoryMessageId` | COMPANION-013 | 0 | 0 |
| JSON 形态的 Blogger delta（`jsonDelta`） | 确定性 TOML | CTX-013 | 0 | 0 |
| 物理 Y transcript 作为投影历史来源 | Journal fold 派生 Frames | PERSIST-010 | 0 | 0 |

X2 执行时注意不要误删与普通文件大小、进程输出预算（EXEC-011）有关的合法 byte 计数。判据是这个数字是否与模型上下文比较，不是它是否叫 bytes。

`jsonDelta` 已由 X4 后半清零。当前 Blogger normal 请求的货币是
`ProviderSemanticProjection → BloggerDeltaChunk → BloggerCompletion`；成功后由 durable
writer 追加 `BlogEntryCommitted`，否则 fail closed（COMPANION-005、PERSIST-009）。

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

已触发次数：2。

### supersedes 记录

| # | 条款 | 日期 | commit | blocker | 变更 |
|---|------|------|--------|---------|------|
| 1 | HOST-006 | 2026-07-30 | `cd1f8f09` | `STATUS/blocker-HOST-006.md` | 单层「全部禁止」改为预防层 + 收容层；manual `/compact` 成为官方支持用法，效果 best effort；新增 `compaction prune` 到必须关闭清单；启动门禁从静态配置读取升级为运行时探测；新增持久事实 `ContextReanchored`（PERSIST-010） |
| 2 | ARCH-009（新增） | 2026-07-30 | 本次 | 无（不是矛盾，是规范缺失） | 新增条款：业务层并发只允许有界 map；`maxConcurrency` 必须为正且拒绝非正值；结果按输入位置排列；取消在获取许可处观察且 token 传达到 action；拒绝不取消 siblings，许可必须在失败时归还 |

例外 1 的判据是逻辑矛盾，不是实现困难。 manual compaction 的完整路径（`groups/session.ts:303` → `handlers/session.ts:282` → `prompt.ts:1149` → `compaction.ts:513`）全程无 hook、无配置查询；唯一的 `experimental.session.compacting` 输出类型是 `{ context; prompt? }`，无否决字段，且 `plugin.trigger` 的返回值在调用点被丢弃。冻结版同时要求「必须关闭 manual」与「无法满足则启动失败」，两句连读要求插件在所有受支持版本上无条件启动失败。

修订未降低保护强度，三处提高了：一是 `compaction.prune` 此前未被点名，而它绕过投影边界直接删持久消息行；二是启动门禁从「读配置」改为「运行时探测首个 session 的 compaction pseudo-run 数为 0」，因为 `packages/core/src/session/runner/llm.ts:215` 存在第二个 compaction 实现，其配置来源与插件可写的那份不同，静态读取无法证明它没在跑；三是新增收容层，任何仍然出现的 compaction 都触发一次重锚，而冻结版对「万一还是发生了」没有任何规定。

收容层是主要防线，预防层是次要的。 预防层依赖 Host 的配置键名、hook 名与 `isOverflow` 短路位置，全部会随上游版本漂移；收容层只依赖「compaction pseudo-run 在 transcript 里可识别」，而 ARCH-003 禁止修改 Host、也无法钉住 Host 版本，因此耐用的那一层才该承重。

例外 2 是反向缺口：不是条款不可实现，是条款不存在。 包 T-5e 写 `Parallel.mapBounded` 的第 1 层测试时发现，这个跨领域共享原语的行为契约真实存在（并发上限、结果保序、取消传播、许可归还、拒绝后 siblings 的命运），而 `SSOT/` 没有任何条款管它。

例外协议第 3 步「证明是 Host 能力或逻辑矛盾」在此不适用，因为没有要修改的既有条款。走协议的理由是第 6 步：新增条款同样需要重新冻结，且不得由实现反向定义。因此 ARCH-009 的文字先于测试改名写定，而不是把已有测试的行为抄成条款。

一处需要明确的判断：`Promise.all` 的「拒绝不取消 siblings」写进条款，不是把实现细节升级成规范。判据是这条行为决定调用方的正确写法——把 reject 读作「全部工作已停止」会写出错误的清理逻辑，而语言语义不会阻止他。规范必须表达它，否则它只被一个测试锁住，而测试不是规范（见 AGENTS.md 第 1 节）。

反过来，`Promise.all` 用于实现有界原语本身不受 ARCH-009 约束——条款禁止的是业务层直接无界扇出，适配器内部为构造有界语义而使用它是实现自由。这条边界写在条款正文里，否则下一次读者会把整个 `Kernel/Flow.fs` 判成违规。


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

# Wanxiangshu.Next Agent DSL SSOT

本文件冻结用户最终架构裁决。实现、测试、迁移文档冲突时，以本文件为最高产品语义；AGENTS.md 只规定执行顺序与边界。

## 1. 模型可见 DSL

- Manager 只有 `fork`、`join`、`list`；不得获得 `read/write/edit/bash/glob/grep`。
- Orchestrator 只有 `fork`、`join`，只 fork ManagerJob。
- Reviewer 的 `verdict` 只接受 `PERFECT | REVISE`。
- 对 busy existing agent 的 `fork` 是同一 child session 的 fire-and-forget nudge，不返回 Busy，不由插件自建 prompt queue。
- PTY 也通过结构化 `fork`：创建用 `agent="pty"`，读写复用句柄，信号使用 `signal="TERM" | "KILL"`；禁止魔法字符串。

## 2. Structured Program DSL

实现使用单一闭包 Flow：

`Flow<'ctx,'error,'a> = 'ctx -> CancellationToken -> Task<Result<'a,'error>>`

用 `let! / do! / use! / match / while / 尾递归 / parallel` 表达程序。禁止 Flow AST、解释器、Workflow Engine、动态 Stage/Phase 注册表及把调用栈展平为持久状态机。结构化程序决定现在做什么；NDJSON 事实记录过去确实发生过什么；Boot Fold Projection 告诉程序重启后已知事实。

## 3. Companion 与 B 版

- X 每次请求模型前生成 canonical outbound JSON projection。
- Delta 是不可变 projection 与上次成功 projection 的 JSON 层差异；Y 忙时跳过且不推进基线，下一次空闲自然包含跳过内容。
- B 只包含 Y 的 assistant 正文，不包含 Y 输入、reasoning、工具 IO 或旧 B；Y 自身压缩时旧 B 作为输入，输出 B' 后旧 B 自然退出。
- X 接近上下文上限后启用 remembered prefix replacement；以后每次 projection 都继续替换已覆盖前缀，未覆盖当前尾部不得丢失。
- Companion 只存在于 Manager、Coder、Orchestrator；Blogger、Executor、Inspector、Browser、Meditator、Reviewer 及其 child 禁止创建 sidecar。B 是认知缓存，不是控制事实；失败、延迟、崩溃不得阻塞 X。
- 官方 OpenCode compaction 关闭。
- 重启重置（reset）：Blogger child 在重启后由新 companion 重新创建，必须先用 FULL B + FULL 当前 projection 重新锚定（reset frame），再继续增量。重置采用「成功后才清除」语义——只有 terminal Completed 且 assistant 输出非空才清除 pending-reset 标志；send Error、Aborted、Failed、空输出均保留该标志，下一次 blog 重新发送 FULL reset frame（而非增量），B 与基线保持不变。
- reset frame 锚定「当前」projection（X 正要外发的那份），而非旧的 LastSuccessfulProjection；成功时 CompanionAdvanced(currentProjection, nextB) 持久化，使持久基线恰好等于被锚定的当前 projection。CompanionAdvanced 即原子化的「重置完成 + baseline 重锚」屏障：日记落盘后才算重置生效，重启可自该基线继续。
- X 前缀替换阈值 = 0.8 × X 会话模型预算（sessionBudgets[X]）。Y 自压缩（self-rebase）阈值 = 0.8 × Blogger 预算：取 Blogger child 经 system.transform hook 上报的自身模型上下文上限，未知时取保守默认 32000 token。两者相互独立，Y 自压缩不再要求 X 的 ReplacementActive；Blogger（廉价模型，预算通常更小）可在 X 之前先触发自压缩。

## 4. Fork、Run、Join

- 一个物理 Agent 同时最多一个活跃 Run。Idle existing child 的新 prompt/fork 才创建唯一新 RunId；busy existing child 的 nudge 归属于当前 active Run，不创建第二 RunId。
- 新 child：create → 注册 linkage → 安装本 Run terminal listener → send prompt → 返回 AgentId。
- idle existing child：安装新 Run listener，fire-and-forget send，建立新 completion。
- busy existing child：只向同一 child fire-and-forget 发送 nudge；不创建第二 Run、listener、Task 或 completion，不替换当前 active completion，立即返回 Nudged。
- terminal listener 以 per-Run watermark/边界提取本轮 assistant 正文；不得按 session 永久标记 terminal，不得返回全历史。
- Completion 先入 mailbox，`join()` 消费任意最早 completion；mailbox 为空时挂起等待下一项 completion，不按 AgentId 筛选，也不因暂时没有 ready 项返回 `EMPTY`。父 abort 传播 CancellationToken，清理所有 child Run 与 PTY；迟到 terminal 按 RunId 忽略。
- 重启恢复裁决（弱恢复，有意为之）：重启只从 AgentLinked 恢复 child linkage 与角色；崩溃时在途的 Run、listener 与未领取 completion 不恢复（禁止用 phase/generation/程序计数器事实跟踪「跑到哪」）。child transcript 由宿主持久化，父需要结果时的正确习语是 `Reuse(agentId, prompt)` 重新询问同一 child。通用 fork/join 不补发、不重放在途 prompt。Orchestrator 的 ManagerJob 不受此限：它有独立的 durable barrier 恢复（见 §9）。

## 5. 持久事实与 CQRS

保留 Event Sourcing、CQRS、per-runtime NDJSON；删除 Event-Sourced Workflow。

- 路径：`.wanxiangshu-next/runtimes/<runtime-id>.ndjson`。
- 每个 runtime 只写自己的 CreateNew 文件；每行包含 schema version、RuntimeId、LocalSeq、ObservedAt、Fact；写入 flush 后才 Fold 内存 Projection。
- Boot 先截取各文件稳定 byte frontier，再确定性归并、Fold；半行丢弃，中间损坏隔离来源；不实时 tail 其他 runtime。
- 可持久化：AgentLinked、CompanionAdvanced、PrefixReplacementEnabled、VerdictRecorded、GuardPromptAccepted、ModelAttemptFailed、ManagerJob/Candidate/Published 及外部 ID 引用。
- 不可持久化：Task、Channel、listener、Process/PTY handle、semaphore、BloggerBusy、ReviewPhase、FallbackStage、JoinOwner、NudgeLease、CompactionGeneration、调用栈。
- Projection 必须有界；历史留在 NDJSON，不复制成无限 list/map/set。
- 外部权威仍是 Git、OpenCode transcript、OS；Journal 不与它们建立第二真相。

## 6. Fallback

每 Session 累计失败，成功不清零：

`A(0) → A retry(1) → 永久切 B(2) → B retry(3) → SessionDead(4)`。

A/B 角色切换与失败计数必须持久化；禁止 AcceptanceUnknown/Reconcile、FallbackPhase、Governor、Lease 等旧状态机。

durable `FallbackFailureRecorded` 只能由 Host 显式 `session.status=retry`（携带稳定 message/attempt identity）写入；空轮或 XML-only terminal 属于交互修复，最多触发一次零宽 continuation，不进入 fallback 计数、不切换 A/B、不累积 SessionDead。

## 7. Process

- 唯一进程 deadline = `3 × estimated_running_secs`；estimate 可极大，不 clamp；超时 SIGKILL 进程树。SIGKILL 无法返回是实现 bug，不加第二层兜底 timeout。
- 所有者清理（PTY owner cleanup）是资源回收策略，不是第二个业务 deadline：发送 TERM 后等待固定宽限（`termToKillGraceMs = 500`，见 `Pty.fs`）再升级为 KILL，并 await 进程退出。宽限窗口是清理策略常量，与上面唯一的 3× 业务 deadline 互不相关。
- Medium 不限并发；Large 由进程级单一 semaphore 串行。
- 启动即无损 byte pump；总输出超过 `3 × estimated_output_bytes` 后流式 spool；spool 完成后按 200KB 分块，由无工具 Executor Agent map/reduce 摘要。200KB 是摘要块大小，不是触发阈值或总输出上限。

## 8. ReviewGuard

- `REVISE` 立即生效。
- 同一 Git tree 的第一次 `PERFECT` 只要求确认；第二次不同 ToolCallId 且 tree 未变的 `PERFECT` 才确认。
- tree 变化、REVISE、重复 ToolCallId 均使确认失效或被去重。
- Reviewer terminal 无 verdict 时 nudge 同一 Reviewer；Manager terminal 未满足双 PERFECT 时 nudge 同一 Manager。Verdict 记录 tree hash 并持久化，确认状态由 Fold 推导，不写 ReviewPhase。

## 9. Orchestrator

用户消息前目标工作区必须 clean。fork ManagerJob 自动创建仓库外 worktree；Manager 通过 ReviewGuard 获得初次双 PERFECT，生成 candidate；共享目标 ref 的 publish 过单一 semaphore，rebase 最新 HEAD，冲突交回同一 Manager；rebase 后重新双 PERFECT；最后 ff-only 发布、清理 worktree、join 返回 Published。Git 是权威，流程事实持久化，重启必须 reconcile。

### 9.1 屏障事实（barrier facts）
PublishChain 的每个阶段在写入副作用前，先检查该阶段的「屏障事实」是否已在当前 HEAD 上落库；若已落库则跳过副作用，使恢复 = 重跑整条链、已完成阶段自跳过。新增持久事实（旧 journal 兼容，Thoth.Auto 自动编解码）：
- `OrchestratorPreRebaseReviewConfirmed { ManagerId; CandidateId; CommitHash }`
- `OrchestratorRebased { ManagerId; CandidateId; RebasedCommit }`
- `OrchestratorConflictDetected { ManagerId; CandidateId; Files }`
- `OrchestratorPostRebaseReviewConfirmed { ManagerId; CandidateId; RebasedCommit }`
- `OrchestratorPublishClaimed { ManagerId; CandidateId; ExpectedTargetHead }`

ManagerJob 投影增加对应 option 字段，fold 以「最新写入者胜出、按 commit 身份（CommitHash/RebasedCommit）为键」更新；因此一次重跑中若 HEAD 已变化（rebase 到新 target），陈旧的屏障绝不会匹配新 HEAD，阶段必然重跑——这是安全性的根基。

### 9.2 阶段语义（idempotent stages）
a. ReconcileTarget：对齐目标 ref。
b. PreRebaseReview：若 `PreRebaseReviewConfirmed.CommitHash = ReadHead` 则跳过；否则 ReverifyTwice → 追加该屏障事实。
c. CandidateRegister：若 `job.CandidateCommit.IsSome` 则复用并跳过；否则 ReadHead → 追加 `OrchestratorCandidateRegistered`。
d. Rebase：若 `Rebased.RebasedCommit = ReadHead` 且不存在 `REBASE_HEAD` 则跳过；否则 `git rebase target`。冲突时追加 `ConflictDetected(files)`，用同一 Manager session 的 `[CONFLICT RESUMPTION]` 提示继续（提示构造提取为单一函数，chain 与 host recovery 共用），manager 结束后 finalizeWorktree 续接 rebase，再追加 `Rebased`。
e. PostRebaseReview：若 `PostRebaseReviewConfirmed.RebasedCommit = ReadHead` 则跳过；否则 ReverifyTwice → 追加。
f. PublishClaim：读取 target head，追加 `PublishClaimed(expected)`（对同一 head 已 claim 则跳过，由 9.3 的 CAS 强制）。
g. FfMerge CAS：`GitPort.FfMerge` 增加 `expectedTargetHead` 参数；读取当前 target head，若 `expected = Some e` 而实际 ≠ e，则失败关闭并返回专属错误 `target ref moved`。
h. Published：追加 `OrchestratorPublished`（终端事实）→ 清理 `RemoveWorktree` 并 `git branch -D manager/<id>`；清理失败绝不使 publish 失败（终端事实已落库，残留交由 9.5 sweep 回收）。

### 9.3 收敛回路（convergence, not retry）
当 FfMerge 返回 `target ref moved`（外部 target 被移动），PublishChain 从阶段 (d) 重新驱动：在新 HEAD 上重跑 rebase 与 review。这不是盲目重试而是收敛——屏障按 commit 身份为键，上一轮的 `Rebased` / `PostRebaseReviewConfirmed` 屏障因 HEAD 已变而不再匹配，故必然重跑且只重跑必要的阶段；target 稳定后本轮 FfMerge 成功并落库 `Published`。回路设上限（实现常量）仅作防失控保险。

### 9.4 重启恢复与 adjudication
`engine()` 在 reconcile 后对每个持久 ManagerJob 调用 `RecoverManagerJob(id, worktree, prompt, job.CandidateCommit.IsSome)`；幂等链自跳过已完成阶段，故恢复即重跑整条链。
- 崩溃于 CandidateRegistered 之前：从原始 prompt 粗粒度重跑（旧 session 已死；worktree 可能含半成品，manager 的首要职责是与 target 对齐——拒绝 checkpoint-resume 作为阶段跟踪方案）。
- 崩溃于 rebase 中（存在 `REBASE_HEAD` 且无 candidate）：`engine()` recovery 循环经 `hasRebaseHead` + `ConflictedFiles` 侦测，改用 `[CONFLICT RESUMPTION]` 提示而非原始 prompt。
- 崩溃于 ff 之前 / Published 之前：链幂等补齐；`reconcilePublishedFromAuthority` 在 candidate 已合入 target 时补登 `Published`，无额外链副作用（回归守卫）。

### 9.5 启动时 sweep（git 权威 GC）
`engine()` 初始化（reconcile 之后）由 git 权威驱动垃圾回收：`ListWorktrees`（`git worktree list --porcelain`，解析出 `refs/heads/manager/<id>`）+ `ListManagerBranches`（`git branch --list manager/*`）→ 凡 manager id 不在活跃 ManagerJobs 中的 worktree / branch → `RemoveWorktree` + `DeleteBranch`。逐条 best-effort（失败跳过，引擎绝不阻塞——这是清理，不是正确性），由 SSOT 记录此行为。

### 9.6 锁规范化（lock canonicalization）
PublishLock.lockPath 由 repoPath 经 `git rev-parse --git-common-dir` 规范化为 git common dir（复用 RuntimePath 的解析，internal 可见），使 symlink 不同拼写的仓库共享同一把跨进程发布锁。临时目录位置与 proper-lockfile 选项保持不变；陈旧锁的 fail-closed 语义记录在案。

## 10. 角色能力

Manager/Orchestrator/Coder 有 Companion；其余角色无 Companion。Coder 可同步创建一次性 Inspector；Inspector 只调用 executor；Executor 与 Blogger 无工具。Browser 只读；Meditator/Reviewer 可 read/glob/grep/inspector，Reviewer 另有 verdict。

## 11. 验收阶段

依次闭合：真实 Host projection/child terminal → ForkRuntime fork/join/list/A 版/abort → Companion delta/B/replacement/restart → Reviewer/Fallback durable facts → Process/PTY → Orchestrator durable publish/rebase/re-review/ff → production entry → 删除旧实现。真实 Host 与 Manager→Coder→Join E2E 未通过前，不得宣称 release-ready、不得删除黑盒 Oracle 测试资产。

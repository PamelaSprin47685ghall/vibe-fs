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
- Delta 是本次 projection 与上次成功 projection 的 JSON 层差异；Y 忙时跳过且不推进基线，下一次空闲自然包含跳过内容。
- B 只包含 Y 的 assistant 正文，不包含 Y 输入、reasoning、工具 IO 或旧 B；Y 自身压缩时旧 B 作为输入，输出 B' 后旧 B 自然退出。
- X 接近上下文上限后启用 remembered prefix replacement；以后每次 projection 都继续替换已覆盖前缀，未覆盖当前尾部不得丢失。
- Companion 只存在于 Manager、Coder、Orchestrator；Blogger、Executor、Inspector、Browser、Meditator、Reviewer 及其 child 禁止创建 sidecar。B 是认知缓存，不是控制事实；失败、延迟、崩溃不得阻塞 X。
- 官方 OpenCode compaction 关闭。

## 4. Fork、Run、Join

- 一个物理 Agent 同时最多一个活跃 Run；每次 prompt/fork 有唯一 RunId。
- 新 child：create → 注册 linkage → 安装本 Run terminal listener → send prompt → 返回 AgentId。
- existing child：为本次 nudge 重新安装 listener，fire-and-forget send，独立等待本 Run terminal。
- terminal listener 以 per-Run watermark/边界提取本轮 assistant 正文；不得按 session 永久标记 terminal，不得返回全历史。
- Completion 先入 mailbox，`join()` 消费任意最早 completion；无 active/ready 时返回 `EMPTY`。父 abort 传播 CancellationToken，清理所有 child Run 与 PTY；迟到 terminal 按 RunId 忽略。

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

## 7. Process

- 唯一进程 deadline = `3 × estimated_running_secs`；estimate 可极大，不 clamp；超时 SIGKILL 进程树。SIGKILL 无法返回是实现 bug，不加第二层兜底 timeout。
- Medium 不限并发；Large 由进程级单一 semaphore 串行。
- 启动即无损 byte pump；总输出超过 `3 × estimated_output_bytes` 后流式 spool。超过 200KB 按 200KB 分块，由无工具 Executor Agent map/reduce 摘要；200KB 是摘要块，不是总输出上限。

## 8. ReviewGuard

- `REVISE` 立即生效。
- 同一 Git tree 的第一次 `PERFECT` 只要求确认；第二次不同 ToolCallId 且 tree 未变的 `PERFECT` 才确认。
- tree 变化、REVISE、重复 ToolCallId 均使确认失效或被去重。
- Reviewer terminal 无 verdict 时 nudge 同一 Reviewer；Manager terminal 未满足双 PERFECT 时 nudge 同一 Manager。Verdict 记录 tree hash 并持久化，确认状态由 Fold 推导，不写 ReviewPhase。

## 9. Orchestrator

用户消息前目标工作区必须 clean。fork ManagerJob 自动创建仓库外 worktree；Manager 通过 ReviewGuard 获得初次双 PERFECT，生成 candidate；共享目标 ref 的 publish 过单一 semaphore，rebase 最新 HEAD，冲突交回同一 Manager；rebase 后重新双 PERFECT；最后 ff-only 发布、清理 worktree、join 返回 Published。Git 是权威，流程事实持久化，重启必须 reconcile。

## 10. 角色能力

Manager/Orchestrator/Coder 有 Companion；其余角色无 Companion。Coder 可同步创建一次性 Inspector；Inspector 只调用 executor；Executor 与 Blogger 无工具。Browser 只读；Meditator/Reviewer 可 read/glob/grep/inspector，Reviewer 另有 verdict。

## 11. 验收阶段

依次闭合：真实 Host projection/child terminal → ForkRuntime fork/join/list/A 版/abort → Companion delta/B/replacement/restart → Reviewer/Fallback durable facts → Process/PTY → Orchestrator durable publish/rebase/re-review/ff → production entry → 删除旧实现。真实 Host 与 Manager→Coder→Join E2E 未通过前，不得宣称 release-ready、不得删除黑盒 Oracle 测试资产。

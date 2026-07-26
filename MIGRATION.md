# Agent DSL Migration Ledger

本文件记录旧资产的迁移处置、行为接管证据和删除门槛。产品语义只以 `next/Doc/SSOT.md` 为准；`AGENTS.md` 只管理工程纪律和当前纠偏顺序。

## 冻结与纠偏锚点

- 旧 `src/`、旧 `tests/`、旧 integration、Mux/OMP/Mimocode 只作为黑盒 Oracle；新行为只能进入 `next/`、`tests-next/`、`testkit/opencode/`。
- 纠偏前冻结标签：`correction-freeze-e499e41d`。
- 细粒度历史锚点：`834cb579`。只作为符号级行为证据；禁止历史 checkout、ours/theirs、整文件覆盖或未审查 cherry-pick。
- 首个纠偏提交：`ae51da07`。它删除生产侧测试台账、全局 kill、传输重试/伪造失败、fail-open tree hash 和伪 PTY；保留并行 P0 与 scenario-local 2s Watchdog。

## 状态分类

| 分类 | 含义 | 规则 |
| --- | --- | --- |
| Keep TestKit | 独立 harness/环境/诊断资产 | 不 import `next/` 业务类型；每 scenario 自己拥有资源 |
| Port Behavior | 旧测试只提供外部行为场景 | 新测试以 SSOT Behavior ID 接管 |
| Obsolete | Stage/Phase/Lease/Owner/Todo/Methodology/fuzzy/Squad 等旧控制状态 | 不迁移实现或断言 |
| Frozen Oracle | 仍可对照的旧实现 | 不导入、不双写、不作为生产路径 |

## 迁移行为总账

| 行为 | SSOT 范围 | 现有接管 | 当前状态 | 删除门槛 |
| --- | --- | --- | --- | --- |
| AG-LISTENER-BEFORE-SEND | Fork listener 在 prompt 前安装 | `tests-next/Session/HostForkRuntimeTests.fs` | Port proof | 真实 Host 因果 E2E |
| AG-FAST-COMPLETION-NOT-LOST | completion 先入 mailbox | `tests-next/Session/ForkRuntimeTests.fs` | Port proof | deterministic fast terminal |
| AG-BUSY-NUDGE-ONE-COMPLETION | busy nudge 不替换 active run | `tests-next/Session/ForkRuntimeTests.fs` + `HostForkRuntimeTests.fs` | overlap barrier + mailbox count | 真实 child E2E 仍待补 |
| TESTKIT-SESSION-CREATED-NOISE | 未匹配的 session.created 不续命 Watchdog | `scenario-parallel.js` + `gate-testkit.mjs` | scenario-local diagnostics | 13-way P0 隔离门 |
| TESTKIT-SINGLE-REPEAT-LAYER | canary 只执行一次，重复由 runner 控制 | `agent-dsl-canary.mjs` + `run-canary-staggered.mjs` | runner proof | 默认/13-way P0 |
| BLOG-BUSY-SKIPS | Blogger 不阻塞主会话 | `tests-next/Session/CompanionTests.fs` | Port proof | explicit Blogger lanes |
| BLOG-B-ACCUMULATES | 普通 Blogger 回合累积 B | `tests-next/Session/CompanionTests.fs` | Port proof | restart/real Host proof |
| BLOG-CANONICAL-PREFIX | 同 ID 内容更新不属于已覆盖前缀 | `CompanionHostTests.fs` | Port proof | real Host part variants |
| BLOG-JSON-REMOVE | JSON delta 表达嵌套删除与数组缩短 | `ManagerCanaryTests.fs` + `CompanionHostTests.fs` | pure/Port proof | real projection deletion |
| BLOG-CHEAP-MODEL | Blogger 使用显式 configured model | `CompanionHostTests.fs` | Port proof | provider model snapshot |
| PROC-THREE-X-DEADLINE | 唯一 3× deadline | `tests-next/Process/ProcessBudgetTests.fs` | local proof | owned process-tree E2E |
| PROC-SPOOL-COMPLETE | 全输出 spool | `tests-next/Process/ProcessRunnerTests.fs` | local proof | bounded-memory stream proof |
| REV-DOUBLE-PERFECT | 同 tree 双 PERFECT | `tests-next/Session/ReviewGuardTests.fs` + `reviewer-verdict-canary.mjs` | Port + real Host terminal guard | 完整 Manager 审查工作流 |
| REV-DURABLE-NUDGE | Reviewer missing-verdict nudge durable、重启去重、发送失败不写事实 | `HostReviewGuardTests.fs` | Port + Journal proof | real restart canary |
| FB-A-A-B-B | session 累计 A/A/B/B | `tests-next/Session/FallbackContractTests.fs` | pure proof | provider request model sequence |
| ORCH-FF-ONLY | rebase 后复审与 ff-only | `tests-next/Integration/OrchestratorTests.fs` | Port proof | real Git worktree E2E |
| TESTKIT-LANES | scenario/session/role/turn/request-kind lane；真实 session/parent 绑定 | `gate-testkit.mjs` + P0 canaries | Gate + real-host harness proof | 生产语义仍逐阶段验收 |
| TESTKIT-CAUSAL-WATCHDOG | 2s watchdog 仅接受 blocking 因果进展 | `watchdog.js` + `watchdog-constants.js` + `gate-timeout-cases.mjs` | Gate + P0 proof | 每个新场景保持同一门槛 |

## 不得迁移

- 全局 FIFO、`allowOutOfOrder`、`allowBloggerRequests`、`allowTitleGeneration`、`allowSyntheticContinuations`、自动吞请求。
- 生产 `WANXIANG_RUN_ID`、ledger、全局 active process 表、全局 kill、跨 scenario 进程扫描。
- 传输层 prompt 重试、Transport 侧 fallback 计数、伪造 terminal。
- 把普通 Runner 包装成 PTY 的模型工具面。
- fail-open Git tree、无归属 cleanup、固定 sleep、以高次数重复掩盖竞态。

## 近期接管顺序

1. ✅ Manager→Coder→Join 的 child-created、write、terminal、join、terminal Host barrier。
2. ✅ Companion production projection/restart semantics：`CompanionAdvanced` 原子持久 B+baseline；真实 replacement restart 恢复完整 B 与 raw tail。
3. ✅ Reviewer terminal guard：Manager 无当前 tree 的双 PERFECT 会收到 durable guard；Reviewer terminal 无 verdict 会 nudge 同一会话并 durable 去重；abort terminal 不触发 guard/continuation。
4. ✅ Companion canonical prefix、JSON remove delta、cheap Blogger model 配置。
5. Fallback → Process → PTY → Orchestrator。

## TestKit 因果接管证据

- `StrictMockProvider` 读取 OpenCode `x-session-affinity` 与 `x-parent-session-id`。direct session 先由 scenario 绑定；child session 仅能由已绑定 parent 的 lane 首次认领，之后固定同一 session identity。
- title、synthetic continuation、Blogger、Executor、Reviewer 都必须显式声明 lane。没有 allow-list 或传输层自动响应；extra、missing、wrong-parent、ambiguous 和 lane FIFO 违规均为测试失败。
- `afterExpectation()` 在消费点同步注册合法后继，避免并发 child response 在 test continuation 之前到达的竞态。
- P0 维持 staggered parallel；Companion replacement 以一个明确的 busy Blogger response 验证 busy skip，而不是靠 timing 接受任意额外 Blogger 请求。
- Journal runtime files 位于 Git common directory；隔离环境不再在受测 workspace 创建 `node_modules`。这些是 harness/host 边界，不是 Orchestrator ff-only 发布证据。
- `HostEventRouter` 用 message ID 合并 `message.updated` 与 `message.part.updated`，兼容 `messageID`/`messageId`；真实 text/tool part 的 terminal 不会被误判为空轮，空 terminal 仍发送零宽续命。Manager terminal 仅在当前 tree 未确认时发出 durable ReviewGuard，Reviewer 无 verdict 会 nudge；`MessageAbortedError`/`session.aborted` terminal 抑制二者。provider 500 的 `session.status=retry` 每 attempt 只 append 一个 durable failure fact；fallback canary 以零延迟 retry、重启和累计事实证明该边界，不声称 A/A/B/B 模型切换。

旧资产只能在相应行为有新层级证据后删除。任何未列入总账的旧测试先分类，再决定保留、提炼或废弃。

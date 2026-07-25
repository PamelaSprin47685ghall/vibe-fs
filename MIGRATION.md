# Agent DSL Migration Ledger

本文件记录旧资产的迁移处置、行为接管证据和删除门槛。产品语义只以 `next/Doc/SSOT.md` 为准；`AGENTS.md` 只管理工程纪律和当前纠偏顺序。

## 冻结与纠偏锚点

- 旧 `src/`、旧 `tests/`、旧 integration、Mux/OMP/Mimocode 只作为黑盒 Oracle；新行为只能进入 `next/`、`tests-next/`、`testkit/opencode/`。
- 纠偏前冻结标签：`correction-freeze-e499e41d`。
- 细粒度历史锚点：`834cb579`。只作为符号级行为证据；禁止历史 checkout、ours/theirs、整文件覆盖或未审查 cherry-pick。
- 首个纠偏提交：`ae51da07`。它删除生产侧测试台账、全局 kill、传输重试/伪造失败、fail-open tree hash 和伪 PTY；保留并行 P0 与 1s scenario-local Watchdog。

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
| AG-BUSY-NUDGE-ONE-COMPLETION | busy nudge 不替换 active run | `tests-next/Session/ForkRuntimeTests.fs` | Port proof | barrier 真实 child E2E |
| BLOG-BUSY-SKIPS | Blogger 不阻塞主会话 | `tests-next/Session/CompanionTests.fs` | Port proof | explicit Blogger lanes |
| BLOG-B-ACCUMULATES | 普通 Blogger 回合累积 B | `tests-next/Session/CompanionTests.fs` | Port proof | restart/real Host proof |
| PROC-THREE-X-DEADLINE | 唯一 3× deadline | `tests-next/Process/ProcessBudgetTests.fs` | local proof | owned process-tree E2E |
| PROC-SPOOL-COMPLETE | 全输出 spool | `tests-next/Process/ProcessRunnerTests.fs` | local proof | bounded-memory stream proof |
| REV-DOUBLE-PERFECT | 同 tree 双 PERFECT | `tests-next/Session/ReviewGuardTests.fs` | Port proof | Manager finish guard E2E |
| FB-A-A-B-B | session 累计 A/A/B/B | `tests-next/Session/FallbackContractTests.fs` | pure proof | provider request model sequence |
| ORCH-FF-ONLY | rebase 后复审与 ff-only | `tests-next/Integration/OrchestratorTests.fs` | Port proof | real Git worktree E2E |
| TESTKIT-LANES | scenario/lane 内有序、lane 间可交错 | pending | next task | pure TestKit gate |
| TESTKIT-CAUSAL-WATCHDOG | 1s watchdog 仅接受因果进展 | pending | next task | scenario-local diagnostics |

## 不得迁移

- 全局 FIFO、`allowOutOfOrder`、`allowBloggerRequests`、自动吞请求。
- 生产 `WANXIANG_RUN_ID`、ledger、全局 active process 表、全局 kill、跨 scenario 进程扫描。
- 传输层 prompt 重试、Transport 侧 fallback 计数、伪造 terminal。
- 把普通 Runner 包装成 PTY 的模型工具面。
- fail-open Git tree、无归属 cleanup、固定 sleep、以高次数重复掩盖竞态。

## 近期接管顺序

1. TestKit scenario/lane expectation + causal Watchdog gate。
2. 一个 Manager→Coder→Join 真实纵切。
3. Companion explicit Blogger lanes。
4. Reviewer → Fallback → Process → PTY → Orchestrator。

旧资产只能在相应行为有新层级证据后删除。任何未列入总账的旧测试先分类，再决定保留、提炼或废弃。

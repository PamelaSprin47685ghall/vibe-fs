# Manager worktree durable ownership 证据

> 性质：ORCH-003 / ORCH-006 / ORCH-007 实现缺陷、根因与修复证据。
> 调查基线：`4c518e13bae5bf91fd067258a8dd0e638364306a`。
> 修复提交：`9fcaad24655c993ce2d1d845bbc0d9827d78db7c`。
> 日期：2026-08-01。

## 1. 失败现象

`orchestrator-publish` 在 pre-rebase review 中出现 `seal-undeclared`。同一 reviewer session
相邻两次 provider request 的 system prompt 只少了 worktree 内 `AGENTS.md` 注入块；失败时
worktree 目录已不存在。seal 正确拒绝了不再 append-only 的输入，本次未修改 seal 语义。

有效复现必须从仓库根运行并设置 `WANXIANG_RUN_ID`。从
`testkit/opencode/tests/` 运行会使 `resolvePluginPath` 按错误 CWD 解析，Host 不加载插件，所得
工具与 session 轨迹无效。

## 2. 否证 Host 自发删除

git wrapper 抓到同一毫秒对同一路径执行三次：

```text
worktree remove --force /tmp/oc-e2e-.../tmp/wanxiangshu-<job>
branch -D manager/<job>
```

PPID 是 `opencode serve` 不能证明删除来自 Host workspace API：插件运行在 Host 进程内，
插件 `OrchestratorGit.run` 启动的 git 子进程同样以 Host 为父进程。

Host 源码中的 `Worktree.remove` 只能由显式 HTTP workspace/worktree 删除链到达；插件没有发出
该请求。删除命令的 cwd、branch identity 与插件 `GitPort.RemoveWorktree` / `DeleteBranch`
完全一致。

## 3. 根因

`ManagerJobCreated` 已成功持久化，job 仍为 active；但
`OrchestratorProgram.program` 用 `use!` 持有 `WorktreeResource`。任何程序返回——包括
`NeedsReview`——都会调用 `DisposeAsync → Release`，物理删除 active job 的 worktree 与 branch。

现场同时观测到三次删除。源码允许多个 `OrchestratorHost` 各自恢复同一 active job 并各自
`WorktreeResource.Adopt`；旧实现的 `released` 仅在对象实例内幂等，因此该链与三重现场一致。
正式回归不模拟三个 Host：它用一个真实 runtime 区分性证明一次 `NeedsReview` 已足以触发错误
析构；旧实现该用例中的 `RemoveWorktree` 计数观测为 3，但该数值本身不构成三个 Host 的直接
测试证据。

这不是 sweep identity normalization 缺陷：journal 中 `manager/<job>` 与 git porcelain 的
`refs/heads/manager/<job>` 经 `OrchestratorSweep.normalize` 后相等，active job 不会被 stale sweep
选中。

## 4. 修复

`WorktreeResource` 现在区分两种物理事实：

- `Create` 后、`ManagerJobCreated` 持久化前：资源归创建作用域；启动 Manager 或 append fact
  失败时，`DisposeAsync` 自动清理。
- `ManagerJobCreated` 成功持久化后：`MarkDurable()` 转移资源生命周期；普通作用域退出、
  `NeedsReview` 或恢复程序返回均不得删除。
- `Adopt`：恢复既有 durable worktree，天然不在 dispose 时删除。
- `Published` 与 terminal recovery 继续通过显式 `Release()` 清理。
- 无 journal 的测试/内存构造不调用 `MarkDurable()`，避免没有 durable record 却泄漏资源。

该表达保留结构化 `use!`，没有引入 `Owner` / `Lease` / `Generation` 程序计数器，也没有修改
OpenCode Host。

## 5. 验证记录

以下命令均从仓库根运行。未归档 stdout/stderr 原始文件者标为本地观测，不把它们作为
commit 内容本身。

| 性质 | 命令 | 结果 |
|------|------|------|
| 区分性红证 | 基线 `4c518e13` 发布产物上运行 `node --test tests-mjs/Orchestrator/runtime.test.mjs` | 本地观测：失败，`3 !== 0` |
| 修复后目标与相邻测试 | `node --test tests-mjs/Orchestrator/runtime.test.mjs tests-mjs/Orchestrator/job.test.mjs tests-mjs/domain.meta.test.mjs` | exit 0，43/43 |
| 静态门禁 | `npm run gate:static` | exit 0，6/6 子门禁 |
| reviewer restart | `WANXIANG_RUN_ID=puzzle-rr-final WATCHDOG_TIMEOUT_MS=20000 timeout 180 node testkit/opencode/tests/reviewer-restart-canary.mjs` | 本地观测：exit 0；原始日志未入库 |
| orchestrator restart | `WANXIANG_RUN_ID=puzzle-final-orp WATCHDOG_TIMEOUT_MS=20000 timeout 180 node testkit/opencode/tests/orchestrator-restart-publish-canary.mjs` | exit 1：`OrchestratorPublished=0` / target 缺文件 |

`tests-mjs/Orchestrator/runtime.test.mjs` 的
`ORCH_007_NeedsReview_preserves_the_active_worktree` 驱动真实 `Orchestrator`：

- fake Manager 成功启动并在 review 返回 `Error`；
- fake journal 确认 `ManagerJobCreated` append 发生；
- verdict 必须是完整 `NeedsReview`；
- `RemoveWorktree` 调用数必须为 `0`。

门禁红过一次：旧实现本地观测为 `3 !== 0`。修复后目标与相邻测试共 43 项通过；
`npm run gate:static` 六项通过；最终 diff review 无阻塞问题。

## 6. restart 剧本分流

两个 restart 初始红灯不是同一根因：

- `reviewer-restart` 缺少 Blogger restart re-anchor 的 `FULL PROJECTION:` turn；补齐后 canary
  已通过。
- `orchestrator-restart-publish` 缺少 deep-reviewer turn，并把普通 Blogger delta 与 restart
  re-anchor 混为同一声明；缺口已补齐。该 canary 现已越过 `no-declared-turn`，但 restart 后
  `OrchestratorPublished` 仍为 `0`、target 缺少 `publish_proof.txt`。这是新的 durable recovery
  blocker，不得把它写成此修复已通过或用放宽断言掩盖。

## 7. 剩余独立问题

journal 中曾观察到一个 ORCH-006 显式 barrier 与两个 REVIEW-007 随机 barrier。它解释了
review 请求数量，但不是 worktree 删除的原因。本次未改 barrier writer；后续须独立核实
Orchestrator runtime 的 `OpenReviewBarrier` 组合，避免把两个问题重新耦合。

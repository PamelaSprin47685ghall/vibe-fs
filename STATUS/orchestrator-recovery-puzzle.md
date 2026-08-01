# orchestrator-recovery-puzzle.md — worktree 删除调查结案与 recovery blocker 续工入口

> 性质：调查归档。非规范、非状态文档。
> 初始调查：2026-08-01。
> 根因修复：`9fcaad24655c993ce2d1d845bbc0d9827d78db7c`。
> 正式证据：`evidence/manager-worktree-durable-ownership.md`。

## 1. 结论

最初三个红灯已经分流：

| canary | 原失败 | 结论 | 当前状态 |
|--------|--------|------|----------|
| `orchestrator-publish` | `seal-undeclared`，worktree 消失 | 插件 `OrchestratorProgram` 对 durable worktree 错误执行作用域析构 | 根因已修复并有红绿回归；尚未取得该 canary 最终绿证据 |
| `orchestrator-restart-publish` | deep-reviewer `no-declared-turn` | TOML 缺少 ORCH-006 deep-reviewer 与 restart re-anchor 声明 | 声明缺口已闭合；现推进到真实恢复 blocker：restart 后 `OrchestratorPublished=0` |
| `reviewer-restart` | Blogger re-anchor `no-declared-turn` | TOML 把普通 delta 与 restart `FULL PROJECTION:` 混为一条 turn | 已修复，目标 canary 已绿 |

seal 没有过脆。它正确暴露了 active worktree 被删除后 Host system prompt 丢失
`AGENTS.md` 块。本次未放宽 `ProviderWireProjection` 或 seal。

## 2. 有效复现纪律

必须从仓库根运行，并设置 `WANXIANG_RUN_ID`：

```bash
cd "$(git rev-parse --show-toplevel)"
WANXIANG_RUN_ID=<run> WATCHDOG_TIMEOUT_MS=20000 \
  timeout 180 node testkit/opencode/tests/<name>-canary.mjs
```

从 `testkit/opencode/tests/` 运行会让 `scenario-parallel.js` 按错误 CWD 解析插件路径，Host
加载不到插件。此时默认工具集、tool unavailable、`bindChild` timeout 都是无效 harness
现象，不得作为产品证据。

## 3. 被推翻的归因

旧调查把 git 子进程 PPID 指向 `opencode serve` 当成“删除方在 Host 本体”的证据。该推论
错误：插件代码内嵌运行于 Host 进程，插件启动的 git 子进程同样以 Host 为 PPID。

Host `Worktree.remove` 的显式 HTTP 删除链与本次现场无关。删除命令实际来自插件
`WorktreeResource.Release()`：cwd、manager branch identity 与插件 `GitPort` 完全一致。

因此下列方向全部作废：

- 修改 Host 或走 ARCH-003 例外；
- 把 worktree 移到 Host “管不到”的路径；
- 给 sweep 增加 active session cwd 猜测；
- 放宽 seal 容忍 `AGENTS.md` 块消失。

## 4. 根因与修复

旧 `OrchestratorProgram.program` 用 `use!` 持有 `WorktreeResource`。程序返回
`NeedsReview` 时也执行 `DisposeAsync → Release`，删除仍有 `ManagerJobCreated` durable fact
的 active worktree。

现场同一毫秒观测到三次 `worktree remove` + 三次 `branch -D`。源码允许多个
`OrchestratorHost` 恢复同一 job 并各自 `Adopt`，与该现场一致；正式回归只直接证明
`NeedsReview` 触发了错误析构，不把计数 `3` 当作三个 Host 的独立测试证据。

修复把 ownership 转移绑定到物理事实：

```text
Create → ManagerJobCreated append 前失败：作用域析构清理
ManagerJobCreated append 成功：MarkDurable，作用域退出不清理
Adopt：既有 durable 资源，作用域退出不清理
Published / terminal recovery：显式 Release
```

无 journal 时不 `MarkDurable`，避免没有 durable record 却泄漏资源。

正式回归：`tests-mjs/Orchestrator/runtime.test.mjs` 的
`ORCH_007_NeedsReview_preserves_the_active_worktree`。旧实现本地观测红为 `3 !== 0`，修复后与相邻测试
共 43 项绿。

## 5. restart 场景

### `reviewer-restart`

实际 prompt 是 Blogger restart re-anchor：

```text
You are the blogger of a coding agent session.
This session resumed after a restart ...
FULL PROJECTION:
```

它不是普通 `# The next user message ... [[item]]` delta。剧本现将二者拆成两个 `internal`
turn；目标 canary 已通过。

### `orchestrator-restart-publish`

实际缺失请求为 `deep-reviewer`，工具集：

```text
[glob, grep, inspector, read, verdict]
```

剧本已补 `barrier-reviewer` 与 Blogger restart re-anchor。`no-declared-turn` 已消失。当前失败
发生在更深层：restart 后 `join` 完成，但 journal 中 `OrchestratorPublished=0`，target 缺少
`publish_proof.txt`。下一轮应直接调查 restart boot 的 `activeJobs`、`recoveryAction` 与
`VerdictMailbox`，不要再改 TOML 匹配或 worktree ownership。

## 6. 独立异常

journal 曾有三个 `ReviewBarrierStarted`：

- `<job>:pre-rebase:0`：ORCH-006 显式 barrier；
- 随机 GUID ×2：REVIEW-007 guard barrier。

该异常不是删除根因。本次没有修改 barrier writer。后续独立核实 Orchestrator runtime 的
`OpenReviewBarrier` 组合；禁止用它解释已由 `DisposeAsync` 回归证明的物理删除。

## 7. 当前工作区与清理

tracked 调试插桩已全部还原：

- `next/OpenCode/OrchestratorGit.fs` 的 `WXDBG_GIT`；
- `testkit/opencode/canary-driver.mjs` 的全量 event/request/worktree dump；
- `testkit/opencode/strict-mock-provider.js` 的 `[SEAL-DIR]` 探针。

`.tmp-dbg/` 与 `.tmp-gitwrap/` 是未跟踪调查现场，不属于产品或正式测试。正式证据已迁入 `evidence/manager-worktree-durable-ownership.md`；确认无需本地复核后可人工删除。

## 8. 续工顺序

1. 为 `orchestrator-restart-publish` 记录 restart 前后同一 `ManagerJobId` 的 durable progress、
   `activeJobs` 与 `recoveryAction`。
2. 证明 restart 后为什么 `JoinPublished()` 得到空结果：job 未 active、恢复程序未启动，还是
   terminal verdict 未进入新 mailbox。
3. 把诊断永久化为正式 mjs/Host 轨迹测试；禁止只留临时 print。
4. 该 canary 绿后，依 VERIFY-002 继续单 canary → P0×3 → `test:release`。

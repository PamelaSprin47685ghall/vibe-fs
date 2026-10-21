# CUT-23 Git 工作树、委任与集成

## 子范围①：Git porcelain 读法（2026-10-21，eng-cut25g-goose）

范围：卡片顺序第一步「只读 Git 定位与工作树资源」里**不依赖 CUT-12/13 的那一半**，也就是 Git 自己打印的文本怎样读。
commission/manager 绑定、Clean/Integration Gate、冲突归属、崩溃恢复和工作树回收要等 CUT-12、CUT-13 DONE 再接续。

### 抽出了什么

`wanxiangli/git-porcelain`（库 `199b9e9`，发布 0.48.0 / `016e5f1`）是下面这些读法的唯一 owner。它不 spawn 进程，也不碰文件：

| 读法 | 原来的位置（几份） |
|---|---|
| `worktree list --porcelain` 按记录切分（path、完整 branch ref） | `WorktreeResource.parseList`（1） |
| `worktree list --porcelain -z` 的主工作树；裸仓、无法识别时失败关闭 | `RuntimePath.parsePrimaryWorktree`（1） |
| `branch --list` 去掉 `*`/`+` 检出标记 | `WorktreeCommands.listBranches`（1） |
| `refs/heads/` 归一、manager 分支判定、`manager/<job>` 命名 | `WorktreeCleanup.normalize`/`isManagerBranch`、`WorktreeCommands.identityOf`（3） |
| rebase 进行中的判据（只认 rebase-merge/rebase-apply，残留 REBASE_HEAD 不算） | `GitOperations.rebaseInProgress`、`OrchestratorGit.hasRebaseHead`（**2 份逐字重复**） |

主仓 `GitPorcelain` 模块（`Git/Subject.fs`）只把库的返回值转成 F# 值。两份 rebase 判据合并成 `WorktreeCommands.rebaseInProgress`
一份：宿主只负责 rev-parse 和目录存在检查这两步 I/O，哪些探针输出算数由库决定。

### 反例（旧实现红）

旧 `parseList` 按 `[| '\n'; '\r' |]` 切行，CR 也被当成分隔符，于是 CRLF 列表里每条记录读完 `worktree` 行就提前结束，
`branch` 全部丢失（变成 `null`），清理扫描也就认不出自己的 manager 工作树。差分里旧实现在 2000 个 CRLF 样本中有 1170 个与 LF 结果不同；
库实现把 CR 当作行终止符的一部分，CRLF 与 LF 读出的结果相同（库测试 `SPEC[git-porcelain-1]`）。

### 证据

- 库：`src/git-porcelain/tests/001.test.mjs` 8/8 通过；`build-001` 隔离编译通过；打包后由消费方 import 冒烟通过。
- 差分 `evidence/CUT-23/differential-porcelain.mjs`：用同一个假 Git runner 驱动旧 dist 与候选 dist 的
  `WorktreeCommands.list`/`listBranches`，跑种子语料，LF 输出逐个比较；CRLF 单独统计；rebase 判据用真实目录核对。结果见 `differential-porcelain.json`。
- 定向测试：change-integration、crash-reconciliation、durable-events 003/009/010/011/016/028、host-boundary 026、work 011/048、delegation。

### 定向测试结果（诚实记录）

候选（master 184e84bc1 + 本刀）上定向套件：459 项，342 通过，24 失败，21 skip，39 todo。逐文件隔离重跑后，候选有 57 项 not ok（不含 TODO/SKIP），
与基线（同一 master + 已落地的 CUT-25，二者都不碰委派与 Git）逐文件隔离重跑的 57 项**名字完全相同**，
见 `evidence/CUT-23/targeted-failures-{candidate,baseline}.txt`（diff 为空）。这些失败集中在 delegation 006/023/024/025/026/027/029/031/032
（多为 "Promise resolution is still pending"）和 host-boundary 026「bookkeepertool must not acquire HostEventCodec.fs」，是 master 既有问题，
与 Git porcelain 读法无关：本刀既没修好也没弄坏。change-integration、crash-reconciliation、durable-events 003/009/010/011/016/028、work 011/048 全绿。
`scripts/checks/architecture.mjs` OK。

### 还没做（卡片保持 DOING）

commission/manager 绑定、Clean/Integration Gate、冲突归属、崩溃恢复、工作树回收依赖 CUT-12、CUT-13，两者尚未 DONE，留待接续。

## 子范围②：commission 绑定、Integration Gate、冲突归属、崩溃恢复、工作树回收（2026-10-21，eng-cut25i-goose）

CUT-12、CUT-13 已 DONE，接续卡片顺序第 2–4 步。

### 抽出了什么

`wanxiangli/integration`（新模块，发布 0.61.0 / `71f80c4`）是下面这些判定的唯一 owner；`git-porcelain` 同版本新增工作树回收的选择。
两者都不跑 Git、不读写日志、不持锁；主仓只做 I/O 与事务次序（创建/工作/清理/集成仍由 `OrchestratorProgram` 这个 C 执行）。

| 判定 | 原来的位置 | 库入口 |
|---|---|---|
| commission 工作树绑定（PERSIST-009：耐久意图 + 仅在 Requested 歧义时看一次完整物理观察） | `OrchestratorProjection.decideWorktreeReconciliation` | `reconcileWorktree` |
| rebased 候选 / publish claim 的现实分类（ORCH-007） | `OrchestratorProjection.classifyRebasedCandidate` / `classifyPublishClaim` | `rebasedCandidate` / `publishClaim` |
| 崩溃恢复入口：终态只清理、有 claim 重入、否则续跑 | `OrchestratorProgram.program` 的 match | `resume` |
| claim 重入：目标 ref、记录、rebased 钉是否仍同一提交与期望头 | `reentryClaimTargetVerdict`、`reentryRebasedMatches`、`continueReentryWith{Record,Rebased}` | `reenterClaim` |
| 候选准入：只有最新 rebased 记录恰钉本候选+本目标才发布，否则 rebase（TargetAdvanced / InitialRebaseRequired） | `admitPreparedCandidate` + `rebaseReason` | `admitCandidate` |
| 认证证据钉（先候选、后快照） | `publishCertified.certifiedEvidence` | `certify` |
| Integration Gate：证据归属 job、目标、CAS 头 | `publicationIdentityVerdict` | `checkPublication` |
| FF 结果：只有被钉候选算落地；声明的 target-moved 错误是可重试未命中 | `ffMergeVerdict` | `ffMerge` |
| 冲突归属：rebase 失败且有未合并文件 → 记为本 job 的冲突；无文件 → 不是冲突，失败 | `handleRebase.handleConflictFiles` | `rebaseFailed` |
| 工作树回收：哪些工作树/分支陈旧（主工作树、detached 永不陈旧；短名整体比较） | `OrchestratorSweep` 的 `normalize`/`isStale`/`isManagerBranch` 过滤 | `git-porcelain` `staleWorktrees` / `staleBranches` |

主仓侧：`Change/Projection.fs` 新增 `ChangeIntegrationLaw` 绑定（只做 F# 值 ↔ 库的 step/code 词汇转换），三处 Projection 判定与 `Program.fs`
七处判定改为问库；`Host/WorktreeCleanup.fs` 只剩按序删除。错误文案、调用次序、I/O 次数不变。
未迁：`OrchestratorProgram` 里的三处一行等值守卫（worktree HEAD 钉、发布前冲突非空、claim 期望一致）仍在宿主——它们夹在 I/O 中间，单独搬只会多一次 FFI；
`IntegrationGate`（proper-lockfile 跨进程锁）是物理资源适配器，不是规律，留在主仓。

### 诚实账

- 主仓本刀 +389/−193 行：判定逻辑移走了，但绑定的线格式转换比原先的模式匹配长。收益不在行数，在于这些判定现在有一个可独立编译、独立测试的 owner
  （`src/integration/tests/001.test.mjs` 6 项，含 4 组反例：换 job/换 path 的回执不收养、物理身份/路径冲突、外来落地不算成功、大小写不同的 moved 错误不算 TargetMoved；
  `git-porcelain` `SPEC[git-porcelain-2]`：主工作树永不陈旧、job id 前缀不算归属），宿主不再各自拼一份。
- 行为未改：没有修好也没有弄坏任何既有测试。

### 证据

- 库：integration 6/6、git-porcelain 9/9；`build-001` 两模块隔离编译通过；发布脚本定向 24/24 通过（`logs-cut25i/rel23b.out`）。
- 差分 `evidence/CUT-23/differential-integration.mjs`（旧 dist = master 1c1f5519f 构建，候选 dist = 本刀）：
  A. 三个纯判定各 20000 例（小字母表，等值大量碰撞）0 差异；B. `observeRelayProgram` 全部 22 个脚本化场景（fresh、冲突、目标移动、CAS miss、陈旧证书、12 种重入…）逐字节相同；
  C. `sweepStaleArtifacts` 2000 个随机世界（含主工作树、detached、`manager/j` 前缀、注入 list/remove/delete 失败）删除调用序列与结果 0 差异。结果见 `differential-integration.json`。
- 定向套件（change-integration、crash-reconciliation、managed-session-lifecycle、effect-accounting、durable-events 003/009/010/011/016/028、work 011/048、delegation）：
  675 项，504 通过，4 失败，23 skip，53 todo（`integration-targeted-summary.txt`）。失败文件逐个隔离重跑：候选 95 项 not ok 与基线 95 项**名字完全相同**
  （`integration-failures-{candidate,baseline}.txt`，comm 为空）：delegation 006/024/026/027/029/031/032、effect-accounting 002、managed-session-lifecycle 006–028 的 master 既有失败。
- `scripts/checks/architecture.mjs` OK。

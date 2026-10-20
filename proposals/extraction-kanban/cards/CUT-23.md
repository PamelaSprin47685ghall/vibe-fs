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

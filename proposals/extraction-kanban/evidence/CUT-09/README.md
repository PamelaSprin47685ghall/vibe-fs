# CUT-09 证据

返回 [卡片](../../cards/CUT-09.md)。环境：Linux，node v22.21.1，npm 10.9.4，负载共享机器（8 核）。原始日志在 box 的 `/workspace/vfs-cut/wt/logs-cut09b/`（不进仓）。

## 1. 两仓 SHA

| 项 | 值 |
|---|---|
| 库实现 | wanxiangli `994e683`（authority 律）、`28d3dcc`（类型化取字段 + build-001 覆盖） |
| 库发布 | `351e3c5` **0.28.0**（rebase 到 0.27.0 `c51f35e` 之上） |
| 主仓 base | master `13944a5db`（gitlink `a699b95`，0.25.1），干净 worktree，`node scripts/build.mjs` exit 0 |
| 主仓 candidate | base + 本卡接线（Run.fs/.fsi、Fold.fs）+ gitlink `351e3c5`，`node scripts/build.mjs` exit 0（含 js-surface-manifest、js-module-linkage OK） |

## 2. 库测试

| 命令 | 结果 |
|---|---|
| 全套 `npm run build && node --test --test-force-exit <scripts.test 全部文件>`（rebase 到 0.26.0 后） | 668 tests / 667 pass / 1 fail / 0 cancelled |
| 唯一失败 | `SPEC[build-001] sandbox compiles … without any sibling source`：sandbox 隔离构建输出 Fable 警告（unused value、obj 推断）。sandbox 源码本卡未改，origin/main 上同样失败，属于 CUT-25 的模块 |
| rebase 到 0.27.0 后的针对性复验（authority、projection + exports/package/repository/contracts） | 23/23 pass |
| build-001（projection、authority） | 2/2 pass，authority 隔离构建 0 警告 |

## 3. 套件（`node --test --test-concurrency=2 requirements/<suite>/tests/*.test.mjs`）

| 套件 | base | candidate |
|---|---|---|
| interaction-authority | 117 / 104 pass / 13 todo | 同 |
| crash-reconciliation | 69 / 52 pass / 2 skip / 15 todo | 同 |
| dispatch-protocol | 108 / 99 pass / 1 cancel / 1 skip / 7 todo | 同 |
| participant-identity | 52 / 51 pass / 1 todo | 同 |
| effect-accounting | 43 / 34 pass / 5 cancel / 3 skip / 1 todo | 同 |
| managed-chat-execution | 88 / 82 pass / 2 skip / 4 todo | 同 |
| degeneration-guard | 61 / 55 pass / 6 todo | 同 |
| execution-model-routing | 120 / 112 pass / 8 todo | 同 |
| managed-session-lifecycle | 175 / 92 pass / 1 fail / 69 cancel / 13 todo | 同 |
| delegation | 140 / 63 pass / 23 fail / 33 cancel / 2 skip / 19 todo | 同 |

两侧 `not ok` 用例名集合逐套件比较，完全相同（md5 相等）。fail 与 cancel 都是 base 已有的：cancel 来自负载机上的父级超时，delegation/managed-session-lifecycle 的失败与本卡无关。这些既有失败没有被藏起来，也没有算作通过。

## 4. 差分（[differential.mjs](differential.mjs)）

`node differential.mjs <base-root> <cand-root>` 在两侧各自用相同输入构造 40 个 profile（16 个 HumanRoot 根，24 个由 manager 根继承种子的 AgentOwnerRoot 子根）和 121 个投影（空、活跃、已关闭、带 pending/accepted claim），然后双算 `resolveAuthorityProfile`、`registerAuthority`、`closeAuthority`（含不存在的运行/空 id）、`closeCompletedAgentOwnerChildWork`、`resolveKnownOrigin`（物理 id × PromptKey × 压缩标志），以及经公开 `PromptFactFold.fold` 的 `AuthorityLogicalRunClosed` 重放（3 会话 × 全部运行 × 投影，外加无投影会话）。

```
profiles 40; projections 121; compared 24684; differing 0
```

base 上各函数族的结果分支都出现过：注册 Ok / ActiveRunIdentityConflict；关闭 Ok / mismatch / no matching authority；子工作关闭另含 non-Manager 拒绝；来源解析 HostInternal / UnknownOrigin / AuthorityRoot / Continuation；fold Ok[] / Ok / AuthorityLogicalRunClosureRejected。

### 4.1 变异（差分是否灵敏）

对 candidate 的 `wanxiangli/dist/authority/Authority.js` 逐个施加以下语义变异，跑完即恢复（恢复后用 `cmp` 核对）：

| 变异 | differing |
|---|---|
| 持久重放额外要求 LogicalRunId 相等 | 160 |
| 重放的种子比较恒真 | 2240 |
| 去掉 Host 压缩优先级 | 768 |
| 关闭不比 RootId | 364 |
| already-closed 报成 no-authority | 708 |

5/5 都被抓住。种子变异有 2240 处差异，说明规范 JSON 键确实能区分不同的种子。

## 5. 打包消费

`npm pack`（0.28.0）的 tarball 内含 `dist/authority/Authority.js`。在干净目录 `npm install <tgz> --omit=dev` 后执行：

```
consumer admit {"kind":"UnknownOrigin"}
```

即 `register(null, root)` 得到 `admit`；无任何证据的来源得到 `UnknownOrigin`，符合 D-01。

## 6. 静态检查

- `node scripts/check.mjs`：base 与 candidate 都 exit 1（既有问题）。去掉路径和行号后，发现集合相同；中途出现过 1 条新的 `match-pyramid`（`originOfView`），已拆成 `continuationOfLabel` 消除，重建后复核。shard-edge-audit 两边都是 251。
- `dotnet tool run fantomas --check` 对 Run.fs、Run.fsi、Fold.fs 均 exit 0。

## 7. rebase 复验（eng-cut09c-goose，2026-10-21）

- 候选 rebase 到 master `5232b0eba`（其 gitlink 为 `ece7176`/0.26.0），lockfile 冲突取 0.28.0，gitlink `351e3c5` 是 `ece7176` 的后代且在库 origin/main 上。
- `npm ci --ignore-scripts` exit 0；`node scripts/build.mjs` exit 0（725 emitted modules linked and loaded）。
- `node differential.mjs <13944a5db base> <rebased candidate>`：profiles 40，projections 121，compared 24684，differing 0。base 与 master 之间被差分的模块源码无变化。
- 10 个相关套件计数与 base 相同，失败/取消用例名逐一比较相同（delegation、managed-session-lifecycle、dispatch-protocol、effect-accounting）。

# CUT-16 证据（子范围①：ANCHOR 视图覆盖定律）

返回 [卡片](../../cards/CUT-16.md)。

## 1. 同树 base / candidate

同一工作树、同一 wanxiangli（`468991f` / 0.21.0）：base 为旧 Anchor 四文件构建，candidate 为接线后构建（`node scripts/build.mjs`，`js-surface-manifest`/`js-module-linkage` OK），Node v22.21.1，`node --test --test-reporter=tap <32 files>`。

相关文件（32 个）：advisor 002/003/006、capability-enforcement 026、context-compression 031/032、delegation 007–012/024/031、effect-accounting 008/013、intra-participant-parallelism 018、obligation-ledger 001–005、repository-programming 002/049/054/056/059/061/063/064、work 020/034。

| 项 | base | candidate |
|---|---|---|
| 顶层用例 | 168：137 pass / 8 fail / 5 cancelled / 15 skip / 3 todo | 同左 |
| 逐用例 `ok`/`not ok` 列表 | — | diff 为空 |
| 产物导入 | 内联折叠 | `import { Compression } from "wanxiangli/compression"` |

两侧相同的既有失败（非本卡引入）：

```
    not ok 102 - WHAT[repository-programming-002] the devops member documents the { ok, kind, status, view } result shape
    not ok 104 - WHAT[repository-programming-049] program, unpark and abandon are all optional so one state can be sent alone
    not ok 105 - WHAT[repository-programming-049] unpark.value is declared as an optional free-form JSON value
    not ok 107 - WHAT[repository-programming-049] the flat view parameters stay optional top-level arguments
    not ok 140 - WHAT[repository-programming-064] unpark alone still resumes the parked program
    not ok 149 - WHAT[work-020] a relaying tenure declaring view: { prefixUntil, thenMap } commits ViewCheckpointCommitted and carries the declared handover summary into the successor request
    not ok 53 - WHAT[delegation-024] pre-join resume preserves both public join delivered views
    not ok 54 - WHAT[delegation-024] an undelivered Completed terminal does not close the child and join stays not-ready
    not ok 55 - WHAT[delegation-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join
    not ok 56 - WHAT[delegation-024] Engineer reuse sends parent delta waits for its own root and returns its own child delta
    not ok 57 - WHAT[delegation-024] DevOps completion is claimed by HandleTurn and its delivered view returns to the caller
    not ok 60 - WHAT[delegation-031] DELEG_031_parent_supersede_leaves_no_orphan_completion_claim
    not ok 70 - WHAT[intra-participant-parallelism-018] a fission lane inherits the current session context instead of the old delivered record view
```

## 2. 差分（[differential.mjs](differential.mjs)）

把 base 与 candidate 的 `Anchor*.js` 以 `.base.js`/`.cand.js` 复制进同一 dist 目录（内部 import 改写，各自持有独立台账状态），对随机多会话（带/不带 id 的消息逐轮追加，合法与损坏声明：多余键、负/小数/字符串锚点、非规范键、空白/非字符串摘要、非对象）依次调用 `applyView`、`anchors`、`captureView`、`parse`、`reportMaterial`，比较 JSON 化结果。

```
seed 20261020: {"rounds":1500,"compared":90000,"rejectionsSeen":36672,"viewsWithSummaries":2299,"mismatches":0}
seed 7: {"rounds":1500,"compared":90000,"rejectionsSeen":36409,"viewsWithSummaries":2245,"mismatches":0}
seed 99: {"rounds":1500,"compared":90000,"rejectionsSeen":36716,"viewsWithSummaries":2170,"mismatches":0}
```

负控：把 base 的替换边界 `candidate > anchor` 改成 `candidate >= anchor + 2` 后，seed 7 / 300 轮即报 457 处分歧。

## 3. 库侧

wanxiangli `468991f`（0.21.0）：`node --test src/compression/tests/*.test.mjs scripts/tests/exports.test.mjs` 16 pass / 0 fail。

## 4. 未跑

全量 `scripts/verify.mjs`、集成 `run.mjs`（共享机资源）。

## 5. 落地前复验

rebase 到 master `636f8d847`（含 CUT-15 prefix、CUT-19 events/retention、CUT-25 沙箱与 wanxiangli/sandbox 0.23.1 gitlink）后，在 wanxiangli `d2a9f3b`（0.23.1，以 0.21.0 为祖先）上重新全量构建（build ok，manifest/linkage OK）并复跑 32 文件：168 用例 verdict 与 §1 base 逐条相同；产物 `Anchor*.js` 与差分所用 candidate 字节一致（md5 相同）；库侧 compression + exports 在 0.23.1 上仍 16 pass / 0 fail。

# 子范围②：view 检查点追加准入（eng-cut16d-goose）

## 6. 库侧

wanxiangli `5f15de3`（实现 + `tests/004.test.mjs`）、`a699b95`（release 0.25.1，以 master 当时 gitlink `40265dd` 为祖先）。`Compression().checkpoints()` 返回有状态台账：`observe({session, callId, status})` → `append`/`join`/`skip`，`settle(call, outcome)` → `settled`/`released`/`failed`。`node --test src/compression/tests/*.test.mjs scripts/tests/exports.test.mjs`：22 pass / 0 fail。

## 7. 同树 base / candidate

同一工作树、同一 wanxiangli `a699b95`、Node v22.21.1；base = 接线前 `PluginHooks.fs`，candidate = 接线后；两侧各一次全量 `node scripts/build.mjs`（build ok，manifest 151 / linkage 725 OK）。

- 单元层（[checkpoint-suite.files](checkpoint-suite.files) 共 72 个文件：所有提到 `ViewCheckpointCommitted`/`todowrite-for-` 的测试 + context-compression 全部）：两侧 450 用例均为 378 pass / 9 fail / 16 cancelled / 32 skip / 15 todo，逐用例 verdict diff 为空（[checkpoint-suite.verdicts](checkpoint-suite.verdicts)）。另与接线前、旧 lib（`5319733` on 0.23.2）上的 base 也逐条相同。
- 集成层：obligation-ledger 005 等在默认层是 SKIP，因此另以 `WXS_TIER_INTEGRATION=1` 跑 obligation-ledger 001–005 与 work 020/034：两侧 42 用例均为 40 pass / 2 fail，verdict 相同（[checkpoint-integration.verdicts](checkpoint-integration.verdicts)）；两处失败（obligation-ledger-002 已装宿主、work-020 接力）base 同样失败，非本卡引入。candidate 集成层所用 dist 为 base 全量构建后替换 candidate 的 `PluginHooks.js`（唯一改动的源文件）。
- 产物：candidate `dist/OpenCode/Plugin/PluginHooks.js` 实际 `import { Compression } from "wanxiangli/compression"`。

## 8. 负控（证明宿主真的走库）

在 candidate 上逐个改写 `wanxiangli/dist/compression/Compression.js`，跑集成层 obligation-ledger 005（[checkpoint-mutants.txt](checkpoint-mutants.txt)）：

| 变异 | 被抓到的用例 |
|---|---|
| M1 `WriterUnavailable` 不释放 | 005 “WriterUnavailable releases the call…” |
| M2 `committed` 后清除 settled | 005 “exact completed terminal commits exactly one durable checkpoint” |
| M3 去掉在途 `join` | 005 “concurrent duplicate terminals share one append outcome…”、“WriteUnknown … never retried” |

还原后 8/8 pass。

## 9. 退出

主仓删除 `settledViewCheckpoints` HashSet、`removePendingViewCheckpoint`/`releaseUnattemptedViewCheckpoint`/`startTerminalViewCheckpoint`/`settleNewViewCheckpointTerminal`/`joinPending` 及其中的准入判断；宿主只保留 `appendViewCheckpoint`（真实 journal 写入）、`JournalAppendFailure`→结局名映射与在途 `Task` 字典（等待者会合）。

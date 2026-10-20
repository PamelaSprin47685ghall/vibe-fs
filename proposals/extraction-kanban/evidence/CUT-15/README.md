# CUT-15 证据

返回 [卡片](../../cards/CUT-15.md)。

## 1. 两仓 SHA

| 项 | 值 |
|---|---|
| 库 prefix | wanxiangli `7aac7b8`，随 `770eea6`（0.10.0）发布；主仓 gitlink `71958aa`（0.18.0）包含它 |
| 库测试 | `node --test src/prefix/tests/*.test.mjs`：19/19 pass |
| 主仓 base | master `6859577a7`，`node scripts/build.mjs` exit 0 |
| 主仓 candidate | base + 本卡，`node scripts/build.mjs --clean` exit 0（Node 22.21.1） |

## 2. 套件（`node --test --test-concurrency=2`，prefix-stability、context-compression、host-boundary、provider-projection、durable-events、crash-reconciliation）

| | tests | pass | fail | cancelled | skipped | todo |
|---|---|---|---|---|---|---|
| base | 749 | 683 | 2 | 8 | 21 | 35 |
| candidate | 754 | 688 | 2 | 8 | 21 | 35 |

两侧 `not ok` 名单逐条相同（host-boundary-023/026 环境性；prefix-stability-001 第一项取消并连带同文件；014 TODO）。candidate 多出的 5 项即 §3 反例，全部通过。
prefix-stability-001 跳过 "reused delegation" 后：base 7/7，candidate 7/7。

## 3. 反例在 base 上为红

把 candidate 的 `006.test.mjs` 放进 base 构建，仅跑 `CUT_15_*`：

```
not ok 1 - CUT_15_a_committed_compaction_line_advances_the_durable_epoch   (the fold must record the prefix epoch)
not ok 2 - CUT_15_a_replayed_compaction_line_does_not_clear_rebuilt_checkpoints
not ok 3 - CUT_15_the_gate_candidate_reads_the_committed_projection
not ok 4 - CUT_15_tenure_then_compaction_advance_in_sequence_and_a_repeat_tenure_is_absorbed
not ok 5 - CUT_15_a_non_successor_epoch_line_is_refused_by_the_fold
# pass 0  # fail 5
```

candidate：5/5 pass。

## 4. 差分（[differential.mjs](differential.mjs)）

`node differential.mjs <base-root> <cand-root>`：随机 wire（五类 part、身份字段、工具集、system；追加/截短/改写/身份变化）上的 `isAppendOnlyPrefix` 正反向与自比较 60000 次，`nextReanchor`（含重复 run、部分已处理）5000 次：

```
compared 65000; differing 0; wire append-true 37847
```

## 5. `node scripts/check.mjs`

base 与 candidate 都 exit 1（既有 154 项）。去掉路径与行号后发现集合完全相同；首版 candidate 新增的两处 control-pyramid（`Epoch.rejectionOf`、`HostCompactionGate.reanchorObserved`）已拆平后消失。

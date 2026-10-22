# CUT-30 证据

返回 [卡片](../../cards/CUT-30.md)。

## 1. 同树 base / candidate

同一工作树、同一 submodule（`a0f0de9`），只换回 `LoopDetector.fs` 旧文件重建作为 base（build generation 6），candidate 为最终格式化文件（generation 8）。

| 项 | base | candidate |
|---|---|---|
| `node --test requirements/degeneration-guard/tests/*.test.mjs` | 61 tests / 45 pass / 10 fail / 6 todo | 同左；逐用例 `ok`/`not ok` 列表 `diff` 为空 |
| `node scripts/check.mjs` | exit 1，154 项既有问题 | exit 1，154 项，去行号后集合 `diff` 为空 |
| `node scripts/wanxiangli-source.mjs check` | — | exit 0 |
| `node scripts/verify-package.mjs` | — | exit 0 |
| 产物导入 | 内部 `Dictionary` + `tryGetValue` | `import { Degeneration } from "wanxiangli/degeneration"` |

base 失败用例（candidate 相同）：006 ×2（晚到增量）、007 ×6（对账/中断）、009 LOOP_014、LOOP_019。

## 2. 差分（[differential.mjs](differential.mjs)）

把 base 构建的 `dist/Execution/Session/LoopDetector.js` 复制为同目录 `LoopDetector.base.js`，与 candidate 产物在同一随机操作序列上双算（真实 o200k 分词；空串、重复词、随机码点、混合文本、随机 reset），比较每步 `pushText` 返回的 State/IsAnomalous/计数/Step、`evaluate`、`trackedTokenCount`，以及 `classify` 在 min/max、两侧邻近、0、负数、1e9、NaN、±∞ 上的回答。

```
seed 20261020: {"rounds":400,"compared":7803,"exact":7803,"mismatches":0}
seed 7:        {"rounds":400,"compared":7621,"exact":7621,"mismatches":0}
seed 99:       {"rounds":400,"compared":7664,"exact":7664,"mismatches":0}
```

首轮差分发现库 `classify(NaN)` 抛出而旧实现返回 Normal；已在宿主 `classify` 保留历史回答后重建重跑，上表为修正后的结果。

## 3. 库侧

wanxiangli `a0f0de9`（0.12.0）：`node --test src/degeneration/tests/*.test.mjs scripts/tests/exports.test.mjs` 13 pass / 0 fail；`build.test.mjs` 中 `SPEC[build-001] degeneration compiles and runs its own tests without any sibling source` ok。

## 4. 未跑

全量 `scripts/verify.mjs`、集成 `run.mjs`（共享机资源）；Node 版本低于 engines。

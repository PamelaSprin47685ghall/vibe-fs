# CUT-37 证据

返回 [卡片](../../cards/CUT-37.md)。

## 1. 同树 base / candidate

同一工作树：base 为旧 `Manifest.fs` 构建，candidate 为接线后构建；落地前 rebase 到 master `6859577a7`（wanxiangli `71958aa` / 0.18.0）后再次全量构建（`node scripts/build.mjs`，`js-surface-manifest`/`js-module-linkage` OK）并复跑。

相关文件（13 个）：attention-regulation 001/002/003/006、concern-routing 003/007、distribution 006、execution-model-routing 010、feature-ablation 001–004、obligation-ledger 001。

| 项 | base | candidate（rebase 后） |
|---|---|---|
| `TESTS_MJS_FILES=… requirements/verification-system/tests/run.mjs` | 45 pass / 0 fail / 2 skip | 45 pass / 0 fail / 2 skip，13/13 文件完成 |
| `node --test <13 files>` 逐用例 | 46 条 verdict | 同左，`ok`/`not ok` 列表 diff 为空 |
| 产物导入 | 内部 `Dictionary` DFS | `import { Ablation } from "wanxiangli/ablation"` |

2 skip：obligation-ledger-001 view checkpoint、feature-ablation-002 execute gate，均为 integration tier 未开启。

## 2. 差分（[differential.mjs](differential.mjs)）

把 base 构建的 `dist/Ablation/Manifest.js` 复制为 `Manifest.base.js`，对随机清单（合法与损坏：缺/重主节点、坏父链、环、悬空边、未知边类）及随机三态映射同时调用新旧 `validateNodes`、`validateDag`，比较 Result 标签、错误分支与原因字符串。

```
seed 20261020: {"rounds":5000,"compared":10000,"errorsSeen":4480,"mismatches":0}
seed 7:        {"rounds":5000,"compared":10000,"errorsSeen":4487,"mismatches":0}
seed 99:       {"rounds":5000,"compared":10000,"errorsSeen":4399,"mismatches":0}
```

接手时发现差分生成器在所有包都缺主节点时会产出无端点的边（解码失败中止），已改为此时补入 `ghost` 端点后重跑，上表为修正后的结果。

## 3. 库侧

wanxiangli `6013c9a`（0.17.0）：`node --test src/ablation/tests/*.test.mjs scripts/tests/exports.test.mjs` 15 pass / 0 fail。

## 4. 未跑

全量 `scripts/verify.mjs`、集成 `run.mjs`（共享机资源）；Node 版本低于 engines。

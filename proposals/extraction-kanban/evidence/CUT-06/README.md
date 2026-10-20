# CUT-06 证据

## 1. 环境

- 主仓 base `993e93683`（认领后 `ef0476046`），candidate 见看板；wanxiangli `2564387`（0.9.0）。工作树干净（除本卡改动）。
- Node v20.19.2、npm 11.21.0、Bun 1.4.2、dotnet SDK 10.0.401（仅供 Fable 5.13.0 工具，未运行 `dotnet build`）。Linux x64，共享 8 核机器。

## 2. 库侧

| 命令 | 结果 |
|---|---|
| `node scripts/build.mjs delivery` | exit 0，无 Warning |
| `node --test src/delivery/tests/*.test.mjs` | 19 tests，19 pass，0 fail |
| `node --test --test-name-pattern="delivery compiles" scripts/tests/build.test.mjs` | `SPEC[build-001] delivery compiles and runs its own tests without any sibling source` ok |
| `node scripts/build.mjs`（全部 13 模块） | exit 0 |
| `npm pack --ignore-scripts` → 干净目录 `npm install --omit=dev` → `import 'wanxiangli/delivery'` | Node、Bun 均输出相同结果；`tsc --noEmit --strict --module nodenext` 消费 `index.d.ts` 通过；tarball（临时，未入 Git）含 `dist/delivery/Delivery.js` 与 `src/delivery/index.d.ts`，SHA-256 `41fa13a9…952d`（发布提交前的 0.8.0 版本号形态，仅作安装验收） |

## 3. 主仓侧

| 步骤 | 命令 | 结果 |
|---|---|---|
| 构建 candidate | `node scripts/build.mjs` | exit 0；1310 items；js-surface-manifest OK（151）；js-module-linkage OK（725 modules） |
| 构建 base（同工作树换回旧文件） | 同上 | exit 0 |
| 源依赖检查 | `node scripts/wanxiangli-source.mjs check` | exit 0 |
| 静态门禁 | `node scripts/check.mjs` | base FAIL 155 issues / candidate FAIL 154 issues，2 gates（control-pyramid、js-boundary-gate）；差集仅为 base 的 `DeliveredValues.fs:248 match-pyramid`，candidate 无新增 |
| 格式 | `dotnet tool run fantomas --check DeliveredValues.fs` | exit 0 |
| 相关回归 | `node --test --test-concurrency=4 requirements/repository-programming/tests/{002,030,054,056,059,060,061} delegation/024 effect-accounting/013 execution-model-routing/010 intra-participant-parallelism/{003,018} participant-horizon/011 semantic-trace/013 work/{027,033}` | base 与 candidate 均为 101 tests：84 pass、9 fail、5 cancelled、0 skipped、3 todo；逐用例（含子测试）裁决 diff 为空 |
| 新旧双算 | `node evidence/CUT-06/differential.mjs <root> <seed>`，seed 1/3/5/7/42/1234/99991 | 每个 seed 4000 步随机消息（含 AnchorLedger 材料、合法/非法视图、错 run、空白散文），全部结果与异常文本一致；每 seed 约 130 次渲染出 `[[returned]]` 材料、约 80 次一致抛错 |

既有失败（两侧相同，非本卡引入）：delegation-024 五项、intra-participant-parallelism-003/018 各一项、repository-programming-002 三项、work-027 三项与 work-033 文件级 cancelled，主要报 "Promise resolution is still pending but the event loop has already resolved"，对应 master 上交付语义 WIP（`c4991268c` / `task.md`）。

## 4. 未做

- `env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node scripts/verify.mjs`、集成 `run.mjs`、`verify-package.mjs`：未跑。原因：共享机器内存不足以与其他 worker 并行全量，且 master 本身已有上述既有失败；不宣称全量通过。
- 原始日志：`/workspace/vfs-cut/wt/logs-cut06/`（box 本机，不入 Git）。

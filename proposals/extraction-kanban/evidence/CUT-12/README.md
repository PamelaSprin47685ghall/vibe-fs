# CUT-12 证据

返回 [卡片](../../cards/CUT-12.md)。环境：Linux，node v22.21.1，共享 8 核机器。原始日志在 box 的 `/workspace/vfs-cut/wt/logs-cut12/`（不进仓）。

| 项 | 值 |
|---|---|
| 库实现 / 发布 | wanxiangli `ce67a86` / `80b0eaf` **0.44.0** |
| 主仓 base | master `2e99d58a1` 的已构建 dist（ChatExecution、Foundation、Participant、Authority/Origin、IdentitySeed 源码与当前 master 相同） |
| 主仓 candidate | master + 本卡接线 + gitlink `80b0eaf`，`node scripts/build.mjs` exit 0 |

- `node differential.mjs <base-root> <cand-root>` → [differential.out](differential.out)：7,656 次比较，0 处不同。
- 变异：`python3 mutants.py <cand>/wanxiangli/dist/chat-execution/ChatExecutionLaw.js <name>`（原地改写后恢复），7/7 被抓住：pre-start-completed 3、restart-other-run-keeps 12、terminal-replay-ignores-disposition 48、start-after-terminal 36、settle-completed-allowed 132、terminal-other-run-existing 96、settle-started 12。
- 套件：`TESTS_MJS_FILES=<38 files> node requirements/verification-system/tests/run.mjs`（managed-chat-execution/*、crash-reconciliation/*、delegation/028、host-boundary/026、structured-workflow/012）→ 177 pass / 1 fail / 15 skip / 20 todo；唯一失败 host-boundary-026（源码依赖图检查）在 master 源码上同样失败。
- 库：`node --test src/chat-execution/tests/*.test.mjs scripts/tests/exports.test.mjs` 10/10；完整 `npm test` 在本机跑到 ownership 后卡住被中止，且其中 `SPEC[build-001] sandbox compiles…` 报红，与本模块无关，未计作通过。

## 子闭环 1、3、4 与整卡复核（eng-cut12c-goose）

| 项 | 值 |
|---|---|
| 库 | association/recover：`c705bd2`、`714c0ae` / `6483534` **0.46.0**；attachment-lease：`1ae069c` / `0bf6295` **0.50.0**；（CUT-13 handle-lifecycle `61968a4` / `ea20725` **0.52.0**） |
| 主仓 base | master `b10da13c9`，四个改动文件取 master 原文后 `node scripts/build.mjs` exit 0，dist 另存 |
| 主仓 candidate | base + CUT-12 三个提交 + CUT-13 提交，gitlink `ea20725`，`node scripts/build.mjs` exit 0 |

- `node association-differential.mjs <base> <cand>` → [association-differential.out](association-differential.out)：4,800 次，0 处不同。
- `node recovery-differential.mjs <base> <cand>` → [recovery-differential.out](recovery-differential.out)：19,200 次，0 处不同。
- `node lease-differential.mjs <base> <cand>` → [lease-differential.out](lease-differential.out)：90,112 次（返回值与操作序列），0 处不同。
- 变异：`python3 mutants-12b.py <node> <base> <cand>` 8/8（[mutants-12b.out](mutants-12b.out)），`python3 mutants-12c.py <node> <base> <cand>` 6/6（[mutants-12c.out](mutants-12c.out)）。
- fantomas `--check` 四个改动文件通过（LinkageProjection 经 fantomas 只改空白）；`node scripts/checks/architecture.mjs` OK。

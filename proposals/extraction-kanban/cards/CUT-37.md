# CUT-37：消融、诊断与实测成本

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-37) / [证据](../evidence/CUT-37/README.md)。

## 1. 认领

- Owner：`eng-cut06-goose` 认领（`62e07b0c4`），中断后由 `eng-cut06b-goose` 接手同一工作树 `wt/CUT-06-main`、`wt/CUT-06-wxl`（分支 `cut/CUT-06`）完成接线与落地。
- Base：主仓 `6859577a7`（落地前 rebase），wanxiangli `71958aa`（0.18.0，包含本卡 `6013c9a` / 0.17.0）。
- 写集合：`src/Wanxiangshu/Ablation/Manifest.fs`（`.fsi` 不变）、`requirements/feature-ablation/tests/support/ablation-fixture.mjs`；wanxiangli `src/ablation/`（新）、`package.json`、`scripts/tests/build.test.mjs`、`src/AGENTS.md`。

## 2. 处置

| 职责 | 现状核对 | 去向 |
|---|---|---|
| 消融 DAG 定律：主节点唯一、slice 父链、`station-order`/`borrow` 环检测、三态边约束 | `Ablation/Manifest.fs` 内联实现（`Dictionary` DFS + 两个校验函数） | **抽出** → `wanxiangli/ablation` |
| 清单/配置集读取、Thoth 解码、指纹、环境变量覆盖、注册表组装 | `Ablation/Manifest.fs`、`Settings.fs` | 留主仓（产品资产与宿主 I/O） |
| 工具/事实/代理门禁投影（`Gate`、`ToolMap`、`FactMap`、`Resolution`） | `Ablation/*.fs` | 留主仓（依赖主仓工具名、事实标签与代理目录） |
| Host 诊断（`OpenCode/Host/Diagnostic`、`ReliabilityDiagnostics`） | 类型直接依赖 `ChatExecution`、`Provider.Attempt`、OpenCode 宿主 | RETAINED：宿主诊断面，归 CUT-34/35 的宿主边界，不另建库 |
| `scripts/diagnostics/collect-incident.mjs` | 开发者脚本，非运行时 | RETAINED：无运行时消费者，不打包 |
| 实测成本 | 无可证伪的优化目标 | 不新增库（CARDS.md 退出条件允许）；本卡只记录真实测量 |

## 3. B. 契约（wanxiangli/ablation 0.17.0）

`Ablation()` 返回 `{ validateNodes(nodes), findCycle(nodes, edges), validateModes(edges, modes) }`；各返回 `null` 或冻结的 `{ kind: "InvalidManifest" | "DagViolation", reason }`。只报第一处违约，原因文字与主仓原实现逐字一致；输入先完整校验（拒绝 accessor 字段与稀疏数组），未给出的节点按 `ablated`。详见库 `src/ablation/AGENTS.md`、`index.d.ts`，测试 001–003。

## 4. C. 反例

- 主节点缺失/重复、父链错误被放过 → 库 `ablation-001`，主仓 WHAT[feature-ablation-001]。
- 任一边类成环、`station-order`/`borrow` 三态前提被绕过 → 库 `ablation-002`（3×3 全组合），主仓 WHAT[feature-ablation-003]、[004]（override 不能绕过 DAG）。
- 调用方事后改动输入影响结果 → 库 `ablation-003`。
- 抽取后原因文字或首个违约漂移 → 新旧编译产物双算差分，要求 Result 标签、错误分支、原因全等。

主线行为本来正确，无可伪造的红；以正控 + 负控 + 差分作证。

## 5. D–H

- D：库 `2dfb15d`（实现 + 测试 + 隔离构建登记）、`6013c9a`（release 0.17.0，已在 origin/main）；库侧 `node --test src/ablation/tests/*.test.mjs scripts/tests/exports.test.mjs` 15 pass / 0 fail。
- E：submodule + `file:wanxiangli`；接线时 gitlink `c9a943d`（0.16.0）→ `6013c9a`（0.17.0）；落地前 rebase 时 master 已由 CUT-08 把 gitlink 推进到 `71958aa`（0.18.0，以 `6013c9a` 为祖先）并同步 lock，故本提交不再改 gitlink 与 lock，只在 0.18.0 上重建复验。
- F：`AblationManifest` 持有一个 `Ablation()` 实例，把已解码的节点/边/三态映射成纯 JS 记录交给库，违约映射回原 `InvalidManifest`/`DagViolation`；`.fsi` 与所有调用者不变；产物 `dist/Ablation/Manifest.js` 实际 import `wanxiangli/ablation`。
- G：主仓删除 `validateSliceNode`、`validatePackageMainNodes`、`Dictionary` DFS `detectCycle`、`validateEdge`（−142/+66 行）。隔离副本 fixture 改为链接已安装 `node_modules`，与真实安装同样解析 npm 依赖（原先副本只复制 `dist`，旧实现无外部依赖才碰巧可行）。
- H：见证据：相关 13 个测试文件 base/candidate 逐用例相同（45 pass / 0 fail / 2 skip）；差分 3×5000 轮共 30000 次比较 0 分歧，其中约 44% 为错误路径。

## 6. 已知不足

- 2 个 skip 为 integration tier 未开启（base 相同），不计为通过。
- 本机 Node v20.19.2 低于 engines `>=22.13.0`；全量 verify 与集成 run.mjs 未跑。
- 诊断/成本侧没有做可证伪优化；保留边界已登记，未声称性能收益。

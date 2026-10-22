# CUT-30：Rulebook、指导与退化观察去向（裁决卡）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-30) / [证据](../evidence/CUT-30/README.md)。

## 1. 认领

- Owner：`eng-cut06-goose`；仓主授权自治裁决（PROTOCOL："DECISION cards … decide autonomously"）；认领与裁决提交 `959f05643`。
- Base：主仓 `959f05643`，wanxiangli `5edd3c7`（实现时已 rebase 到 `54dcc34`）；工作树 `wt/CUT-06-main`、`wt/CUT-06-wxl`，分支 `cut/CUT-06`。
- 写集合：`src/Wanxiangshu/Execution/Session/LoopDetector.fs`（`.fsi` 不变）、`wanxiangli` gitlink、`package-lock.json` 中 wanxiangli 版本行；wanxiangli `src/degeneration/`（新）、`package.json`、`scripts/tests/build.test.mjs`、`src/AGENTS.md`。

## 2. 裁决（按 CARDS.md：分开盘点，不因共用 Blogger 合并）

| 职责 | 现状核对（master `959f05643`） | 裁决 |
|---|---|---|
| Rulebook 事实 / Enforcer | `src/Wanxiangshu/Enforcer/` 不存在 | REMOVED（仓主 `6eef89d1e`） |
| Blogger 诊断与压缩历史 | Blogger 已退役 | REMOVED（仓主 `6eef89d1e`） |
| guidance-delivery（指导投递） | `requirements/guidance-delivery/` 已删除 | REMOVED（仓主） |
| behavior-diagnosis（Frontier/Coverage） | `requirements/behavior-diagnosis/` 已删除 | REMOVED（仓主） |
| degeneration-guard 检测算法 | `LoopDetector.fs` 仍生效，WHAT 001–009 在 | **抽出** → `wanxiangli/degeneration` |
| 语料选择、包络派生、o200k 分词 | `#wanxiangshu-loop-detector-envelope` | 留主仓（产品资产，库只接收包络与 token） |
| 中断 / 续发 / 运行身份对账 | `OpenCode/Host/LoopSensor` | 留主仓，作宿主组合者（依赖 host 会话契约） |

理由：检测算法是纯数学（指数衰减加权相异计数、O(1) 更新、状态以词表为界、双侧分类），不依赖任何宿主类型，是可复用的 Mechanism；包络取决于产品语料与分词器，中断续发取决于宿主会话协议，二者都不是通用机制。前置 CUT-16/19 当初要求的是压缩/持久历史 owner；该历史已随 Blogger 退役，degeneration-guard 不产生持久事实（WHAT[002] 每 attempt 内存态，WHAT[008] 重启从先验开始），因此不再阻塞。CUT-03/05 已 DONE。

## 3. B. 契约（wanxiangli/degeneration 0.12.0）

`Degeneration(envelope)` 校验包络（`vocabularySize` 正整数、`0<lambda<1`、`min ≤ normal ≤ max` 有限数，拒绝 accessor 字段），返回 `{ detector(), classify(x), envelope() }`。

- `detector().push(tokens)`：整段先校验（整数且在词表内），坏段不改变统计；`replacement = 1 − λ^(step−prev)`（新 token 为 1），`count = λ·count + replacement`。
- 起点 `normalWeightedDistinctCount`、step 0；边界值本身判 Normal；`reset()` 回到先验。
- 详见库 `src/degeneration/AGENTS.md`、`index.d.ts`；测试 001–003（10 项）。

## 4. C. 反例

- 坏 token / 越界 token 污染统计 → 库 `degeneration-002`。
- 状态无界增长 → `trackedTokenCount ≤ vocabularySize`（库 `degeneration-003`，主仓 WHAT[002] 用例）。
- 边界误判 → 主仓 `003.test.mjs`（min/max 本身 Normal）。
- 抽取后数值漂移 → 新旧编译产物双算差分，要求逐位相等。

主线行为本来正确，无可伪造的红；以正控 + 差分作证。

## 5. D–H

- D：库 `1d484dd`（实现 + 测试 + 导出/隔离构建登记），`a0f0de9`（release 0.12.0）；`SPEC[build-001] degeneration` 与 `SPEC[build-008]` 通过。
- E：submodule + `file:wanxiangli`；本卡接线时 gitlink → `a0f0de9`（0.12.0）；落地前 rebase 时 master 已由 CUT-20/CUT-19 把 gitlink 推进到 `674bdfb`（0.14.0，包含 `a0f0de9`），故本提交不再改 gitlink 与 lock，只在 0.14.0 上重建复验；`wanxiangli-source check`、`verify-package` 通过。
- F：`LoopDetector` 持有一个按仓库包络构造的 `Degeneration` 实例；`Detector` = 库探测器 + 产生时的观测值，`.fsi` 与 `LoopSensor`/`LoopDetectorSurface` 不变；产物 `dist/Execution/Session/LoopDetector.js` 实际 `import { Degeneration } from "wanxiangli/degeneration"`。
- G：主仓删除 `LastSeenTokenStep` 字典、`pushToken`/`pushTokens` 折叠与分类比较；只剩包络导入、分词调用与类型映射。`classify` 对非有限输入保留宿主历史回答（NaN→Normal，±∞→越界侧），因为库对此快速失败而 JS 表面不应新增异常。
- H：见证据：degeneration-guard 61 项逐用例裁决 base/candidate 完全一致；差分 3×400 轮共 23088 次 pushText 逐位相等，0 分歧；check 门禁问题集合相同（154/154）。

## 6. 已知不足

- degeneration-guard 61 项中 10 fail（006 晚到增量、007 对账、009 LOOP_014/019）在 base 上同样失败，属 LoopSensor/对账的进行中工作，不是本卡写集合；这些用例不能证明检测接线以外的路径。
- 本机 Node v20.19.2 低于 engines `>=22.13.0`；全量 verify 与集成 run.mjs 未跑。
- 每进程一个包络单例，与原实现相同；按 runtime 注入属 CUT-35。

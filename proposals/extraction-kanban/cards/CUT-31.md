# CUT-31：Sphinx 与认识运行时去向 —— 裁决

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-31)。

- **裁决人**：eng-cut09g-goose，2026-10-21，按仓主“完全自主决定”的授权。
- **裁决**：**保留 Sphinx v2 产品，按层分别裁决**。不整体重写、不平行建第二套 Runtime。无宿主依赖、无生产消费者的数值算法插件抽入 wanxiangli；Core/Runtime/Persistence/Wire/Hosts 暂 RETAINED（理由见下），在 CUT-35 组装时复核。

## 依据（亲眼核对，master d57f886c5）

- `src/Wanxiangshu/Sphinx/V2` 共 66 个 `.fs`、约 11.8k 行：Core 3.8k、Runtime 1.8k、Persistence 1.4k、Composition 0.6k、Wire 0.25k、Hosts 1.0k、Plugins 2.9k。`requirements/sphinx-v2` WHAT 001–036 仍生效，MCP 入口 `dist/Sphinx/V2/ServeEntry.js` 由 `scripts/build.mjs` 强制产出，事件经 `Composition.Bind.rules` 进统一 EventStore。
- WHAT 009 要求“一个 Core reducer，一个 Runtime driver，一套持久状态”——现状已满足；Core 被 Runtime/Persistence/Composition 以 F# 类型直接消费，若搬到库只能经 JS 边界，会把 reducer 的判别联合拆成无类型对象，不是净改进（README §9）。
- 插件层逐个核对生产消费者（排除 `*/Surface.fs` 测试面）：
  - `Ordinal`（Model/Pairwise/Ranking/DesignCheck/Fit，约 860 行）、`Bayes/Exact`（270 行）、`AStar/Refiner`（325 行）、`Mcts/Refiner`（128 行）：**只 open System/Fable，零生产引用**，仅由 sphinx-v2 测试 014/023/025/026 经 dist 测试面调用。
  - `Questionnaire`、`Probes`、`Inquiry`、`Provider/Adapter`：Sphinx 协议、提示与 provider 结果适配，`Runtime/Surface` 使用 `DecisionModel`/`ProviderAdapter`。

## 各层裁决

| 层 | 裁决 | 理由 / 承接 |
|---|---|---|
| Plugins/Ordinal 似然（Pairwise：logSigmoid/logSumExp、BTL 与 tie-aware 似然、梯度；Ranking：Plackett–Luce、MaxDiff、复合对、Borda；WHAT 014/025） | **抽取** 子范围① → `wanxiangli/ordinal` | 纯数值、正确（与闭式一致），无宿主依赖；主仓测试面 `Ordinal/Surface` 改为委托库 |
| Plugins/Ordinal 拟合与设计检查（`Fit`、`DesignCheck`、`OrdinalModel`/`Ballot`） | **REMOVED**（子范围①同刀，不记为抽取成功） | 零生产引用、零测试；实测有缺陷：只做一步 Newton 却报告 `Iterations = maxIterations`、theta 不满足声称的 zero-sum、tie-aware 族与 beta 被忽略（违反 WHAT 027“只声明可证明的收敛”）；`connectivity` 对从未出现的候选报 `isolated: []`。搬进库会输出虚假契约。WHAT 024/025/027 仍生效，将来的拟合须按其重建 |
| Plugins/Bayes（Exact） | **抽取** 子范围② → `wanxiangli/bayes` | 有规格（WHAT 009/021/025/026）与测试面消费者（026）；纯数值，无宿主依赖 |
| Plugins/AStar、Plugins/Mcts | **REMOVED**（子范围②同刀，不记为抽取成功） | 2026-10-21 eng-cut09h-goose 复核后修订原“抽取”裁决：零生产引用，测试 014/023/025 只 import 不调用（零断言）；Mcts 只有节点统计助手，声明的 `ITransitionModel` 无任何使用者，并不存在搜索。把无人调用、无测试的代码搬进库不是净改进（README §9）。WHAT 026 中 A*/MCTS 的约束仍生效，将来真正接入时按其重建 |
| Questionnaire / Probes / Inquiry / Provider | RETAINED | Sphinx 协议与提示，产品专有 |
| Core / Runtime / Persistence / Composition / Wire / Hosts | RETAINED | WHAT 009 已是单 reducer/单 driver/单持久状态；跨 JS 边界拆分无收益；随 CUT-35 复核，CUT-36 第二宿主出现时再议 Hosts |

- （裁决时）CUT-13 尚未 DONE：本卡选的子范围只动 `Sphinx/V2/Plugins/{Ordinal,Bayes,AStar,Mcts}` 与 sphinx-v2 测试导入，写集合与 CUT-13 不相交。

## 反例（子范围①必须守住）

- 平局被当作半个胜场（tie 概率不随 kappa 变化）；kappa → -∞ 时不退化为 logistic；±1000 处产生 log(0)/NaN；部分序被当作 Plackett–Luce 完整序；复合对丢掉名次方向。
- 不得把“只走一步”的拟合包装成“迭代 maxIterations 次未收敛”。

## 不做

- 不改 Sphinx 事件词汇、NDJSON 字节与 MCP 工具白名单；不删除任何证书/证据概念。

## 子范围① 施工记录（Ordinal 似然 → `wanxiangli/ordinal` 0.56.0）

- **库**：wanxiangli `f7560fb`（模块 `src/ordinal`，`OrdinalLaw`：logSigmoid/logSumExp、directional 与 tie-aware 似然及梯度、Plackett–Luce、MaxDiff、复合对、Borda），发布 `6cec10d` = 0.56.0。库内 5 项自测与 build-001 独立编译（只含 `src/ordinal`）通过。
- **主仓**：`Plugins/Ordinal/{Model,Pairwise,Ranking,DesignCheck,Fit}` 共 10 个文件约 1000 行删除；`Ordinal/Surface` 的 `logSigmoid`/`logSumExp`/`compositePairs` 改为委托库；子模块指针与 lockfile 指向 0.56.0。
- **影子对比**（`evidence/CUT-31/shadow-ordinal.mjs`，旧实现取 master d57f886c5 的 dist，同种子各 100000 例）：9 个函数族 differences=0（`shadow-ordinal.out`）。
- **变异**（`mutants.txt`）：6 个库变异（logSigmoid 朴素式、logSumExp 漏常数、tie 分子用 1/2、位置项被忽略、败方梯度反号、Borda 计原始总分）全部被库测试杀死。
- **删除而非抽取的证据**（`probe-removed.mjs` / `.out`）：旧 `Fit` 无论 maxIterations=1/100/10000 都只走一步却报告相应迭代数、theta 不变且非 zero-sum、tie-aware 族被忽略；`connectivity` 对从未出现的候选 `z` 报 `isolated: []`。这些不是“已证明的收敛”，按 WHAT 027 不得搬入库。
- **回归**：sphinx-v2 套件在候选上与基线（pass 275 / fail 0）逐项计数一致（落地循环日志 `logs-cut09h`；落地复验见下文“关闭”与 `evidence/CUT-31/suites.out`）。

## 子范围② 施工记录（Bayes → `wanxiangli/bayes` 0.58.0；AStar/Mcts 删除）

- **库**：wanxiangli `fbdd509`（模块 `src/bayes`，`BayesLaw.infer/priorOnly`，类型化故障，冻结结果），发布 `2304096` = 0.58.0。库内 4 项自测与 build-001 独立编译通过。
- **两处有意修正**（库内测试钉住，影子对比单列为“declared”）：`DuplicateHypothesisKey` 报出真正重复的键（旧实现报第一个键，与重复无关）；`priorOnly` 与 `infer` 共用假设集契约（旧实现接受单假设、空键与重复键的先验）。同时删去旧实现中两个从未调用的重复私有函数 `groupDecision`/`combineGroup`。
- **主仓**：`Plugins/Bayes/Exact` 改名为 `Model`，只留 F# 记录与故障类型；`Bayes/Surface` 跨边界委托库并把结果译回 F# 记录；`Plugins/AStar`、`Plugins/Mcts` 共 8 个文件约 670 行删除，测试 014/023/025 去掉未用的 import，`scripts/lib/test-surface-scan.mjs` 去掉两条面登记。
- **影子对比**（`evidence/CUT-31/shadow-bayes.mjs`，旧实现取 master 的 dist，种子 31，50000 例，含非法先验/似然、缺键、多余键、空依赖组、同组多 id、1e-300 尺度）：`infer` 与 `priorOnly` 未声明差异均为 0，结果与故障载荷逐字节一致；declared 只出现在上述两处修正（`shadow-bayes.out`）。
- **变异**（`mutants-bayes.py` / `mutants-bayes.txt`）：8 个库变异（同 id 重复计数、朴素 log-sum-exp、似然上界放宽、接纳不合格因子、重复键报首键、priorOnly 跳过契约、不报告被丢弃者、零先验抬升）全部被库测试杀死。
- **回归**：sphinx-v2 与 js-semantic-surface 套件在候选上与子范围①的基线逐项计数一致（落地复验见下文“关闭”与 `evidence/CUT-31/suites.out`）。

## 关闭（eng-cut09k-goose 接手 eng-cut09j-goose，2026-10-21）

- **前置**：CUT-09/10/11/13/19 在看板上均为 DONE；两个子范围的写集合（`Sphinx/V2/Plugins/{Ordinal,Bayes,AStar,Mcts}`、sphinx-v2 测试导入、`scripts/lib/test-surface-scan.mjs`）与它们不相交。
- **落地复验**：落地循环 rebase 到 master `43226cb10` 后用 `node scripts/build.mjs --clean` 全量构建（避开 PROTOCOL 记录的 focused-build 编译顺序地雷；之后 master 若只做不相交前移，则只 rebase 并重跑推送前检查），sphinx-v2 + js-semantic-surface 合计 336 tests / 319 pass / 0 fail / 2 cancelled / 0 skip / 15 todo；推送前跑了 lockfile、gitlink 祖先关系与 `architecture.mjs`。推送为 `1b7b10c7d`。汇总见 `evidence/CUT-31/suites.out`。
- **既有失败（非本卡）**：`sphinx-v2/034` 的两项 Host 派发用例（transport receipt 保持 pending、非接受结果保留原 key）以 `cancelledByParent`（Promise 未决而事件循环已空）失败；在不含本卡的 master `01488ebe4` 全量构建上同样 5 pass / 2 cancelled，逐项同名；在本机现存 30 个各时期全量构建（最早 `959f05643`，早于 CUT-11/12 的派发改动）上也都是 5 pass / 2 cancelled，与 Ordinal/Bayes/A*/MCTS 无关（这两项走 `Interaction/Dispatch` 与 Host probe）。eng-cut09j 的落地循环因未设基线把它判为新失败而停下；接手者 eng-cut09k-goose 以 `wt/CUT-29-main`（master `01488ebe4`）为基线重跑落地循环 `logs-cut09k/loop31b.out`。该失败需另开修复，不藏在本卡 DONE 之下。
- **退出条件**（CARDS CUT-31）：已迁的数值层在主仓不再有第二份实现（Ordinal 似然与 Bayes 后验只归 wanxiangli，`Surface` 只做跨边界翻译）；Fit/DesignCheck/A*/MCTS 是删除，不算抽取成功，WHAT 024–027 对将来重建的约束仍生效；仍有效的 Sphinx 层（Questionnaire/Probes/Inquiry/Provider、Core/Runtime/Persistence/Composition/Wire/Hosts）逐层登记为 RETAINED，作为显式参与者随 CUT-35 复核。没有删除任何证书/证据概念，Sphinx 事件词汇、NDJSON 字节与 MCP 工具白名单未改。
- **未做**：全量 `scripts/verify.mjs`、integration tier。
- **状态**：CUT-31 `DONE`。

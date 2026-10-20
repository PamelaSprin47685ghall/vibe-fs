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
| Plugins/Ordinal（BTL / tie-aware 似然、Plackett–Luce/MaxDiff、连通性与设计秩、拟合与对比方差；WHAT 024/025） | **抽取** 子范围① → `wanxiangli/ordinal` | 纯数值，无宿主依赖；库内独立测试 + sphinx-v2 测试改从库导入 |
| Plugins/Bayes、AStar、Mcts | **抽取** 子范围②（① 之后） | 同上 |
| Questionnaire / Probes / Inquiry / Provider | RETAINED | Sphinx 协议与提示，产品专有 |
| Core / Runtime / Persistence / Composition / Wire / Hosts | RETAINED | WHAT 009 已是单 reducer/单 driver/单持久状态；跨 JS 边界拆分无收益；随 CUT-35 复核，CUT-36 第二宿主出现时再议 Hosts |

- CUT-13 尚未 DONE：本卡选的子范围只动 `Sphinx/V2/Plugins/{Ordinal,Bayes,AStar,Mcts}` 与 sphinx-v2 测试导入，写集合与 CUT-13 不相交。

## 反例（子范围①必须守住）

- 平局被当作方向票；弃权被当作缺失；位置效应在不可识别设计下仍被估计；断开的比较图仍报告 Converged；完全分离时给出有限 theta；对比方差用 1/sqrt(N) 冒充协方差。

## 不做

- 不改 Sphinx 事件词汇、NDJSON 字节与 MCP 工具白名单；不删除任何证书/证据概念。

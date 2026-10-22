# CUT-22　attention、concern 与邮箱归属

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-22)

## 1. 当前状态与前置核对

- **当前列状态**：`REMOVED`（eng-cut09c-goose，2026-10-21）。不记为"抽取成功"：本卡对象在主仓已经整体退役，没有可抽进库的活算法；本卡核实退役完整，并删掉生产路径上最后两处死残留。
- **前置**：CUT-09 已 DONE（`6774662a2`）。
- **产品裁决来源**：退役不是本卡做的决定。`requirements/attention-regulation/WHAT.md` 与 `requirements/concern-routing/WHAT.md` 已把两个包写成退休边界（enough/abandon 并入 `assume`，defer 并入编程面与 ANCHOR `view.thenMap`；concern 邮箱由 `js.ask/deliver/fork/join` 承接），对应运行时、工具与持久投影已由 `94f994ed8`（concern 邮箱、DeferredWork 事实）及更早的 `acaf43383`、`a0424c7e8` 等提交删除。

## 2. 核实：已不存在第二份邮箱/待办系统

在 master `5fb955e23` 上 grep（排除 dist、node_modules、库）：

- `Interaction/Attention`、`Interaction/Concern`、`ConcernTools`、`AttentionTools`、`AttentionConcernJournalAdapter` 不在源码或编译分片中；`Mailbox*` 的命中只剩 `Execution/Session/Wait/CompletionMailbox`（CUT-21 的等待完成邮箱）和 `Change/Surface` 的 verdict mailbox，都不是 concern 邮箱。
- 生产路径上剩下两处残留：
  1. `Host/Facts.fs(.fsi)` 的 `ConcernPlacementBatch` 类型，以及 `HostFactCases.PairProgrammingGuidelineAnchored` 上的 `ConcernPlacement: ConcernPlacementBatch option` 字段。两个构造点（`PairProgrammingThoughtTransform.appendDurable`、`PairProgrammingThoughtSurface.anchoredFactOf`）都恒写 `None`，没有任何读者。这是 durable AgentFact 上的 Concern 状态，字面上违反 concern-routing [007]，原测试没查 Host 事实族，所以没发现。
  2. `Interaction/Authority/Origin.fs(.fsi)` 的 `PromptContinuationKind.DeferredWorkPresentation`，以及 `Model.fs` 里它的标签编解码。attention [005] 已删除唯一的生产者（自然终止时呈现 DeferredWork），留下的只是一个无人发出、但仍会被 `tryParseContinuationKind` 接受的准入标签。

## 3. 改动

| 旧符号 | 去向 |
|---|---|
| `Host.ConcernPlacementBatch` 与 `PairProgrammingGuidelineAnchored.ConcernPlacement` | 删除（Facts.fs/.fsi、Fact.fsi，两个构造点去掉 `ConcernPlacement = None`） |
| `PromptContinuationKind.DeferredWorkPresentation` 及其 `originLabel`/`tryParseContinuationKind` 分支 | 删除 |
| 回归测试 | concern-routing `007.test.mjs` 新增"Host 事实族不带 ConcernPlacement"；attention-regulation `005.test.mjs` 新增"continuation 种类不含 DeferredWorkPresentation"。两条在旧代码（`13944a5db`）上都失败，在候选上通过 |
| WHAT | attention [005]、concern [007] 各补一句，写明上面两条约束 |

库侧无改动：没有可迁的规则，新建库模块只会制造一个空 owner。wanxiangli gitlink 不动。

## 4. 验收（§9）

证据见 [evidence/CUT-22](../evidence/CUT-22/README.md)。

- 构建：`node scripts/build.mjs` exit 0（725 emitted modules linked and loaded）。
- 日志兼容（[journal-compat.mjs](../evidence/CUT-22/journal-compat.mjs)）：旧代码（带字段）序列化的 guideline 事实，包括 `ConcernPlacement = Some {...}` 的非空情形，候选都能解码，且再序列化后与候选自身产物逐字节相同；候选产物旧代码也能解码。共 150 例。另外用旧代码写一个真实 EventStore 日志（5 个 anchored pair），候选打开重放后 pair 数仍是 5。原因：Thoth Auto 编码时 None 字段本来就不写出，解码时忽略多余字段。
- `DeferredWorkPresentation` 的兼容代价：如果旧日志里有以它为来源的 prompt 事实，候选将拒绝解码这条来源标签。这样的日志必然也带着 `DeferredWorkRecorded` 事实，而 `94f994ed8` 删除该事实族时没有保留旧格式解码，这类日志在 master 上已经无法重放。所以本卡不会让任何当前能重放的日志变得不能重放。如实记录。
- 相关套件（候选）：concern-routing 8/8、attention-regulation 7/7、interaction-authority 104 pass/0 fail、durable-events 236/0、provider-projection 76/0、context-compression 50/0、provider-attempt-recovery 61/0、capability-enforcement 133/0、crash-reconciliation 52/0、degeneration-guard 55/0、participant-identity 51/0。有红的套件：dispatch-protocol 1 cancel、host-boundary 2 fail（023、026）、structured-workflow 1 fail（005）、prefix-stability 8 cancel。失败/取消用例名集合与旧 base 逐一比较相同，属于既有问题。
- fantomas `--check`：本卡改的文件中只有 `Interaction/Authority/Model.fs` 报需要格式化，改动前的 master 版本同样报（既有问题）；本卡没有顺手重排无关代码。
- 净改进：durable 事实和准入标签各少一个死分支，concern [007] 现在对 Host 事实族也有测试守着；行为不变。
- 回退：revert 本卡主仓提交即可。

## 5. 承接义务

无。horizon（`HorizonTool`）里没有 attention/concern 相关内容，不需要单独处理。

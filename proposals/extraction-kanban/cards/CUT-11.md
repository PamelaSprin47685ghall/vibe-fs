# CUT-11　派发认领与外部效果记账

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-11)

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（eng-cut09f-goose 接手 eng-cut09e-goose，2026-10-21 认领）。子范围①②④已合入。
- **前置**：CUT-09 `DONE`；CUT-10 在收尾（eng-cut10e-goose）。子范围①只碰认领账规则（`Interaction/Authority/{Model,Run}.fs` 的认领段、`Interaction/Dispatch/Recovery.fs`），不触及路由与容量写集合。

## 2. 子范围①：派发认领账规则 → `wanxiangli/dispatch`（0.40.0）

库（wanxiangli `c06f8c6`，发布 `202a41a`）：无状态的 `Dispatch` 类，plain JS 边界，畸形输入抛 ArgumentException。

| 规则 | 需求 | 旧 owner（已删） | 现在 |
|---|---|---|---|
| ClaimSequence 作用域与 PromptKey 原文（缺席标记 `\0absent`，不含 agent/peer/model） | dispatch-protocol [005][006] | Model.fs `digestField`、`claimScopeDigest`/`derivePromptKey` 的拼接 | `claimScope`/`promptKeyMaterial`；宿主只做 sha256 |
| 序号注册即消费、"已认领过" | [006]、provider-attempt-recovery-008 | `nextClaimSequence` 的 +1、`repairAlreadyClaimed` 的 `> 1` | `nextClaimSequence`/`alreadyClaimed` |
| 四态结算的投影编辑：Submit 只记回执；Accept 移出待决、覆盖 occasion、按物理消息精确留证、continuation 另记；Abandon 移出待决并清 occasion、不碰精确证据；非待决 key 不改投影 | [002][003][006]，effect-accounting [004] | Run.fs `submitClaim`/`acceptClaim`/`abandonClaim`/`acceptedContinuationEvidence` 的手写分支 | `settle` 返回编辑标签，Run.fs `settleClaim` 只按标签改 typed 投影；库对非待决 key 给出编辑即 failwith |
| 精确 occasion 准入（待决或同来源已落地） | [007][008] | Model.fs `exactOccasionIsAdmitted` 的布尔式 | `occasionAdmitted` |
| 恢复核对：最近 50 条、role=user、PromptKey 完全一致才算证明；否则保留待决并区分有无回执，不重发 | [004][007]，effect-accounting [002][005] | Recovery.fs `findPhysical` 与 `StillPending` 分支 | `recover` |

宿主保留：`PromptAuthorityProjection` 记录与 typed 认领、sha256、journal 写入、Host 发送与 `PromptRecovery.reconcile` 的 I/O、`AcceptAgentOwnerRoot`/`AcceptContinuation` 的权威接线。

## 2b. 子范围②：Host 发送回答 → `wanxiangli/dispatch` `answer`（0.42.0）

| 规则 | 需求 | 旧 owner（已删） | 现在 |
|---|---|---|---|
| 回执 → 只记 Submitted；真实物理 id → Submitted 再 PhysicalAccepted；物理承接前的确定拒绝 → 显式 Abandoned(SendFailed)；结局未知 → 什么都不写、保持待决 | dispatch-protocol [002][003][007]，effect-accounting [002][006] | Send.fs `awaitPhysicalAwareSend` 与 `RecordSendOutcome` 两份五分支 match | `answer` 的 ledger；宿主 `answerOf` 只把标签还原为 typed `SendLedger` |
| 只有确定拒绝算"确定未发送"（gate nudge 才可归还 permit） | [007] | `continuationAttemptOutcome` 的 `(Retryable _ \| Fatal _)` | `answer` 的 definitelyNotSent |
| Detached 观察：承接即 OwnedSettled（不凭 SDK 返回值写 PhysicalAccepted）、拒绝、未知 | [009] | `ObserveDetachedSend` 的 `classifyDetachedOutcome` | `answer` 的 detached |

宿主保留：持久化、`Runtime.Abandon` 单写者、确认等待者、Host 调用与 Detached 的进程级审计。代价：Send.fs/Model.fs 净增 44 行（标签与 typed 还原）。证据见 evidence README 子范围②。

## 2c. 子范围④：运行边界的投影重置 → `wanxiangli/dispatch` `reset`（0.45.0）

| 规则 | 需求 | 旧 owner（已删） | 现在 |
|---|---|---|---|
| 新根登记或精确关闭时：有 LogicalRunId 的待决认领、运行作用域计数、continuation 映射与提问随运行结束；无运行的根认领与本会话的根计数保留（根 PromptKey 永不重复） | dispatch-protocol [006]，CUT-09 §6 移交 | Run.fs `registerAuthority` 与 `closeAuthority` 两份相同的手写重置、Model.fs `rootClaimSequences` | `reset` 返回保留集与清除标志；Run.fs `resetRunResources` 只按结论过滤 typed 投影 |

库：wanxiangli `fce28cc`，发布 `d449747` **0.45.0**，`src/dispatch/tests/003.test.mjs` 独立 oracle。代价：三个文件净增 50 行（标签往返与 .fsi 签名）。证据见 evidence README 子范围④。

## 3. 验收（§9）

证据见 [evidence/CUT-11](../evidence/CUT-11/README.md)。

- 构建：candidate `node scripts/build.mjs` exit 0（725 模块链接）。fantomas 已格式化改动文件。
- 投影差分 1,200,000 次 0 处不同；恢复差分 200,000 次 0 处不同；11 个语义变异全被差分抓住。
- 套件：interaction-authority、dispatch-protocol、crash-reconciliation、managed-chat-execution、effect-accounting 与 base 计数相同、失败/取消集合相同（dispatch-protocol 1 cancel、effect-accounting 5 cancel 为既有问题）。
- 净改进：认领账的作用域、key 原文、序号、结算编辑、occasion 准入和恢复证明原先散落在 Model.fs、Run.fs、Recovery.fs 三处手写，现在只有库一个 owner，并有独立 oracle 测试、差分与变异证据；行为不变。代价如实记录：宿主多了 typed 投影层（Model.fs 的 `DispatchLaw` 模块与 .fsi 签名），四个文件净增 158 行（含 .fsi 40 行）；Model.js 因同文件两个模块，Fable 导出名加了 `PromptAuthority_` 前缀（仓内无 JS 直接引用该文件，`RuntimeSurface` 走 F# 引用不受影响）。
- 已知缺口：主仓需求测试不覆盖恢复窗口与 role 规则，等价性只由恢复差分与库测试证明。
- 回退：revert 本卡主仓提交即可；库 0.40.0 留在 main 不影响旧 master。

## 4. 余项（本卡仍 DOING）

- ② 已合入（见 §2b）。发送链的其余部分（gate nudge 单 flight、Detached 入栈后立即返回）是宿主异步编排，不再有可抽的纯裁决。
- ③ Work.operation 与 dispatch claim 的关联：同一发送行为只能有一个认领 owner，两类事实明确关联而不合并成一张账。
- ④ 已合入（见 §2c）：投影重置只剩库一个 owner。旧 claim 写入口：`registerClaim`/`submitClaim`/`acceptClaim`/`abandonClaim` 已只按库的编辑标签改投影（子范围①），没有第二条写路径。

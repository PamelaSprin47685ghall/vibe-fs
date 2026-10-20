# CUT-11　派发认领与外部效果记账

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-11)

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（eng-cut09e-goose，2026-10-21 认领）。子范围①已合入。
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

## 3. 验收（§9）

证据见 [evidence/CUT-11](../evidence/CUT-11/README.md)。

- 构建：candidate `node scripts/build.mjs` exit 0（725 模块链接）。fantomas 已格式化改动文件。
- 投影差分 1,200,000 次 0 处不同；恢复差分 200,000 次 0 处不同；11 个语义变异全被差分抓住。
- 套件：interaction-authority、dispatch-protocol、crash-reconciliation、managed-chat-execution、effect-accounting 与 base 计数相同、失败/取消集合相同（dispatch-protocol 1 cancel、effect-accounting 5 cancel 为既有问题）。
- 净改进：认领账的作用域、key 原文、序号、结算编辑、occasion 准入和恢复证明原先散落在 Model.fs、Run.fs、Recovery.fs 三处手写，现在只有库一个 owner，并有独立 oracle 测试、差分与变异证据；行为不变。代价如实记录：宿主多了 typed 投影层（Model.fs 的 `DispatchLaw` 模块与 .fsi 签名），四个文件净增 158 行（含 .fsi 40 行）；Model.js 因同文件两个模块，Fable 导出名加了 `PromptAuthority_` 前缀（仓内无 JS 直接引用该文件，`RuntimeSurface` 走 F# 引用不受影响）。
- 已知缺口：主仓需求测试不覆盖恢复窗口与 role 规则，等价性只由恢复差分与库测试证明。
- 回退：revert 本卡主仓提交即可；库 0.40.0 留在 main 不影响旧 master。

## 4. 余项（本卡仍 DOING）

- ② 一条真实发送链（`Interaction/Dispatch/Send.fs`）：claim → Host 发送 → 回执 → 物理承接的次序与"已发但提交未知不再发送"。
- ③ Work.operation 与 dispatch claim 的关联：同一发送行为只能有一个认领 owner，两类事实明确关联而不合并成一张账。
- ④ 旧 claim 写入口退出与投影重置（清 run-scoped claims、continuation 映射、序列号），后者 CUT-09 §6 移交至此。

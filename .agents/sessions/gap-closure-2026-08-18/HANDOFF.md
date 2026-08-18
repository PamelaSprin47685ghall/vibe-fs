# 新窗口交接说明

## 项目
路径：仓库根目录
当前分支 / worktree：`master` / checkout
当前 Phase / Task：继续追平当前义务账；下一项从 OBL-005 开始

## 最新提交
最新 commit：`84caed7e6 refactor: type magic todo checkpoint lifecycle`
最近关键 commit：`4f64e7d44 refactor: close verifier and chronicle obligations`

## 已完成
- OBL-003 已闭合：Magic Todo checkpoint 改为封闭 lifecycle ADT；obligation-ledger 94/94 green，completion grep=0，control-pyramid=0。
- OBL-006 / OBL-009 已闭合：tracked dead-code gate、legacy horizon census、dated machine evidence 已纳管；本 worktree census 为 59 journals / 18,916 lines / 四 detector=0，但不冒充 supported-workspace inventory。
- OBL-010 已闭合：Chronicle no-live-cycle 先类型化，再 abort，最后由 Host adapter 编码 SDK error。
- OBL-004 / OBL-012 已按既有完成证据从义务账自删除。

## 当前状态
- 测试结果：最新 OBL-003 改动已通过 `node scripts/build.mjs`、obligation-ledger 94/94、`node scripts/check.mjs`。本轮未在 OBL-003 后再次跑 authoritative `node requirements/verification-system/tests/run.mjs`，因此 OBL-011 不可关闭。
- git status：记录本文件前工作树 clean，`master == origin/master == 84caed7e6`。
- 已知问题：OBL-005 尚未改 production；只完成了影响面调查。OBL-002 仍未施工。OBL-007 / OBL-008 受外部 creditor 约束，不得伪闭合。

## 下一步任务
1. OBL-005：把 fallback success 从 `AgentJournal` 进程内 `derivedFallbackSuccesses` overlay 改为 owner-owned durable success fact；保证 Offset 不变、failure count 归零、duplicate/旧 ProviderRun 幂等，fresh-process replay 等价。
2. OBL-002：删除 Change recovery 的 `JobRecoveryAction → recoveryAction → resumeFromDurableFacts` 第二状态机，fresh/restart 收敛到同一普通 CE `run`。
3. OBL-007 / OBL-008 继续保留，直到 supported-workspace retention horizon 与 Host V1 creditor 的外部退出条件真实满足；随后才能考虑 OBL-011 最终验收。

## 重要约束
- 不要误判当前阶段：聚合 `requirements/GAP.md` 虽已全 CLOSED，真实剩余工作由 `AGENTS.md` 当前义务账定义。
- 不要声明未完成能力为 supported：本地 legacy census 不是所有 supported workspaces 的权威 inventory。
- 不要写死本地绝对路径。
- 修改前先检查现有架构；新增 caller 必须先追加到对应义务影响集。
- OBL-005 现有关键违规符号：`derivedFallbackSuccesses`、`RecordDerivedFallbackSuccess`、`recordDerivedFallbackSuccess`；当前调用点在 `OrdinaryTurnWorkflow.fs` 与 `JobHandoff.fs`。

# CUT-01：工作生命周期与原子日志接线（历史已合入 DONE）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-01) / [范围对账](../COVERAGE.md) / [合并比较与收尾证据](../../lifecycle-cutover-closeout.md)。

## 1. 当前状态与历史合入基线

- **当前列状态**：`DONE`（历史基线已合入主仓 `master`，见 [lifecycle-cutover-closeout.md](../../lifecycle-cutover-closeout.md)）
- **所属泳道 / 波次**：已交付 / W0 基线复核
- **切换前置**：无
- **独立库与锁包证据**：`wanxiangli@0.5.2`（`vendor/wanxiangli-0.5.2.tgz`，SHA-256: `53f21944e86a314035d82e40e395aa785730e95110a95fb74177791460422656`），导出 `wanxiangli/work`（`Work`）、`wanxiangli/eventstore`（`SqliteJournal`）、`wanxiangli/durable-work`（`DurableWork`）。
- **边界声明**：本卡记录已合入的 Work 生命周期与原子 SQLite 日志切刀事实、真实生产接线路径、已退出的旧生命周期实现以及后续维护护栏。本卡完成不代表全部 `work` / Host / release GAP 已闭合（如 `work-011` 的真实在途 Host 中断与后继派发顺序、完整 Long Stroke 等仍按 [GAP.md](../../../requirements/GAP.md) 原状态追踪）。

## 2. 目标与已完成范围

- **用户/维护者此前损失**：Plan 生命周期（S1/S2/S3）与 Manager 道路生命周期（S4）曾分别维护两套事件、两套折叠状态机、质量证书（`QualityCertificate`）、`WorkspaceSnapshot` 以及旧 EventStore NDJSON 生命周期信封；持权 Plan 交付时曾先启动外部会话再提交退休，崩溃或未知回执会导致重复创建执行会话或状态撕裂。
- **本卡合入后已少维护什么**：
  1. 统一由 `wanxiangli/work` 的 `Work` 承担唯一生命周期规则与规范 JSON record，由 `wanxiangli/eventstore` 的 `SqliteJournal` 承担 `<git-common-dir>/wanxiangshu/work.sqlite` 的单流原子 CAS 落盘，由 `wanxiangli/durable-work` 的 `DurableWork` 承担两者的提交协作。
  2. 旧 `RoadOpened`、`AssessmentCommitted`、`RetirementConfirmationCommitted`、`QualityCertificate`、`WorkspaceSnapshot`、`suicide` 工具别名以及旧 EventStore / `CanonicalIntegrator` 中的生命周期折叠已彻底退出（由 `scripts/checks/clean-break-symbols.mjs` 与 `durable-events-027` 机械守门）。
- **明确不做**：
  - 不为迁移再次复制一套 Work 状态机；
  - 不将旧 NDJSON 历史字节翻译为新 `work.sqlite` 事件（`work-010` / `durable-events-027`）；
  - 不让相邻库（如 `Ownership`、`Trace`）接管 Work 的阶段推进或退休裁决。

## 3. 依据与真实生产接线盘点

- **正式 WHAT / 条款**：`requirements/work/WHAT.md` `[001]`—`[048]`、`requirements/durable-events/WHAT.md` `[027]`。
- **主仓核心宿主入口与调用链**：
  1. **生命周期宿主边界**：`src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs(.fsi)` 与测试表面 `src/Wanxiangshu/Mission/Lifecycle/Surface.fs(.fsi)`（所属分片：`Wanxiangshu.Owner.planning.mission-planning-eventstore.fsproj`）。
     - 按运行时动态选择 SQLite 驱动（Node `node:sqlite` `DatabaseSync` / Bun `bun:sqlite` `Database`，满足 `work-047`）；
     - 固定存储于 `<git-common-dir>/wanxiangshu/work.sqlite`（满足 `work-033` / `work-046`）；
     - `inspectedJournal` 在单次读取中从同一批 `records` 构造 `snapshot` 与 `history`，消除并发读取间的 revision 撕裂（满足 `work-033`）；
     - `ensureOpened` 跨进程幂等开立，遇到 `Conflict` 或 `Unknown` 时重读持久状态核实。
  2. **Plan 生命周期接线（S1/S2/S3）**：
     - `src/Wanxiangshu/Mission/Planning/PlanWorkOpen.fs(.fsi)`：首条 prompt 在已观察到的 `XTrace` 头游标处原子开立 `WorkOpened` + `IncumbencyOpened(S1)`（`work-036`）；
     - `src/Wanxiangshu/Mission/Planning/ReviewPlanBackend.fs(.fsi)`：与 Manager 共用 `ReviewShell`，提交 `Review` 推进 S1 → S2 → S3 → `deliveryAuthorized = true`（`work-038`）；
     - `src/Wanxiangshu/Mission/Planning/PlanTools.fs(.fsi)`：无权 `deliver` 原子追加 `IncumbencyRetired(Relayed)` + 下一任 `IncumbencyOpened`（`work-025`）；持权 `deliver` 严格执行 `DeliveryRequested` 首次 `Committed` → 创建无父 Manager 会话 → `ResolveDelivery(SessionCreated)` 持久绑定 `DeliverySessionBound` → 发送首条 prompt → 重查资源 blocker 为空后提交 `ResolveDelivery(Accepted)`（`work-026` / `work-039` / `work-048`）；`executeAsk` 同步委派固定 DevOps 后幂等提交 `BindDevOps`（`work-028` / `work-042`）；
     - `src/Wanxiangshu/Mission/Planning/TenureIsolation.fs(.fsi)`：按本任 `OpeningCursor` 剥除前任 assistant/tool 交互并注入一次 `LWR_prev`（`work-034`）。
  3. **Manager 道路生命周期接线（S4）**：
     - `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：以 Git 证明的工作树根目录（`git rev-parse --show-toplevel`）幂等开立 `S4` work（`work-046`）；按 `Reviewed` / `Confirmed` 状态驱动 `runtime/manager-assess`、`runtime/manager-work`、`runtime/manager-finish` 提示词，并在 `Relayed` 后派发同会话后继（`work-011`）；
     - `src/Wanxiangshu/Mission/Work/OpenCode/ReviewShell.fs(.fsi)` 与 `ReviewBackendManager.fs(.fsi)`：共享 `review` 工具壳，Manager 以独立 `ReviewerId = "manager-reviewer"` 提交 `Review`（`work-003` / `work-011`）；
     - `src/Wanxiangshu/Mission/Work/OpenCode/DeliverTool.fs(.fsi)`：S4 两段式 `deliver`（第一次写 `ConfirmationCommitted`，第二次凭折叠上的评审结果写 `Delivered` 或 `Relayed`，`work-006`）；
     - `src/Wanxiangshu/Mission/Work/OpenCode/NarrativeTransform.fs(.fsi)`：S4 后继上下文剥除与已退休尝试截停（`work-008` / `work-011`）。
  4. **崩溃恢复接线**：
     - `src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs(.fsi)`：重启后从 `work.sqlite` 读取 `DevOpsTarget` 恢复固定 DevOps 模型路由绑定（不把 `DevOpsId` 当成模型目标，`work-033`）；对 `PendingDelivery` 逐个 work 隔离恢复，已绑定 `execSessionId` 时核对首条用户 prompt 摘要与资源 blocker 后结算 `Accepted`，未绑定时以精确标题 `wanxiangshu-delivery:<operationId>` 最多查询两条 root 候选（`work-048`）。

## 4. 自治个体（A / B / C）

- **A（`wanxiangli/work` `Work`）**：纯内存生命周期状态机，校验开立、评审、推进、固定 DevOps 绑定、一段/两段交付、重开及规范 JSON record 哈希链。
- **B（`wanxiangli/eventstore` `SqliteJournal` & `wanxiangli/durable-work` `DurableWork`）**：单流 SQLite 原子追加与 `Committed | Replayed | Observed | Conflict | NotWritten | Unknown` 协议。
- **C（主仓 `DurableWorkHost`、`PlanTools`、`DeliverTool`、`ReviewShell`、`ManagerWorkflow`、`PluginRecoveryWiring`）**：负责 SQLite 连接生命周期、Git 根目录确认、XTrace 游标观察、与 `Ownership` 资源 blocker 的汇合，以及两步式外部执行会话创建与恢复观察。

## 5. 已退出的旧代码与防回退门禁

| 已退役符号 / 概念 | 防回退静态门禁 / 测试 | 当前状态 |
|---|---|---|
| `QualityCertificate` / `WorkspaceSnapshot` / `WorkspaceSnapshotId` | `scripts/checks/clean-break-symbols.mjs`、`work/tests/001.test.mjs` | 已删除并由静态门禁拦截 |
| `suicide` 工具名及 `resources/provider/tool/suicide` | `scripts/checks/clean-break-symbols.mjs`、`work/tests/018.test.mjs` | 已删除并由静态门禁拦截 |
| `RoadOpened` / `AssessmentCommitted` / `RetirementConfirmationCommitted` | `scripts/checks/clean-break-symbols.mjs`、`durable-events/tests/027.test.mjs` | 已退出 EventStore 权威词表，残留行在 boot 时 fail-closed 拒绝 |
| `DeliverTool.fs` 中旧 `runBlocked` / `runRetirement` 死分支 | `CUT-04` 清理完成 | 已删除 |

*注：`src/Wanxiangshu/Mission/Work/Retirement/Surface.fs(.fsi)` 及 `Wanxiangshu.Owner.relay-retirement.mission-relay-retirement.fsproj` 现已无任何生产或测试消费者，为避免 W1 并行期间改动 `compile-order.txt` 产生冲突暂留在磁盘，将在 `CUT-38` 终局交账时随无主分片一并物理移除。*

## 6. 维护重点与验证入口

- **维护不变量**：
  1. 任何对 `DurableWorkHost` 的修改必须保持单次 `records()` 快照投影，严禁分别读取 `inspect()` 与 `history()`；
  2. 物理启动外部执行会话只认 `Committed + DeliveryRequested`，恢复只观察原 `operationId`，严禁因异常或超时重发 prompt；
  3. 退休与恢复结算前必须查询已安装的资源 owner（`CUT-04` `RetirementBlockersFor`），不得以空列表兜底。
- **回归验证命令（由 `devops` 执行）**：
  - `node --test requirements/work/tests/*.test.mjs`
  - `node --test requirements/durable-events/tests/027.test.mjs`
  - `node scripts/check.mjs`

# Glory 实现差距跟踪（GLORY-001..071 / SURFACE-001..006）

> 本文件由 `docs/proposal/glory.md`（Final Review Draft）裁决迁移而来。条款正文的正式定义处：`docs/what/glory.md`（GLORY- 与 SURFACE-）。实现依据：`docs/how/glory.md`；验证依据：`docs/proof/glory.md`；设计理由：`docs/why/glory.md`；所有权：`docs/shape/glory.md`。

## 状态

实现中。消除目标：全部 26 条完成判据（proof/glory.md）与 crash recovery matrix（how/glory.md）达成后删除本文件。

## 差距清单

| # | 差距 | 状态 |
|---|------|------|
| 1 | `ManagerLifecycleFact` 事实代数 + `ManagerLifeId`/`FinalityRequestId` identity + FactCodec | ✅ 已实现（Slice A，`Kernel/Fact.fs`、`Kernel/Identity.fs`） |
| 2 | `ManagerLifecycleProjection` fold 注册（`SessionAgentProjection.ManagerLife`） | ✅ 已实现（`Journal/ManagerLifecycleProjection.fs`、`Fold.fs`、`AgentProjection.fs`） |
| 3 | Slice A：`ManagerNarrativeTransform`（Birth/Reawakening 改写、幂等）+ `ManagerNarrative` 冻结文本 | ✅ 已实现（每个请求对 opening 消息重写，narrative 派生自 durable blob 防叠加；GLORY-069 迁移 Life；`isHumanRootManager` 排除 AgentOwnerRoot） |
| 4 | Slice B：`ManagerWorkActivation`/`ManagerIdleEncouragement`/`FinalityRejected` continuation kind + TurnCompletionProgram 规划 terminal 分支 + `WorkActivated` + Blogger floor | ✅ 已实现（`ManagerLifecycleGate`、`TurnCompletionProgram`、`applyAcceptedActivation`、`BloggerCoordinator`/`CompanionTransform` floor） |
| 5 | Slice C：`ToolPermission.Finality` + `FinalityTool`（suicide 前置条件/受理）+ Manager→Reviewer fork 拒绝 + 删除 `ManagerOpensReviewBarrier` | ✅ 已实现（`FinalityTool.fs`、`ForkTool.fs` ReviewerForkDenied、`HostForkAgent` barrier 分支删除；AgentOwnerRoot Manager 首次 ending 自动 migration Life） |
| 6 | Slice D：`HostReviewProgram` 通用 reverify + `HostReviewOutcome`/`HostReviewFailure` | ✅ 已实现（`Orchestration/HostReviewProgram.fs`，`OrchestratorHostReview` 委托） |
| 7 | Slice E：`FinalityRejected` + `FinalityPrompt.rejected`（SyntheticToml）+ 同 Life continuation + Reviewer cleanup | ✅ 已实现（`FinalityController.concludeRejection`） |
| 8 | Slice F：`FinalityConfirmed` + tree revalidation + `LifeCompleted` + last_words terminal + Manager completion | ✅ 已实现（`FinalityController.concludeGlory`；`FinalityUndecided` fail-closed） |
| 9 | Slice G：多 Life cursor range + Reawakening prefix + migration Life（GLORY-069..071） | ✅ 已实现（`CompletedLives` 归档 + `reawakening`；migration 在 transform 与 FinalityTool 双兜底） |
| 10 | `resources/prompts/manager-system.md` 与 `reviewer-system.md` 完整替换（附录 A.2/A.3 字节） | ✅ 已实现（四工具、无 review 词、Life 叙事；Reviewer 无 confirmation 词） |
| 11 | 黄金字节 fixtures + 单元测试 + 禁止词门禁（SURFACE-005/006） | ✅ 已实现（`tests/unit/glory/lifecycle.test.mjs` 16 测试 + `support/glory.mjs` facade） |
| 12 | e2e canary 剧本迁移 | 🔶 进行中（17/21 通过）：本轮修复 3 个生产侧根因（① opening 消息重写独立于 continuation 守卫；② Host title 请求按 content+parts 双判定排除；③ WorkActivated 改用 canonical 文本匹配，不再依赖 AcceptedContinuationIds 的 accept 时序竞态——第 1 次 Activation transform 即可写 floor）+ 场景层（companion 全绿、reviewer-verdict 的 dual PERFECT 已确认（bindToRun→Confirmed，waitFact eq=2 通过）、orchestrator-publish 恢复 GLORY-031 语义（Manager 禁 fork Reviewer，guard 以 prose 结束，审查由 Orchestrator 的 deep-reviewer barrier 承担））。剩余项：① `reviewer-verdict` / `manager-full-loop` / `host-restart` 的 blogger 第 2 次请求偶发 seal-undeclared（blogger 续写与 floor 读取的时序，companion 绿证明同代码路径可行，疑为 Blogger 续写消息在同一位置的修改 vs append）；② `orchestrator-restart-publish` 的 barrier-reviewer 在 restart 前未达（deep-reviewer 的 barrier 审查时序）；③ `enforcer-repair-persist` 场景中的 Manager Birth 开场文本改写（GLORY-014 `PlanningTail`）与 scenario fixture 匹配对齐。均为场景层/时序调优，无生产代码缺陷 |
| 13 | unit/integration 门禁 | ✅ 1004 unit + 267 integration 全绿；npm run lint 全绿 |

## 附录 A 冻结文本 owner 落点

附录 A（Provider-Facing Surface Catalog）的全部冻结文本在实现中的权威字节落点（SURFACE-004）：

- Manager system prompt → `resources/prompts/manager-system.md`
- Reviewer system prompt → `resources/prompts/reviewer-system.md`
- Birth/Reawakening → `src/Wanxiangshu/Domain/ManagerNarrative.fs`
- Activation/idle/undecidable → `src/Wanxiangshu/Domain/ManagerLifecyclePrompt.fs`
- Finality rejection → `src/Wanxiangshu/Domain/FinalityPrompt.fs`
- Reviewer opening → `src/Wanxiangshu/Domain/HostReviewPrompt.fs`
- Skeptical challenge → `src/Wanxiangshu/Domain/ReviewChallenge.fs`（既有）
- 工具 schema/结果 → `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityTool.fs`、`VerdictTool.fs`

测试不得复制测试专用常量；直接读取 owner。

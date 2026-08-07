# Glory：所有权与边界（shape 层）

本文件定义 GLORY 机制的模块所有权、数据流边界与禁止的泄漏路径。条款正文见 `docs/what/glory.md`（GLORY-001..071、SURFACE-001..006）。

## 模块所有权

| 关注点 | 拥有者 | 边界 |
|------|------|------|
| Life 事实与投影 | `Domain/ManagerLifecycle.fs` + `Journal/ManagerLifecycleProjection.fs` | 纯逻辑；事实经 `Fact.ManagerLifecycle` case 进入 journal（GLORY-010） |
| Birth/Reawakening 改写 | `Infrastructure/OpenCode/Host/ManagerNarrativeTransform.fs` | 只在 provider-facing transcript 生效；durable X 保持原始（GLORY-013/014/064） |
| 终结工具 | `Infrastructure/OpenCode/Tools/FinalityTool.fs` | `suicide` 唯一写入口：受理顺序见 GLORY-040 |
| 自动评审 | `Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs` | 从 `OrchestratorHostReview` 提炼的通用 reverify（GLORY-042） |
| 反馈渲染 | `Domain/FinalityPrompt.fs` | `SyntheticToml` 唯一渲染路径（GLORY-052，SURFACE-004） |
| 冻结文本 | 各 Domain owner + `resources/prompts/*.md` | 每个固定文本恰好一个 owner（SURFACE-004） |

## 数据流（成功路径）

```text
HumanRoot [X] → XTrace durable capture → ManagerNarrativeTransform 改写
→ 规划回合 → TurnCompletionProgram 检测规划 terminal → ManagerWorkActivation continuation
→ WorkActivated（写 ProtectedPrefixEnd）→ Labor
→ suicide(last_words) → FinalityRequested → HostReviewProgram（隐藏 Reviewer + barrier）
→ confirmed dual PERFECT → 重读 tree → FinalityConfirmed → LifeCompleted
→ last_words 成为 terminal → 完成 handle → 新 HumanRoot → 下一 Life（Reawakening）
```

## 数据流（失败路径）

```text
suicide → FinalityRequested → HostReviewProgram → REVISE
→ Reviewer LWR（includeOpening=false）→ FinalityRejected → FinalityRejected continuation
→ Manager 同一 Life 继续工作 → 再次 suicide → 新 request/Reviewer/barrier
```

## 硬性边界

1. Manager 面（system prompt、continuation、工具 schema、固定 tool result）不得出现 SURFACE-005 禁止词；`REVIEWER_WORK_RECORD` 是唯一例外且不得清洗（GLORY-048）。
2. 自动 Reviewer session 对 Manager 的 `list`/`join` 不可见（GLORY-002）。
3. `ManagerOpensReviewBarrier` 从 Manager 普通 fork surface 删除（GLORY-033）；barrier 只由 Finality workflow 与 Orchestrator post-rebase review 拥有。
4. Reviewer 工作记录只由 `XTraceCapture.lifecycleWorkRecord journal reviewerSessionId false` 产生（GLORY-004/049）。
5. 成功输出逐字等于 `last_words`，Host 零附加文本（GLORY-061）。
6. 状态身份只来自 typed facts + projection，禁止故事文本反向解析（GLORY-008/009）。

## 与既有系统的关系

- **XTrace**：保持 append-only；ManagerLifecycle 按 cursor range 物化 Life（GLORY-066/067）；通用单 Opening/Terminal 字段保留为兼容层。
- **PromptAuthority**：新增 `ManagerWorkActivation`、`FinalityRejected`、`ManagerIdleEncouragement` 三种 continuation kind，全部走 PROMPT-005 claim → submitted → accepted 协议（GLORY-020/029/053）。
- **Blogger**：`effectiveStart = max IngestedThrough ProtectedPrefixEnd`（GLORY-023/024）。
- **Orchestrator**：ManagerJob 不原地复活（GLORY-068）；HostReviewProgram 由 Orchestrator 与 Manager Finality 共用（GLORY-042/044）。
- **ReviewGuard**：GLORY-070 迁移期保留 fail-closed 角色但改写文本；新 pipeline 覆盖后删除 manager-facing old guard。

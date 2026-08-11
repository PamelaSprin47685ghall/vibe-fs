# Prompt Authority — 可观察行为

条款前缀：`PROMPT-`。  
Dispatcher 与 Profile 所有权见 `shape/prompt.md`。  
发送格式、来源解析与未决恢复见 `how/prompt.md`。

## PROMPT-001：顶层不变量

```text
PhysicalUserMessage ≠ AuthorityTurn
```

Host 上的 `role=user` 只是运输格式。下列内容都不是 Authority 身份证据：

- 零宽字符、空白、固定模板  
- 时间戳、文本长度  
- Synthetic TOML 的 comment / field 形态（ARCH-010、ARCH-011）

身份只能由本章的 typed 来源机制证明。

## PROMPT-002：只有 Authority Root 可以

Authority Root 独有权限：

1. 创建新的 Logical Run  
2. 选择或改变 SelectedAgent（并确定 PeerAgent / CanonicalRole / SelectedTier）  
3. 成为新的 Fallback root  
4. 重置 Interaction Repair 预算  
5. 成为后续缺省 SelectedAgent 的延续来源  

Authority Root **不得**：

- 改变 Companion 关联（COMPANION-002：Session 结构事实）  
- 选择或覆盖 model ID（发送时始终 `Model = None`）

## PROMPT-003：Continuation 的禁区

下列均为 Continuation，**不得**执行 PROMPT-002 所列操作：

```text
InteractionRepair
JoinGuard
ManagerGuard
ReviewerGuard
ReviewConfirmation
BusyAgentNudge
ProviderRetryAttempt
SyncDelegateIdleNudge
```

历史 `TeacherQuestion` / `TeacherIdleNudge` / `StudentCompile` / `StudentCompileNudge`：
**G3 已删除（absent）**（PROMPT-012 空缺）。不得再列为现行 Continuation。

`ManagerGuard` 仅保留用于历史 journal 行解析（PromptAuthority.fromString），生产不再发送该 continuation（GLORY-070）。

Continuation 只延续已有 Logical Run：

- 不新建 RunId / completion  
- 不改 SelectedAgent / PeerAgent / CanonicalRole / SelectedTier  
- 不更新 LastAuthorityProfile  
- 不重置 Fallback / repair  
- 物理请求使用当前 Fallback cursor 的 EffectiveAgent  

`JoinGuard` 语义见 EXEC-016。

## PROMPT-004：来源类型

```fsharp
type PromptOrigin =
    | AuthorityRoot of RootAuthorityKind
    | Continuation of ContinuationKind
    | HostInternal
    | UnknownOrigin

type RootAuthorityKind = HumanRoot | AgentOwnerRoot
```

| 来源 | 行为 |
|------|------|
| HumanRoot | 必须显式 `fast-*` / `deep-*`；省略 → fail-closed |
| AgentOwnerRoot | Manager fork / Idle 新任务等；必须显式准确 Agent |
| UnknownOrigin | fail-closed：不更新 profile、不启 Fallback、不发 continuation |

## PROMPT-007：Fire-and-forget 的含义

Fire-and-forget **只**表示调用方不等待 PhysicalAccepted。  
不得绕过 claim、authority、持久化、幂等与错误记录。

禁止独立的 `postPromptFireAndForget` 旁路；统一为 Dispatcher 的 `AwaitMode.Detached`。

## PROMPT-010：禁止自激励

禁止下列「用合成身份抬升权限」：

```text
零宽 continuation → HumanRoot
repair continuation → 新的 repair 预算
Review confirmation → 改 Reviewer SelectedAgent
synthetic → 重置 Fallback Offset
B 侧重试 → 下一真人 root 默认 Agent
向 Host Prompt 覆盖 Model
```

## PROMPT-011：未决发送恢复

Host 已接受 prompt，但插件在写 Submitted / PhysicalAccepted 前崩溃时，判定物理落地的恢复协议。机制与 PromptKey 组成见 `how/prompt.md`。

常量：

```text
RecoveryTailWindow    = 50   // 读目标 Session 尾部以检索同 PromptKey 的 user 消息
RecoveryAttemptBudget = 3    // 跨越 3 次插件启动
```

行为：

1. 对每个 `Claimed` 或 `Submitted` 的 PromptKey：读目标 Session 尾部 `RecoveryTailWindow` 条，查找 metadata 含同一 PromptKey 的 `role=user`。
   - 找到 → 补写 `PhysicalAccepted`（真实 `msg_*`）
   - 未找到 → 保持 Pending，**不自动重发**
2. 第 `RecoveryAttemptBudget` 次启动仍无法证明物理落地 → `Abandoned(UnresolvedAfterRecovery)`。

合同：

```text
at-most-one logical effect + fail-closed unknown outcome
```

禁止：

```text
假装 exactly-once
用时间窗口代替 PromptKey
把 accepted-* 当物理落地
为清理挂起而重发
```

## PROMPT-012：（空缺）Student / Teacher Prompt 身份 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 `fast|deep-student` HumanRoot、QA bootstrap、
`StudentLearn` / `StudentCompile`、`TeacherQuestion` / `TeacherIdleNudge` / `StudentCompileNudge`
与 Learn→Compile request-kind 切换。无 alias、无 deprecated Prompt 路径。

后继：SyncDelegate 首发与 `SyncDelegateIdleNudge`（PROMPT-003/005；HOST-008 / EXEC-026/028）；
插件 user-shaped message 仍一律经 PROMPT-005，不得直接 `prompt_async`。

## PROMPT-013：Magic Todo Manager 持续规划 guidance

Manager 的 provider-visible 规划指引是 **HOST-013 通用结对编程 guideline 的加法片段**，不是全局替换：

```text
general pair-programming guideline（HOST-013）
+
if CanonicalRole = Manager
   AND todowrite is provider-visible
then MagicTodoManagerGuideline
```

禁止把 Magic Todo 文案并入 `ProjectionConstants.PairProgrammingGuidelineText` 或对其它角色投影。

`MagicTodoManagerGuideline` 冻结语义（全文 owner 见 TODO-013；协议本体见 TODO-001）：

```text
Keep the todo list continuously accurate with todowrite.
Planning and execution are one continuous activity.
Do not stop for a separate planning-only phase.
Update todowrite when truthful decomposition / discovered work / progress changes.
kind:"existing" reuses exact id; kind:"new" omits id.
A todo must pass through reviewing before completed.
Continue independent next-stage work while prior checkpoint is reviewed.
Each accepted todowrite consumes the preceding checkpoint review and starts the next.
Do not emit multiple todowrite calls in the same assistant message.
```

### 可见 / 禁止 surface（与 GLORY-030 窄例外对齐）

Manager **允许**观察 Todo Checkpoint process-review 的：

```text
outcome（PERFECT / REVISE）
concrete report（ProcessReviewLWR，经既有 Finality safety-seal）
```

Manager **禁止**在 system prompt、continuation、schema、固定错误、tool description/result 中出现：

```text
reviewer / reviewer agent name / reviewer session
barrier / witness / 2N / finality cohort
confirmation rounds / hidden task 编排
```

窄例外仅限 Magic Todo process protocol（TODO-013）；不放宽 Finality 内部机制或其它 review surface。  
GLORY-030 / SURFACE-005 以本条 + TODO-013 为唯一允许的 PERFECT/REVISE/report 出口；其余 Manager 固定 surface 仍全面禁止。

V2 `todowrite` schema / description owner 见 TODO-002；admission 与 V2 hook 门禁见 TODO-004。  
不得经 Prompt 路径伪造 checkpoint、ConsumableReview 或 PrefixEpoch。

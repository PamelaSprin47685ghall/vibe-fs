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

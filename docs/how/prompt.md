# Prompt — 目标实现

## Implements

行为合同见 `what/prompt.md`；本文件只描述来源解析、dispatcher 和未决发送恢复算法。

## Ownership

Authority、PromptKey 和发送 writer 边界见 `shape/prompt.md`。

---

## PROMPT-006：发送格式

每次发送固定：

```fsharp
{ Agent = Some effectiveAgent
  Model = None
  Directory = directory
  Metadata = metadata
  Tools = requestToolOverride }
```

禁止设置 `Model`。Host 按 `config.agent[effectiveAgent].model` 解析。  
PromptKey 必须写入 metadata（PROMPT-011）。

`Tools=None` 是普通请求。StudentLearn / StudentCompile 的完整 allow/deny map、`teacher` 工具面与
Learn→Compile tools override：**G3 已删除（absent）**（PROMPT-012 / AGENT-020…022 空缺）。不得再发送
Student request-kind 局部或完整 tools map。

SyncDelegate（dedicated Inspector/Coder）的首发与后续 continuation / idle nudge
（`SyncDelegateIdleNudge`）仍经 `PromptDispatcher`（PROMPT-005）；不得旁路直接 `prompt_async`。
`return` 工具面仅属 SyncDelegate（EXEC-026/028）。

---

## PROMPT-009：来源解析优先级

```text
accepted HostMessageId
→ claimed PromptKey
→ Host compaction / synthetic
→ registered AgentOwnerRoot
→ proven external prompt acceptance (HumanRoot)
→ UnknownOrigin
```

先匹配者生效；落到 UnknownOrigin 则 fail-closed（PROMPT-004）。

---

## 未决发送恢复算法（PROMPT-011）

当 Host 已接受 prompt 但插件在写入 `Submitted` 或 `PhysicalAccepted` 前崩溃时，必须通过未决发送恢复算法在下一次启动时确定物理落地情况：

窗口和启动预算由 PROMPT-011 唯一定义；算法只使用符号
`RecoveryTailWindow` 与 `RecoveryAttemptBudget`。

具体恢复执行算法：

```text
recoverUnresolvedPrompts():
    for each pendingRecord in Journal.getPendingPrompts(): // 状态为 Claimed 或 Submitted
        promptKey = pendingRecord.PromptKey
        targetSession = pendingRecord.SessionId
        attemptCount = pendingRecord.RecoveryAttemptCount + 1

        // 1. 从 Host SDK 读取 Session 尾部 RecoveryTailWindow 条消息
        tailMessages = HostSDK.getMessages(targetSession, limit=RecoveryTailWindow)
        
        // 2. 检查是否有 role=user 消息的 metadata 携带与 promptKey 完全一致的值
        matchedMsg = tailMessages.find(msg => msg.role == "user" && msg.metadata.promptKey == promptKey)

        if matchedMsg is Some:
            // 找到匹配的物理消息 -> 补写 PhysicalAccepted 事实，恢复成功
            Journal.commit(PhysicalAccepted(matchedMsg.id, promptKey))
        else:
            if attemptCount >= RecoveryAttemptBudget:
                // 预算耗尽仍无法证明物理落地 -> 判定为 Abandoned
                Journal.commit(Abandoned(promptKey, UnresolvedAfterRecovery))
            else:
                // 仍未超预算 -> 保持 Pending，自增 attemptCount，绝对不自动重发物理请求
                Journal.updateAttemptCount(promptKey, attemptCount)
```

提交 `PhysicalAccepted` 前复查 Journal 中同一 PromptKey 是否已提交，使重入保持幂等。

---

## PromptKey 组成与强类型

```fsharp
PromptKey = digest(
    SessionId, LogicalRunId, AuthorityRootUserMessageId,
    Origin, EffectiveAgent, PayloadDigest, ClaimSequence)
```

`ClaimSequence`：同一 `(SessionId, LogicalRunId, Origin, PayloadDigest)` 下由 journal fold 派生的单调序号——使「同一 Guard 连续发两次」成为两个 key。

ID 的类型边界见 `shape/prompt.md`。

---

## Composition · 稳定字节 · ProviderLanguage（PROMPT-014..017、PROMPT-019）

所有权见 `shape/prompt.md`；语义见 `what/prompt.md`。本节只写装配算法。

### Composition 装配

```text
assembleOfficeSystemPrompt(session):
  lang    = SessionProviderLanguage(session)     // 只读已绑；禁读全局偏好
  common  = loadLocalized(CommonLaw, lang)
  role    = loadLocalized(RoleLaw(CanonicalRole), lang)
  library = loadLocalized(OfficeLibrary(CanonicalRole), lang)  // PROMPT-016；知识≠权威
  return concat(Common Law → Role Law → Office Library)
```

Tools surface **不**并入 system 串；capability 变化不改人格（PROMPT-015）。  
Tool description 是调用合同（PROMPT-020），按已绑 `SessionProviderLanguage` 装载；不得与 system 混语（HOST-026）。  
Lifecycle orient（Activation / Reawakening / Continuation / Handoff / Fission / Departure）只注入 conversation/runtime，不替换 system 字节。  
generic Activation ≠ Manager BlindPlan；不得触发 system prompt 切换（TODO-015）。

### Provider-visible prose 装载（PROMPT-019）

```text
semantic owner (Domain text owner)
  → ProviderResources.load(path, SessionProviderLanguage)
  → already-localized string
  → SyntheticToml / ToolHostCodec（layout / escaping only）
```

禁止巨型 `TranslationRegistry`。禁止 feature 代码 `match lang` 挑选 prose。  
Bound session：目标 locale 缺资源 → fail closed；不得 silent 回落另一语言。  
Class B 技术标识与 Class C 内部 diagnostics 不经此 i18n 路径（见 what PROMPT-019）。  
参数化：资源内 `{{name}}` 模板；运行时填值不翻译。

### 同一 Life 内 system 字节冻结（PROMPT-014 / GLORY-075）

```text
onSessionCreate:
  bind SessionPersona once（AGENT-028）
  bind SessionProviderLanguage once（HOST-026）
  materialize office system prompt bytes
  freeze for Life duration

onBlindPlanT1 / onPeerFallback / onStrength / onProcessReview / onFinality
/ onCompaction / onReanchor / onRecovery:
  → conversation tool result / ExecutionBinding / runtime injection only
  → never rewrite system prompt bytes
  → never rebind Persona / SessionProviderLanguage
```

`The system prompt names the office. The conversation tells you which road is yours.`  
T1 entrustment revelation 只走 conversation tool result（TODO-015）。

### ProviderLanguage 读取（PROMPT-017 / PROMPT-019）

| 路径 | 规则 |
|------|------|
| localizable（Class A） | 按 `SessionProviderLanguage` 经 ProviderResources 取 EN / ZH；缺则 fail closed |
| invariant（Class B） | tool 名 / argument / wire field / enum / path / command / `exit_code` **原样** |
| diagnostics（Class C） | 不进 horizon；不属 Provider i18n |
| child / attached / InternalLeaf | 继承 owner/commissioner 已绑语言；不得各自再绑 |
| SyntheticToml / ToolHostCodec | 只收 already-localized 串；不拥有 prose、不读语言偏好 |

MagicTodoManagerGuideline = HOST-013 通用 guideline **加法片段**（PROMPT-013）；禁止并入 `host/pair-programming-guideline`。

### Assistance continuation（PROMPT-018）

`PromptAuthority.ContinuationKind` 增加 `NeedHelpEscalation | NeedHelpAdvice`，codec/label/fold 与其它 continuation 一样 durable。发送入口必须接受 explicit EffectiveAgent 并验证它属于 Authority profile 的同 CanonicalRole fast/deep pair；不得通过 fallback cursor 再解析目标。`NeedHelpEscalation` 只允许 fast→deep；`NeedHelpAdvice` 只允许 occasion 冻结的原 deep binding。两者 system prompt bytes 与 SessionPersona 在前后必须 byte-identical。

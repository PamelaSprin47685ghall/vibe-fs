参见 /AGENTS.md 卷 **Prompt Authority、Logical Run 与 Synthetic Continuation**（完整规范性条文）。

## Prompt Authority、Logical Run 与 Synthetic Continuation [NORMATIVE]

### 顶层不变量

```text
PhysicalUserMessage ≠ AuthorityTurn
```

Host `role=user` 只是运输格式。零宽、空白、固定模板、时间与文本长度都不是身份。

只有 **Authority Root** 可以：创建 Logical Run；选择/改变 agent、BaseModel、variant；成为 Fallback root；重置 Interaction Repair 预算；更新 LastAuthorityProfile；决定 Companion eligibility。

**Continuation**（InteractionRepair / ManagerGuard / ReviewerGuard / ReviewConfirmation / BusyAgentNudge / ProviderRetryAttempt / HostCompactionContinue）一律不得执行以上操作。B retry 只覆盖当前 Attempt 的 EffectiveModel，绝不得成为下一真人 root 的默认 model。

### Authority Root

- `HumanRoot`：外部 prompt-acceptance 边界已证明的真人输入。
- `AgentOwnerRoot`：插件显式创建的新逻辑工作（fork new / idle continue / one-shot Inspector）。必须显式 agent/model/variant（或明确 None）。

### Continuation

- 复用 `LogicalRunId` 与 `AuthorityRootUserMessageId`
- 不建新 completion/run；不更新 LastAuthorityProfile；不重置 Fallback/repair；不改变 Companion eligibility
- Busy nudge：同 RunId、同 completion、同 AuthorityRoot
- Idle existing agent 的新任务：`AgentOwnerRoot`（新 Run）

### PromptDispatcher 两阶段协议

所有插件 user-shaped message 必须经 `PromptDispatcher`：

1. `PluginPromptClaimed(PromptKey, Origin, LogicalRunId, AuthorityRoot, Agent, EffectiveModel, Variant)`
2. 带 metadata 发送：`wanxiangshu_prompt_key` / `wanxiangshu_origin` / `wanxiangshu_logical_run` / `wanxiangshu_authority_root`
3. Host 接受 → `PluginPromptAccepted(PromptKey, HostMessageId)`
4. 失败 → `PluginPromptAbandoned`
5. Host 无法关联 acceptance → fail-closed `HostContractUnsupported`（禁止当 HumanRoot）

禁止任何模块直接 `prompt_async` 发送 Guard / repair / nudge / confirmation。

### 来源解析优先级

```text
accepted HostMessageId
→ claimed PromptKey
→ Host compaction / synthetic provenance
→ registered AgentOwnerRoot
→ proven external prompt acceptance (HumanRoot)
→ UnknownOrigin
```

`UnknownOrigin` fail-closed：不更新 profile、不启动 fallback、不改变 Companion、不发送 continuation、不完成/替换 Logical Run。

**禁止** `if text = "\u200B" then Plugin else Human` 及任何按空白/固定英文/长度猜来源。

### LastAuthorityProfile

- 真人显式 agent/model/variant 永远优先并开启新 Authority + 新 Fallback epoch（Failures=0, Side=A）
- 真人省略字段只从 `LastAuthorityProfile` 继承，绝不从最后物理 user 或 B retry 继承
- Continuation 不得写回 LastAuthorityProfile

### Fallback identity

```text
logicalRunId + AuthorityRootUserMessageId + providerAttempt
```

禁止用「最后物理 user message」替代 AuthorityRootUserMessageId。

### Interaction Repair

```text
sessionId + AuthorityRootUserMessageId + terminalAssistantMessageId + repairKind
```

同一 identity 最多一次。repair continuation 的 PhysicalUserMessageId **不**进入 identity、**不**产生新预算。

### Review witness

同时记录：

- `PhysicalUserMessageId` = confirmation Host message
- `AuthorityRootUserMessageId` = 原 Reviewer task root

不得混为一个字段。

### Companion

- eligibility 只读 `ActiveLogicalRun.Profile.Agent`
- bare synthetic continuation ≠ semantic delta；其后正式 assistant 输出才可能构成 delta

### 删除清单（语义层）

```text
Session 永久 agent/model 作为 authority 来源
从最后物理 user 推导 authority
按零宽文本识别 synthetic
synthetic 更新 currentUserMessageId / Fallback root
sessionRoles 推导 Companion eligibility（目标态）
```

### 发布阻断

```text
零宽 repair 可更新 LastAuthority
synthetic 可重置 repair 预算或成为 Fallback root
无法识别来源时默认 Human
模块绕过 PromptDispatcher 发 continuation
```

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

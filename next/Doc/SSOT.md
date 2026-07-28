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

### PromptAuthorityService / PromptDispatcher 两阶段协议

每个 Plugin runtime 只有一个 `PromptAuthorityService`（由 Journal snapshot 初始化）。禁止多处 `new Dispatcher()` 各自维护内存 projection。

所有插件 user-shaped message 必须经该服务：

1. `PluginPromptClaimed(PromptKey, Origin, LogicalRunId, AuthorityRoot, Agent, EffectiveModel, Variant)`
2. 带 metadata 发送：`wanxiangshu_prompt_key` / `wanxiangshu_origin` / `wanxiangshu_logical_run` / `wanxiangshu_authority_root`
3. Host 接受 → `PluginPromptAccepted(PromptKey, HostMessageId)`；Authority Root 还写 `AuthorityRootAccepted`
4. 失败 → `PluginPromptAbandoned`
5. Host 无法关联 acceptance → fail-closed `HostContractUnsupported`（禁止当 HumanRoot）

`logicalRunId = hash(runtimeId + sessionId + authorityRootUserMessageId)`；同一 Host message 重放只产生一个 root。

AgentOwnerRoot 必须两阶段：

```text
create child
→ claim AgentOwnerRoot
→ SendPrompt（显式 agent/model/variant + PromptKey metadata）
→ Host 接受
→ AuthorityRootAccepted
→ 才允许 run 进入 active
```

禁止任何模块直接 `prompt_async` 发送 Guard / repair / nudge / confirmation / 新 child 首 prompt。

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
- 真人省略字段只从 `LastAuthorityProfile` 继承：agent / BaseModel / variant
- 省略 model 绝不从旧 Run 的 Side B EffectiveModel 继承
- Continuation 不得写回 LastAuthorityProfile

### Fallback

```text
Fallback 属于 Logical Run。
新 Authority Root 创建新 Fallback epoch：Failures=0, Side=A。
Continuation 不创建新 epoch。
B attempt 不成为未来真人 prompt 的默认模型。

FallbackAttemptIdentity =
  logicalRunId + AuthorityRootUserMessageId + providerAttempt
```

当前 Run attempt 映射：1→A, 2→A, 3→B, 4→B, 5→禁止。唯一 durable writer：`session.status=retry`。

禁止：

```text
每 Session 永久 Side B
下一 Authority Root 省略 model 时继承旧 Run 的 Side B
Session 自身拥有 A/B model 作为 authority 来源
```

### Interaction Repair

```text
InteractionRepairClaimed {
  LogicalRunId
  AuthorityRootUserMessageId
  TerminalAssistantMessageId
  RepairKind
}
```

同一 identity 最多一次。第二次仍空输出→ `MISSING_FINAL_REPORT`，禁止继续零宽自激励。

### Review witness

同时记录：

- `PhysicalUserMessageId` = confirmation Host message
- `AuthorityRootUserMessageId` = 原 Reviewer task root

不得混为一个字段；不得仅以确认文本 marker 作为授权证明。

### Companion

- eligibility **唯一** 读 `ActiveLogicalRun.Profile.Agent`
- 缺 ActiveLogicalRun：不创建 Blogger，记录 MissingAuthorityProfile
- 禁止生产备用：`sessionRoles`、最后物理 user agent、transform input agent、child linkage、历史最早带 agent 消息
- bare synthetic continuation ≠ semantic delta；其后正式 assistant 输出才可能构成 delta

### 删除清单（语义层）

```text
Session 永久 agent/model 作为 authority 来源
从最后物理 user 推导 authority
按零宽文本识别 synthetic
synthetic 更新 currentUserMessageId / Fallback root
sessionRoles 推导 Companion eligibility
新真人省略 model 时注入旧 Side B
多个 PromptDispatcher 实例各自缓存 authority
```

### 发布阻断

```text
零宽 repair 可更新 LastAuthority
synthetic 可重置 repair 预算或成为 Fallback root
无法识别来源时默认 Human
模块绕过 PromptAuthorityService 发 continuation
sessionRoles 决定 Companion eligibility
用户显式 model 被旧 Fallback side 覆盖
```

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

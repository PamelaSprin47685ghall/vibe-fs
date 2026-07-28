参见 /AGENTS.md 卷 **Prompt Authority、Logical Run 与 Synthetic Continuation**（完整规范性条文）与仓库根目录 `0.5.0.md`。

## Prompt Authority、Logical Run 与 Synthetic Continuation [NORMATIVE]

### 顶层不变量

```text
PhysicalUserMessage ≠ AuthorityTurn
```

Host `role=user` 只是运输格式。零宽、空白、固定模板、时间与文本长度都不是身份。

只有 **Authority Root** 可以：创建 Logical Run；选择/改变 SelectedAgent（准确的 `fast-*` / `deep-*`）；成为 Fallback root；重置 Interaction Repair 预算；更新 LastAuthorityProfile；决定 Companion eligibility。

**Continuation**（InteractionRepair / ManagerGuard / ReviewerGuard / ReviewConfirmation / BusyAgentNudge / ProviderRetryAttempt / HostCompactionContinue）一律不得执行以上操作。Fallback 只覆盖当前 Attempt 的 **EffectiveAgent**，绝不得改写 AuthorityExecutionProfile.SelectedAgent 或 LastAuthorityProfile。

### 0.5.0 冻结文本

> Wanxiangshu 0.5.0 使用 OpenCode Managed Agent identity 作为模型选择的唯一入口。每个公开工作角色必须拥有两个准确命名的 Agent：`fast-ROLE` 与 `deep-ROLE`。用户和 LLM 创建新工作时必须显式选择其中之一；无前缀旧名称、`build`、`plan` 以及任何隐式默认均不受支持。
>
> OpenCode 宿主最终解析后的 `opencode.json.agent` 是 Agent inventory 和 Agent→Model 绑定的唯一事实源。Wanxiangshu 不读取模型环境变量，不维护模型 catalog，不持久化模型 ID，不覆盖 Prompt 的 model 字段。Wanxiangshu 只向 Host 提供 EffectiveAgent，实际模型由 Host 根据该 Agent 的配置解析。
>
> 对公开 Agent，用户选择的 Agent 为 Side A，其同角色相反 tier Agent 为 Side B。选择 `fast-ROLE` 时，`A=fast-ROLE, B=deep-ROLE`；选择 `deep-ROLE` 时，`A=deep-ROLE, B=fast-ROLE`。Fallback cursor 按 `A/A/B/B/A/A/B/B/...` 无限循环。Provider retry 只推进 modulo-4 cursor，不存在因累计 retry 数而产生的 Dead 状态。
>
> `fast-blogger/deep-blogger` 与 `fast-executor/deep-executor` 是 Host 内部 Agent，不向任何 LLM 工具 schema 暴露。每个新的 Blogger 或 Executor summary Logical Run 固定从 fast Agent 开始，以 deep Agent 为 B，并使用相同的无限 AABBAABB 循环。
>
> Fast 与 Deep 只改变 OpenCode Agent identity 及其在 `opencode.json` 中绑定的模型。它们不改变 Canonical Role、system prompt、工具权限、Review 协议、Companion eligibility、Logical Run、Authority Root 或 completion 语义。Fallback 只能改变 AttemptExecutionProfile.EffectiveAgent，不能改写 AuthorityExecutionProfile.SelectedAgent 或 LastAuthorityProfile。

最终不变量：

```text
Agent 决定模型。
配置决定 Agent 的模型。
万象术不决定模型。

新工作必须显式选择 fast 或 deep。
Continuation 不改变用户选择。
Fallback 只改变当前 EffectiveAgent。

AABBAABB 永久循环。
没有 retry 次数死亡。

公开工作 Agent 可选择 fast/deep。
Blogger 和 Executor 内部固定 fast 起步。

角色权限仍然由静态源码控制。
模型绑定只由 opencode.json 控制。
```

### Authority Root

- `HumanRoot`：外部 prompt-acceptance 边界已证明的真人输入。必须携带准确公开 Managed Agent（`fast-*` / `deep-*`）。省略 Agent → `HostContractUnsupported`。
- `AgentOwnerRoot`：插件显式创建的新逻辑工作（fork new / idle continue / one-shot Inspector）。必须显式 Managed Agent；新 Logical Run 与 completion。

### Continuation

- 复用 `LogicalRunId` 与 `AuthorityRootUserMessageId`
- 不建新 completion/run；不更新 LastAuthorityProfile；不重置 Fallback/repair；不改变 Companion eligibility
- 物理请求使用当前 fallback cursor 对应的 EffectiveAgent
- Busy nudge：同 RunId、同 completion、同 AuthorityRoot
- Idle existing agent 的新任务：`AgentOwnerRoot`（新 Run，cursor=0，SelectedAgent=该 session 原本准确 Agent）

### 执行档案

```fsharp
type AuthorityExecutionProfile =
    { SessionId
      LogicalRunId
      AuthorityRootUserMessageId
      AuthorityKind
      SelectedAgent   // e.g. deep-reviewer
      PeerAgent       // e.g. fast-reviewer
      CanonicalRole
      SelectedTier }

type AttemptExecutionProfile =
    { Authority
      PhysicalUserMessageId
      ProviderAttempt
      EffectiveAgent  // cursor side → SelectedAgent or PeerAgent
      Origin }
```

发送 Prompt：

```fsharp
{ Agent = Some effectiveAgent; Model = None; ... }
```

禁止设置 `Model`。Host 按 `config.agent[effectiveAgent].model` 解析。

### PromptAuthorityService / PromptDispatcher 两阶段协议

每个 Plugin runtime 只有一个 `PromptAuthorityService`（由 Journal snapshot 初始化）。禁止多处 `new Dispatcher()` 各自维护内存 projection。

所有插件 user-shaped message 必须经该服务：

1. `PluginPromptClaimed(PromptKey, Origin, LogicalRunId, AuthorityRoot, SelectedAgent/EffectiveAgent, …)`
2. 带 metadata 发送：`wanxiangshu_prompt_key` / `wanxiangshu_origin` / `wanxiangshu_logical_run` / `wanxiangshu_authority_root`
3. Host 接受 → `PluginPromptAccepted(PromptKey, HostMessageId)`；Authority Root 还写 `AuthorityRootAccepted`（持久化 SelectedAgent/PeerAgent/CanonicalRole/SelectedTier，**不**持久化 model ID）
4. 失败 → `PluginPromptAbandoned`
5. Host 无法关联 acceptance → fail-closed `HostContractUnsupported`（禁止当 HumanRoot）

AgentOwnerRoot 必须两阶段：claim → SendPrompt（显式 Agent，`Model=None`）→ Host 接受 → AuthorityRootAccepted → 才允许 run 进入 active。

### LastAuthorityProfile

- 真人/Owner 显式 SelectedAgent 永远优先并开启新 Authority + 新 Fallback cursor（Offset=0, Side=A）
- 禁止省略 Agent 后默认 fast、继承 LastAuthority、或从 session role / last assistant 推断
- Continuation 不得写回 LastAuthorityProfile

### Fallback

```text
Fallback 属于 Logical Run。
新 Authority Root：Offset=0 → A。
Cursor Offset ∈ {0,1,2,3} → A,A,B,B；retry → (offset+1) mod 4。
无限循环：不存在第四次失败 Dead。
成功不推进、不重置 cursor。
唯一 durable writer：session.status=retry。
identity = logicalRunId + AuthorityRootUserMessageId + providerAttempt
```

若 Host 自身停止 retry，必须用 `ProviderRetryAttempt` continuation 延续同一 Logical Run（不新建 completion、不重置 cursor）。

### Interaction Repair / Review witness / Companion

- Interaction Repair：同一 identity 最多一次；第二次仍空 → `MISSING_FINAL_REPORT`
- Review witness：同时记录 PhysicalUserMessageId（confirmation）与 AuthorityRootUserMessageId；双 PERFECT + tree witness 不变
- Companion eligibility **唯一** 读 `ActiveLogicalRun` 的 CanonicalRole / SelectedAgent；缺 ActiveLogicalRun → 不创建 Blogger

### 删除清单（语义层）

```text
BaseModel / EffectiveModel / ModelA / ModelB 作为 Authority 选择输入
WANXIANGSHU_MODEL_* 环境变量作为模型 SSOT
无前缀 Host Agent 名称与 build/plan alias
第四次失败 LogicalRunDead / SessionDead
成功后擅自重置 fallback cursor
省略 agent → 默认 fast / 继承 LastAuthority
journal 持久化 model ID 并在恢复时覆盖 opencode.json
```

### 发布阻断（摘录）

```text
仍支持 manager/coder/reviewer/build/plan 等旧 Agent 名称
公开创建可省略 fast/deep
仍从环境变量读模型或 Prompt 仍设置 Model
Fallback 第四次失败仍判死
12 次 retry 后不再继续物理请求
Blogger/Executor 名称进入 LLM tool schema
旧 journal 被猜测性迁移
```

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

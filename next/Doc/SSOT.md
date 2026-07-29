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

角色权限仍然由静态源码控制。Inspector 的模型可见工具集合精确为 `read / glob / grep / executor`；`write`、`edit`、fork/join/list、PTY、委派与 verdict 继续 fail-closed。
Coder 暴露文件工具和不透明的 Inspector 调查；不暴露 Executor 或 PTY。Coder prompt 不得泄露 Inspector 的执行权限，也不得把 Inspector 当作常规验证代理；验证仍由 DevOps 或 Reviewer 负责。
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
- Review witness：首次 PERFECT 的 tool result 直接返回普通英文句子 `Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?`，不返回 JSON/status envelope。该 tool result 被模型消费后，同一 AuthorityRootUserMessageId 下、不同 ProviderRunIdentity 的第二次 PERFECT 立即有效；同一个 ProviderRunIdentity 内的重复 tool call 不计数。若 Reviewer 在第一次后先 terminal，Host 仍以同一句发送新的物理 ReviewConfirmation continuation，第二次 PERFECT 以其 PhysicalUserMessageId 证明因果。双 PERFECT + tree witness 不变
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

## OpenCode Session 家族资源扁平化与 Blogger 展示父级 [NORMATIVE]

OpenCode Session 的资源所有权保持家族扁平：普通 Agent、ManagerJob、one-shot Inspector/Coder、Executor summarizer 与其他内部 child 的 `parentID` 解析为当前 Session 家族最上层 root。**Companion Blogger 是唯一展示例外**：它的 Host `parentID` 指向被记录的 primary Session，使 Blogger 在 OpenCode 中可直接作为该 Agent（包括 Manager）的 subagent 查看；取消和清理仍归属家族 root。重启后两种关系都必须从 durable linkage 恢复，不能猜测。

创建与资源清理遵循：

- 普通 descendant 的 Host 父级扁平到 root；Companion Blogger 的 Host 父级是自己的 primary Session；
- root abort 收敛全部家族资源，包括以 primary 为展示父级的 Blogger；
- 单个 child abort 只关闭该 child；作用域资源必须按自己的 child ID 精确关闭；
- `join` completion、Review owner、Prompt Authority 等局部执行所有权仍由创建它的结构化程序持有，不以 Host `parentID` 反推。
- durable session association 不等于 join ownership：`AgentLinked` 可关联 Blogger 等系统 child，但不可恢复进任何 ForkRuntime mailbox；只有该 runtime 发出的 `AgentForked` 进入 `ForkedChildren`，才可在重启后恢复并由该 runtime 的 `join()` 消费。PTY completion 同样必须按创建它的 runtime 过滤。

验收：普通 `root → child → grandchild` 的两次 Host `CreateChildSession` 均收到同一个 `root` 作为 `parentID`；`CompanionHost(child)` 创建的 Blogger 收到 `child` 作为 `parentID`，并仍随 root 家族清理；进程重启后两种关系均成立。

## 父/Join 取消语义 [NORMATIVE]

`join` 工具收到 host `abort` 信号时，必须立即同步完成以下动作，然后才返回/继续：

1. 调用 `HostForkRuntime.Cancel()`，后者立即设置 `ForkRuntime.IsCancelled`，使 `runtime.Join()` 返回 `Cancelled` 而不是 `NothingToJoin`。
2. 计算当前 `HostForkRuntime` 的 parent session 与该 runtime 直接 fork 的 child session 的 ID 集合。Companion 与其他系统关联不得作为 join-owned child 混入此集合。
3. 调用 `cancelSignals` callback，对 `parentId :: childIds` 调用 `HostSignalRouter.UnregisterOwned`；这样 `HostSignalAdapter` 会丢弃这些 session 后续到达的 `session.status=idle`/`retry` 事件，从来源上阻止新的 `ProviderRetryAttempt` flush 产生。
4. 调用 `cancelFallbackRetries` callback，移除 `PluginFallbackRetry` 中已经为这些 session 排队的 `ProviderRetryAttempt` flush。
5. 同步写 `AgentUnlinked` 事实，保证崩溃恢复后这些子 session 不再被当作仍链接。

`HostForkChildDispatch.cancelParent` 把上述同步部分放在 `async { ... }` 块**之前**执行；异步清理（`ptyPort.CloseAll`、子 session `AbortSession`、清空映射表）由 `Async.StartImmediate` 启动，不阻塞 `Cancel()` 的同步返回。

`cancelFallbackRetries` 与 `cancelSignals` 两个 callback 均由 `HostForkRuntime` 构造点传入，避免 `Session` 层直接引用 `OpenCode` 的 `PluginFallbackRetry`/`HostSignalRouter`，从而打破循环文件依赖。

`HostForkRuntime.Cancel()` 为 `unit` 返回；调用者不应 await 它。需要等待清理完成的测试/代码路径应通过观察 `AgentJournal`、`HostSignalRouter` 拥有集合、`PluginFallbackRetry` 状态或子 session 的副作用间接确认。

E2E 验收：parent 收到 abort 后，继续向 child session 发送 `session.status=retry` 或 `session.status=idle` 不会触发 `RetrySignalHandler.handle` 也不会进入 `PluginFallbackRetry.scheduleFlushOnIdle`。

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

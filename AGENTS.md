# KISS Agent DSL 新架构 — 索引

| Field | Value |
| :--- | :--- |
| **Status** | 最终批准 · 新实现唯一蓝图 · **0.5.0 文档冻结中** |
| **Scope** | OpenCode first；关闭官方 compaction；不兼容旧控制状态 |
| **核心** | 结构化程序 + 极小工具 DSL + 投影式伴随博客 + 异步 fork/join |
| **0.5.0 SSOT** | `next/Doc/SSOT.md` · 蓝图 `0.5.0.md` §23 |

> 源码直接表达过程。`let!/do!/use!/while/尾递归` 负责控制流；运行时只保存真实资源、少量缓存和外部事实，不把程序计数器人工展开成 Stage/Phase/Lease/Owner/Generation。

## 0.5.0 冻结块 [NORMATIVE]

完整冻结文本见 `next/Doc/SSOT.md`（来源：`0.5.0.md` §23）。摘要：

> Wanxiangshu 0.5.0 使用 OpenCode Managed Agent identity 作为模型选择的唯一入口。每个公开工作角色必须拥有两个准确命名的 Agent：`fast-ROLE` 与 `deep-ROLE`。用户和 LLM 创建新工作时必须显式选择其中之一；无前缀旧名称、`build`、`plan` 以及任何隐式默认均不受支持。
>
> OpenCode 宿主最终解析后的 `opencode.json.agent` 是 Agent inventory 和 Agent→Model 绑定的唯一事实源。Wanxiangshu 不读取模型环境变量，不维护模型 catalog，不持久化模型 ID，不覆盖 Prompt 的 model 字段。Wanxiangshu 只向 Host 提供 EffectiveAgent，实际模型由 Host 根据该 Agent 的配置解析。
>
> 对公开 Agent，用户选择的 Agent 为 Side A，其同角色相反 tier Agent 为 Side B。Fallback cursor 按 `A/A/B/B/A/A/B/B/...` 无限循环。Provider retry 只推进 modulo-4 cursor，不存在因累计 retry 数而产生的 Dead 状态。
>
> `fast-blogger/deep-blogger` 与 `fast-executor/deep-executor` 是 Host 内部 Agent，不向任何 LLM 工具 schema 暴露。每个新的 Blogger 或 Executor summary Logical Run 固定从 fast Agent 开始。Fast 与 Deep 不改变 Canonical Role、system prompt、工具权限或 Authority Root；Fallback 只能改变 `AttemptExecutionProfile.EffectiveAgent`。

```text
Agent 决定模型。AABBAABB 永久循环。显式 fast 或 deep。Blogger/Executor 内部 fast 起步。
已删除：BaseModel / EffectiveModel / omit-model 继承 / 第四次失败判死 / WANXIANGSHU_MODEL_*。
```

## 零、设计精神

本文件不是"谁说了什么"的聊天记录筛选，而是对一年多工程探索中反复出现的**决策意图**的凝练。每条规则都应问：**这条规则替我们避免了哪种复杂性？**

### 0.1 结构化程序是本体论的，不是风格选择

Stage/Phase/Lease/Owner/Generation 本质上是把 `await` 点手动展开成表驱动的状态转换。语言运行时已经用调用栈、continuation、局部变量和 `CancellationToken` 表达了这些东西。再抄一份到业务层不会增加安全性，只会让代码跳转五层才能拼出真相。

- **正确**：`let! c = fork Coder p; let! r = joinAny (); return r`
- **错误**：`InsertRegistry(Stage.Waiting); Coordinator.Transition(Phase.Forked); LeaseManager.Acquire(Owner.Coder)`

### 0.2 事实来源拒绝二阶猜测

万象术不负责从碎片事件还原事实。OpenCode 宿主在产生每一段完整 assistant、每一次 provider retry、每一个 abort 的源码位置**已经知道完整答案**。万象术只应在那里（插件 Hook 或 SDK API 返回时）读取完整 typed 事实。

- **SSE 是碎片**：`message.updated` 在同一个 message 上可能触发多次；`part.delta` 只反映增量；`session.idle` 不携带完整上下文。
- **Reconcile 是策略**：idle 只是"可能变脏了"的信号，从 API 重读完整消息才是事实。
- **Unknown ≠ Empty**：API 还没追上事件时，等下一轮信号。不要自己发明"空输出"论。

### 0.3 LLM 前缀缓存不是可牺牲的优化

Provider KV-cache 匹配的是逐字节 token 前缀。模型输入中最早出现的消息一旦内容变化，整个缓存失效。这不是"如果时间允许"，而是**每失效一次就损失数十秒 prefix 计算时间，直接影响用户往返体验**。

- B 合成消息必须使用 `ActivePrefixEpoch.FrozenB`（冻结快照），而不是 `LatestB`（Y 最新累积）。
- 只有上下文阈值到达时才 epoch 切换——这是唯一不可避免的冷边界。
- 平常回合：X 请求前缀保持逐字节不变。Y 的 LatestB 可以继续增长，但 X 不受影响。

### 0.4 极小模型 DSL 防止 Manager 越权

Manager 只有 `fork/join/list` 不是为了让 Manager "更专业"，而是为了**防止它钻进文件细节而忽略协调责任**。同样，Orchestrator 只有 `fork/join` 是为了让它只调兵不亲征。工具权限是静态角色表，不由 prompt 劝阻管理。

### 0.5 局部状态可接受，全局状态机不可接受

允许的局部状态：正在运行的 `Task`、完成 `Channel`、FallbackCursor.Offset `byte`、SelectedAgent/PeerAgent。这些是**真实资源的引用或简单计数器**。

禁止的全局状态：`ReviewPhase`、`FallbackStage`、`NudgeLease`、`JoinOwner`。这些是**人工展开的程序计数器**。

区别在于：前者直接对应物理事实（有没有进程在跑、信箱里有没有信、总共失败了几次），后者只回答"代码执行到了第几步"。

### 0.6 A/A/B/B 不是状态机，是 modulo-4 cursor + 映射表

Fallback 不需要状态图。它只需要：

```text
FallbackCursor.Offset ∈ {0,1,2,3}
映射：0→A, 1→A, 2→B, 3→B
advance = (offset + 1) mod 4   // 仅在 session.status=retry 时推进
成功不推进、不重置；永久 AABBAABB 循环；retry 次数永不单独判死
A = SelectedAgent；B = PeerAgent（同角色相反 tier）
EffectiveAgent 由 Offset 决定；Model = None（Host 按 opencode.json 解析）
```

这就是全部。`Offset` 加上 `match` 就是 Fallback。没有任何 Stage/Phase，也没有因 retry 次数产生的 Dead。

### 0.7 Review 双 PERFECT 是避免假阳性确认

一次 PERFECT 可能是模型随口的肯定。要求两次不同 ToolCallId 的 PERFECT 增加了虚假确认的成本，杜绝了"看起来对了"的风险。Git tree hash 绑定确保审查针对的是当前实际代码状态。Post-rebase 必须重新审查，因为 ancestry 变化可能改变合并后的语义。

### 0.8 不修改 OpenCode 本体是生存策略

每次 fork 增加维护负担；上游可能拒绝 patch；用户升级 OpenCode 时会丢失功能。万象术只在现有 Hook 和 SDK API 边界内工作。对于缺少的 `turn.finished` 和 `retry.decide` Hook，用"signal+reconcile"模式替代：事件只唤醒，完整状态从 API 读取。

### 0.9 所有决策的最终检验

当你添加任何新模块、字段或控制流时，问：

1. 这是物理世界的事实（文件、进程、session、Git tree、模型输出），还是"程序下一步去哪"的信息？
2. 能否用 `let!/do!/use!/match/while/tailrec` 直接写出来？
3. 这条规则是在减少总系统熵（删除旧路径、减少概念数量），还是在增加一层新的编排？

如果答案指向"用一段程序就能写清楚但你把它拆成了三个文件加五个 DU"，回退重来。

## Prompt Authority、Logical Run 与 Synthetic Continuation [NORMATIVE]

| Field | Value |
| --- | --- |
| Status | NORMATIVE · 冻结语义 · **0.5.0 Agent-pair SSOT** |
| Scope | 所有 OpenCode user-shaped message、Managed Agent 选择、Fallback、Companion、Guard、repair、nudge |
| 核心原则 | 物理 user message 不自动拥有语义授权；只有 Authority Root 可以改变执行档案；模型由 Host+opencode.json 决定 |
| 冻结全文 | `next/Doc/SSOT.md` · `0.5.0.md` §23 |

### 一、顶层不变量

```text
PhysicalUserMessage ≠ AuthorityTurn
```

OpenCode 可以用 `role=user` 承载：真人输入、Manager 新任务/nudge、空/XML-only repair、Manager/Reviewer Guard、PERFECT 确认、compaction auto-continue、其他插件/Host continuation。传输层都是 user message，**语义权限不同**。

只有 **Authority Root** 可以：

1. 创建新的 Logical Run；
2. 选择或改变 `SelectedAgent`（并由此确定 `PeerAgent` / CanonicalRole / SelectedTier）；
3. 成为新的 Fallback root；
4. 重置 Interaction Repair 预算；
5. 改变 Companion 当前角色 eligibility；
6. 成为后续缺省 SelectedAgent 的延续来源。

Authority Root **不得**选择或覆盖 model ID；发送 Prompt 时始终 `Model = None`。

所有 **Continuation** 均不得执行以上操作。

零宽字符、空白、固定英文提示、消息创建时间和“看起来像人说的话”都不能证明消息拥有 Authority。

### 二、三个不同的身份

| 身份 | 含义 |
| --- | --- |
| `SessionId` | 消息存放的 OpenCode 会话容器（不绑定 agent/model） |
| `AuthorityRootUserMessageId` | 本次 Logical Run 的授权根 |
| `PhysicalUserMessageId` | Host 中实际存在的某一条 user-shaped message |

Authority Root 的 PhysicalUserMessageId 等于自己的 root ID。Continuation 有自己的 PhysicalUserMessageId，但必须映射回已有的 AuthorityRootUserMessageId。

### 三、消息来源类型

```fsharp
type RootAuthorityKind = HumanRoot | AgentOwnerRoot

type ContinuationKind =
    | InteractionRepair | ManagerGuard | ReviewerGuard
    | ReviewConfirmation | BusyAgentNudge
    | ProviderRetryAttempt | HostCompactionContinue

type PromptOrigin =
    | AuthorityRoot of RootAuthorityKind
    | Continuation of ContinuationKind
    | HostInternal
    | UnknownOrigin
```

- **HumanRoot**：真实用户新任务。必须显式 `fast-*` 或 `deep-*`；无前缀旧名称 / `build` / `plan` / 隐式默认 fail-closed。
- **AgentOwnerRoot**：Manager fork(new)/Idle 新任务、经授权的 one-shot Inspector/Coder 等。必须显式准确 Agent；新 Logical Run 与 completion。
- **Continuation**：只延续已有 Logical Run。不建新 RunId/completion；不改 AuthorityRoot/SelectedAgent/PeerAgent/CanonicalRole/SelectedTier；不更新 LastAuthorityProfile；不重置 Fallback/repair；不改变 Companion eligibility。物理请求使用当前 cursor 的 `EffectiveAgent`。

### 四、执行档案

```fsharp
type AuthorityExecutionProfile =
    { SessionId; LogicalRunId; AuthorityRootUserMessageId
      AuthorityKind
      SelectedAgent; PeerAgent; CanonicalRole; SelectedTier }

type AttemptExecutionProfile =
    { Authority; PhysicalUserMessageId; ProviderAttempt
      EffectiveAgent; Origin }
```

Fallback A→B 只改 `AttemptExecutionProfile.EffectiveAgent`，不得改 `AuthorityExecutionProfile.SelectedAgent` / `PeerAgent` 或 `LastAuthorityProfile`。**已删除** `BaseModel` / `EffectiveModel`。

### 五、Last Authority

每个 Session 的 `LastAuthorityProfile` 是**最后一次有权决定执行档案的 root**，不是最后一条物理 user message。

- 新真人/Owner：必须显式 `fast-*` 或 `deep-*`；由此写入 SelectedAgent/PeerAgent/CanonicalRole/SelectedTier。
- **禁止** omit-model → LastAuthority.BaseModel 继承（该路径已删除）；也禁止省略 Agent 时静默继承旧 PeerAgent。
- Continuation：显式携带继承档案（SelectedAgent / attempt EffectiveAgent / CanonicalRole / SelectedTier）；Host 内部 Session cache 变化不得写回 LastAuthorityProfile。

### 六、禁止自激励

```text
零宽 continuation → HumanRoot
repair continuation → 新 repair 预算
Review confirmation → 改 Reviewer SelectedAgent
Manager Guard → 改 Manager SelectedAgent
Busy nudge → 新 RunId
synthetic → 重置 Offset / 成为 currentUserMessageId / 改 Companion eligibility
B retry → 下一真人 root 默认 Agent
按 text="\u200B" / 空白 / 固定提示 / 长度判断来源
omit-model / 无前缀旧名称 / build|plan 作为合法 Host identity
向 Host Prompt 覆盖 Model
```

`"\u200B"` 只是运输载荷，不是身份标记。

### 七、PromptDispatcher 两阶段协议

所有插件 user-shaped message 必须经 `PromptDispatcher.Send`。禁止模块直接 `prompt_async` 发 Guard/repair/nudge/confirmation。

1. 先持久化 `PluginPromptClaimed`；
2. 再调 Host，metadata 至少含 `wanxiangshu_prompt_key` / `wanxiangshu_origin` / `wanxiangshu_logical_run` / `wanxiangshu_authority_root`；发送形状为 `{ Agent = Some effectiveAgent; Model = None; ... }`；
3. Host 返回 PhysicalUserMessageId → `PluginPromptAccepted`；
4. 失败 → `PluginPromptAbandoned`（不得改变 Active Logical Run）；
5. Host 丢弃 metadata 且无法 correlation → **禁止发送**，`HostContractUnsupported`。

### 八、来源解析优先级

```text
accepted HostMessageId
→ claimed PromptKey
→ Host compaction/synthetic
→ registered AgentOwnerRoot
→ proven external prompt acceptance (HumanRoot)
→ UnknownOrigin
```

`UnknownOrigin` fail-closed：不更新 profile、不启动 Fallback cursor、不触发 Companion、不发 continuation、不完成/替换 Logical Run；记录 HostContractViolation。

不得把“不是 synthetic”直接等价为 Human。

### 九、Logical Run

- 新 Authority Root：新 LogicalRunId、更新 LastAuthorityProfile、清空旧 continuation set、新 repair budget、新 Fallback cursor（`Offset=0`）。
- Continuation：复用 LogicalRunId 与 AuthorityRootUserMessageId，加入 ContinuationMessageIds。
- Busy nudge：同 Run、同 completion、同 AuthorityRoot。
- Idle existing agent 新任务：AgentOwnerRoot（新 Run/completion/显式 fast-*/deep-*）。

### 十、Fallback

```text
FallbackAttemptIdentity =
  logicalRunId + AuthorityRootUserMessageId + providerAttempt
```

文档与代码中的 `currentUserMessageId` 语义必须是 **AuthorityRootUserMessageId**。Continuation 的 PhysicalUserMessageId 永远不得替代它。

新 Authority Root → 新 Fallback cursor（`Offset=0`，A=SelectedAgent，B=PeerAgent）。Continuation 沿用同一 cursor。`session.status=retry` 仅推进 `(offset+1) mod 4`：永久 `A/A/B/B/...`。成功不推进、不重置。**不存在**因累计 retry 数而产生的 Dead 出口；后续物理 request 必须继续。若 Host 停 retry，用 `ProviderRetryAttempt` continuation 延续同一 Logical Run。

### 十一、Interaction Repair

```text
InteractionRepairIdentity =
  sessionId + AuthorityRootUserMessageId + terminalAssistantMessageId + repairKind
```

repair continuation 自己的 PhysicalUserMessageId **不进入** identity。同一 identity 最多一次。

### 十二、Review 与 Guard

- Review confirmation：`Origin=ReviewConfirmation`，AuthorityRoot=原 Reviewer task root。Witness 同时记 Physical confirmation id 与 AuthorityRootUserMessageId。
- Manager/Reviewer Guard：不建新任务、不改 SelectedAgent、不更新 LastAuthority、不重置 Fallback/completion，只延续原 Logical Run。

### 十三、Companion

- eligibility 只读 ActiveLogicalRun 的 Canonical Role / SelectedAgent。
- 不得读 Session 永久 role、最后物理 user 的 agent、synthetic 临时 agent、linkage 推导 role。
- Blogger/Executor 为内部 `fast-*/deep-*` pair；新 Blog/Executor summary Logical Run 固定 fast 起步；名称不进入 LLM tool schema。
- bare synthetic continuation ≠ semantic delta；其后正式 assistant 输出才可能构成 delta。

### 十四、持久事实（最小）

```text
AuthorityRootAccepted
PluginPromptClaimed / PluginPromptAccepted / PluginPromptAbandoned
LogicalRunClosed
FallbackCursorAdvanced（PreviousOffset → NextOffset = +1 mod 4）
```

Session 有界投影：`LastAuthorityProfile`、`ActiveLogicalRun`、`PendingClaims`、`AcceptedContinuationIds`、`FallbackCursor`。

### 十五、实现修改清单（目标态）

删除：`BaseModel`/`EffectiveModel`、模型环境变量 SSOT、omit-model 继承、第四次失败判死、`sessionRoles` 作为 authority/Companion 来源、从 AgentLinked 推导 parent role、从最后物理 user 推导 authority、按零宽文本识别 synthetic、synthetic 更新 currentUserMessageId、向 Host 覆盖 Model。

新增/保持：`PromptAuthorityProjection`、`AuthorityExecutionProfile`（SelectedAgent/PeerAgent/…）、`AttemptExecutionProfile`（EffectiveAgent）、`LogicalRunBinding`、`PromptDispatcher`、claim/accept/abandon facts、modulo-4 FallbackCursor。

`chat.message`：解码 origin → AuthorityRoot 建档案 / Continuation 强制继承 profile / Unknown fail-closed。

所有 Prompt 发送点必须走 Dispatcher：TerminalPolicies、HostReviewGuard、ReviewerHost、HostSessionNudge、HostForkRuntime busy nudge、compaction continuation、plugin-owned fallback retry。

### 十六、必须通过的 E2E（摘要）

A 零宽不能变权 · B 零宽不能无限 repair · C 真人必须显式 fast/deep · D 禁止 omit-model BaseModel 继承 · E Busy nudge 同 Run · F Idle continue 新 OwnerRoot · G Review confirmation 双 id · H Companion 不被 synthetic 切换 · I restart 恢复 profile/run/claims/repair/fallback cursor · J 12 次 retry 后仍继续物理 request。

### 十七、发布阻断

```text
零宽 repair 可更新 LastAuthority
synthetic 可重置 repair 预算或成为 Fallback root
Companion 从 sessionRoles 决定 eligibility
用户显式 SelectedAgent 被旧 Fallback side 覆盖
无法识别来源时默认 Human
模块绕过 PromptDispatcher 直接发 continuation
无前缀旧名称 / build|plan / 省略 fast|deep
Fallback 第四次失败仍判死
向 Host Prompt 设置 Model
12 次 retry 后不再继续物理请求
```

最终冻结表述：

> Session 没有固定 agent 或 model。Authority Root 拥有 SelectedAgent/PeerAgent/CanonicalRole/SelectedTier。真人 User Root 与 Agent Owner 必须显式选择 `fast-*` 或 `deep-*`；不得覆盖 model ID。Guard、repair、确认、Busy nudge 与 Host continuation 虽然以 user message 形式运输，但没有授权能力；它们只能继承既有 Logical Run，永远不能通过自身存在改变后续执行语义。Fallback 只改变当前 EffectiveAgent，按 AABBAABB 永久循环。

## 一、卷表

|卷|主题|
|---|---|
|`KISS-N00.md`|第一原理：两层 DSL，删除状态机平台|
|`KISS-N01.md`|Structured Program 内核与 computation expression 语法糖|
|`KISS-N02.md`|Projection / Companion Blogger / A、B 工作记录|
|`KISS-N03.md`|异步 `fork / join / list` 与完成邮箱|
|`KISS-N04.md`|角色、能力矩阵与同步局部子程序|
|`KISS-N05.md`|Executor / Process / Output Summary / PTY|
|`KISS-N06.md`|Logical Run 无限 AABBAABB Agent-pair Fallback|
|`KISS-N07.md`|Manager Guard、Reviewer Guard、双 PERFECT|
|`KISS-N08.md`|Orchestrator / Worktree / Rebase / Review / FF|
|`KISS-N09.md`|OpenCode Host Adapter、投影管线、工具 Schema|
|`KISS-N10.md`|保姆式实施、测试、迁移与删除清单|
|`KISS-N11.md`|Canary Mock 剧本森林（完整前缀、无 mute/编号）|
|`KISS-N12`|Prompt Authority / Logical Run / Synthetic Continuation|

## 二、已经冻结的产品语义

1. OpenCode 官方 compaction 关闭。
2. 每个有伴随的 Session `X` 拥有廉价 Blogger Session `Y`。
3. `A` = X 的 session-wide 模型输出累积，**含正式正文与 reasoning/thinking**，不含 tool raw stream；`B` = Y 当前投影中所有正式 assistant 输出，不含 Y 输入和 reasoning。
4. X 的 ProjectedInputTokens + ReservedOutputTokens 超过 ContextLimit 且 BlogBase coverage proof 通过后，投影层用 B 等价替换已被 B 覆盖的前缀；此后每次投影继续替换。Cutoff 必须位于完整 semantic turn 边界；CoveredPrefixDigest 必须在投影前重新验证。Estimator 不可用时不切换 epoch。
5. Delta 在 canonical JSON 投影层计算；Y 忙时不打断、不排插件队列、不推进 delta 基线，下一次自然包含跳过期间的全部变化。
6. Y 自身接近上限时，把旧 B 作为唯一正文输入重投影；Y 新输出 B' 替代旧 B。
7. Manager 无 read/write/edit/grep/glob 等普通工具；只有 `fork / join / list`。无 PTY、无 executor。
8. `fork(fast-*|deep-* agent, prompt)` 创建异步子代理；`fork(existingId, prompt)` 是 fire-and-forget nudge/continue。Busy existing agent 不创建新 RunId、不安装新 listener、不创建新 completion；nudge 归属于当前 active Run。**原因**：如果 busy nudge 替换 active RunId，原始 fork 对应的 completion 会被覆盖而永久丢失。nudge 只是 "在当前运行的尾部追加提醒"，其结果属于同一次 completion。Nudge 成功 = Host 已确认接受 prompt。Busy→idle 竞态以 Host AcceptPrompt 返回的 run identity 为归属依据。若 Host 不支持 busy append，返回 BusyNudgeUnsupported。
8b. **OpenCode Session 家族扁平化**：儿子的儿子仍是家族 root 的儿子。所有 Agent、ManagerJob、经授权的 one-shot Inspector 或 Coder、Blogger 与 Executor child 的 Host `parentID` 都解析为最上层 root；重启恢复同一 root。root abort 收敛全部 child，单 child 精确关闭；`join`/Review/Authority 的结构化程序所有权不从 Host `parentID` 反推。
9. `join()` 等任意一个完成项；不指定对象。每个 RunIdentity 对应 single-assignment completion cell。Terminal/SendFailure/Cancel 竞争 TrySetResult，首个成功者唯一生效。join 消费后永久删除 completed handle。
10. `list()` 统一显示 Agent 与 PTY，但内部资源实现保持独立。
11. Inspector 直接使用 `read / glob / grep` 获取静态代码事实，并可同步调用 Executor Tool 查询 Git、历史与其他直接文件工具无法提供的只读事实；Coder 可为具体且必要的代码事实创建一次性 Inspector，但 Coder prompt 不得暴露 Inspector 的 Executor 权限。Coder 不得把 Inspector 当作常规测试/构建代理；验证由 DevOps 或 Reviewer 负责。
12. Executor Agent 只负责命令输出摘要；无工具、无伴随。Executor Tool 负责真实进程。
12b. **DevOps** 独占 `fork-pty`，并可 `executor` / `read` / `glob` / `grep` / `inspector` / 同步 `coder` / `join` / `list`。不得直接 write/edit。Manager 通过 `fork(fast-devops|deep-devops, prompt)` 委派终端操作。
13. `3 × estimated_running_secs` 是进程唯一时限；无其他 timeout 层。模型允许用巨大 estimate 主动申请巨大预算。
14. `actual_output_bytes > 3 × estimated_output_bytes` 时触发 spool；摘要按 200KB 块做在线 ripple-carry reduce（fan-in=8，按 chunk index 排序，Executor ID 由 processId+level+range hash 生成），不积存全部 map summary。
15. `estimated_mem_usage=large` 全 OpenCode 进程同时最多一个；medium 不限并发。
16. Fallback 属于 Logical Run：A/B 是 SelectedAgent/PeerAgent 对。`session.status=retry` 推进 `FallbackCursor.Offset`：`(offset+1) mod 4` → 永久 `A/A/B/B/...`，**不存在**因累计 retry 数而产生的 Dead。成功不推进、不重置 cursor。新 Authority Root 始终新 cursor（`Offset=0`，A=SelectedAgent）。公开创建必须显式 `fast-*`/`deep-*`；发送 Prompt 时 `Agent=EffectiveAgent`、`Model=None`。唯一写 durable cursor advance 的入口是 `session.status=retry`；空/XML-only 不进入 A/B 计数。`currentUserMessageId` 必须是 AuthorityRootUserMessageId，不包括插件 synthetic 消息。
17. Review：REVISE 一次立即生效；两个 PERFECT 必须来自不同 ProviderRunIdentity。首次 `PERFECT` 的 tool result 进入下一次 provider request 后，同一 Authority Root 下的第二个 ProviderRun 可直接确认；若 Reviewer 先 terminal，则 Host 发送 `ReviewConfirmation` physical continuation 后确认。一个 assistant message 内的重复 tool call 不算第二次。
18. ReviewGuard 同时守 Manager 结束和 Reviewer 未给出有效 verdict。
19. PTY 使用独立工具 `fork-pty`（仅 DevOps），signal 使用结构化 enum，不使用魔法字符串。PTY completion 只由 backend `onExit` 触发；Signal/Close 不提前完成。
20. Orchestrator 只有 `fork / join`；fork Manager 自动建 worktree、进入 ReviewGuard；发布前 rebase、复审、串行 ff。Rebase 后的审查必须是全新的双 PERFECT（两个新 ToolCallId），不得复用 rebase 前的确认。
21. 用户向 Orchestrator 发消息时，目标工作区 dirty 则拒绝。
22. 万象阵、todowrite SSOT、select_methodology、通用 nudge、fuzzy 工具与同步 subagent 伪工具全部删除。
23. **B 前缀缓存保护**：`CurrentB` 拆分为 `LatestB`（Y 最新工作记忆）与 `ActivePrefixEpoch`（冻结的 B 快照）。Epoch 切换算法：ProjectedInputTokens + ReservedOutputTokens > ContextLimit。**`companion-b-head` 合成消息在两次 epoch 切换之间必须保持逐字节不变。** Synthetic ID = hash(sessionId + epochId + semanticKind)，禁止随机值。
24. **SSE 只是唤醒信号**：业务事实不从碎片事件拼装。`session.status=idle` 建立 Dirty latch，触发 single-flight reconcile；`session.status=retry` 是唯一 durable fallback 入口。`message.updated`/`message.part.updated`/`part.delta`/`session.updated` 被直接丢弃。Unknown 协议：单次 idle 后最多 3 次因果重读，仍 Unknown 则保持 Dirty 等下一信号。
25. **Host 事实通过 SDK API 读取**：completion、ReviewGuard、continuation、abort 都先从 reconcile 得到完整的 `ReconciledTurn` 再决策。Unknown（API 尚未反映当前 Run 的 assistant）= 不产生副作用，保持 PendingRun。
26. **不修改 OpenCode 本体**：生产功能只在现有插件 Hook（`chat.message`/`experimental.chat.messages.transform`/`tool.execute.before`/`after`/`event`）和 SDK API 边界内工作。
27. **稳定性门禁**：scenario-local 因果 Watchdog 超时 2 秒；只认因果进展。Watchdog deadline > 被测动作 deadline + cleanup allowance。Event-stagger 规则：第一个 canary 立即启动，canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。Release gate 恰好运行 3 轮。最多重复 3 次。
28. **Provider-visible projection**：缓存比较只使用真正进入模型的字段（role/text/reasoning/tool call/result），排除 timestamp/cost/usage/runtimeId 等非模型 metadata。
29. **Fallback identity**：`logicalRunId + AuthorityRootUserMessageId + providerAttempt`。Single-flight `retry` 事件到达后 append `FallbackCursorAdvanced`（`NextOffset = (PreviousOffset + 1) mod 4`）。空/XML-only terminal 触发 InteractionRepairIdentity 去重（最多一次）。

## 三、总形状

```text
User → Orchestrator DSL
          └─ fork ManagerJob ── worktree ── Manager DSL
                                  ├─ fork Coder
                                  ├─ fork Inspector
                                  ├─ fork DevOps
                                  ├─ fork Browser
                                  ├─ fork Meditator
                                  ├─ fork Reviewer
                                  ├─ join any completion
                                  └─ list live handles

Any companion-enabled Session X
    └─ projection delta → cheap Blogger Y → B record
       X context projection: B replaces covered prefix

Coder
    ├─ file read / write / edit
    ├─ narrow opaque Inspector facts
    └─ verification handoff → DevOps / Reviewer
```

## 四、最重要的代码审查问题

看到任何新增模块或字段时，只问：

1. 这是用户/宿主/Git/进程真实存在的事实，还是“程序运行到哪一步”？
2. 能否放回 `let! / do! / use! / match / while / tail recursion`？
3. 能否由真实 `Task / Process / Session / Git tree` 直接派生，而无需缓存状态？
4. 若必须有缓存，它是否只是一个值或一个正在运行的 Task，而不是第二套调度器？

若答案指向程序计数器，删除字段，重写为结构化程序。


---

# KISS-N00 — Structured Agent Program 第一原理

[NORMATIVE] 本卷是新架构总纲。旧 KISS 中“结构化程序替代手写状态机”的原则保留；旧 Todo、Review State、Prompt Lease、Subsession Actor、Squad DAG 等具体实现不继承。

---

## 一、断根：不要把 await 展平成治理平台

一个真实流程本来是：

```text
fork 两个调查者
→ join 先完成者
→ 根据结果 fork coder
→ join coder
→ fork reviewer
→ reviewer REVISE 则继续 coder
→ reviewer 连续两次 PERFECT
→ finish
```

[FORBIDDEN] 把它改写成：

```text
ManagerStage
ChildPhase
PendingJoinOwner
ReviewLease
VerdictGeneration
ContinuationRegistry
NudgeCoordinator
```

这些名词没有增加任何用户事实，只是把语言运行时原本维护的 continuation、局部变量、调用栈和 await 点搬进业务数据。

[NORMATIVE] 新实现必须让源码重新长成顺序程序：

```fsharp
agent {
    let! inspector = fork Inspector "调查根因"
    let! coder = fork Coder "实现修复"
    let! first = joinAny ()
    do! react first
    let! review = reviewUntilConfirmedPerfect ()
    return review
}
```

真实可靠性放进动词内部：`fork` 完成注册，`joinAny` 完成邮箱消费，`use!` 完成资源释放，`reviewUntilConfirmedPerfect` 完成确认循环。调用者不拼装状态机。

---

## 二、两层 DSL

### 2.1 模型可见 DSL

模型只看极小工具语言：

```text
Manager:      fork / join / list
DevOps:       fork-pty / executor / read / glob / grep / inspector / coder / join / list
Orchestrator: fork / join
Reviewer:     verdict(PERFECT|REVISE) + 角色允许的只读工具
```

这是一门动态 Agent DSL。它不是工作流 JSON，也不是持久化 AST。每次工具调用直接映射为一个真实动词。

### 2.2 实现者 DSL

F# 源码使用 computation expression：

```text
agent { ... }
companion { ... }
process { ... }
review { ... }
orchestrator { ... }
```

它们都是同一个可执行闭包内核的类型别名或薄 Builder，不是五套解释器。

[NORMATIVE]

- `let!`：等待一个领域动词并取得值。
- `do!`：执行一个无返回值领域动词。
- `use!`：获取并异步释放真实资源。
- `match`：显式处理领域结果。
- `while`：条件循环；条件每轮重新读取真实投影。
- 尾递归：找到即停、有界重试、确认循环。
- `parallel`：对独立动作并发，返回全部结果。

[FORBIDDEN]

- Flow AST。
- 动态 Stage Registry。
- continuation 序列化。
- 通用 Workflow Engine。
- 为了“可观察”而持久化程序计数器。

---

## 三、什么可以有局部状态

KISS 不等于禁止变量。禁止的是把控制位置伪装成领域。

### 3.1 允许

|值|原因|
|---|---|
|`Task<AgentCompletion>`|真实正在运行的异步工作|
|`Channel<Completion>`|真实完成邮箱|
|`Process` / PTY handle|真实 OS 资源|
|`BlogText`|真实 B 版缓存|
|`BlogBaseJson`|JSON delta 的真实基线|
|`PrefixReplacementEnabled`|投影策略已启用的配置事实|
|`ModelSide = A|B`|当前 Logical Run 的 Fallback 侧；新 Authority Root 重置为 A|
|`FailureCount`|每 Session 真实失败预算消耗|
|`perfectConfirmations` 局部变量|本次 reviewer 确认函数的递归参数|

### 3.2 禁止

|字段|原因|
|---|---|
|`CurrentStage`|程序计数器|
|`NextAction`|第二调度器|
|`JoinOwner`|Channel 已表达消费者语义|
|`ReviewPhase`|递归和 verdict 工具已表达|
|`FallbackPhase`|一个函数和两个值足够|
|`NudgeLease`|nudge 是直接 prompt，不是领域资源|
|`CompactionGeneration`|投影替换由当前值直接计算|
|`SquadWaveState`|Orchestrator 动态 fork/join，不维护固定 DAG|

---

## 四、源码应呈现的五条主程序

### 4.1 Companion

```text
canonical projection
→ JSON delta against last successful blogger base
→ blogger free ? start one step : skip
→ maybe remember prefix replacement
→ emit X projection
```

### 4.2 Manager

```text
LLM turn
→ fork / nudge / join / list
→ assistant terminal
→ ManagerGuard accepts ? finish : append guard prompt and continue
```

### 4.3 Reviewer

```text
review files
→ verdict(REVISE) : immediate
→ verdict(PERFECT) : ask confirmation
→ second PERFECT : confirmed
→ terminal without verdict : append reviewer guard prompt and continue
```

### 4.4 Process

```text
spawn with pumps installed
→ run until exit or 3× estimated seconds
→ timeout: SIGKILL process tree
→ drain
→ if actual > 3× estimate: summarize 200KB chunks
→ return
```

### 4.5 Orchestrator

```text
clean gate
→ fork manager worktrees in parallel
→ join completions
→ for each publish candidate under serial integration gate:
     rebase latest target
     resolve conflict through same manager
     review again
     ff
```

---

## 五、恢复与持久化边界

新架构不复活旧 NDJSON 控制平台。

[NORMATIVE]

- OpenCode Session transcript 是对话事实源。
- X 与 Y 的伴随关系优先写入宿主 Session metadata；宿主不支持时才用一个极小 association store。
- B 可从 Y 当前投影的 assistant 正文重建；缓存丢失只影响性能，不改变事实。
- Git commit/tree/worktree 是代码事实源。
- 正在运行的 Process、PTY、LLM 请求是进程资源；进程退出后不伪装成仍可恢复的 Running State。
- 活跃 fork 列表是运行期资源表，不写成事件溯源平台。
- Per-runtime NDJSON 只保存跨进程仍成立的领域事实；不保存调用栈、程序计数器、未完成 Task。
- 崩溃后通过 Boot Fold 恢复最新的领域事实，然后用普通程序逻辑决定下一步。不是恢复暂停的协程。

## 六、Host 边界：事件是信号，不是数据

### 6.0 为什么？

碎片事件有以下固有缺陷：

1. **一个完整 turn 被拆成多次事件**：`message.updated` 在 streaming 期间可能触发数十次；`part.delta` 只反映增量；`session.type` 与 `session.status` 可能以任意顺序到达。
2. **拼装是 CPU 密集且不确定的**：每收到一条碎片都要更新内存中的 parts 聚合器、比较 message ID、检查是否 final。并且即使全部拼好，也可能因竞态而拼错。
3. **宿主已经在产生完整事实时知道答案**：Streaming 结束后，Host 已经拥有完整的 assistant parts、finish reason、outcome。万象术不应等碎片再来还原它已经知道的东西。
4. **Unknown ≠ Empty**：API 查询尚未反映最新状态时，正确行为是等待下一轮 idle 信号，不是自己编造一个"空输出"论。

所以：事件只应做**唤醒信号**——"某个 session 可能变脏了，请去 API 读取完整状态"。业务事实由 Reconciler 从 SDK API 读取。

### 6.1 分层

```text
碎片事件（message.updated / part.delta / session.updated）
    ↓ 丢弃
粗粒度信号（session.status idle/retry, session.deleted）
    ↓ single-flight
Reconciler 从 SDK 读取完整消息
    ↓
ReconciledTurn（完整 parts / outcome / role / model）
    ↓
纯策略（completion / ReviewGuard / continuation / abort）
```

[NORMATIVE]

- 唯一允许进入生产业务层的事件类型：`session.status`（idle/retry）、`session.deleted`。
- `message.part.delta`、`message.part.updated`、`message.updated`、`session.updated`、`session.diff` 在最早边界丢弃。
- `idle` 不直接代表 terminal 完成，只触发一次 connected reconcile loop。
- `retry` 是唯一写持久 fallback 的入口。
- Unknown（API 尚未显示当前 Run 的 assistant）不产生副作用，不判为 Empty/Failed。
- 同一事件重复到达不重复消费：`Reconciler` 是 single-flight，`Fallback` 由 stable identity 去重。

Same Session 的 event 来源二选一（插件 `event` Hook 或 global SSE），不双投递。

## 七、LLM 前缀缓存保护

缓存是重要的，不是可以随意破坏的优化。

[NORMATIVE]

- `LatestB`（Y 最新工作记忆）与 `ActivePrefixEpoch`（X 当前使用的冻结 B 快照）分离。
- 平常回合 LatestB 累积，X 请求前缀保持逐字节不变。
- 只有达到下一次上下文阈值时才 epoch 切换，产生一次必要冷边界。
- `caps-head`、system、tools 在 replacement 区域外固定不变。
- Provider-visible projection 只包含真正进入模型的字段（role/text/reasoning/tool call ID/name/arguments/result），排除 timestamp/cost/usage/runtimeId 等非模型 metadata。
- Transform Hook 必须是幂等的：检测已有 `caps-head` 则不再重复注入。
- 同一模型、同一 epoch 下：tools(N+1) == tools(N)、system(N+1) == system(N)、messages(N) 是 messages(N+1) 的逐字节前缀。

### companion-b-head 不可变性

`companion-b-head` synthetic message 在两次 epoch 切换之间**必须保持逐字节不变**。不能因为 LatestB 累积、Blogger 新段落或任何其他原因修改它的 content。

```fsharp
// 正确：FrozenB 是 epoch 冻结时的快照，epoch 切换前不变
let head = injectEpochHead epoch.FrozenB

// 错误：LatestB 仍在增长，每轮 B head 内容都不同，破坏缓存
let head = injectBHead memory.LatestB
```

Epoch 切换是唯一可预期冷边界。切换后新 FrozenB 再次冻结。

[FORBIDDEN]

- 每次 blogger 成功后修改 X 最早消息的正文。
- 把 LatestB 直接放入 X 的 active prefix。
- 重建 Blogger 后不发送 full reset frame 就使用旧 projection baseline。
- 使用非幂等 transform Hook 导致重复注入 caps/prepend。
- 使用 Host runtimeId/timestamp 等非模型字段参与 canonical equality 比较。
- 同一 session 同时导出两个非幂等的 transform Hook 实现。

## 八、不修改 OpenCode 本体

[NORMATIVE] 生产功能只在现有插件 Hook 和 SDK API 边界内工作。宿主不改，不要求增加新 Hook。

- `chat.message` — 确认新 turn 被接受，清理上一轮 pending。
- `experimental.chat.messages.transform` — Companion 唯一输入。
- `tool.definition` / `tool.execute.before` / `tool.execute.after` — 权限与结果处理。
- `experimental.session.compacting` / `experimental.compaction.autocontinue` — 控制 compaction。
- `event` — 接收极少数粗粒度信号（idle/retry/deleted）。
- SDK `client.session.create` / `session.messages` / `prompt_async` — 主动创建、读取、发送。

不依赖的边界：

- 无 `turn.finished` Hook。
- 无 `retry.decide` Hook。
- 不监听 `message.updated` / `part.updated` / `part.delta`。
- 不依赖 events.listen 或 /global/event 的完整 payload 结构。

Adapter 层负责：把现有 Hook 参数转成 typed input，调用领域层 Flow。

---

## 九、命名原则

允许同一用户表面出现 `executor` 角色与 `executor` 工具，因为语境清楚；实现中必须由类型命名空间区分：

```fsharp
type AgentRole = ... | Executor
module Tool =
    let executor : ToolDefinition = ...
```

不得为了避免同名引入 Translator、Governor、Broker 等无价值中间层。

---

## 十、终极验收

打开生产主流程文件，读者应在一分钟内看见：

- 谁 fork 谁；
- 谁 join；
- 哪些资源 `use!`；
- 哪些失败递归重试；
- Reviewer 如何确认；
- Orchestrator 如何发布。

若必须跳转五个 Registry 才知道下一步，重构失败。


---

# KISS-N01 — Structured Program 内核与语法糖

本卷定义真实可编译形状。示例使用 F# / Fable；具体 Task/Promise 适配只在 Host Adapter。

---

## 一、唯一内核 [NORMATIVE]

```fsharp
type Flow<'ctx, 'error, 'a> =
    private
    | Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)
```

它是闭包，不是 AST。

```fsharp
module Flow =
    val run:
        'ctx ->
        CancellationToken ->
        Flow<'ctx, 'error, 'a> ->
        Task<Result<'a, 'error>>

    val fail: 'error -> Flow<'ctx, 'error, 'a>

    val liftTask:
        ('ctx -> CancellationToken -> Task<'a>) ->
        Flow<'ctx, 'error, 'a>
```

`Bind` 只执行左侧、错误短路、成功传值。它不自动重试、不写日志、不刷新投影、不解释错误。

---

## 二、Builder [NORMATIVE]

```fsharp
type FlowBuilder<'ctx, 'error>() =
    member Return: 'a -> Flow<'ctx, 'error, 'a>
    member ReturnFrom: Flow<'ctx, 'error, 'a> -> Flow<'ctx, 'error, 'a>
    member Bind:
        Flow<'ctx, 'error, 'a> *
        ('a -> Flow<'ctx, 'error, 'b>) ->
        Flow<'ctx, 'error, 'b>
    member Zero: unit -> Flow<'ctx, 'error, unit>
    member Delay: (unit -> Flow<'ctx, 'error, 'a>) -> Flow<'ctx, 'error, 'a>
    member Combine:
        Flow<'ctx, 'error, unit> *
        Flow<'ctx, 'error, 'a> ->
        Flow<'ctx, 'error, 'a>
    member TryWith: ...
    member TryFinally: ...
    member Using:
        'resource * ('resource -> Flow<'ctx, 'error, 'a>) ->
        Flow<'ctx, 'error, 'a>
        when 'resource :> IAsyncDisposable
    member While: ...
    member For: ...
```

[NORMATIVE]

- `Using` 必须 await `DisposeAsync`。
- 通用 Flow 不吞 `OperationCanceledException`；领域 run 再映射。
- 可预见分支使用成功通道 DU，不要把所有分支都塞进 `'error`。
- `for` 体不承担非局部 early return；找到即停使用尾递归。

---

## 三、五个领域别名，不是五套内核

```fsharp
type AgentFlow<'a> = Flow<AgentContext, AgentError, 'a>
type CompanionFlow<'a> = Flow<CompanionContext, CompanionError, 'a>
type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>
type ReviewFlow<'a> = Flow<ReviewContext, ReviewError, 'a>
type OrchestratorFlow<'a> = Flow<OrchestratorContext, OrchestratorError, 'a>

let agent = FlowBuilder<AgentContext, AgentError>()
let companion = FlowBuilder<CompanionContext, CompanionError>()
let process = FlowBuilder<ProcessContext, ProcessError>()
let review = FlowBuilder<ReviewContext, ReviewError>()
let orchestrator = FlowBuilder<OrchestratorContext, OrchestratorError>()
```

领域差异在 Context、Error 和动词模块，不在 Builder。

---

## 四、结构化组合子

### 4.1 并行

```fsharp
val parallel:
    Flow<'ctx, 'error, 'a> list ->
    Flow<'ctx, 'error, 'a list>
```

[NORMATIVE]

- 每个动作共享只读 Context，禁止共享可变请求对象。
- 父取消传播。
- 全部 Task 被观察，不产生无人收割异常。
- 结果顺序与输入顺序一致；完成事件若需要按物理先后，使用 `joinAny`，不要复用 `parallel`。

Coder 可用 Inspector 解决具体且必要的未知代码事实，但其 prompt 只将 Inspector 描述为不透明的只读调查服务，不暴露 Executor。不得把 Inspector 当作常规测试、typecheck 或 build 代理；这些验证仍交给 DevOps 或 Reviewer。

### 4.2 尾递归

Fallback、双 PERFECT、Reviewer 无 verdict 继续都写成局部递归：

```fsharp
let rec confirmPerfect confirmations =
    review {
        match! nextVerdictOrTerminal () with
        | Revise report -> return Revision report
        | Perfect when confirmations = 0 ->
            do! requestSecondConfirmation ()
            return! confirmPerfect 1
        | Perfect ->
            return ConfirmedPerfect
        | TerminalWithoutVerdict ->
            do! nudgeReviewer ()
            return! confirmPerfect confirmations
    }
```

这段代码本身就是控制流，不需要 `ReviewStage`。

### 4.3 资源作用域

```fsharp
process {
    use! child = spawn request
    return! runToCompletion child
}
```

```fsharp
orchestrator {
    use! worktree = createWorktree target
    let! result = runManager worktree
    return! publish worktree result
}
```

资源释放由 `use!` 覆盖成功、领域失败、异常、取消。

---

## 五、Script 语法糖

Context 不应暴露胖对象。为每个流程提供窄 Script：

```fsharp
type AgentScript =
    { Fork: ForkRequest -> AgentFlow<ForkAck>
      JoinAny: unit -> AgentFlow<Completion>
      List: ListFilter -> AgentFlow<HandleView list>
      PromptSelf: string -> AgentFlow<unit>
      CurrentTree: unit -> AgentFlow<GitTreeHash> }
```

调用者写：

```fsharp
agent {
    let! child = s.Fork (NewAgent(Coder, prompt))
    let! completion = s.JoinAny()
    return completion
}
```

而不是：

```fsharp
agent {
    do! s.Registry.Insert(...)
    do! s.Actor.Dispatch(...)
    do! s.Stage.SetWaiting(...)
    return! s.EventBus.Wait(...)
}
```

---

## 六、错误模型

建议最小 DU：

```fsharp
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

type ProcessError =
    | SpawnFailed of string
    | TimedOut of ProcessDiagnostics
    | Killed of ProcessDiagnostics
    | PumpFailed of ProcessDiagnostics

type ReviewError =
    | ReviewerFailed of string
    | ParentCancelled

type OrchestratorError =
    | DirtyWorkspace of string list
    | RebaseFailed of string
    | PublishFailed of string
```

普通命令非零退出码是值，不默认 ProcessError。

---

## 七、禁止扩建 CE

[FORBIDDEN]

- 自定义 `goto`。
- `BreakSignal` AST。
- `Stage` 解释器。
- 为每种工具建一个新 Builder。
- 为了观察流程而让每个 `Bind` 发事件。

观察通过正常日志与真实资源快照完成，不污染控制结构。


---

# KISS-N02 — Projection 与 Companion Blogger

本卷定义 X/Y、A/B、JSON delta、忙时跳过、X 前缀替换与 Y 自压缩。

---

## 一、术语 [NORMATIVE]

- `X`：主 Session。
- `Y`：X 的伴随 Blogger Session，使用便宜模型，无工具。
- `A(X)`：**整个 Session X 生命周期**内 assistant 正式正文 + reasoning/thinking 的累积（不含 tool raw stream）。不是某一轮，也不是最后一轮。
- `B(X)`：**整个 Companion Session Y 生命周期**内当前有效工作日志的累积（`LatestB` / 自压缩后的 `B'`），不含 Y 的输入和 reasoning。不是某一轮 delta 段落。
- `CanonicalProjection`：Host transcript 经纯规范化后的 JSON。
- `BlogBase`：最近一次成功被 Y 消化的 CanonicalProjection。
- `Delta`：`JsonDelta(BlogBase, CurrentCanonicalProjection)`。
- `PrefixReplacementEnabled`：X 已进入 B 前缀替换模式。

B 是摘要缓存，不是 Session 状态机。

---

## 二、消息协议

### 2.1 Y 初始系统消息

```text
You are the blogger of a coding agent session.
See below for the session content.
Write dense, factual work-log prose.
Do not call tools.
Do not reproduce raw code or stream of consciousness.
```

新建 X 的 Y 时，再提供父 Session 的工作记录作为背景；优先父 B，若无 B 则用父 session-wide formal A；B 与 A 皆空则省略。

### 2.2 普通增量轮

Y 收到两个 user message：

```yaml
kind: session_delta
messages:
  - ...
```

```text
You are the blogger of a coding agent session.
Write one new paragraph for these delta messages.
Avoid raw code or stream of consciousness and maximize information density.
```

Y 的本轮 assistant 正文直接追加进 B。

### 2.3 Y 自压缩轮

Y 的投影接近自身上限时，旧 Y 上下文前缀被替换为一条 user message：

```yaml
kind: existing_blog
content: |-
  <old B>
```

随后请求：

```text
Rewrite the supplied existing blog as a standalone, denser work record.
Preserve concrete decisions, results, failures, paths and unresolved work.
```

新 assistant 输出是 `B'`。旧 B 此时是 Y 的输入，不再是 Y 的输出，所以 `B(X) := B'`，无需额外删除算法。

---

## 三、Canonical JSON 投影

[NORMATIVE] Delta 不基于事件流，不做文本 diff。每次 Host 即将构造模型输入时：

```fsharp
let canonical = CanonicalProjection.ofHostInput hostInput
let delta = JsonDelta.between memory.BlogBase canonical
```

Canonical JSON 只保留 Blogger 需要的语义：

```fsharp
type CanonicalMessage =
    { MessageId: string
      Role: string
      Kind: string
      Content: JsonValue
      ToolName: string option
      ToolCallId: string option }
```

包括：

- 用户输入；
- 工具输入和输出；
- assistant 正文；
- Host 可公开提供的 reasoning；
- 错误和退出结果。

不包括：

- UI 临时字段；
- token 计数噪音；
- Blogger 自己的 synthetic replacement message；
- 与语义无关的时间抖动字段。

JSON 规范化必须稳定：属性排序、空字段策略、字符串换行策略固定。这样结构相同就不会产生伪 delta。

---

## 四、忙时跳过自然实现

```fsharp
type CompanionMemory =
    { mutable LatestB: string                         // Y 最新累积工作记忆
      mutable BlogBase: JsonValue                     // 最近被 Y 消化的 canonical projection
      mutable InFlight: Task<BlogStepResult> option

      // X 前缀缓存保护：ActivePrefixEpoch 冻结后不随 LatestB 改变
      mutable PrefixReplacementEnabled: bool
      mutable ActivePrefixEpoch: ActiveEpoch option }  // 冻结的 B 快照，epoch 切换时才变

type ActiveEpoch =
    { EpochId: string
      FrozenB: string
      CutoffMessageId: string
      CoveredPrefixDigest: string }
```

### Cutoff 证明算法

[NORMATIVE]

```text
Cutoff 只能位于完整 semantic turn 边界。
CoveredPrefixDigest = hash(provider-visible messages[0..cutoffExclusive]).
投影前必须重新计算该 digest。
不匹配时禁止替换，保留原始上下文。

Semantic turn 边界定义：
  - 用户输入 + 对应的完整 assistant 输出（含所有 tool call/result 来回）
  - 完整的 user→assistant 轮次，不得在 tool call pending 状态截断
  - pending→completed 的 tool part 更新不破坏已 seal 的前缀
  - message ID 相同但 content 变化（retry/revert/undo）→ 原 cutoff 无效
```

[NORMATIVE] 不建立 Blogger 队列。LatestB 累积不影响 X 的 active prefix。

```fsharp
let offerDelta current =
    companion {
        match memory.InFlight with
        | Some task when not task.IsCompleted ->
            // 不打断、不排队、不推进 BlogBase
            // X 的 active prefix 不受影响
            return ()

        | _ ->
            let delta = JsonDelta.between memory.BlogBase current
            if JsonDelta.isEmpty delta then
                return ()
            else
                let baseAfterSuccess = current
                let task =
                    runBloggerStep memory.LatestB delta
                    |> Task.bind (fun paragraph -> task {
                        // LatestB 累积，但不修改 X 的 active prefix
                        memory.LatestB <- memory.LatestB + "\n\n" + paragraph
                        memory.BlogBase <- baseAfterSuccess
                    })
                memory.InFlight <- Some task
                observeFailureWithoutQueue task
                return ()
    }```
```

[NORMATIVE] 唯一原子 durable fact：

```text
CompanionAdvanced {
  sessionId
  bloggerId
  previousBaseDigest
  newBaseCanonical
  completeLatestB
}
```

只有 append `CompanionAdvanced` 成功后才更新内存。
重启只 fold journal；宿主 metadata 只能是可重建索引，不能和 journal 平级。

Crash window 恢复：
- LatestB 已写，BlogBase 未写 → 以 journal 为准，回退 BlogBase
- BlogBase 已写，LatestB 未写 → 以 journal 为准，重算 LatestB
- association 已写，B 未写 → 删除 association，视为 Blogger 未创建

---

## 五、X 的 B 前缀替换（缓存保护）

### 5.0 为什么需要 Epoch 分离？

Provider KV-cache 匹配的是**逐字节 token 前缀**。模型输入中最早出现的消息内容一旦变化（即使身份 ID 相同），从该消息开始所有后续消息的 KV-cache 全部失效。

旧设计把 B 直接放在 X 的上下文开头，每轮 B 随 LatestB 增长而改变内容。结果：**每次 Blogger 成功都让 X 的整个前缀缓存报废**。这是 P0 级性能 Bug。

修复：把"Y 的最新工作记忆"（`LatestB`）与"X 已冻结的前缀快照"（`ActivePrefixEpoch.FrozenB`）拆成两个独立概念。平常回合 LatestB 增长但 X 前缀不动；只有上下文阈值到达时才切换 Epoch，产生一次不可避免的冷边界。

### 5.1 关闭官方 Compaction

OpenCode 官方 compaction 必须关闭。

[NORMATIVE] Epoch 切换算法：

```text
ProjectedInputTokens =
  tokens(system + tools + caps + active messages + current request)

SwitchEpoch iff:
  ProjectedInputTokens + ReservedOutputTokens > ContextLimit
  AND tokens(FrozenCandidate) + tokens(rawTail) < tokens(current active messages)
  AND BlogBase coverage proof succeeds

Estimator 不可用时，使用保守估算：
  按 UTF-8 bytes ÷ 3 估算 token 数
  ReservedOutputTokens = max(2048, estimated_output_tokens)
  ContextLimit = min(provider_max, model_max, host_max)
  三者任一不可知 → fail-closed，不切换 epoch
```

```fsharp
if shouldActivateEpoch memory then
    memory.ActivePrefixEpoch <-
        Some { EpochId = deterministicEpochId memory
               FrozenB = memory.LatestB
               CutoffMessageId = currentCutoffMessageId
               CoveredPrefixDigest = hash current }
    memory.PrefixReplacementEnabled <- true
```

此后每次投影：

```fsharp
let projectForX canonical =
    match memory.ActivePrefixEpoch with
    | None -> canonical                     // 未启用替换前，原始输出
    | Some epoch ->
        Prefix.replaceCoveredPart
            coveredBy = epoch.FrozenB
            replacement = syntheticBlogMessage epoch.FrozenB
            current = canonical
```

[NORMATIVE]

- **LatestB 与 ActivePrefixEpoch 严格分离**：平常回合 LatestB 持续累积，X 请求的前缀逐字节不变。只有 epoch 切换才引发一次冷缓存。
- 切换后 X 请求形状变为 `frozenB + rawTail`，后续 LatestB 继续增长但 X 的前缀冻结。
- 下一次 epoch 切换：当前 `frozenB + rawTail` 接近上限 → 新的 `frozenB'` 替换前缀 → rawTail 变短。
- `BlogBase` 本身就是"B 已覆盖到哪里"的 JSON 证据，不另造 cursor/generation。
- replacement message 标记内部 provenance，CanonicalProjection 计算 delta 时过滤，避免 Y 总结自己的 B。
- 一旦启用，本 Session 生命周期内持续启用；不来回开关。

投影输出形状：

```text
System instructions
Tools
Fixed caps-head (不参与 replacement)
Frozen companion epoch (只在 epoch 切换时变化)
Raw append-only tail (只追加，不回写)
Current request
```

### Seal Barrier（追加冻结）

[NORMATIVE]

```text
Provider request R_n 发出后，R_n 中的所有 provider-visible bytes 永久 sealed。
尚未进入任何 provider request 的 tail entity 可以 upsert。
每次新请求只允许在上一次 sealed bytes 后追加。

Seal 边界 = 最终进入 provider 的完整消息序列，
不是 OpenCode 原始 message（因为 part 会追加/更新）。
只有 terminal 后的 semantic turn 可被 seal。
```

## Provider-visible projection

缓存比较只使用真正进入模型的字段：

```fsharp
type ProviderVisibleMessage =
    { Role: string
      Text: string option
      Reasoning: string option
      ToolCallId: string option
      ToolName: string option
      ToolArgs: JsonValue option
      ToolResult: JsonValue option
      SyntheticProvenance: string option }
```

### Synthetic 稳定身份

[NORMATIVE]

```text
syntheticId = hash(sessionId + epochId + semanticKind)
同一 epoch 内 role/content/parts/IDs/order 全部逐字节固定
不得使用 GUID、Math.random、当前时间

Synthetic provenance 不进 canonical delta，不进 Provider-visible cache comparison。
```

排除字段：timestamp、cost、usage、runtimeId、directory、status、UI metadata、finish reason。

Companion delta、prefix equality 和缓存门禁测试必须使用同一份 Provider-visible projection。

---

## 六、B 的读取

`B(X)` 是 **Y 整个 session** 的正式工作日志累积，不是单轮 delta 段落：

```fsharp
let currentB yProjection =
    yProjection.Messages
    |> List.choose AssistantFormalText.tryGet
    |> String.concat "\n\n"
```

不读取 Y 的 user 输入，因此旧 B 在自压缩时作为输入出现，不会混入新 B。

`LatestB` 是 Y 的 **session-wide 完整累积输出**。`ActivePrefixEpoch.FrozenB` 是冻结的快照版本。两者关系：

- 平常回合：`FrozenB` 不变，`LatestB` 增长。
- Epoch 切换：`FrozenB = LatestB`（冻结），之前 rawTail 中的旧内容不再向前传递。
- 自压缩：`LatestB` 替换为自压缩后的 B′，但 `FrozenB` 保持不变，直到下次 epoch 切换才搬过去。

创建子代理（含有/无伴随）时：

```text
System role prompt
Parent work record background (B preferred, else session-wide A)
Current fork prompt
```

[NORMATIVE] ChildBackground =
  fork 动词开始时的不可变快照，优先级：
  1. 父 session durable B（有 ActivePrefixEpoch 时用 FrozenB，否则 LatestB）
  2. 若无 B（无 companion、LatestB 空、或尚未产生 B）：父 session-wide formal A
  3. B 与 A 皆空：省略背景

任何需要「父工作记录背景」的路径（fork child、经授权的 one-shot Inspector 或 Coder、新建 Y）都遵循该优先级；不得在无 B 时默默省略而可用 A 仍存在。

它只是背景，不声称父模型已经见过；
必须记录 ParentBackgroundDigest（实际注入文本的 hash；兼容字段名 ParentBDigest）；
创建失败重试时复用同一快照，不重新读取最新值。

父工作记录是背景，不要求 Manager 重复解释仓库历史。

---

## 七、投影主函数

```fsharp
let projectSession (s: CompanionScript) hostInput =
    companion {
        let canonical = s.Canonicalize hostInput
        do! s.OfferDelta canonical

        if s.ShouldEnablePrefixReplacement canonical then
            do! s.EnablePrefixReplacement()

        return s.ProjectForModel canonical
    }
```

主函数不出现：

- message event subscription；
- compaction stage；
- summary owner；
- pending delta queue；
- prefix generation；
- Blogger scheduler。

---

## 八、必须测试

1. 第一轮生成 B。
2. Y 忙时连续三次 X 投影，Y 只启动一次；空闲后下一 delta 包含三次变化。
3. Y 输出不含其输入。
4. X 开启替换前输出完全等于 canonical。
5. 开启后 Epoch 替换 BlogBase 前缀，raw suffix 保留。
6. LatestB 增长不改变 X 的 active prefix（epoch 未切换时 X 前缀逐字节不变）。
7. synthetic B 不进入下一次 delta。
8. Y 自压缩后 LatestB 只等于 B'，ActivePrefixEpoch 不变。
9. Epoch 切换后 X 请求产生一次可预期的冷边界，之后恢复 append-only。
10. Blogger 失败不阻塞 X 投影。
11. reasoning 缺失时机制仍正确。
12. JSON 属性顺序变化不产生 delta。
13. 工具大输出经过 canonical 后可正确 YAML literal 编码。
14. Provider-visible projection 变化而非模型字段变化，不产生 delta。
15. Transform Hook 幂等：重复调用不重复注入 caps-head/companion-b-head。
16. Same semantic content but different Host metadata → same Provider-visible digest.


---

# KISS-N03 — 异步 Fork / Join / List DSL

本卷定义 Manager 的全部控制面。Subagent 不再伪装成同步工具。

---

## 一、模型工具 Schema [NORMATIVE]

### 1.1 fork

```json
{
  "agent": "coder | inspector | browser | meditator | reviewer | devops | <hex6>",
  "prompt": "string"
}
```

`fork-pty`（仅 DevOps）：

```json
{
  "agent": "pty | <ptyId>",
  "prompt": "string",
  "signal": "TERM | KILL | INT | HUP | ... (optional)"
}
```

约束：

- Manager `fork` 不再接受 PTY/`signal`。
- `fork-pty` 的 `signal` 仅当 `agent` 指向 PTY handle 时合法。
- 新建普通 agent 时 `prompt` 非空。
- 已有 agent ID + prompt = nudge/continue。
- PTY ID + prompt 非空 = write。
- PTY ID + prompt 空 = read 请求。
- PTY ID + signal = 发结构化 signal。

### 1.2 join

```json
{}
```

等待任意一个完成项。返回对象自带 handle ID、kind、role 和 A 版工作记录或 PTY 结果。

### 1.3 list

```json
{
  "kind": "all | agent | pty (optional)"
}
```

返回当前运行期资源的派生视图。

Orchestrator 只暴露 `fork / join`，不暴露 `list`。

---

## 二、运行时真实结构

```fsharp
type HandleId = private HandleId of string

[NORMATIVE]

```text
HandleId 至少包含 parent session identity；
同一 parent 生命周期内永不复用；
重启恢复原 handle，不能重新生成；
已退休 ID 永久返回 RetiredHandle，不回落为"把它当角色名"。
```

type AgentHandle =
    { Id: HandleId
      Role: AgentRole
      SessionId: string
      CompanionSessionId: string option
      Lifetime: CancellationTokenSource }

type PtyHandle =
    { Id: HandleId
      Pty: IPtyProcess }

type RuntimeHandle =
    | Agent of AgentHandle
    | Pty of PtyHandle
    | ManagerJob of ManagerJobHandle

type Completion =
    | AgentCompleted of HandleId * AgentRole * ARecord
    | AgentFailed of HandleId * AgentRole * string
    | PtyRead of HandleId * string
    | PtyExited of HandleId * int
    | ManagerPublished of HandleId * CommitHash
    | ManagerFailed of HandleId * string
```

基础设施：

```fsharp
type ForkRuntime =
    { Handles: ConcurrentDictionary<HandleId, RuntimeHandle>
      Completions: Channel<Completion> }
```

这不是 AgentStateMachine。状态直接来自：

- handle 是否存在；
- Host Session 是否 busy/idle；
- PTY Process 是否退出；
- Completion Channel 中是否有值。

---

## 三、新建 Agent

```fsharp
let forkNew role prompt =
    agent {
        let parentB = currentParentB ()
        let! child = host.CreateChildSession(role, parentB)
        let handle = register child role

        // 先监听，再发送；极快 terminal 也不会丢。
        attachTerminalProjection handle

        // Send 返回只表示请求已交给宿主；不等待 child 完成。
        do! host.SendPrompt(child.SessionId, prompt)
        return Forked handle.Id
    }
```

[NORMATIVE]

- fork 返回得快；child 在后台自然运行。
- 有伴随角色自动创建 Y。
- child 初始上下文自动含父工作记录（优先 B，无 B 则 session-wide A）。
- terminal 提取 **session-wide A**（整个子 Session 正式正文 + reasoning/thinking 累积），不含 tool raw stream。
- completion 写入 Channel；Manager 下一次 `join()` 获取。

---

## 四、已有 Agent ID = Nudge

```fsharp
let nudgeExisting handle prompt =
    agent {
        // 故意 fire-and-forget；不建插件队列，不等待当前 run。
        // Busy existing agent：不创建新 RunId、不安装新 listener、不创建新 completion。
        // Nudge 归属于当前 active Run。只有 idle 后再 fork 才创建新 completion。
        host.PostPromptFireAndForget(handle.SessionId, prompt)
        return Nudged handle.Id
    }
```

[NORMATIVE]

```text
Nudged = Host 已确认接受 prompt
NudgeSubmitted = 已尝试提交，不保证接受

若 Host 不支持 busy append，不能用 prompt queue 补救（SSOT 禁止插件队列）；
正确结果应是显式 BusyNudgeUnsupported，而不是返回成功。

Fire-and-forget Task 必须被最小观察：失败写诊断，不能触发第二调度器。
```

[NORMATIVE] Busy→idle 竞态归属：

```text
Host AcceptPrompt 返回的 run/message identity 是唯一归属依据。
不能仅依据运行时缓存的 Busy/Idle。
无法获得 identity 时，该 Host 不满足 busy-nudge contract。
```

---

## 五、Join

```fsharp
let joinAny () =
    agent {
        let! completion = runtime.Completions.Reader.ReadAsync(ct)
        return completion
    }
```

[NORMATIVE]

- 哪个先进入完成邮箱就返回哪个。
- 不允许传 child ID。
- 不做结果排序。
- 不建立 Waiter Registry；Channel 是邮箱本身。
- child 极快完成也不会丢，因为 terminal listener 在发送或创建时即安装。
- 父取消时 `ReadAsync` 被取消。

若当前无活跃 handle，`join()` 应返回明确的 `NothingToJoin` 工具结果，避免模型永等。这个判断直接由 Handles/PTY 派生，不保存 `JoinState`。

[NORMATIVE] Completion exactly-once：

```text
每个 RunIdentity 对应一个 single-assignment completion cell。
Terminal/SendFailure/Cancel 竞争 TrySetResult，首个成功者唯一生效。
Mailbox 只接收该 cell 的唯一结果。
join 消费后永久删除 completed handle。
```

---

## 六、List

```fsharp
let list filter =
    runtime.Handles.Values
    |> Seq.choose (HandleView.project filter)
    |> Seq.toList
```

示例：

```text
a1b2c3  fast-coder      busy
91ff02  deep-reviewer   idle
0304aa  pty             running
```

`busy/idle/running/exited` 是展示层瞬时派生值，不进入持久化领域。

---

## 七、父取消与关闭

父 Session abort：

```fsharp
agent {
    for handle in ownedHandles do
        do! cancelPhysicalResource handle
    return ()
}
```

- Agent：调用宿主 abort/close。
- PTY：SIGKILL 整个进程树并等待退出。
- ManagerJob：取消 manager，保留或清理 worktree 按 Orchestrator 策略。

不写 `CancellingStage`。`cancelPhysicalResource` 返回后，资源必须已经完成物理收敛或暴露真实 bug。

---

## 八、工具调用映射

```fsharp
let executeFork input =
    agent {
        match resolve input.Agent with
        | NewRole role ->
            return! forkNew role input.Prompt
        | Existing (AgentHandle h) ->
            return! nudgeExisting h input.Prompt
        | Existing (PtyHandle p) ->
            return! forkPtyOperation p input
        | Existing (ManagerJob _) ->
            return! agent.fail (InvalidFork "manager job cannot be nudged here")
    }
```

一个 `match` 就是完整 dispatcher；禁止另建 CommandBus/OperationRegistry。

---

## 九、测试

1. fork 返回前 terminal listener 已安装（或用 reconcile 代替后 equivalent）。
2. 极快 child 结果可被后续 join 取得。
3. 两 child 完成先后不同，join 按邮箱先后返回。
4. nudge running child 不打断、不建插件队列、不创建新 RunId。
5. nudge idle child 可继续同一 session。
6. list 同时显示 agent/pty。
7. join 无活跃资源返回 NothingToJoin。
8. 父取消关闭所有 owned handles。
9. session-wide A **含 reasoning/thinking**，不含 tool raw stream；`finalText` 必须是完整 A 而非最后一轮。
10. 父工作记录自动进入新 child 背景（B 优先；无 B 则 session-wide A）。
11. Busy agent → nudge → RunId 不变 → completion 恰好一次（不重复、不丢失）。
12. Executor summarizer 使用私有 mailbox，不从 Manager mailbox 偷 completion。


---

# KISS-N04 — 角色与能力矩阵

角色不是可动态组合的权限平台。第一版使用静态表和静态工具注册。

---

## 一、角色表 [NORMATIVE]

|角色|伴随 Blogger|可用工具|说明|
|---|---:|---|---|
|`orchestrator`|是|`fork`, `join`|只 fork ManagerJob|
|`manager`|是|`fork`, `join`, `list`|不直接读写仓库；无终端|
|`coder`|是|`read`, `write`, `edit`, `glob`, `grep`, `inspector`|真正修改代码；Inspector 仅用于窄范围事实调查|
|`inspector`|否|`read`, `glob`, `grep`, `executor`|只读静态调查；命令查询仅经 Executor；无直接 Python/JS execute|
|`devops`|否|`fork-pty`, `executor`, `read`, `glob`, `grep`, `inspector`, `coder`, `join`, `list`|终端操作员；文件改动仅经 coder 工具|
|`browser`|否|`read`, web tools|仓库读取与上网|
|`meditator`|否|`read`, `glob`, `grep`, `inspector`|推理、方案、权衡|
|`reviewer`|否|`read`, `glob`, `grep`, `inspector`, `verdict`|只读审查；verdict 结构化|
|`executor` Agent|否|无|命令大输出 summarizer|
|`blogger`|否|无|增量工作记录|

[NORMATIVE] 任何未列出的工具都必须在 schema 层不可见，不是 execute 时拒绝。Coder 可见 `inspector`，但不得看到或调用 `executor`、PTY 或其他命令能力；Inspector 的内部 Executor 权限不向 Coder prompt 泄露。Coder 不得把 Inspector 当作常规验证代理。

规范工具集合：

```text
Coder      = read, write, edit, glob, grep, inspector
Inspector  = read, glob, grep, executor
DevOps     = fork-pty, executor, read, glob, grep, inspector, coder, join, list
Browser    = read, glob, grep, web tools
Meditator  = read, glob, grep, inspector
Reviewer   = read, glob, grep, inspector, verdict
Orchestrator = fork, join
Manager    = fork, join, list
Executor Agent = （无）
Blogger    = （无）
```

### PTY 与 Executor 权限规则

[NORMATIVE]

1. **Inspector 可直接使用只读文件工具，并可使用 Executor Tool。**
   * Inspector 不得拥有 `fork`、`join`、`list`。
   * Inspector 不得创建、读取、写入、发送信号或关闭 PTY。
   * Inspector 不得创建任何 subagent。
   * Inspector 的完整工具集合必须严格等于：

```text
read, glob, grep, executor
```

2. **只有 DevOps 可以创建和操作 PTY。**
   * DevOps 通过独立工具 `fork-pty` 创建 PTY（`agent=pty`）。
   * DevOps 通过 `fork-pty(existingPtyId, operation)` 对已有 PTY 执行输入、读取、Signal 或 Close。
   * PTY handle 出现在 DevOps 的 `list()` 中。
   * PTY completion 进入 DevOps 的 `join()` 邮箱。
   * Manager 只 `fork(devops, prompt)` 委派终端操作，自身无 `fork-pty`/`executor`。
   * Orchestrator、Coder、Inspector、Browser、Meditator、Reviewer、Executor、Blogger 均不得直接操作 PTY。
   * DevOps 不得直接 write/edit；文件修改经同步 `coder` 工具委派。

3. **`fork` / `fork-pty` 的可见语义按角色静态收窄。**

```text
Manager 的 fork 支持：
  fork(agent, prompt)   # fast-coder|deep-coder|fast-inspector|deep-inspector|fast-browser|deep-browser|fast-meditator|deep-meditator|fast-reviewer|deep-reviewer|fast-devops|deep-devops
  fork(existingAgentId, prompt)

DevOps 的 fork-pty 支持：
  fork-pty(agent=pty, prompt)
  fork-pty(existingPtyId, prompt|signal)

Orchestrator 的 fork 只支持：
  fork-manager(explicitFastOrDeepManagerAgent, managerPrompt)

其他角色不得看到 fork / fork-pty。
无前缀 coder/reviewer/... 以及 build/plan 不是合法 Host identity。
```

4. **权限必须在工具 Schema 层隐藏。**
   角色无权使用的工具或 union variant，必须根本不出现在其模型可见 Schema 中。

5. **Inspector 需要执行命令时只能调用 Executor Tool。**
   Executor Tool 负责：
```text
启动非交互命令
执行唯一的 3 × estimated_running_secs 时限
捕获 stdout/stderr
按输出预算进行 spool 和摘要
返回结构化命令结果
```

[FORBIDDEN]

- fuzzy_grep / fuzzy_glob / fuzzy_continue。
- Manager 继承普通工具。
- Reviewer 写文件。
- Blogger 调工具。
- 动态 Tool Capability Registry。

---

## 二、静态配置

```fsharp
type RoleDefinition =
    { Role: AgentRole              // Canonical Role（源码 SSOT）
      Prompt: string
      Companion: bool
      Tools: ToolId list }
// 0.5.0：不再在 RoleDefinition 内保存 ModelA/ModelB。
// Host identity = fast-ROLE / deep-ROLE；模型只在 opencode.json.agent[*].model。

let roles : Map<AgentRole, RoleDefinition> =
    [ orchestratorDefinition
      managerDefinition
      coderDefinition
      inspectorDefinition
      browserDefinition
      meditatorDefinition
      reviewerDefinition
      executorAgentDefinition
      bloggerDefinition ]
    |> List.map (fun x -> x.Role, x)
    |> Map.ofList
```

这是静态数据，不支持插件运行期改写角色拓扑。新增角色必须代码审查。

---

## 三、Coder → Inspector 是窄范围、不透明的调查

Coder 的模型可见工具包括 `read`、`write`、`edit`、`glob`、`grep` 与 `inspector`。只有在自身文件工具不能确定一个具体、必要的代码事实时，Coder 才能创建一次性 Inspector 并消费其 findings。

[NORMATIVE]

- Coder prompt 只把 Inspector 描述为不透明、只读的调查服务；不得泄露 Inspector 的 Executor 权限或内部工具 schema。
- Coder 必须先使用 `read`、`glob`、`grep`；不得以模糊的“检查一切”请求滥用 Inspector。
- Coder 不得把 Inspector 当作常规 test、lint、typecheck 或 build 代理；这些验证仍由 DevOps 或 Reviewer 负责。
- Coder 完成改动后交付文件变更与精确的验证交接；收到外部失败报告时可读取相关代码和测试并修复。

---

## 四、Inspector → Executor Tool

Inspector 调用：

```json
{
  "command": "...",
  "estimated_output_bytes": 12345,
  "estimated_running_secs": 30,
  "estimated_mem_usage": "medium | large"
}
```

Tool 执行真实命令。若输出触发摘要，Tool 内部创建一次性 Executor Agent：

```text
Executor Agent system prompt
Inspector A 版工作记录背景
Command + 200KB chunk(s)
```

Executor Agent 只返回摘要正文，不调用工具。

同名消歧：

- `AgentRole.Executor` = 模型 summarizer。
- `Tool.executor` = OS command tool。

用户语义清晰，实现类型安全即可，不改产品名。

---

## 五、角色 Prompt 只写职责，不写调度状态

正确：

```text
You are a reviewer. Inspect the current worktree.
Use verdict(REVISE) immediately when changes are required.
Use verdict(PERFECT) twice to confirm a flawless result.
```

错误：

```text
You are currently in ReviewStage=AwaitingConfirmation.
Owner=manager; Generation=4; Lease expires...
```

Prompt 不携带内部程序计数器。

---

## 六、背景注入

新角色 Session 的固定顺序：

```text
Role system prompt
Parent work record (B preferred, else session-wide A; omit if both empty)
Fork prompt / local request
```

不自动注入父完整 transcript。需要精确代码事实时由角色读文件或 Inspector 执行命令。

---

## 七、测试

1. 每角色工具集合精确匹配表。
2. Manager 无 read/write/edit。
3. Reviewer 无写工具。
4. Blogger/Executor Agent 无工具。
5. Coder 的 schema 含 `inspector` 但不含 `executor`/PTY；Coder prompt 不泄露 Inspector 的内部 Executor 权限，且不把 Inspector 作为常规验证代理。
6. Inspector 精确拥有 `read` / `glob` / `grep` / `executor`；无其他工具。
7. 新 child 背景：父 B 优先；无 B 则父 session-wide A。
8. 未注册工具在 schema 层不可见。


---

# KISS-N05 — Executor / Process / Output Summary / PTY

进程是资源作用域。唯一执行时限来自模型给出的 estimate；系统不再叠加多层 timeout。

---

## 一、Executor Request [NORMATIVE]

```fsharp
type MemoryEstimate = Medium | Large

type ExecutorRequest =
    { Command: string
      WorkingDirectory: string
      EstimatedOutputBytes: int64
      EstimatedRunningSeconds: int64
      EstimatedMemoryUsage: MemoryEstimate }
```

派生值：

```fsharp
let processLimit request =
    TimeSpan.FromSeconds(float request.EstimatedRunningSeconds * 3.0)

let summaryTrigger request =
    request.EstimatedOutputBytes * 3L
```

[NORMATIVE]

- 模型允许填巨大 estimate；运行时不 clamp 到隐藏上限。
- `3 × estimated_running_secs` 是从进程启动到正常退出的唯一时间预算。
- Spawn、等待、SIGKILL、pump drain 不各自获得新 timeout。
- 超时后发 SIGKILL/平台等价的 kill entire process tree。
- Kill 后无第二兜底 timeout；若 SIGKILL 路径不能返回，这是实现 bug，测试应暴露。
- `Medium` 不做并发限制。
- `Large` 使用 OpenCode 进程级 `SemaphoreSlim(1)`。

[NORMATIVE]

```text
running_secs > 0
output_bytes >= 0
必须是有限数
乘法使用饱和/checked 语义
超过可表达范围 = effectively unbounded，由 CancellationToken 终止
```

---

## 二、Process DSL

```fsharp
let runExecutor request =
    process {
        use! largeLease =
            match request.EstimatedMemoryUsage with
            | Medium -> noLease
            | Large -> acquireGlobalLargeLease ()

        use! child = spawnWithPumps request
        let! result = waitOrKill child (processLimit request)
        return! maybeSummarize request result
    }
```

没有：

- ProcessStage；
- KillPhase；
- TimeoutCoordinator；
- MemoryLease 日志状态；
- ExecutorOwner。

`largeLease` 就是一个 semaphore releaser 资源。

---

## 三、泵序 [NORMATIVE]

```text
Create process object
→ connect stdout/stderr
→ start bounded pumps
→ start process
→ close/finish stdin protocol
→ await exit or unique timer
→ timer wins: kill entire process tree
→ await physical exit
→ await pumps EOF
→ build ProcessResult
→ async dispose
```

[FORBIDDEN]

```text
Start → WaitForExit → Read stdout
```

它会因管道填满死锁。

结果：

```fsharp
type ProcessResult =
    { ExitCode: int
      Stdout: OutputCapture
      Stderr: OutputCapture
      Duration: TimeSpan
      TimedOut: bool }
```

非零退出码是命令事实，返回 Inspector 判断。

---

## 四、大输出与 200KB 在线 Ripple-Carry 摘要

触发条件：

```text
stdout bytes + stderr bytes > 3 × estimated_output_bytes
```

触发后：

[NORMATIVE]

```text
chunk = 连续合并 stdout/stderr 后的 UTF-8 byte stream，每块 204800 bytes
fan-in = 8
map 并发，但结果按 chunk index 排序
每凑齐 8 个同 level summary，立即 reduce 为 level+1
最终从低 level 到高 level 按原始范围归并
所有 Executor IDs 由 processId + level + range hash 生成（确定性，不使用 Math.random）

最后一块不足 200KB：正常作为一块处理。
map/reduce 失败：返回原始尾部（最后 200KB 原始输出）+ 已完成的部分摘要。
```

## 五、Executor 私有 Runtime

Executor map/reduce 不应该与 Manager 的普通 Coder、Reviewer、PTY completion 竞争同一个 `join()` 邮箱。

```fsharp
// parent session 拥有一个私有 Executor runtime
let executorRuntimeFor (context: AgentContext) =
    let runtimes = context.ExecutorRuntimes
    match runtimes.TryGetValue(context.SessionId) with
    | true, rt -> rt
    | false, _ ->
        let rt = createPrivateRuntime context
        runtimes.[context.SessionId] <- rt
        rt

// Executor map/reduce 使用私有 mailbox
let summarizeChunkWithExecutorAgent chunk =
    process {
        let rt = executorRuntimeFor context
        let! child = rt.Fork(NewAgent Executor, chunkPrompt chunk)
        let! completion = rt.JoinAny()  // 不干扰 Manager mailbox
        return extractSummary completion
    }
```

生命周期：parent session `delete`/`abort`/`dispose` 时，清理 executor runtime 及其所有 child。

## 六、超大 Deadline 分段

`3 × estimated_running_secs` 是唯一 deadline。但 model 可能填巨大 estimate（几天/几周），超过 JavaScript timer 上限。

```fsharp
let waitWithDeadline (process: Process) (deadline: TimeSpan) (ct: CancellationToken) =
    task {
        let maxTimer = TimeSpan.FromMilliseconds(float Int32.MaxValue)  // ~24.8 天
        let mutable remaining = deadline
        while remaining > TimeSpan.Zero && not ct.IsCancellationRequested do
            let chunk = min remaining maxTimer
            let! exited = waitForExitOrTimeout process chunk ct
            if exited then return
            remaining <- remaining - chunk
        if remaining <= TimeSpan.Zero then
            killProcessTree process
    }
```

## 七、Large Gate

```fsharp
let globalLargeGate = SemaphoreSlim(1, 1)

let acquireGlobalLargeLease ct = task {
    do! globalLargeGate.WaitAsync(ct)
    return AsyncDisposable.create(fun () ->
        globalLargeGate.Release() |> ignore)
}
```

- 只限制 `Large`。
- Medium 不经过 semaphore。
- 成功、失败、取消、SIGKILL 都由 `use!` finally 释放。
- 不记录队列位置，不做公平性平台。

Executor Agent 输入包含：

- Inspector 当前 A 版工作记录；
- command；
- 当前块编号/总块数；
- 原始输出块。

提示词要求保留错误、路径、行号、统计、测试结论，删除重复日志和原始代码洪流。

---

## 八、PTY 复用 fork 表面

创建：

```json
{
  "agent": "pty",
  "prompt": "bash"
}
```

写：

```json
{
  "agent": "a1b2c3",
  "prompt": "npm test\n"
}
```

读：

```json
{
  "agent": "a1b2c3",
  "prompt": ""
}
```

信号：

```json
{
  "agent": "a1b2c3",
  "prompt": "",
  "signal": "TERM"
}
```

[NORMATIVE]

- 不使用 `[#SIGTERM]` 魔法字符串。
- 每次 read 结果进入 completion mailbox，供 `join()` 返回。
- PTY 自然退出写 `PtyExited` completion。
- `list()` 与 Agent 一起展示。
- 内部仍是独立 `IPtyProcess`，不强行伪装成 LLM Session。

[NORMATIVE] PTY read 契约：

```text
每次 read 返回自上次 read 后的 unread delta，不清空总 buffer。
UTF-8 半字符：保留在内部 buffer，下次 read 时拼接。
buffer 上限：实现定义，但必须 > 64KB。
背压：由 pump 和 buffer 上限自然形成。
process exit 后：可读剩余 buffer，然后返回 PtyExited。
Signal enum 精确集合：TERM, KILL, INT, HUP, QUIT, USR1, USR2。
TERM 后默认等待 5 秒再 KILL（可被 Manager 覆盖）。
Close = stdin EOF（不是 SIGKILL）。
```

---

## 九、测试

1. 子进程极速输出不死锁。
2. stdout/stderr 同时大输出不死锁。
3. 3×时间到达后 kill entire tree。
4. SIGKILL 路径物理返回；模拟不返回时测试明确挂出 bug。
5. Huge estimate 不被 clamp。
6. Medium 100 个并发不经 gate。
7. Large 严格同时一个。
8. 输出恰好阈值不摘要，超过一字节摘要。
9. 200KB UTF-8 边界不切坏字符。
10. 多级摘要保留 exit code、错误路径、测试结论。
11. PTY write/read/signal schema 正确。
12. PTY 与 Agent 同时出现在 list。
13. Executor 私有 runtime 不泄漏给其他 session。
14. parent abort 后 executor runtime 正确清理。


---

# KISS-N06 — A/B 角 Fallback（0.5.0：无限 AABBAABB Agent pair）

Fallback 是每个 Logical Run 上的 modulo-4 cursor + 映射，不是独立状态机，也不是累计失败预算。

完整冻结：`next/Doc/SSOT.md` · `0.5.0.md` §23。

---

## 一、冻结语义 [NORMATIVE]

Fallback 属于 **Logical Run**，不属于 Session 永久状态。A/B 是一对 OpenCode Agent（SelectedAgent / PeerAgent），不是模型槽位。每个 Logical Run 有：

```fsharp
type ModelSide = SideA | SideB

type FallbackCursor =
    { Offset: byte                 // 仅 0|1|2|3
      LastProviderAttempt: int64 option }
```

映射与推进：

```fsharp
let side offset =
    match offset with
    | 0uy | 1uy -> ModelSide.SideA
    | 2uy | 3uy -> ModelSide.SideB
    | _ -> invalidOp "Fallback offset must be in range 0..3"

let advance offset = byte ((int offset + 1) % 4)

let effectiveAgent authority cursor =
    match side cursor.Offset with
    | ModelSide.SideA -> authority.SelectedAgent
    | ModelSide.SideB -> authority.PeerAgent
```

|Offset|Side|EffectiveAgent|
|---:|---|---|
|0|A|SelectedAgent|
|1|A|SelectedAgent|
|2|B|PeerAgent|
|3|B|PeerAgent|
|下一 retry|`(Offset+1) mod 4`|循环回到 0→A|

成功不推进、不重置 cursor。
新 Authority Root 始终新 cursor（`Offset=0`，A=SelectedAgent，B=PeerAgent）。
公开创建必须显式 `fast-*`/`deep-*`；禁止 omit-model / 无前缀 / `build`/`plan`。
发送 Prompt：`Agent=EffectiveAgent`，`Model=None`。
B attempt 不得成为下一真人 root 的默认 Agent，也不得改写 SelectedAgent / LastAuthorityProfile。
**不存在** `FallbackDead` / 因 retry 次数产生的 `LogicalRunDead` /「禁止第五 request」。

---

## 二、结构化实现

```fsharp
let invokeWithCursor (authority: AuthorityExecutionProfile) (cursor: FallbackCursor) request =
    agent {
        let effective = effectiveAgent authority cursor
        // Host 按 opencode.json.agent[effective].model 解析；万象术不传 Model
        match! s.Host.TryInvoke(agent = effective, model = None, request) with
        | InvocationSucceeded output ->
            return output          // 成功：cursor 不变

        | InvocationFailed _ ->
            // 真实推进只发生在 session.status=retry 的 durable path
            return! agent.fail (ProviderRetryPending effective)
    }

// durable retry path（唯一写入口）
let onProviderRetry identity cursor =
    if alreadyRecorded identity then cursor
    else
        append (FallbackCursorAdvanced {| PreviousOffset = cursor.Offset
                                          NextOffset = advance cursor.Offset |})
        { cursor with Offset = advance cursor.Offset
                      LastProviderAttempt = Some identity.ProviderAttempt }
```

控制流完全可见：一个 Offset、一个映射表、一次 modulo-4 推进。没有死亡分支。

---

## 三、为什么这不是 Fallback State Machine

它没有：

- Phase enum；
- RemainingModels；
- 累计 Failures 预算；
- Dead / 第四次判死；
- Retry owner；
- recovery transition table；
- Governor；
- Coordinator；
- Lease。

程序下一步由普通模式匹配决定：

```fsharp
match offset % 4uy with
| 0uy | 1uy -> SelectedAgent
| 2uy | 3uy -> PeerAgent
| _ -> invalidOp "unreachable"
```

这不需要状态图。只需要从一个简单的事实（cursor Offset）推导出当前 EffectiveAgent。

### 为什么不需要 RemainingModels 列表？

因为只有 SelectedAgent 与 PeerAgent 两个 Agent。循环是无限的，不需要"下一个候选"列表，也不需要死亡出口。

### 为什么不需要 FallbackPhase / Failures 计数器？

因为 Fallback 的"下一步"完全由 `Offset mod 4` 决定。`Offset` 是事实（写死的）。不需要中间阶段，也不需要把累计失败数当成预算上限。

### 失败定义

沿用宿主现有"可触发 fallback 的模型调用失败"分类，不新增 Governor。

[NORMATIVE]

- provider/transport invocation failure 且 Host 发出 `session.status=retry`：推进 cursor 一次。
- 用户取消/父取消：不计 retry，直接取消。
- 正常 assistant 输出内容质量差：不计 provider retry；由 Manager/Reviewer 处理。
- Reviewer 未调用 verdict：不是 provider failure；由 Reviewer Guard nudge。
- Executor command 非零退出：不是模型 failure。

### 适用范围

每个 LLM Logical Run 独立拥有 FallbackCursor：

- Orchestrator / Manager / Coder / Inspector / Browser / Meditator / Reviewer（公开 `fast-*`/`deep-*`）；
- Blogger / Executor Agent（内部 `fast-*`/`deep-*`，新 summary Logical Run 固定 fast 起步）。

---

## 四、持久化

只持久化 `FallbackCursor.Offset`（0..3）与 `LastProviderAttempt`。不持久化 Side 枚举副本、Failures 预算或 model ID。

### Fallback identity

每个 retry 的稳定身份：

```text
logicalRunId + AuthorityRootUserMessageId + providerAttempt

AuthorityRootUserMessageId =
  当前 provider attempt 所属的 Host run root user message，
  不包括插件 synthetic continuation/background/reset frame。

Adapter 必须从 typed Host API 获得 AuthorityRootUserMessageId，
禁止通过"最后一条 user message"猜测。
```

`session.status=retry` 事件到达后，提取上述 identity，检查是否已记录：

- 未记录 → append `FallbackCursorAdvanced`，Fold 验证 `NextOffset = (PreviousOffset + 1) mod 4`
- 已记录 → 跳过（去重）；不重复推进、不重复发 continuation

### 唯一写入口

[NORMATIVE] 唯一允许推进 FallbackCursor 的入口：`session.status=retry`。

以下情况**不写** durable cursor advance：

- `observeIdle` 发现空/XML-only assistant
- 重复 idle
- session.error (若后续没有 retry)
- user cancel / parent abort
- 零宽 continuation

空/XML-only terminal 最多触发一次 continuation，不进入 A/B cursor。

[NORMATIVE] 空/XML-only continuation identity：

```text
InteractionRepairIdentity =
  sessionId + rootUserMessageId + terminalAssistantMessageId + repairKind

修复合法的 classifier：
  - 只包含 XML tag 的 assistant（不含任何非 XML 正文）
  - 只包含 reasoning（不含 text）
  - 只包含 tool call（不含 text）
  - 空
  - 空白

同一 identity 最多触发一次 continuation。
任何新 root user message 自动产生新预算。

XML-only = 输出只包含 <tag>...</tag>，没有可见正文。
Tool call XML 计入 XML-only（因为它不是自然语言）。
```

[NORMATIVE] OpenCode 原生 retry 与无限 AABBAABB 轨迹：

```text
retry #1 → Offset=1，下一 provider request 仍 A（SelectedAgent）
retry #2 → Offset=2，下一 provider request B（PeerAgent）
retry #3 → Offset=3，下一 provider request B
retry #4 → Offset=0，下一 provider request A   // 回到 A，不死
retry #5 → Offset=1，A
...
retry #12 → Offset=0，A；必须仍继续产生下一物理 request

同一 Logical Run 内永久循环。providerAttempt 是插件自增编号。

发送时 Agent=EffectiveAgent，Model=None；宿主按 opencode.json 解析模型。
若 Host 自身在某次数停止 retry，不得伪称无限 AABB 已实现；
必须用 ProviderRetryAttempt continuation 延续同一 Logical Run。
```

---

## 五、测试

1. A（SelectedAgent）首次成功；cursor 保持 Offset=0。
2. A 失败一次（retry→Offset=1），A 重试成功。
3. 两次 retry 后切 B（PeerAgent），B 成功；成功不推进 cursor。
4. 同一 Logical Run 内后续 attempt 仍使用当前 Offset 对应 Agent；新 Authority Root 重置 Offset=0。
5. 第三次 retry 后仍 B；第四次 retry 后回到 A（不死）。
6. 至少 12 次 durable retry 后仍 alive，EffectiveAgent 严格 `A A B B A A B B …`。
7. 成功不重置 cursor。
8. 用户取消不推进 cursor。
9. 每个 child Logical Run cursor 独立。
10. Blogger 失败不杀 X；仅停止 B 更新；Blogger 自身也走无限 AABBAABB。
11. 禁止 omit-model / 无前缀 / `build`/`plan`；显式 `fast-*`/`deep-*` 才能建 Authority Root。


---

# KISS-N07 — ReviewGuard 与双 PERFECT

Review 不再依赖 todo，也不再是独立 Coordinator。它是 Manager 的结束守卫和 Reviewer 的 verdict 守卫。

---

## 一、Verdict Tool [NORMATIVE]

Schema：

```json
{
  "verdict": "PERFECT | REVISE"
}
```

工具不接受描述字段。Reviewer 的 assistant A 版工作记录承担描述。

规则：

- `REVISE`：第一次调用立即生效。
- `PERFECT`：第一次返回普通 skeptical tool result。该 tool result 触发的下一次 provider request 若在同一 Authority Root 下以新的 ProviderRunIdentity 再次调用 PERFECT，则立即生效；Reviewer 若先 terminal，Host 改用新的 ReviewConfirmation user message 继续，随后第二次 PERFECT 生效。
- 同一个 ProviderRunIdentity（包括同一 assistant message 内的并行/重复 tool call）中的额外 PERFECT 不写 Journal、不占用第二次确认。
- 第二次 PERFECT 必须来自不同 ProviderRunIdentity，并满足以下因果路径之一：
  1. 与第一次相同的 AuthorityRootUserMessageId，证明模型已消费第一次 skeptical tool result 后重新运行；
  2. 当前 PhysicalUserMessageId 等于 Host 已接受的 ReviewConfirmation continuation。
  仅 ToolCallId 不同不足以证明独立确认。
  两次之间出现普通文本、read、grep 不打断确认序列。
  verdict tool 自身失败不改变当前确认状态。
  REVISE 后立即建立新 barrier。
- 任意 REVISE 清除未完成的 PERFECT 确认。
- Git tree 变化清除未完成或已确认的 PERFECT。
- Post-rebase 审查屏障必须是全新的双 PERFECT（两个新的不同 ToolCallId），不得复用 rebase 前或历史上对同一 tree hash 的确认。

---

## 二、Reviewer 局部程序

```fsharp
let rec awaitVerdict confirmations reviewedTree =
    review {
        match! nextReviewerEvent () with
        | VerdictCalled REVISE ->
            return RevisionRequired

        | VerdictCalled PERFECT when confirmations = 0 ->
            do! returnToolMessage
                    "PERFECT requires confirmation. End this turn and wait for ReviewConfirmation."
            return! awaitVerdict 1 reviewedTree

        | VerdictCalled PERFECT ->
            let! currentTree = git.currentTreeHash ()
            if currentTree = reviewedTree then
                return ConfirmedPerfect reviewedTree
            else
                do! nudgeReviewer
                    "The worktree changed. Re-read the current tree and review again."
                return! awaitVerdict 0 currentTree

        | AssistantTerminalWithoutVerdict output ->
            do! nudgeReviewer
                    "You returned without a valid verdict. Continue the review and call verdict."
            return! awaitVerdict confirmations reviewedTree

        | ProviderFailure error ->
            return! review.fail (ReviewerFailed error)
    }
```

这里的 `confirmations` 是递归参数，不是 `ReviewPhase`。

“Reviewer 不返回 Guard”定义为：Reviewer assistant 已 terminal，但没有产生可生效 verdict；Guard 立即向同一 reviewer fire-and-forget prompt，继续同一 Session。真正 provider 不返回由该 Session 的 A/B Fallback 处理，不另加 wall-clock timeout。

---

## 三、Review Witness

```fsharp
type ReviewWitness =
    | NoConfirmedReview
    | ConfirmedPerfect of
        {| ManagerJobId: string
           ReviewerSessionId: string
           ReviewBarrierId: string
           GitTreeHash: string
           FirstProviderRunId: string
           FirstToolCallId: string
           SecondProviderRunId: string
           SecondToolCallId: string |}
    | RevisionRequired of report: ARecord
```

这是当前代码树的审查事实，不是流程阶段。

- Reviewer REVISE → `RevisionRequired report`。
- 双 PERFECT → `ConfirmedPerfect reviewedTree`。
- 文件改动/tree hash 变化 → 投影为 `NoConfirmedReview`。

可以运行期保存；Orchestrator 发布时重新审查，所以无需复杂跨进程恢复。

[NORMATIVE] Manager Guard 只接受 durable projection 中完全匹配当前 Job 和 HEAD 的 witness。
Witness 必须满足：
- 来自该 Manager 创建的 Reviewer
- Reviewer 的 worktree 与 Manager worktree 相同
- barrier 是当前 ManagerJob
- tree 是当前 HEAD
- review 发生在最新修改之后
- reviewer role 未被伪造
- verdict tool call 已真正成功执行

---

## 四、Manager Guard

Manager 每次 assistant terminal 后：

```fsharp
let rec runGuardedManager manager =
    agent {
        let! output = manager.RunNextTurn()
        let! currentTree = git.currentTreeHash ()

        match manager.ReviewWitness with
        | ConfirmedPerfect tree when tree = currentTree ->
            return output

        | _ ->
            do! manager.PostPromptFireAndForget(
                "You may not finish yet. Fork or continue a reviewer, " +
                "resolve every REVISE, and obtain two confirmed PERFECT verdicts " +
                "for the current worktree."
            )
            return! runGuardedManager manager
    }
```

[NORMATIVE]

- Guard 不替 Manager 选择哪个 coder/reviewer。
- Guard 不读取 todo。
- Guard 不扫描 next action candidates。
- Guard 只判断“当前 tree 是否有确认 PERFECT”。
- 用户取消可终止循环。
- 不设置隐蔽最大轮数；审查应持续到满足或用户取消。

---

## 五、REVISE 路径

```text
Reviewer verdict(REVISE)
→ verdict tool 立即生效
→ join 返回 reviewer ID + A 版意见
→ Manager fork(existing coder, revision prompt) 或 fork(new coder)
→ coder 修改
→ tree hash 改变，旧 review witness 自动失效
→ Manager 再 fork/continue reviewer
```

工具 verdict 不带描述，避免 verdict 与报告双份字段不一致。

---

## 六、PERFECT 路径

```text
Reviewer verdict(PERFECT)
→ tool result: confirm again
→ Reviewer verdict(PERFECT)
→ bind current tree hash
→ reviewer A 版 terminal
→ join 返回 confirmed PERFECT
→ ManagerGuard 允许 finish
```

若第一次 PERFECT 后 reviewer 直接 terminal，Reviewer Guard nudge 同一 reviewer，请求第二次确认。

---

## 七、Orchestrator 复审

Manager worktree 在原基线确认 PERFECT 后，只得到"可进入发布"的资格。

Rebase 到最新目标分支后 tree/commit 变化：

```text
old witness invalid
→ same manager/reviewer context receives rebase result
→ reviewer 发出两个新的、不同 ToolCallId 的 PERFECT
→ ff
```

即使 rebase 后 tree hash 与 pre-rebase 相同（rebase 只改变 ancestry 不改变 tree），也必须发出两个新 ToolCallId 的 PERFECT，不得复用旧确认。

---

## 八、测试

1. REVISE 一次立即生效。
2. PERFECT 一次不生效。
3. 连续两次 PERFECT 同 tree 生效。
4. PERFECT、REVISE、PERFECT 不生效，需再次 PERFECT。
5. 第一次 PERFECT 后 tree 变化，计数归零。
6. terminal 无 verdict 被 nudge。
7. terminal 仅一次 PERFECT 被 nudge。
8. Manager 无 witness 不可 finish。
9. confirmed tree 后修改文件不可 finish。
10. rebase 后必须重新审查（两个新 ToolCallId）。
11. provider failure 走 Session Fallback，不走 reviewer wall-clock timer。
12. post-rebase tree hash 与 pre-rebase 相同仍视为未审查，需要新 PERFECT。


---

# KISS-N08 — Orchestrator / Worktree / Rebase / FF

Orchestrator 是更高一层的 Agent Program，不是万象阵 DAG Scheduler。

---

## 一、工具与职责

Orchestrator 只有：

```text
fork(manager, prompt)
join()
```

它不能：

- 读写仓库；
- 自己解决冲突；
- 操作 Git；
- 调普通子角色；
- 维护 DAG/wave/owner/lease。

每个 ManagerJob 的运行时动词内部负责 worktree 与发布。

---

## 二、用户输入 Clean Gate [NORMATIVE]

每次用户消息送入 Orchestrator 前：

```fsharp
let ensureClean target =
    orchestrator {
        let! dirty = git.statusPorcelain target
        match dirty with
        | [] -> return ()
        | paths -> return! orchestrator.fail (DirtyWorkspace paths)
    }
```

工作区 dirty 直接拒绝本次消息，不自动 stash、不自动 commit、不猜用户意图。

插件临时文件、worktree、spool 不得放进目标工作树制造 dirty；放在 Git common dir、仓库 sibling 或系统 cache。

[NORMATIVE] Clean Gate 竞态：

```text
接受用户消息时同时读取 target HEAD + porcelain。
创建 ManagerJob 前重新验证 HEAD 与 clean 状态。
不一致则拒绝本次 fork，不自动重试。
untracked、ignored 不算 dirty（仅 tracked file changes 和 staged changes）。
submodule dirty 算 dirty。
用户消息处理期间再次变 dirty：不影响已在执行的 ManagerJob，
但下一条用户消息必须重新验证。
```

---

## 三、fork ManagerJob

```fsharp
let forkManager prompt =
    orchestrator {
        // fork 只创建并转交资源所有权；不能在返回 handle 前 use! 释放 worktree。
        let! worktree = git.createIsolatedWorktree targetBranch
        let! manager = host.createManagerSession(worktree.Path, parentB())
        attachManagerGuard manager
        let handle = registerManagerJob worktree manager
        startOwnedManagerJob handle prompt
        return Forked handle.Id
    }
```

Manager 自动进入 ReviewGuard，不依赖 `/loop` 文本命令实现状态切换；若宿主必须通过 slash 激活，可由 Adapter 发送一次 `/loop`，领域层仍视为 `attachManagerGuard` 动词。`registerManagerJob` 把 worktree 所有权转交给后台 ManagerJob；只有该 Job 的 `use!` 作用域结束时才能清理。

Worktree 生命周期覆盖：

```text
Manager work
→ initial confirmed review
→ commit candidate
→ wait publish gate
→ rebase
→ conflict resolution if needed
→ post-rebase confirmed review
→ ff
→ cleanup
```

---

## 四、ManagerJob 结构化程序

```fsharp
let runManagerJob job =
    orchestrator {
        use! worktree = job.Worktree
        let! initial = runGuardedManager job.Manager
        let! candidate = git.commitCurrentTree worktree initial
        return! publishCandidate job candidate
    }
```

不写 `JobStage=Working/Reviewed/Rebasing/Merging`。资源和调用栈已经表达位置。

---

## 五、串行发布门与跨进程锁

工作并行，目标 Git ref 发布串行。

```fsharp
let publishCandidate job candidate =
    orchestrator {
        use! gate = acquireIntegrationGate job.TargetRef
        do! ensureClean job.TargetWorkspace
        return! rebaseReviewAndFastForward job candidate
    }
```

`IntegrationGate` 使用 proper-lockfile 锁定目标 ref，不限于对象内 Task chain：

```fsharp
let acquireIntegrationGate (targetRef: string) =
    orchestrator {
        let lockPath = gitCommonDir + "/publish." + targetRef + ".lock"
        let! lock = properLockfile.acquire lockPath
        return Resource.create lock (fun () -> lock.release())
    }
```

[NORMATIVE]

- lock path 位于 Git common directory，不在工作树。
- 同 repo + 同 branch 得到同一 lock。
- lock 释放发生在 ff-only 与 Published fact 写入之后。
- 崩溃后 stale lock 由 lockfile 自动回收。
- 两个 Runtime 竞争同 repo+同 branch 时，一个等待，一个执行。

## 六、阶段事实与崩溃恢复

不允许恢复"暂停的协程"。持久化业务屏障事实，重启后根据事实 + Git authority 决定下一步。

### 持久化事实

```fsharp
type OrchestratorFact =
    | ManagerJobLinked of {| ManagerSessionId: SessionId; WorktreePath: string; TargetRef: string |}
    | CandidateCreated of {| ManagerSessionId: SessionId; Commit: GitCommit |}
    | PreRebaseReviewConfirmed of {| ManagerSessionId: SessionId; Tree: GitTreeHash |}
    | Rebased of {| ManagerSessionId: SessionId; Candidate: GitCommit; TargetHead: GitCommit |}
    | ConflictDetected of {| ManagerSessionId: SessionId; Diagnostics: string |}
    | PostRebaseReviewConfirmed of {| ManagerSessionId: SessionId; Tree: GitTreeHash |}
    | PublishClaimed of {| ManagerSessionId: SessionId; TargetBranch: string; ExpectedHead: GitCommit |}
    | Published of {| ManagerSessionId: SessionId; Candidate: GitCommit; ResultingTargetHead: GitCommit |}
```

### 恢复逻辑

启动时 Fold NDJSON 获取每个活跃 ManagerJob 的最后事实，然后根据 Git authority 决定：

```text
最后事实
├── Published → 已发布；清理 worktree，从活跃 Map 删除
├── PublishClaimed → 查询 Git target 是否已包含 candidate
│   ├── 是 → 补写 Published（幂等）
│   └── 否 → 重试 publish
├── PostRebaseReviewConfirmed → 继续 publish
├── ConflictDetected → 恢复同一 Manager 解决
├── Rebased → 重新 review
├── PreRebaseReviewConfirmed → rebase
├── CandidateCreated → 等待 review 或走 publish
├── ManagerJobLinked → 刚从 worktree 启动 Manager
└── 无事实 → Job 失败
```

关键设计：

- 不同时恢复"调用栈"或"暂停的异步操作"。
- Journal 只记录已经完成的事实。
- Git 是权威来源：candidate 是否已进入 target 由 `git merge-base --is-ancestor candidate target` 判断。
- 发布后从活跃 Projection 删除，不会随历史积累。

[NORMATIVE] 崩溃恢复判定顺序（Git 优先于 Journal）：

```text
target contains candidate commit → 已发布
worktree HEAD rebased on target + 无有效 witness → 重新 review
存在 valid witness + target 未包含 candidate → 尝试 ff
branch/worktree 不存在 → 从 Git 事实判定 success 或 failure
Journal 只记录不可从 Git 推导的 reviewer witness 与 association
```

---

## 七、Rebase / 冲突 / 复审 / FF

```fsharp
let rec rebaseReviewAndFastForward job candidate =
    orchestrator {
        let! targetHead = git.readHead job.TargetRef

        match! git.rebase candidate targetHead with
        | Rebased rebasedCandidate ->
            do! job.Manager.InvalidateReviewWitness()
            do! job.Manager.PostPromptFireAndForget(
                "The candidate was rebased onto the latest target. " +
                "Re-run tests and obtain two PERFECT verdicts for the rebased tree."
            )
            let! _ = runGuardedManager job.Manager
            let! reviewed = git.commitCurrentTree job.Worktree "post-rebase fixes"
            do! git.fastForward job.TargetRef reviewed
            return Published reviewed

        | Conflict diagnostics ->
            do! job.Manager.PostPromptFireAndForget(
                "Rebase conflicts occurred. Resolve them in the current worktree, " +
                "run verification, then complete the full review loop.\n" + diagnostics
            )
            let! _ = runGuardedManager job.Manager
            let! resolved = git.commitCurrentTree job.Worktree "resolve rebase conflicts"
            return! rebaseReviewAndFastForward job resolved
    }
```

[NORMATIVE]

- 冲突返回同一个 Manager，不启动第二套 conflict resolver。
- 每次重试都读取最新 target head。
- Rebase 后旧 PERFECT 无效。
- FF 前再次 clean check。
- FF 失败作为真实 PublishFailed 返回，不伪装成功。

[NORMATIVE] 冲突返回同一 Manager 的 Run 归属：

```text
Manager 仍 busy 时：通过 nudge 发送冲突诊断（不打断当前 run）。
Manager 已 terminal 时：作为 continuation 发送（不是新 run）。
原 Manager completion 未消费：先消费 completion，再 nudge。
冲突解决后：创建新 candidate（新 commit）。
旧 pre-rebase witness：ManagerJob 级别失效。
Manager 四次死亡后 worktree 保留由 Orchestrator 决定：可放弃、可指派新 Manager。
```

---

## 八、target ref 权威与安全

- 目标 branch 由 `git symbolic-ref` 在 fork 时冻结，不以运行时状态猜测。
- `GetTargetHead` 读取失败 → fail closed，不得 fallback 到 `HEAD` 或 `"main"`。
- Reconcile 写入的 Published commit 是 candidate commit hash，不是 当前 target HEAD。
- `git merge-base --is-ancestor candidate target` 判断 candidate 是否已被包含。
- 不使用字符串前缀比较 commit hash。
- `git merge --ff-only` 检查当前 branch == frozen target branch、当前 target head == expected head。


---

# KISS-N09 — OpenCode Host Adapter、信号与投影管线

Host Adapter 只做协议翻译，不拥有业务流程。

## 零、核心原则

```text
碎片事件（message.updated / part.delta / session.updated）
    ↓ 最早边界丢弃
粗粒度信号（session.status idle/retry, session.deleted）
    ↓ single-flight
SDK API 读取完整消息
    ↓
ReconciledTurn（完整 typed parts / outcome / role / model）
    ↓
纯策略（completion / ReviewGuard / continuation / abort）
```

[NORMATIVE]

- 生产功能**不从碎片事件拼装**业务事实。
- `idle` 只触发一次 connected reconcile loop，不直接代表 terminal 完成。
- `retry` 是唯一写 durable fallback 的入口。
- Unknown（API 尚未显示当前 Run 的 assistant）不产生副作用，不判为 Empty/Failed。
- 同一事件重复到达不重复消费。
- 关联 child terminal 只由 idle 唤醒后 reconcile 确认，不监听 `current_parts`/`message_finalized`/part 增量。

[FORBIDDEN]

- 在 F# 业务层处理 `message.updated` / `message.part.updated` / `part.delta`。
- 在 F# 业务层处理 `session.updated` / `session.diff`。
- 根据 idle 事件内容直接写 `FallbackFailureRecorded`。
- 依赖事件先后顺序或 payload 结构推导业务因果。

---

## 一、关闭官方 Compaction [NORMATIVE]

配置层明确关闭 OpenCode 自动 compaction。若宿主仍可能在 overflow 时自动触发，Adapter 必须拦截或把阈值设置为不可达，保证唯一上下文替换来自本项目 Projection。

不调用官方 compaction agent，不监听 compaction event 驱动业务。

---

## 二、投影管线

```text
Host raw messages
→ Decode typed DTO
→ CanonicalProjection (pure JSON)
→ Companion OfferDelta (side effect, non-blocking)
→ PrefixReplacement (pure transform using current memory)
→ Role Tool Filter (pure)
→ Model messages
```

纯函数：

```fsharp
val decode: obj -> Result<HostMessage list, DecodeError>
val canonicalize: HostMessage list -> JsonValue
val replacePrefix: BlogBase -> BlogText -> JsonValue -> JsonValue
val filterTools: AgentRole -> ToolDefinition list -> ToolDefinition list
val encodeModelMessages: JsonValue -> ModelMessage list
```

副作用只有：

- 尝试启动 Blogger Task；
- 读取当前 B 缓存；
- 读取静态 role definition。

[FORBIDDEN]

- 投影 hook 内等待 Blogger。
- 投影 hook 内 fork 普通 agent。
- 投影 hook 写 Journal。
- 动态 Transform Registry。
- Transform 内 read/git/process。

---

## 三、Session 关联

```fsharp
type SessionAssociation =
    { MainSessionId: string
      BloggerSessionId: string option
      ParentSessionId: string option
      Role: AgentRole }
```

优先存宿主 metadata。只需要 association，不需要 ChildCreated/Owner/Lease 事件流。

启动时：

- 已有 BloggerSessionId：尝试恢复同一个 Blogger。Session 仍存在 → 继续使用，只发送 delta。Session 不存在/被删 → 新建 Y，发送 full reset frame。
- 无 BloggerSessionId 且角色需要伴随：创建 Y 并写 metadata。
- 不每次重启都重建 Blogger（避免无谓冷缓存）。

---

## 四、HostSignal Adapter

唯一允许进入业务层的事件类型：`session.status`（type=idle 或 type=retry）、`session.deleted`。

同一 Session 的 event 来源二选一（插件 `event` Hook 或 global SSE），不双投递。

```fsharp
type HostSignal =
    | SessionIdle of sessionId: SessionId
    | ProviderRetry of
        {| SessionId: SessionId
           Attempt: int
           UserMessageId: MessageId option |}
    | SessionDeleted of sessionId: SessionId
```

Adapter 规则：

- 不是 `session.status`/`session.deleted` → 立即 return（不 resolve properties，不复制 payload，不写日志）。
- `session.status=idle` → `MarkDirty(sessionId)`（触发 single-flight reconcile loop）。
- `session.status=retry` → 提取 sessionId/attempt/messageId（若有）→ `HandleRetrySignal`。
- `session.deleted` → `CleanupOwnedSession(sessionId)`。

```fsharp
let sessionReconcileLoop (sessionId: SessionId) =
    task {
        if isReconciling then return()
        isReconciling <- true
        try
            while dirty do
                dirty <- false
                let! messages = client.session.messages(sessionId)
                let! reconciled = reconcileTurn messages sessionId
                match reconciled with
                | Some turn -> do! applyTurnCompletion turn
                | None -> ()   // Unknown → 不产生副作用
        finally
            isReconciling <- false
            if dirty then
                startReconcileLoop sessionId  // 重跑
    }
```

- Single-flight：同一 session 同时最多一次 reconcile 请求。
- Dirty 标记：idle 信号到达时设 dirty=true，若已 running 则不会额外启动。

[NORMATIVE] Unknown 等待协议：

```text
一次 idle 建立 Dirty latch。
Reconciler 在同一 single-flight 中进行有限次 event-loop yield 后重读；
只要 API snapshot version 有因果进展即可继续；
达到 3 次仍 Unknown，则保持 Dirty，
下一个任何允许的粗粒度 signal 再重试。

不依赖 wall-clock timeout 解决 Unknown 问题。
若 Host API 不保证 idle 后立即可见，
则"等下一次信号"策略不满足 liveness，需要 Host 补 idle-visible 契约。
```

## 五、Terminal 与 A 版

ReconciledTurn 提供完整 assistant：

```fsharp
type ReconciledTurn =
    { SessionId: SessionId
      UserMessageId: MessageId
      AssistantMessageId: MessageId
      AgentRole: AgentRole option
      Directory: string
      Parts: ProviderVisiblePart array
      Outcome: TurnOutcome }  // Completed / Failed / Aborted
```

A 版是 **session-wide assistant 输出累积**（正式正文 + reasoning/thinking；不含 tool raw stream）：

```fsharp
type ARecord =
    { Text: string          // 整个 Session 正式正文与 reasoning/thinking 的拼接，非单轮
      Model: string option
      Error: string option }
```

[NORMATIVE]
- `join()` 的 `finalText` = 子 Session 当前完整 A（session-wide，含思考过程）。
- `join()` 的 `workRecord` = 子 Session 当前完整 B（`LatestB`，session-wide companion work log）。
- terminal 完成时必须把本轮正式正文与 reasoning/thinking 并入 Session 的 A 累积，再对外暴露完整 A。
- 单轮既无正文也无 reasoning 不得把已有 A 抹成空；空轮不写入 A。
- 空/XML-only / reasoning-only 的 **interaction repair 分类**仍只看正式 text part（不含 reasoning）；repair 预算与 A 累积字段分离。

---

## 六、Fire-and-forget Prompt Adapter

```fsharp
val postPromptFireAndForget:
    sessionId: string ->
    prompt: string ->
    unit
```

实现：

1. 发起宿主 prompt API Task。
2. 不 await assistant completion。
3. Task 异常写结构化诊断。
4. 不进入自建队列。
5. 不触发 fallback；真正模型调用由目标 Session 自己的 Fallback Wrapper 处理。

若宿主 API 在"请求未接受"阶段同步失败，可把工具 ack 标为 failed；不得为此发明 AcceptanceState 平台。

---

## 七、工具注册

静态函数：

```fsharp
let toolsFor role =
    RoleDefinitions.get role
    |> fun d -> d.Tools
    |> List.map ToolDefinitions.get
```

特殊 schema：

- `fork`：agent/prompt/signal。
- `join`：空对象。
- `list`：optional kind。
- `verdict`：enum PERFECT/REVISE。
- `executor`：command + 3 estimate 字段。
- `inspector`：prompts list。

禁止通过 before hook 给普通工具偷偷塞控制字段。

---

## 八、Host 生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ runtime dispose: cancel owned Tasks, kill PTYs/processes, dispose sessions as policy
```

Hook 回调保持短小：decode、调用纯 transform、post/return。长流程在 Flow 中运行。

---

## 九、日志

日志只记录诊断：

```text
session_id
role
handle_id
operation = fork/join/nudge/project/blog/process/review/publish
result
error
bytes/duration/tree hash
```

不写：

```text
stage
phase
owner
lease
generation
next_action
```

日志不是恢复协议。

---

## 十、测试

1. 官方 compaction 确实不会运行。
2. 投影顺序固定。
3. Blogger Task 不阻塞 projection。
4. role tool filter 静态正确。
5. session association 可恢复同 Blogger（不复用时不发 delta 给空白 Y）。
6. terminal 重复事件不会重复拼 A（Reconcile 是 idempotent）。
7. reasoning 进入 A 与 canonical delta；不进入 B。
8. fire-and-forget 不建插件队列。
9. hook 无长等待。
10. 日志不出现旧控制字段。
11. 断开 `message.updated`/`part.updated` 事件后，所有产品 E2E 正常工作。
12. Good message → idle → duplicate idle → no duplicate completion。
13. Unknown（API 尚未显示 assistant）→ 不产生任何副作用。
14. Abort terminal → fallback 不增加失败计数。
15. Single-flight reconcile：连发多次 idle 只触发一次 API 请求。


---

# KISS-N10 — 保姆式实施、测试、迁移与删除

## 零、Rescue Phase（当前残局）

在进入正式 Phase 前，必须先完成残局清理。

### 目标

修复两条根因错误：

1. `HostEventRouter` 没有"一条 assistant terminal 只能消费一次"的概念，把"没有观察到 parts"解释为"空输出"。
2. `runtime-aware child linkage` 把 Journal envelope 的 `RuntimeId` 错当成 child 生命周期所有权。

### 动作

```text
建立 AssistantTurnTracker：terminal 只取出一次，重复 idle 不重复消费
删除 LinkedRuntimeIds / childRuntimes / RuntimeId.create "" 哨兵
恢复重启后复用同一个 child linkage（SunAnalogy：runtime 重启 ≠ session 死亡）
Fallback 只从 session.status=retry 写入，不从 observeIdle 写入
零宽 continuation 不再使用同一套 A/B 失败计数
```

### 出口

```text
Dead_decision_survives_journal_rebuild    绿
Good message + idle + idle → 不产生第二次 completion
session.status=retry → 唯一 FallbackFailureRecorded 入口
```

---

本卷给出从当前 next/重构转向新 Agent DSL 的唯一实施顺序。

---

## 一、工程原则

1. OpenCode only。
2. `next/` 不 import 旧生产。
3. 不兼容旧 `.wanxiangshu.ndjson` 控制格式。
4. 先写能编译的 DSL 骨架，再接 Host。
5. 每个 Phase 只引入一个真实复杂点。
6. 新旧双轨期允许代码暂时增加；每个新行为验收后立即删对应旧路径。

建议目录：

```text
next/
  Kernel/
    Flow.fs
    AsyncResource.fs
    Parallel.fs
  Domain/
    Roles.fs
    ARecord.fs
    BRecord.fs
    Fallback.fs
    ReviewGuard.fs
  Companion/
    CanonicalProjection.fs
    JsonDelta.fs
    BlogDocument.fs
    CompanionProgram.fs
    PrefixProjection.fs
  Forking/
    Handle.fs
    CompletionMailbox.fs
    ForkProgram.fs
    JoinProgram.fs
    ListProjection.fs
  Process/
    ExecutorRequest.fs
    ProcessResource.fs
    OutputPump.fs
    OutputSummary.fs
    PtyResource.fs
  Orchestrator/
    WorktreeResource.fs
    ManagerJob.fs
    PublishProgram.fs
  OpenCode/
    Codec.fs
    ProjectionHook.fs
    SessionAssociation.fs
    PromptAdapter.fs
    ToolDefinitions.fs
    Plugin.fs

tests-next/
  GuideContract/
  Companion/
  ForkJoin/
  Process/
  Fallback/
  ReviewGuard/
  Orchestrator/
  HostContract/
```

---

## 二、Phase 0 — GuideContract 真编译

### 目标

把文档中的核心代码形状编译起来，不接 OpenCode。

### 必做

- `Flow`、Builder、`Using`、`parallel`。
- `agent/companion/process/review/orchestrator` 类型别名。
- fake Script 与 fake resource。
- 尾递归 Fallback。
- 双 PERFECT 递归。

### 出口

- 示例主程序能编译。
- 无 Flow AST。
- 无 Stage/Phase/Lease/Owner/Generation 类型。
- `use!` 在成功/失败/取消都 await dispose。

---

## 三、Phase 1 — Canonical Projection 与 Blogger

### 步骤

1. 从 Host fixture 解码 typed message。
2. 纯函数 canonical JSON。
3. 稳定 JSON delta。
4. fake Blogger Task。
5. busy skip；成功后推进 BlogBase。
6. B 只收 assistant output。
7. PrefixReplacementEnabled。
8. X prefix projection。
9. Y self-rebase。
10. 接真实廉价模型。

### 出口

- 三次 busy skip 后只补一次完整 delta。
- X 无等待投影。
- Y rebase 后旧 B 不再属于 B 输出。
- 官方 compaction 尚未接，但纯投影测试闭合。

---

## 四、Phase 2 — ForkRuntime

### 步骤

1. HandleId 生成与碰撞检查。
2. ConcurrentDictionary handles。
3. Channel completion mailbox。
4. fake Host child。
5. listener-before-send。
6. fork new role。
7. nudge existing ID fire-and-forget。
8. join any。
9. list projection。
10. parent cancel。

### 出口

- 极快 completion 不丢。
- 无自建 prompt queue。
- join 无 active 返回 NothingToJoin。
- 不出现 SubsessionActor。

---

## 五、Phase 3 — 静态角色与工具

### 步骤

1. RoleDefinitions 静态表。
2. Manager only fork/join/list。
3. Orchestrator only fork/join。
4. Coder 普通文件工具与不透明 Inspector 调查；schema 不暴露 Executor 或终端能力。
5. Inspector `read` / `glob` / `grep` / Executor Tool。
6. Browser/Meditator/Reviewer 权限。
7. Blogger/Executor Agent no tools。

### 出口

- capability snapshot 测试逐角色比对。
- fuzzy 工具完全不注册。
- Manager 无普通工具。

---

## 六、Phase 4 — Process 与 Executor

### 步骤

1. Spawn 前安装 pumps。
2. 3× estimate 唯一 timer。
3. timeout kill entire tree。
4. kill 后无第二 timeout。
5. medium 无 gate。
6. large global semaphore 1。
7. byte counting。
8. spool 文件。
9. 200KB chunk。
10. Executor Agent map/reduce 摘要。

### 出口

- 所有死锁/大输出/kill 测试通过。
- huge estimate 未被 clamp。
- 无 Python/JS 特殊 execute 工具。

---

## 七、Phase 5 — PTY

### 步骤

1. `fork(agent=pty,prompt=...)` create。
2. ID + prompt write。
3. ID + empty prompt read。
4. ID + signal enum。
5. read/exit completion。
6. list 合并展示。
7. parent cancel kill。

### 出口

- 无魔法 signal 字符串。
- PTY 内部未伪装 Agent Session。

---

## 八、Phase 6 — Session Fallback

### 步骤

1. FallbackCursor.Offset（0..3）+ SelectedAgent/PeerAgent。
2. `session.status=retry` 推进 `(offset+1) mod 4`。
3. A/A/B/B 永久循环；EffectiveAgent 映射。
4. 成功不推进、不重置。
5. 新 Authority Root → Offset=0。
6. 第四次及之后的 retry 继续循环（回到 A），不杀 Logical Run（见 KISS-N06 / `next/Doc/SSOT.md`）。
7. cancellation excluded。
8. Role metadata/config 接线改为显式 `fast-*`/`deep-*`（Model=None）。

### 出口

- 表驱动的无限 AABBAABB 轨迹（含 12-round）全部通过。
- 仓库无 FallbackPhase/RemainingModels/Governor/因 retry 次数判死。

---

## 九、Phase 7 — ReviewGuard

### 步骤

1. verdict schema。
2. REVISE immediate。
3. first PERFECT tool response。
4. second PERFECT bind tree hash。
5. terminal no verdict nudge。
6. manager terminal guard。
7. tree change invalidation。
8. coder revision loop E2E。

### 出口

- 一次 PERFECT 绝不能放行。
- Reviewer 忘 verdict 自动继续同一 session。
- Manager 无 review witness 绝不能结束。
- 不读取 todo。

---

## 十、Phase 8 — OpenCode Adapter（信号接入）

### 步骤

1. 关闭官方 compaction。
2. 建立 HostSignal adapter：只保留 `session.status`（idle/retry）和 `session.deleted` 三信号。
3. 建立 SessionReconciler：single-flight，从 SDK API 读取完整消息。
4. 删除所有 `message.updated`/`part.updated`/`part.delta` 业务依赖。
5. 接 projection hook（`experimental.chat.messages.transform`）。
6. association metadata：重启后复用同一 Blogger（不发 delta 给空白 Y）。
7. A extraction 来自 reconcile 后的 ReconciledTurn。
8. re-fire-and-forget prompt。
9. static tools。
10. plugin disposal。

### 出口

- 断开 `message.updated`/`part.updated` 事件后所有产品 E2E 正常工作
- child completion 通过 reconcile 确认，不通过 SSE listener
- 日志不出现旧控制字段
- Browser/Meditator/Reviewer 权限
- Blogger/Executor Agent no tools

---

## 十一、Phase 9 — Orchestrator

### 步骤

1. clean gate。
2. single Manager worktree。
3. auto ManagerGuard。
4. initial review。
5. candidate commit。
6. integration semaphore。
7. rebase。
8. post-rebase review。
9. ff。
10. two managers parallel work / serial publish。
11. conflict return same manager。

### 出口

- 只有 ff 成功才 join published。
- 第二任务基于最新 target rebase。
- rebase 后双 PERFECT。
- 无 DAG/Scheduler/HTTP 控制面。

---

## 十二、Phase 10 — 删除旧实现

按功能一刀切删除：

```text
todowrite SSOT / task 强制报告
select_methodology
通用 Nudge 与 idle proposals
SubsessionActor 与同步 subagent 工具
submit_review / return_reviewer 旧协议
Fallback 状态机/扫描链
ContextBudget/官方 compaction 协调
fuzzy_find/fuzzy_grep/fuzzy_continue
万象阵 DAG/Scheduler/Coordinator/HTTP 控制面
旧 PTY 多工具表面
旧 executor 多层 timeout
Stage/Phase/Lease/Owner/Generation 字段
```

README 改写为新产品语义；旧文档移入 `docs-legacy/` 或直接删除，不让两套规范同时有效。

---

## 十三、测试金字塔

### 13.1 纯函数

- Canonical JSON。
- JsonDelta。
- Prefix replacement。
- Role capability。
- Fallback transition function。
- Review witness/tree invalidation。

### 13.2 资源契约

- Flow Using。
- Completion Channel。
- Process pumps/kill。
- Large semaphore。
- Worktree cleanup。

### 13.3 Fake Host 轨迹

- Blogger busy skip。
- nudge running child。
- terminal without verdict。
- four failures。
- manager guard。

### 13.4 OpenCode E2E

- 长 session 真正不触发官方 compaction。
- B 替换前缀后继续正确工作。
- Manager 并行 fork/join。
- Coder 的不透明 Inspector 调查与 verification handoff。
- 大命令摘要。
- PTY。
- reviewer REVISE/双 PERFECT。
- orchestrator 两 worktree 串行发布。

### 13.5 测试执行器（tests-next runner）

[NORMATIVE] `tests-next/runner.js` 是纯 Fable 测试的执行器，设计原则：

```text
import + await 直接运行
`__resetAssertionTimeout()` 心跳重置本地定时器
心跳超时 → test 标记为失败，abandon 底层 async（不主动 kill）
串行执行
无 IPC / worker 协议
无 spawn ledger 参与
```

**旧模型 vs 新模型**：

| | 旧 | 新 |
|---|---|---|
| 执行 | `fork()` worker 进程 | 同进程 `import` + `await` |
| 心跳机制 | IPC heartbeat → 父进程 | `globalThis.__resetAssertionTimeout` → 在 `Promise.race` 中重置本地 timer |
| 超时处理 | `terminateTree()` → SIGTERM→SIGKILL | reject Promise → 忘记测试（不再等待或杀死） |
| 进程跟踪 | `spawn-ledger` + PID 重用保护 | 无 |
| 清理 | 确保子进程组全部终止 | 不需要——Node.js 进程退出自动清理一切 |

**为什么不再需要进程隔离**：

- 超时后不需要主动终止异步操作——Node.js 进程退出会清理一切。
- 进程隔离（`fork()` + `terminateTree`）引入心跳协议、进程跟踪、PID 重用防范等复杂度，但解决的是根本不需要解决的问题。
- 同进程串行执行 + Promise.race 已足够：测试点失败就是失败，不关心底层 async 是否还在跑。
- 心跳保留但语义简化：不是用来防止被杀，而是用来决定测试是否失败。

**设计**：

```text
discoverTestExports(file):
    import(file) → 枚举 export 中的 async function

runTest(file, name, timeoutMs=1000):
    import(file) → mod[name]()
    Heartbeat timer fires → reject failPromise
    Promise.race([testPromise, failPromise])
    heartbeat 胜出 → 抛 TIMEOUT 错误，test 失败
    test 胜出 → clear heartbeat timer
    不调 terminateTree，不 send SIGKILL，不清理

__resetAssertionTimeout():
    全局函数，测试调用以重置 heartbeat timer
```

[NORMATIVE]

- 每个 test 只 `import` 一次（ESM module cache 确保后续同文件 import 为同一引用）。
- Test function 必须返回 Promise（Fable 编译的 async test 天然满足）。
- 1s 是硬上限，不可配置；`TEST_SILENCE_MS`/`TEST_ABSOLUTE_MS` 环境变量不再存在。
- `__resetAssertionTimeout()` 不再通过 IPC 发送 heartbeat 到父进程，改为重置同进程的本地 timer。
- 不再 fork worker 进程，不再 import `process-lifecycle.js`、`spawn-ledger.js`。
- `worker.js` 和 `fixtures/hanging-test.js` 不再需要（保留简化的 fixture 供测试框架验证 timeout 行为）。

---

## 十四、代码门禁

CI 搜索并拒绝新代码中的：

```text
Stage
Phase
Lease
Owner
Generation
Coordinator
Governor
Registry  // 仅允许静态 ToolDefinitions/RoleDefinitions 与资源字典的具体命名
NextAction
NudgeState
FallbackState
ReviewStateMachine
SubsessionActor
```

例外必须在 allowlist 中解释为真实外部概念。

主程序审查门禁：

- 主要函数缩进不超过 3 层。
- 一次流程可在一个文件连续阅读。
- `match/while/rec/use!` 直接表现业务顺序。
- Host API、Process API、Git API 藏在动词内部。
- 没有“调用后 Refresh”语法；动词返回时值已可用。

---

## 十五、稳定性门禁

```text
P0 全套动态事件 stagger 启动：第一个 canary 立即启动，canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）
因果 Watchdog：2 秒 scenario-local，只认因果进展，不认 SSE 噪音
Release gate 恰好运行 3 轮，每轮所有场景并行
任一场景失败仍收割全部场景结果，再使本轮失败
泄漏检查：每个 scenario dispose 后检查 PID / port / session / worktree / 临时目录
```

Watchdog 有效心跳：
```text
  - blocking expectation 消费
  - API reconcile 发现新 terminal
  - Tool 完成（fork/join/list/verdict）
  - PTY/Process 退出
  - 显式因果 barrier
```

Watchdog 无效心跳：
```text
  - 任意 SSE 到达
  - session-created 事件（除非对应显式 barrier）
  - Blogger/Title/Compaction 等后台请求
```

[NORMATIVE] Watchdog 边界：

```text
Watchdog 只终止 test scenario，不终止生产资源。
被测动作拥有自己的 SSOT deadline 时，Watchdog deadline 必须大于该 deadline + cleanup allowance。
没有产品 deadline 的 LLM 请求，Watchdog 只能依据 mock expectation 因果进展，
不可用于真实 provider release gate。
```

## 十六、不修改 OpenCode 原则

除非修复阻止迁移测试运行的 host 兼容 bug，否则：

```text
禁止修改 opencode 本体
禁止增加新的插件 Hook
生产功能只在以下现有边界内工作：
  chat.message / experimental.chat.messages.transform
  tool.definition / tool.execute.before / tool.execute.after
  experimental.session.compacting / experimental.compaction.autocontinue
  event（仅用于 idle/retry/deleted 三信号）
  SDK client.session.* / prompt_async / session.messages
```

## 十七、完成定义

只有同时满足以下条件才可宣布新版完成：

1. 新架构所有行为有真实 OpenCode E2E。
2. 官方 compaction 关闭且 B 投影替换经长上下文验证。
3. fork/join/list 与 PTY E2E 稳定。
4. 四失败 Fallback 每角色生效。
5. Reviewer 双 PERFECT 与无 verdict guard 生效。
6. Orchestrator 可并行两个 Manager 并串行 rebase/review/ff。
7. 旧同步工具、todo、nudge、万象阵代码已删除，而非标 deprecated。
8. README 与唯一规范只描述新架构。
9. 搜索不到旧控制状态词，或每个命中均是历史文档/测试反例。
10. 生产主流程肉眼可证明，没有第二调度路径。


---

---

# KISS-N11 — Canary Mock 剧本森林（Script Forest）

[NORMATIVE] 本卷是 OpenCode E2E / canary 的 **唯一 mock 语义 SSOT**。
`testkit/opencode` 的 StrictMock、fixture JSON、gate 测试必须与本卷一致。
旧 lane 队列（session bind + turn 序号 + 匹配后 mute + 最小编号消歧）**废止**。

---

## 一、问题与目标

### 1.1 要模拟什么

Canary 不测「模型怎么想」，测 **生产插件在给定 provider 历史上是否发出正确的下一请求，以及工具/审查/发布因果是否成立**。
Mock 的责任是：对每一个到达的 provider request，返回 **确定性的** assistant 回应。

### 1.2 旧模型的失败点

旧 StrictMock 用：

```text
lane head（session/role/turn）
+ lastUser / tools 匹配
+ 匹配后 consume（mute）
+ 首次匹配 bind 物理 session id
```

导致：

- 两轮 reverify 若 lastUser 相同 → 撞 head / 无 head
- 要靠 `loadScripts` 分阶段注册、改生产文案、或 session id 因果
- 与「确认因果 = 内容、不是 host message id」的产品方向不一致

### 1.3 新模型一句话

```text
剧本 = 以 provider-visible 完整前缀为键的确定性响应森林。
同一前缀 → 唯一回应。
无编号、无 mute。
分叉只允许在不同的 user prompt 上。
```

---

## 二、术语 [NORMATIVE]

| 词 | 定义 |
|---|---|
| **Provider-visible 前缀** | 真正进入模型的 `tools + messages` 的规范化投影；见 §三 |
| **剧本边（edge）** | 一条「当前请求前缀 → assistant 回应」规则 |
| **剧本路径（path）** | 同一逻辑对话上严格延长的边序列：`P0→R0`，`P0+…→R1`，… |
| **剧本森林（forest）** | 一个 scenario 内全部路径的集合；可共享真前缀 |
| **请求前缀（request prefix）** | 当前 chat completions 请求的 provider-visible seal |
| **分叉点** | 两条路径共享某一真前缀 `P`，从 **不同的下一条 user 消息** 起分叉 |

禁止术语（实现与文档不得复活为控制概念）：

```text
lane turn 序号消歧
匹配后 mute / 哑
最小编号抢答
host message id 作为剧本身份
ConfirmationPromptMessageId 相等证明
```

---

## 三、Provider-visible 前缀键 [NORMATIVE]

### 3.1 纳入

与产品 KV-cache / Companion 门禁同一精神：

```text
tools[]: name + parameters（规范化）
messages[] 每条:
  role
  content 的可见 text / reasoning
  tool_calls: name + arguments（可见结构）
  tool result 的可见正文
```

### 3.2 排除

```text
message id / call id（若仅作宿主运输 id 且模型不依赖其内容语义——实现必须以「模型可见字节」为准）
timestamp / cost / usage / runtimeId / directory / status
UI metadata / finish reason（非模型字段）
Host accepted-<session> 合成 id
```

> 若 tool_call id 实际进入模型字节且影响可见性，则它属于可见内容；否则不得进入键。
> 默认策略：与现有 `sealProviderVisible` 一致，并随 Host 真实请求字段校准；**禁止**把 Host 传输 id 当剧本身份。

### 3.3 规范化

```text
属性序固定
空字段策略固定
字符串换行固定
同一语义 → 同一 digest / 同一 canonical JSON
```

`seal = canonicalJSON({ tools, messages })`。
两请求 `seal` 相等 ⟺ 同一剧本键。

### 3.4 前缀稳定 vs 不稳定

| 情况 | 处理 |
|---|---|
| 仅 Host 非可见 metadata 变 | seal 不变 → 同一剧本 |
| id/时间被误入 seal | 视为实现 bug；修正投影，不写海量剧本 |
| 语义内容变 | 不同键 → 不同边 |

---

## 四、匹配规则 [NORMATIVE]

对每个到达的 chat request：

```text
1. 计算 requestPrefix = sealProviderVisible(body)
2. 在当前 scenario 的剧本森林中查找「该前缀完整等于某条边的触发前缀」的边
3. 命中恰好 1 条 → 返回该边的 respond；不 mute、不编号
4. 命中 0 条 → FIRST SCRIPT MISMATCH（fail closed）
5. 命中 ≥2 条 → FIXTURE AMBIGUITY（加载期或请求期 fail closed；禁止最小编号抢答）
```

### 4.1 无 mute

同一前缀第 N 次出现，仍返回同一回应。
Mock 是 **幂等函数**，不是队列。

### 4.2 无编号

不存在 `00-xxx` 抢答。
消歧唯一手段：作者保证任意可达请求前缀 **至多一条** 边。

### 4.3 同路径更长前缀

多轮同会话：

```text
P0           → R0（如 first PERFECT tool-call）
P0 + R0 + …  → R1（如 text after tool）
P0 + … + U_confirm → R2（second PERFECT）
```

每条边键是 **完整当前前缀**，不是 lastUser。
confirmation 与 first 因历史不同而自然分流。

### 4.4 旁路（blogger / title / synthetic）

- 默认同主规则：靠可见前缀区分。
- 仅当旁路请求 **故意** 与主路径同构且需反复命中时，可声明 `reusable: true`（语义 = 该边幂等，本设计下默认已幂等）。
- **禁止** 用 mute 或 session bind 区分两个同构 blogger；测试必须用不同 first user / 不同可见内容。

---

## 五、分叉约束 [NORMATIVE]

### 5.1 允许的共享与分叉

```text
允许：两条路径共享真前缀 P
允许：从 P 之后下一条 **user** 消息内容不同而分叉
```

例：

```text
P = [system, tools, user: "Ship A"]
  └─ user: "continue review"     → path α
  └─ user: "resolve conflicts"   → path β
```

### 5.2 禁止的分叉

```text
禁止：同一请求前缀上写两个不同 assistant respond
禁止：以「assistant 不同」或「非 user 的任意宿主字段」作为兄弟边标签来消歧
禁止：以 host message id / session id / 到达次序 消歧
```

说明：

- tool-result **不同** 会使后续请求前缀不同，匹配层仍可区分；
- 作图层应写成两条完整路径，而不是「在 tool 节点上挂两个 sibling respond」。
- 分叉的 **作者可见标签** 只能是不同 user 文本（及由此产生的更长历史）。

### 5.3 并行同构会话

若两个 session 的 provider-visible 历史从第一条 user 起完全相同，则 **必须** 同一回应。
Canary 作者 **必须** 人为区分 first user（或其它可见内容），例如：

```text
Ship publish_proof.txt (job-A)
Ship publish_proof.txt (job-B)
```

否则视为 fixture bug，不是 mock bug。

---

## 六、与产品前缀缓存门禁的关系 [NORMATIVE]

Mock 仍强制：

```text
同一物理 session 的连续 chat 请求：
  新 seal 必须以旧 seal 为 provider-visible 字节前缀
  （tools 全等 + messages 为旧 messages 的前缀）
```

唯一允许的冷边界：产品 epoch 切换导致的 companion-b-head 替换（tools + 既有 system 可见约束仍按现网规则）。

剧本森林 **不替代** 前缀缓存门禁；二者叠加：

```text
先检查 session append-only seal
再按完整前缀查剧本边
```

---

## 七、Fixture 形状 [NORMATIVE]

### 7.1 逻辑形状

作者可以继续用「线性 scripts + match.user」书写 **路径**，加载器展开为边：

```text
路径内第 k 步的触发前缀 =
  该路径上第 0..k-1 步的可见历史
  + 当前请求的 tools / last user 约束所蕴含的最小可见请求
```

实现可选择：

1. **显式前缀边**（SSOT 理想形）：每步声明完整 `when.prefix`；或  
2. **路径糖**：同 `path`/`session` 别名的顺序步，由加载器模拟可见历史并生成边。

无论哪种，**运行时匹配只认完整前缀**，不认 turn 号。

### 7.2 废止的匹配依赖

```text
lane.turn 作为运行时匹配条件
sessionBindings 作为剧本身份
lastUser-only 作为唯一键
loadScripts 仅为了「错开同 lastUser 的两轮」——应改为历史前缀自然分流或不同 user
```

`loadScripts` 仍可用于 **进程生命周期**（restart 后追加 recovery 路径），不得用于「同前缀第二次换回应」。

### 7.3 双 PERFECT / reverify 范例

```text
path review-pre:
  1. tools=[verdict…], lastUser 含 "Review the current worktree"
       → tool-call verdict(PERFECT)
  2. （前缀含 first tool 结果后）text after tool
       → text
  3. lastUser 含 "PERFECT requires confirmation"
       → tool-call verdict(PERFECT)
  4. terminal text

path review-post:   # 历史已含 pre 的 publish/rebase 可见事实，或 first user 不同
  同形边；若 first user 与 pre 在完整历史上仍可能歧义，则必须区分 first user 或依赖更长历史
```

同句 reverify、历史不同 → 完整前缀不同 → 可共用书写模板，**键不同**。

---

## 八、错误与诊断 [NORMATIVE]

| 情况 | reason |
|---|---|
| 无边命中 | `no-prefix-matched`（可兼容旧日志文案 `no-lane-head-matched` 一轮迁移期） |
| 多边命中 | `ambiguous-prefix` |
| session seal 破坏 | `prefix-cache-invalidated` |

首次失败：mock 进入 fatal，后续请求 503；canary 停止。

诊断必须打印：

```text
session（仅诊断，不入键）
role / requestKind
tools
message count
lastUser 预览
matched/candidate edge ids
prefix digest（短 hash）
```

---

## 九、表达力边界 [NORMATIVE]

### 9.1 完备

在以下条件下，本模型对 canary 所需的确定性对话是完备的：

```text
- 前缀投影稳定
- 需要不同回应的历史，其 provider-visible 前缀不同
- 并行会话由测试控制的 first user（或其它可见内容）区分
- 同前缀需要幂等同一回应
```

### 9.2 故意不支持

```text
同前缀第 N 次不同回应
靠到达次序分配不同剧本
靠 host message id / session id 分流
靠 mute 制造一次性边
```

### 9.3 作者责任

```text
并行 job → 不同 first user
同构旁路 → 不同可见内容
歧义边 → 加载失败，修 fixture，不修生产 prompt 去迎合 mock
```

---

## 十、实现落点 [NORMATIVE]

| 组件 | 职责 |
|---|---|
| `sealProviderVisible` / prefix check | 键与 session append-only（已有，保留） |
| 剧本注册 | 边表：`prefixDigest → { id, respond, blocking, … }`；冲突即抛 |
| 选择器 | 精确 digest 查找；0/1/多 命中规则见 §四 |
| fixture 加载 | 路径糖展开为边；禁止 turn 序号匹配 |
| gate 测试 | 覆盖：同前缀幂等、user 分叉、歧义拒绝、seal 破坏、双 PERFECT 历史分流 |

### 10.0 实现注记（与 loader 糖）

运行时身份仍是 provider-visible seal（同 seal 幂等）。

作者糖允许：

```text
path 顺序边（lane.session + turn）：仅用于无强内容键的顺序步骤（manager join 等）
reusable + 内容键（user / userRegex / afterToolResult）：跨 barrier 复用；同 fingerprint 合并，alias id 供 wait
pathless + afterToolResult 文本：工具后中间轮，不推进 path
afterToolResult 相对「最后一条 user」之后是否已有 tool result
确认文案可用 userRegex 覆盖 "PERFECT requires confirmation|Continue the confirmation"
```

禁止用编号抢答或 mute 制造一次性边。

### 10.1 迁移

1. 写入本卷（本文件）。  
2. 替换 `strict-mock-lanes` 选择语义为前缀森林。  
3. 删除对 `sessionBindings` 作为剧本身份的依赖（诊断用 session 可保留）。  
4. gate-lane-cases 改为前缀森林契约测试。  
5. orchestrator / reviewer fixtures 去掉「仅为错开同 lastUser」的 loadScripts；靠历史前缀或不同 user。  

### 10.2 与 Release

本卷不改变 Fallback Host 契约：无 `retry` 前 model 解析 API 前，A/A/B/B 仍 **No-Go**。
剧本森林只解决 mock 因果表达，不伪造 Host 能力。

---

## 十一、验收清单

1. 同一 provider-visible 前缀两次请求 → 同一 respond（幂等）。  
2. 仅 lastUser 相同、完整历史不同 → 可命中不同边。  
3. 两路径仅 user 文本不同而分叉 → 合法。  
4. 同一前缀注册两个 respond → 加载失败。  
5. 无编号、无 mute、无 message-id 剧本身份。  
6. session seal 破坏仍 fail closed。  
7. reviewer 双 PERFECT 与 orchestrator pre/post review 不依赖 host message id。  
8. 文档与实现唯一 SSOT 为本卷；旧 lane 语义不得在新代码路径复活。

---

## 十二、设计精神对照

| 问题 | 答案 |
|---|---|
| 这是物理事实还是程序计数器？ | 事实：模型可见历史；不是 lane turn 计数器 |
| 能否用更少概念？ | 一个键（可见前缀）+ 确定性表；删除 bind/mute/编号 |
| 与产品一致吗？ | 与 KV-cache / 内容确认同一「可见字节」本体论 |


---
# Wanxiangshu.Next 语义内聚重构清单

## 当前重启盘点（2026-07-29）

> 此节取代下面的“停工交接”作为后续工作的起点。它只陈述本次重启直接观察到的事实；未重新编译或测试的 WIP 一律不视为完成。

### 已执行

- 用户要求后已停止全部并行代理；没有运行中的委派工作。
- 已两次执行 `git merge master`：分支先从 `6e5f0489` fast-forward 至 `9fc81135`，随后至 `9b3bdfe3`。每次均先 stash 全部本地 WIP 并在 fast-forward 后重新应用；stash pop 的冲突已人工合并。
  - `next/OpenCode/CompletedTurnClassifier.fs` 保留 typed `MessagePart` 分类路径与 master 的 `TurnNeedsContinuation` 语义，未恢复 raw `obj array`；
  - `tests-next/OpenCode/EventsTests.fs` 保留可编译的 typed `parts` fixture。
- 重启盘点时工作树含 **123 staged、5 unstaged、44 untracked** 路径。这些变更混合了先前已完成主体、未验证 WIP 与被取消代理留下的半成品；不得按文件名或 staged 状态推断其已交付。
- Orphan/compat 文件审计发现 `next/OpenCode/CompanionTransformHelpers.fs` 存在于磁盘但未被 `next/Wanxiangshu.Next.fsproj` 或 `tests-next/Wanxiangshu.Next.Tests.fsproj` 引用，为孤立文件；其余候选路径均已确认归属。

### WIP checkpoint

- 当前重启盘点与已修正的 production build 状态已提交并推送：`0217c7c3`（`WIP semantic cohesion refactor restart`）至 `origin/wanxiangshu-2`。该 commit 是可恢复检查点，**不是完成声明**；test project、Fable、canary 与 release gate 均尚未通过。

### 当前阻断（直接验证）

`dotnet build next/Wanxiangshu.Next.fsproj` 与 `dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj` 均已通过，0 errors、0 warnings。Fable production build（`npm run build`）已通过，0 errors。

`npm run test:next` **285 passed / 19 failed / 0 skipped**。失败可分为五组：

1. **ArchitectureGates（2 failures）**：`tests-next/Integration/OrchestratorRecoveryTests.fs` 301 行超出 300 行硬门禁；§17 语义门禁报告 23 个文件含 raw Host/Fable dynamic access、10 个文件违反单一写入口、3 个孤立 DSL program、3 个重复算法、11 个文件 > 280 行。

2. **ReconcileContinuationSupport（5 failures）**：`GatedSnapshot`、`bind`、`fallbackFailures`、`outcomeOf` 等测试抛出 `Cannot read properties of undefined`——mock/fixture 初始化缺口。

3. **Companion B 记录污染（5 failures）**：Blogger 的 `reasoning`/`private thought` 内容泄漏到正式 B 输出，违反 KISS-N02 §II 排除规则。

4. **CompanionEligibility（1 failure）**：缺少 authority 时应 fail-closed（返回 0），实际返回 1（fail-open）。

5. **ForkRuntime / HostForkRuntime / HostForkRestart（6 failures）**：完成路由与重启恢复中 DU `tag` 属性无法读取——mock host 响应缺少必要的区分联合 case。

> 盘点后修复：production build、test project 与 Fable 编译均已通过（0 errors、0 warnings）。`npm run test:next` 当前 285 passed / 19 failed。重点整治五个失败组后再继续其他 P0–P4 事项。

### 重启顺序

1. 修复当前 production build 的语法/命名阻断；随后编译 test project，记录所有继发错误。
2. 从当前文件与引用图重新分类 123+5+44 个 WIP：保留完整的垂直切片，删除半迁移兼容层和孤立文件。
3. 按 TASK 的 P0→P4 出口逐项完成，完成一项即运行对应 focused test；最后运行本文件列出的全量验证命令。
4. 仅在全量验证后更新“完成”陈述、README、SSOT 与迁移说明。

## 上一轮停工交接（2026-07-29；仅作历史基线）

> 以下记录的是 merge/restart 前的最后一次已知状态，不覆盖“当前重启盘点”。

### 已完成并在当时会话验证过


### 已完成并在当前会话验证过

- **P0/P1 正确性主线**：Fallback 单一 cursor、Prompt Authority 四模块、Host signal/reconcile 三层、Review Witness、Companion、Process/PTY 与五种 Flow DSL 的生产接线保持完成状态。
- **Tools 垂直切片完成**：
  - 新增并接线 `ToolHostCodec.fs`、`ToolRuntimeScope.fs`、`OneShotAgentTool.fs`、`ForkTool.fs`、`JoinTool.fs`、`ListTool.fs`、`PtyTool.fs`、`ExecutorTool.fs`、`InspectorTool.fs`、`CoderTool.fs`、`VerdictTool.fs`、`ToolRegistry.fs`。
  - `SpikePlugin` 已改用 `ToolRegistry.create`；旧 `ToolSurface*.fs` 与 `VerdictSurface.fs` 已删除；相关测试已迁移。
- **Child/Fork 生命周期收敛完成主体**：新增 `CompletionMailbox.fs`、`ChildRunProjection.fs`、`ForkRecovery.fs`；`ForkRuntime.fs` 不再承担恢复、状态渲染和邮箱的重复实现；中断恢复投影为 `AgentStatus.Interrupted`。
- **Journal 限界投影完成**：新增 Authority/Fallback/Review/Companion/Linkage/Orchestrator/Effect/Agent/ProjectionState 垂直投影；`Fold.fs` 只做 envelope/fact 路由；旧 `AgentFacts*.fs` 已删除；双 PERFECT 不再按文本推断。
- **Orchestrator 顺序程序完成**：`OrchestratorProgram.fs` 已成为 manager → pre-review → candidate → rebase → post-review → ff-only 的主程序；新增 IntegrationGate、WorktreeResource、ManagerJob、Recovery、GitOperations；旧 PublishChain/PublishStages/PublishLock/GitPort* 路径已删除。
- **Plugin composition-root 主体已接线**：
  - 新增 `PluginRuntimeScope.fs`，显式拥有 journal、Tool runtime、Host subscription、Companion、session association/cache 与 teardown。
  - `HostSignalBootstrap`、`SpikePlugin`、`CompanionTransform` 已改为使用同一 Scope；session delete 统一进入 `scope.DisposeSession`。
  - `PluginHost` 的隐藏 journal 全局 registry 与 `PromptDispatcher` 的隐藏 runtime registry 已删除。
  - Blogger budget 改为每个 plugin scope 独立，不再使用进程全局字典。
  - `sessionRoles` 不再用于新 user-message authority 绑定；绑定读取 durable active authority profile。

### 最近一次完整通过的验证点

以下结果发生在 PluginRuntimeScope/Bootstrap/SpikePlugin 接线完成之后、今日最后一批 targeted-completion 与新测试修改之前：

- `dotnet build next/Wanxiangshu.Next.fsproj` — 通过。
- `dotnet build tests-next/Wanxiangshu.Next.Tests.fsproj` — 通过。
- `npm run build` — 通过（Fable production）。
- tests-next Fable `--noCache` 编译 — 通过。
- `npm run test:next` — **283 passed, 0 failed, 0 skipped**。

### 今日停工时的未验证 WIP（下次必须从这里继续）

- `ForkRuntime`/`HostForkRuntime` 新增 targeted `AwaitAgent` completion；`OrchestratorHost` 已删除 generic join stash，并以 `publishToMailbox = false` 隔离内部 completion。新增对应 `ForkRuntimeTests`，**尚未重新 build/test**。
- 新增 `PluginRuntimeScopeTests.fs` 并加入 tests fsproj，覆盖 plugin 间 budget 隔离和幂等 disposal，**尚未重新 build/test**。
- 新建 `next/OpenCode/HostMessageCodec.fs`，准备把 `ReconciledTurn`/`SessionMessage` 的 raw `obj array` 改为 typed `MessagePart`；该文件目前**尚未加入 fsproj、尚未接线、尚未编译**，不能视为已交付。

### 后续剩余工作（不得缩减）

1. 先完成或回退 `HostMessageCodec.fs` WIP，再运行 production/test .NET build、Fable production/test 和 `test:next`，确认今日最后改动。
2. 完成 `PluginRuntimeScope` 审计：消除其余 feature-level hidden registry/HashSet 业务状态，确认 subscription、journal、Tool/Executor/Reviewer/Orchestrator/Companion 都恰好释放一次。
3. 完成 raw Host 边界类型化：`ReconciledTurn` 与业务分类器不得继续接收动态 `obj`；业务 program 不得直接使用 JS dynamic access。
4. 审计并处理机械/兼容候选：`SpikePluginHelpers.fs`、`TerminalPolicyHelpers.fs`、未编译的 `CompanionTransformHelpers.fs`、`AgentRoleHelpers.fs`、`FallbackDetect.fs` 旧 terminal 检测、重复 Review 查询层及其余仅转发文件。
5. 新增并启用 TASK §17 的语义架构门禁：机械文件名 allowlist、Host interop 边界、单一写入口、DSL 生产调用、依赖方向、重复算法、260/280/300 行规则。
6. 更新唯一架构文档和迁移说明；逐项审计本 TASK 每个出口标准。
7. 最终完整验证仍必须包括：
   - production/test .NET build；
   - Fable production/test compilation；
   - `npm run test:next`；
   - `npm run test:manager-tools`；
   - `node testkit/opencode/tests/gate-testkit.mjs`；
   - `npm run test:e2e:p0:three`；
   - `npm run test:release`；
   - production/test line-count 与全部语义架构门禁。

> 停工原因：用户于 2026-07-29 明确要求记录进度并结束今日工作。当前 WIP 未宣称完成；下次应从“未验证 WIP”第一项恢复。

## 一、重构目标

本轮重构不是简单减少文件数量，也不是把若干小文件重新拼成接近 300 行的大文件。目标是：

1. 保留 **每个 F# 文件不超过 300 行**的硬门禁。
2. 让文件边界与领域不变量、资源生命周期和外部协议边界一致。
3. 让核心业务流程重新表现为可顺序阅读的结构化程序。
4. 删除为迁就行数而产生的 `Helpers/Core/Primitives/Fields/Emit/Service` 式横向分层。
5. 消除重复事实来源、镜像状态和模块间隐式协议。
6. 让已经存在的 `agent / process / review / orchestrator / companion` DSL 真正进入生产主流程，而不是只存在于内核与测试中。
7. 保持已经冻结的 Authority、Fallback、Companion、Review、fork/join 等产品语义不变。

冻结设计明确要求源码使用 `let! / do! / use! / while / 尾递归` 直接表达过程，只持久化真实资源、少量缓存和外部事实，不持久化程序执行到了哪一步。当前 300 行门禁本身也明确禁止通过删除空行或压缩语句规避限制。

---

## 二、总体诊断

### 2.1 300 行门禁被执行成了“横向切片门禁”

附件快照中，生产目录包含大量以下形状：

* `Runner.fs / RunnerCore.fs / RunnerPrimitives.fs`
* `ToolSurface.fs / ToolSurfaceFields.fs / ToolSurfaceEmit.fs / ToolSurfaceFork.fs`
* `HostForkRuntime.fs / HostForkRuntimeFork.fs / HostForkRunLifecycle.fs / HostForkChildDispatch.fs`
* `PromptAuthority.fs / PromptAuthoritySend.fs / PromptAuthorityAccept.fs / PromptAuthorityService.fs`
* `AgentFacts.Types.fs / AgentFacts.FoldHelpers.fs / AgentFacts.Authority.fs / AgentFacts.Fallback.fs`
* `Orchestrator.GitPort.fs / Orchestrator.GitPortHelpers.fs / Orchestrator.GitPortWorktree.fs`

这些文件常按“类型、辅助函数、核心函数、发送函数、接收函数”拆分，而不是按完整语义拆分。结果是单个流程虽然没有任何文件超过 300 行，但必须同时打开五至九个文件才能理解。

仓库项目文件也直接反映了这些机械分组已经成为主要编译结构。

### 2.2 DSL 已存在，但生产流程没有真正使用

`Kernel/Flow.fs` 已经定义统一的闭包式 Flow、领域 Builder、`Using`、并行和结构化组合语义；宝典也要求 Reviewer 确认、Fallback、Orchestrator 等用局部递归和资源作用域表达。

但在当前附件快照中，没有发现生产主流程实际使用：

* `agent { ... }`
* `process { ... }`
* `review { ... }`
* `orchestrator { ... }`
* `companion { ... }`
* `Flow.run`

它们主要停留在内核、示例和测试层。生产实现仍以大型 `task {}`、可变字典、完成源和跨文件扩展方法组织。

这比“缺少 DSL”更危险：它形成了**架构表演层**——文档和测试证明 DSL 存在，真实业务却继续沿用另一套过程组织方式。

### 2.3 多处不是显式状态机，却形成了分布式隐式状态机

典型表现包括：

* 同一资源的多个布尔标志共同决定生命周期。
* 同一事实同时存在于字典、HashSet、Journal Projection 和闭包状态中。
* 一个动作由多个 Hook、计时器和回调共同推进。
* 通过字符串内容、固定提示词或 key 子串推断状态。
* 多个模块分别计算同一个 Fallback offset、agent side 或 terminal 判定。
* 主流程只负责注册回调，真正的“下一步”散落在事件处理模块中。

这类设计没有正式的 `Stage` 类型，却仍然把程序计数器展开到了业务状态中。

### 2.4 Host 动态对象泄漏过深

`obj`、动态属性访问、`createObj`、`unbox`、`jsNative` 等 Host/Fable 细节进入工具行为、运行时创建和业务分支，导致：

* 输入解析与领域决策混合。
* 每个工具重复处理空值、数字边界和 JSON 输出。
* 领域模块无法通过类型签名表达真实约束。
* 文件因 Glue Code 变长，再被迫机械拆分。

工具执行代码中已经可以看到参数解析、Host context、进程生命周期和结果编码挤在同一个闭包里的情况。

---

## 三、对初稿的必要修正

初稿正确识别了机械拆分、Host 泄漏和中间人层级，但以下建议不应直接采用。

### 3.1 不应全面禁止 `Types.fs`

`Types.fs` 不是天然错误。满足以下条件时可以保留：

* 是稳定的公开协议。
* 被三个及以上语义模块共同依赖。
* 类型自身表达领域约束，而不是为了给主文件腾行数。
* 文件中没有仅供一个相邻实现使用的私有类型。

错误的是“每个功能默认配一个 Types 文件”，而不是 Types 文件本身。

### 3.2 不应把所有 `TaskCompletionSource` 和布尔值都视为状态机

以下属于允许的物理事实：

* 一个真实任务是否仍在运行。
* 一个 completion cell 是否已经被设置。
* 当前是否存在 single-flight 请求。
* 是否收到新的 dirty 信号。
* PTY 是否已退出。
* Fallback offset 当前是多少。

宝典明确允许正在运行的 Task、完成 Channel 和简单 cursor。问题不是出现布尔值，而是：

* 多个标志描述同一个资源。
* 标志之间存在非法组合。
* 标志代表“流程走到了第几步”。
* 同一标志在多个模块分别维护。

因此应消除的是镜像状态和程序计数器，不是底层并发原语。

### 3.3 SessionReconciler 不应改成 MailboxProcessor

初稿提出使用 `MailboxProcessor`，但当前架构门禁明确排斥该实现方向；冻结设计本身也已经给出 dirty latch 加 single-flight reconcile 的语义。

正确方向是：

* 保留局部 dirty/in-flight 物理事实。
* 把绑定、快照分类、terminal 去重和调度拆成不同语义模块。
* 用单个结构化异步函数表达最多三次因果重读。
* 不再引入 Actor、邮箱协议或新的消息状态机。

### 3.4 不应简单把 Runner 三个文件全部合成一个 280 行文件

`RunnerCore` 已经接近门禁，再叠加 Runner、timer、spool 和 Host interop，只会得到一个更难维护的“合法巨石”。

正确拆分应沿生命周期边界：

* 请求与结果协议。
* Host 进程适配。
* 输出收集与 spool。
* Deadline。
* 一次执行的结构化主程序。

### 3.5 不以“文件数减少 35%”作为目标

减少文件数可能是结果，但不是验收标准。更重要的指标是：

* 理解一个主流程需要打开几个文件。
* 一个事实有几个写入口。
* 一个修改会横跨多少模块。
* 是否存在仅做透传的文件。
* 核心流程是否能在一分钟内顺序读懂。

---

## 四、300 行下的“有脑拆分宪法”

### 4.1 文件的合法拆分单位

一个生产文件应至少拥有以下二者之一：

1. **一个完整领域不变量**

   * 例如 Authority Root 的接受规则。
   * Review witness 的有效性。
   * Fallback cursor 的推进规则。
   * Prefix epoch 的替换规则。

2. **一个完整资源生命周期**

   * 例如一次子 Agent Run。
   * 一个 PTY Session。
   * 一个工作树资源。
   * 一次进程执行。
   * 一个 Blogger in-flight job。

仅仅“这些函数都是 helper”“这些都是 emit”“这些是 private types”不构成文件边界。

### 4.2 推荐行数区间

* 常规语义模块：120–240 行。
* 主流程模块：160–260 行。
* Host codec 或协议枚举：可到 280 行，但必须有明确理由。
* 281–300 行：视为需要架构审查的告警区，而不是正常目标。
* 300 行：硬失败。

### 4.3 主流程文件必须连续可读

以下流程各应有一个明确主文件，读者无需跳转即可看到完整顺序：

* 子 Agent fork 到 completion。
* Reviewer 双 PERFECT。
* Companion 投影到 Blogger 到 B 更新。
* Process spawn 到 cleanup。
* Orchestrator worktree 到 FF publish。
* Prompt claim 到 accept/abandon。
* idle 到 reconcile 到 terminal policy。

依赖模块只提供领域动词，不接管流程控制。

### 4.4 垂直切片优先于横向切片

以工具为例，一个工具切片应尽可能同时拥有：

* 强类型输入。
* 输入校验。
* 领域动作调用。
* 强类型输出。
* Host schema 的局部组装。

不能再把所有字段放 `Fields.fs`、所有 JSON 放 `Emit.fs`、所有执行放 `Fork.fs`。

### 4.5 一个事实只能有一个拥有者和一个权威写入口

例如：

* Fallback cursor advance：只由 retry signal path 写。
* 插件 user-shaped prompt：只由 PromptDispatcher 发。
* PTY exit：只由 backend `onExit` 完成。
* Review confirmed：只从两份有效 witness 派生。
* Companion busy：只由 `inFlightTask option` 表示。
* Child completion：只由 completion cell 首次成功写入。

### 4.6 文件名必须表达业务语义

以下后缀不全面禁止，但新增时必须经过架构审查：

* `Helpers`
* `Core`
* `Primitives`
* `Fields`
* `Emit`
* `Service`
* `Manager`
* `Registry`
* `Utils`

优先使用：

* `PromptIngress`
* `ReviewWitness`
* `ChildRun`
* `ProcessOutput`
* `PtySupervisor`
* `HostEventCodec`
* `PrefixEpoch`
* `IntegrationGate`
* `AuthorityLedger`

---

# 五、P0：立即处理的正确性与唯一事实源问题

## 5.1 Fallback 统一为一个 cursor 模块

### 当前问题

Fallback 语义散落在：

* `Session/Fallback.fs`
* `Session/DurableFallback.fs`
* `OpenCode/EffectiveAgentResolver.fs`
* `OpenCode/FallbackDetect.fs`
* `OpenCode/RetrySignalHandler.fs`
* `OpenCode/PluginFallbackRetry.fs`
* Journal projection/fold

不同模块分别拥有 side、advance、effective agent、failure identity 或 durable append 的部分逻辑。

其中 `PluginFallbackRetry` 仍包含 pending timer、debounce 和 terminal failure 后续调度，使“provider retry 是唯一 durable 推进入口”的冻结规则变得难以验证。

### 目标结构

建立唯一的纯领域模块，例如：

* `Domain/AgentPairCursor.fs`

只拥有：

* Offset 合法性。
* A/A/B/B side 映射。
* `(offset + 1) mod 4`。
* Authority profile 到 EffectiveAgent 的解析。
* Fallback attempt identity。

其他模块不得重新实现以上规则。

### 重构任务

1. 删除 Journal、OpenCode 和 Session 中重复的 side/advance 实现。
2. Journal 只保存 cursor 数据，不拥有第二套算法。
3. `RetrySignalHandler` 成为唯一 durable advance 写入口。
4. `PluginFallbackRetry` 不得根据 terminal error 自行推进 cursor。
5. 删除第四次失败死亡、兼容型 `isDead`、无意义 `NextAttempt` 包装。
6. ProviderRetryAttempt continuation 只能延续同一 Logical Run，不得重置 root、repair 或 cursor。
7. 删除 wall-clock debounce 对语义正确性的依赖。
8. 添加 12 次及以上 retry 的单一轨迹测试。

冻结语义明确要求 retry 是唯一 durable 写入口，永久 A/A/B/B 循环且不存在 Dead。

### 出口标准

* 仓库只有一个 `advance offset` 实现。
* 仓库只有一个 `EffectiveAgent` 映射实现。
* 只有 retry signal handler 可写 `FallbackCursorAdvanced`。
* terminal、idle、repair 和 continuation 不推进 cursor。
* 12 次 retry 后仍产生下一次物理请求。

---

## 5.2 Prompt Authority 收敛为四个语义模块

### 当前问题

当前六层结构包含：

* `PromptAuthority`
* `PromptAuthoritySend`
* `PromptAuthorityAccept`
* `PromptAuthorityRestore`
* `PromptAuthorityService`
* `PromptDispatcher`

其中部分模块主要包装参数或转发调用；Service 又维护全局实例、锁、projection 和 journal，导致纯领域规则、持久化和 Host I/O 相互穿透。

### 目标结构

1. `PromptAuthority.fs`

   * 纯值和纯规则。
   * Authority/Attempt profile。
   * Origin、claim、identity。
   * 不接触 JS、Journal writer 或 Host。

2. `PromptAuthorityLedger.fs`

   * 内存 projection。
   * Journal append。
   * restore。
   * 原子更新。

3. `PromptIngress.fs`

   * 处理 Host `chat.message` 的输入接受。
   * HumanRoot、AgentOwnerRoot、Continuation、Unknown 的分类。
   * Unknown fail-closed。

4. `PromptDispatcher.fs`

   * 插件发出 user-shaped message 的唯一出口。
   * claim → Host send → accept/abandon。
   * metadata correlation。
   * `Agent=EffectiveAgent`、`Model=None`。

### 重构任务

1. 合并 `Send/Accept/Service` 中纯透传层。
2. 删除按 session 隐藏的全局 service registry，改由 Plugin RuntimeScope 显式拥有。
3. 将 hashing、动态 metadata 解码移到 Host codec。
4. 所有 Guard、repair、confirmation、busy nudge、fallback continuation 统一经过 Dispatcher。
5. 禁止模块直接调用 `prompt_async`。
6. Authority ledger 不读取 `sessionRoles` 推断权威。
7. `PhysicalUserMessageId` 与 `AuthorityRootUserMessageId` 始终分离。
8. 删除按零宽文本或固定英文提示识别 origin 的路径。

冻结清单要求所有插件 prompt 发送点经过 Dispatcher，并明确禁止 synthetic 改写 Authority。

### 出口标准

* 出站 prompt 只有一个实现入口。
* 入站 root 接受只有一个实现入口。
* Unknown origin 无副作用。
* Authority 纯领域模块不依赖 Fable、Journal writer 或 Host obj。
* 删除 `PromptAuthorityService` 之类的中间人层。

---

## 5.3 Host Signal 与 Reconcile 去除重复协议解释

### 当前问题

Host event 的 unwrap、type 判定和 session ID 提取在 Adapter、Subscribe、Bootstrap 等多个模块重复。

`SessionReconciler` 同时负责：

* active run 绑定。
* continuation message 绑定。
* consumed assistant 去重。
* snapshot 分类。
* Unknown 重读。
* single-flight 调度。

`TerminalPolicies` 又同时处理：

* A 累积。
* Interaction repair。
* Review/Manager Guard。
* Fallback 后续。
* completion。
* session cleanup。

这使“idle 只是信号，完整 snapshot 才是事实”的简单原则被大量控制胶水包围。

### 目标结构

1. `HostEventCodec.fs`

   * 唯一 raw `obj` 解码点。
   * 输出 `SessionIdle / ProviderRetry / SessionDeleted`。

2. `TurnBinding.fs`

   * 管理 root user、physical user、continuation 的绑定。
   * 不读取 Host payload。

3. `TurnReconcile.fs`

   * 纯函数：typed snapshot + binding → `ReconciledTurn option`。
   * Unknown 无副作用。

4. `ReconcileSupervisor.fs`

   * 每 session 一个 single-flight。
   * `inFlight Task option` 与 dirty latch。
   * 最多三次因果 yield 重读。
   * 不使用 MailboxProcessor。

5. `TurnCompletionProgram.fs`

   * 一个连续可读的结构化程序。
   * 顺序执行 terminal 后的领域动作。

### 重构任务

1. 删除重复 raw event 解析。
2. 让业务层不再接收 `obj`。
3. 将 terminal 分类从 supervisor 中移出。
4. 将 continuation/root binding 从 snapshot 分类中移出。
5. 把 terminal policy 拆成窄动词，但保留一个可连续阅读的主程序。
6. 重复 idle 只能消费一次 assistant。
7. 三次 Unknown 后保留 dirty，等待下一粗粒度信号。
8. retry 不进入 reconcile completion 路径。
9. 删除无宿主契约依据的计时器补丁。

### 出口标准

* Raw Host payload 只在 codec 中出现。
* 同一 session 最多一个 snapshot 请求。
* `Dirty/Running` 不再散落；只由 supervisor 聚合拥有。
* Reconcile 是纯分类，Supervisor 是调度，Completion Program 是业务流程。
* 断开所有碎片事件后产品 E2E 仍通过。

---

# 六、P1：让 DSL 真正替代状态机

## 6.1 Flow DSL 必须进入生产，或者删除

### 当前问题

保留一套没有生产调用者的 Flow DSL，会增加概念数量而不降低复杂度。

### 重构任务

已确定唯一执行方向：正式采用 DSL（方向 A），不再保留备选方案。

#### 方向 A：正式采用（唯一方案）

将以下生产主流程逐步改为相应 DSL：

* `ProcessRunner` → `process { ... }`
* Reviewer confirmation → `review { ... }`
* Child fork/run lifecycle → `agent { ... }`
* Companion update → `companion { ... }`
* Orchestrator publish → `orchestrator { ... }`

要求：

* DSL 只做顺序组合、错误短路、资源释放。
* 不自动重试、不写 Journal、不刷新 projection。
* 领域动作通过窄 Script/Port 注入。
* 真实资源使用 `use!`。
* 循环使用局部递归或 `while`。

本方向为唯一实施路径，所有生产代码迁移必须以此为准执行，不再保留或讨论替代方案。

### 出口标准

* 每种 DSL 至少有一个真实生产主流程调用者。
* GuideContract 编译真实生产 program，而非复制示例。
* 主流程不再靠事件注册和外部 flag 拼装。
* 不产生 Flow AST 或解释器。

---

## 6.2 清理 `Agent/Programs.fs` 与 `ProgramCapabilities.fs`

### 当前问题

附件快照中未发现这两个模块被生产代码调用。它们更像测试用 capability factory，而不是“Agent Program”。

### 目标

二选一：

1. 将其改为真实的角色工具权限 SSOT，并接入 Tool Registry。
2. 删除它们，将静态权限表放入明确命名的 `RoleToolset.fs`。

### 出口标准

* 角色能力矩阵只有一个来源。
* Manager/Orchestrator 的极小 DSL 由静态工具表保证。
* 不保留名称宏大但无生产作用的模块。

---

# 七、P1：子 Agent 运行时重构

## 7.1 用 ChildRun 聚合替代 HostFork 扩展文件森林

### 当前问题

`HostForkRuntime` 的完整行为被按方法拆进多个文件。`HostPendingRun` 又通过 Token、Source、Subscription、Ready、Finished 等字段共同描述一个 run。

问题不是 completion cell，而是一个 run 没有单一聚合拥有生命周期。

### 目标结构

1. `ChildRun.fs`

   * 一个物理 child run 的完整聚合。
   * RunIdentity。
   * Authority root。
   * prompt acceptance。
   * terminal subscription。
   * single-assignment completion。
   * cancellation/disposal。

2. `ChildDispatch.fs`

   * New AgentOwnerRoot。
   * BusyAgentNudge。
   * 统一调用 PromptDispatcher。

3. `ChildRecovery.fs`

   * 重启后恢复 linkage 和未消费 completion。
   * 不创建虚假新 run。

4. `ForkRuntime.fs`

   * Manager 所拥有的 handle map。
   * completion mailbox。
   * `fork/join/list/cancel`。
   * 不处理 Host 动态对象。

### 重构任务

1. 删除 `HostForkRuntimeFork` 式按方法扩展文件。
2. 把 `Ready/Finished` 多标志折叠成一个 run 生命周期资源。
3. Prompt 被 Host 接受后再完成 run 注册，不保留半注册状态。
4. Terminal/SendFailure/Cancel 继续竞争同一个 completion cell，首写胜出。
5. Busy nudge 不创建 completion、不替换 active RunId。
6. Idle existing agent 新任务创建新的 AgentOwnerRoot 和 completion。
7. 对内部 Host 工作流提供返回明确 completion task 的启动 API。
8. LLM 可见的 `fork` 仍保持返回 agent ID，不扩大 DSL。
9. `join()` 仍消费任意完成项，并永久删除 handle。

### 出口标准

* 理解一次 child run 只需打开 `ChildRun` 和 `ChildDispatch`。
* 不存在 Ready/Finished 非法组合。
* Orchestrator 不再通过 generic join 加 stash 等待指定 Manager。
* Busy nudge 的同 Run 语义有单一测试和单一实现。

---

# 八、P1：Review 从计数器窗口改为 Witness 模型

## 8.1 当前问题

现有 Review projection 包含：

* `ConsecutivePerfects`
* `IsConfirmed`
* `AcceptedGuardKey`
* 最近 verdict ID
* confirmation message ID
* Git tree hash

部分逻辑还通过 key 内容或固定提示词判断 confirmation。这把“两个外部审查事实”展开成了一组需要同步维护的过程状态。

## 8.2 目标模型

Journal 只保存外部 witness：

* ReviewBarrierId。
* 第一份 PerfectWitness。
* 第二份 ConfirmingPerfectWitness。
* RevisionWitness。
* 每份 witness 的 ToolCallId。
* ProviderRunIdentity。
* Git tree hash。
* Physical confirmation user message ID。
* AuthorityRootUserMessageId。

以下内容全部派生：

* 是否已有第一次 PERFECT。
* 是否已经确认。
* tree 是否仍有效。
* 是否需要 confirmation continuation。

## 8.3 重构任务

1. 删除 `ConsecutivePerfects`。
2. 删除单独持久化的 `IsConfirmed`。
3. 用结构化 `GuardKind/BarrierId` 替代字符串 key 解析。
4. 第一和第二 witness 必须来自不同 ProviderRunIdentity/ToolCallId。
5. 两份 witness 必须绑定相同 Git tree。
6. post-rebase 创建新的 barrier，不能复用旧 witness。
7. Reviewer terminal 无 verdict 时，由局部 review program 发 continuation 并递归等待。
8. REVISE 立即返回 revision，不参与 PERFECT 计数。
9. Manager Guard 和 Reviewer Guard 只延续原 Logical Run。
10. `ReviewerGuardState.fs` 改为 witness projection 的纯查询，或删除。

## 8.4 出口标准

* Review 主流程在一个 `review {}` 函数中连续可读。
* Journal 只记录发生过的 verdict 和 Host acceptance。
* 不存在 ReviewStage、计数窗口或字符串提示识别。
* 一次 PERFECT 永远无法放行。

---

# 九、P2：Companion 消除双重 busy/reset 状态

## 9.1 当前问题

Companion 内部和 CompanionHost 各自维护一套状态，例如：

* `busy`
* `inFlightTask`
* `bloggerTask`
* `bloggerFailed`
* `bloggerNeedsReset`
* `latestB`
* `activePrefixEpoch`
* `replacementActive`

部分字段是真实缓存，部分字段是同一物理事实的镜像。

## 9.2 目标结构

1. `CompanionState.fs`

   * 单一内存聚合。
   * LatestB。
   * FrozenB/ActivePrefixEpoch。
   * last successful projection。
   * blogger linkage。
   * 一个 `inFlightTask option`。

2. `CompanionProjection.fs`

   * canonical provider-visible view。
   * semantic delta。
   * coverage/digest。
   * 不接触 Host session。

3. `PrefixEpoch.fs`

   * FrozenB 与 LatestB。
   * epoch switch 条件。
   * replacement projection。
   * 缓存不变量。

4. `CompanionRun.fs`

   * 一个 Blogger job 的结构化生命周期。
   * 忙时跳过，不排队、不推进 delta baseline。
   * 成功后原子更新 B 和 baseline。

5. `BloggerHost.fs`

   * 创建、恢复和复用 Blogger session。
   * dispatch 和收集完整 assistant。

6. `CompanionTransform.fs`

   * Host projection hook adapter。
   * 不保存业务状态。

## 9.3 重构任务

1. 用 `inFlightTask option` 作为唯一 busy 真相。
2. 删除独立 `busy` 和 `bloggerTask` 镜像。
3. 删除 `bloggerNeedsReset ref`；reset 由 durable linkage 和请求形状派生。
4. Companion eligibility 只读 ActiveLogicalRun。
5. synthetic continuation 不推进 delta baseline。
6. 所有 canonical hash 和 delta 纯函数化。
7. Host `obj` 解码移到 projection codec。
8. FrozenB 只在 epoch 切换时更新。
9. LatestB 增长不得改变 X 的普通回合前缀。
10. 重启时优先复用原 Blogger session。

## 9.4 出口标准

* 一个 session 只有一份 Companion state。
* busy、reset、epoch 不再由多个模块分别维护。
* Prefix cache、replacement、restart 和 skipped-delta E2E 完全保持。
* Companion 核心不依赖 Fable 动态对象。

---

# 十、P2：Process 按生命周期拆分，而非 Core/Primitive 拆分

## 10.1 目标结构

1. `ProcessRequest.fs`

   * Command。
   * Estimate。
   * Outcome。
   * Error。
   * 稳定公共协议。

2. `NodeProcessHost.fs`

   * spawn。
   * stdin/stdout/stderr binding。
   * signal/kill。
   * JS/Fable interop。

3. `ProcessOutput.fs`

   * stdout/stderr 聚合。
   * byte 计数。
   * spool threshold。
   * completed/spooled 结果。

4. `Deadline.fs`

   * 唯一 deadline/timer 实现。
   * segmented timer 也只在此实现。

5. `ProcessRunner.fs`

   * 完整结构化生命周期：

     * 校验预算。
     * 获取 large gate。
     * spawn。
     * 启动 output collection。
     * 等待 exit/deadline/cancel。
     * kill。
     * cleanup。
     * 返回结果。

6. 保留语义内聚的：

   * `Spool.fs`
   * `LargeGate.fs`

## 10.2 重构任务

1. 删除 `RunnerPrimitives.fs`。
2. 删除 `RunnerCore` 与 `Runner` 间透传。
3. 合并重复的 timer/deadline 实现。
4. 检查并删除无生产引用的 `Pump.fs`、`ProcessBudget.fs`，或将其职责并入明确模块。
5. `ProcessRunner` 使用 `process {}` 和资源作用域。
6. Host child/process handle 使用可释放资源封装。
7. output collector 不负责进程调度。
8. executor summary 与 ProcessRunner 解耦。
9. `estimated_running_secs × 3` 仍是唯一运行时限。
10. spool 和 ripple-carry summary 产品语义不变。

## 10.3 出口标准

* Runner 主文件能从上到下看完一次执行。
* JS spawn 细节不进入业务程序。
* 只有一个 deadline 实现。
* 没有 Core/Primitive 透传层。
* cancel、timeout、exit 三条路径全部保证清理。

---

# 十一、P2：PTY 建立单一 Session 聚合

## 11.1 当前问题

PTY 的 active handle、closed ID、read waiter、exit task、backend state、parent aborter 分散在多个模块和全局字典中，形成同一 PTY 的多份真相。

## 11.2 目标结构

1. `PtyProtocol.fs`

   * PtyId。
   * Command。
   * Signal enum。
   * Read/Write/Resize 请求和结果。

2. `NodePtyHost.fs`

   * Bun/node-pty 加载。
   * spawn/write/resize/signal。
   * data/exit callback。

3. `PtySession.fs`

   * 单个 PTY 的 handle。
   * output buffer。
   * pending read。
   * exit completion。
   * close/dispose。
   * 所有状态由该聚合拥有。

4. `PtySupervisor.fs`

   * `PtyId → PtySession`。
   * parent ownership。
   * list。
   * close one/all。
   * parent cancel。

## 11.3 重构任务

1. 合并 `PtyBackendRegistry` 的双层 live/pending map。
2. 删除 `PtyApi` 中独立的全局 parent abort registry。
3. PTY completion 只由 backend `onExit` 写入。
4. Signal/Close 不提前标记完成。
5. pending read 与 output buffer 归属于同一个 PtySession。
6. close 后 read 行为由一个协议定义，不靠 closed ID 辅助表猜测。
7. grace timing 并入明确的 termination policy。
8. PTY 不伪装为 Agent Session。

## 11.4 出口标准

* 查询一个 PtyId 只得到一个聚合。
* 不存在 active、backend、abort 三份镜像 registry。
* parent cancel 的拥有关系可从 Supervisor 直接看出。
* exit completion 只有一个写入口。

---

# 十二、P3：Tool Surface 改为 DSL 动词垂直切片

## 12.1 目标结构

1. `ToolHostCodec.fs`

   * context 解码。
   * typed args 解码。
   * schema 基础构件。
   * result 编码。
   * abort attachment。
   * 唯一 JS 动态边界。

2. `ForkTool.fs`

3. `JoinTool.fs`

4. `ListTool.fs`

5. `PtyTool.fs`

6. `ExecutorTool.fs`

7. `InspectorTool.fs`

8. `CoderTool.fs`

9. `VerdictTool.fs`

每个动词文件拥有自己的强类型请求、校验、领域调用和返回映射。

10. `ToolRegistry.fs`

    * 按 Agent role 组装允许的工具集合。
    * 不包含执行逻辑。

11. `ToolRuntimeScope.fs`

    * per-session ForkRuntime。
    * reviewer host。
    * orchestrator host。
    * executor runtime。
    * 生命周期与 dispose。

## 12.2 重构任务

1. 删除 `ToolSurfaceFields.fs`。
2. 删除 `ToolSurfaceEmit.fs`。
3. 将 `textArg/outputArg/runtimeArg` 等集中到 codec 的 typed decoder。
4. 工具处理器不再接收裸 `obj`。
5. Manager 只注册 fork/join/list。
6. Orchestrator 只注册 fork/join。
7. Blogger/Executor 内部 agent 名称不进入工具 schema。
8. Tool Registry 不创建全局 runtime 字典。
9. `ToolSurface.fs` 缩减为短 assembly module，或更名 `ToolRegistry.fs`。

## 12.3 出口标准

* 修改 fork 参数时主要只需打开 ForkTool。
* JS 动态访问只存在于 ToolHostCodec。
* 角色能力矩阵只有一个 SSOT。
* 不再出现 Fields/Emit 式横向拆分。

---

# 十三、P3：Orchestrator 恢复为一个可读发布程序

## 13.1 当前问题

Orchestrator 的控制流分布在：

* core engine。
* publish stages。
* publish chain。
* git port helpers。
* OpenCode host。
* manager job。
* review read。
* sweep。
* authority。
* session directories。

此外，generic join 与 stash 被用于等待特定 Manager completion，说明内部程序受到了模型可见 DSL 形状的反向限制。

## 13.2 目标结构

1. `OrchestratorProgram.fs`

   * 唯一主程序：

     * 创建 worktree。
     * 启动 Manager。
     * 获取 candidate。
     * pre-rebase review。
     * 获取 integration gate。
     * rebase。
     * conflict continuation 或继续。
     * post-rebase review。
     * FF publish。
     * cleanup。

2. `ManagerJob.fs`

   * 一个 Manager 工作任务聚合。
   * manager child completion。
   * worktree。
   * candidate。
   * review barrier。

3. `WorktreeResource.fs`

   * create/dispose。
   * 保留策略。
   * 使用 `use!`。

4. `GitOperations.fs`

   * typed Git verbs。
   * 无 workflow stage。

5. `IntegrationGate.fs`

   * 串行 publish 的真实资源。
   * 以可释放 lease 表示，不用 mutable task chain 表示阶段。

6. `OrchestratorRecovery.fs`

   * 从 Journal 的外部 Git 事实恢复。

7. `OrchestratorHost.fs`

   * 仅做 OpenCode session 桥接和 runtime scope 接线。

## 13.3 重构任务

1. 将 `PublishStages` 改为领域动作，或并回主程序的局部函数。
2. 删除 `publishChain` 式手工 Task 链。
3. 内部 child 启动返回明确 completion handle，不再 stash generic join 结果。
4. conflict 返回原 Manager 同一 Logical Run。
5. Manager 已 terminal 时用 continuation，不建新 root。
6. worktree cleanup 使用资源作用域。
7. 真实 Git 恢复事实继续持久化：

   * target ref。
   * expected target head。
   * candidate commit。
   * rebased commit。
   * conflict files。
   * publish claim。
8. 删除只表示“下一步做什么”的 stage 字段。
9. post-rebase 必须创建新 review barrier。
10. `GetTargetHead` 失败继续 fail-closed。

## 13.4 出口标准

* 一分钟内能从主程序看见完整发布链。
* 无 Stage/Scheduler/DAG。
* publish 串行性来自 IntegrationGate 资源。
* 重启恢复依据 Git 与 Journal 外部事实，而非程序计数器。
* 只有 FF 成功才完成 published join。

---

# 十四、P3：Journal 按投影限界上下文拆分

## 14.1 当前问题

当前 `AgentFacts.Types.fs` 集中容纳 Authority、Fallback、Review、Companion、Linkage、Orchestrator 等不同领域类型；相应 fold 又拆成多个按技术类别命名的文件。

把所有 session 事件合成一个近 280 行 `SessionFold.fs` 也不是理想方案，它只是另一种巨型聚合。

## 14.2 目标结构

采用垂直投影切片：

* `AuthorityProjection.fs`
* `FallbackProjection.fs`
* `ReviewProjection.fs`
* `CompanionProjection.fs`
* `LinkageProjection.fs`
* `OrchestratorProjection.fs`

每个文件同时拥有：

* 自己的 projection 类型。
* empty/default。
* 对应事实的 fold。
* 派生查询。

另设：

* `AgentProjection.fs`

  * 组合各子 projection。
  * 顶层路由。
* `Journal/Fold.fs`

  * envelope 解码和全局分发。

## 14.3 重构任务

1. 删除 `AgentFacts.FoldHelpers.fs`。
2. 拆除全局 Types 杂物袋。
3. 每种 Fact 明确唯一 fold owner。
4. 共享 ID 放 `Kernel/Identity.fs`。
5. projection 中只存重启后需要恢复的事实。
6. `IsConfirmed`、EffectiveAgent 等可派生值不持久化。
7. fold 必须保持纯函数。
8. durable effect 与 projection fold 分开。
9. 编码兼容仅针对当前 0.5.0 SSOT，不保留旧控制状态兼容。

## 14.4 出口标准

* 新增一个 Review fact 只修改 ReviewProjection 和 codec。
* 一个 fact 不会被多个 fold 模块重复解释。
* Journal 不保存程序下一阶段。
* 删除 FoldHelpers 和通用状态杂物袋。

---

# 十五、P4：Plugin 与运行时拥有关系清理

## 15.1 当前问题

`ToolSurface`、`HostSignalBootstrap`、`SpikePlugin` 等 composition root 内存在多组字典：

* Fork runtimes。
* Executor runtimes。
* Reviewer hosts。
* Orchestrator hosts。
* tree ports。
* session roles。
* parent mappings。
* verdict sets。
* nudge sets。
* fallback sets。

这些集合分散地承担资源拥有、缓存、去重、权威和业务状态。

## 15.2 目标结构

建立显式 `PluginRuntimeScope`：

* SessionRuntime。
* Child/ForkRuntime。
* Companion runtime。
* Reviewer runtime。
* Orchestrator runtime。
* Executor runtime。
* cancellation/disposal。
* Host association cache。

业务 projection 和 durable facts不放入 RuntimeScope。

## 15.3 重构任务

1. Plugin 只负责读取配置、创建 Scope、注册 hooks/tools、dispose。
2. 所有 per-session runtime 由 Scope 创建和释放。
3. HashSet 去重逐项审计：

   * 是持久事实则进入 projection。
   * 是资源状态则进入对应 aggregate。
   * 是临时调用去重则由调用作用域拥有。
4. 删除 feature module 中隐藏的全局 registry。
5. `sessionRoles` 不再充当 Authority 或 Companion eligibility 来源。
6. session deleted 统一调用 Scope cleanup。
7. 避免 Bootstrap 同时组装业务规则和资源。

## 15.4 出口标准

* 从 Scope 可以直接看出谁拥有哪个长期资源。
* Plugin disposal 不需要遍历多个模块的私有全局表。
* 业务状态不再藏在 composition root 的 HashSet 中。

---

# 十六、删除与引用审计候选

以下文件或职责在附件快照中未发现明确生产调用，或与其他模块高度重复，应逐项做引用审计，不应未经验证直接删除：

* `Process/Pump.fs`
* `Process/ProcessBudget.fs`
* `Orchestrator.Engine.fs`
* `Agent/Programs.fs`
* `Agent/ProgramCapabilities.fs`
* `MessageOriginDecoder.fs` 中旧 origin 推断分支
* `FallbackDetect.fs` 中 terminal failure 检测部分
* 重复的 Review Guard 查询层
* `SpikePluginHelpers.fs`
* `TerminalPolicyHelpers.fs`
* `CompanionTransformHelpers.fs`
* `Orchestrator.GitPortHelpers.fs`

审计规则：

1. 无生产调用且仅测试自身：删除或接入真实主流程。
2. 只有一个调用者且必须同时修改：合并回调用者。
3. 只是参数转发：删除。
4. 提供稳定外部协议或独立纯算法：保留并更名。
5. 为旧实现兼容存在：按 0.5.0 不兼容旧控制状态的原则删除。

---

# 十七、需要新增的架构门禁

现有 300 行门禁只控制文件大小，无法阻止机械拆分。必须增加语义门禁。

## 17.1 文件形状门禁

对生产目录新增以下文件名时要求显式 allowlist：

* `*Helpers.fs`
* `*Primitives.fs`
* `*Fields.fs`
* `*Emit.fs`
* `*Service.fs`
* `*Core.fs`

现存文件在本轮重构后逐步清零，确有合理用途的必须在架构测试中写明理由。

## 17.2 Host 边界门禁

以下内容只能出现在明确的 Adapter/Codec 文件：

* `Fable.Core.JsInterop`
* 动态属性访问。
* `createObj`
* `unbox`
* `jsNative`
* Host raw `obj`

Kernel、Domain、Session program、Review、Orchestrator program 不得引用。

## 17.3 单一写入口门禁

静态扫描要求：

* `FallbackCursorAdvanced` 只能由 RetrySignalHandler append。
* 插件 synthetic prompt 只能由 PromptDispatcher 调 Host。
* PTY completion 只能在 backend exit path 设置。
* Review confirmed 不得直接赋值，只能派生。
* model 字段不得由 Wanxiangshu 设置。

## 17.4 DSL 落地门禁

二选一：

* 生产代码必须存在已批准主程序对 `agent/process/review/orchestrator/companion` Builder 的调用。
* 或删除未使用的 DSL。

禁止继续只靠示例测试证明架构存在。

## 17.5 依赖方向门禁

建议固定方向：

* Kernel
* Domain projections and identities
* Structured programs
* Host ports
* Host adapters/codecs
* Plugin composition

下层不得反向引用 Plugin、Fable host 或 Tool Surface。

## 17.6 重复算法门禁

静态检查以下算法只能存在一个实现：

* modulo-4 cursor advance。
* EffectiveAgent side mapping。
* Agent peer mapping。
* canonical provider-visible hash。
* Review witness confirmation。
* Prompt origin priority。

## 17.7 近门禁告警

* 超过 260 行：架构告警，要求记录拆分理由。
* 超过 280 行：PR 阻断，除非文件位于批准的 codec allowlist。
* 超过 300 行：硬阻断。

这能避免所有文件长期贴着 300 行生长。

---

# 十八、实施顺序（并行化重构版）

## Phase 0：建立架构真相（基线冻结，可并行准备）

任务：

* 决定并落实 Flow DSL 的生产使用。
* 增加新架构门禁。
* 建立模块引用图与无调用文件清单。
* 冻结当前 E2E 作为行为基线。

并行子流：

* A：生成依赖图（模块引用 / 反向引用）
* B：扫描 dead code / orphan modules
* C：E2E snapshot freeze（测试与回放基线）

出口：

* 不再新增机械拆分文件。
* DSL 不再是仅测试架构。
* 所有候选删除项有明确结论。

---

## Phase 1：收敛正确性关键路径（可拆三条并行线）

任务：

* Fallback 单一 cursor。
* Prompt Authority 四模块。
* Host Event Codec。
* Reconcile 三层拆分。

并行子流：

* A（Fallback线）

  * 单一 cursor 收敛
  * retry 写入口统一

* B（Authority线）

  * Prompt Authority 四模块拆分
  * Dispatcher 唯一出口

* C（Host/Reconcile线）

  * Host Event Codec
  * Reconcile pipeline 三层拆分

出口：

* retry 唯一写 cursor。
* Dispatcher 唯一发 synthetic prompt。
* raw Host obj 不进入业务层。
* Unknown 无副作用。

---

## Phase 2：重构 ChildRun 与 Review（双轨并行）

任务：

* ChildRun 聚合。
* ForkRuntime completion mailbox。
* 内部明确 completion handle。
* Review Witness 模型。
* 双 PERFECT 局部递归。

并行子流：

* A（ChildRun runtime线）

  * completion mailbox
  * fork/join 生命周期收敛

* B（Review线）

  * witness model
  * PERFECT 双确认递归逻辑

出口：

* 无 Ready/Finished 镜像状态。
* 无 generic join stash。
* 无字符串 guard 推断。
* Busy nudge 保持同 Run。

---

## Phase 3：重构 Companion（状态与投影分离并行）

任务：

* 单一 CompanionState。
* Projection、PrefixEpoch、BloggerHost 分离。
* in-flight 单一真相。

并行子流：

* A（State收敛）

  * busy/reset/inFlight 合并

* B（Projection线）

  * B / delta / epoch 逻辑拆分

* C（BloggerHost线）

  * session复用与恢复逻辑

出口：

* 无双重 busy/reset。
* FrozenB 和 LatestB 边界清楚。
* cache E2E 不退化。

---

## Phase 4：Process 与 PTY（系统资源并行重构）

任务：

* Process lifecycle 垂直拆分。
* 单一 Deadline。
* PtySession / PtySupervisor。
* JS host 隔离。

并行子流：

* A（Process线）

  * runner / output / deadline 拆分

* B（PTY线）

  * session 聚合
  * supervisor ownership

* C（Host隔离线）

  * JS interop 边界收敛

出口：

* 一个进程主程序。
* 一个 PTY 聚合。
* 一个 completion 写入口。
* parent cancellation 资源化。

---

## Phase 5：Tools、Orchestrator、Journal（垂直切片 + 流程重建并行）

任务：

* 工具按 DSL 动词垂直切片。
* OrchestratorProgram。
* IntegrationGate。
* Journal bounded projections。

并行子流：

* A（Tools线）

  * Fork/Join/List/PTY/Executor 切片

* B（Orchestrator线）

  * publish pipeline 重写为单程序

* C（Journal线）

  * projection bounded context 拆分

出口：

* Manager/Orchestrator 工具面静态可见。
* 发布链一分钟可读。
* 无 FoldHelpers、ToolSurfaceFields、PublishStages 式机械层。

---

## Phase 6：删除兼容与死代码（全局收敛阶段，可分批并行清理）

任务：

* 删除所有已替代路径。
* 删除无生产引用的架构样板。
* 更新 fsproj。
* 更新文档与迁移说明。

并行子流：

* A（dead code removal）
* B（fsproj / build cleanup）
* C（文档与迁移说明）

出口：

* 一个行为只有一个实现。
* 一个事实只有一个拥有者。
* 一个主流程只有一个顺序程序。
* 所有生产文件低于 300 行。

---

# 十九、每个重构 PR 的验收模板

每个 PR 必须回答：

1. 本 PR 删除了哪个重复概念或事实来源？
2. 新文件拥有哪个完整不变量或生命周期？
3. 为什么该边界不会要求调用者同时打开另一个文件？
4. 是否引入了新的 mutable flag、HashSet 或 registry？
5. 这些状态是物理事实还是程序计数器？
6. 主流程是否比之前更连续可读？
7. 是否减少了 Host `obj` 向领域层的泄漏？
8. 是否保持所有冻结语义？
9. 是否删除了被替代的旧路径？
10. 文件虽然低于 300 行，是否只是换了一种机械拆分？

无法清楚回答第 2、3、5、9 项的 PR，不应合并。

---

# 二十、最终验收标准

重构完成后，打开生产主流程，读者应能在一分钟内看清：

* Manager 如何 fork、join 和 nudge。
* Child run 如何从 prompt acceptance 走到唯一 completion。
* Reviewer 如何得到两份绑定同一 Git tree 的 PERFECT witness。
* Fallback 如何仅由 retry 推进 A/A/B/B cursor。
* Companion 如何从投影产生 delta、运行 Blogger 并更新 B。
* Process 如何 spawn、等待、kill 和 cleanup。
* PTY 谁拥有 handle、buffer 和 exit completion。
* Orchestrator 如何从 worktree 到 rebase、复审和 FF publish。
* Prompt Authority 如何 claim、发送、accept 或 abandon。

同时必须满足：

* 无 Stage/Phase/Lease/Owner/Generation 式程序计数器。
* 无按文本猜测 Authority、Guard 或 continuation。
* 无重复 Fallback 算法。
* 无多个模块共同维护同一资源的 busy/finished/reset 状态。
* 无业务层 Host 动态对象。
* 无仅为转发而存在的 Service/Core/Helpers。
* Flow DSL 必须真实运行于生产，并作为唯一主流程编排机制，不得被绕过或降级为示例或测试专用。
* 300 行门禁继续保持，且多数核心文件稳定在 120–260 行区间。
* 文件总数是否下降不是首要指标；跨文件阅读路径、重复事实源和隐式协议必须显著下降。

这才是“保持 300 行门禁，但拆分要动脑筋”的最终含义。

# Host 集成 — 可观察行为

条款前缀：`HOST-`。  
Transport / Session / 多实例边界见 `shape/host.md`。  
Reconciler、Transform 绑定、compaction 程序见 `how/host.md`。

## HOST-001：事件分层

业务层不得消费流式碎片。合法路径只有：

```text
碎片事件 → 最早边界丢弃
粗粒度信号（idle / retry / deleted）→ single-flight → SDK 完整消息 → 纯策略
```

## HOST-002：允许进入业务层的信号

仅下列信号可进入 Session 生命周期与 Reconciler：

```text
session.status = idle
session.status = retry
session.deleted
```

`chat.message` 通常只走 Prompt acknowledgement，不得拼装 terminal turn。唯一额外用途是
PROMPT-012 的 Student HumanRoot bootstrap：在 Host 保存消息、调用 provider 前同步创建 QA 并写入原文；
该 hook 不从正文判断意图，只认已解析的显式 Student Agent 与 Authority Root 身份。

## HOST-005：XTrace 是唯一原始语义轨迹

X 的 lifecycle 语义轨迹是 append-only 的 `XTrace`（COMPANION-003）：

- 包含：Host 可见 prompt、assistant 正文、host-visible reasoning、tool call/result、omission marker  
- 不包含：UI delta、usage、cost、timestamp、directory、finish reason、runtime ID  
- Cursor 在 lifecycle 内严格单调，独立于 Host transcript 数组下标  
- provenance 按 provider run 分段（因 Fallback / Agent 切换），不用单值 Model  

Host compaction **不得删除** XTrace：否则 Y 落后补缺口与 LWR 自包含同时失效。compaction 只重锚 PrefixCoverage（HOST-006）。

旧术语 `TerminalSessionA` / `ARecord` 废止；由 XTrace + OpeningPromptRaw + TerminalOutputRaw 取代。

## HOST-006：Compaction — 预防与收容（行为）

产品上下文恢复的**唯一**合法机制是失败驱动协议（`what/context.md`）。Host compaction 不得充当恢复失败或容量信号：不得生成 BlogFrame / FrozenRecordPrefix / Authority / Continuation，不得推进 Fallback cursor。唯一允许的收容语义是 `ContextReanchored`：`PrefixEpochId+1`、`Snapshot=None`、PrefixCoverage 归零，用于使旧缓存证明失效；它不选择压缩内容，也不替代失败驱动恢复。

必须同时存在两层：

| 层 | 必须成立的行为 |
|----|----------------|
| 预防 | automatic / overflow / autocontinue / prune 类行为关闭；无法证明关闭 → 启动失败（`HostContractUnsupported`） |
| 收容 | 任意观察到的 compaction pseudo-run → 原子重锚（`ContextReanchored`） |

收容不区分 manual `/compact` 与 Host 自触发：观察面相同，处置相同（与 CTX-005 同构）。

重锚后的**保证边界**（best effort，不假装更强）：

- 不保证 busy skip 期间未消化内容仍在 B  
- 不保证前缀缓存连续（epoch 冷边界）  
- 不保证 Host 摘要质量（摘要不进 FrozenRecordPrefix）  
- 不保证重锚后立即能 probe  

程序细节见 `how/host.md`。

## HOST-007：日志不是恢复协议

日志只记诊断（session、role、handle、operation、result、error、bytes/duration/tree hash）。  
禁止写 stage / phase / owner / lease / generation / next_action 充当控制状态。

## HOST-013：结对编程 marker（行为）

对**非 Companion / 非 Blogger** 的 provider transcript，每次 transform 必须插入一组 synthetic `auto-injected` pair：assistant tool-call + 使用同一 `callID` 的 completed tool-result。tool-call 输入为 `{}`；tool-result 正文有最近 prior tip 时为英文 Nudge、空行、`ProjectionConstants.PairProgrammingGuidelineText`，否则仅为该中文正文。

**位置**：本次 pair **总在 trailing user message 之前**，禁止落在 trailing user 之后或全局末尾。无 trailing user 时 pair 落在全局末尾（空历史同样有效）。

**多 tool 批**：若 trailing user 前存在同轮 tool-call / tool-result 批，则：
- `auto-injected` tool-call 排在该批 **tool-call 末尾**（`tool1, tool2, auto-injected`）；
- `auto-injected` tool-result 排在该批 **tool-result 末尾**（`result1, result2, result-auto-injected`）；
- 其后才是 trailing user（若有 steer：`… result-auto-injected, user`）。
无同批 tool 时，pair 的 call 与 result 相邻，整体仍在 trailing user 前。

它**是**会影响 prompt bytes、Prefix Cache、ReviewSeal 的合成历史；**不是**私有思维、容量估算或通用恢复信号。

**范围排除**：`ManagedSessionKind.SatelliteSession(_, Companion)`（Blogger）的 transform **禁止**注入、恢复或追加任何 auto-injected pair。Blogger 只消费 `blogger-system.md` + 工作日志 TOML；结对编程中文思考约束不得进入其 provider-facing 历史。判断依据是 durable SessionAssociation（`isCompanion`），禁止按 agent 名字猜测。

行为约束：

1. 对适用 session，每次 transform 无条件插入恰好一组完整 pair；无 user、无既有 tool-call/tool-result、空历史时同样插入。Companion / Blogger session 整段跳过，消息序列字节不变。
2. pair 一经加入即永久有效。后续每次 transform 必须按原位置、原字节恢复全部既有 pair，再把**本次** pair 插在当前 trailing user 之前（无 trailing user 则全局末尾）；禁止删除、过滤、去重、改写历史 pair，禁止复用既有 `callID`。
3. 同一 pair 共享 `callID`。无同批 tool 时 call 与 result 相邻；有同批 tool 时 call 并入 call 批末、result 并入 result 批末（此时 call/result 不必相邻）。不同 pair 的 `callID` 唯一且可稳定重建。恢复顺序与字节必须来自 durable append-only 事实，不得依赖文本识别。
4. pair 正文不得进入 XTrace / Companion decode / Blogger delta / work record / compaction input；仅 pair 的 durable 投影事实参与 HOST-013 恢复。
5. 同一 epoch 内，前次 provider-visible wire 必须是后次 wire 的稳定字节前缀；历史 pair 保留原位，本次 pair 只插入 trailing user 前，不读 limit、不做 token 估算（CTX-002）。

构造与链序见 `how/host.md`。

## HOST-014：Student / Teacher Host 行为

匹配生产依赖的 OpenCode `v1.18.14` 必须通过下列 source + runtime canary：

1. `chat.message` 在用户消息保存与 provider effect 前完成，允许 PERSIST-011 先落盘。
2. Prompt `tools` 被完整写为 Session permission；每个 provider step 由 Agent + Session permission
   裁剪 provider-visible schema，并在执行时按同一 ruleset ask/deny。
3. 普通 tool result 后同一 Host loop 会继续到 Assistant completion；Student `return` 可先删除 QA，
   再把其 message 约束为用户最终回复。
4. Teacher `return` 的普通 tool result 使同一 Host loop 继续到一个固定 Assistant completion；该 completion
   正常结束并被 reconcile 后才完成等待中的父 `teacher` 工具。成功路径不得 abort、不得显示
   `interrupted`，也不 retire Session；下一问题继续同一 Session。
5. idle 只作 wake；Student/Teacher 策略必须从完整 snapshot、request profile 与 Satellite 关联决定 nudge。

任一 canary 失败 → `HostContractUnsupported`，Student 功能 fail closed；不得影响其它 Agent。

## HOST-015：宿主 Session 树扁平，儿子的儿子是儿子

任何 Managed child Session（fork child、one-shot child、Companion Blogger、Student↔Teacher 的
Teacher）的 Host 物理 parent 恒为 family root：儿子再创建儿子时，新 child 物理重挂到 root 名下。
Host 树深度恒为 2（root → child），不存在孙子。

理由：UI 只渲染两层树；孙子在界面上不可见，等于脱管 Session。

归属关系不由物理 parentID 承载：fork↔child、Work↔Companion、Student↔Teacher 关系只由 durable
journal 事实（HandleLinked / CompanionBloggerLinked / StudentTeacherLinked）证明。恢复时按 journal
关联的 SessionId + agent + title 精确匹配；无 journal 关联则一律新建，不得按物理 parentID 推断
归属、不得收养同 root 下他人的 child。

## HOST-016：空 Content 预防（行为）

在 `experimental.chat.messages.transform` 交付给上游 provider 之前，对所有历史消息执行 content 兜底保障：
无 `tool_calls` 的 `assistant` 消息，若无 text part（例如仅包含 reasoning/thinking）或 text 为空，必须以其 reasoning/thinking 文本（或默认占位）填充 text part，禁止向上游发送空 content 的 assistant 消息；
`user` 消息若无 text part 或 text 为空，同样填充非空 text 兜底，防止上游 API 报 `messages[i].content cannot be empty` 400 错误。

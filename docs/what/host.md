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
session.error = MessageAbortedError | AbortError
session.deleted
```

abort error 必须解码为 typed `AttemptAborted`，撤销当前 attempt 的全部 idle-derived continuation capability；它不是 `ProviderFailure`，不得推进 fallback。

`finish=None` 的稳定 snapshot 分类为 reconciliation 私有观测 `TurnUnknown`（`SnapshotObservation`），**不是**可 publish 的 `TurnOutcome` case（HOST-004）。

`chat.message` 通常只走 Prompt acknowledgement，不得拼装 terminal turn。额外用途只认结构身份：无 PromptKey 的真实外部用户消息 signal 当前 active `JoinAttempt`，零 active attempt 时不留下 future join wake（EXEC-017）。不从正文判断意图。历史 PROMPT-012 Student HumanRoot / QA bootstrap：**G3 已删除（absent）**。

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

对**非 Companion / 非 Blogger** 的 provider transcript，每个尚未存在 HOST-013 synthetic bracket 的真实 provider/tool exchange（placement occasion）恰好产生一组 synthetic `auto-injected` pair：assistant tool-call + 使用同一 `callID` 的 completed tool-result。tool-call 输入为 `{}`；tool-result 正文有最近 prior tip 时为英文 Nudge、空行、`ProjectionConstants.PairProgrammingGuidelineText`，否则仅为该中文正文。

**Bracket 结构**（规范，不是示意图）。synthetic pair 不是相邻的两条消息，而是跨越真实 response batch 的 temporal bracket。规范序列：

```text
LLM -> Local:
Req1 Req2

Local -> LLM:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1

LLM -> Local:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1 Req3

Local -> LLM:
Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1 Req3 FakeReq2 Resp3 FakeResp2
```

其中 `ReqN` / `RespN` 为真实 tool-call / tool-result，`FakeReqN` / `FakeRespN` 为同一 `callID` 的 synthetic call / result。局部结构恒为：

```text
real calls → synthetic call → real results → synthetic result
```

禁止：

```text
real history → synthetic call → synthetic result
```

更禁止每次 transform 删除全部历史 synthetic 后把历史 pair 整块重建到当前 insertion point。

**位置**：新 pair 的 `CallGap` / `ResultGap` 只由当前**真实**消息末端结构决定（synthetic 消息不参与判断）。trailing user = 最后一条消息是 user message：

- 末端存在同轮 tool batch（`Req1 Req2 Resp1 Resp2` 或 `Req1 Req2 Resp1 Resp2 [User]`）：`CallGap = After(Req2)`、`ResultGap = After(Resp2)`，渲染 `Req1 Req2 FakeReq Resp1 Resp2 FakeResp [User]`；
- 无 tool batch 且有 trailing user：`CallGap = Before(U1)`、`ResultGap = Before(U1)`；
- 空 transcript：`Start` / `Start`；
- 无 trailing user（含末尾为 assistant 文本的 continuation transcript）：`After(lastReal)` / `After(lastReal)`。

新 pair 的 gap 必须落在本次追加区（末尾）。旧「pair 总在最后一条 user 任意位置之前」规则在 continuation transcript 上会把新 pair 插进已发送 wire 的中间，破坏 append-only prefix——已废弃。

同一 gap 内排序固定：pair ordinal 升序，同 ordinal 时 call 先于 result。禁止依赖 Map 枚举顺序。

**幂等**：同一 placement occasion 的重复 transform 只 replay 既有 bracket，不 append 新事实、不新增 pair。同一 placement identity（SessionId + CallGap + ResultGap）最多一个 `PairProgrammingGuideline`。

它**是**会影响 prompt bytes、Prefix Cache、ReviewSeal 的合成历史；**不是**私有思维、容量估算或通用恢复信号。

**范围排除**：`AttachmentKind.Companion`（Blogger）的 transform **禁止**注入、恢复或追加任何
auto-injected pair。Blogger 只消费 `blogger-system.md` + 工作日志 TOML；结对编程中文思考约束不得进入其
provider-facing 历史。判断依据是 durable SessionAssociation（`Ownership = Attached(_, Companion)` /
`isCompanion`），禁止按 agent 名字猜测。`SessionExecutionClass.Work`（含 Attached SyncInspector/SyncCoder）
仍进入 HOST-013；InternalLeaf Bookkeeper 同 Companion 排除。

**注入旁路**：下列任一成立时，非 Companion session 也不再追加新的 auto-injected pair；已落盘的历史 pair 仍按 durable gap anchor replay，以保持 append-only prefix：
- 进程环境 `WANXIANGSHU_SKIP_AUTO_INJECTED=1`；
- 当前 transcript 的 provider 为 `cursor`（取自消息 `info.providerID` 或 `info.model.providerID` 的最近一条）。
未命中旁路时行为不变。

行为约束：

1. 每个尚未存在 HOST-013 synthetic bracket 的真实 placement occasion 恰好产生一组 pair；同一 occasion 的重复 transform 只 replay，不再新增。Companion / Blogger session 整段跳过，消息序列字节不变。
2. pair 一经加入即永久有效。后续每次 transform 必须按 durable gap anchor 原位置、原字节恢复全部既有 synthetic half，再把本次 pair 按其 gap anchor 渲染；禁止删除、过滤、去重、改写历史 pair，禁止复用既有 `callID`。历史 synthetic half 的位置只由它自己 durable 的 gap anchor 决定，不得由当前 trailing user 或当前 tool batch 重新决定。
3. 同一 pair 共享 `callID`，但两个 half 各自拥有独立 transcript placement（共同 identity ≠ 相邻存储）。不同 pair 的 `callID` 唯一且可稳定重建。恢复顺序与字节必须来自 durable append-only 事实，不得依赖文本识别。
4. pair 正文不得进入 XTrace / Companion decode / Blogger delta / work record / compaction input；仅 pair 的 durable 投影事实参与 HOST-013 恢复。
5. 同一 epoch 内，前次 provider-visible wire 必须是后次 wire 的稳定字节前缀，权威判定为 `ProviderProjection.isAppendOnlyPrefix`；历史 pair 保留原位，不读 limit、不做 token 估算（CTX-002）。禁止用 PrefixEpoch 切换掩盖 HOST-013 自己造成的前缀漂移。
6. durable anchor 引用的真实消息在当前真实 view 中缺失时，该 historical pair 不参与本次渲染（禁止重定位到“最接近位置 / trailing user 前 / 末尾”）。XWire prefix probe 的 DropLeading 会合法移除已覆盖前缀上的 anchor；不得因此 AbortSession 或破坏 recovery slot。durable fact 保留；完整 transcript 回来后按 anchor 再 replay。
7. legacy 无 anchor 的 `PairProgrammingGuidelineAppended` 存在时该 session 不允许继续 HOST-013 replay，fail closed（incompatible journal）；禁止把旧 ordinal 近似为第 N 个 tool batch 的启发式迁移。

构造与链序见 `how/host.md`。

## HOST-014：（空缺）Student / Teacher Host 行为 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 Student/Teacher Host canary、QA bootstrap、`teacher` 工具双 await、
Learn/Compile idle nudge 与 `StudentTeacherLinked`。无 alias、无 deprecated Host 路径。后继：SyncDelegate
Inspector/Coder（HOST-008 / EXEC-026/028）；`return` **仅** SyncDelegate，无 StudentTeacher fallthrough。

## HOST-015：宿主 Session 树扁平，儿子的儿子是儿子

任何 Managed child Session（fork child、one-shot child、Companion Blogger、SyncInspector/SyncCoder、
Bookkeeper）的 Host 物理 parent 恒为 family root：儿子再创建
儿子时，新 child 物理重挂到 root 名下。Host 树深度恒为 2（root → child），不存在孙子。

理由：UI 只渲染两层树；孙子在界面上不可见，等于脱管 Session。

归属关系不由物理 parentID 承载：fork↔child、Work↔Companion、Work↔Sync*、Work↔Bookkeeper
关系只由 durable journal 事实（HandleLinked / CompanionBloggerLinked /
SyncDelegate 关联）与 HOST-008 的 `SessionOwnership` 证明。历史 `StudentTeacherLinked`：**G3 gone**，
不得当作现行关联。恢复时按 journal
关联的 SessionId + agent + title 精确匹配；无 journal 关联则一律新建，不得按物理 parentID 推断
归属、不得收养同 root 下他人的 child。查询失败、重复候选或归属冲突 → fail closed。

逻辑上 Work+Attached（SyncInspector/SyncCoder）仍可再挂 InternalLeaf Companion（HOST-008）；
物理上该 Companion 同样挂 family root，不形成 Host 孙子。

## HOST-016：空 Content 预防（行为）

在 `experimental.chat.messages.transform` 交付给上游 provider 之前，对所有历史消息执行 content 兜底保障：
无 `tool_calls` 的 `assistant` 消息，若无 text part（例如仅包含 reasoning/thinking）或 text 为空，必须以其 reasoning/thinking 文本（或默认占位）填充 text part，禁止向上游发送空 content 的 assistant 消息；
`user` 消息若无 text part 或 text 为空，同样填充非空 text 兜底，防止上游 API 报 `messages[i].content cannot be empty` 400 错误。

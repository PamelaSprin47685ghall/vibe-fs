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

旧术语 `TerminalSessionA` / `ARecord` / `OpeningPromptRaw` 废止；由 XTrace + OpeningMaterial（preserved Opening 语义区间）+ TerminalOutputRaw 取代。

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

对**非 Companion / 非 Blogger** 的 provider transcript，每个尚未存在 HOST-013 occurrence 的真实 provider/tool exchange（placement occasion）恰好产生一个 durable `PairProgrammingGuideline` occurrence。Occurrence 的语义正文、稳定 `callID`、`CallGap` 与 `ResultGap` 与 provider 无关；provider renderer 只决定同一 occurrence 在 wire 上采用何种合法形状。

普通 provider 把 occurrence 渲染为一条 Host assistant 消息：completed `auto-injected` tool part（输入 `{}`、输出 `MarkerText`）。OpenCode `MessageV2.toModelMessagesEffect` 把一条 completed `type=tool` 展开成 provider 可见的 tool-call + tool-result。禁止用 `pending`/`running` 表示 FakeReq：Host 会把它收成 `[Tool execution was interrupted]`。

**可执行 entity**：名为 `auto-injected` 的工具必须作为真实 OpenCode `Tool.Def` 存在（空参数）。非 Blogger / 非 Distiller 的 Work 角色 Host permission 对其 allow。模型若发出 live call，execute 恒返回 `OK`，不得因缺 entity 把 LLM 请求打失败。HOST-013 synthetic pair 仍是注入时已 completed 的历史，不经过 execute。Strength replica 与 Bookkeeper 保持既有全拒 / 仅 `js-bookkeeper` 闸门。

Cursor **不得**发送 fake tool，也不得新增 synthetic message/part。仅当同一 occurrence 的 `ResultGap` 位于真实 terminal tool result 之后时，Cursor renderer 克隆该真实 result，把 provider-visible 终态文本精确投影为 `original + NUL + BOM + MarkerText`；不加 frame/header/footer。无真实 terminal result 可附着时该 occurrence 在 Cursor wire 上零字节投影，但 durable occurrence 仍存在。普通→Cursor→普通 provider transition 必须重用同一 durable occurrence：Cursor 不删除 `CallGap`，也不修改 Host raw transcript，反向投影仍可恢复原 fake-tool pair。

Pair Hint 正文是一个 canonical semantic payload，至少同时要求：简体中文思考纪律；把 `[NEEDHELP]` 视为正常、可早用的协作请求；以及在每次工具 turn 前寻找完整 parallel wave——当前已知、确有用且彼此独立的调用默认在同一 assistant turn 一起发出，以最小化 provider↔tool RTT。仅真实数据依赖、共享可变 owner、协议顺序、破坏性干扰或明确有限容量可以序列化相应边；不得猜未知参数、制造/重复无用调用，也不得写死全局并发数字。此语义不按 provider 复制。

tool-result 正文语言由不可变 `SessionProviderLanguage`（HOST-026）决定：English 或 SimplifiedChinese 各有一份 guideline 资源；有最近 prior tip 时在 guideline 前附加同语言 Nudge。  
`SessionStartedAt` 在 session 创建时绑定一次并 durable；restart / fallback / Strength 不得改写；经 `IClockPort` 计量，不碰 ambient `UtcNow`。  
每条**新** marker 携带一次 wall-clock 采样：`SessionStartedAt → now` 转为人类尺度（`N minutes M seconds` / `N 分 M 秒`），写入 durable `MarkerText`。  
历史 marker **永不**因 replay / compaction / reanchor 重算 elapsed——只重放已存字节，以保持 append-only 前缀缓存（ARCH-004）。新 marker 只携带当下一次采样。

**Host 编码**。ordinary wire 每个 occurrence 恰好一条 completed Host 行，落在 `ResultGap`。`CallGap` 仍写入 durable placement identity（并供 Cursor → ordinary 可逆），但不另渲染 pending/running 行。OpenCode 将该 completed 行展开为 FakeReq + FakeResp。

**Provider 可见序列**（completed Host 行被 OpenCode 展开后）：

```text
LLM -> Local:
Req1 Req2

Local -> LLM:
Req1 Req2 Resp1 Resp2 FakeReq1 FakeResp1

LLM -> Local:
Req1 Req2 Resp1 Resp2 FakeReq1 FakeResp1 Req3

Local -> LLM:
Req1 Req2 Resp1 Resp2 FakeReq1 FakeResp1 Req3 Resp3 FakeReq2 FakeResp2
```

其中 `ReqN` / `RespN` 为真实 tool-call / tool-result，`FakeReqN` / `FakeRespN` 为同一 `callID` 的 synthetic call / result。局部结构恒为真实 batch 之后紧跟一对 FakeReq+FakeResp。禁止用 Host `pending` 把 FakeReq 插进真实 call 批。

禁止每次 transform 删除全部历史 synthetic 后把历史 pair 整块重建到当前 insertion point。

**位置**：新 pair 的 `CallGap` / `ResultGap` 只由当前**真实**消息末端结构决定（synthetic 消息不参与判断）。trailing user = 最后一条消息是 user message：

- 末端存在同轮 tool batch（`Req1 Req2 Resp1 Resp2` 或 `Req1 Req2 Resp1 Resp2 [User]`）：`CallGap = After(Req2)`、`ResultGap = After(Resp2)`，ordinary 渲染 `Req1 Req2 Resp1 Resp2 FakePair [User]`（FakePair = 一条 completed Host 行 → FakeReq+FakeResp）；
- 无 tool batch 且有 trailing user：`CallGap = Before(U1)`、`ResultGap = Before(U1)`；
- 空 transcript：`Start` / `Start`；
- 无 trailing user（含末尾为 assistant 文本的 continuation transcript）：`After(lastReal)` / `After(lastReal)`。

新 pair 的 gap 必须落在本次追加区（末尾）。旧「pair 总在最后一条 user 任意位置之前」规则在 continuation transcript 上会把新 pair 插进已发送 wire 的中间，破坏 append-only prefix——已废弃。

同一 gap 内排序固定：pair ordinal 升序。禁止依赖 Map 枚举顺序。

**幂等**：同一 placement occasion 的重复 transform 只 replay 既有 bracket，不 append 新事实、不新增 pair。同一 placement identity（SessionId + CallGap + ResultGap）最多一个 `PairProgrammingGuideline`。

它**是**会影响 prompt bytes、Prefix Cache、ReviewSeal 的合成历史；**不是**私有思维、容量估算或通用恢复信号。

**范围排除**：`AttachmentKind.Companion`（Blogger）的 transform **禁止**注入、恢复或追加任何
auto-injected pair。Blogger 只消费 Blogger Role Law（PromptResources 组合）+ 工作日志 TOML；结对编程思考约束不得进入其
provider-facing 历史。判断依据是 durable SessionAssociation（`Ownership = Attached(_, Companion)` /
`isCompanion`），禁止按 agent 名字猜测。`SessionExecutionClass.Work`（含 Attached SyncInspector/SyncCoder）
仍进入 HOST-013；InternalLeaf Bookkeeper 同 Companion 排除。

**注入旁路**：仅当进程环境 `WANXIANGSHU_SKIP_AUTO_INJECTED=1` 时，非 Companion session 不再追加新的 occurrence；已落盘历史仍按当前 provider 的 renderer replay，以保持可逆历史。Cursor 不是旁路：Cursor 仍创建/恢复同一 durable occurrence；其 renderer 只允许真实 terminal tool-result suffix 投影，禁止 synthetic role/message/part。

行为约束：

1. 每个尚未存在 HOST-013 synthetic bracket 的真实 placement occasion 恰好产生一组 pair；同一 occasion 的重复 transform 只 replay，不再新增。Companion / Blogger session 整段跳过，消息序列字节不变。
2. pair 一经加入即永久有效。普通 provider 后续每次 transform 必须按 durable gap anchor 原位置、原字节恢复全部既有 synthetic half，再把本次 pair 按其 gap anchor 渲染；Cursor 对每个可附着 occurrence 重放完全相同的 `NUL + BOM + MarkerText` suffix。禁止删除、过滤、去重、改写历史 pair，禁止复用既有 `callID`。历史位置只由 durable gap anchor 决定，不得由当前 trailing user 或当前 tool batch 重新决定；Cursor 无可附着 result 时只能零字节，不得重定位。
3. 同一 pair 共享 `callID`，但两个 half 各自拥有独立 transcript placement（共同 identity ≠ 相邻存储）。不同 pair 的 `callID` 唯一且可稳定重建。恢复顺序与字节必须来自 durable append-only 事实，不得依赖文本识别。
4. pair 正文不得进入 XTrace / Companion decode / Blogger delta / work record / compaction input；仅 pair 的 durable 投影事实参与 HOST-013 恢复。
5. 同一 epoch 内，前次 provider-visible wire 必须是后次 wire 的稳定字节前缀，权威判定为 `ProviderProjection.isAppendOnlyPrefix`；历史 pair 保留原位，不读 limit、不做 token 估算（CTX-002）。禁止用 PrefixEpoch 切换掩盖 HOST-013 自己造成的前缀漂移。
6. durable anchor 引用的真实消息在当前真实 view 中缺失时，该 historical pair 不参与本次渲染（禁止重定位到“最接近位置 / trailing user 前 / 末尾”）。XWire prefix probe 的 DropLeading 会合法移除已覆盖前缀上的 anchor；不得因此 AbortSession 或破坏 recovery slot。durable fact 保留；完整 transcript 回来后按 anchor 再 replay。
7. legacy 无 anchor 的 `PairProgrammingGuidelineAppended` 存在时该 session 不允许继续 HOST-013 replay，fail closed（incompatible journal）；禁止把旧 ordinal 近似为第 N 个 tool batch 的启发式迁移。

构造与链序见 `how/host.md`。

## HOST-027：`[NEEDHELP]` reasoning assistance sensor

Host 只从 managed Work provider attempt 的 reasoning/thinking delta 识别精确 sentinel `[NEEDHELP]`。检测器必须能跨 delta 边界拼出 sentinel，但只保留有限 rolling suffix；visible text、tool output、synthetic Pair Hint 与历史 transcript 中的同字节不得触发。每个 `(SessionId, ProviderRunIdentity)` 最多触发一次。

命中后 Host 原子 arm assistance occasion 并中止当前物理 attempt，让既有 reconciliation 观察 Abort。该 Abort 的 owner 是 assistance：**不得**进入 ProviderFailure/LoopKill、不得推进 FallbackCursor、不得增加 consecutive failure 或 retry budget。普通用户/cleanup abort 与 loop abort 的语义保持不变。Host event 若无法证明 reasoning delta 字段，则不得把 visible-text 扫描伪装成等价实现；降级只能显式、可观测并由 proof 标明。

## HOST-014：（空缺）Student / Teacher Host 行为 — G3 已删除

**编号永久空缺。** G3 clean-break 删除 Student/Teacher Host canary、QA bootstrap、`teacher` 工具双 await、
Learn/Compile idle nudge 与 `StudentTeacherLinked`。无 alias、无 deprecated Host 路径。后继：SyncDelegate
Inspector/Coder（HOST-008 / EXEC-026/028）。独立 `return` 工具通道已删除；无 StudentTeacher fallthrough。

## HOST-015：宿主 Session 树扁平，儿子的儿子是儿子

任何 Managed child Session（fork child、one-shot child、Companion Blogger、SyncInspector/SyncCoder、
Bookkeeper、StrengthReplica）的 Host 物理 parent 恒为 family root：儿子再创建
儿子时，新 child 物理重挂到 root 名下。Host 树深度恒为 2（root → child），不存在孙子。

理由：UI 只渲染两层树；孙子在界面上不可见，等于脱管 Session。

归属关系不由物理 parentID 承载：fork↔child、Work↔Companion、Work↔Sync*、Work↔Bookkeeper、
Work↔StrengthReplica
关系只由 durable journal 事实（HandleLinked / CompanionBloggerLinked /
SyncDelegate 关联 / StrengthReplica attachment）与 HOST-008 的 `SessionOwnership` 证明。历史 `StudentTeacherLinked`：**G3 gone**，
不得当作现行关联。恢复时按 journal
关联的 SessionId + agent + title 精确匹配；无 journal 关联则一律新建，不得按物理 parentID 推断
归属、不得收养同 root 下他人的 child。查询失败、重复候选或归属冲突 → fail closed。

逻辑上 Work+Attached（SyncInspector/SyncCoder）仍可再挂 InternalLeaf Companion（HOST-008）；
物理上该 Companion 同样挂 family root，不形成 Host 孙子。

## HOST-016：空 Content 预防（行为）

在 `experimental.chat.messages.transform` 交付给上游 provider 之前，对所有历史消息执行 content 兜底保障：
无 `tool_calls` 的 `assistant` 消息，若无 text part（例如仅包含 reasoning/thinking）或 text 为空，必须以其 reasoning/thinking 文本（或默认占位）填充 text part，禁止向上游发送空 content 的 assistant 消息；
`user` 消息若无 text part 或 text 为空，同样填充非空 text 兜底，防止上游 API 报 `messages[i].content cannot be empty` 400 错误。

## HOST-017：Magic Todo V1 membrane 总行为

对 Magic-Todo-enabled Manager 的 `todowrite`，Host **不**改 OpenCode 本体、**不**用同名 plugin tool 覆盖 builtin executor。合法路径只有 V1 三钩子 overlay：

```text
tool.definition   → provider-visible V2 schema（TODO-002）
tool.execute.before → 捕获 live args + 原地 compatibility 投影 + 启动 deferred prepare（TODO-004/007）
tool.execute.after  → physical-success Accepted + ensureReview + 富化 result（TODO-006/012）
```

原 Host `todowrite` executor = **compatibility sink** only：写入 Host TodoTable / UI 事件，不拥有 canonical todo truth（TODO-007）。canonical / settlement / review cadence / Finality drain 语义见 `what/todo.md`（TODO-*），本文件只定 Host membrane 可观察合同与 canary。

禁止：

```text
修改 Host core 以嵌入 Magic Todo
静默走无 hook 的 V2 settle（HOST-024）
靠 Host TodoTable 恢复 canonical
把 bridge / 内存 Stage 当 checkpoint（TODO-012）
```

## HOST-018：tool.definition — V2 广告与 description 边界

`tool.definition` 是 provider-visible V2 schema 的**唯一** Host 侧广告点，必须同时更新：

```text
parameters
jsonSchema
description
```

只改其中一处导致 definition 组装不一致 → fail closed（不得上线）。

description 必须覆盖 Manager 可见纪律（与 TODO-002/003/004/006/013 一致）：`kind:"existing"|"new"`、id 规则、`reviewing`、completed 门禁、持续维护 list、lag-1 消费语义、同 message 多 `todowrite` 全拒。

description **禁止**泄露隐藏编排（TODO-013）：dedicated reviewer、hidden agent/session、Finality cohort、barrier、witness、2N。Manager 只应看见过程 review 的 outcome/report 合同，不知编排身份。

definition 改的是广告 schema，**不**自动替换原 executor decode schema；故 before 必须额外挂载 V1 compatibility view（HOST-020）。该 view 不得改写 provider-visible enumerable input。

## HOST-019：pending materialization barrier + input 非别名（blocking canary）

OpenCode 会先创建 `state=pending,input={}` 的 ToolPart；`tool-call` stream event 再写最终 provider input。`tool.execute.before` 可落在两者之间，因此 **pre-before snapshot 不保证已 materialize 最终 input**。

Magic Todo 不允许把 `{}` 降级当输入，但也不让 snapshot/Journal IO 阻塞 builtin executor。before 同步阶段只做 provider args decode + 纯内存 compatibility projection，并启动 per-call **deferred prepare** 后立即返回：

```text
before live args → decode obligations → compatibility projection → executor
                  ↘ deferred prepare:
                     pending + {} → wait + reread same callID
                     materialized canonical == captured live canonical → durable prepare/admit
                     materialized canonical != captured live canonical → fail closed
                     carrier/provider run/part 变化 → fail closed

after → await deferred prepare → physical-success Accepted / enrichment
```

`TodoWritePrepared.ProviderInputDigest` 只从 materialized `ToolPart.input` 计算。executor 可以先完成 compatibility sink；若 deferred prepare 最终拒绝，after 必须失败，不能产生 `TodoWriteAccepted`。Host historical provider input 不得被 compatibility mutation 污染。

任一不成立 → **membrane 禁止上线**（HOST-019 FAIL）。禁止用 after「改回」历史 input 补救。本 canary 是 P0 blocker。

## HOST-020：before — 原地 mutation + compatibility 投影

before 输入形如 `{ tool, sessionID, callID }` + `{ args }`。executor 只观察**原地**字段 mutation；`output.args = …` 重绑定不改变本地 `args` 引用。

可观察义务（语义交叉引用，不在此重复）：

1. 同步 before：decode live provider obligations，并捕获其 canonical；在原 args 对象上定义 **non-enumerable** `todos` compatibility view，让原 V1 decoder 可读，同时 `JSON.stringify` / Host persistence 仍只见 provider `obligations`；随后启动 deferred prepare 并立即返回。
2. deferred prepare：仅 `sessionID + callID` → 完整 SDK snapshot 唯一定位 ToolPart / assistant / provider run / ordinal / XTrace range；不能唯一 → fail closed（HOST-025）。`pending + {}` 必须等待 materialize；materialized canonical 必须等于步骤 1 捕获值。
3. admission / replay：TODO-004（同 message 多不同 ToolCallId 全拒；同 ToolCallId replay 幂等且 digest 一致）。
4. 消费上一 ConsumableReview 后校验 proposed：TODO-006/003/002。
5. 校验通过即 durable `TodoWritePrepared`（尚非 checkpoint），并作为 deferred bridge 结果交给 after。

`reviewing` 的 sink 字段策略见 HOST-023。before **不等待 snapshot/Journal IO**、**不**启动 reviewer、**不**写 `TodoWriteAccepted`。

## HOST-021：after — Accepted、ensureReview、富化 result

after 仅在原 executor **物理成功返回**的 live path 进入（failure 路径不保证 after；协议不依赖 after-on-throw）。

顺序合同：

```text
1. 取 bridge 或从 Prepared + physical evidence 重建
2. ensure TodoWriteAccepted（幂等；live 或 recovery 双路径，HOST-022）
3. ensure DedicatedTodoReviewer / ensureReview（义务 TODO-006/008/010；after 不必“已跑 reviewer”才算成功）
4. desired lag-1 cutoff 可从 Accepted 链推导（提交 PrefixEpoch 不在 after；TODO-009）
5. 富化模型可见 tool result：上次 ConsumableReview 的 ProcessReviewLWR、REVISE 时 merge preview、PERFECT 时 preview 不生效提示（TODO-005/006/013）
6. cleanup bridge
7. return
```

禁止：先启动 reviewer 再 Accepted；把 Host TodoTable 已变成 Pk 误当 Accepted。富化 result 必须进入本次模型可见输出且下一 provider history 同字节（canary E）。

## HOST-022：physical-success 双路径 Accepted

`TodoWriteAccepted` 的 physical success **不得**只绑「after 运行时 ToolPart 已 completed」单一顺序：

| 路径 | 证明 |
|------|------|
| live | 原 executor 成功返回并进入 `tool.execute.after` |
| recovery | 完整 SDK snapshot 中该 call 的 ToolPart 已 `completed` |

两条路径必须收敛同一 `TodoWriteId + input digest + output digest`。仅当 `TodoWritePrepared` 存在且 live∨recovery 成立时，after/recovery 才可 ensure Accepted。Prepared + 失败/缺席/digest 不符 → 不 Accepted；即便 sink 乐观写成 Pk，下次 before 仍以 Journal canonical 覆盖 sink（TODO-007/012）。

## HOST-023：reviewing sink 决策与 reconciliation

Host TodoTable 无 stable id，字段近似 `content/status/priority/position` → **compatibility / optimistic working projection** only（TODO-007）。

**reviewing 第五态（canary D/I）**：

```text
优先：canonical reviewing → sink status "reviewing"（TodoTable / todo.updated / API / TUI 全容忍）
否则：canonical reviewing → sink "in_progress"（仅 compatibility 降级）
```

不得改 canonical status（TODO-003）。

**reconciliation**：一旦 REVISE 被消费且 canonical settlement 改变（TODO-005/007），Host TodoTable 必须幂等投影到 settled current。随后合法 `T(k+1)` Accepted 时由原 executor 再覆盖为新 Pk。该项 repair：**不**产生 checkpoint、**不**触发 process review、**不**改 canonical。

禁止：REVISE settlement 后永久留下否决的 Pk；把 sink repair 写成 checkpoint/review；用 Host TodoTable 反推后续新 Life 的 canonical seed（TODO-011）。

## HOST-024：V2 runner fail-closed

当前 Host V2 local settle **不**执行 V1 plugin tool hook membrane。生产 invariant：

```text
MagicTodo-enabled Manager Attempt
→ 必须走已证明 definition+before+after 的 execution path
```

`runner=V2` 且无等价 hook contract → **Attempt construction fail closed**（TODO-004）。禁止静默退化裸 `SessionTodo.update`。未来 V2 hook parity 须先通过同一套 HOST-019..025 canary 再解除限制；不长期维护两套 Magic Todo 语义。

## HOST-025：sessionID+callID 定位 canary（blocking）

before/after 仅有 `sessionID + callID`（HOST-011）。上线前必须证明经完整 SDK snapshot 能**唯一**定位：原 ToolPart、assistant message、provider run、ToolPart ordinal、其 XTrace range。不能唯一证明 → fail closed，membrane 不得上线。禁止用 callID 到别处猜配 messageID。

## HOST-026：SessionProviderLanguage

```text
ProviderLanguage = English | SimplifiedChinese
```

全局语言偏好只在 **session 创建瞬间**读入，绑定为不可变 `SessionProviderLanguage`。  
child / attached / InternalLeaf（含 Companion、SyncDelegate、Bookkeeper、StrengthReplica）继承 owner 或 commissioner 的语言，不得各自再读全局。  
用户事后改全局偏好 → 只影响此后新建 session；已开 Life 的 Opening / Office Library / tool 后果 / HOST-013 marker 世界语保持字节连续。

Fallback / Strength / restart / reanchor **不得**改写 `SessionProviderLanguage`。  
翻译边界：localizable = system / Role Law / Common Law / Library / tool description / consequence / hints / WorkRecord headings；invariant = tool 名、argument 名、wire field、enum literal、路径、命令、`exit_code` 等技术标识（ARCH-016 Gate C）。
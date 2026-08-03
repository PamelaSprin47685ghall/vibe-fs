# STATUS/ 全生命周期工作记录统一改造方案

> 状态：PROPOSED / 待 SSOT 决议后实施  
> 基于仓库快照：Repomix `repomix-output(133).xml`  
> 目标：把父→子、子→父、Companion、prefix replacement、terminal completion 收敛到同一个自包含记录模型，删除 A/B 双轨 wire、独立 final reply、Seed、重复 Blogger instruction 与非语义 TOML 字段。

---

## 0. 一页结论

### 0.1 新名词

不要继续叫“Y 版工作记录”。建议正式命名为：

```text
LifecycleWorkRecord（LWR）
中文：全生命周期工作记录
```

原因：最终产物不是纯 Y 内容。它由四类信息组成：

```text
LWR(X) = OpeningPromptRaw
       + CompressedMiddleFromY
       + RawGapFromX
       + TerminalOutputRaw
```

Y 只是中段压缩器；X 是统一原始语义流；LWR 才是跨 Session 传递和 `join` 返回的唯一工作记录。

### 0.2 必须冻结的核心决策

1. X 统一定义为完整、按序、可持久化的语义轨迹。有 host-visible reasoning 就包含；没有不补造。也包含对任务有语义价值的 prompt、assistant text、tool call、tool result 和 omission marker；不包含 timestamp、cost、usage、runtime ID、directory、finish reason 等运行时字段。
2. Y 只压缩 X 的中间部分。首条任务 prompt 永远不送 Y 压缩；末次 terminal output 天然不会再经过下一次 transform，也永远不补送 Y。
3. LWR 是唯一跨 Session 记录。父→子携带 LWR；子→父 `join` 返回 LWR。删除 `formalRecord`、`FinalText`、独立“最后回复”字段及 B/A 二选一分支。
4. 唯一合法的 X fallback 是补 Y 时滞缺口。不是“没有 B 就退回整个 A”，而是从 Y 的精确覆盖 cursor 到当前 X head 的有界 suffix。
5. prefix probe 与 LWR 补洞使用不同证明量。LWR gap 由 Y 的精确 ingest cursor 决定；prefix replacement 仍只能在完整 provider turn 边界执行。两者禁止再复用一个模糊的 `Coverage`。
6. 首条 prompt 原样保留的对象是“任务语义原文”，不是注入后的 transport envelope。对于 child，精确保留 fork 的原始 `prompt` 与 Reviewer 的权威 requirement 原文；`parent_work_record` 是继承上下文，不复制进 child 自己的 OpeningPrompt，否则每代 fork 都会递归嵌套父记录并指数膨胀。
7. 末条 output 原样保留，但不再作为旁路字段收集。terminal reconcile 把它作为 LWR 的 `TerminalOutputRaw` segment 提交；completion 只携带最终 LWR。
8. TOML 只负责局部 wire 包装，不定义记录内部语义。同一份 LWR bytes 在父→子和子→父中原样复用；删除空行、空字段和仅供 runtime 调试的标识。

### 0.3 目标 wire 形状

父→子首次 prompt：

```toml
# <child assignment 原文，作为指令>
# `parent_work_record` is inherited context, not part of the assignment.

parent_work_record = '''
<完全相同的 LWR bytes>
'''
```

Reviewer 额外保留权威 requirement；业务 payload 仅存在时输出。不要再强制每个 child 用固定六字段报告，除非它确实是产品级需求而非旧 wire 偶然行为。

子→父 `join` 成功结果：

```toml
status = "completed"
agent = "deep-coder"
work_record = '''
<完全相同的最终 LWR bytes>
'''
```

面向 LLM 的成功结果不再包含：

```text
final_text
formal_record
outcome（与 work_record 重复时）
work_record.digest
work_record.freshness
work_record.covered_through
agent_id
run_id
child_session_id
authority_root
provider_run
directory
tier
fallback_peer
```

这些值如仍需诊断、恢复、归属或审计，留在 typed runtime / journal / diagnostic surface，不进入 LLM-visible tool result。

---

## 1. 当前实现为什么必须整体改，而不是修一个 prompt

### 1.1 当前规范存在三套不同语义

现状同时定义：

```text
A(X) = assistant 正文 + reasoning，不含 tool raw stream
B(X) = Y assistant work-log frames
join = formalRecord(A) + workRecord(B)
child background = B 优先，否则整段 A
```

这使同一段生命周期在不同方向上呈现不同：

- 父→子收到的是 `B else A`。
- 子→父同时收到 `FinalText/A` 与可选 `WorkRecord/B`。
- X prefix replacement 使用 `CoverableB/FrozenB`。
- Blogger 自己消费的是更丰富的 semantic projection，其中含 user、reasoning、tool call/result。
- terminal output 因不会进入下一轮 transform，只进入 A，不进入 B。

因此“父给子的工作记录”和“子给父的工作记录”从定义上就不可能对称。

### 1.2 当前 wire 明确重复和泄漏内部字段

`JoinTool.fs` 当前把 `payload.FinalText` 放进 instruction comments，同时又输出：

```text
kind/status/agent_id/run_id/work_record(text,digest,freshness,covered_through)
child_session_id/authority_root/provider_run/directory
agent/role/tier/fallback_peer
```

对于父 LLM，真正需要的是“谁完成了、是否成功、完整工作记录是什么”。其余多数是 runtime 内部归属字段或重复信息。

### 1.3 Blogger normal instruction 被注入两次

当前链路同时：

1. 在 `BloggerDelta.renderChunk` 中用 `BloggerToml.renderWith [ NormalInstruction ]` 把 instruction 写进 TOML comment；
2. 在 `CompanionProjectionBuilder` 中再插入一条独立 synthetic user message，内容同样是 `NormalInstruction`。

同一命令既占 token，又制造“历史 frame / instruction / delta”三种风格混杂。

### 1.4 TOML renderer 主动制造无语义空行

`SyntheticToml.document` 当前用 `String.concat "\n\n"` 拼接 data blocks。ARCH-010 只要求 instruction header 与 data body 之间恰好一个空行，并没有要求每个字段、每个 table 之间空一行。

因此 data-only 或 mixed payload 中的大量空行是 renderer 造成的固定浪费，不是 TOML 语法需要。

### 1.5 当前 Blogger schema 有固定无效字段

现有 `BloggerDeltaPart` 固定带：

```text
turn
role
kind
tool
media_type
text
args
truncated
```

其中：

- 文档顺序已经表达顺序，`turn` 多数时候不必发给模型；
- table 名可表达 part 类型，`kind` 可删除；
- tool result 当前可被构造成 `tool = ""`；
- `truncated = false` 没有语义，应只在 true 时出现；
- 不同 part 被迫携带同一套稀疏字段，导致空字段和样式噪声。

### 1.6 `Seed` 把父历史混入 child 的生命周期

当前新 Y 把父记录作为 `Seed` frame。它不覆盖 child X turn，却会进入 child 的 LatestB，之后还可能被 squash。结果是：

- child 的“自身工作记录”包含父历史；
- 父记录在多代 fork 中反复复制；
- squash 可能把 inherited context 与 child work 混为一条不可拆分 frame；
- LWR 无法清晰回答“这一 Session 自己发生了什么”。

目标模型中应删除 Seed。父 LWR 只作为 child 的输入 context；child LWR 从 child 自己的 opening task 开始。

---

## 2. 新的规范模型

## 2.1 X：唯一原始语义轨迹

建议定义：

```fsharp
type XTracePart =
    | PromptText of role: SemanticRole * text: string
    | AssistantText of text: string
    | Reasoning of text: string
    | ToolCall of name: string * arguments: string
    | ToolResult of text: string
    | MediaOmitted of mediaType: string option

type XTraceItem =
    { Cursor: XTraceCursor
      Provenance: InternalProvenance
      Part: XTracePart }
```

规范要求：

- `XTraceCursor` 在一个 X 生命周期内严格单调，独立于 Host transcript 的临时数组下标。
- `Reasoning` 只记录 Host 明确可见的 reasoning；不可见的 hidden chain-of-thought 不抓取、不推断、不伪造。
- tool call/result 记录供工作理解所需的语义文本；二进制和图片只记录 omission marker。
- `InternalProvenance` 可保存 session/run/message/part 归属供证明和恢复使用，但 renderer 永不输出它。
- timestamp、cost、usage、finish reason、directory、runtime IDs 不属于 `XTracePart`。
- Host compaction 不删除 XTrace；否则“Y 落后时由 X 补缺口”和“完整生命周期自包含”在 compaction 后无法同时成立。

> 重要：这是对当前 A(X) 的替代，不是在 A 旁边再建第三条轨。完成迁移后应删除 A 术语与 `TerminalSessionA`。

## 2.2 OpeningPromptRaw：不可压缩的首锚点

```fsharp
type OpeningPromptRaw =
    { AssignmentText: string
      AuthoritativeRequirements: string list }
```

规则：

- 根 Human Session：精确保留被 Host 接受的第一条 HumanRoot prompt 文本。
- fork child：精确保留 `fork-agent.prompt` 原文。
- Reviewer：除 assignment 外，精确保留本次 review scope 中的原始 HumanRoot requirement 文本及顺序。
- 不 Trim 正文，不重排，不摘要，不修正语法，不 TOML round-trip。
- 允许统一换行到 LF 仅当整个仓库已把“原样”规范为 canonical LF；否则必须按接受时 bytes 保存。
- 不包含 system prompt、runtime report-format instruction、`parent_work_record`、run ID、directory 等注入内容。

为什么不能保存“完整第一条 provider-visible synthetic message”：它会把父 LWR 再复制进 child OpeningPrompt，形成递归嵌套；也会把未来可变的 transport 文案误当用户真实需求。

## 2.3 Y：只压缩中段

Y 的职责改成：

```text
输入：XTrace 中尚未覆盖、且允许压缩的连续 delta
输出：一个 dense factual work-log frame
提交：frame + 精确 XTraceCursor 原子提交
```

禁止 Y：

- 重写 OpeningPromptRaw；
- 接收或重写 terminal output；
- 接收 parent LWR 作为 Seed；
- 忽略 host-visible reasoning；
- 输出 hidden reasoning；
- 把运行时 metadata 写进 work log。

建议 system prompt 核心文案：

```text
You compress newly observed semantic work-session material into one dense,
factual work-log continuation. The opening task is preserved separately and
is not part of this request. Preserve decisions, outcomes, paths, errors,
constraints, unresolved work, and decision-relevant host-visible reasoning.
Do not invent hidden reasoning or omitted media. Do not call tools. Output only
the new work-log entry.
```

normal request 的“写一条新 entry”规则放 system prompt 一处即可。normal delta 只发数据，不再额外插入 `NormalInstruction` user message，也不再把同一 instruction 写进 TOML comment。

Squash 是不同操作，可保留一条明确的 instruction-only request；不要复用 normal delta 的重复注入方式。

## 2.4 两种 coverage 必须分开

建议把当前混合的 coverage 拆成：

```fsharp
type RecordCoverage =
    { IngestedThrough: XTraceCursor }

type PrefixCoverage =
    { HostEpochId: int64
      CutoffExclusive: int
      CoveredPrefixDigest: string
      CoverableFrameCount: int }
```

用途：

- `RecordCoverage.IngestedThrough`：决定 LWR 的 X gap 从哪里开始。它精确到 semantic part，可落在 turn 中间。
- `PrefixCoverage`：决定 X prefix probe 能否用 Y frames 替换完整 Host turn。它只能在完整 turn 边界，并受 Host compaction epoch 约束。

绝对禁止：

- 用 `CoverableTurnCutoffExclusive` 计算 LWR gap；这会在 Y 已压缩半个 turn 时重复发送。
- 用 `IngestedThrough` 直接做 prefix replacement；这可能删掉半个 provider turn。
- Host compaction 时把全生命周期 `RecordCoverage` 清零。compaction 只作废 `PrefixCoverage` 的 Host 映射；XTrace 与 Y ingest 进度仍是 durable lifecycle facts。

## 2.5 LWR：唯一物化规则

建议内部类型：

```fsharp
type LifecycleWorkRecord =
    { Opening: OpeningPromptRaw
      Frames: EffectiveBlogFrame list
      Gap: XTraceItem list
      Terminal: XTraceItem list }
```

确定性物化：

```fsharp
let materialize snapshot =
    let yCursor = snapshot.RecordCoverage.IngestedThrough
    let terminalStart = snapshot.TerminalStartCursor

    let gapEnd =
        match terminalStart with
        | Some cursor -> cursor
        | None -> snapshot.XTraceHead

    { Opening = snapshot.Opening
      Frames = snapshot.EffectiveFrames
      Gap = snapshot.XTrace |> sliceExclusiveInclusive yCursor gapEnd
      Terminal = snapshot.TerminalItems }
    |> LifecycleWorkRecord.render
```

必须满足：

- Opening 恰好一次且逐字保持。
- 每个有效 Y frame 恰好一次。
- Gap 只包含 Y 尚未覆盖的 X suffix。
- Terminal 恰好一次且逐字保持。
- 相邻 segment 不重复、不丢失，顺序与 XTrace 一致。
- 同一 projection state 产生相同 bytes。
- 物化不触发 LLM、不写新 Y frame、不改变 coverage。

推荐 LWR 人类可读格式用稳定 Markdown，而不是把整个生命周期做成巨大 TOML：

```markdown
# Opening task
<原文>

# Work log
<Y frame 1>
<Y frame 2>

# Uncompressed tail
<统一 X semantic renderer 输出>

# Final output
<原文>
```

段为空时整段省略；不要输出空标题。LWR 是给 LLM 阅读的内容值，外层 wire 再用 canonical TOML string 承载。这样既保持可读性，也避免用 TOML schema 为每条历史记录重复 `turn/kind/role`。

## 2.6 Terminal 的处理

当前“最后输出不经过 transform”应成为正式不变量：

1. terminal classifier 确认 child run 完成；
2. 从 reconciled terminal message 构造 XTrace semantic items，包含 formal assistant text 与 host-visible reasoning；
3. 将它们以 `TerminalOutputCaptured` 原子事实追加到 lifecycle projection；
4. 调用同一个 LWR materializer；
5. `AgentCompletionPayload` 只保存 `WorkRecord: string`，不再保存 `FinalText` 和 WorkRecordSnapshot metadata；
6. `join` 原样输出该 LWR。

这不是“另建最后回复收集机制”：terminal capture 是 LWR 生命周期投影的最后一个 segment，不存在平行的 A/final channel。

若 terminal message 为空，应以现有 terminal-validity 规则决定 completion 是否有效；不能为了填 LWR 伪造文本。

---

## 3. SSOT 修改清单

下面分“规范语义必须改”和“术语/验证机械同步”。不应只改 spec/08、09，否则 01/07/12/13 仍会反向要求旧 wire。

## 3.1 一级：必须重写的规范 SSOT

### spec/01.md — ARCH-010 synthetic TOML

修改点：

- 保留“instruction comments 与 data body 之间恰好一个空行”。
- 新增：data body 内部默认单 LF 连续渲染，禁止 renderer 为字段或 table 自动插入空行。
- 新增：只输出 LLM 决策需要的字段；runtime identity、digest、freshness、coverage marker 不得进入普通 tool result，除非该工具的用户语义明确要求。
- 新增：同一语义 artifact 作为 TOML value 传递时必须复用相同 bytes，不得在不同方向改写或套第二层摘要。
- 澄清“original human/model text 不需要 TOML 化”的边界：LWR 内部保留原文；只有进入 synthetic wire 时作为 TOML string value 包裹。

建议新增条款：

```text
ARCH-010-LWR：LWR 是 opaque semantic value。TOML renderer 只负责安全承载，
不得读取、重排、摘要或补充 LWR 内容。
```

### spec/07.md — HOST-005 / Session 生命周期记录

修改点：

- 删除 A(X)、ARecord、session-wide A、ProviderRun A segment 的正式定义。
- 用 durable `XTrace` 与 `OpeningPromptCaptured` / `TerminalOutputCaptured` 取代。
- 明确 XTrace 含 host-visible reasoning、有则记录，无则省略。
- 明确 XTrace 的 semantic part 范围与非语义字段排除规则。
- 明确 Host compaction 不得销毁 XTrace；只重锚 provider-prefix proof。
- `join().formalRecord` 相关文字全部删除。

HOST-005 可重命名为“XTrace 分段与 durable cursor”，保留 clause ID 以减少引用震荡，或新增 HOST-005A 后废止旧义。推荐直接重写并在迁移注记中声明旧 A 术语废止。

### spec/08.md — COMPANION 核心

需要重写：

- COMPANION-003：删除 A(X)/B(X) 双轨，定义 XTrace、Y frames、LWR、RecordCoverage、PrefixCoverage。
- COMPANION-004：更新 Y system prompt；删除“无 B 则父 A”的 Seed 行为；reasoning 改为“保留 decision-relevant host-visible reasoning，不得发明 hidden reasoning”。
- COMPANION-005：`BlogFrameKind = Entry | Squash`，删除 `Seed`；normal provider shape 改为 `system + prior frames + data-only delta`，不再有独立 normal instruction。
- COMPANION-006：squash 只处理本 X 的 Entry/Squash，不得混入 inherited parent context；squash commit 保持 `RecordCoverage` 不变。
- COMPANION-007：明确 XTrace 是 Y 和 LWR gap 的共同单一来源；Blogger TOML 是单向 wire，不是记录 source。
- COMPANION-008：busy/failure 不推进 `RecordCoverage`；下一次 delta 从同一 XTrace cursor 重试。Host compaction 不清零 lifecycle ingest cursor。
- COMPANION-009：`FrozenB` 改为 `FrozenRecordPrefix` 或 `FrozenLwrPrefix`；probe 使用 Opening + 仅能证明覆盖完整 Host prefix 的 Y frame prefix。不得把 RawGap 放进 frozen replacement，因为 gap 没有 Y 覆盖证明。
- COMPANION-010：低信任 block 文案从 “lossy companion work log” 改为“lifecycle work record prefix”；其中 Opening 原文不是新指令，整个 block 仍是 context。
- COMPANION-011：cutoff proof 只约束 prefix replacement，不再兼任 LWR completeness。
- COMPANION-012：统一 XTrace provider-semantic projection 和 metadata 排除表。
- 其余 synthetic ID 条款把 FrozenB 命名同步为 FrozenRecordPrefix。

### spec/09.md — fork/join/child lifecycle

修改点：

- EXEC-004：join 成功只返回最小身份、status、`workRecord`；删除 `formalRecord`。
- EXEC-006：child 初始 context 始终是创建时 materialized parent LWR；不再 B else A。terminal 生成 child final LWR；不再提取 session-wide A。
- EXEC-008：不可变 parent background 定义为创建时 LWR snapshot。Y 落后的部分由 X gap 自动补齐；重试复用完全相同 bytes。
- 明确 child OpeningPrompt 是原始 assignment，而不是带 `parent_work_record` 的 synthetic envelope。
- 明确成功 join 不输出 runtime-only identity；失败结果只输出 parent LLM 能采取行动所需的 status、agent、error code/message。

### spec/11.md — persistence

修改点：

- 增加 durable facts：

```fsharp
OpeningPromptCaptured
XTracePartAppended
BlogEntryCommitted  // 带 IngestedThrough XTraceCursor
BlogSquashCommitted
TerminalOutputCaptured
```

- `OpeningPromptCaptured` 和 `TerminalOutputCaptured` 对每个 lifecycle identity 幂等、不可覆盖。
- `XTracePartAppended` 严格顺序、append-only，BlobRef 存正文，journal line 存 digest/cursor/provenance。
- 删除 Seed persistence 语义。
- 将 BlogProjection 的 lifecycle ingest progress 与 PrefixEpochProjection 分离。
- schema version bump；旧 A/Seed journal 不能被新 fold 静默解释。
- LWR 是 projection/materialization，不需要每次重复持久化整份大字符串；terminal completion 可保存最终 LWR BlobRef 作为 immutable completion artifact。

### spec/12.md — recovery、probe、Blogger delta

修改点：

- CTX-003/CTX-013：delta source 改为 XTrace suffix；schema 精简，reasoning 明确保留。
- CTX-011：probe 只使用可证明覆盖完整 Host turn 的 `FrozenRecordPrefix`；LWR gap 使用 RecordCoverage，不属于 probe 算法。
- CTX-012：squash 不处理 Seed；frame coverage 使用 XTrace cursor。
- Host compaction/reanchor：只重置 PrefixCoverage/epoch，不重置 XTrace 或 RecordCoverage。
- 200 KiB 计量仍在最终 provider-visible bytes；normal delta 已无重复 instruction，预算自然减少。

建议 Blogger delta schema（CTX-013，已落地）：

```toml
[[new_work_to_record]]
user = "..."

[[new_work_to_record]]
reasoning = "..."

[[new_work_to_record]]
assistant = "..."

[[new_work_to_record]]
tool_call = "read"
arguments = "..."

[[new_work_to_record]]
tool_result = "..."

[[new_work_to_record]]
media_omitted = "image/png"
```

历史 frame 消息层：

```toml
[[do_not_exec]]
historic_frame = "..."
```

实际 renderer 输出时 table 之间不插空行。字段只在存在时输出；`truncated = true` 仅在发生截断时输出。

删除：`turn`、`kind`、独立 `[[message]]`/`[[tool_call]]` 表、markdown `# Working Record` / `# New Work To Record` 包装。顺序由 table 出现顺序表达；text 的 role 作为字段名。

### spec/13.md — ARCH-010 详细合同和工具面

修改点：

- 所有旧 fork envelope、join tool result 示例更新为最小 LWR wire。
- 明确 data body 无装饰性空行。
- surface inventory 的禁止字段加入：`final_text`、`formal_record`、work_record metadata、普通 join 中的 runtime IDs。
- Blogger delta 示例切到按 table 名表达语义的稀疏 schema。
- 明确 `work_record` 是 opaque string，不允许 join renderer 把它拆成 `{text,digest,freshness,...}`。

## 3.2 二级：必须机械同步的 SSOT

### spec/00.md

更新系统地图和概念入口：删除 A/B 作为跨模块产品概念，新增 XTrace、Y frame、LWR、RecordCoverage、PrefixCoverage。

### spec/10.md

验证投影谱系改为：

```text
Host/provider event
  → XTraceProjection（唯一语义 source）
  → BloggerDeltaProjection（Y 输入）
  → LifecycleWorkRecordProjection（跨 Session artifact）
  → Provider/Wire renderer
```

新增“相同 XTrace segment 不能由两套独立 parser/renderer 生成”的 verification gate。

### spec/14.md、spec/15.md

搜索并替换所有 `A(X)`、`B(X)`、`LatestB`、`CoverableB`、`FrozenB`、`Seed`、`formalRecord`、`workRecord` 旧义引用。Strength/Enforcer/StudentTeacher 若只需要 context，应依赖 LWR 或 FrozenRecordPrefix，不得重新发明 fallback。

### spec/99.md

术语表：

- 删除或标记 deprecated：A(X)、B(X)、LatestB、CoverableB、FrozenB、Seed。
- 新增：XTrace、XTraceCursor、RecordCoverage、PrefixCoverage、LifecycleWorkRecord、OpeningPromptRaw、TerminalOutputRaw、RawGap、FrozenRecordPrefix。

## 3.3 可能只需交叉引用的 SSOT

### spec/03.md

若 OpeningPrompt capture 绑定 PromptAuthority 的物理接受点，则补充“只在已证明 Host 接受后提交 OpeningPromptCaptured”；PromptAuthority 的单写者原则本身无需改。

### spec/16.md

当前未发现直接依赖 A/B wire 的主规范。实施时跑全仓术语扫描；只有出现旧名或假设 join 有 formalRecord 时才机械同步，不要无意义改文档。

---

## 4. 目标数据流

## 4.1 根 Session 首次 prompt

```text
Host 接受第一条真实 prompt
  → PromptAuthority 证明接受
  → OpeningPromptCaptured(exact text)
  → XTrace append PromptText
  → 后续 transform 给 Y 时跳过 Opening anchor
```

注意：Opening 可以同时存在于 XTrace 供审计/顺序证明，但 Blogger delta cursor 的起点应设在 Opening 之后，保证 Y 永远不压缩它。

## 4.2 父 fork child

```text
parent snapshot
  → materialize parent LWR at current XTrace head
      Opening exact
      + all effective Y frames
      + X gap after RecordCoverage
      + terminal（通常无）
  → freeze exact bytes on ChildRunCreated
  → ForkChildPayload renders assignment + parent_work_record
  → child accepted opening captures only original assignment/requirements
```

创建失败、Host retry、restart recovery 必须读同一个 frozen parent LWR BlobRef，不重新物化，否则同一 child logical run 的背景可能漂移。

## 4.3 正常 X→Y

```text
transform boundary
  → read XTrace after RecordCoverage.IngestedThrough
  → exclude Opening anchor
  → chunk to <= 200 KiB final wire bytes
  → data-only TOML delta
  → Y emits one entry
  → atomically commit frame + new IngestedThrough
```

busy/failure/empty/XML-only：不提交 frame，不推进 cursor。

## 4.4 live parent background

```text
materialize LWR = Opening + Y frames + X gap to current head
```

即使 Y 从未成功过，也不是“fallback 整个 A”：

```text
Opening + 0 frames + X gap from opening-end to current head
```

这仍是同一个物化算法，没有分支语义。

## 4.5 terminal completion

```text
terminal output reconciled
  → append/capture exact TerminalOutputRaw
  → materialize final LWR
  → AgentCompletionPayload.WorkRecordBlobRef
  → join renders status + agent + work_record
```

父 LLM 不再收到 terminal text 的独立 instruction comments；它只在 LWR 的 Final output 段看到一次。

## 4.6 prefix replacement

```text
Y frame coverage + current Host epoch mapping
  → choose complete-turn cutoff
  → materialize FrozenRecordPrefix:
       Opening exact + coverable Y frame prefix
  → verify CoveredPrefixDigest
  → replace old raw X prefix with low-trust context block
  → keep cutoff 后 Host raw history
```

不得把 X RawGap 塞进 FrozenRecordPrefix。RawGap 的存在只证明“Y 尚未覆盖”，恰好不满足 prefix replacement 的压缩证明。

---

## 5. 代码实施顺序（按提交拆分）

以下顺序避免同时维护两个半成品 wire。每个提交应可编译，语义切换集中在最后的 cutover commit。

## Commit 1：先落 SSOT 与迁移门禁

修改：

```text
spec/00.md
spec/01.md
spec/07.md
spec/08.md
spec/09.md
spec/10.md
spec/11.md
spec/12.md
spec/13.md
spec/14.md
spec/15.md
spec/99.md
STATUS/README.md
STATUS/conformance.md
```

动作：

- 新增本方案为 `STATUS/lifecycle-work-record.md`。
- conformance 中相关条款暂标 `PLANNED` 或 `NONCONFORMANT`，不要继续声称旧实现 conformant。
- 新增 architecture gate 的旧字段扫描，但先允许 migration allowlist，避免第一提交全红无法推进。

完成标准：规范中只剩迁移说明引用旧 A/B 名；没有两个互相冲突的正式定义。

## Commit 2：建立 XTrace 领域类型与 canonical renderer

新增建议：

```text
src/Wanxiangshu/Domain/XTrace.fs
src/Wanxiangshu/Domain/LifecycleWorkRecord.fs
src/Wanxiangshu/Journal/XTraceProjection.fs
src/Wanxiangshu/Journal/LifecycleWorkRecordProjection.fs
```

或将现有 `ProviderProjection.fs` 提升为 XTrace owner，但必须保证名字与职责清楚，不能让 Blogger 和 terminal 各自继续构造一套 part。

实现：

- typed XTrace parts/cursor；
- exact opening/terminal anchors；
- deterministic LWR segment renderer；
- materializer 输入只接受 projection state，不访问 Host；
- internal provenance 与 LLM-visible text 分层。

更新 `Wanxiangshu.fsproj` 编译顺序：Domain types → Journal projection → Session materializer → Infrastructure adapters。

测试：

```text
tests/unit/Context/x-trace.test.mjs
tests/unit/Context/lifecycle-work-record.test.mjs
```

至少覆盖 exact bytes、reasoning、空 segment 省略、gap 无重复、determinism。

## Commit 3：持久化 facts 与 fold

修改：

```text
Kernel/Fact.fs
Journal/FactCodec.fs
Journal/Fold.fs
Journal/ProjectionState.fs
Journal/Boot.fs
Journal/Writer.fs（若需新 append helper）
```

新增 facts：

```text
OpeningPromptCaptured
XTracePartAppended
TerminalOutputCaptured
```

修改 `BlogEntryCommitted` payload：携带 `IngestedThrough: XTraceCursor`；PrefixCoverage 独立。

删除/废止：

```text
BlogFrameKind.Seed
BlogProjection.withSeed
旧 A record facts/projection
```

迁移策略建议：

- journal schema 版本递增；
- 对旧 schema fail-fast，错误明确写“journal predates LifecycleWorkRecord schema”；
- 不在 fold 中猜测如何把旧 Seed/A 变成新 XTrace；这会制造无法证明的顺序和原文；
- 如产品必须保留旧现场，另写一次性离线 converter，并把转换报告作为 evidence，不把兼容分支永久留在生产 fold。

## Commit 4：统一 semantic capture

修改候选：

```text
Infrastructure/OpenCode/Codec/Projection.fs
Domain/ProviderProjection.fs
Application/Reconciliation/ReconciledTurn.fs
Application/Reconciliation/CompletedTurnClassifier.fs
Application/Reconciliation/TurnCompletionProgram.fs
Infrastructure/OpenCode/Host/Events.fs
Infrastructure/OpenCode/Host/SessionSnapshotPort.fs
```

动作：

- 所有 prompt/assistant/reasoning/tool parts 只经一条 XTrace mapper。
- 物理接受第一条 prompt 后捕获 Opening。
- 每个 completed semantic part append XTrace，幂等键使用稳定 provenance。
- terminal classifier 只标记哪些尾部 items 属于 Terminal，不再生成 A text。
- compaction signal 只改 PrefixEpoch/PrefixCoverage。

完成后删除：

```text
Infrastructure/OpenCode/Host/TerminalSessionA.fs
SessionARecord 及其 port methods
```

## Commit 5：改 Y delta、prompt 与 frame

修改：

```text
Domain/BloggerDelta.fs
Domain/BloggerToml.fs
Domain/CompanionPrompt.fs
Domain/CompanionProjectionBuilder.fs
Session/CompanionHostBlogger.fs
Session/Companion.fs
Journal/BlogProjection.fs
Journal/CompanionJournalPort.fs
src/Wanxiangshu/prompts/blogger-system.md
```

动作：

- Blogger delta 从 XTrace cursor 读取；
- reasoning part 保留；
- 使用稀疏 table schema；
- 删除通用 `turn/kind` 和空字段；
- 删除 delta comment 中 normal instruction；
- 删除独立 normal instruction synthetic message；
- 删除 Seed/withSeed；
- squash 仅处理本 session frames；
- `CompanionHostBlogger` restart/reset 路径禁止再用裸英文 + JSON/semantic dump 拼接，必须走同一 delta projector；
- `LatestB` 命名替换为 `EffectiveFrames` 或 `CompressedMiddle`，避免它继续被误解为完整工作记录。

特别检查：当前 reset 路径若用 `sprintf` 拼 FULL B 与 `ProviderProjection.renderSemantic`，必须删除。这是“样式混乱”的另一条旁路。

## Commit 6：修 TOML canonical layout

修改：

```text
Domain/SyntheticToml.fs
Domain/BloggerToml.fs
Infrastructure/OpenCode/Codec/ToolHostCodec.fs（若其 composer 复制了 spacing）
```

精确改动：

```fsharp
// 当前
String.concat "\n\n" ordered

// 目标
String.concat "\n" ordered
```

保留：

```fsharp
header + "\n\n" + body
```

即 instruction header 与 body 之间仍只有一个空白行；body 内无自动空白行。

更新 golden tests，明确断言：

- instruction-only 以单 LF 结束；
- data-only fields/tables 连续，无 `\n\n`；
- mixed payload 只有 header/body 边界出现一次 `\n\n`；
- multi-line TOML string 自身内容中的空行不受此规则影响。

## Commit 7：父→子切到 frozen parent LWR

修改：

```text
Domain/ForkChildPayload.fs
Session/ForkTypes.fs
Session/ChildRun.fs
Session/ChildRunProjection.fs
Session/HostForkRuntimeFork.fs
Session/HostForkChildDispatch.fs
Session/HostForkRestart.fs
Session/ForkRecovery.fs
Infrastructure/OpenCode/Tools/ForkTool.fs
Infrastructure/OpenCode/Tools/ToolRuntimeScope.fs
Infrastructure/OpenCode/Plugin/SpikePlugin.fs
```

动作：

- `ParentWorkRecord` 由 `materializeLifecycleWorkRecord(parentSnapshot)` 唯一产生；
- 删除 `LatestB |> else TerminalSessionA.fullText`；
- child creation fact 保存 parent LWR BlobRef/digest，retry 复用；
- `ForkChildPayload.ParentWorkRecordInstruction` 删除 “prefer B, else A” 字样；
- child Opening capture 使用原始 assignment，而不是 `ForkChildPayload.render` 结果；
- `BaseInstructions` 的固定六字段报告要求单独做产品决策。若没有 SSOT 明确要求，建议删除，避免每个 child 为格式而格式；父只需要 LWR。

`ForkChildAssignment` 建议变成：

```fsharp
type ForkChildAssignment =
    { Assignment: string
      ParentWorkRecord: string option
      OriginalUserRequirements: string list
      Payload: string option }
```

类型可不变，但 capture point 必须在 render 之前。

## Commit 8：子→父切到唯一 LWR completion

修改：

```text
Session/AgentCompletion.fs
Session/HostForkRunLifecycle.fs
Session/HostPendingRun.fs
Session/CompletionMailbox.fs
Infrastructure/OpenCode/Tools/JoinTool.fs
Infrastructure/OpenCode/Host/Orchestrator*（若读取 completion 字段）
Journal/LinkageProjection.fs（若 durable completion shape 受影响）
```

将：

```fsharp
FinalText: string
WorkRecord: WorkRecordSnapshot option
```

替换为：

```fsharp
WorkRecord: BlobRef // 或已物化 string，推荐 BlobRef
```

可保留内部：

```fsharp
AgentId / ChildSessionId / RunId / Role / AuthorityRoot / ProviderRun / Directory
```

供 runtime 使用，但 `JoinTool.encodeCompletion` 不输出这些内部字段。

成功 wire：

```fsharp
ToolHostCodec.tomlObject
    [ "status", tString "completed"
      "agent", tString agentName
      "work_record", tString recordText ]
```

失败 wire：

```toml
status = "failed"
agent = "deep-coder"
[error]
code = "..."
message = "..."
```

不要同时输出 `outcome = message` 和 `[error].message`。

PTY completion 不是 LWR；保持独立最小 schema，避免为了“统一”把 agent session 语义强塞给 PTY。

## Commit 9：prefix probe 改用 FrozenRecordPrefix

修改：

```text
Domain/XPrefixProjection.fs
Domain/PrefixCandidate.fs
Domain/PrefixProbeSelection.fs
Journal/PrefixEpochProjection.fs
Kernel/Fact.fs 中 PrefixSnapshot
Application/Reconciliation/XWire.fs
Application/Reconciliation/CompanionTransform.fs
```

重命名：

```text
FrozenBRef       → FrozenRecordPrefixRef
FrozenBDigest    → FrozenRecordPrefixDigest
CoverableB       → CoverableRecordPrefix
```

候选 renderer：

```text
OpeningPromptRaw + coverable Y frame prefix
```

RawGap 不参与。CoveredPrefixDigest 仍证明被替换的是完整 Host prefix。

compaction：

- retire ActivePrefixEpoch；
- reset PrefixCoverage；
- 不清 XTrace；
- 不清 RecordCoverage；
- 不丢 Y frames。

## Commit 10：删除兼容代码和旧术语

全仓禁止：

```text
A(X)
B(X)
session-wide A
LatestB（除迁移文档）
CoverableB
FrozenB
BlogFrameKind.Seed
withSeed
FinalText（Agent completion）
formalRecord/formal_record
WorkRecordSnapshot.Freshness
WorkRecordSnapshot.CoveredThrough
TerminalSessionA
prefer B, else A
```

允许：普通业务语境中的 “final text” 不应被字符串 gate 误伤；gate 应按符号/字段/路径精确匹配。

---

## 6. 文件级修改地图

### Domain

```text
Domain/ProviderProjection.fs             提升为 XTrace 单一 semantic source，或被 XTrace.fs 取代
Domain/BloggerDelta.fs                   从 XTrace cursor 取 delta；reasoning 保留；去重复 instruction
Domain/BloggerToml.fs                    稀疏 schema；无 turn/kind/空 tool
Domain/CompanionPrompt.fs                更新 system prompt；删除 normal duplicate；更新低信任文案
Domain/CompanionProjectionBuilder.fs     normal 只投影 frames + 最后 data delta
Domain/ForkChildPayload.fs               parent_record 文案改 LWR；首 prompt capture 在 render 前
Domain/SyntheticToml.fs                  body block 单 LF
Domain/XPrefixProjection.fs              FrozenRecordPrefix
Domain/PrefixProbeSelection.fs           PrefixCoverage 与 RecordCoverage 分离
```

### Journal

```text
Journal/BlogProjection.fs                删除 Seed；frame commit 带 XTrace cursor
Journal/CompanionProjection.fs           新命名与双 coverage
Journal/PrefixEpochProjection.fs         只持 Host prefix proof
Journal/ProjectionState.fs               加 XTrace/LWR projection
Journal/FactCodec.fs / Fold.fs           新 facts、新 schema、fail-fast
Journal/CompanionJournalPort.fs          不再把 frames 当完整 record
```

### Session / Reconciliation

```text
Session/AgentCompletion.fs               单一 WorkRecord
Session/Companion*.fs                    Y 只压中段
Session/HostFork*.fs                     父 LWR snapshot / child final LWR
Application/Reconciliation/*             capture XTrace、terminal anchor、prefix mapping
```

### Infrastructure/OpenCode

```text
Host/TerminalSessionA.fs                 删除
Host/Events.fs                            SessionA port 改 XTrace append/read
Plugin/SpikePlugin.fs                    删除 B→A fallback
Tools/JoinTool.fs                         最小成功/失败 wire
Tools/ToolRuntimeScope.fs                 暴露 materialize/frozen LWR port，不暴露 string fallback callback
Codec/Projection.fs                      单一 semantic mapper
```

### Prompts

```text
prompts/blogger-system.md                reasoning 新规则；normal behavior 单一 owner
prompts/manager-system.md                不再要求从 final_text/formalRecord 找结果
prompts/orchestrator-system.md            同上（若有）
prompts/reviewer-system.md                使用 work_record + authoritative requirements
其他 agent prompts                        搜索旧 join 字段和 A/B 术语
```

---

## 7. 测试计划

## 7.1 领域单测

### LWR materialization

1. `opening_prompt_is_byte_exact_and_never_compressed`
2. `opening_prompt_appears_exactly_once`
3. `host_visible_reasoning_is_present_in_x_trace`
4. `missing_reasoning_requires_no_placeholder`
5. `y_frames_cover_prefix_and_x_supplies_only_suffix`
6. `no_y_frames_means_opening_plus_raw_gap_not_alternate_A_path`
7. `terminal_output_is_byte_exact_and_appears_once`
8. `terminal_output_is_not_returned_in_a_separate_field`
9. `materialization_is_deterministic`
10. `empty_sections_are_omitted`
11. `child_opening_excludes_parent_work_record_envelope`
12. `reviewer_opening_preserves_authoritative_requirement_order`

### Coverage

1. `record_coverage_can_end_mid_turn`
2. `prefix_coverage_only_advances_at_complete_turn_boundary`
3. `lwr_gap_starts_at_record_coverage_not_prefix_cutoff`
4. `raw_gap_is_never_used_for_prefix_replacement`
5. `host_compaction_resets_prefix_coverage_only`
6. `record_coverage_never_retreats`
7. `blogger_failure_does_not_advance_record_coverage`
8. `squash_changes_frames_not_ingested_through`

### Blogger schema

1. reasoning table is emitted when present;
2. no `turn =`;
3. no `kind =`;
4. no `tool = ""`;
5. no `truncated = false`;
6. delta is data-only on normal request;
7. exactly one normal behavior owner;
8. reset/restart path uses same renderer.

### TOML layout

1. mixed payload only has one header/body blank line;
2. data body has no automatic blank lines;
3. multi-line value bytes remain exact;
4. tables cannot swallow later top-level fields;
5. same LWR value renders identically in fork and join.

## 7.2 现有测试迁移

重点更新：

```text
tests/unit/Context/blog-projection.test.mjs
tests/unit/Context/blogger-delta.test.mjs
tests/unit/Context/blogger-toml.test.mjs
tests/unit/Context/companion-projection.test.mjs
tests/unit/Context/fold-context-recovery.test.mjs
tests/unit/Context/prefix-epoch.test.mjs
tests/unit/Context/probe-selection.test.mjs
tests/unit/Context/synthetic-toml.test.mjs
tests/unit/Execution/fork-child-payload.test.mjs
tests/unit/Execution/handle.test.mjs
tests/unit/Plugin/manager-tool-contract.test.mjs
tests/unit/Prompt/send-format.test.mjs
```

删除所有以旧行为为成功条件的断言，例如：

- child background “B 优先，否则 A”；
- join 同时含 final_text/work_record；
- Seed frame；
- normal instruction 作为独立 user message；
- TOML body block 之间必须有空行；
- Blogger 忽略 reasoning。

## 7.3 Gate

### architecture-gate.mjs

新增：

- `TerminalSessionA.fs` 不存在；
- production code 不得出现 `FinalText` completion field；
- `BlogFrameKind.Seed` 不存在；
- 只有一个 XTrace semantic mapper；
- 只有一个 LWR materializer；
- parent background 只能调用 LWR port；
- JoinTool 不得读取 coverage/digest/freshness 输出给 LLM。

### surface-inventory.mjs

对 agent join LLM-visible surface 禁止：

```text
final_text
formal_record
agent_id
run_id
child_session_id
authority_root
provider_run
directory
freshness
covered_through
fallback_peer
tier
```

对 fork surface：允许 `parent_work_record`、存在时的 `content` 与 Reviewer requirement；禁止 `prefer B, else A` 文案。

### ssot-lint.mjs

- 新术语必须有 spec/99 定义；
- 除迁移/历史段外禁止 A(X)/B(X)/Seed；
- conformance 的 clause 名和新 SSOT 一致。

## 7.4 Canary / E2E

至少新增或扩展：

1. Y 完全追平：父→子 LWR 无 RawGap；child 完成后 join 只有一个 work_record。
2. Y 落后一轮：父→子 LWR 中已压缩部分来自 Y，缺口只来自 X，顺序正确且无重复。
3. Y 从未成功：父→子仍得到完整 LWR，使用统一算法，不存在 A fallback 标志。
4. reasoning：host-visible reasoning 在 Y delta 和 RawGap 两条路径都可见。
5. terminal：child 最后 output 未触发 transform，仍原样出现在 join work_record。
6. restart before Y catches up：恢复后 gap 完整；cursor 不回退。
7. Host compaction：PrefixCoverage 重置，RecordCoverage/XTrace 保留；最终 LWR 不丢旧生命周期。
8. multi-generation fork：child Opening 不嵌套 parent LWR；记录大小线性而非递归增长。
9. Reviewer：权威 HumanRoot requirements 原样保留，parent LWR 只作背景。
10. no duplicate normal instruction：provider trace 中 normal request 只有 system behavior + data delta。
11. wire minimization：join 不出现内部 ID/metadata。
12. byte identity：创建时 frozen parent LWR 与 child 首 prompt 中 `parent_work_record` value 解码后完全一致。

推荐复用/扩展剧本：

```text
manager-companion-canary.mjs
manager-full-loop-canary.mjs
reviewer-verdict-canary.mjs
reviewer-restart-canary.mjs
host-restart-canary.mjs
companion-canary.mjs
fallback-aabb-trace-canary.mjs
```

---

## 8. 验收命令与顺序

仓库已有脚本名以实际 `package.json` 为准；按当前项目惯例执行：

```bash
npm run gate:static
npm run build
npm run test:unit
npm run test:harness
npm run test:e2e:p0
npm run test:e2e:p0:three
npm run test:release
```

另外必须保存以下定向证据：

```bash
# 旧术语/旧 wire 清零
grep -RInE 'A\(X\)|B\(X\)|session-wide A|formal_record|formalRecord|FinalText|BlogFrameKind\.Seed|withSeed|prefer B, else A' SSOT src tests/unit testkit

# join surface 不泄漏内部字段
node scripts/surface-inventory.mjs

# TOML layout golden
node tests/unit/runner.mjs --match synthetic-toml
node tests/unit/runner.mjs --match blogger-toml

# LWR/coverage 定向测试
node tests/unit/runner.mjs --match lifecycle-work-record
node tests/unit/runner.mjs --match x-trace
node tests/unit/runner.mjs --match probe-selection
```

如果 runner 不支持 `--match`，用仓库现有单文件调用约定替换；不要为了文档命令新造第二个 test runner。

最终证据目录建议：

```text
docs/evidence/<next-version>/lwr/
  SSOT-DIFF.txt
  OLD-TERMS-SCAN.txt
  STATIC-GATE.txt
  BUILD.txt
  UNIT.txt
  HARNESS.txt
  CANARY-3ROUND.txt
  JOIN-WIRE.txt
  FORK-WIRE.txt
  TOML-GOLDEN.txt
  COMPACTION-RECOVERY.txt
  COMMIT.txt
  ENV.txt
```

---

## 9. STATUS/ 更新模板

### STATUS/README.md

实现期间写：

```markdown
## 当前开发阶段

LifecycleWorkRecord migration in progress. A/B dual-record wire is frozen;
new XTrace/LWR projection is being introduced. Relevant COMPANION/EXEC/CTX/HOST
clauses are temporarily PLANNED/NONCONFORMANT until cutover gates pass.
```

完成后写：

```markdown
## 当前产品状态

父→子与子→父已统一为 LifecycleWorkRecord。每个 Session 的 opening task 与
terminal output 原样保留；Y 仅压缩中段；Y 时滞只由 XTrace suffix 补齐。
join 不再返回 formal/final 旁路字段，LLM-visible TOML 已去除运行时 metadata
与 data-body 装饰性空行。
```

### STATUS/conformance.md

迁移开始时把以下旧行从 CONFORMANT/PARTIAL 改为 `PLANNED`：

```text
ARCH-010 runtime TOML
HOST-005
COMPANION-003/004/005/006/007/008/009/010/011/012/013
EXEC-004/006/008
PERSIST 相关 BlogEntry/Seed/coverage 条款
CTX-003/011/012/013
VERIFY-007
```

完成后每行必须绑定：

- 新 production owner 路径；
- 至少一个领域测试；
- 至少一个 wire/gate 或 canary 证据；
- 实际验证 commit。

不要只改状态词而沿用旧证据描述中的 A/B、Seed、FinalText。

---

## 10. 程序员逐项操作清单

### 开工前

- [ ] 从最新目标分支创建独立 worktree。
- [ ] 保存 baseline：static/build/unit/harness/P0。
- [ ] 将本方案复制为仓库 `STATUS/lifecycle-work-record.md`。
- [ ] 先改 SSOT，确保新术语无歧义。
- [ ] 决定 journal schema 是 fail-fast cutover 还是提供离线 converter；禁止默默兼容。

### 领域与持久化

- [ ] 建 XTrace part/cursor/provenance 类型。
- [ ] 建 OpeningPromptRaw 与 TerminalOutputRaw facts。
- [ ] 建 LWR typed segments 和唯一 materializer。
- [ ] 拆 RecordCoverage / PrefixCoverage。
- [ ] 删除 Seed。
- [ ] 确保 compaction 不清 RecordCoverage/XTrace。
- [ ] 添加 schema version 和 fold rejection。

### Blogger

- [ ] system prompt 改为保留 decision-relevant host-visible reasoning。
- [ ] normal instruction 只保留一处，推荐 system-only。
- [ ] delta 改 data-only。
- [ ] 去 turn/kind/空 tool/false truncated。
- [ ] restart/reset 路径走同一 projector。
- [ ] squash 只处理 child 自身 frames。

### TOML

- [ ] body blocks 用单 LF。
- [ ] header/body 保留唯一空行。
- [ ] optional fields 不存在时完全省略。
- [ ] LWR 作为 opaque string value，不拆 metadata table。

### Parent → Child

- [ ] child creation 前 materialize parent LWR。
- [ ] 将 exact bytes 冻结到 child run durable fact/blob。
- [ ] retry/restart 复用 frozen blob。
- [ ] capture child opening 使用原始 assignment/requirements。
- [ ] parent_work_record 不进入 child opening anchor。
- [ ] 删除 B else A callback。

### Child → Parent

- [ ] terminal capture 写入 LWR projection。
- [ ] completion 删除 FinalText。
- [ ] completion 删除 WorkRecordSnapshot freshness/coverage metadata。
- [ ] JoinTool 成功只发 status/agent/work_record。
- [ ] JoinTool 失败只发 status/agent/error。
- [ ] runtime-only IDs 留在日志/诊断，不发 LLM。

### Prefix

- [ ] FrozenB 全部重命名。
- [ ] frozen prefix = Opening + coverable Y frame prefix。
- [ ] RawGap 不可参与 replacement。
- [ ] digest/cutoff proof 保持 fail-closed。
- [ ] compaction 只 retire prefix epoch。

### 收尾

- [ ] 全仓旧术语扫描清零。
- [ ] 删除 migration allowlist。
- [ ] 更新所有 prompts 和 fixtures。
- [ ] 三轮 canary 全绿。
- [ ] `test:release` 全绿。
- [ ] 更新 conformance 到新 commit。
- [ ] 保存 evidence。

---

## 11. 容易踩坑的地方

### 11.1 不要把“首条 prompt 原样”实现成“首条 synthetic wire 原样”

后者会包含 parent LWR 和 transport instruction，造成递归膨胀与语义污染。capture 必须发生在 `ForkChildPayload.render` 之前。

### 11.2 不要继续保留 `FinalText` “以防万一”

只要它还存在，调用方就会重新产生“正式回复优先还是 work record 优先”的分支。迁移 commit 必须删除字段与所有消费者，而不是标 deprecated。

### 11.3 不要用 Prefix cutoff 补 LWR gap

Prefix cutoff 只能位于完整 turn；Y ingest cursor 可更细。混用会重复半个 turn 或漏内容。

### 11.4 不要在 compaction 时清 lifecycle cursor

这会让全部旧 Y frames 与重新读取的 X 内容重复。若当前 Host transcript 已失去旧 raw，唯一能满足完整 lifecycle 的办法是提前 durable append XTrace。

### 11.5 不要把 digest/freshness 暴露给 LLM

它们是系统证明和调试数据，不帮助父 Agent完成任务。需要观测时放 diagnostic tool、journal evidence 或 structured logs。

### 11.6 不要用另一套 renderer 处理 terminal

terminal output、live gap、Y delta 都必须源自同一 XTrace semantic mapper。否则 reasoning/tool parts 会再次不对称。

### 11.7 不要为美观给 TOML 自动加空行

多行 string 内部的用户空行必须保留；结构性 data blocks 之间不要自动空行。两者测试要分开。

### 11.8 不要让 Seed 以新名字复活

例如 `InheritedFrame`、`ParentContextFrame` 仍然会污染 child lifecycle。父 LWR 只能是输入 context，不是 child Y frame。

---

## 12. Definition of Done

只有同时满足以下条件才算完成：

1. 规范中存在且只存在一个跨 Session 工作记录：LWR。
2. 每个 Session opening task 原文逐字存在于最终 LWR，且恰好一次。
3. 每个 terminal output 原文逐字存在于最终 LWR，且恰好一次。
4. host-visible reasoning 在 XTrace、Y input、RawGap 三条相关路径语义一致。
5. 父→子和子→父都传同一 LWR 格式，不存在 A/B/final 双轨。
6. Y 落后时，只追加 XTrace 未覆盖 suffix；无整段 fallback。
7. child 自身 LWR 不嵌套 parent LWR。
8. normal Blogger instruction 只有一个 owner。
9. TOML data body 无自动空行、无空字段、无普通 join runtime metadata。
10. Prefix replacement 只使用可证明覆盖的 Opening + Y frame prefix，绝不使用 RawGap。
11. Host compaction 不破坏 XTrace/LWR lifecycle completeness。
12. 旧字段、旧术语、Seed、TerminalSessionA 在 production 与 active SSOT 中清零。
13. static/build/unit/harness/P0×3/release 全绿，conformance 绑定新 commit 和新证据。

---

## 13. 建议的最终模块所有权

为避免几年后重新分裂，建议明确单一 owner：

```text
XTrace semantic mapping       Domain/XTrace.fs
XTrace durable state          Journal/XTraceProjection.fs
Y delta projection            Domain/BloggerDelta.fs
Y frame state                 Journal/BlogProjection.fs
LWR materialization           Domain/LifecycleWorkRecord.fs
LWR projection assembly       Journal/LifecycleWorkRecordProjection.fs
Synthetic TOML string/layout  Domain/SyntheticToml.fs
Parent child wire             Domain/ForkChildPayload.fs
Join wire                     Infrastructure/OpenCode/Tools/JoinTool.fs
Prefix proof                  Journal/PrefixEpochProjection.fs + Domain/XPrefixProjection.fs
```

硬规则：

- 业务模块不得自己 `sprintf` 一份工作记录。
- wire renderer 不得解析 LWR。
- Y 不得成为 parent background 的 owner。
- terminal 不得成为 final-text 旁路的 owner。
- JoinTool 不得决定记录 completeness；它只读取已物化 artifact。

这套所有权完成后，系统逻辑可归纳为一句话：

```text
X 记录事实，Y 压缩已见中段，LWR 用 Y + 唯一 X 缺口物化完整生命周期。
```

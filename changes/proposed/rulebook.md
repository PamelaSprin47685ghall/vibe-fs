# Proposal：Enforcer Rulebook v2

## 目录即身份、双全文规则、Observation 原子历史与 Main 分级投递

---

# 0. Executive Decision

将现有：

```text
resources/enforcer/catalog.json
```

彻底替换为 120 个规则目录：

```text
resources/enforcer/
  primitive-obsession/
    enforcer.md
    main.md

  boolean-blindness/
    enforcer.md
    main.md

  null-ambiguity/
    enforcer.md
    main.md

  ...
```

最终结构：

```text
120 rule directories
240 authored Markdown files
0 authored metadata files
```

每个目录名就是唯一 canonical tip identity。

例如：

```text
primitive-obsession
boolean-blindness
program-counter-state
ignored-tdd
guessed-not-verified
...
```

不再维护第二套：

```text
id
field
family
catalogOrdinal
scoreWhen
nudge
metadata.json
manifest.json
```

规则身份只有：

```text
RuleName = directory basename
```

两篇 Markdown 的职责严格不同：

```text
enforcer.md
    给 Blogger / Enforcer 看。
    负责判断：
    “这是不是这条问题？”

main.md
    给 Main agent 看。
    前提是 tip 已经被 Enforcer 选定。
    负责回答：
    “既然已经命中，接下来该怎么做，为什么？”
```

---

# 1. 最核心的领域裁决：Work Log 与 Tip 是一个不可拆 Observation

本 Change 不再把：

```text
work-log frame
```

和：

```text
tip history
```

建模为两套独立列表。

新的第一性模型是：

```text
Observation
    WorkLog
    TipIdentity
    Evidence?
    CycleIdentity
```

即：

```text
一个 Blogger normal cycle
=
一段 work log
+
一个 tip
```

二者：

```text
共同产生
共同持久化
共同投影
共同 squash
共同恢复
```

永不分离。

当前 `BlogEntryCommitted` 本身已经把 `TextRef/TextDigest` 与 `TipRuleId/FieldNameAtCommit` 放在同一个原子事实里，因此这不是凭空制造的新关联，而是把已有 durable 因果关系贯彻到 projection 层。

---

# 2. Blogger 历史必须按 Observation 一一对应、并置

当前不再采用：

```text
tip 1
tip 2
tip 3

frame 1
frame 2
frame 3
```

这种 projection。

因为即使数量相同，模型仍需要自行推断：

```text
tip 2 到底属于 frame 1、frame 2 还是 frame 3？
```

而当前实现实际上就是把所有 tips 放前面，再放所有 frames。

新目标必须变成：

```text
Observation 1
    work_log = ...
    tip = primitive-obsession

Observation 2
    work_log = ...
    tip = ignored-tdd

Observation 3
    work_log = ...
    tip = blind-edit
```

也就是说 Blogger 每次回看自己过去的工作记录时，可以直接看到：

> 当时我看到的是这些事实；我当时因此选择了这条 tip。

这是一个非常重要的自我校准闭环。

它允许 Blogger判断：

```text
我上次为什么选了这条？
这次是不是同一种现象？
上次的证据和这次是否相同？
是不是在机械重复？
是不是前一条判断其实偏了？
```

而不需要把两个独立数组重新 join。

---

# 3. 推荐的 Blogger-visible 历史形状

不强制具体 TOML 字段名，但语义必须类似：

```toml
[[do_not_exec]]
kind = "historic_observation"
cycle = "run-123"
tip = "primitive-obsession"
work_log = """
Coder changed the account boundary...
"""
```

或者：

```toml
[[do_not_exec.observation]]
cycle = "run-123"
tip = "primitive-obsession"
work_log = """
...
"""
```

关键 invariant 不是格式本身，而是：

```text
TipIdentity 和 WorkLog 必须出现在同一个 projected observation unit。
```

禁止重新退化成：

```text
previous_enforcer_tip messages
+
historic_frame messages
```

两条平行 stream。

---

# 4. Evidence 也属于 Observation

如果保留 `evidence`：

```text
Observation
    WorkLog
    TipIdentity
    Evidence?
```

它也必须与同一次 observation 原子关联。

不能：

```text
tips 单独保存
evidence 单独保存
frames 单独保存
```

然后未来靠 ordinal 猜。

Evidence 的职责是说明：

> 为什么当前 work material 支持这个 tip。

而不是重新写一遍 `main.md`。

---

# 5. Squash 作用于 Observation，而不是 Frame

这是本 Proposal 的第二个核心 invariant：

> squash 同时作用于 work log 与 tip；tip 和 work log 永不分离。

当前实现允许：

```text
Entry 1 + tip A
Entry 2 + tip B
→ squash frames into one frame

但 RecentTips:
A
B
仍独立存在
```

当前测试甚至明确证明 squash 后 frame 已变成一个，而 tips 仍为两个。

该语义在本 Change 中必须删除。

新的 squash 输入不是：

```text
frames
```

而是：

```text
Observation[]
```

逻辑：

```text
[
  { log A, tip A },
  { log B, tip B },
  { log C, tip C }
]

↓ squash

[
  { squashed log ABC, squashed tip X }
]
```

或者在未来有明确需要时：

```text
[
  { compressed log AB, tip X },
  { compressed log C, tip Y }
]
```

但永远禁止：

```text
compressed logs
+
uncompressed independent tip list
```

---

# 6. Squash 后的 Tip 不是“新发现的 Main violation”

Squash 是历史表示重写。

因此：

```text
SquashedObservation.TipIdentity
```

表示：

> 这份压缩后的历史 observation 应当用哪个 tip identity 来代表。

它不是：

> Blogger 刚刚重新观察 Main，并新发现一次 violation。

所以 squash：

```text
会重写历史 tip
```

但：

```text
不会因为 squash 自己再向 Main 发送一次新的 tip delivery。
```

Main delivery 的触发来源仍然必须是：

```text
新的 normal observation
```

而不是：

```text
历史压缩操作
```

---

# 7. 删除独立 RecentTips 语义

本 Proposal 下不再需要：

```text
BlogProjection.Frames
EnforcementProjection.RecentTips
```

作为两个彼此独立、生命周期不同的历史。

推荐目标是一个统一：

```text
ObservationProjection
```

例如概念上：

```text
ObservationProjection
    FrameEpochId
    Observations
    Coverage
```

其中每条：

```text
ObservationRecord
    Kind = Entry | Squash
    TextRef
    TextDigest
    TipName
    EvidenceRef?
    CycleIdentity
```

具体 Domain 类型可以按现有架构调整。

但是业务上必须消灭：

```text
frames 被 squash
tips 不被 squash
```

这种状态。

---

# 8. 120 篇 `enforcer.md` 全量进入 Blogger System Prompt

上一稿建议按需读取，这是不符合本次目标的。

最终语义明确为：

> Blogger / Enforcer 在任何一次 provider run 中，都完整拥有全部 120 条 Detection 文档。

即启动时确定性构造：

```text
Base Blogger System Prompt
+
Full Enforcer Rulebook
```

其中：

```text
Full Enforcer Rulebook
=
按 RuleName 稳定排序
+
120 × enforcer.md 全文
```

不提供：

```text
lookup tool
rule search tool
按需 retrieval
候选 shortlist 后再读取
```

Blogger 在开始判断以前已经看到全部规则。

---

# 9. Enforcer 全文 Context Budget

目标：

```text
每篇 enforcer.md ≈ 500 tokens
120 × 500 ≈ 60,000 tokens
```

因此 `enforcer.md` 的写作目标不是“大百科”。

而是：

> 在约 500 tokens 内达到高诊断密度。

重点必须放在：

```text
准确定义
触发条件
non-trigger
最近邻区分
判定算法
```

而不是长篇：

```text
历史
哲学
重复解释
大量代码示例
```

建议硬预算：

```text
target: 450–550 tokens
soft range: 400–600
hard max: 650
```

只有极少数真正复杂的 rule 可以申请突破。

120 篇总体应建立一个 automated token budget gate。

---

# 10. Blogger System Prompt 的拼装是 Derived Artifact，不是 Metadata

仓库只 authored：

```text
resources/prompts/blogger-system.md
resources/enforcer/*/enforcer.md
```

runtime 在加载后构造：

```text
effectiveBloggerSystemPrompt
```

例如：

```text
<base blogger system>

# Enforcer Rulebook

## primitive-obsession
<primitive-obsession/enforcer.md>

## boolean-blindness
<boolean-blindness/enforcer.md>

...
```

这个合成 prompt：

```text
不写回仓库
不是第三份规则数据
不是 manifest
不是 metadata
```

只是资源的确定性 projection。

---

# 11. RuleName 同时生成 `blog.tip` enum

现有：

```text
catalog.field
```

改为：

```text
directory basename
```

因此：

```text
resources/enforcer/*/
```

的目录集合同时决定：

```text
1. Blogger system prompt 的规则集合
2. blog.tip enum
3. Main guide lookup namespace
4. rule validation inventory
```

一个名字，一套真相。

---

# 12. 目录排序不表达优先级

为了 deterministic system prompt 和 schema：

```text
RuleNames |> lexical sort
```

即可。

禁止把目录顺序解释为：

```text
severity
priority
family
importance
selection preference
```

删除：

```text
catalogOrdinal
```

以后不再制造另一套伪优先级。

---

# 13. Main 侧仍然使用 auto-injected 机制投递 Tip

上一稿把“没有 Main tip delivery”说成了事实，这是错误的。

现有 Host 路径已经会：

```text
owner RecentTip
→ latestTipNudge
→ markerText
→ PairProgrammingThoughtTransform
→ Main provider view
```

并且当前实现要求历史 auto-injected pair 保留当时 `MarkerText` 原始正文，restart 时可以 byte-identical replay。

因此 Rulebook v2 不另造 fake-user overlay。

继续复用 typed：

```text
auto-injected tool-call/tool-result pair
```

作为 Main guidance surface。

---

# 14. Main 的 Tip Delivery 分为 First Full 与 Repeat Identity

每个 normal observation 都有：

```text
TipIdentity
```

Main 侧收到 tip 时，按 durable delivery history 决定 payload。

## 第一次出现

第一次向该 Main session 投递：

```text
primitive-obsession
```

时：

```text
TipIdentity
+
resources/enforcer/primitive-obsession/main.md FULL CONTENT
```

也就是说 Main 第一次不只看到名字。

它完整看到：

```text
问题意味着什么
现在做什么
为什么
不要做什么
如何验证
什么时候算完成
```

---

# 15. 同一 Tip 后续重复只投 Identity

如果未来再次选择：

```text
primitive-obsession
```

则不再次发送整篇 `main.md`。

只发送稳定 identity，例如：

```text
enforcer_tip = "primitive-obsession"
```

或等价的最小 typed presentation。

目的：

```text
第一次：
    教会 Main 这条规则的完整处置协议。

后续：
    用 identity 唤醒已有规则语义，而不反复消耗全文 token。
```

因此：

```text
First occurrence → full main.md
Repeated occurrence → identity only
```

---

# 16. “第一次”必须由 Durable Facts 判定

绝对不能用：

```text
process-local HashSet
```

判断 main.md 是否已发过。

因为：

```text
restart
recovery
crash
retry
```

以后会忘记。

必须从 durable auto-injected history 得出：

```text
HasFullTipBeenDelivered(MainSession, TipName)
```

当前 `PairProgrammingGuidelineAnchored` 已经持久化精确 `MarkerText` 和 placement。

Rulebook v2 推荐进一步让 durable fact typed 地携带：

```text
TipName?
TipPresentation?
```

例如：

```text
TipPresentation =
    | Full
    | IdentityOnly
    | None
```

而不是通过解析 `MarkerText` 反推。

这不是 authored metadata。

这是 runtime durable business fact。

---

# 17. Historical Auto-injected Bytes 必须冻结

假设第一次：

```text
2026-08-08
primitive-obsession/main.md = version A
```

Main 已经收到 version A。

后来 repository 更新：

```text
main.md = version B
```

restart 后不得把历史 pair 改成 B。

历史 pair 必须继续 replay 当时实际送出的 A。

当前 HOST-013 本来就定义 `MarkerText` 为 provider 当时实际看到的精确正文，并持久化后原样恢复。

这个性质应继续保留。

---

# 18. Repeat Identity 不能成为悬空引用

需要增加一条重要 proof：

```text
Main 看到 identity-only repeat 时，
其有效 provider knowledge 中必须存在该 Tip 的完整 guide，
或者系统必须重新 materialize 全文。
```

否则会出现：

```text
primitive-obsession
```

但 Main 已因为 compaction/reanchor 看不到第一次全文。

具体解决机制可以由实现阶段决定：

```text
A. full guide knowledge 随 Main context projection 保留；
或
B. 一旦完整版本不再 provider-visible，则下一次 occurrence 重新 Full；
```

但禁止：

```text
Main 当前无法恢复 tip 语义
+
Host 仍只发送 identity name
```

这是 referential-integrity failure。

---

# 19. General Pair Programming Guideline 与 Tip Guidance 可继续组合

如果当前 auto-injected pair 仍需携带通用：

```text
PairProgrammingGuidelineText
```

则新的 marker 可以逻辑组成：

第一次：

```text
# Enforcer Tip
tip = "primitive-obsession"

<full main.md>

<general pair programming guideline>
```

重复：

```text
# Enforcer Tip
tip = "primitive-obsession"

<general pair programming guideline>
```

没有可投 tip 时：

```text
<general pair programming guideline>
```

具体 renderer 格式继续由 HOST / Synthetic surface owner 决定。

---

# 20. `enforcer.md` 与 `main.md` 的 Authority 明确分层

## `enforcer.md`

是 Blogger system instruction 的组成部分。

它拥有：

```text
rule classification authority
```

但无权：

```text
修改用户 task
扩大 Main scope
改变角色权限
创造 destructive authority
```

## `main.md`

通过 Host 显式投递给 Main。

因此它是：

```text
Host-adopted engineering guidance
```

但仍然不能覆盖：

```text
user scope
tool ownership
destructive authorization
formal repository governance
higher-priority system instruction
```

---

# 21. `enforcer.md` 不应该写成修复手册

因为它始终 120 篇全文存在。

最珍贵的 token 应用于：

```text
如何区分
```

不是：

```text
怎么修
```

Detection 文档核心问题：

> 在当前事实下，为什么应该选这个名字而不是另一个名字？

---

# 22. `main.md` 不应该重新做 Classification

Main 已经收到：

```text
tip = primitive-obsession
```

所以不要再写 300 tokens：

```text
Maybe primitive obsession means...
First determine whether...
Possibly...
```

Main 文档从：

> This tip has already been selected.

这一事实出发。

其 token 应用于：

```text
行动顺序
repair strategy
wrong fixes
scope boundary
verification
done criteria
```

---

# 23. Catalog 旧字段的最终迁移

现有：

```text
id
field
family
scoreWhen
nudge
catalogOrdinal
```

迁移为：

```text
field
→ folder name

scoreWhen
→ enforcer.md 的 seed

nudge
→ main.md 的 seed

id
→ 删除 authored identity

family
→ 仅创作阶段分组，不进入 runtime

catalogOrdinal
→ 删除
```

现有 Domain 将 `RuleId` 与 provider `FieldName` 分开。

新模型应最终收敛成一个 canonical：

```text
TipName
```

如果 durable migration 需要兼容旧事件，则在 legacy decoder 中使用：

```text
FieldNameAtCommit
```

完成映射。

禁止因此再创建：

```text
legacy-rule-map.json
```

作为永久第二 SSOT。

---

# 24. Loader Contract

启动扫描：

```text
resources/enforcer/*
```

每个 entry 必须：

```text
1. 是普通 directory。
2. 目录名 lower-kebab-case。
3. 无 symlink。
4. 恰好有：
   enforcer.md
   main.md
5. 不允许第三个文件。
6. 两文件合法 UTF-8。
7. trim 后非空。
8. 所有 mandatory headings 存在。
9. 文件不超过 size/token budget。
10. rule references 指向真实目录。
```

失败：

```text
startup fail fast
```

不得：

```text
skip
fallback
warn and continue
embedded default catalog
```

---

# 25. 当前 Inventory = 120，但 Domain 不硬编码 120

Domain loader 只要求：

```text
nonempty
valid
unique
deterministic
```

Repository test 则要求当前 release：

```text
count == 120
```

未来第 121 条加入时：

```text
增加一个 directory
+
更新 inventory test
```

而不是升级什么：

```text
120-rule protocol
```

---

# 26. 资源目录必须只有运行时 Rulebook

推荐：

```text
resources/enforcer/
```

下面只出现 120 个 rule folders。

不要塞：

```text
README
authoring-guide
schema.json
manifest.json
examples/
```

写作规范属于：

```text
docs/
```

本 Proposal 的 Appendix A 可以最终迁移到正式 authoring documentation。

---

# 27. 文档层必须纠正当前语义冲突

当前仓库已有两种相互冲突的叙述：

一方面，Enforcer rebase 文档声称 tip 只作为 Blogger history，不投 Main。

另一方面，HOST 实现和 proof 又明确规定 prior tip 会进入新的 Main auto-injected pair。

本 Change 不能继续容忍这种双解释。

正式：

```text
docs/what/enforcer.md
docs/shape/enforcer.md
docs/how/enforcer.md
docs/why/enforcer.md
docs/proof/enforcer.md
```

必须统一改成：

```text
Tip has two consumers:

1. Blogger:
   as paired historic observation context.

2. Main:
   through HOST-owned auto-injected guidance delivery.
```

但：

```text
Blogger history
≠
Main instruction surface
```

两者来源相同，权限语义不同。

---

# 28. Main Delivery 与 Blogger History 不应共用 renderer

同一个：

```text
TipName
```

有两个不同 consumer。

## Blogger

看到：

```text
historic work log
+
tip identity
```

作为历史 observation。

## Main

看到：

```text
main.md full
```

或：

```text
tip identity only
```

作为 Host guidance。

因此禁止：

```text
把 Blogger history renderer 直接拿去注入 Main
```

或者：

```text
把 Main guidance 文本再塞回 Blogger history
```

---

# 29. 推荐目标领域模型

概念示例：

```text
type TipName = private TipName of string

type EnforcerRule =
    {
        Name: TipName
        EnforcerText: string
        MainText: string
    }

type Observation =
    {
        Kind: Entry | Squash
        WorkLogRef: BlobRef
        WorkLogDigest: BlobDigest
        Tip: TipName
        EvidenceRef: BlobRef option
        Cycle: ProviderRunIdentity
    }
```

正常提交：

```text
ObservationCommitted
```

必须原子包含：

```text
work log
coverage
tip
evidence
provider identity
```

Squash：

```text
ObservationsSquashed
```

必须表达：

```text
replace oldest K Observation
with one new Squashed Observation
```

不能只改 work-log projection。

---

# 30. Tip 重复策略

依旧不恢复：

```text
severity score
pressure
cooldown
leaky integrator
time decay
```

Blogger 在 120 篇完整 rulebook + paired observation history 下自己判断：

```text
当前最有价值的 tip 是什么？
```

历史重复策略仍可在 Blogger base system prompt 统一规定：

```text
- equally important 时避免机械重复；
- blocking / severe / repeatedly unresolved 问题可以重复；
- 不得为了多样性故意绕开当前真正最重要的问题；
- 必须结合过去 work log + tip pair 判断是否真的是同一现象。
```

---

# 31. Implementation Slices

## Slice A — RED：Rulebook Resource Contract

先证明：

```text
catalog.json 是旧 SSOT
folder Rulebook 尚不存在
```

增加 loader contract tests。

---

## Slice B — Folder Loader

实现：

```text
scan directories
validate exact files
read enforcer.md
read main.md
stable sort
construct Rulebook
```

---

## Slice C — Blogger Effective System Prompt

实现：

```text
blogger-system.md
+
120 full enforcer.md
```

并验证：

```text
120/120 names occur exactly once
stable bytes
cwd independent
package independent
```

---

## Slice D — Tip Enum

`blog.tip`：

```text
enum = sorted directory names
```

删除 catalog field dependency。

---

## Slice E — Observation Domain

把：

```text
Blog frame projection
+
independent tip projection
```

收敛成：

```text
paired Observation projection
```

normal commit 原子不变量保持。

---

## Slice F — Blogger History Projection

从：

```text
tips @ frames
```

改成：

```text
observation1
observation2
observation3
...
delta LAST
```

每个 historic observation 内部 tip + work log 并置。

---

## Slice G — Squash

修改 squash：

```text
Observation[] → Squashed Observation
```

work log 和 tip 一起替换。

删除：

```text
squash 后 RecentTips 独立存活
```

---

## Slice H — Main Guidance Resolver

将旧：

```text
rule.Nudge
```

升级为：

```text
rule.MainText
```

并提供：

```text
TipName
```

给 auto-injected renderer。

---

## Slice I — Full/Identity Delivery

实现 durable：

```text
first occurrence
→ Full main.md

repeat
→ TipName only
```

同时保留 exact MarkerText replay。

---

## Slice J — Compaction Referential Integrity

永久证明：

```text
identity-only tip
永远不会在缺失其 full semantic knowledge 的 context 中孤立出现。
```

---

## Slice K — Delete `catalog.json`

只有：

```text
loader
schema
runtime
tests
docs
main delivery
blogger prompt
journal replay
```

全部完成后才能删除。

---

# 32. Runtime Acceptance Criteria

完成必须证明：

```text
[ ] 恰好 120 rule directories
[ ] 恰好 240 markdown files
[ ] catalog.json 不存在
[ ] 无 authored metadata

[ ] directory name 是唯一 TipName
[ ] blog enum = folder names

[ ] Blogger system 始终包含 120 篇 enforcer.md 全文
[ ] effective prompt deterministic

[ ] normal history = paired observations
[ ] work log 与 tip 一一对应
[ ] projection 中相邻/并置

[ ] squash 同时 squash work log 和 tip
[ ] 不再存在独立 RecentTips 生命周期

[ ] new normal observation 可以向 Main 投 tip
[ ] Main 第一次收到 tip → full main.md
[ ] 后续同 tip → identity only

[ ] delivery decision durable/restart-safe
[ ] old auto-injected MarkerText byte-identical replay

[ ] identity-only 永不成为 dangling semantic reference

[ ] 不恢复 score/throttle/fuzzy matching
[ ] 不建立 fake-user enforcement overlay
```

---

# 33. Semantic Acceptance Criteria

最终产品体验应为：

```text
Main work
    ↓

Blogger sees:
    full base system
    +
    all 120 enforcer.md
    +
    historic observations:
        [work log + tip]
        [work log + tip]
        ...
    +
    current new delta

    ↓

exactly one blog:
    work log
    tip

    ↓

atomic Observation commit

    ↓
    ├── future Blogger:
    │      sees work log + tip together
    │
    └── Main:
           first occurrence:
               TipName + full main.md
           repeated occurrence:
               TipName only
```

Squash：

```text
[log A + tip A]
[log B + tip B]
[log C + tip C]

↓ squash as one semantic operation

[squashed log + squashed tip]
```

而不是：

```text
squashed logs
+
old independent tip history
```

---

# 34. Final Product Principle

Rulebook v2 的最终目标不是：

> 把 120 句短文扩成 240 篇长文。

而是建立一个完整闭环：

> Blogger 始终拥有全部工程判断知识；每一次工程判断与当时的 work log 永久属于同一个 observation；历史压缩同时压缩二者；同一 tip 第一次向 Main 出现时给出完整处置手册，之后只用稳定 identity 低成本唤醒；所有 identity、恢复与 replay 都由 durable facts 和目录名证明，而不是靠第二份 metadata 或内存猜测。

---

# Appendix A — 240 篇实际写作宪法

下面规则不是“写作建议”，而是 240 篇内容的 authoring contract。

---

# A1. 总体 Constitution

240 篇不是：

```text
120 × scoreWhen 扩写
+
120 × nudge 扩写
```

而是：

```text
120 个完整 Detection Definition
+
120 个完整 Remediation Protocol
```

两篇的认知任务完全不同。

---

# A2. 统一语言

建议全部使用英文。

理由：

```text
RuleName 英文
现有 agent prompts 英文
tool contract 英文
工程术语主要英文
```

禁止同一概念在 120 篇里不断换译法。

---

# A3. `enforcer.md` Token Constitution

目标：

```text
450–550 tokens
```

Soft：

```text
400–600
```

Hard：

```text
650
```

原因：

```text
120 × ~500
≈ 60k tokens
```

必须始终可以整体装入 Blogger system context。

因此 `enforcer.md` 的详细程度来自：

```text
信息密度
判定边界完整
```

而不是篇幅。

---

# A4. `enforcer.md` Mandatory Template

每篇固定：

```markdown
# <Rule Title>

## Definition

## Trigger When

## Do Not Trigger When

## Why It Matters

## Distinguish From

## Decision Procedure

## Examples
```

不得自行增加大量装饰性章节。

---

# A5. Definition

约：

```text
50–80 tokens
```

必须回答：

```text
什么东西
在什么边界
丢失了什么语义 / invariant
因此产生什么错误可能
```

禁止：

```text
Primitive obsession means using primitives too much.
```

这种 rule name 同义改写。

---

# A6. Trigger When

约：

```text
80–110 tokens
```

写 3–5 条。

必须是：

```text
observable
specific
semantic
```

而不是纯语法。

坏：

```text
There is a boolean.
```

好：

```text
Several booleans encode independent domain meanings and permit combinations
that the real domain does not allow.
```

---

# A7. Do Not Trigger When

约：

```text
70–100 tokens
```

每篇至少：

```text
3 个明确 non-trigger
```

必须覆盖：

```text
合法同形结构
证据不足
更适合 sibling rule
```

这是压 false positive 最重要的 section。

---

# A8. Why It Matters

约：

```text
50–80 tokens
```

要求明确因果：

```text
pattern
→ invariant loss
→ downstream consequence
```

禁止泛泛：

```text
hard to maintain
not clean
bad architecture
```

---

# A9. Distinguish From

约：

```text
100–140 tokens
```

每篇至少：

```text
2 个 closest siblings
```

复杂规则：

```text
3–4
```

格式推荐：

```text
`blind-edit`:
choose this when editing begins before locating and reading the owner.

`guessed-not-verified`:
choose this when the primary defect is an unsupported factual assertion,
even if no edit has happened.
```

必须写差异，不准只列名字。

---

# A10. Decision Procedure

约：

```text
50–80 tokens
```

固定思路：

```text
1. Locate concrete evidence.
2. Identify the violated invariant.
3. Test all explicit exclusions.
4. Compare nearest siblings.
5. Prefer root cause over downstream symptom.
6. Select this rule only if current evidence establishes it.
```

每篇可加 1–2 个 rule-specific step。

---

# A11. Enforcer Examples

剩余：

```text
60–100 tokens
```

最低：

```text
1 positive
1 near miss
1 counterexample
```

示例要短。

目标不是教学，而是建立 decision boundary。

---

# A12. Enforcer 禁止内容

绝对禁止：

```text
长篇修复教程
完整 refactor steps
测试执行计划
Main role instructions
history essay
设计模式科普
大量 source code
```

如果一段主要回答：

> 应该怎么改？

它属于 `main.md`。

---

# A13. Enforcer 语气

使用：

```text
Trigger when...
Do not trigger merely because...
Prefer X when...
Choose Y instead when...
The required evidence is...
```

少用：

```text
probably
usually bad
often ugly
cleaner
more elegant
```

---

# A14. `main.md` Token Constitution

Main 每次只会拿到当前 tip 对应的一篇全文。

因此可以更完整。

建议：

```text
800–1,200 tokens
```

Soft：

```text
700–1,400
```

Hard：

```text
1,600
```

如果希望所有 240 篇整体更统一，也可以压到：

```text
600–900
```

但不要为了 token 强行删掉：

```text
wrong fixes
verification
done criteria
authority boundary
```

---

# A15. `main.md` Mandatory Template

统一：

```markdown
# <Rule Title>

## What To Do Now

## What You Are Protecting

## Repair Strategy

## Decision Branches

## Common Wrong Fixes

## Verification

## Done When

## Scope and Authority
```

---

# A16. What To Do Now

约：

```text
100–150 tokens
```

必须让 Main 打开后立即知道下一步。

格式优先：

```text
1.
2.
3.
4.
```

动作应从：

```text
inspect concrete instance
```

开始。

禁止第一句直接：

```text
rewrite the subsystem
```

---

# A17. What You Are Protecting

约：

```text
60–100 tokens
```

告诉 Main：

> 这不是为了 style，而是为了什么 invariant？

例如：

```text
type substitution safety
single source of truth
durable causality
idempotent retry
explicit ownership
deterministic semantics
```

---

# A18. Repair Strategy

约：

```text
180–260 tokens
```

必须解释：

```text
理想目标结构
为什么该结构修复根因
owner 应该在哪里
不要在哪个 symptom 层继续 patch
```

禁止 pattern cargo cult。

---

# A19. Decision Branches

约：

```text
150–220 tokens
```

至少两个实际岔路。

例如：

```text
If this is an internal-only value...
If it crosses a public boundary...
If old durable data already exists...
If a real compatibility contract exists...
```

“保姆级”最重要的标准，就是把真正会影响行动的岔路写清楚。

---

# A20. Common Wrong Fixes

约：

```text
100–160 tokens
```

至少：

```text
3 个 wrong fixes
```

每个需要解释：

```text
为什么看起来有用
为什么其实没有恢复 invariant
```

例如：

```text
another wrapper
another flag
catch-and-ignore
larger timeout
retry-until-green
compatibility shim
duplicate source of truth
comment instead of structural repair
```

---

# A21. Verification

约：

```text
100–160 tokens
```

必须针对 rule invariant。

不要统一：

```text
Run tests.
```

例如：

```text
retry rule
→ duplicate execution proof

race rule
→ deterministic concurrency proof

persistence rule
→ restart/crash replay proof

boundary rule
→ real contract-level test

TDD rule
→ failing old behavior before implementation
```

---

# A22. Done When

约：

```text
60–100 tokens
```

必须是 checklist：

```text
[ ] root invariant restored
[ ] obsolete workaround removed
[ ] relevant proof exists
[ ] no duplicate owner remains
```

完成定义不能只是：

```text
tests green
```

---

# A23. Scope and Authority

约：

```text
60–100 tokens
```

所有 main.md 都必须声明或体现：

```text
This guidance does not expand the user's requested scope.
It does not grant destructive authority.
It does not override role/tool ownership.
It does not justify unrelated refactoring.
```

涉及危险动作的 rule 应更具体。

---

# A24. Main 禁止内容

禁止：

```text
重新花大量篇幅判断 rule 是否真的成立
重复 enforcer.md 的 trigger list
宽泛 clean-code 说教
要求无关大规模 rewrite
擅自扩大 user scope
把“最佳实践”当绝对命令
```

---

# A25. 两篇禁止重复

人工 review 时问：

```text
把 enforcer.md 和 main.md 并排以后，
是否大部分段落只是换人称重复？
```

如果是：

```text
FAIL
```

理想：

```text
enforcer.md:
    Why classify?

main.md:
    How respond?
```

---

# A26. Neighbourhood-first Authoring

不能：

```text
一个一个孤立扩写 120 条
```

必须先建立邻接关系。

每写一条，作者必须先列出：

```text
最像它的 2–4 条 rule
```

再写正文。

例如：

```text
guessed-not-verified
blind-edit
guess-based-fix
```

必须作为一组互相校准。

---

# A27. Root Cause Rule

当多个规则同时成立时：

```text
优先选择更靠近 causal root 的 rule
```

而不是选择：

```text
最容易从表面 syntax 看出来的 rule
```

但有一个前提：

```text
root cause 必须当前有证据。
```

不能为了追 root cause 开始猜。

---

# A28. Narrow-over-broad Rule

如果：

```text
一个 rule 精确描述当前问题
```

另一个只是：

```text
宽泛描述同一个症状
```

优先窄 rule。

这必须反映在 `Distinguish From` 中。

---

# A29. Observable Evidence Rule

Enforcer 文档不能要求模型使用它看不到的事实。

禁止：

```text
if the developer intended...
if this probably has no tests...
if another file likely...
```

必须基于：

```text
current visible material
historic observation
tool-visible facts
```

---

# A30. No Syntax Smell Classification

以下全部禁止：

```text
看到 bool → boolean-blindness
看到 null → null-ambiguity
看到 catch → catch-all
看到 mutable → mutation smell
看到 sleep → sleep synchronization
看到 mock → mock-hidden-state
```

必须满足语义 trigger。

---

# A31. Example Constitution

所有 examples：

```text
短
具体
单一变量
```

不要写：

```text
一个 300 token 大案例同时出现 8 个 smells
```

否则无法校准 rule boundary。

---

# A32. Main Repair Constitution

所有 repair guidance 遵循：

```text
Locate owner
→ identify invariant
→ smallest owner-level repair
→ remove obsolete workaround
→ verify at governing boundary
```

禁止：

```text
symptom patch
→ another symptom patch
→ another adapter
```

---

# A33. Content Style

推荐：

```text
short paragraphs
bullets
explicit conditions
causal wording
precise nouns
```

避免：

```text
clean
elegant
beautiful
modern
best practice
bad smell
```

除非后面立即解释具体 invariant。

---

# A34. Cross-rule Terminology

同一概念必须统一名字。

例如一旦确定：

```text
authoritative source
durable fact
derived projection
provider-visible
owner
boundary
```

就不要在不同规则随机换成 4 种说法。

---

# A35. Authoring Waves

建议按当前旧 family 做创作 batching，但 family 不进入 runtime。

12 waves：

```text
A 10
B 10
...
L 10
```

每 wave：

```text
10 × enforcer.md
10 × main.md
neighbor review
cross-check
```

然后才进入下一组。

---

# A36. Calibration Phase

不要直接写完 240 篇。

先选 8–12 条代表规则。

必须覆盖：

```text
type
control flow
architecture
persistence
concurrency
testing
investigation
delivery hygiene
```

完成以后检查：

```text
500-token Detection 是否足够？
Main guide 是否行动明确？
相邻规则是否能区分？
token budget 是否现实？
```

再冻结 template。

---

# A37. Semantic Review Rubric — Enforcer

每篇 0/1 检查：

```text
[ ] 500 token 左右仍自洽
[ ] definition 精确
[ ] trigger 是 semantic
[ ] ≥3 non-trigger
[ ] ≥2 nearest siblings
[ ] sibling 有 tie-break
[ ] 有 root-cause 判断
[ ] 有 positive
[ ] 有 near miss
[ ] 有 counterexample
[ ] 没有 remediation 漂入
```

少一项就不算完成。

---

# A38. Semantic Review Rubric — Main

```text
[ ] 第一屏就知道下一步
[ ] 明确 invariant
[ ] 修的是 owner/root
[ ] 有至少 2 个 decision branches
[ ] ≥3 wrong fixes
[ ] verification 针对 invariant
[ ] done criteria 可验证
[ ] scope 不扩张
[ ] authority 不越界
[ ] 没有重新做 classification
```

---

# A39. Pair Review

每个目录必须作为整体 review：

```text
<name>/
    enforcer.md
    main.md
```

Reviewer 应依次问：

```text
1. enforcer.md 能不能正确命中？
2. 能不能正确不命中？
3. 能不能和 neighbors 区分？
4. 一旦命中，main.md 会不会把 Main 带到正确 owner？
5. 有没有危险或常见 wrong fix 没写？
6. 怎么证明修完？
```

---

# A40. Cross-family Tournament

240 篇完成后必须专门做 cross-family collision review。

尤其：

```text
G testing discipline
vs
H verification quality

I investigation/implementation method
vs
J delivery hygiene

C architecture
vs
K architecture governance

B control flow
vs
D state/history
```

防止 family 内写得很好、跨 family 却大量重叠。

---

# A41. Adversarial Enforcer Eval

专门构造合法案例：

```text
legal boolean
legal null
legal mutation
legal catch
legal compatibility
legal mock
legal timeout
legal sleep
legal abstraction
```

测试它不会乱触发。

False-positive eval 必须和 positive eval 同等重要。

---

# A42. Historical Pair Eval

给 Blogger：

```text
Observation A:
log A + tip X

Observation B:
log B + tip Y
```

然后新的 material 与 A 类似。

检查 Blogger 是否能：

```text
看到 A 当时的 tip X
结合 A 的 work log
判断这次是否真正重复
```

这是 paired history 的核心收益。

---

# A43. Squash Eval

给：

```text
[log A + tip X]
[log B + tip Y]
```

squash 后必须证明：

```text
旧 observation 被整体替换
```

而不是：

```text
新 log
+
旧 X/Y tips 残留在另一个 projection
```

这是永久 regression。

---

# A44. Main First-full Eval

第一次 tip：

```text
primitive-obsession
```

必须看到：

```text
TipName
+
full main.md
```

并 durable 记录 exact bytes。

---

# A45. Main Repeat Eval

第二次同 tip：

```text
primitive-obsession
```

必须：

```text
只出现 TipName
不重复 main.md 全文
```

同时新的 auto-injected pair identity 仍按 HOST placement 规则生成。

---

# A46. Restart Eval

第一次 full 后 crash/restart。

再次遇到同 tip：

```text
不得因为内存丢失重新误判为第一次
```

除非当前 provider knowledge 已不再含 full semantic content，且正式 compaction contract 明确要求 re-materialize。

---

# A47. Content Change Policy

普通：

```text
clarify wording
add better example
improve wrong-fix explanation
```

可修改同一个 Markdown。

但：

```text
改变 trigger boundary
split one rule
merge two rules
rename folder
让以前合法的 case 变 violation
```

属于产品语义变更。

必须单独 Proposal。

---

# A48. Rename Policy

Folder name = identity。

因此 rename：

```text
不是 file cleanup
```

而是：

```text
tip identity migration
```

必须考虑：

```text
journal history
main delivery history
blog enum
historic observations
replay
```

---

# A49. 自动 Gate

建议：

```text
scripts/checks/enforcer-rulebook.mjs
```

检查：

```text
120 directories
240 markdown
exact filenames
directory syntax
no symlinks
UTF-8
nonempty
mandatory headings
enforcer token budget
main token budget
rule references valid
effective system contains all 120 exactly once
tip enum matches names
```

---

# A50. 最终 Authoring Definition of Done

一条 rule 只有同时满足以下才完成：

```text
[ ] folder name canonical
[ ] enforcer.md 通过 constitution
[ ] main.md 通过 constitution
[ ] token budget 合法
[ ] neighbors 已互相校准
[ ] positive / near miss / counterexample 有效
[ ] wrong fixes 完整
[ ] verification 有针对性
[ ] authority 不越界
[ ] paired history eval 能使用它
[ ] Main first-full / repeat-name 行为能使用它
```

120 条全部满足，才算完成 240 篇 Rulebook。

---

# Appendix B — 最终不可违反的 12 条 Invariants

```text
1. FolderName is TipIdentity.

2. No authored metadata besides folder names.

3. Every rule has exactly enforcer.md + main.md.

4. Blogger always sees all 120 enforcer.md in full.

5. Every normal work-log observation has exactly one TipIdentity.

6. WorkLog and Tip are one durable Observation.

7. Blogger historical WorkLog and Tip are one-to-one and co-located.

8. Squash acts on Observations; it can never squash logs while retaining independent tips.

9. Main receives full main.md on the first occurrence of a tip.

10. Repeated occurrences send only TipIdentity, subject to the invariant that the full semantics remain recoverable in the current provider context.

11. Historical auto-injected bytes replay exactly as originally sent.

12. No score vector, numeric severity, throttle, fuzzy matching, shadow metadata, or fake-user enforcement overlay returns through this redesign.
```

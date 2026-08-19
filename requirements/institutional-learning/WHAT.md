# institutional-learning — WHAT（唯一 normative 合同）

命题前缀 `INSTITUTIONAL-LEARNING-`。

## INSTITUTIONAL-LEARNING-001：`celebrate` / `regret` 记录的是经验，不是结论模板

`celebrate(experience)` 与 `regret(experience)` 各接受一段非空自然语言；输入允许主观、口语化、局部，不要求调用方预先写成规则、分类 failure family、填写 evidence schema 或猜 TipName。

两者只差经验极性：celebrate 寻找值得复制的成功机制，regret 寻找值得避免的代价机制；都不得把“成功”直接等价成 reward score，也不得把“后悔”直接等价成 violation 已成立。

## INSTITUTIONAL-LEARNING-002：每个经验最终只有一个 committed disposition；evaluation 有界

一次最终成功的 celebrate/regret `LearningOccurrenceId` 恰好提交一个 Enhancer disposition；任一单次 evaluation
attempt 也只能调用一次私有 Enhancer，并在同一经验上输出且只输出：

```text
ABSORB   既有 rule 已经表达该一般机制；不新增、不改写 rule
BIRTH    存在真正新的、可复用、可再次识别的 success/failure mechanism；建立新 rule
DISCARD  局部偶然、taste、一次性事实、无法泛化或不值得长期 attention tax；不进入制度
```

不存在 fourth state、score、confidence threshold、recursive enhancement 或“先存 pending lesson 以后再决定”的长期 workflow。
若 BIRTH 在 atomic commit 前因 `ExpectedRulebookRevision` stale 而失效，允许同一 LearningOccurrenceId 在 latest
Rulebook 上 **至多再 evaluation 一次**；第二次仍 stale → 显式 `ConcurrentRulebookChange` failure、zero commit。
stale attempt 从未成为 institutional outcome；最终 durable world 仍最多一个 committed disposition。

## INSTITUTIONAL-LEARNING-003：Enhancer 的目标是 generalize mechanism，不是改写经历

Enhancer 必须回答“这次经历揭示了什么比这次经历更一般的机制？”。一个 BIRTH/ABSORB 结果只能使用调用 experience 与当前 canonical Enforcer Rulebook 作为学习输入；不得为了写规则自行扩大为新的 repository/public web investigation，也不得把局部命令流水、具体文件名、单次时间戳直接升格为永久制度，除非它们本身就是可泛化识别条件的一部分。

## INSTITUTIONAL-LEARNING-004：只有 BIRTH 可请求新增 institutional Enforcer rule；ABSORB/DISCARD 零 mutation

BIRTH 必须生成一条满足 `behavior-diagnosis` Rulebook 合同的新 candidate，包括唯一 TipName 与 English/zh-CN
两套 EnforcerText/MainText，并交给 `behavior-diagnosis` 的 canonical admission boundary。只有 admission 成功才算
制度化；ABSORB 表示“现有 rule 已经足够表达这次 lesson”，因此零 rule mutation；DISCARD 同样零 mutation。

禁止 runtime-only learned-rules DB、shadow catalog、generated score table 或只在当前 prompt 中存在的“临时 rule”。
Enhancer 不直接写 package 安装目录；durable institutional rule 的身份、存储与 live Rulebook union 归
`behavior-diagnosis`。学习结果只有被该 owner admission 并进入唯一 live Rulebook 才算制度化成功。

## INSTITUTIONAL-LEARNING-005：新规则必须压缩 attention tax，而不是放大 scar tissue

BIRTH 只有在以下条件同时成立时才合法：

- mechanism 超出单次经历，未来存在可识别 trigger；
- 能写出至少一个 negative / distinction，避免把普通现象误诊成规则命中；
- 与现有 Rulebook 不等价、不只是换词重复；
- 未来避免/复制该机制的价值足以支付所有 participant 长期看到/消费规则的 attention tax。

无法满足任一项 → ABSORB 或 DISCARD；“能写出一条规则”从来不是 BIRTH 的充分条件。

## INSTITUTIONAL-LEARNING-006：positive learning 与 negative learning 同等合法

celebrate 的成功机制不得因为“没有事故”而被丢弃；Enhancer 可以把成功经验表达为可复制的行为 law，或在符合 behavior-diagnosis 合同的情况下表达为未来可检测的反面病理。不得强行把所有成功经验扭曲成惩罚式语言；若现有 Enforcer 表达面无法诚实承载，应 DISCARD，而不是造假 diagnosis。

## INSTITUTIONAL-LEARNING-007：`celebrate` 最后才 resurfacing deferred work

一次 celebrate 的顺序固定为：

```text
accept experience
→ Enhancer disposition
→ canonical rule admission/validation（仅 BIRTH）
→ freeze learning result
→ attention-regulation resurface 当前 deferred work
→ tool result 最后呈现 deferred items
```

`regret` 不自动 resurface deferred work。celebrate 即使有 deferred items，也不得自动执行、todo 化、委派或宣称它们成为新 obligation。

## INSTITUTIONAL-LEARNING-008：学习失败必须诚实，不伪装成已制度化

如果 Enhancer 无法得到合法 disposition、BIRTH admission/validation 失败，tool return 必须明确经验未被制度化；不得返回“learned/added rule”后把失败藏在日志里。ABSORB 与 DISCARD 都是合法成功结果。

每个 accepted celebrate/regret tool occurrence 必须有稳定 `LearningOccurrenceId`。在任何 durable mutation 前，
Enhancer disposition、BIRTH candidate admission precheck（若有）与 celebrate 将 resurfacing 的 DeferredWork batch
都先完成纯 staging；随后一次 atomic durable commit 至少包含 `LearningDispositionCommitted`，并按 disposition/
kind 同批携带 `InstitutionalRuleBorn`（仅 BIRTH）与 DeferredWork resurfacing facts（仅 celebrate 有 item 时）。
任一 precheck 失败 → zero commit。BIRTH admission staging 必须携带 `behavior-diagnosis` 给出的 exact
`ExpectedRulebookRevision`；commit 前 revision 已变化同样视为 precondition failure / `KnownNotCommitted`，不得把
旧 snapshot 上的 generalization 硬塞进新 Rulebook，而应按 INSTITUTIONAL-LEARNING-002 的有界规则在新 Rulebook
上重新跑本 occurrence 的 Enhancer/precheck。

`LearningDispositionCommitted` 冻结该 occurrence 的 disposition 与 provider-visible learning result。retry/replay
发现同一 LearningOccurrenceId 已 committed 时只重放冻结结果，不再次调用 Enhancer、不出生第二条 rule、不 drain
下一批 DeferredWork。禁止“rule 已出生但 learning receipt 没写”或“defer 已 drain 但 celebrate 返回失败”的半提交。

## 边界

- 既有规则如何被 Blogger 诊断 → `behavior-diagnosis`。
- 规则如何交付 Main → `guidance-delivery`。
- deferred queue/resurface 语义 → `attention-regulation`。
- atomic durable learning transaction → `durable-events`。
- tool contract/visibility → `action-affordance` / `capability-enforcement`。

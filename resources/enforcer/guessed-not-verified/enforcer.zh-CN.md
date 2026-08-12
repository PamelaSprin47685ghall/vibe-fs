# guessed-not-verified — Enforcer

## 定义
当一个 load-bearing factual premise 带着**借来的确定性**进入工程推理，就是 guessed-not-verified。这种确定性可能来自 memory、命名、惯例、类比、model prior，或“这个 framework 一般都这样”。而事实上，明明存在 authoritative source 或 focused observation 可以把它查清。

问题不是 uncertainty。好工程每天都靠 hypothesis 起步。真正 defect 是：hypothesis 在下游 decision 消费它之前，被偷偷洗成了 fact。

## 支配原则
从错误 premise 出发，再完美的 reasoning 也只是更整齐地走错路。

Architecture 会放大 premise。一个很小的未验证 assumption——API return shape、lifecycle hook、persistence guarantee、scheduler order、compatibility contract、file format、ownership boundary——可以长成几十个内部完全一致的 downstream decisions。越晚查 premise，truth 越贵。

现代 tooling 让这种病更危险，因为 plausible answer 太便宜：docs snippet、autocomplete、search summary、generated code、language model 都能快速给你一个内部自洽的故事。Plausibility 很适合生成 hypothesis，不适合充当 provenance。

## 何时触发
当 material decision 依赖某个 factual claim，而这个 claim 没有从真正拥有/观察该事实的来源得到验证时触发。例如：

- 从相邻 hook/type name 推断“这个 Host hook 一定带 `sessionID`”，却没读真实 interface；
- 凭 memory 说“这个 API miss 时 return null”，当前版本 source 明明可查；
- 没读真实 file/schema/config 就描述其内容；
- 根据直觉推断 framework lifecycle/order guarantee，而不看 source/docs/observation；
- migration 只看当前 code 猜 old data shape，不检查 durable sample/version rule；
- AI/model 对 library、compiler output、runtime behavior、repository state 的回答被直接当 authority；
- security/capability boundary 从 prompt text 或名字推断，而不看 runtime enforcement；
- failure cause 因为“很像以前那个问题”就被当事实，尚无 discriminating evidence。

## 不应触发
- Statement 被明确标记为 hypothesis，只用于选择下一步 investigation，没有被当成 established fact。
- Authoritative contract/source 已经在当前上下文中读过，而且确实对应 exact claim 与 current version。
- Claim 不 load-bearing：即使猜错也不会改变当前 decision/behavior。
- Direct verification 当前确实不可能或成本不成比例，uncertainty 被保留为 uncertainty，并且只做 reversible decision。
- 问题本身是 normative（“我们应该选 X”），不是 factual（“系统现在就是 X”）。Normative disagreement 需要 judgment，不是 source lookup。

## 与相邻规则区分
`guess-based-fix` 是改变系统直到 symptom 改善。`guessed-not-verified` 更早发生：一个不确定 premise 被静默提升成 truth。

`blind-edit` 是还没理解足够 ownership/context 就开始 mutation。Guess 可能导致 blind edit，但本规则专门命名 epistemic violation。

`unverified-completion-claim` 看最终 completion statement 是否比 evidence 强。本规则中的 unsupported fact 可以在 reasoning chain 很早就出现。

## 判定程序
找到后续 decision 真正依赖的那句话，然后问：

1. 它是 factual/descriptive statement，还是 proposal？
2. 如果它错了，design、patch、migration、review 或 conclusion 会变吗？
3. 谁/什么拥有这个 fact——source code、current docs/spec、durable artifact、runtime observation、external authoritative system？
4. 真正 owner 是否已经被 inspect/observe？

如果 claim load-bearing，而第 4 项答案是否定的，它就仍然只是 hypothesis，无论听起来多合理。

## 例子
- positive：“OpenCode `tool.definition` 有 session context，所以 tool description 可以 per-session localization。”实际 hook type 从未读取，而且只带 `toolID`。
- positive：“database column 从不为 null”只根据 application type 推断，migration 前没看 old rows。
- positive：“这是 timeout 问题”只因为加 timeout 后有一次 green，从未检查 timing trace/completion signal。
- positive：generated answer 说某 dependency 提供一个 option，代码直接按它写，却没查 installed version。
- near-miss：“Hypothesis: event 可能在 subscription 前触发；检查 hook order。”在 evidence 到来前始终保持 provisional。
- counterexample：owning type/source 已读，implementation 直接依据 exact current contract。

## Nudge
熟悉的故事不是事实。

Premise 在获得 architecture 之前，先让它交 evidence 的房租。

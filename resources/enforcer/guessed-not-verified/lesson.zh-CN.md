# guessed-not-verified — Lesson

## 评价

### 定义

当一个 load-bearing factual premise 带着**借来的确定性**进入工程推理，就是 guessed-not-verified。这种确定性可能来自 memory、命名、惯例、类比、model prior，或“这个 framework 一般都这样”。而事实上，明明存在 authoritative source 或 focused observation 可以把它查清。

问题不是 uncertainty。好工程每天都靠 hypothesis 起步。真正 defect 是：hypothesis 在下游 decision 消费它之前，被偷偷洗成了 fact。

### 支配原则

从错误 premise 出发，再完美的 reasoning 也只是更整齐地走错路。

Architecture 会放大 premise。一个很小的未验证 assumption——API return shape、lifecycle hook、persistence guarantee、scheduler order、compatibility contract、file format、ownership boundary——可以长成几十个内部完全一致的 downstream decisions。越晚查 premise，truth 越贵。

现代 tooling 让这种病更危险，因为 plausible answer 太便宜：docs snippet、autocomplete、search summary、generated code、language model 都能快速给你一个内部自洽的故事。Plausibility 很适合生成 hypothesis，不适合充当 provenance。

### 何时触发

当 material decision 依赖某个 factual claim，而这个 claim 没有从真正拥有/观察该事实的来源得到验证时触发。例如：

- 从相邻 hook/type name 推断“这个 Host hook 一定带 `sessionID`”，却没读真实 interface；
- 凭 memory 说“这个 API miss 时 return null”，当前版本 source 明明可查；
- 没读真实 file/schema/config 就描述其内容；
- 根据直觉推断 framework lifecycle/order guarantee，而不看 source/docs/observation；
- migration 只看当前 code 猜 old data shape，不检查 durable sample/version rule；
- AI/model 对 library、compiler output、runtime behavior、repository state 的回答被直接当 authority；
- security/capability boundary 从 prompt text 或名字推断，而不看 runtime enforcement；
- failure cause 因为“很像以前那个问题”就被当事实，尚无 discriminating evidence。

### 不应触发

- Statement 被明确标记为 hypothesis，只用于选择下一步 investigation，没有被当成 established fact。
- Authoritative contract/source 已经在当前上下文中读过，而且确实对应 exact claim 与 current version。
- Claim 不 load-bearing：即使猜错也不会改变当前 decision/behavior。
- Direct verification 当前确实不可能或成本不成比例，uncertainty 被保留为 uncertainty，并且只做 reversible decision。
- 问题本身是 normative（“我们应该选 X”），不是 factual（“系统现在就是 X”）。Normative disagreement 需要 judgment，不是 source lookup。

### 与相邻规则区分

`guess-based-fix` 是改变系统直到 symptom 改善。`guessed-not-verified` 更早发生：一个不确定 premise 被静默提升成 truth。

`blind-edit` 是还没理解足够 ownership/context 就开始 mutation。Guess 可能导致 blind edit，但本规则专门命名 epistemic violation。

`unverified-completion-claim` 看最终 completion statement 是否比 evidence 强。本规则中的 unsupported fact 可以在 reasoning chain 很早就出现。

### 判定程序

找到后续 decision 真正依赖的那句话，然后问：

1. 它是 factual/descriptive statement，还是 proposal？
2. 如果它错了，design、patch、migration、review 或 conclusion 会变吗？
3. 谁/什么拥有这个 fact——source code、current docs/spec、durable artifact、runtime observation、external authoritative system？
4. 真正 owner 是否已经被 inspect/observe？

如果 claim load-bearing，而第 4 项答案是否定的，它就仍然只是 hypothesis，无论听起来多合理。

### 例子

- positive：“OpenCode `tool.definition` 有 session context，所以 tool description 可以 per-session localization。”实际 hook type 从未读取，而且只带 `toolID`。
- positive：“database column 从不为 null”只根据 application type 推断，migration 前没看 old rows。
- positive：“这是 timeout 问题”只因为加 timeout 后有一次 green，从未检查 timing trace/completion signal。
- positive：generated answer 说某 dependency 提供一个 option，代码直接按它写，却没查 installed version。
- near-miss：“Hypothesis: event 可能在 subscription 前触发；检查 hook order。”在 evidence 到来前始终保持 provisional。
- counterexample：owning type/source 已读，implementation 直接依据 exact current contract。

## 建议

### 现在该做什么

先停止在 guess 下游继续花 design 成本。

命名那个 load-bearing premise，找到真正拥有它的 source，然后在做更多 irreversible decision 之前把 premise 查清。如果暂时无法 settle，就把 statement 降回 hypothesis，并且只选择在这种 uncertainty 下仍安全的动作。

### 为什么重要

未验证 premise 是一种会**乘法增长**的 debt。

一个关于 hook、schema、lifecycle、persistence rule、permission surface 或 API 的错误 assumption，可以生成几页内部非常一致的 implementation。正因为下游 pieces 都共享同一个 false premise，它们彼此会高度“互相印证”，从而让错误更难被察觉。

所以“但 reasoning 很有道理”并不令人安心。Reasoning quality 不能替代 provenance。

发现 premise 错误最便宜的时刻，是 architecture 还没围着它凝固之前。读 owner code 二十行，可能省掉几百行本来要为不存在的 world 编写的 adapter、test、compatibility logic 与 migration machinery。

### 修复策略

先分类 claim，再去最近的真正 owner：

- **repository behavior/type/API：**读 owning source 与当前 installed interface；
- **external library/runtime contract：**读当前 primary docs/source；如果真实 behavior 才是 contract，就做最小 discriminating experiment；
- **durable data/schema：**检查真实 versioned records/samples 与 migration rules；
- **host/framework lifecycle：**读 hook/event implementation，或捕获 focused trace；
- **security/capability：**检查 runtime enforcement，不要只信 prose；
- **failure cause：**取得能把 named cause 与 plausible alternative 区分开的 observation。

优先使用“足以 settle 当前 decision 的最便宜强 source”。如果 canonical source 五行就能说明 contract，不要为了显得严谨跑巨大 e2e；如果 live behavior 才真正决定 property，也不要把 documentation fetish 化。

当这个 fact 未来会重复使用，把新 knowledge 编码进 rightful owner：contract test、type、invariant 或 durable documentation，避免后续工作再次支付同样 discovery cost。

### 决策分支

- **Owner 很容易 inspect：**现在就看，不要继续靠 memory 推理。
- **只有 runtime 能 settle：**设计最小可证伪 observation，并记录 result。
- **多个 authoritative sources 冲突：**保留冲突，找出当前实际 boundary/version 到底由谁 governing。
- **当前无法验证：**保持 claim 显式 uncertain，优先 reversible design，不要把 guess 固化进 schema/API/state。
- **Premise 不 material：**停止调查，不是每个 uncertainty 都值得花钱。
- **问题是 normative 而非 factual：**交回 rightful decision authority，不要寻找一个根本无法决定 value judgment 的 source。

### 常见假修复

- 只搜索能确认预期答案的 snippets。
- 再问一遍同一个 model/person，把重复 plausibility 当 independent evidence。
- 在验证之前就把 assumption 写成 type/comment/abstraction，让未来 reader 继承 guess 当 doctrine。
- 引用 generic docs，而 installed version / host fork 实际不同。
- 为一个未验证 historical shape 先造 compatibility layer “以防万一”。
- 明明 canonical source 几行就能 settle，却跑一个巨大 end-to-end experiment，制造更多 noise。
- 读 adjacent source，而不是拥有 claim 的 source，然后把这叫 verification。

### 验证

修复后的 decision 应有一条足够短、可以说出口的 provenance chain：

> 我们依赖 X，因为 owner/source Y 在 condition/version Z 下建立了 X。

当 behavior 而不是 written contract 才是决定性事实时，observation 必须可重现到足以区分 X 与一个现实 alternative。

然后审下游 work：删除那些只因为旧 guess 扩散才存在的 compatibility、branch、comment、abstraction。

Invariant：

> Load-bearing facts 先获得 provenance，再获得 architecture。

### 完成条件

Hypothesis 可以大胆、便宜、快速。

Fact 必须贵到值得 evidence。

Codebase 中不再有一个看起来很自信的结构，其 foundation 只是：“我当时以为它大概这样工作。”

### Nudge

熟悉的故事不是事实。

Premise 在获得 architecture 之前，先让它交 evidence 的房租。

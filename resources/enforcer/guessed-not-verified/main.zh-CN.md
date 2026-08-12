# guessed-not-verified — Main

## 现在该做什么
先停止在 guess 下游继续花 design 成本。

命名那个 load-bearing premise，找到真正拥有它的 source，然后在做更多 irreversible decision 之前把 premise 查清。如果暂时无法 settle，就把 statement 降回 hypothesis，并且只选择在这种 uncertainty 下仍安全的动作。

## 为什么重要
未验证 premise 是一种会**乘法增长**的 debt。

一个关于 hook、schema、lifecycle、persistence rule、permission surface 或 API 的错误 assumption，可以生成几页内部非常一致的 implementation。正因为下游 pieces 都共享同一个 false premise，它们彼此会高度“互相印证”，从而让错误更难被察觉。

所以“但 reasoning 很有道理”并不令人安心。Reasoning quality 不能替代 provenance。

发现 premise 错误最便宜的时刻，是 architecture 还没围着它凝固之前。读 owner code 二十行，可能省掉几百行本来要为不存在的 world 编写的 adapter、test、compatibility logic 与 migration machinery。

## 修复策略
先分类 claim，再去最近的真正 owner：

- **repository behavior/type/API：**读 owning source 与当前 installed interface；
- **external library/runtime contract：**读当前 primary docs/source；如果真实 behavior 才是 contract，就做最小 discriminating experiment；
- **durable data/schema：**检查真实 versioned records/samples 与 migration rules；
- **host/framework lifecycle：**读 hook/event implementation，或捕获 focused trace；
- **security/capability：**检查 runtime enforcement，不要只信 prose；
- **failure cause：**取得能把 named cause 与 plausible alternative 区分开的 observation。

优先使用“足以 settle 当前 decision 的最便宜强 source”。如果 canonical source 五行就能说明 contract，不要为了显得严谨跑巨大 e2e；如果 live behavior 才真正决定 property，也不要把 documentation fetish 化。

当这个 fact 未来会重复使用，把新 knowledge 编码进 rightful owner：contract test、type、invariant 或 durable documentation，避免后续工作再次支付同样 discovery cost。

## 决策分支
- **Owner 很容易 inspect：**现在就看，不要继续靠 memory 推理。
- **只有 runtime 能 settle：**设计最小可证伪 observation，并记录 result。
- **多个 authoritative sources 冲突：**保留冲突，找出当前实际 boundary/version 到底由谁 governing。
- **当前无法验证：**保持 claim 显式 uncertain，优先 reversible design，不要把 guess 固化进 schema/API/state。
- **Premise 不 material：**停止调查，不是每个 uncertainty 都值得花钱。
- **问题是 normative 而非 factual：**交回 rightful decision authority，不要寻找一个根本无法决定 value judgment 的 source。

## 常见假修复
- 只搜索能确认预期答案的 snippets。
- 再问一遍同一个 model/person，把重复 plausibility 当 independent evidence。
- 在验证之前就把 assumption 写成 type/comment/abstraction，让未来 reader 继承 guess 当 doctrine。
- 引用 generic docs，而 installed version / host fork 实际不同。
- 为一个未验证 historical shape 先造 compatibility layer “以防万一”。
- 明明 canonical source 几行就能 settle，却跑一个巨大 end-to-end experiment，制造更多 noise。
- 读 adjacent source，而不是拥有 claim 的 source，然后把这叫 verification。

## 验证
修复后的 decision 应有一条足够短、可以说出口的 provenance chain：

> 我们依赖 X，因为 owner/source Y 在 condition/version Z 下建立了 X。

当 behavior 而不是 written contract 才是决定性事实时，observation 必须可重现到足以区分 X 与一个现实 alternative。

然后审下游 work：删除那些只因为旧 guess 扩散才存在的 compatibility、branch、comment、abstraction。

Invariant：

> Load-bearing facts 先获得 provenance，再获得 architecture。

## 完成条件
Hypothesis 可以大胆、便宜、快速。

Fact 必须贵到值得 evidence。

Codebase 中不再有一个看起来很自信的结构，其 foundation 只是：“我当时以为它大概这样工作。”

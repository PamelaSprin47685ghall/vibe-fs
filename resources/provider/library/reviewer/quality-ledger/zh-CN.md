# Examiner's Ledger

Class：Binding Ledger

Purpose：规定在判断一项工作是否赢得 acceptance 时，必须认真考虑的方向。

Authority Boundary：这本 Ledger 不规定 report format，不授予 mutation 或 execution authority，也不暴露 review protocol mechanics。它引导 judgment，但不会取代 judgment。

这本 Ledger 属于被托付进行 judgment 的人。

它不规定 report format。
它不告诉你必须写多少段。
它不要求每次 review 都机械写出八个 heading。
它也不会扩大你可以 touch、execute 或 change 的范围。

它教你：在决定工作是否赢得 acceptance 时，哪些方面值得注意。

这些 entries 不是八个需要逐一勾选 Pass 的方框。
它们是八个方向，unfinished 或结构失当的工作可能从这些方向暴露出来。
在思考中走完整本 Ledger。只有真正值得说的内容才写出来。

短 review 可以完整。
长 review 也可能仍然错过重点。
衡量标准不是产生了多少批评。
衡量标准是 judgment 的质量。

Acceptance 必须被赢得。
Rejection 同样必须被赢得。

## Judgment 的重量

WorkRecord 是 evidence。Test result 是 evidence。Clean build 是 evidence。Diff 是 evidence。有说服力的 explanation 是 evidence。Source code 也是 evidence。
这些东西单独都不是 judgment。

你的任务，是判断这些 evidence 对“真正被要求完成的工作”究竟建立了什么。

不要奖励 confidence。
不要惩罚 unfamiliarity。
不要仅仅因为你自己会用另一种方式写代码，就拒绝。
也不要仅仅因为 implementation 很精致，就接受。

用户真实的 requirement 始终是尺度。
当前 review charge 可以让你把注意力更多放在某一部分。
它不能抹掉仍属于整个 request 的 obligations。

Lens 可以缩窄视野。
它不能缩窄 responsibility。

## I. Language & Algorithms

检查 implementation 是否善用它所处的 language，并使用与真实问题相匹配的 mechanisms。

Idiomatic code 不是模仿流行风格的代码。
而是与语言合作、而不是和语言对抗的代码。

检查所选 algorithm 是否匹配问题真实的 shape。
一个逻辑上正确的 algorithm，如果沿着 task 重要的维度产生灾难性成本增长，也可能构成 defect。

检查真正发生的 trade-off。

值得怀疑的迹象包括：
反复转换 representation；手工重建 platform 已经能够表达的 behavior；为了某个单一 call site 的方便而选择 data structure；隐藏的 quadratic work；在不存在 independence 的地方制造 concurrency；在工作独立时强制 serialization；混杂的 error conventions；用低层 manipulation 补偿更早的 abstraction mismatch。

但 novelty 本身不是 defect。
当标准 mechanism 无法表达必要 semantics 时，一个 custom mechanism 可能正是正确选择。

## II. Simplicity

Simplicity 不是最少的 lines、files 或 abstractions。
Simplicity 是删除那些没有赢得存在理由的 complexity。

每一个 abstraction 都要求未来 reader 学会一个 distinction。
每一个 compatibility layer 都要求未来 maintainer 同时维护两个 worlds。

好的 abstraction 让一个重要 truth 更容易只陈述一次。
坏的 abstraction 只是给 accident 起了名字。
好的 state variable 表示一个无法安全推导的 fact。
坏的 state variable 记住世界本来已经知道的东西。

如果某件事能够从 durable facts 无歧义地导出，就应当怀疑是否有必要把它存成另一个 truth。

Radical deletion 并不自动等于 simplicity。
删除一个明确 concept，可能反而让剩余代码依赖 invisible convention。

Simplicity 不是贫乏，而是在不丢失 meaning 的前提下保持 economy。

## III. Structure

Structure 是 responsibility 的放置方式。
结构干净的系统，需要 boundary 对应真实的 responsibility distinction。

当同一个 decision 在多个 layers 被重复作出时，保持警惕。
当 lower layer 知道 higher-level business action 为什么发生时，保持警惕。
当 transport code 决定 semantic policy 时，保持警惕。
当 domain truth 从 rendered prose 反推时，保持警惕。
当 adapter 变成第二个 owner 时，保持警惕。
当两个 modules 每次都必须一起修改时，保持警惕。

也要警惕为了 architecture 本身而表演 architecture。
新 interface 不会自动成为 boundary。
DI layer 仅仅插入 indirection，并不会凭空创造 distinction。

当 program 的 shape 跟随 responsibility 的 shape 时，structure 才是好的：
一个 semantic decision 只有一个 owner；
observations 可以向内流动，但不会因此取得 decision rights；
effects 发生在由 contract 描述其 effect 的 boundary 之后；
只有 machinery 需要的 state 保留在 participant-facing horizon 之后；
causal relationships 被明确表达，而不是从 arrival order 推断。

当跨过一个 boundary 会改变“什么可以正当地被知道、决定或执行”时，这个 boundary 才真正赢得了存在理由。

## IV. Granularity

不存在一个天然有德性的行数。
30 行并不会天然优于 80 行。

根据 semantic pressure 判断 granularity，而不是计数。
当彼此独立的 responsibilities 被迫共享一个 lifecycle 时，一个 unit 可能太大。
当一个简单 idea 被碎裂到很多 pieces 中时，一个 unit 也可能太小。

问：
这一部分是否可能因为与其余部分无关的原因而变化？
这个 unit 是否同时持有若干种不同类型的 knowledge？
Extraction 是否揭示了一个真实 concept，还是只是移动 syntax？

重复的 mechanical structure 可能值得 extraction。
重复文字并不总是意味着重复 meaning。

在 responsibility 改变的地方切分，而不是在尺子到达某个数字时切分。

## V. Tests & Behavioral Evidence

Tests 是工作赢得 behavior claims 的一种方式。
正确的数量与种类，取决于什么发生了变化，以及什么必须被建立。

不要只问：“有没有新增 tests？”
要问：“哪个 behavior claim 需要 proof，而什么 evidence 真正证明了它？”

当一个 test 的 failure 能够区分 intended behavior 与一个 plausible defect 时，它才有价值。
只执行到新代码行的 test 可能几乎什么都没证明。
复制 implementation logic 的 test 可能在 contract 错误时仍通过。
断言 incidental ordering、timing 或 internal structure 的 test 可能冻结 accidents。

重要 boundaries 包括：failure 与 recovery；empty 与 maximal；concurrent events；persistence 与 restart；idempotency；compatibility；security；partial success；cancellation；stale state；malformed input；version change。

Execution evidence 有 provenance。
不要因为代码看起来正确，就推断 command 已通过。
不要因为 test file 存在，就推断 test 已运行。
不要从 obsolete run 推断当前 success。

Passing test 只证明该 test 能够区分的内容，不会更多。

## VI. Logic, Reliability & Boundaries

当 assumptions 不再合作时会发生什么？
Operation 进行到一半失败？
Duplicate request？
Independent events 以任意顺序抵达？
Process 在 prepare 与 commit 之间死亡？
Cancellation 之后 callback 仍然抵达？
被操作的对象在 observation 后又改变？
旧 durable state 被 replay？

并非每个 task 都需要复杂 recovery。
当 failure 没有 meaningful partial effect 时，强行引入 recovery 本身也可能是 defect。

需要警惕的 causal mistakes：
completion 不等于 correctness；arrival 不等于 causality；history 不等于 current state；successful write 不等于 successful outcome；timeout 不能证明 work 已停止；retry 不会自动成为新的 semantic act；capability 不等于 entitlement。

寻找会被 interruption、reordering、duplication 或 stale observation 破坏的 invariants。
寻找只靠 prose 约束 security boundary、但 runtime capability 实际更宽的地方。
寻找 machine state 泄漏到外部，迫使 participants 解码 internal unions 的地方。

不要为了 imaginary catastrophes 要求额外 machinery。
守住真实世界拥有的 boundary。
不要仅仅为了表现谨慎，就发明另一个世界。

## VII. Caller Ergonomics

Internals 正确，并不自动意味着 implementation 完整。
总有人要长期生活在它的 surface 上。

好的 surface 让正确 action 自然发生。
坏的 surface 迫使 caller 在行动前先重建 internal machinery。

同一个 tool name 无论在哪里说出，都应表示同一个 act。
Field 应当因为 caller 需要它而存在，而不是因为 implementation 碰巧存储它。
当 system 已经知道后续 instruction 时，不应把 state label 暴露出去。
Identifier 不应仅仅因为 machine 需要 correlation 就跨过 boundary。
Return value 不应回声式返回 caller 刚刚提供的内容。

Compatibility 重要，但 compatibility 不等于崇拜每一个历史 accident。
Surface 是 program logic 的一部分。它给 caller 增加的负担是真实 complexity。

## VIII. Completeness

Completeness 问的是：这项工作是否完成了使它诞生的 obligation。
这与“central implementation 是否已经存在”不是同一个问题。

警惕用语言伪装 abandonment：
把 requested result 必需的工作称为 “out of scope”；
把已经存在的 requirement 称为 “future enhancement”；
把当前 implementation 引入的 defect 称为 “known limitation”；
在 invariant 仍然 broken 时称为 “good enough”。

但也不要把每个可能 improvement 都变成 unfinished work。
Repository 可以包含与当前 charge 无关的旧 imperfection，而不使当前工作自动无效。

问 causal question：
如果这件事保持现状，requested result 是否仍然 materially incomplete？

Completeness 是走完当前这条 road，而不是铺平从这里能看到的每一条 road。

## 关于 Materiality

Reviewer 必须区分 defect 与 preference。

这不是忽略小事的许可。
一个字符的错误可能使 protocol 无效。
一个缺失 await 的改动可能很小，却构成严重 defect。

Edit size 与 consequence materiality 是不同的量。

当 concern 与以下内容有关时，它值得影响 judgment：用户 requirement；correctness；invariant；behavior；security；recoverability；meaningful boundary 上的 maintainability；public/internal contract；或未来工作被实质变难。

不要为了证明 taste 合理而发明 materiality。
也不要因为 fix 很小就否认 materiality。

Small 不等于 harmless。Large 不等于 important。追踪 consequence。

## 关于 Evidence

Evidence 有 strength、scope 与 age。
让每一种 evidence 只承担它真正能够承载的 claim。
当 distinction 重要时，优先 direct evidence。
一个 decisive counterexample 可以迅速结束一条 inquiry。
没有 counterexample 并不会自动变成 proof。

Evidence 应按照它实际能够区分什么，成比例地赢得 confidence。

## 关于 Simplicity 与 Thoroughness

Thoroughness 不意味着调查一切。
当 decisive material defect 已经建立，不要购买 ceremonial evidence。
当还没有 defect 出现，但 acceptance 依赖 unsupported claims 时，继续。
当若干 independent observations 都值得进行时，一起取得它们。
当下一项 observation 只有在理解前一项 observation 的 semantics 后才有意义时，先理解前者。

Economy without timidity。Doubt without ritual。

## 关于 Existing Imperfection

旧代码可能 awkward。Tests 可能遵循你自己不会选择的 conventions。
Review 不是重新设计当前工作触碰到的一切的许可证。

区分：
阻止 requested result 正确成立的 pre-existing condition；
被新工作 materially worsened 的 pre-existing condition；
新工作正当地依赖的 pre-existing condition；
与当前 obligation 无关的 neighboring imperfection。

前三种可能重要。第四种并不会自动属于你要 prosecution 的范围。
根据 obligation 判断 continuity，而不是根据 habit。

## 关于 Passing Tests / Elegant Work

Green suite 值得尊重。它是有人花费资源取得的 evidence。
不要为了表演 skepticism 而随意否定它。
但也永远不要要求 green tests 证明它们并没有被设计来区分的事情。

Elegant code 仍然可能错误。
不要让 presentation 借走 evidence 尚未赢得的 confidence。
但当两个 designs 都满足相同 obligations 时，elegance 也不是完全无关；不必要 concepts 更少的一方通常更 maintainable。
错误在于把 elegance 当成 self-authenticating。

## 关于 Rejection / Acceptance

Rejection 不是 punishment。
有用的 rejection 会指出哪一个 obligation 尚未被赢得。
让 defect 可定位，并解释 consequence。
除非 implementation detail 本身属于 requirement，否则不要强行规定 repair pattern。

区分 “Use my preferred pattern” 与 “The current pattern permits two writers for a fact that must have one owner.”
前者是 taste。后者是有明确 reason 的 defect。

Acceptance 不是“没有抱怨”。
它是在 reasonably required evidence 下，判断没有 material obligation 仍然 unsupported 或 violated。
接受之前问：什么会让这项工作仍然 materially incomplete？
什么重要 failure 可能没有被现有 evidence 揭示？
我是否把 familiarity 当成 correctness？
我是否仅仅因为 Reviewer 好像总该找到点什么，就在制造 concern？

一个不能接受好工作的 Reviewer 并不严格，而是不准确。

Judgment 的目的不是 rejection，而是 discrimination。

## 八项合看

这些 entries 彼此约束。
Language 没有 simplicity，会变成 cleverness。
Simplicity 没有 structure，会变成 compression。
Structure 没有 granularity，会变成 fragments museum。
Granularity 没有 completeness，会优化 pieces 而丢掉 task。
Tests 没有 logic，会认证错误 behavior。
Logic 没有 ergonomics，会让 correctness 难以被安全使用。
Ergonomics 没有 completeness，会让 unfinished feature 变得很容易调用。
Completeness 没有 restraint，会变成 scope expansion。

不要最大化某一个 entry。
寻找一种 work，使这些 entries 与真实 obligation 彼此一致。
在思考中走完整本 Ledger。只写下 work 真正让你值得写的内容。

## Closing Leaves

第一个 answer 不是最古老的 truth。
完成的 implementation 不是正确 implementation 的 proof。
Passing suite 不是完整工作的 proof。
Strange design 不是 bad design 的 proof。
Small defect 不一定 harmless。
Preference 不是 requirement。
Report 不会仅仅因为很 confident 就成为 evidence。
Observation 在 judgment 将它连接到真正重要之物以前，还不是 defect。

Acceptance 必须被赢得。
Rejection 同样必须被赢得。
根据真实存在的 obligation，使用真实存在的 evidence，判断真实存在的 work。

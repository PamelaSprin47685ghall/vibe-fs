# Kolmogorov 之书

Class：Handbook

Purpose：积累关于 representation、boundary、change、evidence 与 verification 的工程判断。

Authority Boundary：本书只教授如何在已经托付给你的 authority 内把事情做得更好。它不会扩大 scope，不会授予 execution 权，也不会把设计偏好变成 product requirement。

## 选择最简单且足够的表示

复杂度不能用文件数、行数、类型数或函数数直接衡量。
这些都是观察，不是 verdict。

真正要问的是：这个 representation 必须承载多少不可约的意义？
好的设计会让每个重要区别都有一个清楚的归属，并让无效组合难以被表达。

不要仅仅为了减少结构，就把不同含义压进同一个 primitive。
也不要仅仅因为几段代码看起来相似，就创造 framework。
只有当 abstraction 能保护 semantic boundary，或删除反复出现的 reasoning 时，它才真正赢得了存在理由；仅仅缩短文字不够。

行数与函数大小可以作为有用的建议性信号，但永远不是“这个模块一定错误”的硬证据。
一个很大的 coherent owner，可能比若干把同一 invariant 打散的小文件更好；一个很小的文件，也可能正是干净的 legal seam。
用 size 去提出 ownership 问题，而不是用它制造 refactor。

## 区分本质复杂度与偶然复杂度

Essential complexity 来自问题本身：真正独立的 states、真实 failure modes、authority boundaries、causal relationships，以及 restart 后仍必须存在的信息。

Accidental complexity 来自选择的 representation：重复 state、没有 semantic work 的 translation layers、根据已经存在的事实重新拼装出来的 lifecycle flags、任何 supported world 都不需要的 compatibility branches，以及跨多个 owner 扩散的 control flow。

不要以“简单”为名删除本质区别。
也不要仅仅因为偶然复杂度已经存在，就替它辩护。

## 在 abstraction 之前先画 semantic boundary

在选择 class、module、service 或 helper 之前，先问：谁拥有这个 fact？谁可以改变它？谁可以观察它？crash 之后什么必须留下？

没有 semantic responsibility 的 boundary 多半只是仪式。
能够保护 authority、provenance、persistence 或 stable contract 的 boundary，即使实现很小也有价值。

让核心 vocabulary 靠近 domain。
只在真实边界做 translation。
不要让 transport 或 framework shape 变成问题本身的模型。

## 用类型系统排除虚假的世界

优先选择让 illegal state 根本无法出现的 representation，而不是依赖晚期 convention 去检查它。

当 states 真正互斥时，使用 algebraic alternatives。
当混淆 identifiers 会跨越 ownership 或 causal boundary 时，让它们保持不同类型。
当 absence 本身有意义时，把 absence 明确表达出来。

不要用一组 booleans 暗中编码 state machine。
不要仅仅为了读取方便，就把 derived fact 和它的 source 同时存储。
如果一个 value 可以从 durable truth deterministic 地推导出来，优先推导；只有 measurement 证明另一种 representation 真有必要时才改变。

## 把纯决策与 effect 分开

有用的 architecture 往往拥有一个决定“应该发生什么”的 pure center，以及一个真正执行 I/O、time、process、network 或 persistence 的 effectful shell。

目标不是追求仪式性的 purity。
目标是：无需复现整个世界就能测试 decision，并让 effects 明确归属于真正拥有它们的 boundary。

当 time、randomness、process launch 与 external observations 能改变行为时，把它们作为输入注入。
不要让 ambient state 悄悄决定 domain truth。

## 优先表达 declarative truth，而不是依靠 procedural reconstruction

如果系统可以直接陈述一个 durable fact，就不要要求未来代码通过一串偶然 events 去猜出它。

Command 请求 action。
Event 记录什么已经成为事实。
不要混淆二者。

Command 可能在预期 event 发生前失败。
Event 也可能在 command caller 已经消失之后才抵达。
Persistent memory 应记录 recovery 真正需要的 facts，而不是把每个 implementation gesture 都当成 domain meaning 来 replay。

## 围绕 ownership 与 causality 建模 concurrency

当独立工作拥有独立 ownership，而共享 mutation 明确可见时，concurrency 最安全。

不要为了避免思考 interleavings 就串行化一切。
也不要并行化那些 correctness 依赖 hidden order 的工作。

当顺序重要时，表示真正的原因：dependency、compare-and-swap witness、barrier、ownership transfer，或其他明确 relation。
Scheduler order 不能替代 causality。

设计 replay 与 reconciliation，使独立 histories 根据 facts 收敛，而不是根据哪个 callback 恰好先到。

## 让 persistence 与 replay 讲同一个故事

Durable state 必须足以恢复真正重要的 semantic state。
Restart 不能发明 success、抹掉 material failure，或依赖已经不存在的 process-local flag。

为 durable facts 使用 stable identities。
在可能 replay 的 boundary 明确建模 idempotence。
当 recovery 有歧义时，fail closed，而不是猜测。

当一个 representation 通过 clean break 被替换时，删除旧 provider surface，而不是迫使未来每一层同时理解两套 ontology。
只有 recovery 真正需要时，historical decode 才可以保留在内部。

## 调查原因，而不只处理症状

失败的 test、exception、timeout、race 或意外 output 都是 evidence。
它们还不是 root cause。

沿着 ownership 与 data path 追踪，直到“改变你提出的原因”确实能够解释已经观察到的 effect。
优先修复被破坏的 invariant，而不是只压住可见 symptom。

当修复改变了 protocol boundary 时，在真正失败的 boundary 增加 permanent regression test。
不要把一次性 probe 当成 closure 的证明。

## 保存耐久知识，但不要创造第二个真源

记录那些昂贵、并且会跨 assignments 反复出现的区别。
不要把 operational state 写进 doctrine。
已有 canonical technical specification 时，从真正的 owner 组合它，而不是在 handbook 里复制出另一个竞争真源。

好书让未来 judgment 更便宜。
它不会让未来每个问题都被强迫长得像这本书。

## 让名字成为 semantic documentation

名称应当揭示程序真正依赖的区别。
当含义已经改变时，不要继续使用只记录旧 implementation accident 的名字。
避免 generic buckets——它们唯一的承诺只是“互不相关的东西都能塞进去”。

当旧名字教授错误 ontology 时，rename 不是 cosmetic。
反过来，如果 ownership 仍然错误，换一个新名字也修不好设计。

## 用测试保护行为与边界

围绕必须保持成立的 algebra 编写 deterministic tests。
当 adapter 与 framework behavior 本身属于 contract 时，使用 integration tests。
只有少数必须由真实 Host 才能证明的 causal paths，才需要 end-to-end tests。

一个 failing test 的价值，在于它能区分缺失的 behavior。
一个 passing test 的价值，只等于它对自己声称要阻止的 regression 实际具有多强的失败能力。

不要削弱 tests 来让 implementation 通过。
不要抬高 timeout 来隐藏 broken causal wait。
不要把 flaky test 重复到 probability 看起来像 evidence。

Verification 应形成 ladder：pure invariants、component contracts、integration boundaries，然后是能够证明剩余 uncertainty 的最小 real-host path。

## 保持 scope discipline

连贯地完成 entrusted change。
不要把附近的 defect 当成重设计无关 subsystem 的许可。
也不要仅仅因为修正一个已知 defect 会跨越多个文件，就把它保留下来。

正确的 scope 由 obligation，以及让该 obligation 成真的必要 invariants 决定，而不是由 diff size 决定。

最简单且足够的设计，并不是最小的 artifact。
它是 accidental machinery 最少、同时仍能讲完整真相的 representation。

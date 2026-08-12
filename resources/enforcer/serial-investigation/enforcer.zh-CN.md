# serial-investigation — Enforcer

Serial investigation 的问题，不是“调查时一步一步思考”，而是**已经能够独立提出的问题**仍被强行排成一条时间链。

调查也有 dependency graph。若三个问题都能从当前已有 context 直接写出来：

- 哪个模块拥有这个 state？
- 哪个 test 覆盖这条 contract？
- Host 真正返回的 schema 是什么？

那么让第二个问题等第一个答案、第三个再等第二个，并没有增加严谨，只是在凭习惯发明 critical path。

更隐蔽的代价是 anchoring。第一个结果先到，人就容易围绕它构造故事；后面的独立 evidence 反而被当“佐证”或“例外”，而不是平等竞争的事实。并行收集独立证据，往往不仅更快，也更能阻止第一印象抢走 narrative ownership。

以下情形触发：

- 多个 file read / grep / source inspection / log query 都已完全可描述，却逐个执行；
- 同一 failure 的几个独立 hypothesis 各自有 discriminating observation，却只追一条直到走不通；
- 可以同时验证 docs、implementation、test、runtime contract，却按“先看完一类再看下一类”串行；
- remote/source 查询互不依赖，却每次 await 后才发下一次；
- 调查耗时主要是无谓等待，不是真正的信息依赖。

不要误杀真正 sequential reasoning。如果下一条 query 的关键词、范围或真假前提必须由上一条结果决定，那就是实在 dependency。资源 capacity 已满时也应排队，而不是为并行而越过 limit。会修改同一环境的 destructive probe 也不能假装 independent。

与 `serial-when-parallel` 区分：它是通用 execution scheduling smell；本规则专门关注 evidence gathering，因为 investigation 还有一个额外风险——**先到的 evidence 会锚定解释**。`unbounded-fanout` 是反方向：识别了 independence，却完全不尊重容量。

最实用的做法是先画“问题图”而不是“命令列表”：只有一个 answer 真正决定另一个 question 如何被提出时才连 edge。没有 edge 的问题属于同一 evidence wave，可以一起发出；wave 回来后先 synthesis，再决定下一批 dependent questions。

并行也不能替代问题质量。一次发二十个模糊 grep 不是严谨，只是同时制造二十份噪声。

> 调查应该按信息依赖串行，而不是按人的手部习惯串行。能同时求证的事实，应该在同一个叙事形成之前一起到场。
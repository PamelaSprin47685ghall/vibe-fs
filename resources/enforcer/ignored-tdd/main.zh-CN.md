# ignored-tdd — Main

当 change 是可由 behavioral test 先表达的新/变更行为时，先让 requirement 独立变红，再教 implementation 如何变绿。

写最小 caller-visible scenario，先在 old behavior 上运行，确认 failure 原因正是“requirement 尚未满足”，而不是 fixture/import/setup 事故。然后做最小 production change，让**同一条 test**转绿。

这里的顺序服务于独立性，不服务于 ceremony。

常见假修复：

- implementation 已完成后写 test，然后凭想象说“它以前肯定会红”；
- 为制造 red，断言 private helper/field，而不表达 public requirement；
- red 原因其实是 test setup 错，却没修就开始 production edit；
- implementation 不符合 test 时，先把 expectation 放宽；
- pure refactor 明明有充分 coverage，却硬造一条“先红” test；
- spike 明明准备丢弃，却为了形式测试每个探索分支。

验证 TDD evidence 最简单：old behavior red-for-right-reason，new behavior green。若是 bug fix，保留这条 test 作为 regression；若行为有普遍 law，再补 property test。

Pure refactor 不要改 test expectation；它的 proof 是原 suite 在重构前后保持 green。Characterization work 则先用 test 描述 existing reality，再在真正 behavior change 时新增 red requirement。

不要把“测试先行”误解成“测试拥有产品 contract”。Requirement 仍来自用户/规范/domain；test 是其 executable witness。如果 test 与真正 contract 冲突，应改 test，但原因必须是 contract 改/原 test 错，而不是 implementation 想要更容易的 examiner。

完成时，behavior change 有一份独立于实现产生的证据：它曾经能准确指出旧世界哪里不够，然后准确接受新世界。

> Red 不是仪式性的颜色；它是 requirement 在 implementation 出现之前已经能够反驳旧行为的证据。
# big-batch-intent — Main

按 semantic obligation 拆 batch，不按文件数量拆。

把 change 列成几条独立 claim：behavior A、migration B、refactor C、dependency D。对每条问：它是否有自己的 owner、proof、rollback boundary？若有，就尽量让它成为独立可验证单元；只有共享一个真正 atomic invariant 的 edits 才绑在一起。

常见拆法：

- 先加兼容新 path + tests，再迁 callers，再删旧 path；
- 先建立新 contract/boundary，再做内部 refactor；
- behavior fix 与 unrelated cleanup 分开；
- dependency upgrade 与 semantic feature 分开，除非 feature 必须依赖该 upgrade；
- speculative diagnosis changes 一次只保留 causal proven 的一项。

常见假修复：

- 为“每个 commit 小”把一个 atomic semantic change 切成无法独立 green 的碎片；
- 反过来，几十个 unrelated edits 放一起，只因“反正都在这个模块”；
- 用 feature flag 把 batch 表面拆开，实际两套 path 长期共存；
- 把 proof 推迟到 batch 末尾，前面的每一项都没有独立 evidence；
- 大范围 mechanical formatting 混进 behavioral diff，掩盖真正 change。

验证每个 unit：能否单独说明 user-visible/internal invariant、单独让相关 tests green、单独 rollback 而不破坏其他已交付 obligations。若不能，说明它可能确实属于同一 atomic batch；这不是失败。

不要把拆分当 project-management 美学。真正目标是降低 simultaneous uncertainty，让 failure 能定位、review 能按 obligation 判断、rollback 不必牺牲无关正确工作。

完成时一个 batch 可以很大，但里面的 edits 都被同一不可分割语义约束；或者 batch 很小，因为独立假设已经被拆到各自 proof boundary。

> 好 batch 不是“小”，而是每次只要求世界同时相信尽可能少的新东西。
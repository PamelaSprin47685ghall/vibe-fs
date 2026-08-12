# ignored-tdd — Enforcer

Ignored TDD 的核心，不是“作者没按宗教顺序先写 test”，而是一个本来能够由 failing behavioral specification 先独立表达的 change，implementation 已经先存在，于是后写的 test 很容易被现成代码反向塑形。

Red 的价值是 counterfactual：它证明两件事——旧 behavior 确实不满足新 requirement；这条 test 确实有能力识别这种缺失。没有亲眼见过正确原因的 red，post-hoc test 可能只是把当前实现描述了一遍。

以下情形触发：

- new/changing behavior 先实现，再补一条从未在 old code 上失败过的 test；
- test assertion 明显围绕新 implementation structure 写，而非 public behavior；
- bug/feature 的 requirement 本可先变成 failing example，却直接从 diff 开始；
- 代码 green 后才问“该测什么”，最终只测自己已经实现的路径。

不要误杀 pure refactor：已有 behavioral coverage 足够、semantics 明确不变时，不需要制造假 red。Characterization test 先钉住现状、后续再加 red change 也合法。探索 spike 若明确 disposable、不作为 production delivery，也不需要边写边 TDD。

与 `missing-regression-test` 区分：具体 defect 已发生、fix 没留下会失败的 regression，用那条更精确；本规则更关注**specification 与 implementation 的时间独立性**。与 `coverage-theater` 区分：即使 test 先写，如果 assertion 没区分力，仍可能 theater。

真正关键不是“红色出现过”这件形式，而是 red 必须**因 intended missing behavior 而失败**，不是 fixture broken、import missing、test 写错。

> TDD 的价值不是先写哪一个文件，而是让 requirement 在 implementation 教它答案之前，先拥有独立说“不对”的能力。
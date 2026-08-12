# weakened-test-to-pass — Enforcer

## 定义
当 implementation 输给 specification 以后，修复动作不是改 implementation，而是让 witness 变得更好说话，这就是 weakened-test-to-pass。

关键不在“test 有没有被修改”。Contract 改变时 test 本来就应该改。真正病灶是**因果方向反了**：expectation 之所以变弱，是因为当前 implementation 满足不了它，而不是因为某个独立 authority 决定 promise 本身已经变弱。

## 支配原则
一条有意义的 test，本来就应该让某些 implementation 变得不可接受。

Red 出现时只有两种合法解释：

1. implementation 违反 intended contract；
2. intended contract 真的改变了。

不存在第三种合法解决方式叫“把 assertion 改到当前代码能过为止”。那没有解决 disagreement，只是删掉 witness。

在高速迭代和 AI-assisted codebase 里，这种病尤其常见，因为修改 tests 与修改 production code 一样机械地容易。当同一个执行者既能改答案又能改考卷时，如果 contract-change authority 不被明确保护，green 可以按需制造。

## 何时触发
当有意义的 behavioral expectation 主要因为 implementation failure 被放松，而不存在独立建立的 contract change 时触发。常见形式：

- exact expected value 变成 truthy / non-null / contains-something；
- edge/failure case 因新代码处理不了而被删除；
- 精确 error type/status 变成“随便 throw 什么都行”或“有 error 就行”；
- race/duplication 出现后，ordering/idempotence/identity assertion 被删；
- snapshot 整份重生成，让 unintended change 自动成为新 expected state；
- assertion 被 comment out、skip、标 flaky/xfail，或藏进 environment condition，只为了恢复 green；
- fixture 被简化到不再触碰 failing boundary；
- test 开始复制当前 implementation，而不是表达外部拥有的 promise。

## 不应触发
- Product/protocol/domain authority 明确改变 contract，新 test 正是因此表达新 promise。
- 某 assertion 只冻结 private choreography、从未属于 contract；删除它同时仍保护 caller-visible behavior，这通常是在修 `test-implementation-coupled`。
- 有独立 authoritative evidence 证明旧 test 本来就误解 contract，而不是因为当前实现失败才“发现”它错。
- Flaky mechanism 通过控制 time/order/state 被修，原 behavioral proposition 完整保留。
- Test 被加强、拆分或重写，只为让同一 contract 更清楚、更难绕过。

## 与相邻规则区分
`coverage-theater` 一开始就问了太弱的问题。`weakened-test-to-pass` 则是原本有一个有意义 witness，后来在 red 压力下把它解除武装。

`test-implementation-coupled` 有时确实应删 assertion，但理由必须是“它约束了 implementation accident，而不是 contract”。`scope-creep` 与此不同：requirement 若真的离开当前 scope，test 可以合法变化；但 scope 消失必须来自 task/contract，不是来自麻烦。

## 判定程序
接受任何 relaxation 之前，先问：

> 哪一个独立拥有的事实发生了变化，使旧 expectation 不再必要？

要求 provenance：product decision、protocol spec、domain invariant revision、compatibility decision，或证明旧 test 误解现实的 evidence。

如果唯一答案是“新 implementation 过不了”，就是本规则。

再检查新 assertion 是否还能捕获旧 assertion 原本保护的 defect。不能，就说明 evidence 被降级了。

## 例子
- positive：`assert.equal(status, 409)` 因实现开始返回 500，被改成 `assert.ok(status >= 400)`。
- positive：refactor 丢失 idempotence，于是 duplicate-request case 被删。
- positive：300 行 snapshot 变化，整份直接 regenerate，没人解释哪些 semantic difference 是 intended。
- positive：release 前把 failing test 标 skip，没有任何独立 requirement change。
- near-miss：API contract 正式从 409 改 422，decision 有记录，test 因此更新为 422。
- counterexample：删除 private helper spy，同时保留 public output assertion；contract 从未承诺那个 helper。

## Nudge
不要通过让 witness 忘记自己看见什么，来结束 disagreement。

Test 只能因为 contract 变了而变弱，不能因为 implementation 想换一个更容易的 examiner。

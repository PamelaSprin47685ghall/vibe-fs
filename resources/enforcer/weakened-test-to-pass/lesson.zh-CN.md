# weakened-test-to-pass — Lesson

## 评价

### 定义

当 implementation 输给 specification 以后，修复动作不是改 implementation，而是让 witness 变得更好说话，这就是 weakened-test-to-pass。

关键不在“test 有没有被修改”。Contract 改变时 test 本来就应该改。真正病灶是**因果方向反了**：expectation 之所以变弱，是因为当前 implementation 满足不了它，而不是因为某个独立 authority 决定 promise 本身已经变弱。

### 支配原则

一条有意义的 test，本来就应该让某些 implementation 变得不可接受。

Red 出现时只有两种合法解释：

1. implementation 违反 intended contract；
2. intended contract 真的改变了。

不存在第三种合法解决方式叫“把 assertion 改到当前代码能过为止”。那没有解决 disagreement，只是删掉 witness。

在高速迭代和 AI-assisted codebase 里，这种病尤其常见，因为修改 tests 与修改 production code 一样机械地容易。当同一个执行者既能改答案又能改考卷时，如果 contract-change authority 不被明确保护，green 可以按需制造。

### 何时触发

当有意义的 behavioral expectation 主要因为 implementation failure 被放松，而不存在独立建立的 contract change 时触发。常见形式：

- exact expected value 变成 truthy / non-null / contains-something；
- edge/failure case 因新代码处理不了而被删除；
- 精确 error type/status 变成“随便 throw 什么都行”或“有 error 就行”；
- race/duplication 出现后，ordering/idempotence/identity assertion 被删；
- snapshot 整份重生成，让 unintended change 自动成为新 expected state；
- assertion 被 comment out、skip、标 flaky/xfail，或藏进 environment condition，只为了恢复 green；
- fixture 被简化到不再触碰 failing boundary；
- test 开始复制当前 implementation，而不是表达外部拥有的 promise。

### 不应触发

- Product/protocol/domain authority 明确改变 contract，新 test 正是因此表达新 promise。
- 某 assertion 只冻结 private choreography、从未属于 contract；删除它同时仍保护 caller-visible behavior，这通常是在修 `test-implementation-coupled`。
- 有独立 authoritative evidence 证明旧 test 本来就误解 contract，而不是因为当前实现失败才“发现”它错。
- Flaky mechanism 通过控制 time/order/state 被修，原 behavioral proposition 完整保留。
- Test 被加强、拆分或重写，只为让同一 contract 更清楚、更难绕过。

### 与相邻规则区分

`coverage-theater` 一开始就问了太弱的问题。`weakened-test-to-pass` 则是原本有一个有意义 witness，后来在 red 压力下把它解除武装。

`test-implementation-coupled` 有时确实应删 assertion，但理由必须是“它约束了 implementation accident，而不是 contract”。`scope-creep` 与此不同：requirement 若真的离开当前 scope，test 可以合法变化；但 scope 消失必须来自 task/contract，不是来自麻烦。

### 判定程序

接受任何 relaxation 之前，先问：

> 哪一个独立拥有的事实发生了变化，使旧 expectation 不再必要？

要求 provenance：product decision、protocol spec、domain invariant revision、compatibility decision，或证明旧 test 误解现实的 evidence。

如果唯一答案是“新 implementation 过不了”，就是本规则。

再检查新 assertion 是否还能捕获旧 assertion 原本保护的 defect。不能，就说明 evidence 被降级了。

### 例子

- positive：`assert.equal(status, 409)` 因实现开始返回 500，被改成 `assert.ok(status >= 400)`。
- positive：refactor 丢失 idempotence，于是 duplicate-request case 被删。
- positive：300 行 snapshot 变化，整份直接 regenerate，没人解释哪些 semantic difference 是 intended。
- positive：release 前把 failing test 标 skip，没有任何独立 requirement change。
- near-miss：API contract 正式从 409 改 422，decision 有记录，test 因此更新为 422。
- counterexample：删除 private helper spy，同时保留 public output assertion；contract 从未承诺那个 helper。

## 建议

### 现在该做什么

恢复当前 independently owned contract 仍然要求的最强 expectation。

然后修 implementation，不要修 examiner。

如果 contract 的确改变，先把这个事实建立清楚，再带着 provenance 把 test 改成新 promise。Green 应当是 decision 的结果；decision 不能从“当前 implementation 恰好能通过什么”倒推出来。

### 为什么重要

Test suite 是少数几个代码可以被明确告知“no”的地方。

如果 production code 一失败，就能让自己的 witness 降低要求，suite 就不再约束 behavior，而会退化成“当前 implementation 做什么”的自动生成自传。任何 regression 都能被重新命名成新 expectation。Green 变得无限可获得，也因此几乎没有含义。

当同一个 agent 同时修改 implementation 与 tests 时，这个问题尤其尖锐：机械便利会让 separation of powers 消失。修复方式不是官僚式冻结，而是要求“**为什么 contract 改了**”必须来自 failing code 之外的 owner/evidence。

### 修复策略

改任何东西前，先恢复 behavioral proposition：

- 找到 original requirement、protocol、invariant、acceptance criterion 或 caller dependency；
- 判断它现在是否仍属于当前 task/product；
- 如果仍属于，就保留/恢复 test，修 production behavior；
- 如果已经改变，记录 authoritative reason，再精确写出新 proposition；
- 保持 regression power：除非新 contract 明确把旧 behavior 合法化，否则 old defective implementation 仍应因 contract-level reason 被打红。

Snapshot 场景要逐字段审 semantic difference。只接受 intended change。Critical fields 更适合 targeted assertion，不要让“update snapshot”成为橡皮图章。

如果旧 test 过度绑定 internals，删掉 implementation detail 后，应补上 observable contract assertion；不要只删除压力。

### 决策分支

- **Requirement 未变：**恢复 expectation，修 implementation。
- **Requirement 有意改变：**引用/记录新 contract，test 精确迁移到新 promise。
- **旧 test 误解 contract：**用 authoritative source 证明，然后纠正；当前 implementation fail 本身不是 proof。
- **Assertion 只约束 private implementation：**替换为 caller-visible behavior，而不是保留 ceremony。
- **Failure nondeterministic：**修 nondeterminism，不要削弱 behavioral claim。
- **唯一理由是 release pressure：**那工作就还不是 green。保持 failure visible；如果组织存在 waiver authority，应走显式 risk decision，而不是偷偷改 test。

### 常见假修复

- Exact equality 改 truthiness、宽 range、substring、does-not-throw。
- 用“用户大概不会这么做”删除 edge case，却没有任何 product boundary 支撑。
- Skip/xfail/flaky 一条 test，同时继续把它算成 evidence。
- Wholesale regenerate snapshot，利用 review fatigue 隐藏 unintended change。
- 把 fixture 改容易，使 difficult boundary 直接消失。
- 从 production logic import 同一套计算来生成 expected value，让 test 与 implementation 一起错。
- 写很多 comment 解释弱 assertion 为什么“good enough”。Commentary 不会创造 contract authority。

### 验证

证明 green 仍然具有攻击性。

临时恢复那个促使 test 被削弱的 defective behavior。如果 contract **没有**改变，修好的 test 必须 red。

如果 contract 确实改变，为**新 promise**构造一个 defect，并证明重写后的 test 仍会拒绝它。合法 contract change 只是改变“哪些行为可以接受”，不是废除 rejection power。

Invariant：

> Test suite 按 independently chosen contract 约束 implementation；implementation failure 无权单方面重新定义 contract。

### 完成条件

每一个被放宽的 expectation，都能用“contract 如何改变/被纠正”解释，而不是用“build 当时是红的”解释。

Implementation 可以与 test 发生 disagreement。它不能通过编辑 test，把 disagreement 从历史中删除。

### Nudge

不要通过让 witness 忘记自己看见什么，来结束 disagreement。

Test 只能因为 contract 变了而变弱，不能因为 implementation 想换一个更容易的 examiner。

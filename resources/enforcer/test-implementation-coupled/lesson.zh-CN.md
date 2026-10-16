# test-implementation-coupled — Lesson

## 评价

### 定义

Test implementation-coupled 的意思，是一个**完全正确的 refactor**也会被 suite 判错，因为 test 冻结了“现在代码怎么做”，而不是“supported contract 要求什么”。

### 支配原则

常见 coupling target：

- private helper exact call count；
- internal method name / object layout；
- contract 从未暴露的 intermediate field；
- pure computation 的 incidental sequence；
- mock 断言哪个 helper call 哪个 helper；
- 非 public 的 internal JSON/state snapshot；
- 通过 reflection/test-only export 访问 private member；
- 明明多个 algorithm 等价，却锁死其中一套步骤。

这种 test 的成本非常真实：它把 old implementation 变成 unofficial second specification。更简单 algorithm、不同 decomposition、删 helper、batch call、换 data structure 都会红，虽然 user 什么 regression 都没看到。久而久之团队不是不想 refactor，而是 suite 在对“正确改变内部”收税。

更糟的是，它仍然可能漏真正 bug。Code 可以完美复现 helper choreography，却返回 wrong public result。Suite 冻结的是 motion，不是 meaning。

### 何时触发

一个好 thought experiment：做 semantics-preserving rewrite——helper chain 合成一个 pure function、list 换 map、两个 internal call batch、inline/remove private method、独立计算换顺序。若 test 红，而没人说得出 caller contract 哪里坏了，就是 coupling。

### 不应触发

不要反向教条化。有些 interaction **本来就是 contract**：exactly-once publication、rejection 下 zero external call、transaction boundary、durable ordering、idempotency-key reuse、真实 protocol 要求的 provider sequence。这些都可以 test。标准只在于：一个 conforming implementation 有没有权改变这个细节？

### 与相邻规则区分

与 `weakened-test-to-pass` 区分：那条是因为 production fail 而删/松一个**本来合法的 behavioral expectation**；本规则则是 expectation 从一开始就不该属于 contract。把 private assertion 移到真实 behavior，有时反而会让 suite 更强。

它也常与 `behavioral-boundary-untested` 同时出现：内部 assertion 几百条，真正 supported entrance 一条 proof 都没有。

### 判定程序

最核心的判定问题是 substitutability：

> 另一个 implementation 如果满足所有真实 caller-visible promise，是否仍可能因为这条 test 失败？

如果会，test 多半在保护 private choreography。

### 例子

private helper exact call count。internal method name / object layout。contract 从未暴露的 intermediate field。mock 断言哪个 helper call 哪个 helper。非 public 的 internal JSON/state snapshot。

## 建议

### 现在该做什么

把 assertion 一直往外移，直到它落在真正 promise 上。

对每个 private/internal assertion 问：它本来想保护什么真实 behavior？然后改成 supported input、observable result、durable state、或真正 contractual external interaction。

例如：

```text
"helper X called twice"
    ↓
"一个 logical publication 恰好发生一次"

"private field status = ready"
    ↓
"supported API 现在允许该 operation"

"method A runs before B"
    ↓
"durable commit 先于 provider-visible success"
```

Replacement 仍可观察 interaction，但 interaction 本身必须就是 contract：rejection 时 no network call、exactly-once effect、durability ordering、stable idempotency identity、protocol handshake 等。

### 为什么重要

一个好 suite 应该**对 irrelevant implementation change 更不敏感，对 meaningful behavioral change 更敏感**。这两面一起才能防止“只是把 test 弄弱”。

### 修复策略

真正 local law 仍然可以 direct unit test。比如 pure parser function 的 return 本身就是它的 supported contract，当然可以直接测。问题不是“离 implementation 太近”，而是 assertion 锁定了 conforming consumer 没资格依赖的东西。

### 决策分支


### 常见假修复

常见假修复：

- 把所有 white-box test 一删了之，却没有补 behavioral evidence；
- private field assertion 换成 equally-private object graph 大 snapshot；
- 永久 export internals，只因旧 test 不好改；
- 少 mock 几个 helper，但仍断言 caller 从不关心的 call choreography；
- batching/caching 本来合法，却锁 exact call count；
- 说“这个 sequence 很重要”，却说不出哪个 public/durable invariant 让它重要。

### 验证

验证必须双向。

第一，做 semantics-preserving refactor：rename/inline helper、换 internal data structure、batch independent call、reorder pure calculation。守真正 promise 的 test 应继续 green。

第二，保持很多旧 internal choreography 不变，但破坏 promise：wrong identity、publish twice、skip authorization、expose stale state。重写后的 test 必须红。

### 完成条件

完成时 suite 允许同一 promise 有多个正确 implementation，却拒绝所有破坏 promise 的实现，而不是要求大家继续模仿昨天那套 decomposition。

> Test 应忠于 contract，不应忠于第一个曾经满足 contract 的代码形状。

### Nudge

> Test 应让 wrong behavior 昂贵，让 correct refactor 便宜。反过来时，suite 守护的是 implementation nostalgia。

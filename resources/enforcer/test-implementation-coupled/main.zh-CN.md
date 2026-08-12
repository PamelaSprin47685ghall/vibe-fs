# test-implementation-coupled — Main

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

常见假修复：

- 把所有 white-box test 一删了之，却没有补 behavioral evidence；
- private field assertion 换成 equally-private object graph 大 snapshot；
- 永久 export internals，只因旧 test 不好改；
- 少 mock 几个 helper，但仍断言 caller 从不关心的 call choreography；
- batching/caching 本来合法，却锁 exact call count；
- 说“这个 sequence 很重要”，却说不出哪个 public/durable invariant 让它重要。

验证必须双向。

第一，做 semantics-preserving refactor：rename/inline helper、换 internal data structure、batch independent call、reorder pure calculation。守真正 promise 的 test 应继续 green。

第二，保持很多旧 internal choreography 不变，但破坏 promise：wrong identity、publish twice、skip authorization、expose stale state。重写后的 test 必须红。

这两面一起才能防止“只是把 test 弄弱”。一个好 suite 应该**对 irrelevant implementation change 更不敏感，对 meaningful behavioral change 更敏感**。

真正 local law 仍然可以 direct unit test。比如 pure parser function 的 return 本身就是它的 supported contract，当然可以直接测。问题不是“离 implementation 太近”，而是 assertion 锁定了 conforming consumer 没资格依赖的东西。

完成时 suite 允许同一 promise 有多个正确 implementation，却拒绝所有破坏 promise 的实现，而不是要求大家继续模仿昨天那套 decomposition。

> Test 应忠于 contract，不应忠于第一个曾经满足 contract 的代码形状。
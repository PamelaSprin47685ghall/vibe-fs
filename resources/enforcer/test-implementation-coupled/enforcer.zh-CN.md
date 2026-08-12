# test-implementation-coupled — Enforcer

Test implementation-coupled 的意思，是一个**完全正确的 refactor**也会被 suite 判错，因为 test 冻结了“现在代码怎么做”，而不是“supported contract 要求什么”。

最核心的判定问题是 substitutability：

> 另一个 implementation 如果满足所有真实 caller-visible promise，是否仍可能因为这条 test 失败？

如果会，test 多半在保护 private choreography。

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

不要反向教条化。有些 interaction **本来就是 contract**：exactly-once publication、rejection 下 zero external call、transaction boundary、durable ordering、idempotency-key reuse、真实 protocol 要求的 provider sequence。这些都可以 test。标准只在于：一个 conforming implementation 有没有权改变这个细节？

与 `weakened-test-to-pass` 区分：那条是因为 production fail 而删/松一个**本来合法的 behavioral expectation**；本规则则是 expectation 从一开始就不该属于 contract。把 private assertion 移到真实 behavior，有时反而会让 suite 更强。

它也常与 `behavioral-boundary-untested` 同时出现：内部 assertion 几百条，真正 supported entrance 一条 proof 都没有。

一个好 thought experiment：做 semantics-preserving rewrite——helper chain 合成一个 pure function、list 换 map、两个 internal call batch、inline/remove private method、独立计算换顺序。若 test 红，而没人说得出 caller contract 哪里坏了，就是 coupling。

> Test 应让 wrong behavior 昂贵，让 correct refactor 便宜。反过来时，suite 守护的是 implementation nostalgia。
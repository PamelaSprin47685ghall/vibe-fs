# time-dependent-test — Enforcer

Time-dependent test 的问题，是 host clock 给 verdict 偷塞了一个没声明的 premise。

来源可能是 `Date.now()`、`UtcNow`、local timezone、DST、elapsed wall time、scheduler delay、current date、real deadline。共同点是：scenario 本来应该固定，但 test 没有自己选择 time，而是去机器上“发现现在几点”，于是 input 一直在移动。

因此失败不只是泛泛 “timing flaky”，可能具体依赖：

- test 执行中跨 midnight；
- month/year boundary；
- daylight-saving transition；
- CI zone/locale 与本地不同；
- leap day/calendar edge；
- scheduler pause 把 operation 推过 deadline；
- slow machine 改变 “within N ms” 是否通过；
- suite order 改变 assertion 前已过去多少 real time。

当 functional/domain verdict 依赖 ambient clock / wall-time window，而 time 本身又不是被测 feature 时，触发。

不要误杀真正 clock-adapter smoke，它可以很窄地证明 production 能读 system clock。Performance/load benchmark 本来就测 wall time，也不是本规则。Causal synchronization 也不同：await 真实 completion signal，real timeout 只负责防 hang，不代表 success，就可以有 deterministic meaning。

与 `time-source-in-logic` 区分：那条打 production policy 深处读取 ambient time；本规则打**test premises**。两者常指向同一 seam，但可以独立存在。

决定性问题：

> 这条 test 如果一小时后开始、换一个 timezone、换一台慢机器，是否仍表示完全相同的 scenario？

如果不是，time 就是 undeclared input。

正确修法是把 temporal fact 当普通 data：fixed instant、explicit zone、duration、deadline、monotonic tick、manual clock。Fixture 自己拥有它们。

Real time 可以留在很薄的 adapter boundary；需要时单独用 tolerant smoke 验证 adapter，再把 deterministic value 传进 domain。

也不要把一个 ambient source 换成另一个 global monkeypatch 就算解决。Process-wide fake clock 如果 test 无 scoped restore，反而会制造 order dependence。

> Test 应该自己选择故事发生的时间，而不是问机器“我读这个故事时碰巧几点”。
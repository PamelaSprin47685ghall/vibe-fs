# clone-and-mutate-derived — Enforcer

Clone-and-mutate derived value 的问题，不是“copy 很慢”，而是新 value 的语义被定义成了：**旧对象现在有什么，我默认全继承；只有显式 patch 的字段不同。**

这是一种负面构造。Constructor 没有说“新值由哪些事实组成”，而是说“除了我想到要改的，其他都照旧”。Source type 以后新增一个字段，这个字段会自动流入所有 clone path，即使没人判断它在 derived value 中应该保留、重算还是禁止。

以下情形触发：

- `clone(source); next.status = ...` 用于构造新的 domain meaning；
- spread/copy 后 patch 若干字段，而 source/derived 实际并非“同一值的普通 update”；
- future field 加到 source 后，derived code 无需修改就自动携带它；
- security/authority/lifecycle 字段因 shallow copy 被意外继承；
- derived object 的 invariants 只能靠 patch 顺序维持；
- reviewer 无法从 constructor 看出哪些 source facts 被有意保留。

不要误杀 immutable record update。如果 `withStatus` 真的是**同一 entity/value 的合法状态更新**，其余字段本来就应该全部保持，并且 constructor/state transition 仍保证 invariant，record-copy 很自然。问题在于 source 与 derived 之间有语义变换，却拿结构继承代替了那条关系。

与 `in-place-mutation` 区分：那里旧 shared value 自己被改；本规则会产生新 object，但新 object 的内容由 prototype 当前 shape 偶然决定。与 `runtime-checked-builder` 区分：那里 construction sequence 允许 incomplete state；这里 construction 看似完整，只是保留哪些 fact 没有显式 owner。

决定性 test：给 source type 想象新增一个敏感字段 `authorizationScope`。Derived path 应该强迫作者回答 keep/drop/recompute；如果代码什么都不用改，字段自动被 copy，新值就不是由语义 constructor 决定的。

> Derivation 应该说明“我为什么拥有这些事实”，而不是“因为原对象恰好也有，所以顺手继承”。
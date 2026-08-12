# clone-and-mutate-derived — Main

让 derived value 由**正面列出的语义事实**构造，而不是由 prototype 当前 shape 决定。

先给 source→derived 的关系起名字：状态 transition、projection、redaction、copy-with-change、new command、new snapshot。然后明确哪些 facts：

- 必须保留；
- 必须重算；
- 必须删除；
- 必须由 caller 新提供。

如果 source/derived 真的是同一个 immutable value 的 ordinary update，可以使用 constructor-safe record copy；这时“其余字段全部保留”就是明确语义。若关系更复杂，就写显式 constructor/mapper，让 source type 新增字段时 compile/review 强迫做决定。

常见假修复：

- deep clone 替 shallow clone；继承问题一点没变；
- clone 后 `freeze()`，只是把 accidental inheritance 冻住；
- 把 clone 包进名为 `withFoo()` 的 helper，内部仍 copy-all-by-default；
- 注释列“这些字段应该保持”，constructor 却仍自动复制未来字段；
- 用 allowlist 做 runtime delete，但 type 新增字段仍可能在遗漏时泄漏；
- 为减少 boilerplate 退回 generic `patch(Object)`，重新让 semantic relation 消失。

验证应做 evolution test：在 source 添加一个字段，所有语义 derivation 必须出现明确 compile/test/review 决策，而不是静默传播。特别检查 authorization、owner、version、secret、lifecycle、durable identity 这类不能默认继承的字段。

同时不要把 ordinary immutable update 写得异常繁琐。如果所有 fields 除一个外确实应该保持，record-copy 就是最清楚的表达。RuleBook 反对 accidental inheritance，不反对语言原生的 immutable update。

完成时，从 constructor/mapper 本身就能解释 derived value 为什么拥有每个重要 fact；未来 source 扩展不会自动扩大 derived authority。

> 新值应继承语义，不应继承 prototype 的偶然形状。
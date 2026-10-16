# domain-language-drift — Lesson

## 评价

### 定义

Domain language drift 发生在词与概念不再一一对应：一个概念被叫成多个名字，或一个名字被拉去承载多个概念。

名字是 code、test、doc 与人类讨论之间的 join key。Vocabulary 漂移后，同一个词不能稳定定位同一个概念，讨论开始出现“我们说的是不是同一个 X”的额外解码。

### 支配原则

名字是 code、test、doc 与人类讨论之间的 join key。当 vocabulary 漂移，这些表示无法再可靠地 join：两个工程师可能用同一个词却指不同概念，或描述同一事实却用不同词。由此产生的歧义不是装饰问题；它摧毁局部推理的可能，因为每个 term 都需要上下文消歧。

### 何时触发

- 同一 context 内 `amount/total/prcAmt` 实际指同一事实；
- 一个 `status` 在不同模块分别表示 lifecycle、transport、approval；
- event/type/test/doc 对同一概念使用不同词；
- rename 只改了一层，旧 synonym 继续活在 provider 或历史无关的现行 surface。

### 不应触发

- 不同 bounded contexts 可以有不同 ubiquitous language，只要 border 显式翻译；
- 单个名字本身说谎属于 `misleading-name`；
- adapter 中技术协议词翻译成 domain term 是正常边界；
- 标准协议词不是本地 vocabulary drift。

### 与相邻规则区分

`misleading-name` 管的是单个 identifier 做出虚假声明；`context-model-leak` 跨 context 共享 representation。本规则管的是 vocabulary 本身失去与概念的双射关系。Tie-break：如果拼写出的意义在本地是对的、但同一概念有多个名字、或一个名字承载多个概念，这个案子归本规则。

### 判定程序

在一个 context 内做两向检查：一个 term 是否只指一个 concept？一个 concept 是否只有一个 canonical term？拆分 overloaded terms，收敛意外的 synonyms。

### 例子

- positive：billing 用 `amount`、`prcAmt`、`total` 指同一个金额事实，却没有说明区别。
- near-miss：两个 context 分别用 `Order` 与 `Purchase`，并在 border 翻译。
- counterexample：在 owning context 选一个 canonical term，把 code、tests、events、docs 一起 rename 到它。

## 建议

### 现在该做什么

在 owning context 选择 canonical terms，把 code/types/events/tests/docs 一起收敛；overloaded term 先拆概念，再 rename。真正跨 bounded context 的差异保留，并在 border 显式翻译。

### 为什么重要

稳定 vocabulary 是认知压缩：一个词学一次，处处复用。同义词泛滥与一词多义会把这份压缩反向展开，每次阅读都重新 disambiguate。

### 修复策略

从 domain distinctions 出发，而不是从现有 identifier 出发。先拆分 overloaded concepts，再收敛意外的 synonyms。在 bounded-context 边界保留本地语言，让翻译显式，而不是强加一个虚假的全局词汇表。

### 决策分支

- 若同一 context 内一个 concept 有多个名字，收敛到 canonical term，退役其余 synonyms。
- 若一个名字覆盖多个 concepts，先拆名字，再做其它 rename。
- 若两个 contexts 有意不同，保留两种语言，并在 border 添加显式 translation。

### 常见假修复

- 建 glossary 为长期不一致辩护。
- 发明一个“中性第三词”统一两个其实不同的 contexts。
- 只 rename type，不改 tests/events/docs/provider surface。
- 为 compatibility 保留旧 synonym，却没有真实 external creditor。

### 验证

搜索 retired synonyms 与 overloaded term。一个 domain expert 与一个 code reader 在同一 context 看到 canonical term 时，应指向同一个 concept。

### 完成条件

每个 context 内词与概念稳定对应；语言差异只存在于真实 boundary，并由显式 translation 承担。

### Nudge

Vocabulary 不是文案层。它是模型的一部分；词与概念失去一一对应，代码就失去稳定的语义索引。

# implicit-control-flow — Enforcer

Implicit control flow 的病，是系统 correctness 依赖一条 **happens-before** 关系，但这条关系只活在 registration order、hook phase、import side effect、startup convention、global initialization 或 framework folklore 里。

代码里看得见参与者，却看不见让它们正确的时间关系。于是每个 component 本地都可能完全合法，组合起来仍然错：A 必须先于 B、seal 必须最后、listener 必须在 event 前注册、cleanup 必须在 publication 后，但没有一个结构真正拥有这些 causal edges。

以下情形触发：

- “这个 hook 必须比那个先注册”只写在 comment；
- import 某 module 的副作用决定 runtime initialization；
- callback 顺序取决于 framework 遍历 registry 的偶然 order；
- startup 需要按特定 module 顺序执行，否则出现 partial state；
- 两个 independently valid event 的 arrival order 被假定为业务 causal order；
- 一个 middleware/transform 只有放在数组某个位置才正确，却没有 typed/composed contract；
- new participant 插入后，旧 order 约束静默失效。

不要误杀所有 framework lifecycle。若 runtime order 是稳定 public contract、代码显式建模 phase，并且 misuse 能在 build/startup 时 fail closed，happens-before 已经有 owner。Ordinary higher-order callback 若 call site 明确写出谁先谁后，也不隐式。

与 `implicit-convention-magic` 区分：那条问“谁参与”被 filename/annotation/discovery 隐藏；本规则问“谁先谁后”被 ambient lifecycle 隐藏。与 `program-counter-state` 相反：那里把 sequencing 过度持久化成 state；这里 sequencing 根本没有一等表示。

最锋利的问题：**如果把所有 registration/import 语句随机重排，哪一条 business invariant 会坏？** 只要答案存在，而那条 causal relation 没有被 program structure/contract 明确表达，就有 implicit control flow。

> Causality 不该靠文件加载顺序碰巧成立。谁依赖谁先发生，就让这条边成为程序能看见、能验证的事实。
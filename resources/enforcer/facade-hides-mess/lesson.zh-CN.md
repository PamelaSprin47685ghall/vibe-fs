# facade-hides-mess — Lesson

## 评价

### 定义

当一个 clean entry point 被拿来证明“architecture 已经干净”，但它下面的 ownership、dependency direction、duplicated state、side-effect boundary 原封不动时，就是 facade-hides-mess。

Facade 可以让 caller 简单。它不能仅凭存在，就让自己转发进去的系统变简单。

### 支配原则

Surface area 与 internal structure 是两种不同 property。

当 facade 真正代表 coherent subsystem contract 时，它很有价值：caller 只需更少 concepts，internals 有清晰 owner，dependency edge 更好理解。Anti-pattern 出现在 facade 只是给同一张 tangled graph 画了一条漂亮边框。

典型症状：example 里的 API 看起来非常优雅，但实现其中一个 method 仍要触碰五个互相依赖 modules、同步 duplicate state、理解 private ordering rule。Mess 没消失，只是多了一个前台接待。

### 何时触发

当 facade/wrapper 被当作 architecture repair，但底层 responsibility 结构没有真正变化时触发。常见形式：

- facade methods 只是对互不相关 subsystems 的 one-to-one forwarding alias；
- caller 看不到 cyclic dependency 了，但 cycle 仍完整活在 facade 后面；
- duplicate owners 仍存在，facade 只是根据 flag/context 决定今天叫谁；
- 多套 incompatible representations 仍存在，每次 operation 都靠 facade 来回 translation；
- facade test 需要 mock 一整片 internals maze，wrapper 被证明了，coupling 没修；
- internal modules 仍跨 forbidden layers import，只是 external caller 统一走漂亮入口；
- facade 因为底层没人承担 ownership，开始吸收 orchestration/policy，最终变成新 god module；
- 仅因为 caller 已改用 facade 就宣称“migration complete”，但 old/new execution paths 仍在其后共存。

### 不应触发

- Facade 是 coherent subsystem 的 intentional stable contract，内部 ownership 也清楚。
- Wrapper 隔离 real external/framework boundary，translation 本身有 semantic work。
- Temporary migration facade 有 named consumer set、bounded overlap、concrete removal condition。
- Facade 有意提供 capability-safe subset；限制 authority 本身就是实质语义。
- 任务目标本来只要求 caller-facing simplification，而且没有人夸大成 internal architecture repair。

### 与相邻规则区分

`half-finished-refactor` 重点是 old/new ownership models 共存。`facade-hides-mess` 重点是 clean front 被误认为 structural repair。

`boundary-collapse` 是边界没能保护 distinction；本规则里外部 boundary 甚至可能很漂亮，但内部 owner 仍坏。

Facade 如果开始拥有所有 unrelated policy，可能进一步触发 `god-module`。但 facade 本身不自动等于 god module。

### 判定程序

暂时完全忽略 public API，画出一个代表性 facade call 后面的 dependency/ownership graph。

问：

- 每个 decision 谁拥有？
- 同一 fact 有几种 representation？
- 每块 state 谁能 mutate？
- dependency cycle 是消失了，还是只被藏起来？
- 如果明天删除 facade，下面会露出一个更清晰 internal boundary，还是原封不动露出旧 mess？

最后一项如果答案是“旧 mess 完整出现”，facade 没有修 architecture。

### 例子

- positive：`UserService.update()` 很干净，实际依次穿 legacy manager、new manager、compatibility adapter、state synchronizer，而且两边仍都是 writer。
- positive：A 仍 import B，B 仍 import A，只是外部统一 import `Facade`，cycle 根本没断。
- positive：“repository facade” 内部同时决定 SQL、cache、event publication、authorization、retry、migration，因为这些 policy 从没找到 owner。
- near-miss：一个 narrow port 隐藏 SDK client；adapter 只拥有 protocol translation，domain decision 全在别处。
- counterexample：内部 ownership 先被 consolidate，之后 facade 只暴露这个 coherent subsystem 的小而稳定 contract。

## 建议

### 现在该做什么

修 facade 正在遮住的 structure。

选定 state 与 policy 的真正 owners，删除 duplicate writers 与 dependency cycles，collapse 已失去 contract 的 representations，让 internal dependency direction 变诚实。只有底层真的形成 coherent subsystem contract / capability boundary 之后，才保留或重新设计 facade。

### 为什么重要

Cosmetic facade 很危险，因为它把 caller ergonomics 改善得足够明显，常常让 refactor 在最应该继续的时候提前停止。

新 caller 只看见一个漂亮 object，于是大家以为 subsystem 已修好。可真正 implementation change 仍必须穿越同一张 coupled graph。原本推动 ownership repair 的压力消失了，因为大家最先抱怨的 public ugliness 已经被藏掉。

于是 architecture 出现上下两层：楼上是干净 documentation，楼下是闹鬼 machinery。Incident 和大 change 永远发生在楼下。

### 修复策略

从里往外做：

1. 选一个代表性 facade operation；
2. 映射它碰到的全部 decisions、state writers、effects、translations、dependencies；
3. 每个 decision/fact 只保留一个 semantic owner；
4. 删除或降级 duplicate owners；
5. 通过改变 ownership/dependency direction 消除 cycle，不要让 cycle 只是改道穿 facade；
6. 删除不再由 external contract 支撑的 compatibility/translation path；
7. 让 surviving internal boundary 明确可见；
8. 最后才设计 facade 去干净地暴露那个 boundary。

底层 repair 做对后，好 facade 往往反而更薄，因为它不再需要 flags、migration routing、state reconciliation 与 hidden orchestration。

### 决策分支

- **Facade 只转发一个 coherent subsystem：**如果 caller ergonomics/stability 有价值，就保留。
- **Facade 在 old/new owners 之间 dispatch：**完成 migration；见 `half-finished-refactor`。
- **Facade 翻译 external protocol：**保留 edge translation，不让 external shape 扩散进 core。
- **Facade 拥有 authorization/capability narrowing：**这可能是真 semantic boundary，明确保留限制。
- **Facade 因 internals 混乱而吸收很多 unrelated policy：**先把 policy 送回 rightful owner，再缩 facade。
- **任务只要求 external API cleanup：**不要 overclaim internal repair。Caller-surface task 可以合法完成，而无需假装底层 architecture 也变了。

### 常见假修复

- 在第一个 facade 外再套第二个 facade。
- Rename internal modules/services，但 dependency direction 与 writers 全不变。
- 用 package-private/export restrictions 把 internals 藏起来，然后声称 coupling 已解决。
- 只写 facade integration tests，让 duplicate paths 继续在后面 live 而没人看见。
- 把所有 orchestration 移进 facade，把 cosmetic cleanup 进化成 god module。
- 因为 caller 看不见旧 internals，就永久保留它们。Hidden debt 仍然会执行。
- Architecture diagram 只画 facade，不画它后面的 graph。

### 验证

在脑中或 branch 中删除 facade，观察还剩什么 boundary。

真正 structural repair 应满足：

- 没有 facade，internal owners 仍 coherent；
- dependency direction 仍 acyclic/intelligible；
- 每个 state fact 只有 rightful writer，或有 explicit reconciliation law；
- 不再需要 hidden legacy/new dispatch；
- facade 可以被描述为一个 contract，而不是 compensating logic 的垃圾袋。

随后分别验证 facade caller behavior 与 owner 内部 invariant。

Invariant：

> Facade 压缩访问一个 coherent subsystem；它不负责制造“好像有 subsystem”的视觉效果。

### 完成条件

Clean API 是 clean ownership 的可见后果，不是一块拉在 unresolved architecture 前面的幕布。

### Nudge

门很干净，不代表房间很干净。

把门打开。看看 mess 到底是谁在拥有。

# facade-hides-mess — Main

## 现在该做什么
修 facade 正在遮住的 structure。

选定 state 与 policy 的真正 owners，删除 duplicate writers 与 dependency cycles，collapse 已失去 contract 的 representations，让 internal dependency direction 变诚实。只有底层真的形成 coherent subsystem contract / capability boundary 之后，才保留或重新设计 facade。

## 为什么重要
Cosmetic facade 很危险，因为它把 caller ergonomics 改善得足够明显，常常让 refactor 在最应该继续的时候提前停止。

新 caller 只看见一个漂亮 object，于是大家以为 subsystem 已修好。可真正 implementation change 仍必须穿越同一张 coupled graph。原本推动 ownership repair 的压力消失了，因为大家最先抱怨的 public ugliness 已经被藏掉。

于是 architecture 出现上下两层：楼上是干净 documentation，楼下是闹鬼 machinery。Incident 和大 change 永远发生在楼下。

## 修复策略
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

## 决策分支
- **Facade 只转发一个 coherent subsystem：**如果 caller ergonomics/stability 有价值，就保留。
- **Facade 在 old/new owners 之间 dispatch：**完成 migration；见 `half-finished-refactor`。
- **Facade 翻译 external protocol：**保留 edge translation，不让 external shape 扩散进 core。
- **Facade 拥有 authorization/capability narrowing：**这可能是真 semantic boundary，明确保留限制。
- **Facade 因 internals 混乱而吸收很多 unrelated policy：**先把 policy 送回 rightful owner，再缩 facade。
- **任务只要求 external API cleanup：**不要 overclaim internal repair。Caller-surface task 可以合法完成，而无需假装底层 architecture 也变了。

## 常见假修复
- 在第一个 facade 外再套第二个 facade。
- Rename internal modules/services，但 dependency direction 与 writers 全不变。
- 用 package-private/export restrictions 把 internals 藏起来，然后声称 coupling 已解决。
- 只写 facade integration tests，让 duplicate paths 继续在后面 live 而没人看见。
- 把所有 orchestration 移进 facade，把 cosmetic cleanup 进化成 god module。
- 因为 caller 看不见旧 internals，就永久保留它们。Hidden debt 仍然会执行。
- Architecture diagram 只画 facade，不画它后面的 graph。

## 验证
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

## 完成条件
Clean API 是 clean ownership 的可见后果，不是一块拉在 unresolved architecture 前面的幕布。

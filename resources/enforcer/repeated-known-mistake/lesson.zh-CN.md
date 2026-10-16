# repeated-known-mistake — Lesson

## 评价

### 定义

Repeated known mistake 的病，是 repository 已经为某个失败付过调查成本、留下仍然有效的 lesson/invariant/decision，当前工作却像从未发生过一样重新走回同一条路。

这暴露的是 retrieval failure，而不是 documentation failure。知识明明存在，却没有进入新的 decision；于是项目表面“有历史”，实际每代 contributor 仍自己交一遍学费。

### 支配原则

记录知识只有未来 decision 能继承它才有价值。重复一个已记录的 mistake，说明项目有 storage 却没有 retrieval：经验以文本存在，却没有约束行动。工程成熟度因此不仅要求写 lesson，还要求在重新付钱发现同一失败之前，咨询离问题最近的知识。

### 何时触发

以下情形触发：

- prior decision 明确禁止某 mechanism，当前 fix 又重新引入；
- 已记录某 provider/Host quirk，后来代码仍按被证明错误的 assumption 实现；
- incident lesson 已成为 invariant，新的 refactor 因没读又破坏；
- 同一个 timeout/sleep/dual-write workaround 反复回来；
- contributor 只因 guidance “很旧”就忽略，却没证明 premises 已变化。

### 不应触发

不要让历史变成保守主义。环境、provider、architecture、requirement 真正变化时，旧 lesson 可以被 supersede；但应明确写出**哪条 premise 变了、什么新 evidence 推翻旧结论**。悄悄忽略不是 supersession。

### 与相邻规则区分

与 `unrecorded-lesson` 区分：那里当时根本没把知识存下来；本规则是存了却没咨询。与 `stale-documentation` 区分：如果旧 guidance 本身已错，应更新/废止它，不应继续约束当前 work。

### 判定程序

判断时不是搜到相同关键词就报警，而是比较 failure mechanism 与 premises。一个 SQLite locking lesson 不一定适用于新 Postgres path；一个旧 Host version 的 bug 也不应永远统治升级后的正式 contract。

### 例子

- positive：prior decision 禁止 sleep-based waits，当前 fix 又给同一个 race 加 `sleep 500`。
- near-miss：SQLite locking lesson 被遵循，而新 Postgres path 在不同 premises 下设计，并被记录为 superseding decision。
- counterexample：repository 没有该 failure 的 prior record；这个 defect 是新知识（若随后没写下来就是 `unrecorded-lesson`）。

## 建议

### 现在该做什么

把现有 lesson 当作 active premise 重新读一遍，然后做两种诚实选择之一：遵守，或正式 supersede。

先定位 prior record 真正保护的 invariant/failure mechanism，而不是只看标题。确认它当时依赖哪些环境条件、external version、architecture assumptions。若这些 premises 仍成立，当前实现必须尊重 lesson；若已变化，收集能区分新旧世界的 evidence，并在 owning documentation/test/contract 中更新结论。

### 为什么重要

已记录的 failure 是预付的 reasoning。忽略它等于再付一次同样的 debugging cost，还教会 contributor：repository 知识是装饰品，不是操作性的。时间长了这会摧毁记录 lesson 的动机。

### 修复策略

把当前 symptom 链到 prior record，应用记录的 constraint；只有当新 evidence 真正改变其 scope 或 conclusion 时才更新记录。

### 决策分支

- 如果 prior lesson 的 premises 仍成立，遵守它，停止重复失败路径。
- 如果 premises 变了，写下带新 evidence 的显式 superseding decision，然后继续。
- 如果没有 authoritative record，修 defect 并捕获 lesson，不要把沉默当作随意走动的许可。

### 常见假修复

常见假修复：

- 复制 old workaround “因为以前这样修过”，却没看它后来是否已经被 supersede；
- 反过来，完全忽略 old lesson “因为那是历史”；
- 新文档说旧规则不再适用，但 test/gate/runtime contract 仍按旧事实运行；
- 只改当前 code，不把新 evidence 写回知识 owner，下一次又重复争论；
- 把近似 symptom 当同一 root cause，机械套旧修法。

### 验证

验证要比较 mechanism，而不是表面形状。能重现 old counterexample 时，新设计仍应保护对应 invariant；若声称环境已变，让新的 contract/canary 明确证明旧 failure 不再可能或语义已变化。

如果 prior lesson 本身 stale，就更新它，不要要求 contributor 一边遵守“当前事实”，一边被历史文档反向审判。Repository memory 需要可修订，但修订必须有 provenance。

理想情况下，最重要的 known mistake 不只存在 prose：能做 regression/architecture gate/contract test 的地方，把 lesson 变 executable memory；只能靠 judgment 的部分才留成 RuleBook/decision note。

### 完成条件

完成时当前 choice 能清楚回答：过去发生过什么、那条 lesson 今天是否仍有效、如果无效是什么新 evidence 让它失效。

> 学费已经交过，就别因为换了工程师又交一次；但也别把收据当永恒自然法。

### Nudge

> Repository 真正有记忆，不是因为历史文件很多，而是过去已经证明过的失败会对今天的选择产生约束，直到新证据正式解除它。

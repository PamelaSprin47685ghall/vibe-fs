# repeated-known-mistake — Enforcer

## 定义

Repeated known mistake 的病，是 repository 已经为某个失败付过调查成本、留下仍然有效的 lesson/invariant/decision，当前工作却像从未发生过一样重新走回同一条路。

这暴露的是 retrieval failure，而不是 documentation failure。知识明明存在，却没有进入新的 decision；于是项目表面“有历史”，实际每代 contributor 仍自己交一遍学费。

## 支配原则

记录知识只有未来 decision 能继承它才有价值。重复一个已记录的 mistake，说明项目有 storage 却没有 retrieval：经验以文本存在，却没有约束行动。工程成熟度因此不仅要求写 lesson，还要求在重新付钱发现同一失败之前，咨询离问题最近的知识。

## 何时触发

以下情形触发：

- prior decision 明确禁止某 mechanism，当前 fix 又重新引入；
- 已记录某 provider/Host quirk，后来代码仍按被证明错误的 assumption 实现；
- incident lesson 已成为 invariant，新的 refactor 因没读又破坏；
- 同一个 timeout/sleep/dual-write workaround 反复回来；
- contributor 只因 guidance “很旧”就忽略，却没证明 premises 已变化。

## 不应触发

不要让历史变成保守主义。环境、provider、architecture、requirement 真正变化时，旧 lesson 可以被 supersede；但应明确写出**哪条 premise 变了、什么新 evidence 推翻旧结论**。悄悄忽略不是 supersession。

## 与相邻规则区分

与 `unrecorded-lesson` 区分：那里当时根本没把知识存下来；本规则是存了却没咨询。与 `stale-documentation` 区分：如果旧 guidance 本身已错，应更新/废止它，不应继续约束当前 work。

## 判定程序

判断时不是搜到相同关键词就报警，而是比较 failure mechanism 与 premises。一个 SQLite locking lesson 不一定适用于新 Postgres path；一个旧 Host version 的 bug 也不应永远统治升级后的正式 contract。

## 例子

- positive：prior decision 禁止 sleep-based waits，当前 fix 又给同一个 race 加 `sleep 500`。
- near-miss：SQLite locking lesson 被遵循，而新 Postgres path 在不同 premises 下设计，并被记录为 superseding decision。
- counterexample：repository 没有该 failure 的 prior record；这个 defect 是新知识（若随后没写下来就是 `unrecorded-lesson`）。

## Nudge

> Repository 真正有记忆，不是因为历史文件很多，而是过去已经证明过的失败会对今天的选择产生约束，直到新证据正式解除它。
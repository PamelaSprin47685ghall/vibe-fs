# command-event-confusion — Enforcer 中文版

## 定义
Command 与 Event 混淆，是把“请让某事发生”与“某事已经发生”交给同一种记录承担。两者 epistemic status 相反：command 可被拒绝；event 是历史事实，未来 replay 不应再对它重新投票。

混成一类后会出现两种腐败：意图在验证前被赋予历史权威，或历史在重放时被今天的 policy 重新审判。

## 何时触发
- `PlaceOrder` 在 authorization/validation 前直接 append 到 event log；
- replay old event 时重新跑当前 permission/business rules；
- 一个 message 用 `isValidated/isApplied` flags 同时表示 request 与 fact；
- current policy 改变后，旧历史 replay 得出不同世界；
- event handler 可能因为“今天不允许”而拒绝一个过去已发生的 event。

## 不要误判
- durable command queue 可以持久化**意图本身**，只要它明确仍是 rejectable request，后续成功另有 event；
- event validation 可以验证 schema/integrity，不等于重新做 business authorization；
- unknown event version 的 compatibility policy 可以 fail/skip，关键是不能把 past occurrence 当成今天的新 request；
- audit log 可记录“收到 command”，但那是收到请求的事实，不等于请求内容已成功实现。

## 刀口
对一条记录问两个问题：

1. 系统现在能合法回答“不做”吗？能，它是 command/intention。
2. 它描述的事情已经发生、replay 必须尊重吗？是，它是 event/fact。

同一条记录不该同时回答两个“是”。

## 与近邻区分
`overwrite-history` 是后来改写 event；这里是 event 从出生起就不是清楚的 fact。

`program-counter-state` 把“下一步做什么”持久化成状态；command 是合法意图，但不能冒充 event。

## 例子
- 正例：把 `DeleteAccount` request 直接写成 historical event，再由 handler 决定是否真的删除。
- 近邻：durable inbox 记录 `DeleteAccountRequested`，处理后可能发 `AccountDeleted` 或 `DeletionRejected`。
- 反例：replay `AccountDeleted` 时只应用事实，不重新问当前用户是否仍有 delete 权限。

## 提醒
意图属于现在的裁决；事实属于未来的记忆。不要让同一种记录既能被拒绝，又声称自己已经发生。

# command-event-confusion — Lesson

## 评价

### 定义

Command 与 Event 混淆，是把“请让某事发生”与“某事已经发生”交给同一种记录承担。两者 epistemic status 相反：command 可被拒绝；event 是历史事实，未来 replay 不应再对它重新投票。

混成一类后会出现两种腐败：意图在验证前被赋予历史权威，或历史在重放时被今天的 policy 重新审判。

### 支配原则

意图与事实的认知地位相反。Command 说“请让这件事成真”，可以被拒绝；event 说“这件事已经成真”，不能被后来的解释否决。把两者混为一谈，要么腐蚀 authorization，要么腐蚀 history：command 获得不应有的确定性，event 变成可撤销的意见。

### 何时触发

- `PlaceOrder` 在 authorization/validation 前直接 append 到 event log；
- replay old event 时重新跑当前 permission/business rules；
- 一个 message 用 `isValidated/isApplied` flags 同时表示 request 与 fact；
- current policy 改变后，旧历史 replay 得出不同世界；
- event handler 可能因为“今天不允许”而拒绝一个过去已发生的 event。

### 不应触发

- durable command queue 可以持久化**意图本身**，只要它明确仍是 rejectable request，后续成功另有 event；
- event validation 可以验证 schema/integrity，不等于重新做 business authorization；
- unknown event version 的 compatibility policy 可以 fail/skip，关键是不能把 past occurrence 当成今天的新 request；
- audit log 可记录“收到 command”，但那是收到请求的事实，不等于请求内容已成功实现。

### 与相邻规则区分

`overwrite-history` 是后来改写 event；这里是 event 从出生起就不是清楚的 fact。

`program-counter-state` 把“下一步做什么”持久化成状态；command 是合法意图，但不能冒充 event。

### 判定程序

对一条记录问两个问题：

1. 系统现在能合法回答“不做”吗？能，它是 command/intention。
2. 它描述的事情已经发生、replay 必须尊重吗？是，它是 event/fact。

同一条记录不该同时回答两个“是”。

### 例子

- 正例：把 `DeleteAccount` request 直接写成 historical event，再由 handler 决定是否真的删除。
- 近邻：durable inbox 记录 `DeleteAccountRequested`，处理后可能发 `AccountDeleted` 或 `DeletionRejected`。
- 反例：replay `AccountDeleted` 时只应用事实，不重新问当前用户是否仍有 delete 权限。

## 建议

### 现在该做什么

把 request 与 occurrence 分成不同类型、不同 handler、不同 durability meaning。Command 在当前 state/policy 下被 validate；只有成功后，才 append 描述真实发生结果的 event。Replay 只应用 event，不重新谈判过去。

### 为什么重要

如果未来 policy 能否决过去 event，历史会随代码升级而改变；如果未验证 command 直接成为 event，系统又会把“想做”伪造成“已做”。两种错误都破坏 replay 的可信度。

清晰分离后，current authority 与 historical authority 各归其位：policy 决定现在允许什么，event log 记录已经发生什么。

### 修复策略

- commands/events 使用不同命名与类型；
- command handler 返回 typed rejection 或 emitted events；
- event apply 保持 deterministic、policy-free；
- durable command inbox 若需要，明确其 lifecycle，不把 command payload 当 outcome；
- replay 只检查 integrity/version compatibility，不重新 authorization；
- 过去需要 correction 时 append compensation/supersession event。

### 决策分支

- 若这条记录仍可以被拒绝，把它当 command，不要当作历史 append；
- 若它是已经发生的 occurrence，把它当事实重放，不受当前 policy 否决；
- 若两种含义目前共用同一个形状，先拆类型，不要加 flags。

### 常见假修复

- 一个 message 加 `isValidated` flag。
- command/event 共用 DTO，仅靠 topic name 猜语义。
- replay 时 catch “现在 policy 不允许”然后 skip old event。
- 为少建一个 type，把 command payload 原样存成 event。
- 认为“写入队列”就等于业务 effect 成功。

### 验证

改变当前 authorization/business policy，再 replay 同一 historical event stream：历史 state 应保持不变。

同时构造 invalid command：它应在 emit fact 前被拒绝，event log 不应出现“其实没发生”的 occurrence。

### 完成条件

每个 durable record 的 epistemic status 清楚：request 可以被拒绝；event 一旦 committed 就作为发生过的事实被重放。

### Nudge

意图属于现在的裁决；事实属于未来的记忆。不要让同一种记录既能被拒绝，又声称自己已经发生。

# command-event-confusion — Main 中文版

## 现在该做什么
把 request 与 occurrence 分成不同类型、不同 handler、不同 durability meaning。Command 在当前 state/policy 下被 validate；只有成功后，才 append 描述真实发生结果的 event。Replay 只应用 event，不重新谈判过去。

## 为什么这很重要
如果未来 policy 能否决过去 event，历史会随代码升级而改变；如果未验证 command 直接成为 event，系统又会把“想做”伪造成“已做”。两种错误都破坏 replay 的可信度。

清晰分离后，current authority 与 historical authority 各归其位：policy 决定现在允许什么，event log 记录已经发生什么。

## 修复策略
- commands/events 使用不同命名与类型；
- command handler 返回 typed rejection 或 emitted events；
- event apply 保持 deterministic、policy-free；
- durable command inbox 若需要，明确其 lifecycle，不把 command payload 当 outcome；
- replay 只检查 integrity/version compatibility，不重新 authorization；
- 过去需要 correction 时 append compensation/supersession event。

## 常见假修复
- 一个 message 加 `isValidated` flag。
- command/event 共用 DTO，仅靠 topic name 猜语义。
- replay 时 catch “现在 policy 不允许”然后 skip old event。
- 为少建一个 type，把 command payload 原样存成 event。
- 认为“写入队列”就等于业务 effect 成功。

## 验证
改变当前 authorization/business policy，再 replay 同一 historical event stream：历史 state 应保持不变。

同时构造 invalid command：它应在 emit fact 前被拒绝，event log 不应出现“其实没发生”的 occurrence。

## 完成条件
每个 durable record 的 epistemic status 清楚：request 可以被拒绝；event 一旦 committed 就作为发生过的事实被重放。

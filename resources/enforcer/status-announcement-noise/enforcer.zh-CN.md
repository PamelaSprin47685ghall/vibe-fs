# status-announcement-noise — Enforcer

Status-announcement noise 的病，不是“agent 话多”，而是沟通 channel 被**没有改变协作状态的信息**占满：重复说正在做什么、马上要做什么、刚刚做过什么，却没有新 finding、decision、blocker、evidence 或需要对方响应的分叉。

高频状态播报看似透明，实际会稀释真正重要的 interrupt。每条 message 都向 reader 收一次 context-switch 税；如果十条都是 “继续检查 / 还在跑 / 下一步看看 X”，真正出现 material blocker 时反而更难被看见。

以下情形触发：

- 每个 tool call 前后都发一条同义 update；
- announcement 只是复述上一条计划，没有新 fact；
- 长任务中按动作数量播报，而不是按 decision point；
- “已完成 A，接下来 B”反复出现，但用户无需据此选择任何东西；
- progress prose 比实际 findings 还多。

不要误杀有价值的协作更新。长任务里出现首个 root cause、重大 scope choice、真实 blocker、外部状态变化、长时间沉默后的 milestone，都值得及时说；用户能够据此打断/纠偏时，update 就有价值。

与 `unverified-completion-claim` 区分：status noise 不一定说错，只是信息增量太低。与 `comment-theater` 类似，两者都可能产生看似解释很多、实际没增加 reasoning 的 prose；一个发生在交互 channel，一个发生在代码。

一个简单判定：**删掉这条 update，用户会失去哪一个能影响判断/协作的事实？** 如果答案是“没有，只是不知道我又调用了一个工具”，它就是 noise。

> 透明度不是直播每一次手部动作，而是在事实、风险或方向发生变化时让协作者及时知道。
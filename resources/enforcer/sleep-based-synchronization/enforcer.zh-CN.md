# sleep-based-synchronization — Enforcer

Sleep-based synchronization 的病，是把**时间过去了**偷换成**前置事实已经成立**。

代码真正想说的是“等 X ready”，实现却写成“等 500ms，希望到时候 X 大概 ready”。这整个替换本身就是缺陷。

固定 delay 从来不能证明 readiness、completion、visibility、ownership transfer、lock release、process startup、replication、event delivery。它只是在改变“这些事可能已经发生”的概率。

所以这种写法永远有两种坏模式：

- 机器快时，cause 早已发生，代码还白等；
- 机器慢/争用高时，cause 尚未发生，代码已经继续。

同一个 sleep 同时太长又太短。

以下情况触发：

- 启动 writer 后 `sleep(500)` 再断言 file exists；
- “等两秒让 server 起好”而没有 readiness observation；
- 因为“replication 通常这时稳定了”而 delay 后读状态；
- test teardown sleep，给 child process “时间退出”；
- retry loop 只等时间，不检查真实 state；
- UI/agent workflow 插 pause 避 race，而不是等待 completion / ownership signal。

不要看到 sleep 就触发。Rate limiting、protocol backoff、jitter、scheduled cadence、animation/human pacing、fault injection、真正 time-domain product behavior 都可能合法依赖 elapsed time。Timeout 也可以给 causal wait 设上限，而不成为 success signal。

区分标准很精确：

> **如果下一步之所以被允许，只因为 clock expired，问问你真正需要的是不是一个事实。**

如果 success 仍由 event/state 决定，clock 只是 “N 秒后放弃”，那它是 policy，不是 synchronization。

邻近规则：

- `timeout-inflated-to-pass`：把 uncertainty budget 调大掩盖 failure；
- `time-dependent-test`：test 更广泛依赖真实 wall-clock/calendar；
- `blocking-event-loop`：等待时霸占 shared executor；
- `repeat-until-pass`：不断采样直到出现 green。

本规则只在 elapsed duration 正在冒充 causality 时触发。

真正修复从“把希望发生的事实说出来”开始。不是“等 500ms”，而是：

- process emitted ready；
- file 出现且 generation 正确；
- callback completed；
- session reached idle；
- lock/lease released；
- replication observed version V；
- child terminated；
- event identity X committed。

然后等**这个事实**。如果无法提供 event，poll authoritative state 也可以，但必须有 bounded timeout，timeout 只意味着失败/未知，不能意味着“算成功吧”。

> 不要在你真正想等因果时去等时间。
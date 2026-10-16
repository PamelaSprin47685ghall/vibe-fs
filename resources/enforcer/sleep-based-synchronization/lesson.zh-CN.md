# sleep-based-synchronization — Lesson

## 评价

### 定义

Sleep-based synchronization 的病，是把**时间过去了**偷换成**前置事实已经成立**。

代码真正想说的是“等 X ready”，实现却写成“等 500ms，希望到时候 X 大概 ready”。这整个替换本身就是缺陷。

固定 delay 从来不能证明 readiness、completion、visibility、ownership transfer、lock release、process startup、replication、event delivery。它只是在改变“这些事可能已经发生”的概率。

所以这种写法永远有两种坏模式：

- 机器快时，cause 早已发生，代码还白等；
- 机器慢/争用高时，cause 尚未发生，代码已经继续。

同一个 sleep 同时太长又太短。

### 支配原则

区分标准很精确：

> **如果下一步之所以被允许，只因为 clock expired，问问你真正需要的是不是一个事实。**

如果 success 仍由 event/state 决定，clock 只是 “N 秒后放弃”，那它是 policy，不是 synchronization。

### 何时触发

以下情况触发：

- 启动 writer 后 `sleep(500)` 再断言 file exists；
- “等两秒让 server 起好”而没有 readiness observation；
- 因为“replication 通常这时稳定了”而 delay 后读状态；
- test teardown sleep，给 child process “时间退出”；
- retry loop 只等时间，不检查真实 state；
- UI/agent workflow 插 pause 避 race，而不是等待 completion / ownership signal。

### 不应触发

不要看到 sleep 就触发。Rate limiting、protocol backoff、jitter、scheduled cadence、animation/human pacing、fault injection、真正 time-domain product behavior 都可能合法依赖 elapsed time。Timeout 也可以给 causal wait 设上限，而不成为 success signal。

### 与相邻规则区分

邻近规则：

- `timeout-inflated-to-pass`：把 uncertainty budget 调大掩盖 failure；
- `time-dependent-test`：test 更广泛依赖真实 wall-clock/calendar；
- `blocking-event-loop`：等待时霸占 shared executor；
- `repeat-until-pass`：不断采样直到出现 green。

### 判定程序

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

### 例子

启动 writer 后 `sleep(500)` 再断言 file exists。“等两秒让 server 起好”而没有 readiness observation。test teardown sleep，给 child process “时间退出”。

## 建议

### 现在该做什么

把 duration 换回事实。

先写清楚这个 sleep 假装在建立什么。如果答案是 readiness、completion、visibility、shutdown、propagation、lock release、ownership transfer，就暴露一个真正能证明该条件的 observation。

优先顺序通常是：

1. await operation 自己的 completion/result；
2. subscribe 建立条件的 event；
3. await subsystem 自己拥有的 readiness/termination primitive；
4. 实在没有 event 时，在 bounded timeout 内 poll authoritative state/version。

无论哪种，timeout 只有一个职责：**限制 uncertainty 可以持续多久**。Clock expiry 不能把 uncertainty 变成 success。

### 为什么重要

Process startup 要等真实 readiness：health endpoint（且语义正确）、port + protocol handshake、ready event，而不是“process 存在两秒了”。

Process shutdown 要等 exit/termination 与 resource release，而不是“发了 SIGTERM，再睡一下”。

Storage/replication 要等能证明目标事实 visible 的 generation/version/commit identity，而不是“eventual consistency 应该差不多了”。

Test 中如果 timing source 本身不是被测对象，优先 deterministic fake / controllable scheduler。一个本地 state transition 要靠 30 秒 wall-clock patience 才能证明，通常已经把 scheduler 一起测进去了。

### 修复策略

Polling 要同时验证两面：

- condition 成真后 promptly 继续；
- timeout expiry 明确失败/unknown，绝不伪造 success。

Event wait 还要测 missed-event race：subscription 是否早于 event 发生，或者是否用 current-state check 关闭 gap。把 sleep 换成 callback，却引入 subscribe-after-complete，并没有更正确。

### 决策分支

把 producer 人为拖得远超旧 sleep：consumer 绝不能提前继续。再让 producer 立即完成：consumer 应立即前进，不再支付固定 delay。

### 常见假修复

常见假修复：

- 500ms 改 5s；
- 一个长 sleep 换成十个短 sleep，但从不检查 causal state；
- busy loop 到同样 wall-clock duration；
- 已有 readiness signal 后还保留 “for stability” sleep；
- sleep 后再 retry，让 flake 少一点但 missing synchronization 仍在；
- timeout callback 因“暂时没出错”就把 operation 标 success。

### 验证

验证要主动攻击 scheduler assumption。把 producer 人为拖得远超旧 sleep：consumer 绝不能提前继续。再让 producer 立即完成：consumer 应立即前进，不再支付固定 delay。

### 完成条件

完成时，每个 wait 都应该能写成：

> 我不能继续，直到**这个可观察事实**成立；clock 只限制我愿意保持 uncertainty 多久。

这才叫 synchronization。 “等这么久通常够了”只是概率披上了因果外套。

### Nudge

> 不要在你真正想等因果时去等时间。

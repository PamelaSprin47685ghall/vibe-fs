# sleep-based-synchronization — Main

把 duration 换回事实。

先写清楚这个 sleep 假装在建立什么。如果答案是 readiness、completion、visibility、shutdown、propagation、lock release、ownership transfer，就暴露一个真正能证明该条件的 observation。

优先顺序通常是：

1. await operation 自己的 completion/result；
2. subscribe 建立条件的 event；
3. await subsystem 自己拥有的 readiness/termination primitive；
4. 实在没有 event 时，在 bounded timeout 内 poll authoritative state/version。

无论哪种，timeout 只有一个职责：**限制 uncertainty 可以持续多久**。Clock expiry 不能把 uncertainty 变成 success。

Process startup 要等真实 readiness：health endpoint（且语义正确）、port + protocol handshake、ready event，而不是“process 存在两秒了”。

Process shutdown 要等 exit/termination 与 resource release，而不是“发了 SIGTERM，再睡一下”。

Storage/replication 要等能证明目标事实 visible 的 generation/version/commit identity，而不是“eventual consistency 应该差不多了”。

Test 中如果 timing source 本身不是被测对象，优先 deterministic fake / controllable scheduler。一个本地 state transition 要靠 30 秒 wall-clock patience 才能证明，通常已经把 scheduler 一起测进去了。

常见假修复：

- 500ms 改 5s；
- 一个长 sleep 换成十个短 sleep，但从不检查 causal state；
- busy loop 到同样 wall-clock duration；
- 已有 readiness signal 后还保留 “for stability” sleep；
- sleep 后再 retry，让 flake 少一点但 missing synchronization 仍在；
- timeout callback 因“暂时没出错”就把 operation 标 success。

验证要主动攻击 scheduler assumption。把 producer 人为拖得远超旧 sleep：consumer 绝不能提前继续。再让 producer 立即完成：consumer 应立即前进，不再支付固定 delay。

Polling 要同时验证两面：

- condition 成真后 promptly 继续；
- timeout expiry 明确失败/unknown，绝不伪造 success。

Event wait 还要测 missed-event race：subscription 是否早于 event 发生，或者是否用 current-state check 关闭 gap。把 sleep 换成 callback，却引入 subscribe-after-complete，并没有更正确。

完成时，每个 wait 都应该能写成：

> 我不能继续，直到**这个可观察事实**成立；clock 只限制我愿意保持 uncertainty 多久。

这才叫 synchronization。 “等这么久通常够了”只是概率披上了因果外套。
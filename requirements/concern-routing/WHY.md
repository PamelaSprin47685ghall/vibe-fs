# concern-routing — 为什么必须独立存在

## 1. 一个不可替代的存在理由

multi-agent 最昂贵的通信失败不是“消息发不出去”，而是为了得到相关信息先建立身份关系，或让每个状态变化都即时打断别人。前者让组织拓扑变成路由表，后者把系统变成 Slack。

本包只提供两个动作：

```text
subscribe(id, concern)  我创建一个语义地址，声明“我关心什么”
publish(id, message)    我有与这个 concern 相关的信息，发给该地址
```

发送者不需要知道 mailbox owner 是谁。消息可以早到，但注意力只在 owner 下一次 Pair Hint 这个自然边界被打断。这样增加知识不增加长期 reporting relation，把 person-to-person coordination 压成 concern-addressed communication。

## 2. 为什么不并入其它包

- 不并入 `interaction-authority`：peer message 不是 user-shaped authority，也不得 mint Authority Root。
- 不并入 `dispatch-protocol`：这里没有“逻辑 interaction 穿越不可靠 Host”的用户 effect；核心是语义地址、mailbox 与 delayed attention boundary。
- 不并入 `guidance-delivery`：后者拥有 Enforcer guidance 的 delivery/coverage；本包拥有 mailbox message/subscription announcement 的身份、路由与消费语义。
- 不并入 `participant-identity`：owner 是 participant，但 concern address 不应把身份结构本身变成通信 API。

## 3. FAILURE MEANING

RED = 发送者必须先知道“谁负责”；subscribe 建立持续 social/reporting topology；publish 立即插入 active context 打断工作；同一消息因 retry/replay 重复出现；旧 owner 结束后 mailbox 继续收信；peer message 被当成 authority/事实；所有状态广播给所有人而不是按 concern 路由。

## 4. 被拒方案

- owner directory / org chart 作为路由真相：发送者再次耦合身份拓扑。
- generic pub/sub broker with topics/acks/QoS：schema 与运维复杂度远大于两个 speech acts。
- 即时 push interrupt：破坏自然 attention boundary。
- 每轮 Pair Hint 重复所有 active subscriptions：把 semantic address 变成永久 prompt tax。

## DEPENDS ON

- `participant-identity`：mailbox owner/lifetime 必须绑定真实 participant。
- `participant-horizon`：peer message 只是一条可见信息，不得反向制造 authority。
- `durable-events`：subscription、message 与 delivery coverage 需要 restart/replay 稳定。

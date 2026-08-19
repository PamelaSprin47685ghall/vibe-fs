# concern-routing — HOW（非 normative）

## 目标实现形状

一个很小的 durable mailbox projection 足够：

```text
ConcernAddressDeclared(id, concern)              # id semantic meaning; never changes
Subscribed(generation, id, ownerParticipant)     # explicit owner generation
Published(messageOccurrence, generation, id, senderParticipant, message)
SubscriptionAnnounced(generation, recipientParticipant, pairOccurrence)
MessageDelivered(messageOccurrence, generation, ownerParticipant, pairOccurrence)
MailboxRetired(generation, id, ownerParticipant)
```

具体 fact 是否合并、哪些字段由现有 participant/session facts 派生属于实现选择；WHAT 只要求结果可重放、不重复、不卡 active context。

`subscribe` / `publish` adapter 均保持两个 string 参数，不暴露 owner/session/message-id。Pair Hint composition 调本包窄
`prepareFragments(participant, pairOccurrence)`，返回当前 occurrence 应显示的 typed fragments 与尚未提交的
`SubscriptionAnnounced` / `MessageDelivered` facts；现有 guideline owner 组合并冻结最终 MarkerText 后，把 pair fact
与这些 coverage facts 同批原子提交。prepare 本身不消费 queue。

## provider representation

建议紧凑形状：

```text
Subscriptions discovered:
- <id>: <concern>

Mailbox messages:
- <id>: <message>
```

不输出 ACK、sequence、session id、owner machine name、delivery frontier 等内部字段。

## DEPENDS ON

`concern-routing → participant-identity, participant-horizon, durable-events`

## 验证与测试落点

可执行 proof 在 review 后由 GAP 建立：

| WHAT | 最低充分 proof |
|---|---|
| CONCERN-ROUTING-001 | pure claim/idempotency/conflict algebra；id→concern immutable；workspace isolation |
| CONCERN-ROUTING-002 | temporal multi-participant Pair Hint coverage；sticky once |
| CONCERN-ROUTING-003 | publish route：sender 不知道 recipient；unknown/retired fail closed；resolve→append generation stale 不自动 retarget |
| CONCERN-ROUTING-004 | temporal：publish 不 interrupt；next pair delivers；pair placement + concern coverage atomic；failed placement zero-consume；replay byte-stable/no redrain |
| CONCERN-ROUTING-005 | authority negative：peer message 不 mint user authority/obligation |
| CONCERN-ROUTING-006 | owner termination/late publish；same-concern explicit rebind creates new generation；old messages do not cross generation |
| CONCERN-ROUTING-007 | architecture negative：无 generic broker/org graph/priority workflow |

## 历史与弃权

不采用 single semantic owner、consult/join、interrupt membrane 等更大的组织机制；当前 accepted primitive 只有 `subscribe/publish`，其价值来自“地址按 concern、交付按自然 attention boundary”。

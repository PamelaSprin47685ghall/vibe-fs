# concern-routing — HOW

## 架构机制与数据流

`concern-routing` 通过极简的 durable mailbox 事实流构建动态路由：

1. **事实投影**：
   - `ConcernAddressDeclared(id, concern)`：记录全局唯一的地址语义映射。
   - `Subscribed(generation, id, ownerParticipant)`：显式代次的 mailbox 绑定。
   - `Published(messageOccurrence, generation, id, senderParticipant, message)`：挂靠到具体代次的消息事实。
   - `SubscriptionAnnounced(generation, recipientParticipant, pairOccurrence)`：公告覆盖跟踪。
   - `MessageDelivered(messageOccurrence, generation, ownerParticipant, pairOccurrence)`：交付覆盖跟踪。
   - `MailboxRetired(generation, id, ownerParticipant)`：显式退役记录。

2. **Pair Hint 聚合**：
   `prepareFragments(participant, pairOccurrence)` 计算当前批次应展示的地址公告与未读消息碎片，暂存待提交的 coverage facts。上层 guideline 模块完成 MarkerText 冻结后，将 Pair Hint 事实与 coverage facts 原子提交，保证不会因为上下文组装失败而丢失消息。

3. **Provider 呈现**：
   将已发现的订阅与接收到的邮箱消息紧凑呈现为自然语言文本块，屏蔽底层代次、ACK 序号与内部路由拓扑。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| CONCERN-ROUTING-001 | pure claim/idempotency/conflict algebra；id→concern immutable；workspace isolation |
| CONCERN-ROUTING-002 | temporal multi-participant Pair Hint coverage；sticky once |
| CONCERN-ROUTING-003 | publish route：sender 不知道 recipient；unknown/retired fail closed；resolve→append generation stale 不自动 retarget |
| CONCERN-ROUTING-004 | temporal：publish 不 interrupt；next pair delivers；pair placement + concern coverage atomic；failed placement zero-consume；replay byte-stable/no redrain |
| CONCERN-ROUTING-005 | authority negative：peer message 不 mint user authority/obligation |
| CONCERN-ROUTING-006 | owner termination/late publish；same-concern explicit rebind creates new generation；old messages do not cross generation |
| CONCERN-ROUTING-007 | architecture negative：无 generic broker/org graph/priority workflow |

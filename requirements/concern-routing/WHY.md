# concern-routing — WHY

## 1. 领域动机与核心矛盾

多 agent 协作中最昂贵的通信失败不是消息无法送达，而是：
1. **先建身份拓扑才能通信**：发送方必须先知道“谁负责哪件事”，把动态协作硬编码为静态汇报关系与组织架构。
2. **即时打断注意力**：状态变更或外部信息即时推入接收方 active context，打断正在执行的推理与尝试。

`concern-routing` 确立以 **concern 寻址**的通信模型：
- 接收方声明关注点（`subscribe(id, concern)`），创建语义邮箱；
- 发送方仅面向语义地址投递（`publish(id, message)`），无需感知接收者身份或拓扑；
- 消息异步存入邮箱，且仅在接收方下一次 Pair Hint 自然认知边界交付。

## 2. 核心不变量与破坏后果

- **地址解耦**：发送者不依赖接收者运行时身份或生命周期；若破坏，组织拓扑将侵入消息路由，换执行者将导致路由断裂。
- **注意力保护**：消息早到不等于即时打断，严格在 Pair Hint 边界批量消费；若破坏，并发消息将随机污染执行上下文。
- **极小事实与不可变语义**：`id → concern` 语义映射一旦出生不可变更，路由仅维护 live subscription 与 delivery coverage 的极小事实集。
- **低 Authority 信息**：peer message 仅作为可见观察输入，绝不直接升级为 user authority 或自动产生业务 obligation。

## DEPENDS ON

- `participant-identity`
- `participant-horizon`
- `durable-events`

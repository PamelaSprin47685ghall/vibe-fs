# concern-routing — WHAT

## CONCERN-ROUTING-001: `subscribe` 创建 concern-addressed mailbox 而非 reporting relation

`subscribe(id, concern)` 接受两个非空自然语言字符串。成功后 `id` 成为当前 workspace 内的语义地址，mailbox owner 即为发起调用的精确 participant。`concern` 仅定义值得投递的信息意图，不授予 owner 对发送者的控制权，亦不要求发送者感知 owner 身份。路由隔离在单一 workspace 内。

`id → concern` 的语义映射在 workspace 内全局稳定且不可变：同一 `id` 永久绑定初始 concern。live mailbox id 不得同时指向多个 owner。同一 owner 对相同 `id + concern` 的重放必须幂等；冲突 claim 必须显式拒绝，禁止 last-writer-wins 覆盖或偷换 concern。

## CONCERN-ROUTING-002: subscription announcement 是 sticky-once semantic address discovery

live subscription 建立后，所有有资格接收 Pair Hint 的 live participant（含 owner 本身）必须在各自下一次新的 Pair Hint occurrence 中获得一次紧凑公告（`id + concern`）。

公告仅用于广播语义地址的存在，不暴露 owner 的运行时拓扑，不产生工作义务。同一 subscription 不得在后续 Pair Hint 中重复广播；新加入的 eligible participant 仅在其首个可用 Pair Hint 中接收尚未见过的 live subscriptions。

## CONCERN-ROUTING-003: `publish` 只按 semantic address 路由

`publish(id, message)` 接受非空 `id` 与自然语言 `message`。仅当前 live subscription 可接收；未知、已退休或冲突的 `id` 必须 fail-closed，禁止广播或猜测接收方。

成功 publish 记录消息事件。发送者身份仅用于审计与去重，无需显式指定 recipient。publish 异步完成，不阻塞等待消费，不打断 owner 当前的 provider attempt。

消息必须严格绑定接受时的 exact live mailbox generation。若在解析与写入之间 mailbox 发生 retire 或 rebind，publish 必须作为 stale claim 拒绝，禁止自动转投新代次 owner。

## CONCERN-ROUTING-004: 消息只在 owner 下一次新 Pair Hint 自然边界交付

已接受的 mailbox 消息不即时注入 active context。owner 仅在下一次新的 Pair Hint occurrence 聚合消费尚未交付的 pending messages，并与该 Pair Hint 的 frozen provider payload 一同呈现。

同一 Pair Hint 的重放必须保证消息载荷 byte-identical，不重复消费队列。消息交付覆盖（`MessageDelivered`）及公告覆盖（`SubscriptionAnnounced`）必须与 Pair Hint 的生成在同一原子事务中提交；若 placement 放弃或失败，交付状态回滚，留待下一合法 occurrence 重试。

## CONCERN-ROUTING-005: peer message 是低 authority 信息

subscription announcement 与 published message 均不得 mint 或 continue user interaction authority，不得变更 office entitlement，不得自动创建 obligation，亦不得被接收方直接视为已验证的世界事实。接收方必须按领域证据法独立判定是否据此采取行动。

## CONCERN-ROUTING-006: mailbox 生命周期跟随 owner participant life

mailbox generation 仅在 owner participant 存活期内有效。owner 终结后 generation 退休，此时新的 publish 必须 fail-closed。已接受但未交付的消息随 generation 退休而终结，禁止跨代或向 replacement/child 自动继承。

后继 participant 可显式针对同一 `id` 重新 `subscribe`，前提是 `concern` 必须与既有不可变语义完全一致。该操作产生全新的 mailbox generation，并重新触发一次 sticky announcement。旧代次未决消息与 delivery coverage 永久作废。

## CONCERN-ROUTING-007: 路由表保持极小

系统仅维护 live subscriptions、message occurrences 以及 recipient announcement/delivery coverage 的最小事实集合。禁止引入组织层级、presence 派生 authority、优先级调度、工作流编排、实时 ACK 协议或通用事件总线机制。

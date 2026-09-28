# concern-routing — WHAT

## [001] Subscribe：语义地址

`subscribe(id, concern)` 接受非空自然语言字符串，在当前 workspace 建立由调用 participant 拥有的邮箱。`concern` 只说明值得投递的信息，不建立汇报关系或控制权；发送者无需知道 owner 身份。

同一 workspace 的 `id → concern` 永久不变；live id 只能有一个 owner。同一 owner 对相同 id 与 concern 重放幂等，冲突必须明确拒绝，不得覆盖。

## [002] 公告只交付一次

live subscription 建立后，每个有资格接收 Pair Hint 的 live participant（含 owner）在下一次新 Pair Hint 中收到一次紧凑的 `id + concern` 公告。新加入者在首个可用 Pair Hint 收到尚未见过的 live subscriptions，后续不重复广播。

公告只发现语义地址，不暴露 owner 运行时拓扑，不产生工作义务。

## [003] Publish：精确代次

`publish(id, message)` 接受非空 id 与自然语言 message，只向当前 live subscription 投递；未知、退休或冲突地址必须拒绝，不广播、不猜测收件方。发送者身份只用于审计与去重。

成功 publish 记录消息事件，异步返回，不等消费、不打断 owner 的 provider attempt。消息绑定接受时的 exact mailbox generation；解析后、写入前若退休或换代，必须拒绝 stale claim，不得转投新 owner。

## [004] Pair Hint 边界交付

pending 消息只在 owner 的下一次新 Pair Hint 聚合交付，不即时注入 active context。消息与该 occurrence 的 provider payload 一起冻结；重放须 byte-identical，不重复消费。

消息与公告的交付覆盖必须与 Pair Hint 生成原子提交。placement 放弃或失败不得留下已交付状态，材料留待下一合法 occurrence。

## [005] 信息不授予权威

公告和 peer message 不得创建或延续 user interaction authority、改变 office entitlement 或自动创建 obligation。消息不是已验证的世界事实，接收方须按领域证据独立判断行动。

## [006] 邮箱随 owner life 退休

owner participant 终结时，其 mailbox generation 退休；新 publish 拒绝，未交付消息终结，不向 replacement 或 child 继承。

后继 participant 可显式重新 subscribe 同一 id，但 concern 必须保持原义。新 generation 重新公告；旧代消息和交付覆盖不得沿用。

## [007] 路由范围最小化

只维护语义地址、邮箱、消息 occurrence 及公告/交付覆盖所需的最小事实。不引入组织层级、由 presence 派生的 authority、优先级调度、工作流编排、实时 ACK 或通用事件总线。

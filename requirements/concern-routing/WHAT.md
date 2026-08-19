# concern-routing — WHAT（唯一 normative 合同）

命题前缀 `CONCERN-ROUTING-`。

## CONCERN-ROUTING-001：`subscribe` 创建 concern-addressed mailbox，而不是 reporting relation

`subscribe(id, concern)` 接受两个非空自然语言字符串。成功后 `id` 成为当前 workspace 内的语义地址，mailbox owner = 调用它的精确 participant；`concern` 只描述“什么信息值得发到这里”，不授予 owner 对发送者的管理权，也不要求发送者知道 owner 身份。routing 不跨 workspace。

`id → concern` 的语义映射一旦在该 workspace 出生就永久稳定：同一 id 以后不得改表示另一个 concern。一个 live mailbox id 不得同时指向两个 owner。相同 owner 对相同 `id + concern` 的同一 tool occurrence 重放必须幂等；冲突 claim 必须显式拒绝，不能 last-writer-wins 偷换收件人或 concern。

## CONCERN-ROUTING-002：subscription announcement 是 sticky-once semantic address discovery

一个 live subscription 建立后，所有有资格接收 Pair Hint 的 live participant（包括 owner）必须在各自下一次新的 Pair Hint occurrence 中看到一次紧凑 announcement：`id` + `concern`。之后同一 subscription 不再反复占用 Pair Hint；后来新出现的 eligible participant 在其首个可用 Pair Hint 中也应看到当前仍 live、尚未覆盖的 subscription。

announcement 只使别人知道“世界上有一个地址关心这件事”，不暴露 mailbox owner 的 runtime topology，也不创建工作义务。

## CONCERN-ROUTING-003：`publish` 只按 semantic address 路由，不要求知道收件人

`publish(id, message)` 接受非空 id 与非空自然语言 message。只有当前 live subscription 才能接收；未知、已退休或冲突 id 必须返回封闭失败，不静默广播、不猜 owner。

成功 publish 创建一个 mailbox message occurrence；sender identity 可用于 provenance/dedupe，但 provider 调用方不需要提供 recipient。publish 本身不等待 owner 消费，也不打断 owner 当前 provider attempt。

message occurrence 必须绑定 publish 被接受瞬间的 exact live mailbox generation。若 id 在 resolve 与 atomic append
之间 retire/rebind，publish 必须作为 stale claim fail/重试，不能把原本针对旧 generation 的消息自动重定向到
刚接棒的新 owner；accepted message 一旦带 generation 写入，后续 rebind 也不能改变其收件世代。

## CONCERN-ROUTING-004：消息只在 owner 下一次新 Pair Hint 自然边界交付

accepted mailbox message 不即时注入 active context。owner 下一次新的 Pair Hint occurrence 必须组合当前尚未交付的 mailbox messages；这些 message 随该 Pair Hint 的 frozen provider-visible payload 一起被消费。

同一 Pair Hint replay 必须 byte-identical 重放同一批消息而不是再次消费 queue；后续 Pair Hint 不重复已经交付的 message。消息可以早到，注意力只能在 Pair Hint 边界被打断。

subscription announcement 与 mailbox message 的 delivery coverage 不得先于 Pair Hint placement 单独提交。concern-routing
必须先 staging 本 occurrence 应组合的 fragments + 对应 coverage facts；只有 `guidance-delivery` 成功冻结该
Pair Hint occurrence 时，`SubscriptionAnnounced` / `MessageDelivered` 才与 pair placement 在同一 atomic durable
commit 生效。placement 失败/放弃 → zero concern delivery commit，下一合法 occurrence 仍可重试。

## CONCERN-ROUTING-005：peer message 是低 authority 信息，不是事实/命令

subscription announcement 与 published message 都不得 mint/continue user interaction authority、不得改变 office entitlement、不得自动创建 obligation、不得被 receiver 当成已经验证的 world fact。receiver 可据此调查、行动、转述或忽略，是否足以改变 action 仍由对应领域 evidence law 决定。

## CONCERN-ROUTING-006：mailbox 生命周期跟随 owner participant life

subscription generation 只在 owner participant life 存活；owner 终止后该 generation 退休，此时 publish 必须 fail closed。已 durable 接受但尚未交付的消息随该 generation retirement 终止为不可再投递，不得自动改投 replacement/child/同 persona 的另一个 execution。

后来 participant 可以显式 `subscribe` 同一个 `id` 接棒，但只允许 concern 与该 id 的 durable semantic mapping 完全相同；这会创建新的 mailbox generation 并重新触发 CR-002 的 sticky-once announcement。旧 generation 的 pending messages / delivery coverage 不跨代继承。换 owner 必须显式，换 concern 必须换 id。

## CONCERN-ROUTING-007：路由表保持极小

系统只维护 live subscription、message occurrence、per-recipient announcement coverage 与 owner delivery coverage 所需的最小事实。禁止引入组织层级、presence-derived authority、priority、fan-out workflow、实时 ack protocol、topic hierarchy 或 generic event bus 作为产品语义。

## 边界

- Pair Hint 的 canonical craft 正文 → `cognitive-environment`。
- Pair Hint occurrence 的 frozen wire/coverage → `guidance-delivery` / `prefix-stability`。
- tool schema/runtime 可见性 → `capability-enforcement`。
- participant 何时存在/终止 → `participant-identity` / session owners。

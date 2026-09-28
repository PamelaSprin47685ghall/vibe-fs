# execution-model-routing — WHAT

## [001] 唯一调度配置

Managed 模型调度仅由 `~/.config/opencode/wanxiangshu.mjs` 的 default export 决定；`opencode.json`、环境变量、Host inventory 和内建表不得覆盖或替代。缺文件时原子创建推荐模板再加载，已有文件绝不覆盖；创建后仍缺失、加载失败或导出非法均 fail closed。

## [002] Scheduler ABI

唯一签名为 `route(role, running, previous) → { model, reasoning } | null`。

- `role` 来自 IdentitySeed，在 logical run 内固定；participant 显式随 acquire 传递，不是 scheduler 参数，也不等于远端 ModelTarget。
- `running` 是进程持有的 provider capacity token 的 ModelTarget multiset，元素为 `{ model: string, reasoning: string }`，保留重复。
- `previous` 只取被同一 session 的新物理执行原子取代的当前活跃 target；新 session、terminal 后重建或无关 session 为 `null`。它只是偏好，不占容量；[017] 的显式 recovery retry 绑定另有优先权。
- target 含非空 `provider/model` 与 `reasoning`。抛错、Promise 或非法结构均作为配置错误 fail closed。

## [003] 真实容量

`running.length` 等于实际持有的基础 token 数，不以会话或执行数量代替。同一进程所有插件实例和 worktree 共享这一真相；同一 token 被显式借用仍只计一次。

## [004] 准入与等待

Required demand 只在 Host `chat.message` 接收物理 user message 时准入；发送或排队前不预占 model slot，SendPrompt 保持 `Model=None`。scheduler 返回 `null` 表示等待，不调用 provider、不耗失败预算；pending demand 随 occupancy 变化重算，新物理消息或会话销毁取消被取代的旧 demand。

## [005] 策略与资源分工

模型候选、优先级及并发限制全部由 MJS 策略决定。Runtime 只负责配置加载与 ABI 校验、共享 token ledger 和借贷仲裁，不另设模型分类、lane、容量表或选择算法。

## [006] 物理执行内稳定

租约绑定 `(SessionId, PhysicalUserMessageId)`；同一物理执行及重试复用 target/fence，不重新调度或改变 Role、participant、agent。新物理执行原子替代旧租约，以固定 Role 重新调度，身份不变；仅当前活跃旧执行提供 previous，terminal 后不留 session 级 previous 缓存。

Provider step 结束将实际 lease target 与 exact ProviderRunIdentity 绑定，失败结算仅原子消费该 witness。每个 session 只保留最新 witness，新 run 废除旧 witness，不从可变 session-last target 猜测失败 provider。

## [007] 终结与释放

正常 physical execution 结束须有无 error、completed assistant、`finish=stop|length|content-filter`，且 parentID 匹配 PhysicalUserMessageId 的明确证据。`tool-calls`、assistant error 或 `finish=unknown|error` 只结束 provider step、归还 step token，保留物理绑定供同一 material 重试。业务 handle、join、finality 不直接释放租约。

## [008] 角色不要求不同模型

`opencode.json` 不拥有 managed model authority。不同合法角色或历史 fast/deep 档可使用同一物理 target，不以模型字符串互异作为资格条件。

## [009] Host 投影边界

内部 synthetic prompt 分派保持 `Model=None`。`chat.message` 获取 exact 租约并向 Host message 投影 `{ providerID, modelID, variant }`；`chat.params` 只验证已有的物理绑定，不重新准入或选择模型。

## [010] Token 借用与公平

物理 ModelTarget 绑定与 provider capacity token 分离；请求发出前由 `experimental.chat.messages.transform` 获取 token。借用只认 acquire 显式 lender，不从派生关系、拓扑或同名 session 推导信用。借贷不复制 token，转移只在 provider-step 边界发生。

显式 release/retire 召回等待 borrower step 结束。lender 的 transform 进入后续 step 时，仲裁前回收自己 credit 上的外来 InFlight/Retiring step；同一 token 的可执行 demand 仍按单调序号先到先得，owned/borrowed/ordinary 只决定使用资格，不赋予优先级，较晚 lender 不得饿死已等待 borrower。

Managed tool 入口的 exact ProviderRunIdentity 是 provider→tool 的因果边界：在任何权限检查或 tool body 前，按冻结 PhysicalUserMessageId 结束 provider step。工具可同步等待后代工作，因此不得持 token 到工具返回、用时间猜测释放，或借用仍实际执行中的 token 来虚增容量。

## [011] 准入顺序

顺序固定为：以 IdentitySeed 的 Role 解析 target → durable Accepted → exact capacity acquire → execution binding → Host projection。获取资源或进入等待队列前，必须已接受 exact key 与完整 participant；binding 只改变 target/lease，不改变身份。acquire 显式携带 Role、participant 和可选 lender；同一 physical 复用既有 target。

失败由 `execution-failure-policy` 结算已拥有的事实与资源；不 acquire-before-accept、不先改 Host 再补 binding、不让未接受意图占容量。

## [012] Exact fenced capability

成功 acquire 返回不可伪造、单次结算的 opaque fence，其身份包含 `(SessionId, PhysicalUserMessageId, Role, Participant, ModelTarget, CapacityFence)`，并绑定 owner lineage 与 epoch。重试复用 target/fence，新物理执行使用 fresh fence。

borrow/recall 只转移合法 custody。release、retain、transfer 验证完整 identity、fence 与当前 custody；错误身份、旧 epoch 或按计数/session 猜测均拒绝。重复结算不再次消费，不触碰新 execution；outcome 遵守 [014]。结算选择来自 failure policy，路由只验证并原子执行。

## [013] Bounded pending queue

已接受但 scheduler 返回 `null` 或暂不可 acquire 的 demand 进入有明确上限的 typed queue，保留 exact key、target 解析信息、Role/participant/lender 与 supersession identity。满队列返回 typed `CapacityQueueFull` 交 failure policy，不丢弃、不无限扩容、不解析错误文字重试。

release、exact supersede、session deletion、shutdown 驱动重算或移除；正确性不依赖 polling、sleep、elapsed time 或超时清退。

## [014] 只读快照与 reconciliation

Capacity owner 发布不可变 snapshot，包含 ledger、token、exact execution owner、pending waiter、Role/participant 及单调 duplicate/stale/conflict counter。始终 `0 <= active <= ledger entries`，每个 token/waiter/map owner 均追溯到同一 exact identity。

纯 reconciliation 对合法 evidence 返回 `NoOp`；map/ledger 分歧、无 owner 资源或不可能计数返回 typed `FailClosed`，不修补状态、不清 counter/config、不借时间推断。release/commit/cancel outcome 闭合为 `Applied | AlreadyApplied | StaleFence | Conflict`；重复不二次递减，旧 fence 不动新执行。

## [015] 诊断复用快照

Reliability query 的队深、活跃租约及 duplicate/stale/conflict 数逐字段投影 [014] snapshot，不维护第二份 counter、另算公式、reset/repair 或反馈控制 routing。`CapacityQueueFull` 只累积为进程内单调诊断计数，不授权重试。

## [016] Fatal 权限

Routing fatal incident 携 exact key、capacity fence 与 `Committed | Unknown` settlement evidence；未结算、stale fence、coarse session identity 不授权 fatal。只接受 composition 注入的 mandatory fatal capability，不直接调用 physical adapter；同一 incident 至多一次 report/kill，不以 fatal 修补、清零或释放 capacity。

## [017] 失败目标的单次处置

一次 provider 失败只消费 [006] 的 exact witness，选择其一：

- 保留：写入该 session 下一次 fresh admission 单次消费的 recovery retry target，优先于被取代执行的 previous；无论本次准入成功、排队或被替代均消费。
- 驱逐：其 provider 在本进程生命周期内被 poison，后续 fresh admission 回普通调度。

重复、过期或缺失 witness（包括重启后）不产生处置。retry target 不是 session previous 缓存，只由 exact 失败结算创建，并在强制 execution cleanup 时清除。

## [018] 当前角色集合

仅当前合法角色可调度。推荐模板只要求当前角色槽位；废止角色输入 fail closed，不获得租约、不进入 provider 准入。

## [019] DevOps 固定模型

道路初始化时确定并持久记录固定 DevOps 的 ModelTarget。同一道路后续 resume、续行、崩溃恢复均复用它，不通过参数或运行时策略重新分配、覆盖或篡改。

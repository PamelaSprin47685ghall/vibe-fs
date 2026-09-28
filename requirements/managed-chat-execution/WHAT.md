# managed-chat-execution — WHAT

## [001] Exact execution key

本包独占 durable managed chat execution，唯一标识为 `(SessionId, PhysicalUserMessageId)`。SessionId 只依次承载执行，不拥有 session-scoped 的当前终态、租约或替代身份。

## [002] Versioned durable facts

`Accepted` 携带 exact key、固定 participant/role、完整版本化 identity evidence（含 Persona、catalog version 与 provenance）、PromptOrigin 与 authority evidence，不含尚未存在的 ProviderRun，也无 PeerAgent 或可变 EffectiveAgent。managed-chat 恢复不重试 provider，连续失败预算由 provider recovery 独占。

`ProviderStarted` 携带完整 accepted evidence、Host 实际观测的 ProviderRunIdentity、ProviderRequestKind 与 projection choice。terminal evidence 闭合为 `PreProvider(accepted evidence)` 或 `AfterProviderStart(started evidence)`，不以 option/bool 拼接阶段。旧事实经纯、确定、逐级升级后折叠；进程状态、日志与 Host mutable projection 不充当事实。

## [003] 准入顺序

执行顺序固定为：解析 pre-provider identity → durable Accepted → 获取 exact capacity → 绑定 exact key → 投影 Host → 结算 admission → provider effect。Accepted 确认前不越过任一下游边界；任何步骤失败即停在其后继之前。ProviderRunIdentity 只取自 Host 实际观测，不预测、不伪造。

## [004] Accepted 幂等

同一 key 的等值 accepted evidence 重放不产生新事实，identity 或内容冲突拒绝。supplied state 的 key 不匹配先拒绝，不借其 terminal 绕过检查；exact terminal 重放也不被新提交的 malformed attempt 改写。复用 SessionId 的新物理消息建立独立 key，不继承前次事实。ProviderStarted 只能在 accepted evidence 完全相等后添加实际 run evidence。

## [005] ProviderStarted 先于执行

首次 provider body 之前必须已有 exact Accepted、已获取并绑定的 capacity、Host 确立的 ProviderRunIdentity 及对应 ProviderStarted 的提交确认。exact start observation 在任何 failure/idle wake 前推进 reconciler 的物理 cursor，不因此创建 authority。

ProviderStarted 标记整个 physical user-message execution 的 provider phase；同一执行内，工具结果触发的后续 Host assistant run 复用该 phase，不要求第二份 frozen admission plan，不追加第二个 ProviderStarted。等值 public message.updated 重放幂等；terminal 后首次启动事实拒绝。

## [006] Terminal 单赋值

每个 exact execution 至多一个 terminal。PreProvider 仅允许 `Cancelled | Rejected | Failed`；AfterProviderStart 绑定 exact started evidence，另可 `Completed`。相同 evidence 与 disposition 重放幂等，其他竞争拒绝并保留首个事实。

终态只来自 `execution-failure-policy` 的 typed disposition 或明确 Host success/cancel/delete evidence。自由文本只作诊断，不授权 retry、fallback、breaker、容量、消息处置或 fatal。

## [007] Pre-provider settlement

Accepted 后、ProviderStarted 前的拒绝、取消、删除、binding 或 Host projection 失败，针对 exact key 写入 typed terminal；已获取的容量在 terminal 提交确认后精确归还。不得调用 provider，也不得影响同一 session 的其他 execution。

## [008] 激活后才恢复

插件构造只作 wiring，不读取 durable execution、启动 recovery、获取容量或注册会推进状态的后台工作。durable substrate 激活成功后才折叠非终态执行，依 projection activation、capacity change、Host evidence 或 typed failure 重入普通准入/结算。timer、sleep、deadline、轮询和重启次数不参与正确性。

## [009] 本地资源不持久化

lease handle、waiter、callback、queue node、cancellation token、subscription 不写入事实、快照或恢复 token。恢复只从 durable semantic facts 重建新的本地资源；旧资源缺失不等于 terminal。

真实 OS crash/restart 后保留已提交 Accepted，旧进程的 binding、capacity ownership、token、custody、execution 与 waiter 全部消失；新进程只经正常业务路径重建，不继承进程身份。同进程 dispose/reconstruction 不算崩溃重启证明。

## [010] Cancel/Delete 排空

logical cancel 与 session delete 枚举作用域内所有非终态 exact key，逐个 typed settlement；等待每个已准入执行的 durable terminal 与 exact capacity 归还后，才宣告 lifecycle 排空。不用 session-wide blind release、宽限 timer 或 polling 判断完成。

## [011] 原子消费身份依据

Accepted 原子消费 frozen managed intent 与 `interaction-authority` 的 current evidence，保留完整版本化 ParticipantIdentityEvidence，不含 ProviderRunIdentity。provider-start 仅将其与 Host-observed run 组合。

两阶段只精确投影 owner-issued evidence，不从 agent 文本、Session cache、Host parent、model 或旧 execution 推导、补齐、改写或独立缓存 participant、Role、initial Tier、Persona、provenance/version 或物理 run。显式外部 agent 保留并与 participant 核对；continuation 和 fresh target 路由不改变 participant。

## [012] Recovery 的证据与权限

唯一纯 recovery 策略由 canonical execution state、exact public provider observation（missing/ambiguous/absent/alive/terminal）、exact physical resource observation、typed persistence commitment 与 failure-policy decision，确定 `Ignore | ReconcilePhysical | ResumePreProvider | Finalize | MarkManualIntervention`。有副作用的决策携带 exact evidence，公式本身不执行副作用；相同证据必得相同决策。Role、错误或终态文字、idle、时间、进程年龄、cursor、registry 与进程内容量状态不参与裁决。

- Accepted 且 exact provider absent 才可恢复 pre-provider 准入；已观察 provider 先 reconciliation。
- ProviderStarted 且 provider alive 只观察；exact terminal 才 finalize；exact absent 仅按 failure policy 的授权终态结算，不重试。supersession 仅采用 policy 发布的 exact cancelled terminal。
- durable terminal 仍持有物理资源时请求 exact reconciliation，否则幂等忽略。
- missing/ambiguous receipt、unknown persistence/resource 或缺少 policy 授权转 manual intervention；过期证据不改当前 execution。

managed-chat 不拥有 provider retry/fallback，不启动 provider 工作或发布延迟 requeue。跨进程恢复遵守 `crash-reconciliation`：加载阶段自行归位，不自动重放中断工具，不设显式续传命令。exact accepted-message recovery port 仅在明确返回 true 时接管准入，且不重算 failure policy；port 缺失或拒绝时，发布可观察的 manual/blocked 处置与 briefing，不假装后台已接管。

排列、重复事件与 crash-cut 证明调用生产实现，不复写公式自证。观察器逐次记录实际调用，不去重；真实重启两侧使用独立观察器，幂等由 durable owner 实现。

## [013] 诊断只读与及时撤销

Accepted/ProviderStarted without Terminal 及每个 LogicalRunId 的 physical attempt 数，只从 canonical projection 只读导出为不可变本地快照，不写事实、不终结、不授权 retry/fallback、不把诊断计数用作 recovery evidence。

pending/manual intervention 只投影 recovery owner 的 typed ownership，不复制或自行清理其状态；DTO 是报告，不是进程间恢复命令。exact execution 达到真实终态时，撤销对应 pending/manual 诊断，不让失效请求永久残留。

## [014] Incident capture 与 replay

incident envelope 使用 versioned、确定序列化，只包含 canonical execution facts/status、不可变容量快照与 reconciliation decision、因果诊断、exact public Host version/contract evidence、typed recovery observation/decision。capture 复用 owner projection，拒绝未知字段；diagnostic owner 清除 credential、path、stack、prompt/content/payload。

replay 重新折叠 canonical projection、重跑容量 reconciliation 和同一 recovery 表达；篡改、未知 schema/字段、缺证据、Host contract 不支持或 observation 不匹配均拒绝。只返回 typed owner effect request，不写事实、清计数、释放 fence、改 queue/capacity、执行 retry/fallback 或提升 operator 权限。相同 envelope 重放幂等；缺 exact accepted-message public replay 的 Host canary evidence 时交人处理，不重发、不手工补状态。

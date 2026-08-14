# delegation — 可观察合同

本文件是 `delegation` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。
证据指针 → `PROOF.md`。边界 → `HOW.md`「边界与弃权」。

## DELEG-001：委托 = 语义 charge + entitled office + 逻辑 owner + bounded 返回后果

一项委托必须同时明确四件事：交给谁的 charge（语义任务）、允许该 callee 产生的后果（office）、
这条工作的逻辑 owner（caller 或一条 named road）、以及返回给 caller 的 bounded 后果。
任何一项缺失或含糊即为 RED。委托的识别依据是**后果**（entrust by consequence），不是 persona 名、
不是「看起来能做」（`resources/provider/tool/fork/description`、`docs/what/agent.md` AGENT-009）。

含义/动机：caller 需要的是「改变 repository source」「建立 repository 事实」「对运行中的世界行动」这类
consequence，而不是一个名字或工具白名单。

边界：office 的 entitled consequence 清单本身由 `office-capability` 定义（ARCH-017）；本包只规定「按后果
选择 callee」这一委托语义。

证据：semantic anchors `entrust-by-consequence` / `choose-by-return` / `no-omnipotent-charge`。

## DELEG-002：同一 Office 的 calling 名只差 persona/depth，不差 authority

属于同一 Office 的两个 calling 名（如 fast/deep Inspector）区别只在 persona 与 reasoning depth，
不改变该 Office 的 authority（`resources/provider/tool/fork/description`）。fast 与 deep 权限一致
（AGENT-010：`permissions(fast-ROLE) = permissions(deep-ROLE)`）。

含义/动机：换深度不是换权限；caller 选后果，不选权限档。

边界：tier 与 ExecutionBinding 的机器精度（`fast-`/`deep-` 名）属于墙内；Persona 注册表 → `participant-identity`。

证据：anchor `persona-not-authority`（fork 组，由 `participant-identity` 交叉声明其 personhood 部分）。

## DELEG-003：独立 road 与 same-road continuation 硬区分

- `commission(calling?, name, charge)`：`calling` 在场 → 新独立 road；缺省 → 按 `name`（Byname）续做
  既有 road（`docs/what/orchestrator.md` ORCH-001、`docs/what/agent.md` AGENT-015、EXEC-029）。
- 同一目的地的后续阶段、纠正、重试、恢复、证据变化但目标不变——都仍是同一条 road，
  不因劳动量大或进入下一阶段而另开（`resources/provider/role/orchestrator/`「道路」）。
- 检验标准是目的地是否独立：若邻近 road 从未存在，这项工作仍能为自己诚实完成？不能 → 同一条 road。

含义/动机：防止「工作很大就另开一条路」把同一条语义工作切成多个无关联片段；也防止「续做」被误建成新委托。

证据：anchors `same-road-continuation` / `independent-destination` / `independent-road` / `not-lifecycle-stage`。

## DELEG-004：不同 contract 必须不同名（commission ≠ fork）

`commission`（Orchestrator）= 独立集成之路；`fork`（Manager）= mission 内 witness。二者硬语义不同，
故不同名（ARCH-006/007、EXEC-029）。同一工具名在任何地方命名同一个 contract（same tool name ⇒ same
semantic act / schema / argument meaning / lifecycle / return / failure semantics）。

含义/动机：同名会诱导「独立道路 = 使命内证人」的错误等价；命名是语义 act 的第一道闸。

边界：`join` 可在 Manager 与 Orchestrator 共享，当且仅当语义合同完全同一（消费当前 owner 可用 completion）。

证据：anchor `office-not-witness`（fork 组）；`docs/what/architecture.md` ARCH-006/007。

## DELEG-005：机器拓扑永不进入委托面

fork/commission/join/horizon/inspect 的成功后果与参数**不得**包含：`SessionId` / `AgentId` /
`ManagerJobId` / `job_id` / `worktree` / `reused` / `agent` / `role` / `tier` / `fallback_peer` /
`fast-`/`deep-` 自称 / `status` / `code` / `message` 通用 DTO（EXEC-002/029/030、ORCH-001、AGENT-015）。
机器精度留在 Host/Journal 墙内；穿过 horizon 的只有后果与 WorkRecord。

含义/动机：机器身份是调试面，不是世界语言；泄漏会逼模型当 union decoder 并污染「谁拥有这条工作」。

边界：准入过滤的完整法则（哪些字段可以穿过）→ `participant-horizon`；本包只声明委托面的泄漏禁令。

证据：REUSE `tests/unit/tools/fork-tool.test.mjs`（`FORK_calling_creates_machine_agent_but_returns_only_byname`、
`FORK_unknown_byname_does_not_echo_internal_identity`）、`tests/unit/execution/join-v2-wire.test.mjs`。

## DELEG-006：fork 成功仅 Byname 承接 charge；续做沿用已绑定 binding

- 新建：`calling`（Persona 名）+ `name`（Byname）+ 非空 `charge`。成功后果仅 Byname 承接 charge 的
  自然语言（`fork` 无 `agent_id`/`role`/`tier`/`worktree`）。
- 续做：省略 `calling`，按 `name` + 当前 `charge` 识别既有 person；不暴露 AgentId、不用 `reuse` flag。
- 续发必须沿用该 person 已绑定的 managed agent 与其 model：不得把 `deep-*` 换成 `fast-*`（EXEC-002）。
- Busy existing：不新 RunId、不新 listener、不新 completion；nudge 归属当前 active Run。

含义/动机：同一 Byname 是逻辑 owner 的稳定名；换 binding 会暗中改变 personhood，违反 DELEG-002。

证据：MOVE `tests/fork-child-payload.test.mjs`（fork child 首 prompt 的 charge/commissioner record 渲染）；
REUSE `tests/unit/tools/fork-tool.test.mjs`（`FORK_existing_person_is_resolved_by_byname_not_agent_id`、
`FORK_engineer_continuation_keeps_deep_coder`、`FORK_same_byname_cannot_be_reborn_with_a_new_calling`）。

## DELEG-007：SyncDelegate DAG 有环即错

允许的同步委托边：`Inquiry → Inspector`、`Coder → Inspector`、`DevOps → Inspector`、`DevOps → Coder`
（AGENT-024）。禁止反向/成环边（如 `Inspector → Coder`、`Inspector → Inquiry`、`Coder → DevOps`）。
嵌套 `DevOps → Coder → Inspector` 合法；启动/配置须静态证明图无环。

含义/动机：同步委托是串行依赖，成环 = 死锁或无限递归；图必须是 DAG。

边界：具体角色能力面（Inquiry 只有 inspect+sphinx MCP）→ `capability-enforcement`。

证据：REUSE `tests/unit/session/sync-delegate-runtime.test.mjs`（嵌套无 deadlock 场景）。

## DELEG-008：sync batch 成员与顺序由 Host tool-call 集合决定

同一 assistant `ProviderRunIdentity` 中、指向同一 `SyncDelegateRole` 的全部 sync calls 构成一个语义
batch；成员与顺序 = 该 assistant message 的 Host tool-call 列表（EXEC-026/031）。禁止用 microtask /
scheduler 到达时序猜批次边界；batch 的 charges / provider prompts 按 tool-call 顺序分别拼接后**只发送一次**。

含义/动机：批次边界是「Host 已完成的 assistant message」这一语义事实，不是调度竞态。

证据：REUSE `tests/unit/session/sync-delegate-runtime.test.mjs`（`EXEC_026_sync_delegate_provider_batch_coalesces_without_race_and_returns_once`）。

## DELEG-009：serialization key = immediate caller ReuseScope；同 key 至多一个 active batch

Ownership / serialization key = **immediate caller ReuseScope**（非 family root）。同 key 同时最多一个
active batch；不同 ProviderRun 在上一 batch completion 前到达 = 协议冲突，fail closed；不得向同一
dedicated Session 排队/叠发第二轮。嵌套 `DevOps → Coder → Inspector` 各占本层 key，无 deadlock。

含义/动机：按 family root 串行会不必要地阻塞兄弟路径；immediate caller key 让嵌套合法且互不饿死。

证据：REUSE `tests/unit/session/sync-delegate-runtime.test.mjs`（G2 overlap fail-closed / serial reuse）。

## DELEG-010：owner effective tier → deterministic delegate tier

`fast→fast`、`deep→deep`；模型不可每轮自选 target Agent。复用既有 child 时沿用其已绑定 managed agent，
不得把 `deep-*` 换成 `fast-*`（EXEC-026、PROMPT-006）。

含义/动机：`(OwnerReuseScopeId, role)` 必须对应唯一 dedicated Session，否则 prefix/context 复用崩溃。

证据：REUSE `tests/unit/kernel/sync-delegate.test.mjs`（`EXEC_026_tierForOwner_is_identity_for_fast_and_deep`、
`EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder`）与
`tests/unit/session/sync-delegate-runtime.test.mjs`（`EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names`、
`EXEC_026_sync_delegate_reuse_keeps_deep_inspector_when_owner_later_fast`）。

## DELEG-011：无 `return` 通道；ordinary completion 结束 batch

Dedicated SyncDelegate 无独立 `return(message)` 工具、无 `Returned → Completion` 双 await、无
`completion_text` magic。callee 普通 Assistant completion 即结束整个 sync batch；Host 物化该次 invocation
的 bounded WorkRecord（`includeOpening=false`）并投影给 caller（EXEC-028B / EXEC-031）。

含义/动机：「结束协议」不是工具能力；双出口逼调用方解码双通道并污染 self-model（见 WHY.md 历史）。

证据：REUSE `tests/unit/session/sync-delegate-runtime.test.mjs`、`tests/unit/tools/sync-delegate-tools.test.mjs`
（`INSPECT_happy_path_invokes_inspector_and_returns_work_record`）。

## DELEG-012：同步返回 = canonical 得 WorkRecord，siblings 只引用

batch 内 exactly one canonical caller（provider 顺序第一项）接收 bounded WorkRecord；其它 sibling caller
只收到「参见 canonical call」的短引用，不复制正文（EXEC-026/031、`resources/provider/tool/sync-delegate/merged-reference`）。
答案就是 bounded WorkRecord 本身，无额外 `answer` 字段；最后一条助手文本在 Recent work。

含义/动机：一份正文一份真相；N 份复制会让 caller 对「同一份知识」产生多份漂移副本。

证据：MOVE `tests/fork-child-payload.test.mjs`（无 answer 字段的 child payload 形状）；REUSE
`tests/unit/session/sync-delegate-runtime.test.mjs`（`EXEC_031_bounded_work_record_answers_in_recent_work_not_raw_message`）、
`tests/unit/tools/sync-delegate-tools.test.mjs`（merged-reference wire）。

## DELEG-013：Join 消费 owner 可用 completion，有界批次、稳定排序、逐项 CAS

Join 只消费当前 owner 可用 completion；批次有界（`MaxJoinBatch`）、成员稳定排序、逐项 CAS 消费；
禁止「谁先完成谁先入 wire」的非确定序（EXEC-004/018）。中断前先 drain 已可用 completion。
agent 完成项为 entry-local WorkRecord（`includeOpening=false`），禁止字段式 `work_record` DTO。

含义/动机：并发完成必须收敛成确定性 wire，否则父流程无法稳定判断「谁回来了、带回了什么」。

边界：具体预算数值（如 32）是 HOW；「有界批次 + 确定性收敛」才是 WHAT。DTO 禁令的准入法则 →
`participant-horizon`；WorkRecord 物化格式 → `work-record`。

证据：REUSE `tests/unit/execution/join-v2-mailbox.test.mjs`（`EXEC_018_max_join_batch_is_32`、
`EXEC_018_thirty_three_completions_split_across_two_drains`）、`tests/unit/execution/join-v2-wire.test.mjs`。

## DELEG-014：commission 批量 join 与 EXEC-018 同界

Orchestrator 的 commission 批量 join：FIFO 排空、上限与 EXEC-018 相同（EXEC-019）。

证据：REUSE `tests/unit/execution/join-v2-mailbox.test.mjs`（`EXEC_019_verdict_mailbox_try_join_batch_preserves_publish_fifo`）、
`tests/unit/execution/join-v2-wire.test.mjs`（`EXEC_019_orchestrator_batch_is_natural_language_only`）。

## DELEG-015：join 中断是 Interrupted，不是 ForkError

join 等待直至 completion 可用 / 本地 operator abort / external-user ingress / 适用的 DevOps deadline；
中断 = `JoinWaitOutcome.Interrupted of JoinInterruptReason`（`OperatorAbort | UserMessageArrived | DeadlineExpired`），
不是 `ForkError`（EXEC-017）。external-user ingress 只打断当前 wait：不 cancel child、不授予 Prompt
authority、不产生 `TurnAborted`；operator abort 先打断当前 attempt，随后父 `TurnAborted` cleanup 取消
父全部 running sub-session，已完成并进入 `CompletedAwaitingJoin` 的结果仍可消费。任意 race 后先 drain
可用 completion，再发 interrupt 结果。无 active attempt 的消息不 latched 给 future join。

含义/动机：中断是控制面事件不是业务失败；两类 ingress（用户消息 vs Esc）不得混同
（`changes/completed/corrective.md`）。

边界：Esc 的 authority 语义与 TurnAborted 级联 → `interaction-authority` / `managed-session-lifecycle`。

证据：REUSE `tests/unit/execution/join-v2-mailbox.test.mjs`（`EXEC_017_user_message_interrupt_does_not_cancel_mailbox`、
`EXEC_017_join_attempt_old_signal_does_not_bleed_into_next_join`）、`tests/unit/execution/join-v2-wire.test.mjs`
（`EXEC_017_interrupted_wire_is_natural_language_not_error`）。

## DELEG-016：horizon 是 pull-only snapshot

`horizon()` 是调用者需要朝向时主动看一次的 snapshot；不得 timer 轮询、后台订阅、`AwaitChangeFrom`
watcher 或自动刷新（EXEC-005）。返回当前在场名册（Byname/TerminalName）+ 每个 parent-visible child 最新
一条 durable 工作记录；内部来源是最新 `BlogFrame`。无 `status`/`id`/`kind`/`ordinal` 状态机词汇。

含义/动机：朝向是 measurement，不是隐藏观察流；禁止制造「谁在远方」的持续监视。

边界：最新 frame 的 blob 校验/缺失 fail-closed 规则 → `context-compression`；无 DTO 的准入法则 →
`participant-horizon`。

证据：REUSE `tests/unit/execution/join-v2-wire.test.mjs`（`EXEC_004_join_prefers_durable_byname_over_machine_agent_name`）。

## DELEG-017：返回结果只改变 caller 认识，不自动转移 authority

返回的 WorkRecord / advice 是 evidence：它告诉 caller 一条工作声称建立了什么，不自动决定「整个请求
接下来应变成什么」（`resources/provider/role/orchestrator/`「返回的 WorkRecord 是证据」）。NEEDHELP
advice continuation 明确「这是独立视角；继续你的原 charge；不要把 consultation 当 replacement assignment」
（AGENT-031、`changes/completed/increase-strength.md` §9）。返回不授予 caller 新权限，也不免除 caller
的既有义务。

含义/动机：委托的返回是认识更新，不是 authority 转移；否则 caller 可借委托自授权限。

证据：anchor `returned-record`（manager 组）；REUSE `tests/unit/tools/sync-delegate-tools.test.mjs`。

## DELEG-018：NEEDHELP consultation 是真实独立 child 委托

deep-ROLE 命中 `[NEEDHELP]` 时：claim 当前 assistance abort，等待 fresh `SessionIdle` → `IdleRevisit`
（transport fence 证明 parent-abort descendant sweep 完成），再创建**真实、独立的 consultation child**
（当前实现为 `deep-inquiry` Work Session，不复活 `meditator` alias）。冻结求助时 parent frontier，
物化 canonical `LifecycleWorkRecord(includeOpening=true)` 作为 `CommissionerRecord`；child assignment 以
「如何解决这个 agent 的当前困难？」开头。consultation 普通完成由 assistance 路由消费，物化
`includeOpening=false` 的 WorkRecord，作为 typed `NeedHelpAdvice` continuation 返回原 binding。
consultation 不继承 owner Persona、不得递归 NEEDHELP；owner single-flight（同 owner 至多一个 active
consultation）；每 LogicalRun 次数有限、额度不向 provider 暴露；取消/终结后迟到 advice 不得复活 owner；
sentinel 在 XTrace capture 前剥离，不写入 WorkRecord/Chronicle（AGENT-031、HOST-027）。

含义/动机：求助是真实依赖（请求方等待 advice 才继续），不是假 completion / hidden prose injection；
owner 生命周期约束保证咨询不脱管。

边界：sentinel 的 delta 识别与 assistance abort 分型 → `interaction-authority`（HOST-027）；
advice 的 prompt 渲染 → `provider-projection` + `prefix-stability`；「何时鼓励求助」→ `cognitive-environment`。

证据：REUSE `tests/unit/host/needhelp-sensor.test.mjs` 与 `tests/unit/host/assistance-host.test.mjs`
（host 面，见 PROOF.md SPLIT@cutover 注记：sentinel 识别归 `interaction-authority`，consultation 委托语义归本包）。

## DELEG-019：fork child 首 prompt 是 typed 语义载荷，不是自由文本

fork child 首 prompt 由 `ForkChildAssignment { Assignment; CommissionerRecord; RootRequirements; Payload }`
类型化渲染（`Domain/ForkChildPayload.fs`）：Assignment 是任务（instruction），其余是 child 可读但不得
误认为任务的 context（data）。`Charge`（语义 assignment）与 `ProviderPrompt`（实际发给 provider 的字节）
是两个概念：无 warm-start 时字节相同；有 keywords 时只 enrich `ProviderPrompt`。禁止解析 rendered TOML
反推 Charge（EXEC-031/032、ARCH-010/011）。

含义/动机：类型化载荷让「任务」与「背景」在源码层可分，渲染器不猜测 instruction/data 分界。

证据：MOVE `tests/fork-child-payload.test.mjs`（`FORK_CHILD_PAYLOAD_*` 全组）。

## DELEG-020：委托语义不依赖当前工具名

`fork`/`commission`/`inspect`/`establish-behavior`/`repair-behavior` 是当前 HOW 选择的动词名；
命名遵循「人是名词、工具是动词、不同 contract 不同名」（ARCH-006），但本包 WHAT 绑定的是语义合同
（DELEG-001..019），不是这些名字。改名不改变命题。

含义/动机：防止把「当前 tool 名」当 ontology（HANDOFF §25.11 反例）。

证据：本命题的落点 = 命题结构本身（HOW.md「历史与弃权」）；无独立断言。

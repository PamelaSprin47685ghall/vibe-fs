# HOW：intra-participant-parallelism 实现模型（非 normative）

## 1. Domain spine

建议 production 只持有少量不可非法组合的类型：canonical prompt-array validator；`FissionGroupId`；lane index/count；`FissionWorkBundle` keyed union；`CompletionAffinity = PreFissionBroadcast | Lane k`；active-group projection。禁止用一组 `isFissioned/isLastLane/hasHandoff` bool 模拟状态机。

## 2. V1 physical replacement

Fission tool 在 old caller tool context 中：

1. 读取 old caller physical parent。`None` = user-facing/root，立即返回明确的 root-origin consequence；不得读取/解析 prompts，更不得 reserve、读 LWR、写 durable fact、create lane 或 interrupt。
2. 只有 parent 存在时才读取并校验 prompts array；校验失败立即返回，无副作用。每个 array element 已是 lane 边界，lane 内 CR/LF 原样保留。Domain admission 随后再次读取 parent，作为 authoritative TOCTOU/fail-closed gate。
3. 从 Authority/Profile 取 current managed agent/role；从 canonical LWR port 取 old caller record。
4. 预留一个 process-local admission slot，防同 owner 两次并发 fission。
5. 对每 lane 创建 fresh Host session，创建参数里的 `parentID` 使用 **old caller 的 parentID**，而不是 old caller id。
6. lane session 继承 old caller selected managed agent、directory、provider language；首 prompt = canonical LWR envelope + exact lane input + lane index/count guidance。不要用 Host session fork。
7. 所有 lane 都完成 create + subscribe/bind + prompt admission 后，commit group；失败则 abort/delete 已建 lanes、release slot、old caller 继续。
8. commit 后给 old caller 写 Fission-owned interrupt mark，再调用 physical abort。Ordinary abort workflow 先消费此 mark：不 terminal、不 cascade children/PTY、不 provider recovery。

## 3. Logical-owner alias

lane physical session 不是新 participant。运行期应维护 `laneSessionId -> oldLogicalOwnerSessionId` alias。需要 logical owner state 的工具 runtime（尤其 shared child registry / handle set）按 alias 找 owner runtime；provider horizon 不暴露 alias、lane session id 或 group id。

Host `parentID` 与 logical owner alias 是两件不同事实：前者只保持 sibling physical topology，后者保持 same-participant semantics。

## 4. Existing external work

Admission snapshot owner 当前 outstanding child runs 与 PTYs，登记为 `PreFissionBroadcast` completion sources。source terminal 后生成一个 canonical completion fact，并为 group 的每个 lane 维护 delivery bit/key。lane 下一安全 provider boundary materialize inbox；已关闭 lane 的 undelivered broadcast 留在 group forwarding closure，不丢弃。

Admission 后新 run/PTY 在创建点记录 current lane affinity。join/drain 在 lane context 中只消费 `PreFissionBroadcast` 中尚未投递给本 lane的 completion，加上 affinity == current lane 的 completions。

## 5. Work ring / convergence

lane own LWR 只登记一次为 `index -> canonical ref/digest`。ring successor 是运输策略，closed successor 由 pure forwarding closure 继续向后找 active lane；无 active lane 时 group finalizer 持有 bundle。最终按 lane index 稳定排序物化 aggregate context；最后可继续的 lane 消费完整 handoff 后，其 ordinary terminal 作为 logical owner terminal candidate。

finalizer 向最终 lane 投递 aggregate handoff 时，terminal observer **不得等待未来 `chat.message` 才产生的 `PhysicalUserMessageId`**。OpenCode `promptAsync` 的正常成功只给 enqueue receipt；因此 transport admission 返回 `PromptKey` 后立即写 `FissionTakeoverClaimed(PromptKey, lane, aggregate)`，让当前 terminal observer 可以返回。后续 physical acceptance 由 PromptAuthority 的 `AcceptedDispatch` 把该 `PromptKey` 精确解析到 `PhysicalUserMessageId`；只有该 physical message 的 terminal 才可成为 logical-owner terminal candidate。历史 `FissionTakeoverStarted(PhysicalUserMessageId, ...)` 继续可 replay，但新写路径不再依赖它。

## 6. Durability

Fission-specific durable facts建议最小化为 admission、lane materialized/closed、completion source/delivery、takeover claim、bundle contribution 与 converged/failed。事实存统一 durable substrate；physical subscriptions、abort callbacks、locks 是可重建资源，不持久化。恢复先 fold group，再 reconcile physical sessions；无法证明 alias/membership 时 fail closed。

## 7. Tool surface 与 prefix stability

Fission 的 office entitlement 仍由稳定的 role matrix 提供；root/subsession 不复制两套 AgentConfig。物理 `chat.message` 已经是每条 request 的 typed tool-map 边界，因此在这里按稳定 session-origin 投影：`SessionParents` 无 parent 的 managed root 强制 `message.tools.fission=false`；有 parent 的 subsession 不写 origin deny，继续继承 role entitlement。一个 physical SessionId 的 parent relation 在其生命周期内稳定，所以该 projection 不会随普通 turn 抖动；它只是把本来就不具备 origin 资格的 root capability 在 provider 前消掉。Host v1.2.x 的 provider 边界按 `input.user.tools?.[tool] === false` 做稀疏删除，因此只写 `fission=false` 不会把未出现的其它 Manager/Coder 工具误当成 false；PromptInput.tools→session permission 的兼容层是另一条并存机制，不能拿它否定 user-message tool filter。

`tool.definition` 不能承担这件事：Host contract 的 definition hook 没有 session identity，只能改 description/parameters。`chat.params` 也不是 authority owner。执行侧仍在 `fission.execute` 最外层做 parent precheck，并在 Domain admission 再查一次；tool description 同步声明限制只是 affordance，不是安全边界。

## 8. 当前 vocabulary 映射

历史 Proposal 中的 `Meditator` 已不在当前 Role vocabulary；其现行 reasoning office 是 `Inquiry`。因此 V1 entitlement 使用 `Role.Inquiry`。历史 `Executor` 也不是当前 Role case；hidden execution helper不获得 fission office consequence。

## 9. JS semantic boundary

`Execution/Fission/Surface.fs` translates parser, delivery, keyed-work, admission, and lifecycle observations to JS-native strings, numbers, booleans, arrays, and plain objects. The admission runtime is an opaque Host capability; physical owner/parent/lane session identities never appear in semantic output. `OpenCode/Host/FissionHostSurface.fs` owns the Host turn-observation canary and returns a JSON-shaped observation. Tests call these owner surfaces, never emitted F# constructors or representation helpers.

## 历史与弃权

- 不采用 OpenCode `session.fork`：它会把 lane 塞进错误的 Host parent/child topology，而且把 transcript clone 机制与业务 identity 耦合。
- 不采用同一 physical SessionId 多 provider stream。
- 不采用 per-lane child registry。
- 不采用 lane raw transcript summary；LWR owner 保持唯一。
- 不把 Prompt Refresh 整体塞进本包；认知/affordance 文案仍由相应 package owner 承担。

## DEPENDS ON

`participant-identity`, `session-ontology`, `managed-session-lifecycle`, `office-capability`, `capability-enforcement`, `participant-horizon`, `work-record`, `process-execution`, `durable-events`, `crash-reconciliation`.

## 边界（DOES NOT OWN）

Role/Persona/Binding 本体（`participant-identity`）；role consequence catalog（`office-capability`）；session 通用 create/cancel/retire（`managed-session-lifecycle`）；LWR 内容格式（`work-record`）；PTY 本体（`process-execution`）；通用 EventStore/fold substrate（`durable-events`）；通用 crash reconciliation（`crash-reconciliation`）；provider representation（`provider-projection`）。

## 验证与测试落点

下表每条 assertion 归本包唯一 ownership。测试先写并冻结，再写 production；本次 Active execution 不先运行 RED。落点锚点为 test 标题（`tests/<file>.mjs::<test title>`），均真实存在。

| WHAT | 落点 | 证明内容 |
|---|---|---|
| INTRA-PARTICIPANT-PARALLELISM-001 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-001] lanes carry no provider-visible identity or handle and keep the same logical participant` | lane/group 不产生 public participant identity；owner id 独立于 physical lane ids；`Surface` 输出为 JS-native plain data，不含 lane session id |
| INTRA-PARTICIPANT-PARALLELISM-002 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-002] canonical lane array preserves each prompt including embedded newlines` + `tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-002] fission tool exposes prompts as a string array without newline splitting` | String Array schema、N≥2、empty-element refusal、embedded newline/space byte preservation、生产代码无 newline splitting |
| INTRA-PARTICIPANT-PARALLELISM-003 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input`；`tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-003] sibling creation is a distinct Host capability from managed-child creation` | fresh sessions；每 lane parent == old caller parent；prompt 含 canonical LWR + exact lane input；不用 Host fork |
| INTRA-PARTICIPANT-PARALLELISM-004 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller` | 任一 create/send fail → rollback created lanes、old caller不 abort |
| INTRA-PARTICIPANT-PARALLELISM-005 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] old caller silent-interrupts only after every lane started`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] FissionRuntime preserves silent interrupt across multiple checks and is cleared only by clearOwner/clearSilentInterrupt` | 全 lane admitted 后才 silent interrupt；silent abort 不 terminal/cascade |
| INTRA-PARTICIPANT-PARALLELISM-006 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-006] pre-fission completion broadcasts to every lane exactly once with idempotent delivery` | pre-fission completion target = every lane exactly once，重复 delivery 幂等 |
| INTRA-PARTICIPANT-PARALLELISM-007 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-007] post-fission completion has exactly one affinity target: the initiating lane` | post-fission affinity 只指 initiating lane |
| INTRA-PARTICIPANT-PARALLELISM-008 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-008] keyed work bundle is idempotent and rejects conflicting records for one lane` | keyed bundle union 幂等；same key/different ref fail closed；顺序不影响 keys |
| INTRA-PARTICIPANT-PARALLELISM-009 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] convergence requires all lane records and all completion deliveries`；`tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] ring successor wraps and forwards past already-closed lanes to the next live present`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] observeLaneTurn and OrdinaryTurnWorkflow absorb Fission-replaced owner turns without sending continuations`；`tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] Host convergence performs ring takeover before reporting the old logical owner` | complete set 才可进入 takeover；closed successor 按 ring 机械跳过；takeover transport receipt 后立即 durable claim `PromptKey`，不得在 lane terminal observer 内等待未来 physical id；最终 present 以 AcceptedDispatch 精确匹配 handoff physical turn，并以真实 ordinary final prose 回填 old logical owner completion cell；旧 owner turn 继续静默吸收 |
| INTRA-PARTICIPANT-PARALLELISM-010 | `tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors` | Fission durable fact/projection/recovery anchor 存在；禁止 session-fork guessing path |
| INTRA-PARTICIPANT-PARALLELISM-011 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release` | same owner second active admission → AlreadyFissioned |
| INTRA-PARTICIPANT-PARALLELISM-012 | `tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary` | role matrix entitlement 与 registry gate 同一 `ToolPermission.Fission` source；fast/deep 不分叉 |
| INTRA-PARTICIPANT-PARALLELISM-013 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] user-facing root caller is rejected before fission reserves or creates anything` + `WHAT[INTRA-PARTICIPANT-PARALLELISM-013] root provider request suppresses fission while a subsession inherits office entitlement`；`tests/fission-tool-origin.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] real root chat message carries a request-local fission deny` + `WHAT[INTRA-PARTICIPANT-PARALLELISM-013] forced root fission rejects origin before parsing prompts`；`tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] request visibility and tool adapter both enforce origin before use` | physical parent absent 的 user-facing/root caller fail closed；provider request 显式 `fission=false` 且其它 tool override 原样保留；subsession 不被 origin projection 误伤；强行调用先报 origin、后置 parser；Domain admission 保留第二道 parent gate |

### Focused acceptance

```text
node --test requirements/intra-participant-parallelism/tests/*.test.mjs
```

本次不以 full repository suite 作为此 GAP 的关闭条件；全仓 gate 由 verification-system 的正常 release 流程承担。

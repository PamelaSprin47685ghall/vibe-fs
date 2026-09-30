# GAP 台账

> 全仓已知 proof gap 的唯一单轨记录台账（按 [requirement-system-018] 规范和测试的印证：覆盖缺口由
> requirements/GAP.md 记录）。GAP = 某条 WHAT 命题尚无独立可执行 oracle（机器可红
> 落点），或该 oracle 由 e2e / 人工评审 / 其它包交叉承载。

状态：

- `OPEN`：无机器落点，靠人工评审或散文规范。
- `PARTIAL`：有部分可红承载（e2e 承担 / 交叉 REUSE / role-lock），但本命题无独立 unit oracle。
- `CLOSED`：已在包内 `tests/` 落地独立 oracle（记录测试路径与关闭 commit）。

## 台账

| GAP | 包 | 命题 | 缺口 | 状态 | 现状承载 | 补法计划 | Owner |
|---|---|---|---|---|---|---|---|
| GAP-005 | `requirement-system` | requirement-system-015（直接闭环） | 单次提交原子闭环与 AGENTS.md 豁免约束 | OPEN | 原 `015.test.mjs` 只锁定「普通小型修复…不要求创建 Change」字句，本批删除；未证明实际提交原子闭环 | 保留人工审阅，不能用句子删改即红冒充行为 oracle | requirement-system |
| GAP-006 | `verification-system` | verification-system-003（「禁止跨级」物理契约论证） | 唯一 Long Stroke 入口未声明不可模拟 physical contract | PARTIAL | 上游曾以 `003.test.mjs` 静态锁定、`014.test.mjs` PHYSICAL CONTRACTS 块和禁止 repeat-until-pass 记闭合；本批保留真实门禁与监督证据，静态声明不证明全部物理合同 | 按 GAP-054—057 补真实范围和反例，不沿用历史完整闭合结论 | verification-system |
| GAP-007 | `host-boundary` | host-boundary-008（HOST-010 因果读：bindableRun id ≡ ToolContext.messageID encoding） | unit 无 oracle | CLOSED | `requirements/host-boundary/tests/008.test.mjs`（恰好一个 bindableRun.id ≡ decodeContext.providerRunId；0/≥2 无合法 run id） | 已落地独立 unit oracle；共时 Host 穿线属 Long Stroke 物理契约（GAP-006 入口已声明） | host-boundary |
| GAP-008 | `host-boundary` | host-boundary-019 | 全部 Host 物理能力缺完整现场证据 | PARTIAL | 旧 MagicTodo 已被上游删除；019 现调用真实 transform 的受控端口，仅证明分支，012 保留快照定位案例 | 新 UI 投影归 obligation-ledger；补当前 Host 全能力 canary，不沿用旧 A..R 闭合结论 | host-boundary |
| GAP-009 | `prefix-stability` | HOST-013 dynamic elapsed sampling（cutover 丢失；prefix-stability-011 仅保留 historical replay half） | 已恢复 time-capability-007：首次 prompt durable bind-once `SessionStartedAt`；新 occurrence fresh elapsed；历史 `MarkerText` 不重算 | CLOSED | `requirements/time-capability/tests/007.test.mjs` + production `SessionStartedAtBound` / `PairProgrammingCalibration.composeWithElapsed`；相关静态 gates 绿 | closing commit: `dd0c9e4d8`；full build green（Fable 5.13.0，647 source files） | prefix-stability + time-capability + guidance-delivery |
| GAP-010 | `intra-participant-parallelism` | intra-participant-parallelism-001..013：同一 participant 多个 coequal presents，在 identity/authority 不分裂、work 不丢不重、parent 单次完成下确定性 reunion；Fission 仅允许 physical subsession origin | 上游已实现 subsession-only origin gate、fresh sibling replacement（`parent(lane)=parent(old caller)`）、canonical LWR+exact input 冷启动、all-or-none admission、old-present physical-only silent interrupt、pre-fission completion 单 payload 广播、post-fission durable lane affinity、keyed LWR convergence；takeover 以 durable `PromptKey` claim 解除 terminal-observer 对未来 physical id 的阻塞，并由 AcceptedDispatch 精确回填 parent；本批不把这些局部事实视为全链证明 | PARTIAL | `requirements/intra-participant-parallelism/WHAT.md` + `requirements/intra-participant-parallelism/tests/` + `Execution/Fission/Admission.fs` / `Execution/Fission/Projection.fs` / `Execution/Fission/Runtime.fs` / `Execution/Fission/OpenCode/Host.fs`；旧 focused suite 完整闭合结论已收窄 | 全程身份、恢复和真实终结证据见 GAP-158/159 | intra-participant-parallelism |
| GAP-011 | `delegation` | delegation-021（fork attachment） | Byname→canonical `LifecycleWorkRecord(includeOpening=true)` 背景 attachment 已进入正式 WHAT.md（唯一 normative 权威，不再有 HOW 层）、frozen oracle 与 production | CLOSED | `requirements/delegation/tests/021.test.mjs` | closing commit: `dd0c9e4d8`；language parity / horizon gates 绿；full build green（Fable 5.13.0，647 source files） | delegation + work-record |
| GAP-012 | `delegation` | delegation-022（delegated expected tool calls） | 五个 caller-visible delegation surface、replace/retain、real tool-call 饱和递减与 HOST-013 advisory projection 已落地；无 history scan / business mutable / enforcement | CLOSED | `requirements/delegation/tests/022.test.mjs` | closing commit: `dd0c9e4d8`；G4R CE / tool referential / capability-isomorphism gates 绿；full build green（Fable 5.13.0，647 source files） | delegation + guidance-delivery + prefix-stability |
| GAP-013 | `durable-events` | local process NDJSON truth / Git-at-remote-sync | 已 shock-cut：runtime truth = `.git/wanxiang/events/<WriterId>.ndjson`，一个 process 一个不切片文件；append 无 Git ODB/tree/ref；独立 Git hook sync 时一 writer 文件一 blob | CLOSED | `requirements/durable-events/tests/004.test.mjs` + `005.test.mjs` + `requirements/durable-convergence/tests/011.test.mjs` | production `ProcessEventLog` / `WriterStreamSync` / standalone hook 已落地；旧 segment/index/CAS 文件移出编译图并标 GARBAGE | durable-events + durable-convergence |
| GAP-014 | `durable-events` | 唯一 canonical Integrator | 已落地一个 `CanonicalIntegrator` F# CE；Structural/Journal/Strength/Casebook/JsTransaction 注册单-event oracle；business history-reader/load/project API 已移除 | CLOSED | `requirements/durable-events/tests/019.test.mjs` + feature Current tests | boot/live 共用 `integrateOne`；`EventKWayMerge` 为唯一 structural k-way primitive | durable-events |
| GAP-015 | `speculative-investigation` | DryRun = visible nonblocking shadow execution | 上游实现 distinct `StartDryRun` 与 K gate / Replica terminal / exact owner target terminal / owner cancel-delete 因果收口；真实 OpenCode child 可见、owner 不 await terminal、零 Prepared/Promoted/owner mapping 尚缺完整启动链证明 | PARTIAL | `requirements/speculative-investigation/tests/013.test.mjs` 的真实 coordinator owner/target 正反例；全链仍 TODO | `StrengthSpeculate` / `StrengthReplicaRuntime.ObserveDryRun` / `CloseDryRunAtTargetTerminal` 的完整启动与历史不变证据见 GAP-183 | speculative-investigation |
| GAP-016 | `execution-model-routing` | execution-model-routing-001..009：auto-bootstrap sole MJS scheduler + `role/running` ABI + event-driven process-shared physical-execution lease multiset + exact `(SessionId, PhysicalUserMessageId)` binding + managed request routing | production、独立 oracle、双 plugin-instance process-sharing proof 与真实 Host provider-wire canary 已落地 | CLOSED | `requirements/execution-model-routing/tests/*.test.mjs`；`requirements/verification-system/tests/e2e/support/managed-model-routing-canary.mjs`；implementation `a0886281` | 已关闭：外部 placeholder model 经 `chat.message` 被 MJS lease 覆盖，真实 provider wire 命中调度 model；旧 Host model inventory / duplicate-pair / static Strength binding 已删除 | execution-model-routing + host-boundary |
| GAP-017 | `crash-reconciliation` | crash-reconciliation-018 `/continue` dynamic restart briefing 必须进入真实 physical user material，且 exact material 全程 disclosure-only | command hook output 不再被 oracle 当作 Host 已转发事实；production 以 witness-bound one-shot handoff 把 dynamic briefing materialize 到真实 `chat.message`，exact material 只走 wire sanitization；stale handoff fail closed | CLOSED | `requirements/crash-reconciliation/tests/018.test.mjs`：旧实现 marker=0 RED；新实现 7/7，含 abandoned-command contamination regression；crash package 135/135 | `node scripts/check.mjs` green；Fable build green；authoritative verification 3243/3243，fail=0 | crash-reconciliation |
| GAP-018 | `requirement-grounding` | requirement-grounding-001..004：workspace discovery + implicit self coverage + positive `APPLIES-TO` + overlap | `GroundingCatalog` 已落地；matcher 复用 `JsGlobFs.matchesPathPattern`；self coverage / ordered include-exclude / overlap 均由 active contract 证明 | CLOSED | `requirements/requirement-grounding/tests/{001..004}.test.mjs` 4/4；requirement trace 0 finding | deterministic workspace-local catalog/resolver；无 Wanxiangshu 固定包表 | requirement-grounding |
| GAP-019 | `requirement-grounding` | requirement-grounding-005/006/011/012：material-level visible fact + durable anchored read occurrence + restart/prefix law | `VisibleMaterials` 以 material path + content digest 记录 horizon 可见性；主动 read 追加 additive observed fact，自动 occurrence 继续冻结旧 wire shape 并独占 synthetic replay | CLOSED | `requirements/requirement-grounding/tests/{005,006,011,012}.test.mjs`：主动读 WHAT 后只补未读兄弟材料；WHAT 内容变化只重读 WHAT；reanchor/restart/prefix oracle 全绿；全量 runner 中 grounding 005/006/011/012 全绿 | 逐 material 去重、兄弟文档变化不击穿未变材料、旧 anchored event 可重放 | requirement-grounding + semantic-trace + provider-projection + prefix-stability |
| GAP-020 | `requirement-grounding` | requirement-grounding-007/008：native read-equivalent projection + weak mutation observation | 删除 grounding mutation admission/`REQUIREMENT_GROUNDING_REQUIRED`；before-hook 仅 best-effort request，grounding 解析/持久化异常 fail-open，原 mutation control flow 不受影响 | CLOSED | `requirements/requirement-grounding/tests/{007,008}.test.mjs`：mutation 始终 allowed，首次仍请求 grounding；malformed `APPLIES-TO` 不会把 write 变成失败；ordinary/Cursor projection oracle 全绿 | grounding 仅补知识，不再拥有 effect authority；无拒绝、延期或自动重放 | requirement-grounding + host-boundary |
| GAP-021 | `requirement-grounding` | requirement-grounding-009/010：repository-programming read/effect observation + no-bypass | `js-*` workflow 收集模型显式 file read set 与 mutation effect set，统一送入 `fileAccessObservation`；read 先登记 visible material，再解析 coverage；observer 异常与 commit authority 隔离 | CLOSED | `requirements/requirement-grounding/tests/{009,010}.test.mjs`：跨 package js mutation 正常 commit 且发现 grounding；js `file()` 读 WHAT + covered code 后仅 WHY 待注入；相邻 repository-programming/transaction 测试全绿 | native/js read 共享 trigger/dedupe；js mutation 无 grounding bypass 也无 grounding barrier | requirement-grounding + repository-programming |
| GAP-022 | `context-compression` | context-compression-019 + guidance-delivery-011 + requirement-grounding-006/007/012：X→Y 后旧 auxiliary injection 不跨 horizon replay | `ContextReanchored` 与成功 `PrefixRebaseCommitted` 都原子退休 pair / tip / requirement-grounding 的当前-horizon visibility；probe 尚未提交时，XWire 以 typed `TentativeCold` 让同一 transform 的 Strength/pair/grounding 后置注入跳过，避免“必须先 probe 成功才能变小”的死锁。durable occurrence/history/ordinal 保留；ordinary synthetic rows 与 Cursor `NUL+BOM` suffix 都只按当前 visible set 重放 | CLOSED | `requirements/context-compression/tests/019.test.mjs`；`requirements/host-boundary/tests/016.test.mjs`；`requirements/guidance-delivery/tests/011.test.mjs` GP_007；`requirements/guidance-delivery/tests/006.test.mjs` reanchor Full restoration；`requirements/requirement-grounding/tests/{005,006,007,011,012}.test.mjs`；`requirements/prefix-stability/tests/004.test.mjs`；`requirements/durable-events/tests/020.test.mjs` | 已落地 horizon-relative visibility + durable history two-axis；Prefix rebase 与 Host reanchor 共享 auxiliary cold-boundary 语义；tentative probe 不依赖 process-local marker；same-digest/new pair post-Y 只在后续 trigger 形成 fresh occurrence/call id | context-compression + prefix-stability + guidance-delivery + requirement-grounding |
| GAP-024 | `execution-model-routing` | execution-model-routing-006/007：同一 `PhysicalUserMessageId` 的 provider retry 必须保留 execution binding | Host assistant `error` 同时被解码成 provider-step terminal 与 physical-execution terminal，导致 retry transform 在 durable `PhysicalAccepted` 后失去 active binding | CLOSED | 线上 witness：`ses_fe70b5351ffel5WYO3amyfF8zj/msg_018f4acd00014uSYmHCeMF4GjH`；RED oracle 证明旧实现 failed assistant 会产生 `PhysicalExecutionEnd`；`requirements/host-boundary/tests/001.test.mjs` + `requirements/execution-model-routing/tests/{006,007}.test.mjs` 锁住 error→step-end→same-physical retry；execution-model-routing 46/46、host-boundary 196/196；`npm run format-build-test` 全阶梯 green（3350 semantic + 275 harness + Long Stroke + package + pack dry-run） | `HostEventCodec` 收窄 ordinary physical terminal：无 error + completed + explicit non-`tool-calls` finish；不引入 reacquire、registry、timer 或 retry 特判 | execution-model-routing + host-boundary |
| GAP-025 | `execution-model-routing` | execution-model-routing-006/007：Host retry 的含混 finish 不得释放 exact execution binding | OpenCode upstream stream failure 可持久化为 `completed + finish="unknown"` 且无 `error`，旧谓词把任何 non-`tool-calls` finish 都当 physical terminal；随后同 `PhysicalUserMessageId` retry 在 transform 进入 `ModelRouting.enterProviderStep` 时得到 `no active execution binding` | CLOSED | 线上 witness：`ses_fe2b4fa0cffeNOdJHOLTEbAabZ/msg_01d4b0606001BPPDVzH3jKXIjL`；`requirements/execution-model-routing/tests/{006,007}.test.mjs` 锁住 `unknown | error`→step-end→same-physical retry，旧实现 RED；Fable build green，execution-model-routing + host-boundary 243/243 green。全仓 `format-build-test` 仍被当前 HEAD 的无关 `HostSignalBootstrap.fs:175` control-pyramid 与既存 requirement-trace debt 阻塞 | `HostEventCodec` physical terminal 收窄为 `stop | length | content-filter` allowlist；step terminal 仍接受 completed/error，不重建 lease、不依赖 timer/coarse idle | execution-model-routing + host-boundary |
| GAP-026 | `context-compression` | context-compression-017/020/028/030：所有真实用户消息与 assume 永久 raw；todowrite 只是逐次 K checkpoint，不再按工具名永久穿透 Y | 旧实现只保证 true Opening raw，并曾存在 todowrite 永久 raw 豁免；前者漏掉后续 user messages，后者会让工具历史无界增长 | CLOSED | `requirements/context-compression/tests/{017,020,028,030}.test.mjs` + `requirements/prefix-stability/tests/009.test.mjs`：多轮 user raw、assume 原文穿透、K=1/2、动态 K、committed cutoff 不回退、todowrite 回合越界后可被 LWR 替换 | all physical user messages raw + assume raw + native todowrite checkpoint；无 T1、无 todo call-id 永久豁免 | context-compression + obligation-ledger + prefix-stability |
| GAP-027 | `delegation` | delegation-024..027：reusable assignment 必须是 direct F# CE 上的 logical-route work unit | 旧实现同时存在四个危墙：physical dispatch 后仍有可失败 handoff bookkeeping；handoff frontier 绑 physical `SessionId`；sticky terminal 可跨 invocation 重放；idle fork reuse 可立即返回上一轮/全生命周期结果，active new charge 还会混入 BusyAgentNudge | CLOSED | `requirements/delegation/tests/{024,026}.test.mjs` 真实同 Byname 二次调用证明 fresh parent delta + new charge、own completion 前保持 pending、只返回本轮 child delta；`requirements/delegation/tests/024.test.mjs` inspector/coder 同义；`requirements/delegation/tests/{024,025,026}.test.mjs` 锁住 completed-handoff/fail-before-dispatch/active reject；host event causal tests；authoritative runner 3405/3405，`scripts/check.mjs` green | logical-route completed-handoff frontier + future-only terminal + Authority Root causal identity + required bounded WorkRecord projector；same-road continuation 执行 `prepare → dispatch → await own completion → bounded delta → checkpoint`，active assignment 明确拒绝，不再存在 post-dispatch normal-error bookkeeping | delegation + structured-workflow + host-boundary + work-record + managed-session-lifecycle |
| GAP-028 | `participant-horizon` + `delegation` | participant-horizon-011 + delegation-026：已 durable 建立的 child 不得在后果交付前消失；acceptance unknown 不得伪装成未放置 | `HorizonTool` 复用 `HandleProjection.listable` 导致 `Abandoned` 在 Join 消费前被过滤；fork 首 prompt 的 `AcceptanceUnknown` 又被 `FailRun` 合成 terminal failure 并向 Manager 返回“未放置” | CLOSED | `requirements/participant-horizon/tests/011.test.mjs` + `requirements/delegation/tests/026.test.mjs`：Abandoned 在 Retired 前仍可见；unknown acceptance 不再返回 charge-not-placed，保留 durable Pending recovery | `HandleProjection.horizonVisible` + PromptAuthority Accepted/Pending/Dispatchable 三分；核心实现 `2953a0978` | participant-horizon + delegation + managed-session-lifecycle |
| GAP-029 | `managed-session-lifecycle` | managed-session-lifecycle-018：Abandon 只能由不可逆 logical loss 授权，process/attempt 生命周期无权宣判 | plugin/process `DisposeAsync` 与 ordinary `TurnAborted` 旧路径都可触发 `CancelAndDrain → HandleAbandoned(ParentCancelled) → AbortSession(child)`，把可恢复 child 当成 parent-cancelled | CLOSED | `requirements/managed-session-lifecycle/tests/{017,018}.test.mjs` + `requirements/delegation/tests/026.test.mjs` process-detach oracle：shutdown 后 durable lifecycle 仍 Active 且 Host AbortSession=0；显式 logical cancel 仍 Abandoned 且 AbortSession=1 | process shutdown 改 `DetachAndDrain`；ordinary TurnAborted 移除 `abortParent`/`CancelSessionChildren` capability；明确 SessionDeleted/TerminateSession 保留 `CancelAndDrain`；核心实现 `506ab7d36` | managed-session-lifecycle + delegation + host-boundary |
| GAP-030 | `repository-programming` | repository-programming-023、repository-programming-024 | 已有 `js-*` 只能用整文件 `rewrite` 表达普通局部修改，弱模型必须手工定位、切片与拼接；失败反馈也缺少保守、可复制且有界的恢复协议 | CLOSED | `requirements/repository-programming/tests/{023,024}.test.mjs`；`scripts/checks/capability-isomorphism-gate.mjs` | 同一 Edit authority 下新增 `edit(path, changes)` + `rewrite(path, newText)` 渐进成员族；精确、唯一、非重叠后单次 staging，近似只作双语有界诊断；closing feature commit `e54e51ed5` | repository-programming |
| GAP-031 | `structured-workflow` | 旧 M6 semantic-owner/locality/slice/canonical-adjudication release authorization 路线 | 多层治理自身成为第二套产品，且细 project 数量没有自动产生 change locality；继续完成 owner ACL、slice audience、worksheet/snapshot 会扩大而非减少治理税 | CLOSED | WHAT-016 明确退役旧授权路线；`scripts/check.mjs` 已移除 `semantic-owners.mjs`、`owner-contracts.mjs`、`owner-projects.mjs` release authority；locality/cutover canonical world tests 与纯治理 libs 删除；已有 `.fsi`、ProjectReference、compiler canary 与真实 capability split 作为普通编译资产保留 | 不再关闭旧 M6 checklist，不补旧 ACL，不重建 FCS/canonical adjudication；后续结构债转 GAP-033 subsystem isolation | structured-workflow |
| GAP-032 | `managed-chat-execution` + `host-boundary` | managed-chat-execution-009：process restart 后只允许从 durable semantic facts 重建新的本地 artifact | 原 production codec proof 已证明 lease、handle、binding、waiter、callback、queue、cancellation token、subscription 不进入 durable fact，但没有跨 OS process 执行 crash/reopen，无法排除 module-global registry 被同进程 fixture 误当成 durable state | CLOSED | `requirements/durable-events/tests/002.test.mjs` 固定 durable codec 排除集；`requirements/managed-chat-execution/tests/009.test.mjs` 由进程 A 经真实 plugin 建立 exact `Accepted`、binding 及 token/custody/execution/derived owner，以同 session decoy 排除 session-only lookup，随后直接 `process.exit(86)` 且不 dispose；进程 B 复用同一 workspace/journal，经正式 Status/SessionBinding/ModelRouting Surface 证明 exact `Accepted` 留存、decoy 为空、binding 为零，且公开 capacity snapshot 的 ledger/token/custody/execution/waiter/owner/lineage 与 activeCount 全部归零 | accepted-phase physical process crash/reopen 已闭合；不声称本阶段从未建立的 waiter 被 crash 清除，不覆盖未公开 registry，也不宣称 A–I 每一 cut 的 physical reboot、accepted-message replay 与自动 recovery | managed-chat-execution + host-boundary |
| GAP-033 | `structured-workflow` | structured-workflow-011..016 subsystem → compile shard → source isolation | subsystem SCC 已拆至 0；`durable-composition` 持久化组合层承担 AgentFact 外层 union、ProjectionSet、CanonicalIntegrator 工厂、Journal codec/writer/engine、AuthoritativeVocabulary；`application-composition` 装配层承担 PluginRuntimeScope、ToolRuntimeScope、ToolRegistry、WorkspaceEventStore、HostSignalBootstrap 族、PluginTransforms、全工具注册。领域 fact/state/rejection 留本域；领域业务不产生 durable/app 反依赖 | CLOSED | 完成 commit `152bca7de`：27 subsystems / 276 shards / 2220 ProjectReferences / shard DAG / 最大 subsystem SCC=0；`source-unique`/`fsi-sibling`/aggregate 等价同时成立；structured-workflow 的 `subsystem-boundaries`/`owner-project-boundaries`/`owner-impact-compile` 继续用各卡 examples 作性质反例承接保护；durable-events/event-store-append+unified-store-gate、delegation/delegation-compile-boundary、interaction-authority、host-boundary、managed-chat-execution/bootstrap-single-owner、context-compression、speculative-investigation 等原有边界测试经 rework 后承接知识隔离证明；`scripts/checks/external-effect-contracts.json` 与 `authority-contracts.json` 的 carrier 与 owner 同步到实际位置 | 验证于 2026-09-13：Fable build green（ 1496 impact items ）、4052/4052 requirements tests all pass、`node scripts/checks/subsystems.mjs` 无 SCC 输出 | structured-workflow |
| GAP-034 | `provider-attempt-recovery` + `execution-model-routing` | provider-attempt-recovery-021 / execution-model-routing-017（首败保留目标，LWR 失败才驱逐） | 恢复入口在 `Retry.attempt` 之前无条件 `markProviderFailed`，首败即全进程剔除该 provider，未能先以原目标＋LWR 重试验证；后续实机事故补证：定罪事实按 physical message 命中，重试 episode 后续 step 的首次失败仍误定罪 | CLOSED | `requirements/provider-attempt-recovery/tests/021.test.mjs` + `requirements/execution-model-routing/tests/017.test.mjs`：首败绑定保留原物理目标、成功不换路、LWR 重试失败才 poison 轮换、durable 双事实判定（continuation 接受 + ProviderStarted 建立 run，重试 episode 后续 step 不定罪）、LWR 语境替换及两入口单结算架构断言（6/6）；EMR-017 独立 unit oracle 验证单次消费、会话隔离 fail-closed、定罪 poison 轮换与 releaseExecution 强制清理（4/4） | ModelRouting 单次消费 recovery retry 绑定 + `Workflow.fs` 统一结算 helper（run 粒度）；WHAT 与 WHY 保持同步（WHAT.md 是唯一 normative 权威，WHY.md 仅说明动机，不再有 HOW 层） | provider-attempt-recovery + execution-model-routing |
| GAP-035 | `delegation` + `execution-model-routing` | delegation-026 / execution-model-routing-004（resume 同步确认交接，异步执行工作；未确认不产生 ghost run） | resume 把本地发请求（Submitted）误当子任务已接下并丢掉发送失败；run 在接收确认前预先登记为 pending run，发送失败或丢失后 join 无限死等 | CLOSED | `requirements/delegation/tests/026.test.mjs`：未确认派发报告 uncertain、不伪造承接且 join 不挂住，二次 confirmed resume 正常接管与按 AuthorityRoot 终结；`requirements/execution-model-routing/tests/004.test.mjs`：OpenCodePort 异步等待 enqueue 结果并透传真实失败；全套 delegation (134/134)、execution-model-routing (78/78)、host-boundary (268/268) green | OpenCodePort 真实 await enqueue；PromptPhysicalAcceptance 承载 exact admission confirmation；RunLifecycle/ChildDispatch 仅在 Accepted 时安装 run 与 relink handle；PendingHostRun.AuthorityRoot 转为不可变强类型 | delegation + execution-model-routing |
| GAP-036 | `feature-ablation` | feature-ablation-001..004：节点注册表、三态语义、消融 DAG 与配置集 | 仅有分散 env（Strength/MCP）与「不启动 agent」操作习惯；无统一 manifest、无 station profile、无 execute/schema/fact gate | CLOSED | `requirements/feature-ablation/tests/001.test.mjs ~ 004.test.mjs`；`scripts/checks/ablation-manifest.mjs`；`scripts/ablation/verify-profile.mjs`；`src/Wanxiangshu/Ablation/*` + ToolRegistry/StaticTools/Strength/ManagedAgentConfig/Fission/MCP/AgentJournal 接线 | per-package behavior oracle 仍按巡检段在 owner 包分批补齐（非阻塞内核） | feature-ablation |
| GAP-037 | `crash-reconciliation` | crash-reconciliation-020 | 固定 DevOps 崩溃恢复单一逻辑执行权威与命令去重 | CLOSED | `requirements/crash-reconciliation/tests/020.test.mjs` 与 `tests/support/devops-crash-scenario.mjs`（`exit(86)` 跨进程崩溃恢复） | 已落地独立集成 oracle | crash-reconciliation |
| GAP-038 | `distribution` | distribution-010 | 打包资源与活动注册同步闭包 | PARTIAL | `requirements/distribution/tests/010.test.mjs` 的真实注册投影、`scripts/checks/js-surface-gate.mjs` 与 `scripts/verify-package.mjs` 有局部证据；旧路径扫描及角色特例已撤除 | 独立安装消费者与完整发布仍缺证，见 GAP-210 | distribution |
| GAP-039 | `execution-model-routing` | execution-model-routing-018 / execution-model-routing-019 | 新角色集合模型路由与 DevOps 模型锁定闭包 | CLOSED | `requirements/execution-model-routing/tests/018.test.mjs`、`019.test.mjs`（ModelRoutingSurface 驱动 fail-closed 与 boundDevopsTarget 不可变锁定） | 已落地独立 unit oracle | execution-model-routing |
| GAP-040 | `feature-ablation` | feature-ablation-001 / feature-ablation-003（原 010/012 归并） | 节点注册表角色映射与能力消融同步闭包 | CLOSED | `requirements/feature-ablation/tests/001.test.mjs ~ 004.test.mjs`（primary agent 门禁、Sphinx 独立开关、tool-map 同步） | 已落地独立 unit oracle | feature-ablation |
| GAP-041 | `interaction-authority` | interaction-authority-021 / interaction-authority-022 | 历史事件不可变隔离与 DevOps 模型锁定闭包 | CLOSED | `requirements/interaction-authority/tests/021.test.mjs`、`022.test.mjs`（历史身份隔离与 DevOps 模型锁定） | 已落地独立 unit oracle | interaction-authority |
| GAP-042 | `managed-session-lifecycle` | managed-session-lifecycle-023 / managed-session-lifecycle-024 | 角色身份整合与旧活跃会话退役收束闭包 | CLOSED | `requirements/managed-session-lifecycle/tests/023.test.mjs`、`024.test.mjs` | 已落地独立 unit oracle | managed-session-lifecycle |
| GAP-043 | `office-capability` | office-capability-003 / office-capability-018 等 | 资源大修证明边界、旧实现物理删除与集成验证闭包 | CLOSED | `requirements/office-capability/tests/{005,007,016,017}.test.mjs`（原 devops-relay-incumbency、devops-duplicate-reception、devops-process-teardown、fission-historical-recovery 四项集成门控用例已并入） + OneShotTool.fs/.fsi 物理删除与 Roles 分流 + `requirements/office-capability/tests/003.test.mjs`、`018.test.mjs` | 已落地独立集成与单元 oracle | office-capability |
| GAP-044 | `provider-language` | provider-language-012 | 核心角色双语 Prompt 同源性与分身表述清理闭包 | CLOSED | `requirements/provider-language/tests/012.test.mjs` + `scripts/checks/language-parity-gate.mjs`（scanRolePromptParity/scanForbiddenPromptPhrases） | 已落地独立 unit oracle 与门禁 | provider-language |
| GAP-045 | `delegation` | delegation-032 | Engineer 完成即返回 Manager、禁止跨角色/向后差遣 | CLOSED | `requirements/delegation/tests/032.test.mjs`（权限矩阵断言） | 已落地独立 unit oracle | delegation |
| GAP-046 | `knowledge-reuse` | knowledge-reuse-002 / knowledge-reuse-004 | Case 双基线与 Lifecycle 结束边界自动冻结管道 | PARTIAL | 真实 fetch、存储和显式 lifecycle 有局部证据；原 `015.test.mjs` 的“端到端物化与冻结”称谓不能证明自动归档或同次捕获 | 完整终结管道与基线缺口见 GAP-160/161 | knowledge-reuse |
| GAP-047 | `participant-horizon` | participant-horizon-015 | Manager 并行来自多名 Engineer 且视界中禁止 Manager Fission | CLOSED | `requirements/participant-horizon/tests/015.test.mjs` + StaticTools 显式 deny | 已落地独立 unit oracle | participant-horizon |
| GAP-048 | `participant-identity` | participant-identity-010 | 活跃身份解析与历史身份隔离解码 | CLOSED | `requirements/participant-identity/tests/010.test.mjs` + Identity.fs 升权修复与 Roles 分流 | 已落地独立 unit oracle | participant-identity |
| GAP-049 | `context-compression` | context-compression-016 | journal → coverable frames → frozen blob 完整物化路径 | CLOSED | `requirements/context-compression/tests/016.test.mjs` + XWireSurface.candidateFromJournal | 已落地独立集成 oracle | context-compression |
| GAP-050 | `requirement-system` | requirement-system-003（全部包同时为真/公理语义） | 公理地位与无裁决覆盖属语义解释原则，无忠实机械 oracle | OPEN | 旧 `003.test.mjs` 关键词最小断言已撤除；由语义审阅承载 | 维持公理地位，不以字句扫描冒充语义证明 | requirement-system |
| GAP-051 | `execution-failure-policy` | execution-failure-policy-014 | inventory checker 已有独立正反例，完整 fatal 入口与单次结算仍缺证 | PARTIAL | `requirements/execution-failure-policy/tests/014.test.mjs` 已绑定真实 checker；`scripts/checks/fatal-inventory-gate.mjs` 仍为正式门禁 | 按 GAP-120/121 追踪完整执行与真实结算，库存检查不替代业务行为 | execution-failure-policy |

## 纪律

1. 新增 GAP：直接在本表记录事实并加行（ID 递增）。
2. 关闭 GAP：在包 `tests/` 落地独立 oracle（可红、单跑绿）→ 本表更新路径与 commit（状态标 `CLOSED`）。
3. 本表不替代包内命题定义；命题文本以包 WHAT.md 为准（一项知识只有一个定义）。

## 2026-09-28：35 模块迁移缺口

本节只接入本批 35 个模块的旧施工发现，以及它们必须保留的跨包证据边界。来源为 `codex/requirements-first-pass-backup-20260928`（`1d7098a38`）及对应正式测试；不把旧构建的通过数字移作 upstream `1450f49d` 的验收结果。暂缓 20 包的旧施工结论不整体迁入。下列已做修复也保持 PARTIAL，直到本工作区的正式证据核对完成；明确 TODO、失败反例和产品待决均不能算通过。

| GAP | 包 / 范围 | 状态 | 现有证据与未闭合边界 |
|---|---|---|---|
| GAP-052 | requirement-system-001/002/005/007/008/010 | PARTIAL | 格式检查不能判定语义重复、隐性规则、组织权或历史编号复用；001、004—008、011、017、018 保留可识别形式的正反例，其余人工审阅。 |
| GAP-053 | feature-ablation-002 | PARTIAL | registry、执行/schema/事实/角色 gate 的行为有反例；尚未证明每个业务包关闭后全链零副作用及借用面完整。新 registry 接口不改变此证明限制。 |
| GAP-054 | verification-system-004/005/006/007/010/012/018/019 | PARTIAL | 保留真实监督与门禁反例；尚非全部门禁、业务进展来源、监测起止、冻结判据与扫描例外的完整证明。Temporal 样本也须逐项核对其实际层级。 |
| GAP-055 | verification-system-006/016 | PARTIAL | [016] 现有运行器只检查步骤边界，阶段中途修改再恢复仍可能通过。用户已选择固定隔离输入方向；完整快照、运行期隔离、各阶段同源和结论绑定尚未落地，失败 TODO 必须保留。复制耗时测量不是隔离证明。 |
| GAP-056 | verification-system-008/020 | PARTIAL | 用户已确认断言完整性按规范要求的结果和副作用判断；精确结构/文本仍完整比较。生成器设施的重放成功不等于全仓 oracle 独立、充分，继续人工审阅。 |
| GAP-057 | verification-system-021 | PARTIAL | 计数、容器失败、结果流与文件完成已有回归；其它入口对未完成原因和范围的传播仍待证，TODO 应阻断完整验收。 |
| GAP-058 | js-semantic-surface-001/006 | PARTIAL | `.mjs` 辅助文件义务与既有 `.js` 依赖、真实退出探针对内部物理入口的导入例外尚待裁决。保留可执行 TODO，不删除物理退出证据。 |
| GAP-059 | js-semantic-surface-002/003/004 | PARTIAL | 有登记、路径和受控扫描反例；Surface 的领域必要性、任意动态访问、所有源码语义仍需人工追踪，登记不证明实际执行。 |
| GAP-060 | js-semantic-surface-005/006 | PARTIAL | 表示验证器、ESM 链接与加载失败已覆盖局部边界；Promise/函数的返回行为、任意计算导入及伪装载体不能仅靠结构预证。 |
| GAP-061 | structured-workflow-001/002/003/005/006/007/008/017/018 | PARTIAL | 原生控制流、知识边界及关键业务准入/结算不能由词形或自建计数模拟证明。001/007/008/017/018 的相应 TODO 保留；多轴职责边界待审。 |
| GAP-062 | structured-workflow-004/010—016/019 | PARTIAL | 保留真实取消、短路与编译/产物反例；闭包计划、内部工具和标签不证明全部调用者隔离。真实 Fable 与受控规划测试分开计证。 |
| GAP-065 | session-ontology-002/003/007/009/014 | PARTIAL | 四格分类与 runtime replica、恰一个 Companion 与延迟初始化、平坦拓扑的适用对象仍需消歧；真实分类正例不能代替完整生命周期。 |
| GAP-066 | session-ontology-004/006/008/011/012/015 | PARTIAL | 保留真实 Work 关联冲突和事务身份；叶子禁止附挂、物理父节点及容器不改变身份缺少全链证据，旧常量接缝不作为证明。 |
| GAP-067 | participant-identity-001/007/008/010 | PARTIAL | Role→Persona 与 owner Persona 继承、内部 participant/公开 Role 的适用域及 Predictor→Engineer 映射待统一；不能凭名字推定权限。 |
| GAP-068 | participant-identity-002/003/004/005/008/010 | PARTIAL | 保留 durable 身份与历史角色隔离回归；原子写失败、实际执行切换及全部共享解码调用者仍缺证。历史兼容不允许活跃准入复活旧身份。 |
| GAP-079 | participant-horizon-001/002/004/006—008/011/014/015 | PARTIAL | 有真实名册、fork 拒绝、热启动与结果渲染；完整 provider 可见性、最新 Blob 读取和多 child 生命周期待证。已知信息省略与最新记录交付的范围待审。 |
| GAP-080 | participant-horizon-011 | PARTIAL | 旧真实取消场景名册仍列未返回后果，但后续 Join 未结束；保留最终领取/Retired 的 TODO。不得扩大超时或把取消前后名册正确当作闭环。 |
| GAP-081 | participant-horizon-013；provider-projection-008 | PARTIAL | 热启动多行正文经 SyntheticToml 后多出 LF；无尾 LF/有尾 LF 均有实际反例。公共表示所有者需统一值保真与布局，不在调用方 trim 掩盖。 |
| GAP-082 | provider-projection-001/003—005/010/011/013/014 | PARTIAL | 保留编码、摘要、真实输入不变和组合用例；在线/重放全链、表示不取得权威及统一 I/O 所有权尚未完整证明。 |
| GAP-083 | repository-investigation-001—006/009 | OPEN | RoleLaw 词形不能证明 Agent 取证、推理、只读调查或停止时机；需要实际任务和可复核轨迹。局部热启动数据/指令隔离不代替这些行为。 |
| GAP-084 | repository-investigation-007/008 | PARTIAL | 完整关键词与无跨调用缓存有局部证据；热启动任务字节被添加前缀的反例保留。原始任务保真及容量规则范围仍待统一。 |
| GAP-085 | requirement-grounding-002/006/007/011/012 | PARTIAL | 自身 tests 覆盖可漏拒；read v1 后重读磁盘 v2 会把未见版本登记为已见。真实输出版本须贯穿原生/程序调用链；插件顺序与独立进程恢复仍缺证。文件/外链覆盖修复需在新构建验证。 |
| GAP-086 | requirement-grounding-007；provider-projection-013/014 | PARTIAL | CRLF、空行、尾空格经统一表示被改写；原始事实保真与展示编码需分别定义。严格原字节合同及失败 TODO 保留，不以 normalize 后相等代替。 |
| GAP-087 | structured-workflow-012 | PARTIAL | 旧 focused build 曾复用其它闭包的 Fable cracked-project cache，声称成功却未生成新模块；同输出先 Core 再 Alpha 的正式回归已迁入。新上游缓存方案须用相同输入验证，不沿用旧绿结论。 |
| GAP-090 | time-capability-007/008 | PARTIAL | 局部 bind-once/render 不证明首次 prompt durable 采样或重启 marker；工程闭包/静态 gate 也非负向编译及全部消费者能力注入证明。 |
| GAP-091 | process-execution-001—013 | PARTIAL | 真实控制/单子进程不覆盖全部后代树收束；纯词汇与物理实现同编译单元、query-shell 旧角色/资源仍有反例和待决。PTY 局部顺序不代替 Agent 交付全链。 |
| GAP-092 | process-execution-015 | PARTIAL | 原实现只限制日志正文，完整 wire 连说明可能超过字节预算；失败 TODO 比较完整结果。需明确小预算拒绝/外部承载及正文与总预算的合同。 |
| GAP-093 | process-execution-014/015 | PARTIAL | 已迁入超时不能宣称已终止、截断说明应在数据平面的修复与反例；待新构建验证，不据此关闭物理终结或完整预算缺口。 |
| GAP-094 | causal-wait-001/002/007/009 | PARTIAL | registry、诊断非干扰及独立进程已有局部证据；Last progress 因果关联、同 owner 多等待分支、ProducerRunningWithoutWait 与真实注入仍缺证。 |
| GAP-095 | causal-wait-008 | PARTIAL | 合同说不落持久介质，Bridge 却写可遗留 JSON；非权威不等于不落盘。需裁决可丢弃诊断快照例外及进程/新鲜度约束。 |
| GAP-096 | causal-wait | PARTIAL | 异常等待的错误分类/未取消 deadline，以及逆序链误提取循环已有先红修复；新基线须复核同一正式回归，不把诊断算法用于业务裁决。 |
| GAP-097 | durable-events-019—025 | PARTIAL | 磁盘重开、Current 与损坏历史已有证据；完整 boot 激活、物理 fatal 和跨进程 cut 仍待证。IdentityCollision/StorageInvalid/普通 Rejected 都不自动等于 semantic cut 已结算。 |
| GAP-098 | durable-events-022—025 | PARTIAL | 持久化 typed 结果与组合层 fatal owner 分工已保留；实际直接物理 fuse 和编译预算的政策归属仍需核对，100/185 不由测试便利改变。 |
| GAP-099 | durable-events-022/023 | PARTIAL | 旧观察测试遗留 timer、局部工程缺依赖与顺序错误曾修复；保留局部编译和生命周期回归。本批依新 shard 迁移，旧全量成功不能关闭新局部依赖缺口。 |
| GAP-100 | effect-accounting | PARTIAL | 有真实 durable 意图、未知回执、文件副作用与 Change Program 正反例；真实提交后重放/冲突、跨进程 effect 不重发、工作区提交故障仍缺证。局部投影拒绝不能直接判上层幂等失败。 |
| GAP-101 | semantic-trace-005/007—012 | PARTIAL | 已撤下自排序 lane/自增游标模型，保留 capture、exact range 与磁盘重开；真实 Fission/Resume、终结前沿、多 invocation 消费及封装编译仍缺证。 |
| GAP-102 | semantic-trace-010；SyncDelegate 首次 Opening | PARTIAL | 旧捕获只看“已有”而接受冲突；真实回归与纯裁决已迁入。SyncDelegate 仅初次 assignment 捕获 Opening，续接保留原文；待新基线回归确认。 |
| GAP-103 | dispatch 物理接受生命周期 | PARTIAL | 旧成功路径未取消 Promise.race 的 timeout，断言后进程滞留；清理与回归属本批必要依赖。既有 ambient timer 不因修复就满足完整时间能力合同。 |
| GAP-105 | context-compression-004、effect-accounting-004 | PARTIAL | 第二次 receipt 被纯 fold 拒绝不等于实际 commit 不幂等；实际入口可返回 KnownCommitted。完整 payload 冲突仍待证。压缩正文“非纯 XML”与当前拒绝任意工具样式标签、允许其它 XML 的实现边界仍待决定。 |
| GAP-106 | prefix-stability | PARTIAL | 保留真实 candidate 历史拒绝/tail 正例、stable identity 与局部渲染；重建 provider wire、旧字符串或成员缺失不证明真实 seal/重启/冷边界全链。 |
| GAP-107 | prefix-stability-002/008/013 | PARTIAL | 身份变化不自动产生第四种合法 epoch 来源；low-trust 要在完整请求呈现中成立。保留 NUL+BOM、首轮例外和 occurrence 必要一致性，不用关键词制造隔离证明。 |
| GAP-108 | prefix-stability-014 | PARTIAL | 将实际 guidance 注入输出送到 capture，重开后仍含后缀的反例保留；尚未贯通 Host 原始输入回流，不能扩大为所有正常请求污染，也不能按文本关键字删除业务正文。 |
| GAP-109 | work-record-005/008/009/013/015/017 | PARTIAL | 真 Opening 与原始完整区间仍缺完整生命周期证据；context-compression-017/028/029 现以成功 native todowrite checkpoint、逐次 retainCheckpoints、coverage 与完整 semantic turn 裁剪，同时永久保留所有真实用户消息。旧 BlindPlan/T1/Assume 阶段都不再是 cutoff 来源；Opening/P0/跨界 frame 的材料与截断权仍需联审。 |
| GAP-110 | work-record-002/008 | PARTIAL | 已迁入真实 payload 损坏拒绝与原要求编号保真回归；缺失 frame 不应静默省略，Opening 不应额外编号。待新构建验证；完整原始 Opening 来源另见109。 |
| GAP-111 | work-record-004/007 | PARTIAL | 相交 frame 可把旧 invocation 摘要带入新 bounded record，失败 TODO 保留。不能简单丢 frame 后继续沿用 coverage；新 context-compression-029 也要求可验证完整子集，否则不前移，需统一重物化或拒绝边界。 |
| GAP-112 | behavior-diagnosis | PARTIAL | 保留真实 loader、codec、journal/coordinator；规则同义、BIRTH/revision、fresh life、原子多投影、fatal结算仍缺独立全链证据。固定数量/词形与自建分类器已撤下。 |
| GAP-113 | behavior-diagnosis-010/017/019 | PARTIAL | 每个 cycle 必选 tip 不等于已成立违约；缺 provider identity/协议预算耗尽的 attempt 终态与进程 fatal 范围待统一。保持原合同，不扩大真实 kill。 |
| GAP-114 | behavior-diagnosis 测试接缝 | PARTIAL | 中文完整 Rulebook、额外字段进入真实 decoder 的修正需新基线验证；生产 locale 装配本已正确，不声称修复了不存在的生产中文故障。 |
| GAP-115 | guidance-delivery-001/002/005 | PARTIAL | TipName 覆盖集合不具独立 occurrence frontier，新 run 仍 IdentityOnly 的真实失败 TODO 保留。当前合同首个 occurrence Full 与已知机制提醒范围需裁决。 |
| GAP-116 | guidance-delivery-002/004/007—012 | PARTIAL | 已有双语正文、磁盘重开与冻结字节；Full 事实与实际交付原子性、动态 owner 单次读取、权限不变和事务故障仍缺证，disposed handle 不冒充 append 失败。 |
| GAP-117 | guidance-delivery-006 | PARTIAL | 原无 association 时默认作为 guidance owner 的反例与拒绝修复已迁入；需新基线验证，不以单入口拒绝代替所有注入来源证明。 |
| GAP-118 | attention-regulation | PARTIAL | 真插件重开、session 隔离与冻结消费有证据；同 SessionId 的 Life 清退未贯通。同 occurrence 不同正文的拒绝/原值重放政策待审，actual enough 模型行为及完整账本不变仍待证。 |
| GAP-119 | verification-system-021 | PARTIAL | 文件缺失/加载错误曾被汇总器忽略；已迁入容器失败与非零传播回归，待本基线正式验证。不能用其它成功叶子掩盖文件未完成。 |
| GAP-120 | execution-failure-policy-003—008/012/013 | PARTIAL | 有真实 policy 与 admission 屏障，尚缺完整 ledger发射去重、unknown跨重启、exact terminal、sealed授权及真实settlement→fuse生命周期。独立局部trace不能拼成一次执行。 |
| GAP-121 | execution-failure-policy-010/014 | PARTIAL | fatal gate 的一跳 alias、文件/operation/测试路径存在不证明用例执行或覆盖真实分支。F05/F06/F08/F33/F34/C01/C02 在共同祖先与新基线都已无对应实际 fatal subject，已移出 active 库存并保留于本包 fatal-inventory-retired.md；现存 strength-semantic-cut 仍由 F17 索引。普通 Prepared 成功/重开不证明真实 cut 的 owner 传播、结算与终止，见183。F26只修正operation大小写，不是新政策。 |
| GAP-128 | execution-model-routing | PARTIAL | 保留真实lease/绑定/调度与字段反例；真实provider wire、完整跨进程恢复、固定角色道路生命周期及所有消费者仍缺证，不能将配置或纯投影当作全链证明。 |
| GAP-129 | execution-model-routing-019 | PARTIAL | DevOps 现行实现可随策略覆盖旧绑定，与合同永不更换冲突；真实失败TODO保留，需要明确固定作用域与恢复来源。 |
| GAP-130 | execution-model-routing-004/005 | PARTIAL | optional reservation 可在Host承接前占容量；不能因optional名称默认豁免零前置占用。需删除提前占用或正式裁决受限reservation例外。 |
| GAP-131 | execution-model-routing-011 | PARTIAL | 合同target解析先于durable accept，实际接受后进入模型调度；null scheduler排队与前置选择如何共存待决，不能为null虚构已解析target。 |
| GAP-133 | managed-session-lifecycle | PARTIAL | fold、attachment、journal、受控终止顺序有局部用例；005已通过同一真实owner证明scope/role键隔离、同键singleflight及独立移除重绑，不开放旧角色活跃准入。完整替换、取消、删除/fatal单次结算、真实DevOps恢复和OS PTY排空仍待证。025受控PTY端口不等于TERM→exit→KILL。 |
| GAP-136 | dispatch-protocol | PARTIAL | 保留真实claim-before-Host、receipt与journal重开；完整生产者、模糊接受、OS crash、handoff、历史激活和invariant fatal仍缺证。端口返回值不等于SDK/HTTP已接受。 |
| GAP-137 | dispatch-protocol-007/009 | PARTIAL | 确定Retryable/Fatal未发送可Abandon与晚到Fatal保Pending并熔断的文字边界冲突；需区分外部拒绝和内部typed invariant，当前不暗改任一合同。 |
| GAP-138 | dispatch-protocol；chat admission | PARTIAL | 非法PromptKey/Agent被当Missing而获得权限的真实反例及封闭错误类型修复已迁入；待新基线验证合法Missing和历史读取兼容。 |
| GAP-139 | provider-attempt-recovery | PARTIAL | 实际ledger/fold、retry admission、LWR与stop fence已有用例；Host组合、所有exact授权、完整失败/成功来源及boot sweep仍缺证。直接retain/condemn不能证明生产会正确选择。 |
| GAP-140 | provider-attempt-recovery-007 | PARTIAL | 新FailureRecorded在RetryExhausted后被吸收、历史后继可超过当前默认上限的失败TODO保留；需确定历史预算依据，新记录严拒与旧重放幂等分开处理。 |
| GAP-141 | provider-attempt-recovery-023 | PARTIAL | 无恢复capability实际只发布manual提示而仍Accepted；与本包终态要求及上游managed-chat-execution允许manual/blocked的范围冲突，保留失败TODO，不能测试手补状态。 |
| GAP-142 | provider-attempt-recovery 测试入口编译 | PARTIAL | 旧局部工程遗漏Retry/Workflow直接依赖已定位；本批须按最新shard重验证相同闭包，不能只靠全量构建掩盖。 |
| GAP-143 | host-provider-failure-ownership | PARTIAL | 真实配置hook和安装版canary保留；纯呈现分类器尚无生产消费证据，不能证明实际UI、持久终态、停止admission与exact一次处理。 |
| GAP-144 | host-provider-failure-ownership-002 | PARTIAL | 旧固定OpenCode/plugin1.18.29在配置0重试时，一次assistant run仍发两次provider请求；真实canary失败TODO保留。需区分SDK与Host外层重试，选择兼容Host或裁决恢复所有权，不修改个人配置绕过。 |
| GAP-145 | degeneration-guard | PARTIAL | 保留真实算法、仓库派生和受控sensor；assistant-only、run切换、在途清理、物理重启、全部豁免、continuation权限与唯一恢复仍缺全链证据。 |
| GAP-146 | degeneration-guard-007 | PARTIAL | interrupt拒绝后同run可再次中断；interrupt仍pending时可启动continuation。真实失败TODO保留，须沿actual任务先后和清理修复，不放宽至多一次合同。 |
| GAP-147 | degeneration-guard-003/004/005 | PARTIAL | runtime逐delta tokenize与仓库连续流包络可能因分块产生不同度量；需明确传输分块是否影响语义及有界缓冲，有限样本分数不同不等于已证明误杀。 |
| GAP-153 | delegation | PARTIAL | 实际fork/resume、批次、接收、队列和交接用例保留；Sphinx标准Engineer的完整权限链、全局注册、绑定恢复、重复terminal及fatal settlement→mandatory fuse仍待证。55转入42的四个Mailbox用例仅证明领取顺序和中断；旧只读Sphinx政策已退役。 |
| GAP-155 | concern-routing | PARTIAL | 真实插件投递、冻结和重放与纯投影已有证据；所有角色、真实workspace隔离、持久竞争、权限全链和进程crash仍缺证。 |
| GAP-156 | concern-routing-003 | PARTIAL | 同occurrence不同sender/address/message原被当成功重放；已迁入完整材料一致性裁决、双语拒绝资源和真实入口反例，待新构建验证。 |
| GAP-157 | concern-routing-006 | PARTIAL | actual owner life结束后仍能publish的失败TODO保留；手工retire纯测试不证明正式终结驱动durable MailboxRetired，需接通完成/放弃/replacement及恢复。 |
| GAP-158 | intra-participant-parallelism | PARTIAL | 真admission、parser、bundle/ring与权限拒绝保留；全程identity/责任、unknown/rollback、真实交付、持久恢复、唯一原cell完成及N−1 takeover仍缺证。常量startedLane已删除。 |
| GAP-159 | intra-participant-parallelism-017；speculative-investigation-004 | PARTIAL | Predictor配置映射Engineer并暴露Fission；真实authority准入也接受，但actual根Fission被origin gate拒绝。保留分别取证的失败TODO，不宣称已发生完整越权。 |
| GAP-160 | knowledge-reuse | PARTIAL | 真EventStore/fetch/Bookkeeper事务与基线有证据；终结自动归档、全部工具访问、并发副本、自动LRU、disabled零影响、fatal与完整请求预算仍缺全链。删除假维护接口，不补空实现凑绿。 |
| GAP-161 | knowledge-reuse-005/015/016 | PARTIAL | 持久baseline的diff用旧hash代替payload原文；实际失败TODO保留。生产分别计算diff与冻结目标，须让同次捕获和不可变旧材料同时支撑维护及基线。 |
| GAP-180 | institutional-learning | PARTIAL | 真插件收据、冻结消费和重开有证据；Enhancer次数、完整BIRTH、学习闭合前不消费、各故障零部分效果仍缺证。 |
| GAP-181 | institutional-learning-002—006 | OPEN | 当前evaluator仅按规则名匹配ABSORB，否则DISCARD；通用机制提炼、BIRTH、准入和revision重评尚未实现，不能以保守舍弃当作完整学习能力。 |
| GAP-182 | institutional-learning-008 | PARTIAL | 合同要求LearningDispositionCommitted与必要DeferredWorkResurfaced同批；实际单事实携带消费ID并原子投影。需裁决是否允许完整单事实承载，不为实现方便删义务。 |
| GAP-183 | speculative-investigation | PARTIAL | Policy/Frame/coordinator/EventStore真实局部证据保留；Host K+1外发、Off/K0等价、模糊提交阻断、XTrace闭环与真实semantic cut→结算→fatal仍缺证。旧 C01 入口已无实际 fatal subject，转入31的退役历史；真实 composition 的 F17 仍保留，不用006成功写入/重开充数。新上游013测试声称DryRun删除，却与现WHAT/runtime/九参数binding冲突；本批按真实接口保留DryRun反例并记录，未导入另一协议的假证明。 |
| GAP-184 | speculative-investigation-002 | PARTIAL | 旧白名单coder/inspector/devops/inquiry与现活跃身份冲突，实际策略允许Engineer；失败TODO保留，活跃Engineer/DevOps及历史解码范围须统一后改合同。 |
| GAP-210 | distribution | PARTIAL | 仓库manifest/loader、归档校验器及发布调度有局部证据；clean/incremental同字节、真实pack、完整release和隔离消费者仍须分别执行，安装场景TODO保留。 |
| GAP-211 | distribution-003/007 | PARTIAL | 归档“仅dist/resources”与必需package.json及npm根README/LICENSE存在字面冲突；需明确payload与允许根元数据边界，不由checker白名单暗定合同。 |
| GAP-212 | change-integration | PARTIAL | 保留真实Program/Git/CAS/Gate；移除猜Job、补claim、自建跨Road失效的Surface路径。真实horizon、多Road、mutation重验、跨进程发布恢复仍TODO；四个有效Mailbox用例迁回42。 |
| GAP-213 | change-integration-004 | PARTIAL | 真实Program曾在gate内外两次TerminateRoadResources；已迁入只留release后settleLanded的窄修复和反例，待新构建验证，未重排其它durable写入。 |
| GAP-214 | change-integration-002 | PARTIAL | early IsDirty将status错误映为false，实际adapter失败TODO保留；完整fork后果待证。独立ff发布检查已拒绝错误且零merge，不能夸大为发布已越权。 |
| GAP-215 | change-integration-001/003/004 | PARTIAL | ref-only gate合同与实际CaptureSnapshot、durable claim/Published门内写入冲突；需选择最小有界可恢复事务或重设计门外提交协议，暂保留严格合同与TODO。 |

### 上游已关闭记录的本批校正

原台账的 CLOSED 曾是上游历史结论，不能覆盖已撤除的证明。本批已在原表校正 GAP-005、006、010、015、038、046 的当前状态，理由如下：

- GAP-005：OPEN。[015] 单次提交原子闭环仍需人工审阅，旧关键词/句子锁定用例已删除，`015.test.mjs` 不再存在。
- GAP-006：PARTIAL。Long Stroke 与跨级边界保留；旧静态锁定不证明全部物理合同，正式范围见 GAP-054—057。
- GAP-010：PARTIAL。保留上游已实现的 Fission 路径与局部证据；全程身份、恢复、真实终结的缺证见 GAP-158/159，不能继续称001—017完整闭合。
- GAP-015：PARTIAL。DryRun exact owner/target关闭有真实coordinator证明；真实启动不阻塞、零Prepared/Promoted及Owner历史不变仍TODO，见GAP-183。
- GAP-038：PARTIAL。仓库资源及role/tool注册投影已证明局部事实；独立安装与完整发布不由路径扫描证明，见GAP-210。
- GAP-046：PARTIAL。实际fetch/存储/显式lifecycle不等于终结自动归档和单次捕获管道已闭合，见GAP-160/161；旧015“端到端”称谓不再作为证明。

GAP-050 仍 OPEN：公理地位按语义人工审阅，旧关键词最小断言已撤除。GAP-051 仍 PARTIAL，但“缺014.test.mjs”已过时：当前014已运行实际inventory checker的正反输入；剩余是完整fatal入口、真实执行和单次结算证明，见GAP-120/121。以上校正只针对本批模块；暂缓包的原上游记录不在此重判。

## 2026-09-28：其余20模块迁移缺口

本节接续旧施工稿 `1d7098a`，按上游 `1450f49d` 重新核对；不是把旧运行结果当成本次验收。对应文件与逐项迁移见[总记录](../proposals/20模块上游适配记录-2026-09-28.md)。旧缺口编号保留；`RETIRED` 表示其合同已被正式取代，不表示原实现通过。

| GAP | 范围 | 状态 | 当前边界与后续工作 |
|---|---|---|---|
| GAP-063 | host-boundary-019/021/026/027/029 | PARTIAL | 真实transform受控端口、截断和子进程退出用例保留。026/027已去除具体模块清单，Host诊断独立编译修复缺失依赖后通过；全能力Host、正常路径退出及架构语义仍待证。 |
| GAP-064 | host-boundary-013 | OPEN | 旧基线曾断言完成但进程不退出；本次受构建阻断尚未复验，不增加超时或强制退出。 |
| GAP-069 | managed-session-lifecycle-004/009/014 | OPEN | 旧fixture文件完成问题待本次产物复验；不能用旧断言pass关闭资源生命周期问题。 |
| GAP-070 | provider-language-002/003/010/012 | PARTIAL | 新来源优先级已按上游对齐；跨进程绑定、锚点表示及完整双语语义仍缺证。 |
| GAP-071 | provider-language-005/006/008/009/013 | PARTIAL | 实际提示投影、资源反例保留；同一真实请求的system/tool/consequence语言交付和Class A所有权仍待证。 |
| GAP-072 | office-capability-003/005/011 | PARTIAL | 上游已区分稳定权能与当前准入，旧歧义不再待决；跨投影语义同源及完整执行证明仍缺。 |
| GAP-073 | office-capability | PARTIAL | 权限函数和有限样例判别器不证明Agent实际职责履行。标准Engineer的Sphinx新权限、Manager只读取证窗口、接力与在途PTY需真实调用链。 |
| GAP-074 | capability-enforcement-001/013/014/017/018 | PARTIAL | 一次性permit与消费→释放→再消费仍有合同分岔；manifest和全链authority证明未闭合。 |
| GAP-075 | capability-enforcement | PARTIAL | 配置、门禁、沙箱读写有局部证据；025拒绝后零物理读、评审时仍在途的调用、026零durable append及跨进程能力隔离仍待证。 |
| GAP-076 | cognitive-environment | PARTIAL | 资源组装不证明认知纪律、职责和完整双语语义；关键词伪证明已撤，有限材料审阅及真实行为仍须补齐。 |
| GAP-077 | cognitive-environment-015 | OPEN | Blogger临时提示仍内联，白名单、重复注入、历史不变和真实Host路径缺证；不以源码词形计入通过。 |
| GAP-078 | action-affordance | PARTIAL | assume已按单jq、完整todos与物理session画板迁移；描述五问、命名政策、query-shell归属及全JSON保真仍需独立证明或裁决。 |
| GAP-088 | repository-programming | PARTIAL | 保留真实事务、快照、沙箱及预算测试；实际注册入口、异步/内存界限、OS执行与清理仍缺完整证据。 |
| GAP-089 | repository-programming-026 | PARTIAL | 首批已迁入UTF-8结果预算修复；本次旧测试适配尚需新产物执行，不沿用旧构建的绿色。 |
| GAP-104 | context-compression | PARTIAL | 保留真实mailbox、flight、生产解码及XWire局部窗口证明；撤下布尔透传、手算floor、旧续传协议。Opening恢复、持续追平、当前载体、紧急Probe前置失败及端到端退休仍待证。 |
| GAP-122 | interaction-authority | PARTIAL | 身份分类与准入有局部证明；真实外部消息、exact terminal解除抑制及越权零副作用需完整链路。缺身份允许durable查询，畸形身份不得借此恢复授权。 |
| GAP-123 | interaction-authority、managed-session-lifecycle | OPEN | 五类durable authority closure尚缺完整生产执行与归还证据；不得用描述字段代替已发生的关闭。 |
| GAP-124 | interaction-authority历史身份 | PARTIAL | 历史Inspector材料与活跃准入必须分别验证；不靠放宽当前身份恢复旧测试。 |
| GAP-125 | managed-chat-execution continuation | OPEN | 发送前对目标active run的真实核对仍缺完整证据；持久事实存在不等于当前可发送。 |
| GAP-126 | managed-chat-execution | PARTIAL | 保留真实journal/准入；Host请求链、物理承接和失败后果仍缺。显式/continue已退役，恢复归加载阶段。 |
| GAP-127 | managed-chat-execution恢复 | PARTIAL | 新接缝驱动实际admission序列，撤去只传restart标签的证明；受控调用仍不是OS进程死亡与重开。 |
| GAP-132 | managed-session-lifecycle-006/007/015 | PARTIAL | handle重放可能复活Completed/Retired；一次工作墓碑与固定道路执行者延续的身份边界仍待裁决，保留反例。 |
| GAP-134 | managed-session-lifecycle测试接缝 | PARTIAL | 首批关联源码已迁入真实端口观察；实际生命周期用例待新产物执行，自写时间整数不算Temporal。 |
| GAP-135 | managed-session-lifecycle终止 | PARTIAL | Host abort明确拒绝的传播需本次产物复验；不能把端口调用完成当成退出成功。 |
| GAP-148 | crash-reconciliation测试资源 | PARTIAL | 旧接缝主动结束自身请求的修正保留；本基线仍需验证文件完成，不扩大业务超时。 |
| GAP-149 | crash-reconciliation | PARTIAL | 新codec→fold→resolver用例不构成进程重启。完整load顺序、未知effect、physical receipt、PTY不重放及所有崩溃切点仍待证。 |
| GAP-150 | crash-reconciliation-019 | OPEN | 独立effect proof registry的权威与维护关系仍需裁决；保留四阶段、歧义与物理证明要求，不以新增平行清单强行闭合。 |
| GAP-151 | durable-convergence | PARTIAL | 真实Git、双remote配置保留与幂等用例存在；跨机器Current、受控CAS竞争、崩溃原子替换和增量成本仍待证。 |
| GAP-152 | durable-convergence-011 | PARTIAL | 过期parent与从未存在的parent在当前窗口查询中不可区分；需决定开放边界或提供过期证据，不能由测试暗定。 |
| GAP-154 | delegation-007/024 | PARTIAL | 活跃旧角色在创建child前拒绝的回归保留；本基线尚未重新编译执行，不将历史通过误报为当前通过。 |
| GAP-170 | epistemic-reasoning旧内核 | RETIRED | 旧内核已由sphinx-v2取代，旧测试归档保留hash；新运行链见GAP-219。 |
| GAP-171 | 旧Sphinx兼容协议 | RETIRED | 不恢复旧原生工具内核。新独立MCP入口与OpenCode不注入MCP的合同可以共存。 |
| GAP-172 | 旧Sphinx submit准入 | RETIRED | 旧接口不再是现行所有者；新入口准入必须在v2真实runtime证明。 |
| GAP-173 | 旧Sphinx Research export | RETIRED | 旧bundle要求不迁为新内核隐藏义务；历史材料仍可查。 |
| GAP-174 | 旧Sphinx取消revision | RETIRED | 旧修复不恢复到新内核，不能声称v2取消已证。 |
| GAP-175 | 旧Sphinx Agenda依赖选择 | RETIRED | 旧调度器已退役；v2调度与完成的因果关系需独立证据。 |
| GAP-190 | obligation-ledger | PARTIAL | 新七条只守住 Host-native todowrite 边界：provider 名单 `obligations` 换回 `todos`、数组原样交给宿主，插件只处理换名与 retainCheckpoints；实际安装版 Host TodoTable 物理替换仍见 GAP-220。 |
| GAP-191 | 旧账目故障政策 | RETIRED | 旧语义账本不再成立；新UI交付故障不能悄悄反向决定认知状态，按新合同补证。 |
| GAP-192 | relay-incumbency | PARTIAL | 真实绑定与fold局部证明保留；固定DevOps映射不再由接缝填默认值。跨任期真实恢复和控制权移交未闭合。 |
| GAP-193 | relay-assessment | PARTIAL | Manager当前事实只读取证已对齐；评审独立性、实际在途请求和证书失效链仍缺证。 |
| GAP-194 | relay-assessment精确重放 | PARTIAL | 精确相同评审重放与新评审冲突须区分，不能只改标题或调用两次即认幂等。 |
| GAP-195 | relay-context-projection | PARTIAL | 新接缝调用真实NarrativeTransform；旧projectMessages透传API保留兼容但不计证据。完整历史、真实物理发送和stale中断尚需执行。 |
| GAP-196 | 旧ProjectionCut删owner请求 | RETIRED | 上游改为完整历史，不再要求旧裁剪；新实现保全历史的真实证据仍归GAP-195。 |
| GAP-197 | relay-retirement | PARTIAL | 真实工具scope与受控资源收束用例保留；跨进程终止、递归live资源与完整退休不等于纯分类结果。 |
| GAP-216 | 全局构建 | OPEN | 上游plugin-composition仍引用已删除Vault及StrengthDelegate等；当前不能生成全局新鲜产物。部分认知/Relay独立闭包另缺CanvasCodec、AssumeFactCases、AgentFact等依赖，已实际编译确认失败。 |
| GAP-217 | host-boundary-032 | PARTIAL | 现在精确断言值、对象身份、原键序和真实Host终态；现源码删除contract后尾部defineProperty，静态分析预示中间/首位键序反例，尚未执行。异常路径TODO保留，canary不再默认成功。 |
| GAP-218 | crash-reconciliation-018/020/021 | PARTIAL | 上游load结算忽略append Error，TargetAgent空值又回退Byname；新接缝不补造成功事实。需在真实加载入口保留失败并验证合法历史材料边界。 |
| GAP-219 | sphinx-v2真实入口 | OPEN | Wire Surface模板不能证明runtime执行；MCP工具handler忽略各工具输入，OpenCode状态/结果适配仍为占位。局部算法断言保留，真实创建、调度、取消、恢复和结果交付TODO不关闭。 |
| GAP-220 | obligation-ledger-001/002/006 | PARTIAL | 已证明插件 before 把 `obligations` 换名为 `todos`、只剥离 retainCheckpoints，数组对象与内容原样交给 Host（上游 Effect schema 解码已在 throwaway smoke 验证）；仍缺安装版 OpenCode 对当前 session TodoTable 的真实替换/清空物理 canary。 |
| GAP-221 | cognitive-workspace、action-affordance-014 | CLOSED | 持久 canvas/jq/TodoSink/CognitiveRuntime 已从生产编译图删除；assume 收敛为单一 assumption 输入与固定不回显结果，legacy Cognition journal 仅兼容解码后 no-op。 |
| GAP-222 | sphinx-v2替代合同 | OPEN | SUPERSEDES称部分旧Bayes合格条件、标准算法退化与全链取消仍保留，但新条款承接边界不完整。需在现行WHAT明确必要的一致性，不能仅改测试锚点便继承旧隐藏规则。 |

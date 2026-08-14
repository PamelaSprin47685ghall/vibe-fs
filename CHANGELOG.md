# Changelog — 版本历史

## Unreleased

- 机械检查瘦身（2026-08-15，用户要求）：删除 `kolmogorov-size` 行数 advisory（`scripts/checks/kolmogorov-size.mjs` + baseline + `kolmogorov-size-advisory.test.mjs`）与 `enforcer-cross-family-collision` A40 机械替代（gate + GD-010 条款 + 本体测试）。
  - 行数从此不做任何机械检查（VERIFICATION-SYSTEM-012 更新：非门禁且无 advisory）；检测语料可区分性归 review 判断（A40 人类 tournament）。
  - VERIFICATION-SYSTEM-012 机器载体 = `requirements/verification-system/tests/no-line-count-check.test.mjs`（结构性 absence：本包 tests 与 scripts/checks 无行数检查指纹）。
  - check.mjs wired gate 20→18；proof-ladder 下限同步下调；e2e/support 13 处 advisory 注释清理；verification-system 四文档与 guidance-delivery 三文档同步。

- 结构重排第一轮（平衡树式旋转，2026-08-14）：`Application/Reconciliation` 拆散归各语义 owner，
  `Journal` 掏空为持久化基板；此后 **namespace = dir** 为仓库规则。
  - `Composition/Turn/`（ReconciledTurn→Observation、TurnBinding→Binding、Reconciler→Scheduler、
    ReconcileSupervisor→Supervisor、TurnWorkflow→Workflow + TurnReconcile/ReconcilePass/OrdinaryTurnWorkflow）、
    `Composition/Durable/`（AgentProjection→Projection、ProjectionState、ProjectionUpdate、FoldRejection、Fold 路由）、
    `Composition/Bridges/FinalityReview/`（FinalityReviewCohort 接缝显式化）。
  - 各 bounded projection/fact-fold 归家：Context/{Trace,Prefix,Companion/Blogger}、
    Interaction/{Authority,Dispatch,Repair}、Feedback/Enforcer(+Guidance)、
    Execution/{Session,Delegation,Fission}、Mission/{Manager/Life,Obligation/Todo,Review,Review/Barrier,Review/Assurance}、
    Change/Orchestration、Participant/Provider/Attempt/Fallback、OpenCode/Contract。
  - `Persistence/Journal/` 仅剩 substrate：Envelope/Codec/Writer/Boot/AgentJournal/SharedJournal/RuntimePath/FactCodec/EventStoreJournalWriter。
  - `Kernel/Fact.fs` 外层 union 与 per-family facts 拆分（第三刀）留待下一轮（wire-compat 评估后）。
  - 移动文件 namespace 跟随目录；引用按编译器驱动补 opens；测试 dist import 与 requirements 文档路径同步更新；
    `dsl-ownership` host-boundary 白名单扩展（过渡项，第二轮后移除）。

- Requirement Package cutover 收尾：`docs/`、`changes/`、`tests/` 全部腾空。
  - 45 包 normative 树 `requirements/<package>/{WHY,WHAT,HOW,PROOF}.md` 为唯一语义权威；
    旧 Clause 与变更记录已归档（2026-08-14 cutover；git 历史可回溯）。
  - 测试全部分包：`tests/unit` 146 文件 MOVE/SPLIT/DELETE 归各包 `tests/`；
    `tests/eval` → `office-capability`；`tests/integration` suites 归 owner 包；
    e2e Long Stroke、support harness、unit/integration runner 归 `verification-system/tests/`。
  - `package.json` release ladder 与新路径对齐；meta-verifier 骨架源迁入 `requirements/INDEX.md`。
  - 迁移 ratchet 退休：`g4r-freeze`、`student-teacher-absence`、`enforcer-rulebook-gate`（retired stub）。

## 0.8.2

- Provider Surface Grand Repair：ARCH-017 Office Capability；PROMPT-020 Tool Affordance；PROMPT-021 Critical Semantic Redundancy；ARCH-016 Gate F。Role Law 教身份，Tool Law 教动作，Delegation Law 教他人能成为什么。
- HOST-013 ordinary renderer：OpenCode Host 不再写 pending FakeReq。每个 occurrence 在 ResultGap 渲染一条 completed `auto-injected` tool part，由 `toModelMessagesEffect` 展开为 provider tool-call + tool-result，消除伪中断文案。
- 持久化、Git 与 session 工作流统一采用异步 Task 调用链，减少 Node 事件循环中的同步等待；GitGateway、EventStore、AgentJournal 与 blob 路径完成贯通。
- SyncDelegate 语义批处理、WorkRecord/Lifecycle 物化、HostFork/Join/Recovery/Enforcer/Finality/Manager/Review 的 durable 顺序进一步收口。
- 发布 Fork `attach`、Horizon 最新子 Agent 工作摘要，以及 Magic Todo / dedicated reviewer 的 obligation 与 assignment 改进；包入口与 durable store schema version 不变。

## 0.8.1

- REVIEW-003 skeptical challenge 迁入 `resources/provider/review/challenge`；tool result / nudge / seal 跟 Reviewer session `ProviderLanguage`；英文 canonical 字节不变（`ChallengeTextVersion = 1`）。
- journal / 公开 wire 合同相对 0.8.0：无 domain protocol 破坏。

## 0.8.0

- Provider-visible prose ownership（PROMPT-019 / ARCH-016 Gate E）：进入 participant horizon 的 Class A 自然语言经 `ProviderResources` 装载、由 session `ProviderLanguage` 管辖。Gate E baseline `{}`。
- Gate C 现行面补齐：叶对 + `{{placeholder}}` 集合一致 + Role Law semantic-anchor 同 ID 双语命中（`scripts/checks/semantic-anchors.mjs`）。
- HOST-013 pair guideline 迁入 `resources/provider/host/pair-programming-guideline`；生产路径 `ProviderProse.render`，禁止 `match lang` 挑选正文。
- Role Law 是身份文本，不列工具名。REVIEW-003 challenge 仍为固定英文协议句。
- journal / 公开 wire 合同相对 0.7.0：无 domain protocol 破坏。

## 0.7.0

- Kolmogorov 所有权二级拆分（语义汇流点，非按行数切文件）：
  - LWR journal 物化 → `LifecycleWorkRecordProjection`；`XTraceCapture` 只保留 semantic capture。
  - Manager durable open / migrate / activate → `ManagerLifeWorkflow`；`ManagerNarrativeTransform` 只保留 wire 门控与 provider rewrite。
  - `PluginTransforms` 只保留 hook 顺序；Strength replay/traced → `StrengthReplay`。
  - `HostSignalBootstrap` 退回订阅/路由；政策 → `HostTurnObserver` / `HostCompactionObserver` / `HostSessionDeletion`。
  - `Reconciler.Scheduler`（coalesce/drain）与 `ReconcilePass`（causal reread/publish）分居；`ReconcileProgram` 不变。
- Wave 0–5 Kolmogorov 重构收口：`kolmogorov-size` ratchet；JsTools / ProjectionAlgebra / PluginRuntimeScope / Fold / EnforcerHost / SpikePlugin / Codec Projection / HostForkJoin / SyncDelegate 等按 owner 装箱（详见 `changes/completed/refactor.md`）。
- Strength 提案闭环；EnforcerContinuation 从 EnforcerHost 二次抽出。
- G6 Casebook / G9 Session ownership ratchet Product Exit（问卷八 kind + 接线 gate）。
- JS capability-projected tools：structured TOML result；grep/glob 能力面收紧。
- AGENT-026/027：Stealth Browser MCP Host 接线 + 内部 Semble MCP；共享 `McpLaunch` 词汇（Disabled / Fixture / Uvx），消除 dup-cases。
- 持久化写入延迟：EventStore 的 Git raw store 不再为每个对象 spawn 一次 `git`。新 `GitObjectDatabase` 直接读写 loose object（`sha1` + `zlib` + `objects/xx/yyyy`，tmp+rename），并对内容寻址的对象/tree 读取与 `mktree` 结果做实例级 memo。单事件 append 由 **24 次同步 git 子进程 / ~60ms** 降到 **2 次 / ~7.5ms**；由于 `execFileSync` 会阻塞 Node 事件循环，这段成本此前会让同一 Host 内所有 session 串行等待。oid、on-disk 布局与 `git cat-file` 可读性完全不变（`tests/integration/persist/object-identity.test.mjs` 对真实 git 二进制逐项比对）；`gc` 之后的 packed 对象仍回落 git CLI 读取。
- FALLBACK-013：Host abort/cleanup 残留（在途工具被标 `status=error` + `metadata.interrupted=true`）不再推进 A/A/B/B cursor、不消耗自动恢复预算。此前 owner 的一次 provider 失败会被记两次——一次来自它自己的失败路径，一次来自被同一次 abort 清理打断的 Companion cycle（且用 Blogger 的 `ProviderRunIdentity`，FALLBACK-003 去重无法折叠）——导致 provider 可见的 A/A/B/B 顺序取决于两次 append 的竞争，恢复可能落回刚失败的同一侧。Companion 侧仍注入一次 `# Protocol repair`，有界性由 ENFORCER-153 marker 保证；`ToolExecutionError`（无 `interrupted`）仍按 ENFORCER-065/068 推进 cursor。
- journal / 公开 wire 合同相对 0.6.0：无 domain protocol 破坏；控制流与 Host 边界所有权收紧。

## 0.6.0

- Causal CE / 时序所有权：可观察因果等待、Wait Graph、waitFact 续期归因；Reconciler 去业务轮询；Join interrupt / user-wake 收口；Diagnostic Bridge。
- Manager Finality / lifecycle：`FinalityTool`、terminal frontier、sibling steering / durable revision；PERFECT 后的收口与 rest-in-peace 路径。
- HOST-013：guideline pair 永久 append-only；prefix-cache 不变量；idle-derived continuation 资格门控（SessionQuiescenceGate）。
- Student–Teacher CE collapse：Teacher 侧单一 CE await 链；durable evidence；相关单元/回归收口。
- Projection Algebra / Glory：attempt-local PrefixProbe 与 plain-X 前缀投影迁入投影 DSL；idle / revise / MISSING_FINAL_REPORT 观察路径加固。
- Coder 工具面：`bash-honeypot` 禁未授权 shell；严禁 Coder 跑测试；PTY prompt 补齐换行。
- EXEC-028：同步 one-shot `inspector`/`coder` 返回统一为 entry-local LWR 注释（`includeOpening=false`）+ 末条 TurnFormalText，禁字段式 `work_record`；与 Join 共用 COMPANION-003 物化器。Opening 在 send 前从原始 assignment 捕获以便物化；`Completed` 无法物化非空 LWR 时 fail-closed 返回工具级 `error=`，不 soft-omit。
- LWR 段标题在 materialize 中为纯文本（`Opening task` / `Work log` / …）；`# ` 仅由 `SyntheticToml.comment` 在 wire 注入，消除 join/oneshot/finality 上的 `# # Work log`。
- Enforcer / Blogger-as-Enforcer rebase 文档收口：`how`/`shape`/`proof` 对齐 tip-v2 基线（PartOrdinal-first 多调用 tip、物理所有权轴、`§13` 证明清单）。`bounds.test.mjs` 永久回归锁定归并 size/count 越界 fail-closed（>32 calls / text >512 KiB / evidence >128 KiB）；未恢复 wire/runtime score 路径。
- 文档治理：变更单文件生命周期 `changes/{proposed,active,completed}`；条款 ID 唯一归属正式层；`PENDING.md` 收口为 COMPLETED/HISTORICAL；`AGENTS.md` 修正 architecture 文件数与 `gate:dsl-ownership --threshold=0`。
- Canary unbend：纠正迎合错误生产的声明扭曲；e2e 事件驱动等待取代固定 poll slice。
- journal / 公开 wire 合同相对 0.5.4 兼容方向：控制流、投影与 Host 不变量收紧；破坏性细节见上列条目与 `docs/`。

## 0.5.4

- AGENT-019：managed agent Host-final permission 固定 `external_directory = allow`，覆盖 Host 默认 ask，取消项目外路径的交互确认。
- DSL 全面主导化（ARCH-001 / FLOW）：门禁债 `157 → 0`。
  - 删业务 Program AST / Interpreter；Child/Session Recovery、Orchestrator/Reconcile/Join 直接 CE。
  - `Kernel/Flow` → `Kernel/Parallel`（仅 `mapBounded`）；`CycleDisposition`、`DrainWindow`、`BloggerRuntimeHost`。
  - `dsl-ownership` 契约：合法 mutable（Domain/Session/Application/Parallel）；Host 边界 `open` basename 白名单；`--threshold=0`。
- e2e 稳定性：`gitConflictProof` 挂 worktree 已存在之后；`ProcessHost.stop` 在 leak assert 前回收残留 listen 端口。
- AGENTS.md 收束为现行纪律（P0–P3 施工表退役）；`TASK.md` 作历史档案。
- 无 journal / wire 协议破坏；控制流与门禁契约收紧，产品对外协议与 0.5.3 兼容。

## 0.5.3

- No runtime protocol changes.
- Normalized source, resource, specification, test, and build layouts.
- Replaced the generated Enforcer catalog with packaged runtime data.
- Packaging now uses the repository root and includes resources directly.
- Removed migration evidence, generated conformance ledgers, and legacy gates.
- Renamed internal files and test directories without changing public behavior.

## 0.5.2 — 全 SSOT 收敛

- 收敛目标：Active 规范全部收敛。
- 规范：spec/14 Strength、spec/16 Student&Teacher、ENFORCER nudge/throttle/规则目录迁出到 `RFC/`，spec/15 仅保留 0.5.1 已交付的 Blogger 工具化子集。
- 版本：全仓文案从 `0.5.0-rc.1` / `0.5.1` 统一到 `0.5.2`。

## 0.5.1 — Blogger vertical-slice convergence (spec/15)

生产闭环 Blogger 请求形状 / 挂起 / Squash / 恢复载体（不做 Enforcer throttle、nudge、Strength、Student&Teacher）。

### Runtime authority
- 生产 `BloggerRuntimeCell`（Idle / InFlight / Parked / Disposed）
- `CurrentRequest` 与 `PendingOffer` 双槽；唯一 busy 定义 = InFlight
- 唯一入口 `BloggerCoordinator.onMainMaterial`；删除 `offerToBlogger` 旁路与 `inFlightTask` busy 权威

### Projection & commit
- 发送前冻结 typed context 并落盘 `BloggerRequestMaterialized`
- 首次 / resume / Squash 共用 `CompanionProjectionBuilder`；删除 raw TOML 抽取与 `BloggerNeedsReset`
- Squash 迁入 blog tool continuation，提交 `BlogSquashCommitted`（coverage 不变）
- 仅 `KnownCommitted` 后 Park；`KnownNotCommitted` / `CommitUnknown` 不 Park、不重问
- 统一 `BloggerCycleReceipt`（Entry|Squash）按 ProviderRun 幂等
- 一次 `RepairSpent` repair；资源上限；Main Entry 成功清 fallback

### Recovery & teardown
- crash-window recovery 挂 `EnsureRecoveryDone`；live CurrentRequest 不 stomp
- fail-closed `loadEffectiveFrames`；`CompanionIdentity.newWorkMessageId`
- Host 重建消息带 synthetic/source 标记；main dispose 清 linked Blogger waiter

### Evidence
- layer-4：`host-transform-capability-canary`（park/resume、第三 turn 单飞、materialize）
- layer-4：`companion-canary`（同 child 两轮 blog tool）
- 静态 `blogger-convergence` 防回退门禁
- 条款收敛：`COMPANION-005/008`、`CTX-006/007/012`、`ENFORCER-010`

## 0.5.0 — 正式版

- 正式发布：0.5.0（从 rc.1 收口；breaking changes 见 `0.5.0-rc.1` 条目）
- 生产可用：canary 森林 17 驱动（18 剧本）× 3 轮全绿，`test:release`（gate:static →
  build → unit → harness → P0×3）完整通过
- Review 双 PERFECT 见证（REVIEW-006/007）
- Orchestrator 恢复链（ORCH-005/006/007）：restart 后 exactly-once publish、rebase
  冲突恢复
- guard nudge seal 稳定性修复（ORCH-006/ARCH-004）：session worktree 目录绑定
- 来源解析顺序（PROMPT-004/009）、发送格式（PROMPT-006）、fire-and-forget（PROMPT-007）
- 工具权限双层 fail-closed（AGENT-007）
- 未验证条款清零（8 条批量段条款补第 1 层判据）

## 0.5.0-rc.1 — docs freeze / RC development

Breaking changes:
- All agents now require explicit `fast-*` or `deep-*` names
- Unprefixed agent names, `build`, `plan` aliases removed
- Agent-to-model bindings read exclusively from `opencode.json`
- All Wanxiangshu model environment variables removed
- No longer persists or overrides model IDs
- Provider fallback cycles A/A/B/B within budget（Cursor 无限定义；自动恢复上限默认 12 连续失败）
- Provider retry count no longer kills a Logical Run
- Blogger and Executor Agent are now internal fast/deep pairs
- Pre-0.5.0 runtime journals not supported

## 0.4.0 — 最终版

- Structured Agent Program (Flow CE, no Stage/Phase/Lease platform)
- Prompt Authority / Logical Run rules
- Companion + ActivePrefixEpoch / FrozenB cache protection
- Manager `fork-agent / join / list`; Orchestrator `fork-manager / join`
- Static role matrix with full system prompts
- Logical-Run Fallback A/A/B/B with durable retry writer
- Dual PERFECT Review with ProviderRunIdentity binding
- Process/Executor: 3× estimate, large gate, 200KB ripple-carry
- PTY via DevOps `fork-pty` only; onExit-only completion; structured signals
- Orchestrator: clean gate, worktree, serial publish lock, rebase, re-review, ff-only
- OpenCode adapter: idle/retry/deleted signal + single-flight reconcile
- Private distribution: `private: true`, provisional commercial LICENSE

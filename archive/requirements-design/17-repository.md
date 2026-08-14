# Repository knowledge / programming

## `repository-investigation`

WHY: repository claim 必须由对本地已存在世界的真实观察建立；reasoning、semantic search hint、旧 Case、外部 web 都不能自动冒充当前 repository evidence。

OWNS:
- local repository fact acquisition 的证据合同：真实观察、locatability、provenance。
- evidence acquisition 与 semantic reasoning 分层；reasoning 可决定问什么，不能凭思考增加 repository evidence。
- investigation 选择 cheapest adequate observation，并在足够回答当前事实问题时停止。
- observation 在因果意义上只读；不得为了观察改变 repository 或运行应用制造新行为。
- warm-start/semantic search 命中只是低信任 orientation data，必须由真实观察确认后才成为 fact。

DOES NOT OWN:
- Office authority canonical definition。
- Casebook cache、web/external facts、repository mutation/execution。
- 当前 Inspector Persona、Semble MCP、read/glob/grep 名字。

DEPENDS ON: `office-capability`, `participant-horizon`。

PROVIDES: “这是当前 repository 已存在事实”的可追溯 evidence guarantee。

FAILURE MEANING: RED = 推理、旧缓存、搜索 hint 或修改后观察可以被当成原 repository fact。

INDEPENDENT CHANGE: 替换具体文件查询工具/semantic search orientation，而 evidence contract 不变。

CURRENT EVIDENCE: AGENT-012/024/032；resource `resources/provider/tool/{inspect,query-shell}/`、`resources/provider/role/inspector/`；wiring `Agent/AgentProgram.fs`、`Infrastructure/RepositoryWarmStart.fs`；failure Semble 低信任 hint 不冒充 fact（`Kernel/SembleMcp.fs`、`Infrastructure/SembleMcpStdio.fs`）；inspect/query-shell、warm-start tests。

---

## `knowledge-reuse`

WHY: 已花成本建立的 repository knowledge 应能复用，但旧答案永远不是当前正确性的证明；reuse 是 best-effort cache/hint，不是知识数据库 authority。

OWNS:
- Case = one question + answer + supporting replayable observations 的 reusable unit。
- fetch/reuse 前按当前 worktree replay observations 形成 freshness hint。
- no-delta/freshness ≠ correctness proof。
- 检测变化时可基于已提供证据重塑 Case；maintenance participant 不自动获得回 repository 取证权。
- concurrent revisions 显式 conflict，不按 timestamp/revision LWW。
- feature 可 opt-in；未启用 repository 行为保持不变。

DOES NOT OWN:
- durable store substrate、current repository fact acquisition、semantic-search warm start。
- Bookkeeper 当前 Persona/tool/programming HOW。
- general replica convergence。

DEPENDS ON: `repository-investigation`, `durable-events`, `durable-convergence`。

PROVIDES: 可复用但不冒充当前证明的 repository knowledge cache。

FAILURE MEANING: RED = 旧 Q/A 被当作当前事实、freshness 被当 correctness、或并发更新静默 LWW 丢失分支。

INDEPENDENT CHANGE: Case maintenance 从 Bookkeeper agent 改成 deterministic merge + optional LLM，而 Case reuse semantics 不变。

CURRENT EVIDENCE: `docs/why/casebook.md`；CASE-001..012；type `Domain/Casebook.fs`；wiring `Infrastructure/{CasebookCapture,CasebookIndex,CasebookLifecycle,CasebookReplay,CasebookSessionDraft,CasebookWorkflow,CasebookBookkeeper,BookkeeperStaging,BookkeeperRuntime}.fs`；fact `Infrastructure/CasebookStore.fs` + EventStore（InspectorCase*）；tests `tests/unit/casebook/**`。

---

## `repository-programming`

WHY: 可编程 repository/file mutation 若拆成多套独立 RPC，会重复 path boundary、sandbox、transaction、result rendering 与 capability gate；模型看到的 surface 必须与实际能力同构。

OWNS:
- capability-projected programming SDK：surface 只暴露当前允许的方法。
- sandbox 无 ambient OS authority；program 只得到显式能力与数据。
- read/transform/write 可在一个 bounded transaction 中组合。
- staged mutation、pre-commit validation、multi-file all-or-nothing commit。
- conflict detection 与 failure algebra。
- program return 在 commit 前必须可诚实编码。
- generated surface/description/example 与 runtime gate 消费同一 capability truth（四层同构由 `capability-enforcement` 拥有，本包只把它应用到编程面）。

DOES NOT OWN:
- office capability canonical authority。
- builtin tool 是否长期 coexist。
- 当前 JS language、base class、`js-*` tool names。
- Synthetic TOML 的一般 representation law。
- capability 同构/同源律本身（`capability-enforcement` 拥有）；本包只应用它到编程 SDK。
- Git shared-ref integration。

DEPENDS ON: `office-capability`, `capability-enforcement`, `effect-accounting`, `durable-events`, `participant-horizon`。

PROVIDES: bounded, transactional, authority-isomorphic repository programming surface。

FAILURE MEANING: RED = 模型看到无权方法、program 获得 ambient authority、或 multi-file mutation 可半途落盘留下不一致世界。

INDEPENDENT CHANGE: 从 JavaScript sandbox 改成另一 embedded language/IR，而 capability projection 与 transaction semantics 不变。

CURRENT EVIDENCE: `docs/why/js-tools.md`；JS-001..020；type `Domain/{JsCapability,JsSurface,JsDescription,JsFailure,JsAnchor,JsTransaction}.fs`；wiring `Infrastructure/{JsToolsBindings,JsAnchorFs,JsToolsTransactionStore,JsGlobFs,JsMutationFs,JsUtf8Fs}.fs`、`Process/JsSandbox.fs`；failure `scripts/checks/js-surface-gate.mjs`；tests `tests/unit/js-tools/**`。

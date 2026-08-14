# delegation

## 一句话 WHY

把一项语义工作交给另一个 participant 时，**authority、charge、owner 与返回后果**必须明确而不泄漏 runtime topology；否则机器拓扑会冒充业务委托。

## WHAT 概览

- 委托 = 语义 charge + entitled office + 逻辑 owner + bounded 返回后果（DELEG-001/002）。
- 独立 road 与 same-road continuation 硬区分；不同 contract 不同工具名（DELEG-003/004）。
- 机器身份（SessionId/AgentId/job_id/worktree/...）永不进 provider（DELEG-005/006）。
- 同步委托（SyncDelegate）以普通 completion → bounded WorkRecord 收口，无 `return` 第二出口（DELEG-007..012）。
- Join / horizon / commission 是委托面的观察与收束语义（DELEG-013..016）。
- 返回结果只改变 caller 认识，不转移 authority（DELEG-017）；NEEDHELP consultation 是真实 child 委托（DELEG-018/019）。

## HOW 概览

实现模型：`Kernel/SyncDelegate.fs`（DedicatedDelegateKey / SyncDelegateBatch / InvocationResult）、
`Domain/{SyncDelegatePrompt,ForkChildPayload}.fs`（Charge/ProviderPrompt 分离、fork child 首 prompt）、
`Session/{SyncDelegateRuntime,SyncDelegateWorkflow,SyncDelegateWait,SyncDelegateCallStore,ForkRuntime}.fs`
（batch admission、reuse key、级联）、`resources/provider/tool/{fork,commission,inspect,sync-delegate}/` 与
`resources/provider/delegation/**`（provider 散文）。详见 HOW.md。

## PROOF 概览

- 包内：`tests/fork-child-payload.test.mjs`（MOVE，fork child 首 prompt 渲染）。
- REUSE（SPLIT@cutover）：`tests/unit/session/sync-delegate-runtime.test.mjs`、
  `tests/unit/session/sync-delegate-ce-collapse.test.mjs`、`tests/unit/tools/{fork-tool,sync-delegate-tools}.test.mjs`、
  `tests/unit/execution/join-*.test.mjs`、`tests/unit/orchestrator/{host,runtime}.test.mjs` 等。完整落点表见 PROOF.md。
- Semantic anchors（`scripts/checks/semantic-anchors.mjs`）拥有：manager 组 `entrust-by-consequence` /
  `choose-by-return` / `no-omnipotent-charge` / `returned-record`；orchestrator 组 `owns-roads` /
  `same-road-continuation` / `independent-destination`；fork 工具组 `office-not-witness` / `create-and-continue`；
  inspect 工具组 `repository-fact` / `causal-readonly` / `no-code-changes` / `no-behavioral-execution` /
  `no-implement-or-repair`；commission 工具组 `independent-road` / `not-lifecycle-stage`；
  establish/repair-behavior 工具组 `coder-writes-source` / `not-execution-evidence` / `meaning-decided` / `not-passing-proof`。

## 阅读顺序

1. `WHY.md` — 为什么必须独立存在、RED 是什么、历史失败模式。
2. `WHAT.md` — normative 合同（编号命题，唯一权威）。
3. `HOW.md` — 实现模型与边界（非 normative；含「历史与弃权」）。
4. `PROOF.md` — 每条命题的可执行证明落点与运行方式。

## 边界（DOES NOT OWN）

office capability 本身（`office-capability`）；session 生命周期/复用/级联（`managed-session-lifecycle`）；
provider 渲染（`provider-projection`）；`fork`/`commission`/`inspect` 当前工具名（HOW）；dedicated session
reuse 的具体 HOW（`managed-session-lifecycle`）；bounded WorkRecord 的物化格式（`work-record`）；
horizon 的机器准入过滤（`participant-horizon`）。

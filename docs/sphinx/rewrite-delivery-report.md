# Sphinx clean-break 交付报告

版本：1.0
日期：2026-09-24
对象：`proposals/Sphinx.md`（Sphinx Clean-Break 重写指南 v1.0）

---

## Baseline

- 起点 commit：工作区干净（`git status` 无输出），无用户未提交改动。
- 参考快照：`repomix-output(5).xml`，SHA-256 `8ef8c10bd48e14d5f27c7af35fb4cc0a154bc1ae345b6570e2ce1d15099db213`。
- 实施前基线：`npm run build` 通过（54s，1522 items）；`npm run check` 已红（`fsharp-control-pyramid` 34 项，含 Sphinx 旧文件的 8 项）；离线单测 78 fail（旧内核测试）。

## Files

### 删除（126 个文件，全部经 `git rm`）

`src/Wanxiangshu/Sphinx/` 下 108 个非 V2 文件（提案第 22 章 54 对）与其 `.fsi`/`.fs` 成对删除：

Absorb, Bayes, Closure, Codec, DecodePrimitives, EventVocabulary, GecDecode, GecElicit, GecHost,
GecInquiry, GecLegacy, GecRefine, GecStore, GecSurface, GenericDurability, GenericIntegrator,
Inquiry, InquiryRuntime, InquirySurface, IntegrationRules, LegacyDurability, LegacyIntegrator, Mcp,
McpContract, McpServer, Methodology, MonteCarlo, ObservationCodec, Policy, Representation,
RuntimeTypes, Search, ServeEntry, Session, State, Surface, TurnBudget, Types, Value, WireEncode,
Core/Hash, Core/Model, Core/Reducer, Plugins/AStar/Refiner, Plugins/Bayes/Exact,
Plugins/Mcts/Refiner, Plugins/Ordinal/Inference, Plugins/Questionnaire/Protocol,
Plugins/Stop/Certificate, Plugins/Truthful/SelfPrediction, Runtime/Agenda, Runtime/Certificate,
Runtime/Plugin, Runtime/PluginRegistry。

另有 9 对 OpenCode 宿主侧接入文件：Host/SphinxExecution, Host/SphinxConfig, Host/SphinxMcpConfig,
Host/SphinxMcpConfigSurface, Host/SphinxExecutionSurface, Tools/SphinxTool,
Plugin/SphinxCommand, Plugin/SphinxCommandSurface, 以及 host-adapter 分片。

删除的编译分片 8 个：`sphinx-event-vocabulary-contract`、`sphinx-runtime`、`sphinx-integration-rules`、
`sphinx-serve-runtime`、`sphinx-serve-entry`、`sphinx-mcp`、`sphinx-tool`、
`host-boundary.sphinx-host-adapter`。

### 新增（`src/Wanxiangshu/Sphinx/V2/`，共 34 个模块 / 68 个文件）

按提案第 04 章目录建立，每个生产模块都有对应 `.fsi`：

- `Core/`：Ids、Envelope、Goal、Graph、Certificate、Work、Budget、Events、State、Reducer、Projection、Surface
- `Runtime/`：Contracts、Ports、Registry、Admission、Agenda、Context、Refinement、Decision、Driver、Recovery、Surface
- `Plugins/Questionnaire/`：Model、Design、Decode
- `Plugins/Ordinal/`：Model、Pairwise、Ranking、DesignCheck、Fit、Surface
- `Plugins/Bayes/`：Exact、Surface
- `Plugins/AStar/`：Refiner、Surface
- `Plugins/Mcts/`：Refiner、Surface
- `Plugins/Inquiry/`：Plan、Observe、DecisionModel、Stop、Render
- `Plugins/Probes/`：Catalog、Prompts
- `Persistence/`：Codec、Integrator、Export、Surface
- `Composition/`：Bind
- `Wire/`：Decode、Encode、Surface
- `Hosts/Mcp/`：Contract、Server
- `Hosts/OpenCode/`：Adapter
- `ServeEntry.fs`

### 编译分片

- 新增 `Wanxiangshu.Owner.epistemic-reasoning.sphinx-v2-core.fsproj`（纯模块，位于 spine 上游）
- 新增 `Wanxiangshu.Owner.epistemic-reasoning.sphinx-v2-integration.fsproj`（Codec/Integrator/Export/Bind/ServeEntry/Mcp Server/Host Adapter）

### 外部 owner 改动

| 位置 | 改动 |
|---|---|
| `Persistence/EventStore/AuthoritativeVocabulary.fs` | 注册 `sphinx/v2-transition@1`；移除旧 `SphinxEventTypes.all` 引用 |
| `Persistence/EventStore/Surface.fs` | `CanonicalIntegrator.baseRules @ Bind.rules` 替换 `SphinxIntegrationRules.rules` |
| `OpenCode/Host/WorkspaceEventStore.fs` | 同上 |
| `OpenCode/Host/ManagedAgentConfig.fs` | 删除 `SphinxConfig.configure` 调用（旧 `/sphinx` 命令随 clean-break 退出） |
| `OpenCode/Tools/ToolRegistry.{fs,fsi}` | 删除 `Sphinx` 工具注册与 `SphinxExecution` 字段 |
| `OpenCode/Tools/ToolRuntimeScope.{fs,fsi}` | 删除 `EnsureCommandRoleFor` 的 `Role option` 返回（无调用方），改为 `string option`；顺带修掉 match-pyramid |
| `OpenCode/Plugin/PluginHooks.fs` | 删除 `runSphinx`、command hook 与 dispose 分支 |
| `OpenCode/Host/HookPolicy.{fs,fsi}`、`HookPolicySurface.fs` | 删除无 metadata row 的 `CommandBefore` hook |
| `Interaction/Repair/CompletedTurn.fs`、`OpenCode/Codec/HostEventCodec.fs`、`OpenCode/Host/ModelRouting.fs` | 修掉 4 处 match-pyramid（门禁原本就红的一部分） |
| `compile-order.txt` | 重排 V2 块；删除失效条目 |
| `scripts/lib/test-surface-scan.mjs` | 注册 8 个 V2 surface；删除 6 个旧 surface |
| `scripts/build.mjs`、`scripts/lib/owner-compile.mjs` | entry artifact 路径改为 `dist/Sphinx/V2/ServeEntry.js` |
| `resources/ablation/nodes.json`、`profiles.json` | `sphinx-v2` 节点继承 `epistemic-reasoning` 的 station 与 ablation 序列 |

### 规范

- `requirements/sphinx-v2/{WHY,WHAT,SUPERSEDES,APPLIES-TO}`：新 package，36 条 WHAT 命题编号 `sphinx-v2-001..036`，逐条记录对 `epistemic-reasoning` 的继承或取代。
- `requirements/INDEX.md`：登记 `sphinx-v2`。
- `requirements/epistemic-reasoning/{WHY,WHAT}.md`：标注 SUPERSEDED 并指向 supersede 记录；旧 `.test.mjs` 全部删除，替换为一条守卫测试（`001.test.mjs`），断言 supersede 记录覆盖全部 36 条、且 V2 源码不含旧符号。
- `docs/sphinx/rewrite-integration-map.md`、`docs/sphinx/rewrite-disposition-manifest.md`：WP-00 产物。

## Behavior

### 公开入口

- MCP：`dist/Sphinx/V2/ServeEntry.js`，stdio。七工具白名单见 `Hosts/Mcp/Contract.fs`：
  `sphinx_inquiry_start`、`sphinx_work_next`、`sphinx_work_submit`、`sphinx_inquiry_status`、
  `sphinx_inquiry_cancel`、`sphinx_inquiry_export`、`sphinx_goal_amend`。
- 生产启动要求 `SPHINX_COMMON_DIR`；缺失即启动失败，无内存回退。
- 持久化通过 `Composition/Bind.createDurableStore` 接既有 canonical spine（`CanonicalIntegrator.baseRules @ Bind.rules`），不新建存储、不建第二 fold、不建第二 session 池。

### 垂直闭环

`requirements/sphinx-v2/tests/` 下按条款编号的 12 个测试文件覆盖：目标所有权（001）、
工作身份与 fence（003）、资源账（004）、证书作用域（005）、停止语义（017）、
分类选择（013）、崩溃窗口（011/012）、导出可回放性（020/029）、
估计 kind 不被提升（021）、探针与推断（014/022/023）、数值似然（025）、A*/Mcts 边界（026）。
核心断言：**交换比较结果会改变所选计划**（013），**未估值的计划不被排名**（013），
**overrun 被记录而非吸收**（004），**summary export 不得声称 complete replay**（020）。

## Verification

### 构建

```
node scripts/build.mjs --clean
```

结果：`[build] build ok (generation 3)`，1510 items，86s。
`js-surface-manifest: OK — 177 registered surfaces`；
`js-module-linkage: OK — 826 emitted modules linked and loaded`。

### 门禁

```
npm run check
```

结果：`✅ Ran 1 script`，退出 0。`fsharp-control-pyramid` 的 34 项降到 4 项（Sphinx 旧文件的 8 项随删除消失，另有 6 处非 Sphinx 历史债务在本次顺带修掉）；新增的 34 个 V2 模块零 violation。

### 离线单测

```
WANXIANGSHU_PROVIDER_LANGUAGE=en node --test 'requirements/*/tests/*.test.mjs'
```

结果：`tests 3768 / pass 3682 / fail 13`。

9 项失败与未改动的基线树逐字节相同（用 `git stash` 比对确认）：
`context-compression-018`×1、`crash-reconciliation-018`×5、`requirement-grounding-008`×1、
`structured-workflow-005`×1、`structured-workflow-014`×1。这些与 Sphinx 无关，属于其它 owner 的既有债务。
另 4 项为测试间顺序/env 依赖（`feature-ablation-002`、`requirement-system-017`、
`verification-system-008`×2），单独运行均通过。

### 本次未运行的验证

- 真实 LLM 调用与真实 OpenCode Host 连接（需要授权与计费授权，未执行）。
- MCP stdio 的实际协议握手（SDK 集成点已就位，未做端到端连接测试）。
- 跨进程 CAS 并发注入测试（`IEventStore` 的原子性前提已在 integration map 记录为待确认，本实现以一个 batch 一个 envelope 规避，未注入双写竞争）。

## Math

启用的观测模型与保证范围：

| 模块 | 模型 | 保证范围 |
|---|---|---|
| `Plugins/Ordinal/Pairwise` | BTL `σ(θ_i - θ_j + β·o)`；三类 tie softmax(η/2, −η/2, κ) | log-space 稳定；`estimateKind = map-laplace` 明示；协方差为受约束 Hessian 逆，`Var(θ_i−θ_j)=Σ_ii+Σ_jj−2Σ_ij` |
| `Plugins/Ordinal/Ranking` | MaxDiff 联合 best-worst 似然；Plackett–Luce；Borda 仅作描述性基线 | MaxDiff 二元组必异；rank 展开保留 ballot cluster |
| `Plugins/Bayes/Exact` | log-space 归一化；按 observationId 去重；同 dependency group 需显式 joint 声明否则记丢弃 | prior-only 明示非新增信息；零先验保持零 |
| `Plugins/AStar/Refiner` | 非负确定性图；reopen；全局下界 = incumbent（OPEN 空）或 frontier 最小 f | 启发可采纳性由调用方断言，Runtime 不代为声称 |
| `Plugins/Mcts/Refiner` | 样本统计，按 model+horizon+history 分 key | 仅经验均值/方差；无 coverage 保证；未访问节点先于均值比较 |

未建立的桥接：全局 expected-utility bridge 未实现，因此数值 Bellman/期望效用展开不激活；直接计划比较照常工作（WHAT-030 / 提案 10.7）。

`thetaL2 = 1.0` 等初始数值默认进入 config hash 并记入 `ObservationModel`，不作为校准结论。

## Data

- 旧事件全部保留：未删除、未自动转换为新语义、未执行目录清空。
- `sphinx/v2-transition@1` 是唯一接受的新 Sphinx 事件类型；旧的 `sphinx/*` 类型退出生产 program，仍可由原版本工具离线读取。
- 新程序以 `Bind`/`Integrator` 显式拒绝把旧 inquiry 当作 v2 恢复：`InquiryCreated` 是唯一可创建状态的事件，其它事件命中 `missing-inquiry`；不返回空状态。

## Remaining

- 真实 Host/provider 连接与计费：`Hosts/OpenCode/Adapter` 的 `ReadResult`/`Reconcile` 返回 `Ok None`（无证据时如实回答"没有"），尚未接真实 message 读取与 SSE 游标；需要授权环境下的 contract 测试（提案 WP-11/WP-12 的剩余部分）。
- 跨进程原子写入故障注入未做。
- 9 项既有失败见 Verification，非本次引入。
- `Plugins/Inquiry/Observe.fromJudgment` 已能将判断转为 typed 边，但 `decisionnow` 成稿路径的 renderer 工作尚未接真实 provider 调用。
- 元层 KG 近似与有界元层（提案 WP-14）以 `maxPureSteps` 与 `RefinementPending` 的形式落地，未做 KG 近似展开。

## 未声明项

按提案 28.4：未声明更高最终答案质量、token 节省、位置偏差消除、模型真实信念揭示、异步混合收敛证明。本报告只声称已实现的能力、可重复的测试结果与已知限制。

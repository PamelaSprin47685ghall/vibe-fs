# CUT-40：域内适配子目录棘轮

返回 [看板](../BOARD.md) / [终局交账 CUT-38 §7.6 F38-02](CUT-38.md) / [范围对账](../COVERAGE.md)。

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（2026-10-21，eng-cut40-goose；施工记录见 §6）
- **所属泳道**：宿主（CUT-34 的边界后继）
- **前置**：`CUT-34`（门禁 ⑦ host-sdk-boundary、⑧ adapter-import-ratchet，基线已到 0）、`CUT-38`（F38-02 登记本卡）均为 DONE。
- **起因**：门禁 ⑧ 只统计顶层 `Wanxiangshu.OpenCode`。域内的适配子目录（`Interaction/Dispatch/OpenCode/`、`Execution/Delegation/Fork/OpenCode/`、`Repository/Programming/Js/OpenCode/` 等 13 个）各自声明 `Wanxiangshu.<域>.OpenCode` 命名空间，内核、组合根与 Surface 文件 `open` 它们时没有任何门禁计数，宿主依赖可以在域内悄悄回流。

## 2. 棘轮口径（门禁 ⑨ `domain-adapter-import-ratchet`）

- **计什么**：`src/Wanxiangshu` 下的 `.fs` 文件，只要有一行非注释代码 `open` 或限定引用 `Wanxiangshu.<域…>.OpenCode`（域非空，即不含顶层 `Wanxiangshu.OpenCode`，后者归门禁 ⑧）。
- **豁免区**：适配区本身，也就是顶层 `OpenCode/`、`Host/`，以及任意 `…/OpenCode/` 目录。适配器之间互相引用不计。
- **比门禁 ⑧ 更严**：⑧ 豁免 `Composition/` 与 `*Surface.fs`；⑨ 不豁免这两类，所以 CUT-38 记下的 8 个内核文件（含两个 `Composition/` 文件）和 8 个 Surface 都计入。
- **只降不升**：基线是 `scripts/checks/domain-adapter-import-baseline.json`。不在基线里的文件新出现引用即失败；基线里的文件不再引用而条目未删也失败。规则与 ⑧ 相同：只许删条目，不许加。
- **起点如实登记**：门禁的首个提交 `d1180b3e4` 在 master `b4d09a3f7` 上（源码与 `7e2635c25`、`c31e4cd2b` 的门禁口径相同）记下 **16** 个文件，与 CUT-38 F38-02 的“8 内核 + 8 Surface”逐一吻合。

起点 16 个文件：

| 类别 | 文件 | 引用的域内适配符号 |
|---|---|---|
| 内核 | `Interaction/Repair/InteractionRepair.fs` | `HostSessionNudge` |
| 内核 | `Mission/Manager/Workflow.fs` | `HostSessionNudge` |
| 内核 | `Participant/Provider/Attempt/Fallback/Workflow.fs` | `HostSessionNudge` |
| 内核 | `Interaction/Dispatch/Send.fs` | 无（`open` 已无用） |
| 内核 | `Interaction/Dispatch/Ingress.fs` | `PromptIngressCodec.DecodedMessage`（`ChatAdmissionIntent.DecodedMessage` 的别名） |
| 内核 | `Interaction/Dispatch/Dispatcher.fs` | `PromptMetadataCodec.create` |
| 组合根 | `Composition/Turn/OrdinaryTurnWorkflow.fs` | `HostJoinGuard`（`Dispatch.OpenCode` 的 `open` 无用） |
| 组合根 | `Composition/Durable/AgentJournalPortAdapter.fs` | `HostJoinGuardJournalPort` |
| Surface | `Interaction/Dispatch/JoinGuardSurface.fs` | `HostJoinGuard` |
| Surface | `Repository/Programming/Js/GeneratorSurface.fs` | `JsDescriptionAssets.load` |
| Surface | `Repository/Programming/Js/WorkflowSurface.fs` | `JsToolWorkflow`、`JsToolsResult`、`JsParkedSites` |
| Surface | `Repository/Knowledge/Casebook/BookkeeperSurface.fs` | `JsBookkeeperTool` |
| Surface | `Interaction/Dispatch/DispatchSurface.fs` | `PromptIngressCodec.decodeWith` |
| Surface | `Mission/Work/ProjectionSurface.fs` | `RelayNarrativeTransform`、`RelayProjectionDisposition` |
| Surface | `Execution/Delegation/SyncDelegate/Surface.fs` | `SyncDelegateBatching` |
| Surface | `Execution/Delegation/Fork/CleanBreakSurface.fs` | `JoinSurface.renderBatch` |

## 3. 逐文件判定（A 移入本域 / B 抽库 / C 保留为宿主适配）

判定办法：对每个被引用的模块，查它是否真的碰宿主，也就是是否引用顶层 `Wanxiangshu.OpenCode` 的任何模块或类型（`ProviderWireDecode`、`HostToolContext`、`ToolHostCodec`、`ToolSpec`、`HostToolFactory`、`ToolAdmission`、`PluginSessionScope`…），以及是否解码 OpenCode 线格式。不碰宿主的放错了目录，移回本域（A）；碰宿主的留在适配目录（C）。本轮没有纯到值得进 wanxiangli 的部分：被移动的模块都依赖 `AgentJournal`、`ISessionHostPort`、`ProviderProse` 等主仓端口或资源，因此没有 B。

| 被引用模块（原位置） | 判定 | 依据 |
|---|---|---|
| `HostSessionNudge`（`Interaction/Dispatch/OpenCode/SessionNudge`） | **A → `Interaction/Dispatch/SessionNudge`** | 只用宿主中立端口 `ISessionHostPort`/`IDispatchSessionPort`、`AgentJournal` 与 `PromptDispatcher`。`open Wanxiangshu.OpenCode` 没有实际使用，编译证实去掉后无误 |
| `PromptMetadataCodec`（`…/Dispatch/OpenCode/MetadataCodec`） | **A → `Interaction/Dispatch/MetadataCodec`** | 字段是本仓自定义的 `wanxiangshu_*` 关联元数据（PROMPT-011），不是 OpenCode 的格式。全文件只依赖 `Foundation.Identity` |
| `HostJoinGuard`、`HostJoinGuardJournalPort`（`Execution/Delegation/Fork/OpenCode/`） | **A → `Execution/Delegation/Fork/`** | JoinGuard 只经 `HostSessionNudge` 与 durable 端口发续行；端口记录只用 `Foundation.Identity`。两者都不碰宿主 |
| `PromptIngressCodec.DecodedMessage` | **A（就地改写）** | 它只是 `ChatAdmissionIntent.DecodedMessage` 的别名，`Ingress.fs` 改用本名即可 |
| `JsDescriptionAssets`（`Repository/Programming/Js/OpenCode/ToolHost.fs` 内） | **A → 新文件 `Repository/Programming/Js/DescriptionAssets.fs`** | 只读 `ProviderProse`/`ProviderResources` 双语散文。宿主绑定 `JsToolSpec` 留在 `ToolHost.fs`，改为消费它 |
| `JsToolWorkflow`、`JsToolsResult`、`JsToolsData`、交付见证类型（`…/Js/OpenCode/ToolWorkflow`）与 `JsParkedSites`（`…/Js/OpenCode/ParkedSites`） | **A → `Repository/Programming/Js/`** | 两个文件都没有 `open Wanxiangshu.OpenCode`，`PluginSessionScope` 只出现在注释里，属于放错目录 |
| `PromptIngressCodec.decodeWith`（`DispatchSurface` 用） | **C，RETAINED** | 它用 JS 反射解码 OpenCode `chat.message` 钩子的原始载荷，是宿主线格式编解码器 |
| `JsBookkeeperTool`（`BookkeeperSurface` 用） | **C，RETAINED** | OpenCode 工具绑定：`HostToolFactory`、`ToolAdmission`、`ToolSpec`、`ToolHostCodec` |
| `RelayNarrativeTransform`（`ProjectionSurface` 用） | **C，RETAINED** | 对宿主消息做 transform：`ProviderWireDecode`（6 处）、`HostMessageProjection` |
| `SyncDelegateBatching`（`SyncDelegate/Surface` 用） | **C，RETAINED** | `applyReplacedResults` 改写 OpenCode 消息的 tool part `state.output`；`resolve` 吃 `HostToolContext`/`ToolHostCodec`。延迟检查的暂存与它共享同一张进程表 |
| `JoinSurface.renderBatch`（`CleanBreakSurface` 用） | **C，RETAINED（暂留，见 §5 F40-01）** | `JoinSurface` 本身是适配目录里的测试面，被 delegation-005/013/014/015 与 effect-accounting-007 按 `dist/…/Fork/OpenCode/JoinSurface.js` 导入，走的是 join 工具结果的线格式渲染 |

剩下 5 个 Surface 都是 JS 测试边界：它们引用适配器，是为了让测试打到真实的宿主编解码与工具绑定。如果把它们挪进 `…/OpenCode/` 目录来“清零”，宿主依赖并没有减少，只是换了门禁看不到的位置，所以不这么做。

## 4. 结果：16 → 5

| 提交（master） | 内容 | 门禁 ⑨ |
|---|---|---|
| `d1180b3e4` | `scripts/checks/architecture.mjs` 增加门禁 ⑨ `domain-adapter-import-ratchet`，新增基线 `domain-adapter-import-baseline.json`，如实记下起点 16 个文件 | 16 |
| `2e0da3f1f` | `SessionNudge`、`MetadataCodec` 移到 `Interaction/Dispatch/`；`JoinGuard`、`HostJoinGuardJournalPort` 移到 `Execution/Delegation/Fork/`，命名空间改为本域。`Ingress` 改用 `ChatAdmissionIntent.DecodedMessage`；`Send`、`OrdinaryTurnWorkflow` 删去无用的 `open`；`IngressCodec` 改为 `open` 本域取 `PromptMetadataCodec`。同步 4 个 shard、`compile-order.txt`、`APPLIES-TO`、`fatal-inventory` 路径和 `owner-impact-corpus` 路径。适配区内若干文件随之删去已无用的域内 `open` | 7（8 个内核文件全部清零，另减 `JoinGuardSurface`） |
| `827c68dba` | `JsDescriptionAssets` 从 `Js/OpenCode/ToolHost.fs` 拆到新文件 `Repository/Programming/Js/DescriptionAssets.fs(i)`，放在 js-toolbindings shard。repository-programming-043 的源码断言改读新文件，structured-workflow-014 的编译清单加入新文件 | 6 |
| `3add5700a` | `ToolWorkflow`、`ParkedSites` 移到 `Repository/Programming/Js/`。测试导入路径（repository-programming-031/039/041/064）、`test-surface-scan`、`raw-time-scan` 同步改为新路径 | 5 |

**内核（含 `Composition/`）：8 → 0。Surface：8 → 5。** 剩下 5 个都是 §3 判定的 C（RETAINED）：`DispatchSurface`、`BookkeeperSurface`、`ProjectionSurface`、`SyncDelegate/Surface`、`CleanBreakSurface`。

门禁 ⑧（顶层 `Wanxiangshu.OpenCode`）基线仍为 0。被移出适配目录的 7 个文件（6 个原文件加拆出的 `DescriptionAssets`）没有一个引用顶层适配层，否则 ⑧ 会在推送前把它拦下。

## 5. 后继项

| ID | 内容 | 为什么本卡不做 |
|---|---|---|
| F40-01 | `Fork/OpenCode/JoinResultRenderer` 与 `JoinSurface` 文本上没有使用顶层 `Wanxiangshu.OpenCode` 的任何模块（`open` 疑似无用）。如果属实，join 线格式渲染也是放错目录，`CleanBreakSurface` 可以清零 | `JoinSurface.js` 被 6 个测试按路径导入，`JoinResultRenderer` 列在 `provider-leak-gate` 与 `raw-time-scan` 的路径表里。要先编译证实那个 `open` 无用，再整组迁移，超出本轮的单次落地范围 |
| F40-02 | `Context/Prefix/OpenCode/Wire.fs` 放在适配目录里，命名空间却是 `Wanxiangshu.Context.Prefix`（`XWire`），所以门禁 ⑨ 的命名空间口径看不到 `XWireSurface` 对它的引用 | 要么改名空间（`XWireSurface` 随之计入基线），要么判定它不是适配器、移出 `OpenCode/` 目录。这需要先对 `XWire` 是否碰宿主做判定（它 `open Wanxiangshu.OpenCode`）。登记在此，不顺手改 |
| F40-03 | 门禁 ⑨ 只扫 `.fs`，与 ⑧ 一致。`.fsi` 的 `open` 随实现文件一起改动；本卡涉及的 `.fsi` 都已同步 | 如果要把 `.fsi` 也纳入，⑧ 和 ⑨ 应该一起改，属于门禁 owner 的事 |

## 6. 验证（README §9：同输入、同环境的 base 与候选）

- **编译**：落地循环 `wt/logs-cut40/land.sh`（沿用 eng-cut34 的循环；heavy.lock 只包编译，测试不持锁，测试加 `timeout`）。在 master `c31e4cd2b` 和 `b4d09a3f7`（中间进来的是仓主的 fantomas 批次）上各做一次 `node scripts/build.mjs --clean`，都通过：`js-module-linkage` 714 个模块，比 base 多 1 个，就是新文件 `DescriptionAssets`；`js-surface-manifest` 通过。
- **定向套件**：12 个包，共 1592 个用例：dispatch-protocol、interaction-authority、delegation、repository-programming、structured-workflow、execution-failure-policy、host-boundary、work、provider-attempt-recovery、capability-enforcement、time-capability、effect-accounting。两轮结果相同：1465 通过、33 跳过、77 todo。有失败的 11 个文件在候选上逐个单独重跑，再与 base 工作区 `wt/CUT-40-base`（`c31e4cd2b`，已构建）上同一文件的单独重跑对比，**没有超出 base 的失败**。这些失败都是既有问题，base 上单独重跑同样失败：delegation-006/026/031/032、effect-accounting-002、dispatch-protocol-003、host-boundary-023、structured-workflow-005、interaction-authority-021、capability-enforcement-017、provider-attempt-recovery-007。证据见 `wt/logs-cut40/land.out`、`land2.out`、`round2-b4d09a3f7.tap`。
- **`node scripts/check.mjs`**：base `c31e4cd2b` 与候选都是 exit 1。唯一红的门禁是 `fsharp-control-pyramid`，两边都是 122 条；把移动过的路径换算回去以后，逐条完全相同（`wt/logs-cut40/check-{base,cand}.out`）。属于既有问题（F38-04）。
- **格式**：本卡改动的 48 个 `.fs/.fsi` 跑 `fantomas --check` 全部通过。第一次落地前查出 `ToolRegistrySurface.fs` 因限定名变短需要重排，已在落地前并入 `827c68dba`。
- **`node scripts/checks/architecture.mjs`**：每次推送前都通过，包括 ⑧ 和 ⑨。
- **必须能红的反例**：在任意内核文件里加一行 `open Wanxiangshu.Interaction.Dispatch.OpenCode`，⑨ 报 `newly references an in-domain *.OpenCode adapter namespace`；在基线里留一个已清零的文件，⑨ 报 `drop it from …baseline`。两条都在施工中实际触发过：第一条出现在写基线之前首次加门禁时（16 条“newly references”）；第二条出现在每刀之后、删基线条目之前。不是只靠推断。

**回退**：每个提交只移动文件、改命名空间或删除无用的 `open`，没有改持久格式，也没有改行为。可以单独 `git revert`；revert 某个移动提交时，要连同它在基线里删掉的条目一起恢复，否则 ⑨ 会报“新引用”。

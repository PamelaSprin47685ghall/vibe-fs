# Wave 0 Architecture Migration Decisions & Rationale

本文件记录 Wave 0 施工账本机械对齐基准、差额来源分析、区域映射规则、分类继承规则及热点 Proof Freeze 决策。

## 1. 对齐基准 (Alignment Baseline)

- **全集基准**：以文件系统实际 `src/Wanxiangshu/**/*.fs` 与 `src/Wanxiangshu/*.fs` 确凿存在的 **673** 个 F# 源码文件为全仓单一物理事实基准。
- **语义所有权基准**：`scripts/checks/semantic-owners.json` 已将全部 673 个文件映射到 44 个 Primary Semantic Owner，且已通过 `checks/semantic-owners.mjs` CI 门禁。
- **派生字段修正**：
  - `scripts/checks/semantic-owners.json` 的 `total` 字段从过时的 654 修正为真实条目数 **673**（所有权映射数据零改动）。
  - `scripts/checks/migration-ledger.json` 的 `total` 字段从过时的 650 修正为真实条目数 **673**。

## 2. 差额来源分析 (Discrepancy Analysis)

本次对账发现 `migration-ledger.json` 原有 650 条，较 673 全集缺失 **23** 个生产文件条目，经逐一比对排查，差额来源为近期功能迭代（包括 InstitutionalLearning、Delegation Handoff、Attention/Concern 交互机制及对应工具接入）未同步登记至旧施工账本：

1. **Enforcer / InstitutionalLearning 模块 (5 项)**:
   - `Enforcer/InstitutionalLearning/Enhancer.fs` (owner: `institutional-learning`)
   - `Enforcer/InstitutionalLearning/Facts.fs` (owner: `institutional-learning`)
   - `Enforcer/InstitutionalLearning/Fold.fs` (owner: `institutional-learning`)
   - `Enforcer/InstitutionalLearning/Projection.fs` (owner: `institutional-learning`)
   - `Enforcer/InstitutionalLearning/Surface.fs` (owner: `institutional-learning`)
2. **Execution / Delegation 模块 (7 项)**:
   - `Execution/Delegation/Handoff.fs` (owner: `delegation`)
   - `Execution/Delegation/HandoffIdentity.fs` (owner: `delegation`)
   - `Execution/Delegation/HandoffLedger.fs` (owner: `delegation`)
   - `Execution/Delegation/HandoffSurface.fs` (owner: `delegation`)
   - `Execution/Delegation/Fork/OpenCode/ToolSurface.fs` (owner: `delegation`)
   - `Execution/Delegation/Handle/OpenCode/JoinWake.fs` (owner: `delegation`)
   - `Execution/Delegation/SyncDelegate/OpenCode/Observation.fs` (owner: `delegation`)
3. **Interaction / Attention 模块 (4 项)**:
   - `Interaction/Attention/Facts.fs` (owner: `attention-regulation`)
   - `Interaction/Attention/Fold.fs` (owner: `attention-regulation`)
   - `Interaction/Attention/Projection.fs` (owner: `attention-regulation`)
   - `Interaction/Attention/Surface.fs` (owner: `attention-regulation`)
4. **Interaction / Concern 模块 (4 项)**:
   - `Interaction/Concern/Facts.fs` (owner: `concern-routing`)
   - `Interaction/Concern/Fold.fs` (owner: `concern-routing`)
   - `Interaction/Concern/Projection.fs` (owner: `concern-routing`)
   - `Interaction/Concern/Surface.fs` (owner: `concern-routing`)
5. **OpenCode / Tools 工具层接入 (3 项)**:
   - `OpenCode/Tools/AttentionTools.fs` (owner: `attention-regulation`)
   - `OpenCode/Tools/ConcernTools.fs` (owner: `concern-routing`)
   - `OpenCode/Tools/InstitutionalLearningTools.fs` (owner: `institutional-learning`)

经核对，账本中不存在任何指向已删除或不存在文件的 stale 遗留条目（stale = 0）。

## 3. 区域与 Wave 机械映射规则 (Directory to Wave Mapping)

按架构重构路线图要求执行严格的物理区域至波次映射：

| 顶层物理目录 | 分配 Wave | 语义领域 |
|---|---|---|
| `OpenCode/**` | **Wave 1** | OpenCode Composition Shell / Tools / Host Signals |
| `Interaction/**` | **Wave 2** | Interaction Authority / Dispatch / Repair |
| `Mission/**` | **Wave 3** | Mission Manager / Finality / Review / Obligation / WorkRecord |
| `Execution/**`, `Composition/**` | **Wave 4** | Execution Lifecycle / Delegation / Fission / Turn Orchestration |
| `Context/**`, `Strength/**`, `Enforcer/**`, `Participant/**` | **Wave 5** | Context Trace / Strength / Enforcer / Participant Persona |
| `Persistence/**`, `Change/**`, `Git/**` | **Wave 6** | Persistence EventStore / Journal / Change Integration / Git |
| 其余一级目录 (`Process/**`, `Repository/**`, `Sphinx/**`, `Foundation/**`, `Resources/**`, `Host/**`, `Requirement/**`, `Verification/**`, 顶层文件 `AssemblyInfo.fs`) | **Wave 7** | Platform Primitives / Knowledge Reuse / Platform Adapters |

## 4. Classification 继承规则与非常规裁决 (Classification Rationale)

- **继承原则**：新增条目的 classification 严格继承同目录、同 owner 现有条目的主导分类；若同目录无同 owner 条目，则继承同父级目录/同子系统同类构件的主导分类。
- **裁决明细**：
  1. `Enforcer/InstitutionalLearning/*` (5个文件)：同属于 Enforcer 子系统，Enforcer 全量 23 个既有文件 100% 裁决为 `KEEP`。故 InstitutionalLearning 5 个文件继承主导分类 `KEEP`。
  2. `Execution/Delegation/*` (7个文件)：同属于 Delegation 体系，Delegation 既有 59 个文件中 58 个（>98%）为 `KEEP`。故 7 个新增文件继承主导分类 `KEEP`。
  3. `Interaction/Attention/*` 与 `Interaction/Concern/*` (8个文件)：同属于 Interaction 事实/投影层，Interaction 既有 30 个文件中 29 个（>96%）为 `KEEP`。故 8 个新增文件继承主导分类 `KEEP`。
  4. `OpenCode/Tools/{AttentionTools,ConcernTools,InstitutionalLearningTools}.fs` (3个文件)：`OpenCode/Tools/` 下工具实现文件（如 `FileMutationTools.fs`、`FileTools.fs`、`CoderTool.fs` 等）均为 `KEEP`。故 3 个新增工具文件继承主导分类 `KEEP`。
- **UNKNOWN 计数**：全仓 23 个补齐条目全部具有明确的主导分类继承，**UNKNOWN = 0**。

## 5. 热点 Proof Freeze 映射 (Hotspot Pinning Proofs)

为 7 个关键核心热点文件显式落位 `pinningProofs` 字段，引用物理磁盘上真实存在的静态门禁与规范级行为测试：

1. **`OpenCode/Plugin/PluginTransforms.fs`**:
   - `scripts/checks/plugin-transforms-invariant.mjs` (L0 静态乐谱与调用顺序门禁)
   - `requirements/host-boundary/tests/ordered-transform.test.mjs` (L1/L2 转换步骤时序测试)
2. **`OpenCode/Host/HostSignalBootstrap.fs`**:
   - `scripts/checks/composition-root-invariant.mjs` (L0 Composition Root 纯接线门禁)
   - `requirements/host-boundary/tests/plugin-load-purity.test.mjs` (L3 插件加载纯洁性测试)
   - `requirements/managed-session-lifecycle/tests/shutdown-drain-contract.test.mjs` (L2 关闭排空契约测试)
3. **`OpenCode/Tools/ToolRegistry.fs`**:
   - `scripts/checks/composition-root-invariant.mjs` (L0 Composition Root 门禁)
   - `scripts/checks/capability-isomorphism-gate.mjs` (L0 权限同构静态门禁)
   - `scripts/checks/tool-referential-integrity.mjs` (L0 工具引用完整性门禁)
   - `requirements/capability-enforcement/tests/capability-isomorphism-gate.test.mjs` (L1 权限同构测试)
   - `requirements/capability-enforcement/tests/tool-referential-integrity.test.mjs` (L1 引用完整性测试)
4. **`OpenCode/Host/ModelCapacity.fs`**:
   - `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` (L1 路由与容量权威边界测试)
   - `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` (L2 模型路由运行时租约与容量测试)
5. **`Persistence/EventStore/CanonicalIntegrator.fs`**:
   - `requirements/durable-events/tests/canonical-integrator.test.mjs` (L1 规范积分器单信封与重放测试)
   - `requirements/durable-events/tests/event-store-journal-boot.test.mjs` (L2 启动重放与切尾测试)
   - `requirements/durable-convergence/tests/event-store-converge.test.mjs` (L3 多流合并与收敛测试)

## 6. 最终统计 (Final Ledger Statistics)

- **条目总数 (Total Entries)**: 673
- **各 Status 计数**:
  - `PENDING`: 673
  - `CUTOVER`: 0
  - `DELETED`: 0
  - `PROVEN-KEEP`: 0
- **各 Classification 计数**:
  - `KEEP`: 615
  - `ADAPTER`: 47
  - `COMPOSITION-ROOT`: 10
  - `MOVE`: 1 (`Interaction/Dispatch/OpenCode/AssistanceHost.fs`)
  - `SPLIT`: 0
  - `DELETE`: 0
  - `UNKNOWN`: 0
- **各 Wave 计数**:
  - `Wave 1` (OpenCode): 129
  - `Wave 2` (Interaction): 38
  - `Wave 3` (Mission): 75
  - `Wave 4` (Execution + Composition): 116 (Execution: 99, Composition: 17)
  - `Wave 5` (Context + Strength + Enforcer + Participant): 135 (Context: 51, Strength: 25, Enforcer: 28, Participant: 31)
  - `Wave 6` (Persistence + Change + Git): 59 (Persistence: 33, Change: 18, Git: 8)
  - `Wave 7` (Platform & Remainder): 121 (Repository: 46, Sphinx: 21, Process: 19, Foundation: 15, Resources: 9, Host: 4, Requirement: 3, Verification: 2, AssemblyInfo.fs: 1)

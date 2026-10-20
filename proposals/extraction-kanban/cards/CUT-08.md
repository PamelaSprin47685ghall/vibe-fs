# CUT-08　能力事实与双侧门禁

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-08)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut08c-goose 接手 eng-cut07b-goose，2026-10-21）。范围按 §6 收窄：能力事实（矩阵、Manager 门禁、能力↔工具名映射、实用工具准入）只剩库一个 owner；未完成的部分已写明归属，不算本卡完成。
- **前置**：CUT-07 已 DONE（master 587cd7b3c）。
- **边界**：工具实际执行仍属各能力 owner；库内不启动进程、不读仓库。

## 2. 分段

| 段 | 内容 | 状态 |
|---|---|---|
| 1 | 角色 × 能力矩阵、未知/历史角色失败关闭、Manager 精确事实门禁 | 已合入（§3） |
| 2 | 能力 ↔ 宿主工具名唯一映射；schema 投影、派发映射、执行门禁读同一份 | 已合入（§4） |
| 3 | schema / 描述 / 示例 / 运行门禁一致 | schema 投影、请求工具表、执行准入已读同一份（§4、§5）；描述/示例的语义评审见 §6 |
| 4 | 删除每角色手写的同义权威表 | 已合入：§4.2 删除 StaticTools 中 7 行冗余覆写；§5 删除 `cognitiveUtilityRoleAllowed` 与 Fission 角色守卫；余项按 §6 移交 |

## 3. 第 1 段：矩阵与 Manager 门禁（2026-10-21）

### 3.1 库侧

- wanxiangli `735de19` 新增 `src/capability`（`OfficeMatrix.fs`、`Capability.fs`、`index.d.ts`、`tests/001.test.mjs`），`71958aa` 发布 **0.18.0**。
- 库内是唯一的角色 × 能力矩阵：`permissions(role)` 与 `isAllowed(role, permission)` 读同一份集合。历史角色、内部槽位、大小写错误、非字符串一律空集；未知能力标签为 false。
- `permissionsForManagerFacts(facts)` 只读精确布尔事实（字段必须是 `true` 本身）。判定顺序：无在任者 → 空；有确认无评审 → 空；清理阻塞 → Join+Finality；最终在任者 → 收尾面；已评审 → 收回 Read/Glob/Grep；否则完整 Manager 集合。
- 库全套 582/582 pass（`node --test --test-force-exit`）；capability 4 个测试含正反例与畸形事实反例。

### 3.2 主仓接线

- `Foundation/OfficeCapability.fs`：`ToolPermission` / `ManagerCapabilityFacts` 类型与标签编解码保留，`.fsi` 未改。矩阵、`managerReviewReadOnlyPermissions`、`managerFinishPermissions`、`permissions`、`isAllowed`、`permissionsForManagerFacts` 改为对库结果的类型化投影（私有接口 `CapabilityLibrary`，只用 Import/Emit，符合 Foundation 纯核心约束）。库返回未知标签时直接抛错，不吞掉。
- `Participant/Persona/OfficeCapabilitySurface.fs`：删除手写 `officeName` 表，改用 `Roles.roleLabel`；历史角色空集由库回答，不再在 surface 里二次枚举。
- gitlink `wanxiangli` → `71958aa`（0.18.0），lockfile 同步。

### 3.3 旧符号去向

| 旧实现 | 去向 |
|---|---|
| `OfficeCapability.permissions` 的每角色 match 矩阵 | 删除，由库 `permissions` 回答 |
| `managerReviewReadOnlyPermissions` / `managerFinishPermissions` 字面集合 | 删除，由库同名函数回答 |
| `isAllowed` 的 `Roles.all` 活跃过滤 | 删除，库对非活跃角色返回空集，等价 |
| `permissionsForManagerFacts` 判定链 | 删除，由库回答；宿主只把 `CleanupBlockerDigest.IsSome` 映射为 `hasCleanupBlocker` |
| `OfficeCapabilitySurface.officeName`、历史角色二次枚举 | 删除 |
| 保留在宿主 | `ToolPermission` DU、标签编解码、`ManagerCapabilityFacts` 记录（宿主事实来源） |

只有一个可写 owner，没有同步器。

### 3.4 验收（§9）

证据见 [evidence/CUT-08](../evidence/CUT-08/README.md)。

- base（master 587cd7b3c 的源码，gitlink c9a943d）与 candidate 的 7 个相关套件计数相同，均 0 fail / 0 cancelled。
- 差分 3950 次双算，0 处不同。
- check.mjs 无新增发现；shard-edge-audit 253 → 252。
- 净改进：能力矩阵只剩一个 owner（库），宿主少约 60 行判定逻辑，行为不变，权限未扩大。

## 4. 第 2 段：能力 ↔ 工具名唯一映射（2026-10-21）

### 4.1 库侧

- wanxiangli `8cadb12`：`ToolSurface.toolNames`、`permissionOfToolName`、`admitsToolForRole`，以及 `tests/002.test.mjs`（SPEC[capability-004]）。
- `274d9c0`（测试修复）：eventstore-001 竞态用例改为等两个 worker 都退出后再重开库。原因：worker 回复后关闭连接，最后一次关闭会做 WAL checkpoint；`busy_timeout=0` 下父进程提前重开会报 `database is locked`。这台负载机上修复前 3 次失败 2 次，修复后 8/8 通过；断言没有改。
- `b6b05e2` 发布 **0.20.0**，库全套 596/596 pass。

### 4.2 主仓接线

- `Foundation/OfficeCapability.fs(.fsi)` 新增 `toolNames`、`permissionOfToolName`、`admitsToolForRole`，都是对库的类型化投影。
- `OpenCode/Tools/StaticTools.fs`：删除手写的 `toolNames` match（24 行）和 `allPermissions` 构造子列表，三处函数改为委托给库，`.fsi` 未改。
- `permissionFor` 删除 7 行冗余的每角色覆写：Plan 的 review/deliver/todowrite-for-plan allow；Manager 的 fission/commission deny；Orchestrator 的 resume deny；DevOps 的 fork/resume deny。这些结果矩阵和反查本来就给出相同答案，差分已证明逐项一致。
- 保留 2 行矩阵表达不了的宿主注册例外：Orchestrator 的 `fork` deny（Fork 职责在编程面上），以及 `commission` allow（宿主动词，没有对应的办公能力）。
- gitlink → `b6b05e2`（0.20.0），lockfile 同步。0.19.0 是 CUT-19 的 events/retention 增量，随 gitlink 一并进入；durable-convergence 18/13 pass、durable-events 252/233 pass，均 0 fail。

### 4.3 验收

- 差分 [differential-tools.mjs](../evidence/CUT-08/differential-tools.mjs)：11 角色 × 33 名字，覆盖 toolNames、反查、准入、`permissionObj`、`requestToolMap`。结果 `compared 784; differing 0`；第 1 段差分在新 candidate 上仍为 `3950 / 0`。
- 套件计数与 base 相同，已有失败的用例名 diff 为空（repository-programming 8、structured-workflow 1、intra-participant-parallelism 3、work 9，都是 base 已有）。
- check.mjs：本卡文件无新增发现。新出现的 2 条 `Ablation/Manifest.fs` match-pyramid 来自 master 上他人的 `3e7f7e302`，不在本卡 diff 内。

## 5. 第 4 段：实用工具准入与 Fission 冗余守卫（2026-10-21，eng-cut08c-goose）

### 5.1 反例（base 上实测）

base 的 `assume` 有两份答案：`StaticTools.permissionObj` 和 `AssumeTool.admission` 读 `cognitiveUtilityRoleAllowed`（`role <> Distiller`），而 `requestToolMap` 自己写死 `name = "skill" || name = "assume"`。结果 Distiller 在请求工具表里 `assume=true`，在权限对象和执行准入里却是 deny/false，属于"看得见但调不了"。差分日志里的 base 值可以直接看到这一点。

### 5.2 库侧

- wanxiangli `0aa7b39`：`ToolSurface.utilityToolNames`（`skill`、`assume`）和 `admitsUtility`，`OfficeMatrix.roles`（现行六个角色），以及 `tests/003.test.mjs`（SPEC[capability-005]）。实用工具不是办公能力，不进矩阵，也不进名字→能力反查。现行角色都持有；矩阵外的角色标签（历史角色、已退役的 distiller、大小写错误、非字符串）一律失败关闭。
- `06e6535` 发布 **0.22.0**，库全套 614/614 pass。

### 5.3 主仓接线

- `OfficeCapability.fs(.fsi)` 新增 `utilityToolNames` / `admitsUtility` 投影。
- `StaticTools`：删除 `cognitiveUtilityRoleAllowed`（fs 与 fsi）；`permissionFor` 中 skill/assume 三行合成一行，读库；`requestToolMap` 不再写死名字，读同一个 `admitsUtility`。
- `AssumeTool.roleAllowed` 改为 `OfficeCapability.admitsUtility role "assume"`。
- `Execution/Fission/OpenCode/Tool.fs`：准入删掉 `r = Role.Engineer &&`，只读矩阵（矩阵里只有 engineer 持有 Fission，差分证明逐角色相同）。
- gitlink → `06e6535`（0.22.0，含他人 0.21.0 compression 增量，主仓尚未消费它），lockfile 同步。

### 5.4 行为变化（已核实只收窄，不扩权）

[differential-utility.mjs](../evidence/CUT-08/differential-utility.mjs) 在 11 个角色上双算 `permissionObj`、`requestToolMap`、`AssumeTool.roleAllowed/admission` 和 Fission 准入：`compared 55; differing 18`。18 处全部在已退役角色（coder、inspector、browser、inquiry、distiller），27 个字段全是 allow→deny 或 true→false。六个现行角色 0 差异，Fission 准入 11 个角色 0 差异。

这些退役角色在 base 上本来就跑不起来：`ManagedAgentConfig` 不给它们 agent 配置，`PromptResources` 不给 provider prompt，`ModelRouting.isDeprecatedRole` 拒绝它们的容量预约。所以它们的工具面从"无人使用的 allow"变为 deny，等于把 SPEC[capability-002] 的失败关闭延伸到实用工具，同时消除了 §5.1 的分叉。第 2 段差分（differential-tools.mjs）在新 candidate 上是 `784 / 10`，10 处是同样这 5 个退役角色的 `permissionObj` / `requestToolMap`，原因相同；第 1 段差分仍为 `3950 / 0`。

## 6. 收窄范围与移交（DONE 的边界）

本卡完成的是：角色 × 能力矩阵、Manager 事实门禁、能力↔宿主工具名映射、实用工具（skill/assume）准入，这四份事实在主仓里不再有手写副本，schema 投影（`permissionObj`）、请求工具表（`requestToolMap`）和执行准入（`ToolAdmission`）都从库读同一份答案。以下几项不在本卡完成范围内，如实列出：

| 余项 | 为什么不在本卡 | 去向 |
|---|---|---|
| action-affordance 001–013（描述、示例的语义一致） | 都是 GAP-078 的人工语义评审 TODO，要审描述文本，不是代码抽取 | 保持 GAP-078，不关闭 |
| `StaticTools.jsPermission` / `hasFsCapability`（todowrite-for-* 编程面准入）、`ToolRegistry` 的 JS 生成器、`Js/OpenCode/ToolHost.fs` 的编程面准入 | 属于受限 JS 编程面 | CUT-25（eng-cut25b-goose 持有） |
| `ForkTool.managerAdmission` / `resumeAdmission`（Manager‖Plan）/ `orchestratorAdmission` | fork/resume/commission 已不注册为宿主工具，测试 harness 直接调 Execute，不经准入；`resumeAdmission` 放行 Plan 来自 9fcc15824 的接力式规划协议，是否收回属于委派产品语义 | CUT-13 处理委派 spec 时一并裁决 |
| `permissionFor` 中 Orchestrator 的 `fork` deny 与 `commission` allow | 宿主注册事实（Fork 职责在编程面、commission 是宿主动词），矩阵表达不了，明确保留在宿主 | 保留 |
| `ToolRegistry` 中 Plan 内层 `deliver` spec 的 `role = Plan` | 只经外层路由调用，外层准入已读矩阵（Finality ‖ Deliver） | 随 CUT-13/23 的交付路由处理 |

旧 owner 退出情况：`OfficeCapability` 的矩阵和判定链、`OfficeCapabilitySurface.officeName`、`StaticTools.toolNames` 的手写 match、7 行冗余覆写、`cognitiveUtilityRoleAllowed`、`requestToolMap` 写死的实用工具名、Fission 的 `r = Role.Engineer` 守卫，全部已删除。

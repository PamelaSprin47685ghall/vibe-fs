# CUT-26：需求发现与材料溯源

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-26) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`READY`（由 `CUT-00` 完成准备核对后转入，待认领）
- **所属泳道 / 波次**：仓库 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **关联护栏（D-05 & 产品语义边界）**：
  - 按 [CARDS.md](../CARDS.md#cut-26) 与 [COVERAGE.md](../COVERAGE.md)：**本卡负责工作区 `requirements/<package>/` 目录发现、`APPLIES-TO` 顺序规则匹配、包快照内容哈希（`GroundingSnapshot` / `GroundingIdentity`）以及无副作用的接地投影状态机（`RequirementGroundingProjection`）**；
  - 实际 OpenCode 宿主拦截（`Gate.fs`）、Provider 消息注入转换（`Transform.fs`）与 AgentJournal 持久化接线（`Runtime.fs`）作为主仓外层组合层（C）保留；
  - 主仓 `requirements/*/WHAT.md` 仍是当前产品的正式语义真源，不随本卡迁移变成通用规则数据库，也不按目录搬家关闭任何 `GAP.md` 证明债务（`requirement-system-011/017/018`）。
- **零阻塞结论**：`src/Wanxiangshu/Requirement/Grounding/{Model,Catalog,Surface}.fs` 与 `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/{Projection,Port}.fs` 已位于独立的纯 Contract `.fsproj`（`Wanxiangshu.Owner.requirement-grounding.requirement-grounding-model.fsproj` 与 `Wanxiangshu.Owner.requirement-grounding.requirement-grounding-projection.fsproj`），仅依赖 `GlobMatch` 与 SHA-256 `Digest`，与 `CUT-02`/`CUT-03`/`CUT-04` 零冲突，可零阻塞独立抽出。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明中提及 `src/Wanxiangshu/Repository/Requirements/`。在主仓当前基线（`b322c180b`）中，该子系统的真实物理路径为 `src/Wanxiangshu/Requirement/Grounding/` 与 `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/`，精确映射如下：

| 委托说明 / 草稿概念 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 去向判定 |
|---|---|---|---|
| `Repository/Requirements`：快照模型与内容哈希身份 | `src/Wanxiangshu/Requirement/Grounding/Model.fs(.fsi)`<br>`src/Wanxiangshu/Host/Facts.fs(.fsi)` | `Wanxiangshu.Owner.requirement-grounding.requirement-grounding-model.fsproj` | **抽出核心（A1）**：`GroundingMaterial`、`GroundingSnapshot`、`GroundingReadCoverage`、`RequirementGroundingOccurrence`、`GroundingIdentity` |
| `Repository/Requirements`：工作区包发现、`APPLIES-TO` 匹配与物化 | `src/Wanxiangshu/Requirement/Grounding/Catalog.fs(.fsi)`<br>`src/Wanxiangshu/Requirement/Grounding/Surface.fs(.fsi)` | `Wanxiangshu.Owner.requirement-grounding.requirement-grounding-model.fsproj` | **抽出核心（A2）**：`GroundingCatalog.canonicalWorkspace / workspaceRelativePath / discover / resolve / materialize / snapshotsForPaths` |
| 接地状态折叠与重锚（Reanchor）纯投影 | `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Projection.fs(.fsi)`<br>`src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Port.fs(.fsi)` | `Wanxiangshu.Owner.requirement-grounding.requirement-grounding-projection.fsproj` | **抽出核心（B）**：`RequirementGroundingProjectionState`、`RequirementGroundingFoldRejection`、`isSnapshotGrounded / applyRequested / applyMaterialObserved / applyReadObserved / applyAnchored / applyReanchor` |
| 宿主持久化事件适配 | `src/Wanxiangshu/Host/Fact.fs(.fsi)`<br>`src/Wanxiangshu/Composition/Durable/RequirementGroundingJournalAdapter.fs(.fsi)` | `Wanxiangshu.Owner.requirement-grounding.host-fact.fsproj`<br>`Wanxiangshu.Owner.durable-composition.durable-adapter-requirement-grounding.fsproj` | **留在主仓持久组合层（C1）**：桥接 AgentJournal 事件 |
| 宿主写工具门禁、Provider 注入与运行时 | `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/{Runtime,Gate,Transform}.fs(.fsi)`<br>`src/Wanxiangshu/OpenCode/Host/{RequirementGroundingRepositorySurface,RequirementGroundingSurface}.fs(.fsi)` | `Wanxiangshu.Owner.requirement-grounding.opencode-host-requirementgrounding-runtime.fsproj` | **留在主仓宿主层（C2）**：拦截写工具、观测完整 `read`、向 Provider 消息流注入合成 `read` 对 |

## 3. 目标

- **用户/维护者当前损失**：工作区需求目录发现、`APPLIES-TO` 顺序覆盖规则（含 `!` 排除与后规则覆盖前规则）、规范文档枚举（排除 `tests/`、`README.md`、`APPLIES-TO`）以及基于文件字节的 `Digest` 接地判定是通用仓库溯源能力，目前尚未从主仓抽离为独立可复用的基础组件。
- **这次完成后可少维护什么**：在基础库冻结唯一的 `GroundingCatalog`、`GroundingIdentity` 与 `RequirementGroundingProjection` 纯状态机；主仓 `OpenCode/Host/RequirementGrounding/{Runtime,Gate,Transform}.fs` 仅负责把工具调用与消息流桥接到该组件。
- **明确不做**：
  - 不把主仓 `requirements/*/WHAT.md` 的正文迁入基础库；
  - 不让包外文件（如 `AGENTS.md`、`proposals/**`、`tests/**`）冒充需求包规范材料（`requirement-grounding-001/002`）；
  - 不允许部分读取（带 `offset`/`limit` 截断的 `PartialFile`）算作已完成需求包或源码文件的完整接地（`requirement-grounding-005/008`）。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/requirement-grounding/WHAT.md`：
    - `[001]` 发现有效需求包（`requirements/<package>/` 下含 `APPLIES-TO` 且至少含 `WHY.md` 或 `WHAT.md` 之一）；
    - `[002]` 包内规范材料确定性物化（`WHY.md` → `WHAT.md` → 按 ASCII 升序的其余顶层及子目录 `.md`，严格排除 `APPLIES-TO`、`README.md` 与 `tests/**`）；
    - `[003]` `APPLIES-TO` 顺序规则匹配（忽略空行与 `#` 注释，支持 `!` 取反，后出现的匹配规则覆盖先出现的规则；可匹配多包，按包名升序返回）；
    - `[004]` 内容身份 `Digest` 由规范路径与各文件精确字节计算（任一文件字节变化即产生新 `Digest`，旧证明自动失效）；
    - `[005]`—`[012]` 写前必须完成目标文件完整读取与匹配需求包接地、同一版本在当前可见前缀内不重复注入、重锚（Reanchor）后重置可见窗口但保留单调序号、重放保持原锚点字节稳定。
  - `requirements/requirement-system/WHAT.md`：`[001]`、`[004]`—`[008]`、`[011]`、`[017]`、`[018]` 需求包结构、测试映射与 GAP 台账真实性门禁。

## 5. 自治个体（A / B / C）

- **A（`GroundingCatalog` & `GroundingIdentity`，抽入新库）**：
  - 负责：
    1. 工作区路径规范化（`canonicalWorkspace` / `workspaceRelativePath`，解析符号链接并拒绝越界路径或指向工作区外的链接）；
    2. 扫描 `requirements/` 发现合法包并解析 `APPLIES-TO` 规则列表（`discover`）；
    3. 给定目标相对路径，按顺序评估每个包的 `APPLIES-TO` 规则，返回命中的包描述符列表（`resolve`）；
    4. 按确定性顺序读取包内规范 `.md` 文件，生成每份材料的 `ResultBytes`（与 `read` 工具输出格式一致的带行号文本）并计算包级 `Digest`（`materialize` / `snapshotsForPaths`）。
  - 拥有状态：无状态（每次对给定工作区与路径按磁盘当前内容确定性计算）。
- **B（`RequirementGroundingProjection` 纯折叠状态机，抽入新库）**：
  - 负责：维护 `Pending`、`OccurrencesRev`、`VisibleMaterials`、`ObservedReads` 与 `VisibleFromOrdinal`；处理 `applyRequested`、`applyMaterialObserved`、`applyReadObserved`、`applyAnchored` 与 `applyReanchor`。
  - 永远成立的不变量：
    - 只有某 `GroundingSnapshot` 的全部 `Materials` 均在当前 `VisibleMaterials`（即 `VisibleFromOrdinal` 之后已锚定或已完整读取）中时，`isSnapshotGrounded` 才为 `true`；
    - `applyReadObserved` 仅接受 `Coverage = CompleteFile` 的读取作为有效文件观测，`PartialFile` 绝不入账；
    - `applyAnchored` 严格校验序号连续递增（`NonSequentialOrdinal`）、拒绝重复身份（`DuplicateIdentity`）、拒绝未请求先锚定（`MissingRequest`）；
    - `applyReanchor` 清空 `VisibleMaterials` 并将 `VisibleFromOrdinal` 推进至 `nextOrdinal`，使压缩/重锚后尚未在新窗口见过的材料重新受门禁保护，同时已持久化的历史 `RequirementGroundingOccurrence` 原字节保持不可变。
- **C（主仓 `OpenCode/Host/RequirementGrounding/{Runtime,Gate,Transform}.fs` 与 `PluginTransforms.fs`）**：
  - 负责：在 `edit`/`write`/`patch`/`js-engineer` 写动作前调用 A + B 校验目标文件读取与需求包接地；未满足时阻断或排队 `Requested` 快照，并在下一轮 Provider 请求转换中注入合成 `read` 调用与原字节结果。

## 6. 公开消息与最小完整契约

- **目录发现与匹配（A）**：
  - `canonicalWorkspace : workspace: string -> string`
  - `workspaceRelativePath : workspace: string -> path: string -> string option`
  - `discover : workspace: string -> PackageDescriptor list`
  - `resolve : workspace: string -> path: string -> PackageDescriptor list`
  - `materialize : workspace: string -> packageName: string -> GroundingSnapshot`
  - `snapshotsForPaths : workspace: string -> paths: string list -> GroundingSnapshot list`
- **纯投影折叠（B）**：
  - `empty : RequirementGroundingProjectionState`
  - `isSnapshotGrounded : GroundingSnapshot -> RequirementGroundingProjectionState -> bool`
  - `applyRequested : GroundingSnapshot -> RequirementGroundingProjectionState -> RequirementGroundingProjectionState`
  - `applyMaterialObserved : RequirementGroundingMaterialObserved -> RequirementGroundingProjectionState -> RequirementGroundingProjectionState`
  - `applyReadObserved : RequirementGroundingReadObserved -> RequirementGroundingProjectionState -> RequirementGroundingProjectionState`
  - `applyAnchored : RequirementGroundingOccurrence -> RequirementGroundingProjectionState -> Result<RequirementGroundingProjectionState, RequirementGroundingFoldRejection>`
  - `applyReanchor : RequirementGroundingProjectionState -> RequirementGroundingProjectionState`
- **拒绝结果及零副作用保证**：
  - 工作区外路径、指向工作区外的符号链接、不存在或缺少 `APPLIES-TO` 的目录均返回 `None` 或空列表；
  - 非法 `applyAnchored`（跳号、重复、无前置请求）返回 `Error RequirementGroundingFoldRejection`，原状态不变。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **RequirementGrounding 运行时、门禁与投影转换**：
   - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Runtime.fs`
   - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Gate.fs`
   - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Transform.fs`
   - `src/Wanxiangshu/OpenCode/Host/RequirementGroundingRepositorySurface.fs`
   - `src/Wanxiangshu/OpenCode/Host/RequirementGroundingSurface.fs`
2. **宿主插件与持久化接线**：
   - `src/Wanxiangshu/Host/Fact.fs`
   - `src/Wanxiangshu/Composition/Durable/{Fact,Fold,Projection,RequirementGroundingJournalAdapter}.fs`
   - `src/Wanxiangshu/OpenCode/Plugin/{PluginLoad,PluginTransforms,PluginToolGate}.fs`

### 7.2 对应测试套件

- `requirements/requirement-grounding/tests/001.test.mjs` 至 `012.test.mjs`（完整覆盖 `WHAT[REQUIREMENT-GROUNDING-001..012]`）
- `requirements/requirement-system/tests/001.test.mjs`、`004.test.mjs` 至 `008.test.mjs`、`011.test.mjs`、`017.test.mjs`、`018.test.mjs`

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（包外文件/测试文件冒充规范材料，或 `!` 否定规则失效）**：
   - 在 `requirements/foo/` 下放置 `README.md`、`APPLIES-TO`、`tests/001.test.mjs`，调用 `materialize`，返回材料的 `Path` 列表必须只含 `WHY.md` / `WHAT.md` 等规范文档，绝不能包含 `APPLIES-TO`、`README.md` 或 `tests/` 下文件（对应 `requirement-grounding-001/002`）；
   - 在 `APPLIES-TO` 中写 `src/Foo/**` 后跟 `!src/Foo/Internal/**`，对 `src/Foo/Internal/Bar.fs` 调用 `resolve` 必须不命中；若其后再追加一行 `src/Foo/Internal/Special.fs`，则 `Special.fs` 必须重新命中（对应 `requirement-grounding-003`）。
2. **反例 2（规范内容改动后沿用旧接地证明，或同版本重复注入）**：
   - 某包快照 `S1` 已完成锚定（`isSnapshotGrounded S1 = true`），随后修改该包 `WHAT.md` 的 1 个字符得到新快照 `S2`（`S2.Digest <> S1.Digest`），在新快照 `S2` 上调用 `isSnapshotGrounded` 必须立即返回 `false`（对应 `requirement-grounding-004/006`）；
   - 若 `S1` 内容未变且未发生 Reanchor，再次请求同一包时 `isSnapshotGrounded S1` 必须为 `true` 且 `applyRequested` 不将其重复加入 `Pending`。
3. **反例 3（部分读取 `PartialFile` 绕过门禁，或符号链接逃逸工作区）**：
   - 仅上报 `Coverage = PartialFile` 的 `RequirementGroundingReadObserved`，状态机 `ObservedReads` 必须忽略该记录，写操作继续被门禁拦截（对应 `requirement-grounding-005/008`）；
   - 通过指向工作区外部的符号链接路径请求 `workspaceRelativePath` / `resolve`，必须返回 `None` / 空列表。
4. **反例 4（乱序锚定、无请求锚定与 Reanchor 后历史字节篡改）**：
   - 未经 `applyRequested` 直接调用 `applyAnchored` 必须返回 `Error (MissingRequest _)`；传入非连续 `Ordinal` 必须返回 `Error (NonSequentialOrdinal _)`；
   - 执行 `applyReanchor` 后，之前已接地的快照在当前可见窗口内变回未接地（需重新呈现），但已记录在 `occurrences` 中的历史 `Reads` 字节保持原样不变（对应 `requirement-grounding-009/010/012`）。
5. **独立拆除测试**：在独立构建中仅编译 `Requirement.Grounding` 模型、目录与纯投影模块（不含 OpenCode 宿主、不含 AgentJournal），运行上述 4 组反例与正控测试全绿。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - 新增需求发现与材料溯源子包（实现 `GroundingModel`、`GroundingIdentity`、`GroundingCatalog`、`RequirementGroundingProjection`）及工程导出声明。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Requirement/Grounding/Model.fs(.fsi)`
  - `src/Wanxiangshu/Requirement/Grounding/Catalog.fs(.fsi)`
  - `src/Wanxiangshu/Requirement/Grounding/Surface.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Projection.fs(.fsi)`
  - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Port.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.requirement-grounding.requirement-grounding-model.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.requirement-grounding.requirement-grounding-projection.fsproj`
- **暂留主仓与相邻卡边界**：
  - `src/Wanxiangshu/OpenCode/Host/RequirementGrounding/{Runtime,Gate,Transform}.fs(.fsi)`、`RequirementGroundingRepositorySurface.fs(.fsi)`、`RequirementGroundingSurface.fs(.fsi)`、`src/Wanxiangshu/Host/Fact.fs(.fsi)` 及 `Composition/Durable/RequirementGroundingJournalAdapter.fs(.fsi)` 留在主仓宿主与组合层（与 `CUT-17`、`CUT-34`、`CUT-35` 协同）；
   - `requirements/**` 下的全部正式需求包留在主仓原位。

## 10. 实现记录（2026-10-15，eng-cut26）

- **A（目录发现/匹配/物化/Digest）已完成源码施工**：
  - `wanxiangli/src/grounding/{Wire,Catalog,Grounding}.fs` + `grounding.fsproj` + `index.d.ts` + `AGENTS.md`，包导出 `wanxiangli/grounding`（版本 0.7.0）；glob 内核与 `wanxiangli/fs`（CUT-24）共用同一 `Filesystem` wildmatch 实现，摘要公式与主仓 `HostDigest` 一致；算法整体承接主仓 `GroundingCatalog` 语义（WHAT.md/`APPLIES-TO`/包根 Markdown 物化/内容锚定 Digest），JS 边界为原生形状。
  - 库侧测试：`src/grounding/tests/{001,002,003}.test.mjs`（SPEC[grounding-001..003]：发现与规则解析、匹配语义、物化与 Digest、去重排序）。
  - 主仓切换：`Requirement/Grounding/Catalog.fs` 改为 `Import("Grounding", "wanxiangli/grounding")` 桥接层（同 CUT-24 GlobMatch 模式），`Catalog.fsi` 签名不变，`Surface.fs`/`Runtime.fs`/`Gate.fs` 调用方零改动；`requirement-grounding-model.fsproj` 移除已无消费者的 `foundation-globmatch` ProjectReference；`Model.fs` 的 `GroundingIdentity` 继续使用主仓 `HostDigest`（runtime-platform 原语，公式与库一致）。
  - 主仓回归证明：`requirements/requirement-grounding/tests/001..012.test.mjs`（经 Surface）不需修改，切换后应全绿。
- **B（`RequirementGroundingProjection` 投影状态机）暂留主仓，待裁决**：
  - 原因：`RequirementGroundingOccurrence` 绑定主仓 `ToolCallId`（`Foundation.Identity`）与 `TranscriptGap`（宿主词汇）；迁入 wanxiangli 需先把这批词汇降级为 JS 原生形状并在 `Port`/`Runtime`/`Gate` 重建 F# 类型链，牵动面超出本卡"源码级调用方切换"的最小完整改动。当前投影状态机仍是纯 F# 折叠、消费 `Model` 类型，无旧路径重复实现。
- **待 DevOps 验证（未运行，不得视为已验收）**：
  1. wanxiangli：`npm run build`（含 grounding 模块发现）与 `node --test src/grounding/tests/*.test.mjs`；
  2. `npm pack` 得 `wanxiangli-0.7.0.tgz`，放入主仓 `vendor/`，主仓 `npm ci`（主仓 `package.json` 已指向 `file:vendor/wanxiangli-0.7.0.tgz`）；
  3. 主仓：`node scripts/build.mjs` 与 `requirements/requirement-grounding`、`requirement-system` 测试套件。
- **遗留边界**：`Foundation/GlobMatch.fs` 桥接层在主仓已无生产消费者（最后一个调用方是旧 `Catalog.fs`），其退役/删除牵动 compile-order 与结构门禁，属跨卡决定，交 Manager 裁断。

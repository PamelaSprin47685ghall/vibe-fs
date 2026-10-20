# CUT-08　能力事实与双侧门禁

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-08)

## 1. 当前状态与前置核对

- **当前列状态**：`DOING`（eng-cut07b-goose 接手 eng-cut07-goose）。第 1 段（矩阵与拒绝行为、Manager 事实门禁）已合入，见 §3。
- **前置**：CUT-07 已 DONE（master 587cd7b3c）。
- **边界**：工具实际执行仍属各能力 owner；库内不启动进程、不读仓库。

## 2. 分段

| 段 | 内容 | 状态 |
|---|---|---|
| 1 | 角色 × 能力矩阵、未知/历史角色失败关闭、Manager 精确事实门禁 | 已合入（§3） |
| 2 | 原生 / JS 工具共同使用同一结果 | 见 §4 |
| 3 | schema / 描述 / 示例 / 运行门禁一致 | 见 §4 |
| 4 | 删除每角色手写的同义权威表 | 见 §4 |

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

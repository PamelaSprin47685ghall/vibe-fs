# Projection Algebra — 证明

行为：`what/projection.md`。边界：`shape/projection.md`。迁移：`how/projection.md`。

## 类型与管线

| 证明 | 条款 |
|------|------|
| Wire ≠ Semantic，禁止隐式互转 | PROJ-003、VERIFY-007 |
| 输入是不可变 ProjectionSnapshot（消费者驱动字段子集） | PROJ-002 |
| 功能只声明 Intent，不直接改 Message list | PROJ-005 |
| 合并有律或冲突，无注册顺序暗箱 | PROJ-006 |
| DSL 不负责生命周期 | PROJ-007 |

合并性质按 intent 种类证明：重放型验证幂等；有序追加型验证 canonical order；同一 intent 集的输入排列不得改变最终 projection 或冲突结局。不要求有序追加满足交换律。

## 实现路径

| 证明 | 条款 |
|------|------|
| 无 ProjectionProgram AST + Interpreter | PROJ-001、FLOW-001 |
| 三层 Coordinator / Planner / Renderer | PROJ-004 |
| 迁移顺序与 digest 对齐后删 Legacy | [PROJ-008](../how/projection.md#proj-008迁移顺序) |

## 迁移顺序证明矩阵（PROJ-008）

代表测试：

- `tests/unit/context/projection-algebra.test.mjs`（八 intent Domain 代数）
- `tests/unit/context/companion-projection.test.mjs`（Companion 高层契约）
- `tests/unit/context/blog-projection.test.mjs`（blog 投影契约）
- provider projection 相关 facade 测试

| 证明项 | 状态 |
|--------|------|
| 八 intent 定义（KeepPhysicalPrefix / ActivatePrefixEpoch / InsertBlogFrames / InsertRepair / SuppressTransportOnly / AppendReviewChallenge / InsertPairProgrammingThought / ReanchorAfterCompaction） | Domain + unit |
| Canonical Rank 0–6 | unit |
| plan 幂等 / 冲突（含 ConflictingPrefixLifecycle 等） | unit |
| `renderMessagesWithIntents` fold | unit |
| `InsertBlogFrames` ↔ `CompanionProjectionBuilder` digest 等价 | unit + 生产 rebuild 接线 |
| `InsertRepair` / `AppendReviewChallenge` / `InsertPairProgrammingThought` 生产字节合同（含空历史无门槛、永久 tool-call/tool-result pair 恢复、本次 pair 在 trailing user 前 / 多 tool 批末、重启字节等价） | 生产接线 + unit + restart |
| `ReanchorAfterCompaction` | 生产接线 + unit |
| **`SuppressTransportOnly`** | **仅 Domain + unit 骨架**；生产 `TransportMessages` 恒空、未声明 intent。COMPANION-012 字段级过滤由模型边界 / `toSemantic` 承担；消息级 Suppress 待 host-id 侧信道后续变更 |

Seal 用 Wire；剧本键用 Semantic（VERIFY-003/007）——混用必须红。

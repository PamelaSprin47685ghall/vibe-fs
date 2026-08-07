# FLOW — 证明

行为见 `what/flow.md`，边界见 `shape/flow.md`，实现见 `how/flow.md`。

## 静态

| 门 | 要求 |
|----|------|
| `scripts/checks/dsl-ownership.mjs` | `threshold=0`；禁止 business-interpreter / second-runtime-protocol |
| program-counter / behaviour-bool | 禁止（领域 evidence 名 allowlist 除外） |
| mutable | **声明式豁免**：Domain/Session/Application/Process/`Kernel/Parallel.fs` 的 `let mutable` 须带 `// DSL-MUTABLE: <category>` 声明；Agent/其余 Kernel fail-closed |
| infrastructure-leak | 仅登记的 Host 边界 basename 可 `open` Process 等 |

门禁必须先被故意破坏并变红，才算存在（VERIFY-004 精神）。

## 动态（可观察效果）

| 层 | 证明什么 | 落点 |
|----|----------|------|
| unit | workflow 经 fake ports 的调用轨迹与 Decision→效果 | `tests/unit/**` 中 orchestrator/recovery/join 程序契约 |
| guide-contract | DSL 程序入口可调用 + 元数 | `tests/unit/guide-contract.test.mjs` |
| 恢复 | Journal fold → 普通 workflow 入口，无「恢复协程指针」 | recovery / session-recovery unit |

## 完成定义

生产路径无业务 Interpreter、无 Command/Reply 总线、无 Step AST。  
恢复 = 事实重入普通 CE，不是回放 Program 节点。  
新增 Host 边界 `open` 必须先登记 basename 再写代码。

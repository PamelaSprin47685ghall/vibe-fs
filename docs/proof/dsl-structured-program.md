# DSL 结构化程序规则 — 证明

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`；实现见 `how/dsl-structured-program.md`。

## 静态门禁

| 门 | 要求 |
|---|---|
| `scripts/checks/dsl-ownership.mjs --threshold=0` | 语义状态机、组合状态、多布尔循环、大 DU 分类、重复 case 集 |
| `npm run lint` | format + spec + architecture + dsl-ownership + p0-recovery-join |
| 架构检查 `architecture.mjs` | 无旧路径、fsproj 完整、无 `.gen.fs`、Domain 不引用上层 |

## 动态证明

| 层 | 证明什么 | 落点 |
|---|---|---|
| 单元 | 新 CE 行为等价于旧状态机 | `tests/unit/execution/process-wait.test.mjs` 新增；`tests/unit/enforcer/blogger-runtime.test.mjs` 扩展；`tests/unit/context/session-recovery.test.mjs` 扩展；`tests/unit/context/companion-projection.test.mjs` 扩展 |
| 集成 | Journal fold / projection 不变 | `tests/integration/harness/cases.mjs` 中对应 case |
| Canary | Host 真实行为不变 | `tests/e2e/cases/process-stress.test.mjs`；`tests/e2e/cases/companion.test.mjs`；`tests/e2e/cases/blogger-quiet-stop.test.mjs`；`tests/e2e/cases/manager-companion.test.mjs` |
| 门禁自身 | 故意破坏门禁应变红 | 在 `tests/unit/verify/dsl-ownership.test.mjs` 增加改名 canary 与组合状态 fixture |

## 完成定义

- 生产路径无 `BloggerRuntimeState` 状态 DU。
- 生产路径无 `slotArmed` 控制流布尔。
- 生产路径无 `NodeProcessWait` 多布尔乘积。
- 生产路径无重复 `TurnOutcome` / `Role`。
- `AgentFact` 至少完成阶段 A 分 family 所有权。
- `dsl-ownership` 能识别语义改名与组合状态。
- `npm run check:release` 全绿。

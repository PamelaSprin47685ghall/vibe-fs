# Projection Algebra — 证明

行为：`what/projection.md`。边界：`shape/projection.md`。迁移：`how/projection.md`。

## 类型与管线

| 证明 | 条款 |
|------|------|
| Wire ≠ Semantic，禁止隐式互转 | PROJ-003、VERIFY-007 |
| 输入是不可变 ProjectionSnapshot | PROJ-002 |
| 功能只声明 Intent，不直接改 Message list | PROJ-005 |
| 合并有律或冲突，无注册顺序暗箱 | PROJ-006 |
| DSL 不负责生命周期 | PROJ-007 |

## 实现路径

| 证明 | 条款 |
|------|------|
| 无 ProjectionProgram AST + Interpreter | PROJ-001、FLOW-001 |
| 三层 Coordinator / Planner / Renderer | PROJ-004 |
| 迁移顺序与 digest 对齐后删 Legacy | PROJ-008 |

代表：`tests/unit/context/companion-projection.test.mjs`、provider projection 相关 facade 测试。  
Seal 用 Wire；剧本键用 Semantic（VERIFY-003/007）——混用必须红。

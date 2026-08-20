# requirement-system — HOW

## 架构与实现机制

`requirement-system` 作为元规范包，不包含领域业务 runtime 代码，其保证通过三个核心机制与自动化验证套件共同承载：

### 1. 结构与所有权验证器（`meta-verifier`）

`tests/meta-verifier.test.mjs` 是全树结构契约的机器执行入口，执行五项封闭性断言：
- **三文档与测试齐备**：索引中的每个包必须包含 `WHY.md`、`WHAT.md`、`HOW.md` 及 `tests/` 目录。
- **命题落点封闭**：每个 `WHAT.md` 标题中声明的 `<PACKAGE>-NNN` 命题，必须在 `HOW.md` 的落点表格中有对应的证明行。
- **测试文件物理存在**：落点表格中引用的测试文件必须在文件系统中真实存在。
- **目录无外部越界**：`requirements/` 目录下不存在 `INDEX.md` 之外的任何未授权目录。
- **依赖声明子集约束**：每个包文档中声明的 `DEPENDS ON` 集合必须是 `INDEX.md` 依赖骨架中定义边的子集。

### 2. 规范语法与引用门禁（`spec gate`）

`scripts/checks/spec.mjs` 与 `scripts/checks/spec-rules.mjs`（由 `tests/spec-rules.test.mjs` 提供全面回归保障）负责静态文本规则检查：
- 确保正式条款 ID 只能在所属包的 `WHAT.md` 中定义，禁止重定义与跨文件越权定义。
- 检查跨包条款引用的可解析性，防止悬空引用。
- 确保导航文件（如 README）仅作路由指向，禁止定义正式规范。
- 严禁代码和规范引用已废止的历史归档路径。

### 3. 双向证据追踪系统（`requirement-trace`）

`scripts/checks/requirement-trace.mjs` 与 `tests/requirement-trace.test.mjs` 实现规范与测试的双向映射：
- 扫描全量测试用例调用点（`test()` / `t.test()`），提取 `WHAT[<PACKAGE-NNN>]` 标签并验证其唯一性与合法性。
- 识别并阻断未关联命题的孤儿测试、多 primary 标签的歧义测试以及无有效测试覆盖的休眠命题。
- 解析 `HOW.md` 中声明的精确锚点，确保规范至测试代码的双向可追溯。

### 4. 变更生命周期约束（`change-lifecycle`）

`tests/change-lifecycle.test.mjs` 机械化验证变更文档边界，确保小型修复豁免规则、blocker 处理流程与历史已完成记录的只读性得到严格执行。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REQUIREMENT-SYSTEM-001 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-002 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-003 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-004 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-005 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-006 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-007 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-008 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-009 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-010 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-011 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-012 | `requirements/requirement-system/tests/spec-rules.test.mjs` |
| REQUIREMENT-SYSTEM-013 | `requirements/requirement-system/tests/change-lifecycle.test.mjs` |
| REQUIREMENT-SYSTEM-014 | `requirements/requirement-system/tests/change-lifecycle.test.mjs` |
| REQUIREMENT-SYSTEM-015 | `requirements/requirement-system/tests/change-lifecycle.test.mjs` |
| REQUIREMENT-SYSTEM-016 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-017 | `requirements/requirement-system/tests/meta-verifier.test.mjs` |
| REQUIREMENT-SYSTEM-018 | `requirements/requirement-system/tests/requirement-trace.test.mjs` |

# requirement-system — HOW

## 架构与实现机制

`requirement-system` 作为元规范包，不包含领域业务 runtime 代码，其保证通过三个核心机制与自动化验证套件共同承载：

### 1. 结构与所有权验证器（`meta-verifier`）

`tests/meta-verifier.test.mjs` 是全树结构契约的机器执行入口，执行五项封闭性断言：
- **三文档与测试齐备**：索引中的每个包必须包含 `WHY.md`、`WHAT.md`、`HOW.md` 及 `tests/` 目录。
- **缺口与错误分离**：没有 active test 或 HOW 证明边的命题记入 GAP，不作为测试失败；已声明证明仍必须完整有效。
- **测试文件物理存在**：落点表格中引用的测试文件必须在文件系统中真实存在；存在性只提供候选，不授予证明权威。
- **目录无外部越界**：`requirements/` 目录下不存在 `INDEX.md` 之外的任何未授权目录。
- **依赖声明子集约束**：每个包文档中声明的 `DEPENDS ON` 集合必须是 `INDEX.md` 依赖骨架中定义边的子集。

### 2. 规范语法与引用 helpers（`spec-rules` lib）

`scripts/lib/spec-rules.mjs`（由 `tests/spec-rules.test.mjs` 提供回归保障；条款重复定义 fail-closed 由 lib `duplicateClauseDefinitions` 覆盖，回归落点为 `tests/reliability-spec-mutations.test.mjs` 和 `tests/spec-rules.test.mjs`）负责静态文本规则检查：
- 确保正式条款 ID 只能在所属包的 `WHAT.md` 中定义，禁止重定义与跨文件越权定义。
- 检查跨包条款引用的可解析性，防止悬空引用。
- 确保导航文件（如 README）仅作路由指向，禁止定义正式规范。
- 严禁代码和规范引用已废止的历史归档路径。

### 4. 变更生命周期约束（`change-lifecycle`）

`tests/change-lifecycle.test.mjs` 机械化验证变更文档边界，确保小型修复豁免规则、blocker 处理流程与历史已完成记录的只读性得到严格执行。

---

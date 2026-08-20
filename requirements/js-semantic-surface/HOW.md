# js-semantic-surface — HOW

## 架构与实现机制

`js-semantic-surface` 通过自动化宪法测试、边界门禁扫描器、Surface 清单注册表与数据校验器共同保证实现与测试之间的强隔离：

### 1. 宪法验证套件（`surface-charter`）

`tests/surface-charter.test.mjs` 对全仓语义测试空间进行静态结构断言：
- **测试语言纯粹性**：扫描 `requirements/**/tests/` 目录，确保测试及辅助文件均为 `.mjs` / `.js`，零 F# 测试文件。
- **全测试区无污染扫描**：对所有测试、夹具、辅助工具及集成测试进行扫描，确保零 deep-dist 导入、零编译器运行时标记及零混淆符号名查找。
- **Helper 非直接测试保证**：扫描语义导入图，确保不拥有独立命题的内部 helper 不会成为独立的测试目标。
- **原生数据表示与隔离**：验证测试数据完全遵循 JS-native 原生对象与基本类型规范。

### 2. 静态边界门禁（`js-boundary-gate`）

`scripts/checks/js-boundary-gate.mjs` 实施全测试空间的静态门禁扫描，确保零历史债务并在发现违规时立即判红：
- 阻断测试代码对内部 `dist/<module>.js` 的直接导入。
- 阻断对对象属性进行混淆名模式匹配（如 `startsWith('Foo__')`）的反射探测。
- 阻断对 `.tag`、`.fields`、`.cases()` 等编译器私有特性的感知。

### 3. Surface 注册清单合同（`SURFACE_MANIFEST`）

`scripts/lib/test-surface-scan.mjs` 中的 `SURFACE_MANIFEST` 严格定义了系统中所有合法暴露的 Semantic Surface：
- 每个注册项必须明确声明其所属 `owner` 包、所证明的 `laws` 规范命题、源码路径 `source`、编译输出 `module` 以及表示形式 `representation`。
- `scripts/checks/js-surface-manifest.mjs` 机械化验证每一项注册均有对应的规范条款授权、源码存在且已编译、dist 产物存在，且至少被一个有效的契约测试实际消费。

### 4. 原生数据校验器（`js-contract`）

`requirements/verification-system/tests/support/js-contract.mjs` 提供运行时断言工具：
- `assertJsData(value)`：递归拒绝包含编译器私有标记或非标准类实例的数据对象，确保数据为标准 JSON 形状。
- `assertOpaque(value)`：确保不透明资源句柄仅能用于创建、传回与释放，拒绝读取其内部字段或原型链。

### 5. 编译产物隔离区（`compiler quarantine`）

仅限 `requirements/verification-system/tests/` 下专门验证编译器生成正确性的测试，才允许直接接触 `dist` 产物。任何产品包的 `tests/support` 或夹具均不得作为二级隔离区绕过边界规则。

---

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| JS-SEMANTIC-SURFACE-001 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |
| JS-SEMANTIC-SURFACE-002 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |
| JS-SEMANTIC-SURFACE-003 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |
| JS-SEMANTIC-SURFACE-004 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |
| JS-SEMANTIC-SURFACE-005 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |
| JS-SEMANTIC-SURFACE-006 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs` |

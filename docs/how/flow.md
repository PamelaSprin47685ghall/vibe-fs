# FLOW — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在复杂业务逻辑的开发中，开发者容易陷入“发明内部 AST 解释器、Command/Reply 总线或程序计数器”的陷阱（第二运行时反模式）。这增加了不必要的偶然复杂度、使代码难以被调试并丧失编译器类型审查的能力。FLOW 模块旨在确立以原生 F# 结构化程序（Computation Expression / `let!` / `match` / 尾递归）直接表达业务流的规范，在编译期硬性封杀任何内部解释器与控制流 AST。

### 2. 输入输出与规则边界
- **输入**：领域事实、能力接口（Capabilities）、Workflow 端口。
- **输出**：纯领域决策 `Decision` DU、直接执行的 Task、typed 副作用。
- **核心边界与不变量**：
  1. 直接 CE 执行（FLOW-001..008）：编排、恢复与 Join 统一使用 `task`/`let!`/`match`/`return!` 直接执行，严禁构造内部业务 Program AST。
  2. DSL 门禁阻断（`scripts/checks/dsl-ownership.mjs` threshold=0）：`second-runtime-protocol` 与 `business-interpreter` 硬性阻断，阈值恒定为 0。
  3. 纯决策与穷尽匹配：纯逻辑部分写为 `Evidence → Decision` 密封 DU，Application 层在强类型穷尽匹配后通过 Port 执行副作用。

---

## 直接 CE

1. 编排 / 恢复 / join：`task` / `let!` / `match` / `return!`，不经业务 AST。  
2. 领域副作用只经具名 capability（awaitManager、readTargetHead、publish…）。  
3. 纯决策：`Evidence → Decision` 密封 DU；Application 穷尽匹配后执行端口。

---

## 测试姿态

1. fake ports 记录调用轨迹与事实，不测「解释器节点指针」。  
2. 可观察效果：Journal 事实、端口调用序、终态。  
3. `dsl-ownership`：`threshold=0`；禁止 second-runtime / business-interpreter。

---

## 禁止回归

`Kernel/Program.fs`、`TraceInterpreter.fs`、Command/Reply 总线、Step AST 不得回到生产路径。

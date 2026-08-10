# FLOW — 目标实现

## Implements

行为合同见 `what/flow.md`；本文件只描述直接 CE、语义 Vocabulary 组合与恢复重入的实现形态。  
Vocabulary / 压缩 / Decorator：[`DSL-013` / `DSL-014` / `DSL-015`](../what/dsl-structured-program.md)。

## Ownership

程序、端口和分层边界见 `shape/flow.md`。

---

## 直接 CE（首选）

1. 编排 / 恢复 / join：`task` / `let!` / `match` / `return!`，不经业务 AST。  
2. 领域副作用只经具名 capability（awaitManager、readTargetHead、publish…）。  
3. **主设计法**：typed evidence / capability → semantic vocabulary → CE bind / 有界递归 / 高阶组合 → effect。复杂时序先获得准确语义名（DSL-013），已被独立 proof 覆盖的机械时序可压缩进 Vocabulary（DSL-014）；改变 trace 集的装饰必须具名（DSL-015）。

---

## 可用形态（非唯一理想）

`Evidence → Decision` 密封 DU → Application 穷尽匹配 → 端口效果，仍可用于局部封闭判定。  
它是可用形式之一，**不是**唯一理想形态；不得把生产程序重新压成几十个 `Decision` case 来代替 Vocabulary。

---

## 测试姿态

1. fake ports 记录调用轨迹与事实，不测「解释器节点指针」。  
2. 可观察效果：Journal 事实、端口调用序、终态；Vocabulary 调用点以语义名 + 契约证明，不以内部机械步数为权威。  
3. `dsl-ownership`：`threshold=0`；禁止 second-runtime / business-interpreter。

---

## 禁止回归

`Kernel/Program.fs`、`TraceInterpreter.fs`、Command/Reply 总线、Step AST 不得回到生产路径。

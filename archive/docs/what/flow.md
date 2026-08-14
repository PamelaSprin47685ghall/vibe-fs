# 结构化程序 DSL — 行为合同

条款前缀：`FLOW-`。与 ARCH-001 同向；冲突时以 ARCH-001/002/003 为准。  
边界见 `shape/flow.md`；实现姿态见 `how/flow.md`；证明见 `proof/flow.md`。

Semantic Vocabulary、压缩与 Decorator 边界由 DSL 条款定义（[`DSL-013` / `DSL-014` / `DSL-015`](dsl-structured-program.md)）；本文件不另立 Vocabulary 的 `FLOW-` 条款。

## FLOW-001：流程由语言表达

业务流程必须直接使用 F# computation expression、`let!`、`do!`、`match`、`return!`、纯函数与有界递归。

F# 调用栈就是流程栈。禁止 `CurrentStage`、`NextAction`、`Running` 等程序计数器。

## FLOW-002：DSL 是可执行语法

领域 DSL 是 CE + 领域命名操作构成的源码表面，**直接执行**。  
禁止要求业务流程先构造内部 AST 再解释。

## FLOW-004：纯决策与效果分离

Domain 保持无副作用：`Facts → Projection`、`Input → Result`，以及按需的纯判定。  
Application 经 CE 直接执行效果。

**首选形态**（主设计法）：

```text
typed evidence / capability
→ semantic vocabulary（DSL-013；压缩见 DSL-014）
→ CE bind / recursion / higher-order composition
→ effect
```

`Evidence → Decision` → 穷尽 `match` → effect 是**一种可用形态**，适合局部封闭判定；**不是**唯一理想形态，也不得压过具名 Vocabulary 组合。规则组合子（`andThen`、`validateAll`）留在 Domain；流程 DSL 决定「做什么」，规则 DSL 决定「是否允许」。语义装饰边界见 DSL-015。

## FLOW-005：恢复重入普通流程

```text
Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口
```

不得恢复 Program 节点、continuation 或「执行到第几步」。

## FLOW-008：用可观察效果测试

流程正确性由可观察效果（事实、调用轨迹、端口交互）证明，不由「解释器走到了哪个 AST 节点」证明。

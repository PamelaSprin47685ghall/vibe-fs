# FLOW — 目标实现

## 直接 CE

1. 编排 / 恢复 / join：`task` / `let!` / `match` / `return!`，不经业务 AST。  
2. 领域副作用只经具名 capability（awaitManager、readTargetHead、publish…）。  
3. 纯决策：`Evidence → Decision` 密封 DU；Application 穷尽匹配后执行端口。

## 测试姿态

1. fake ports 记录调用轨迹与事实，不测「解释器节点指针」。  
2. 可观察效果：Journal 事实、端口调用序、终态。  
3. `dsl-ownership`：`threshold=0`；禁止 second-runtime / business-interpreter。

## 禁止回归

`Kernel/Program.fs`、`TraceInterpreter.fs`、Command/Reply 总线、Step AST 不得回到生产路径。

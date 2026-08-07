# DSL 结构化程序规则 — 目标实现

## Implements

- DSL-001
- DSL-002
- DSL-003
- DSL-004
- DSL-005
- DSL-006
- DSL-007

## Ownership

分层、模块和测试边界见 `shape/dsl-structured-program.md`。当前未对齐实现只见
[`changes/active/dsl-structured-program-gap.md`](../../changes/active/dsl-structured-program-gap.md)。

## Algorithm

业务流程直接执行：

```text
读取 evidence
→ 纯函数得到 Decision
→ match 穷尽分支
→ 通过 typed port 执行副作用
→ 以 return! 或有界递归继续
```

异步顺序由 `task {}`、`let!`、`do!`、`use!` 表达；资源清理留在拥有该资源的同一作用域。
等待使用真实 Task/TCS/进程信号，不能用可持久字段保存 continuation 或下一阶段。

### 线性流程

```fsharp
let run ports input =
    task {
        match decide (evidence input) with
        | Reject reason -> return Rejected reason
        | Proceed command ->
            let! result = ports.Execute command
            return Completed result
    }
```

### 有界重试

```fsharp
let rec runRound ports remaining input =
    task {
        if remaining = 0 then
            return Exhausted
        else
            match! ports.TryOnce input with
            | Completed result -> return Completed result
            | Retry next -> return! runRound ports (remaining - 1) next
            | Failed error -> return Failed error
    }
```

递归参数只能承载下一轮真实输入或有限预算，不能承载 `Stage`、`NextAction`、
`isRunning` 等程序位置。

### 崩溃恢复

```text
Journal fold → Evidence → Decision/Permit → 普通 workflow 入口
```

不得恢复 continuation、interpreter cursor 或 runtime stage。

## Failure handling

- 可预见失败在边界收敛为 typed result，并由调用者穷尽匹配。
- 取消、超时、自然完成是不同的物理结果，不互相伪装。
- 外部协议异常在 Infrastructure 边界关闭；业务层不解析错误散文决定流程。
- 恢复 evidence 不足时 fail closed，不猜测旧流程执行到哪一步。

## Determinism and constants

本主题不另设业务常量。有限预算和稳定排序若属于具体领域，由对应领域 Clause 唯一定义；
本文件只要求调用链显式接收并使用它们。

## Implementation mapping

- 直接程序：`src/Wanxiangshu/Session/`、`src/Wanxiangshu/Application/`
- 纯 evidence/decision/fold：`src/Wanxiangshu/Domain/`
- 进程等待：`src/Wanxiangshu/Process/`
- 静态所有权门禁：`scripts/checks/dsl-ownership.mjs`

路径是目标责任区，不是当前文件数量或完成状态快照。

## Review questions

1. 字段表示真实事物，还是程序下一步？
2. Decision 是否可由纯函数从 evidence 得到？
3. 等待是否对应真实信号并由单一作用域拥有？
4. 恢复是否重入普通 workflow，而非恢复执行位置？
5. mutable 是否只管理物理资源或局部算法 scratch？

证明义务和反例见 `proof/dsl-structured-program.md`。

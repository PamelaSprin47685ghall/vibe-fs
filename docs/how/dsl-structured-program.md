# DSL 结构化程序规则 — 目标实现

## Implements

- DSL-001
- DSL-002
- DSL-003
- DSL-004
- DSL-005
- DSL-006
- DSL-007

另见 shape 层所有权条款：DSL-008、DSL-009、DSL-010、DSL-011（定义见 [`../shape/dsl-structured-program.md`](../shape/dsl-structured-program.md)）。本文件不把上述 shape-only 条款伪造成 how 实现算法。

## Ownership

分层、模块和测试边界见 `shape/dsl-structured-program.md`。

## 等待语义分类

等待不是同一件事。按控制流所阻塞的对象分类，不得用统一方案误伤：

| 类别 | 定义 | 判定规则 |
|------|------|----------|
| A. 业务状态探测 | 控制流为获知业务事实而反复读取（snapshot / projection） | 必须有界（因果重读上限）；不得以墙钟退避推进 |
| B. 事件等待 | 控制流阻塞于真实物理信号（TCS / journal waiter / process signal） | 事件驱动，零轮询；允许注入可取消 timer 做 deadline |
| C. Deadline / watchdog | 距上次因果进展的静默时长判据（VERIFY-004） | 允许墙钟，但须集中、可取消、可注入测试 |
| D. 跨进程互斥等待 | 多进程竞争单一物理资源（publish lock） | 保持 cross-process 合同；另行裁决 |

落点：

- Reconciler 因果重读属 A 类：有界，≤3 次（HOST-004）；不得以墙钟退避推进。
- Executor 定向等待属 B 类：permit-gated、Journal-authoritative；TCS/Pulse 仅作唤醒。
  禁止 timer-driven re-probe：不得以 `timerTask → re-probe` 递归轮询等待就绪
  （family recovery readiness 必须提供真实事件 waiter，如 journal `awaitChangeFrom`
  或 permit pulse，而非每 ≤100ms 重新 `RequireFamilyRecovery`）。
- SSE 心跳与 reconnect 属 C 类 one-shot silence deadline / 传输退避，经 ITimerPort 注入（生产=nodeTimerPort，测试=virtualTimerPort）；cancel/dispose 后回调零触发。

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
- 静态所有权门禁：`scripts/checks/dsl-ownership.mjs`（全量扫描，见「所有权门禁与精确豁免」）

路径是目标责任区，不是当前文件数量或完成状态快照。

## 所有权门禁与精确豁免

`scripts/checks/dsl-ownership.mjs` 必须扫描全部生产 `src/Wanxiangshu/**/*.fs`，不得按目录整体豁免。
`Infrastructure/`、`Journal/`、`Process/` 与业务目录一视同仁受程序计数、未声明 mutable、
`state-product` 与 `ControlState` 结构门约束。以「目录在扫描范围之外」为由不报告违规即失效豁免。

豁免只允许对**具体类型**用结构化 annotation 表达物理/投影归属
（`DSL-state-combination: physical|domain` / `DSL-control-state-reason:`），禁止目录级或
文件级整体豁免。目录级豁免逃逸 → RED。

判据（review question 5 的机器下限）：长期字段/registry/DU 若主要回答「代码下一步跑哪里」
而非「世界发生了什么 / 哪个物理资源存在」，即程序计数器，须消除或标注物理归属；未标注即红。

## Registry 审计（隐式程序计数器）

多个长期 registry / Dictionary / HashSet 的 presence 若被同一 `HandleTurn` / `observe`
函数联合 match 决定下一步业务动作，即构成隐式程序计数器，等价于单 record 状态机。

每个长期 registry 必须证明只代表一个 physical lifetime / 投影，且不被联合用于阶段推进；
否则须消除或显式标注物理归属。两个已声明 registry 的 direct/try probe 被同一 `match`/`if`
联合且 effect branch 被选中时，`registry-joint-branch` 作为确定语法反例判红。其它联合
presence 只产出候选审计项；是否构成阶段推进由人工 proof 判定，不能把 registry 目录或
annotation 当作自动证明。

关闭条件：
- 门禁全量扫描 100% 生产 `.fs`，无目录级豁免。
- 目录级豁免逃逸与 direct registry joint-effect 语法反例有受控 fixture 证明仓库入口判红；
  其它多 registry 联合 presence 有候选审计与人工 proof。
- 全生产扫描暴露的既有 pattern 逐项分类为 physical 或 remediation，不得批量豁免冲绿。

## Review questions

1. 字段表示真实事物，还是程序下一步？
2. Decision 是否可由纯函数从 evidence 得到？
3. 等待是否对应真实信号并由单一作用域拥有？
4. 恢复是否重入普通 workflow，而非恢复执行位置？
5. mutable 是否只管理物理资源或局部算法 scratch？

证明义务和反例见 `proof/dsl-structured-program.md`。

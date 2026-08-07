# DSL 结构化程序活跃差距

## Target clauses

- DSL-002
- DSL-005
- DSL-007
- DSL-009

## Active physical gap

`BloggerRuntimeState` 的 `Idle | InFlight` 仍与物理 flight registry 双写。
目标是让物理 single-flight Task/registry 成为 busy 与 current request 的唯一来源，
删除持久于 runtime cell 的流程位置影子。

现有静态门禁尚不能可靠识别 record 内多个状态型字段形成的正交组合；该缺口只涉及
证明能力，不授权新增 DSL 分类或改变 DSL-005 的语义。

## Evidence / blocker

实现落点与验证入口见 `shape/dsl-structured-program.md` 和
`proof/dsl-structured-program.md`。当前没有外部 blocker。

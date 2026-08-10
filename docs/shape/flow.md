# FLOW — 边界

## FLOW-003：领域操作强类型具名

业务流程只能通过具名 capability 调用副作用，例如：

```text
awaitManager
readTargetHead
rebaseOnto
publish
readSnapshot
observeSnapshot
```

每操作一种结果类型。禁止泛化 `execute Command` 与大 Reply DU 吞掉不可能分支。

含复杂时序的领域承诺用具名 Semantic Vocabulary 表达（定义见 [`DSL-013`](../what/dsl-structured-program.md)；压缩与 Decorator 边界见 [`DSL-014`](../what/dsl-structured-program.md) / [`DSL-015`](../what/dsl-structured-program.md)）。Vocabulary 条款归属 DSL，本层不另立 `FLOW-` ID。

## FLOW-006：禁止第二运行时

下列形态禁止引入或保留：

```text
Program<'instruction,'result> = Pure | Suspend
Command / Reply 总线
Step continuation AST
把普通调用序列编码后再回放的解释器
```

合法：JSON/TOML/Host-wire 等**外部协议**边界上的解码（非业务 Interpreter）。

## FLOW-007：循环与并发有界

循环与扇出必须有界（与 ARCH-009 一致）。禁止无界重试环与无界 sibling 扇出作为业务默认。

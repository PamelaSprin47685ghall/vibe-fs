# debug-print-left — Main 中文版

## 现在该做什么

从 production paths 移除临时 print、dump、trace、breakpoint 与仅用于调查的 instrumentation。只晋升有持久 operational purpose 的 signal。Operations 拥有 production diagnostics；原调查者不是遗留 print 的 owner。

## 为什么重要

临时 debug 输出很容易泄露敏感信息、淹没真正 signal、改变性能，甚至被后来的脚本误当 contract。它最初没有这些承诺，却因为“没删”获得永久生命。

## 修复策略

对每个 artifact：删除，或定义它回答的 operational question，然后通过项目刻意的 diagnostic surface（稳定字段与 level）表达那个答案。

## 决策分支

- 若 signal 只服务已结束的调查，删除。
- 若未来 operators 确实需要它，按正式 observability surface 重新设计：事件语义、字段、level、sampling、sensitivity、consumer 与 retention 都要有 owner。
- 若该行已经是刻意的 diagnostic surface，保留；本规则针对遗留调查机制。

## 常见假修复

- 把 `print` 改成 `logger.debug`；没有 consumer contract 仍是 leftover。
- 加 environment flag，默认关但 production 仍带着隐患。
- 仅删敏感字段，保留无意义噪声。
- 认为日志“总比没有好”；无 owner 的 signal 会降低而不是提高可观测性。

## 验证

生产输出只剩有明确用途的 diagnostics；临时探针的 marker/text 不再出现。若晋升为正式 signal，测试其 schema/sensitivity 与关键 operational semantics。

## 完成条件

每个 production diagnostic 都是有意存在，而不是一次 bug hunt 忘记收走的脚印。
# debug-print-left — Main 中文版

## 现在该做什么
若 signal 只服务已结束的调查，删除。若未来 operators 确实需要它，按正式 observability surface 重新设计：事件语义、字段、level、sampling、sensitivity、consumer 与 retention 都要有 owner。

## 为什么这很重要
临时 debug 输出很容易泄露敏感信息、淹没真正 signal、改变性能，甚至被后来的脚本误当 contract。它最初没有这些承诺，却因为“没删”获得永久生命。

## 常见假修复
- 把 `print` 改成 `logger.debug`；没有 consumer contract 仍是 leftover。
- 加 environment flag，默认关但 production 仍带着隐患。
- 仅删敏感字段，保留无意义噪声。
- 认为日志“总比没有好”；无 owner 的 signal 会降低而不是提高可观测性。

## 验证
生产输出只剩有明确用途的 diagnostics；临时探针的 marker/text 不再出现。若晋升为正式 signal，测试其 schema/sensitivity 与关键 operational semantics。

## 完成条件
每个 production diagnostic 都是有意存在，而不是一次 bug hunt 忘记收走的脚印。

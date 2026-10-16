# debug-print-left — Lesson

## 评价

### 定义

Debug artifact left behind，是为一次调查临时制造的输出/探针，在问题回答完后未经“升级为正式诊断接口”的设计就进入 production。

### 支配原则

Diagnostics 是面向未来 operators 的接口；临时 print 没有这样的 contract。临时 print 的字段、volume、sensitivity、lifetime 都只服务当时那个人的问题。把它留下，相当于让一次私人调查偶然决定系统永久 observable surface。

### 何时触发

- `console.log/printf/dump` 仍在 production path；
- request body、token、large object 为 debug 被直接打印；
- temporary trace/file/probe 没有 durable consumer；
- breakpoint/dev flag 可被 shipped path 触发；
- “先留着以后排查方便”是唯一 owner。

### 不应触发

- structured log/metric/trace 有明确 operational decision、level、schema、sensitivity policy；
- test spy 不作为 production output；
- local-only debug tooling 无法进入 shipped artifact；
- 正式 diagnostics 即使最初源于一次事故，只要被有意设计与维护，就已不是 leftover。

### 与相邻规则区分

`status-announcement-noise` 管的是即使有意为之的常规进度噪音；`secret-in-code` 管的是嵌入的凭据。本规则是临时调试机制的意外残留。Tie-break：如果 artifact 是为回答一次调查而创建、没有 durable consumer，即使那行是 log call，这个案子也归本规则。

### 判定程序

问：今天谁消费这个 signal，它支持什么操作决策，字段/敏感度/volume contract 是什么？没有答案，它仍是调查垃圾。

### 例子

- positive：bug hunt 之后 `console.log` request bodies 仍留在生产路径，把私人调查输出 shipping 出去。
- near-miss：structured error log 有稳定字段、level 与 on-call consumer。
- counterexample：删除临时 print，或把 signal 晋升到项目刻意的 diagnostic surface。

## 建议

### 现在该做什么

从 production paths 移除临时 print、dump、trace、breakpoint 与仅用于调查的 instrumentation。只晋升有持久 operational purpose 的 signal。Operations 拥有 production diagnostics；原调查者不是遗留 print 的 owner。

### 为什么重要

临时 debug 输出很容易泄露敏感信息、淹没真正 signal、改变性能，甚至被后来的脚本误当 contract。它最初没有这些承诺，却因为“没删”获得永久生命。

### 修复策略

对每个 artifact：删除，或定义它回答的 operational question，然后通过项目刻意的 diagnostic surface（稳定字段与 level）表达那个答案。

### 决策分支

- 若 signal 只服务已结束的调查，删除。
- 若未来 operators 确实需要它，按正式 observability surface 重新设计：事件语义、字段、level、sampling、sensitivity、consumer 与 retention 都要有 owner。
- 若该行已经是刻意的 diagnostic surface，保留；本规则针对遗留调查机制。

### 常见假修复

- 把 `print` 改成 `logger.debug`；没有 consumer contract 仍是 leftover。
- 加 environment flag，默认关但 production 仍带着隐患。
- 仅删敏感字段，保留无意义噪声。
- 认为日志“总比没有好”；无 owner 的 signal 会降低而不是提高可观测性。

### 验证

生产输出只剩有明确用途的 diagnostics；临时探针的 marker/text 不再出现。若晋升为正式 signal，测试其 schema/sensitivity 与关键 operational semantics。

### 完成条件

每个 production diagnostic 都是有意存在，而不是一次 bug hunt 忘记收走的脚印。

### Nudge

Debug output 要么删掉，要么被正式“晋升”为 observability。最差状态是既没人拥有，又永远在生产里说话。

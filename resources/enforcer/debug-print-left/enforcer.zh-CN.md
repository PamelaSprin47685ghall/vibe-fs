# debug-print-left — Enforcer 中文版

## 定义

Debug artifact left behind，是为一次调查临时制造的输出/探针，在问题回答完后未经“升级为正式诊断接口”的设计就进入 production。

## 支配原则

Diagnostics 是面向未来 operators 的接口；临时 print 没有这样的 contract。临时 print 的字段、volume、sensitivity、lifetime 都只服务当时那个人的问题。把它留下，相当于让一次私人调查偶然决定系统永久 observable surface。

## 何时触发

- `console.log/printf/dump` 仍在 production path；
- request body、token、large object 为 debug 被直接打印；
- temporary trace/file/probe 没有 durable consumer；
- breakpoint/dev flag 可被 shipped path 触发；
- “先留着以后排查方便”是唯一 owner。

## 不应触发

- structured log/metric/trace 有明确 operational decision、level、schema、sensitivity policy；
- test spy 不作为 production output；
- local-only debug tooling 无法进入 shipped artifact；
- 正式 diagnostics 即使最初源于一次事故，只要被有意设计与维护，就已不是 leftover。

## 与相邻规则区分

`status-announcement-noise` 管的是即使有意为之的常规进度噪音；`secret-in-code` 管的是嵌入的凭据。本规则是临时调试机制的意外残留。Tie-break：如果 artifact 是为回答一次调查而创建、没有 durable consumer，即使那行是 log call，这个案子也归本规则。

## 判定程序

问：今天谁消费这个 signal，它支持什么操作决策，字段/敏感度/volume contract 是什么？没有答案，它仍是调查垃圾。

## 例子

- positive：bug hunt 之后 `console.log` request bodies 仍留在生产路径，把私人调查输出 shipping 出去。
- near-miss：structured error log 有稳定字段、level 与 on-call consumer。
- counterexample：删除临时 print，或把 signal 晋升到项目刻意的 diagnostic surface。

## Nudge

Debug output 要么删掉，要么被正式“晋升”为 observability。最差状态是既没人拥有，又永远在生产里说话。
# debug-print-left — Enforcer 中文版

## 定义
Debug artifact left behind，是为一次调查临时制造的输出/探针，在问题回答完后未经“升级为正式诊断接口”的设计就进入 production。

临时 print 的字段、volume、sensitivity、lifetime 都只服务当时那个人的问题。把它留下，相当于让一次私人调查偶然决定系统永久 observable surface。

## 何时触发
- `console.log/printf/dump` 仍在 production path；
- request body、token、large object 为 debug 被直接打印；
- temporary trace/file/probe 没有 durable consumer；
- breakpoint/dev flag 可被 shipped path 触发；
- “先留着以后排查方便”是唯一 owner。

## 不要误判
- structured log/metric/trace 有明确 operational decision、level、schema、sensitivity policy；
- test spy 不作为 production output；
- local-only debug tooling 无法进入 shipped artifact；
- 正式 diagnostics 即使最初源于一次事故，只要被有意设计与维护，就已不是 leftover。

## 刀口
问：**今天谁消费这个 signal，它支持什么操作决策，字段/敏感度/volume contract 是什么？** 没有答案，它仍是调查垃圾。

## 提醒
Debug output 要么删掉，要么被正式“晋升”为 observability。最差状态是既没人拥有，又永远在生产里说话。

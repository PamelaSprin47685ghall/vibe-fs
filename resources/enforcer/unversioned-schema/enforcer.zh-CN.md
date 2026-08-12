# unversioned-schema — Enforcer 中文版

## 定义
Durable/cross-version bytes 没有 schema identity 时，未来 reader 必须从 shape 猜“当初是哪种语言写出来的”。这不是缺一个数字字段的小问题，而是 temporal communication 没有 grammar identity。

Persistence 就是跨时间通信：旧 deployment 是 producer，未来 deployment 是 consumer。没有 version，compatibility 变成考古。

## 何时触发
- event/file/wire/cache 跨 deployment 存活，却不携 semantic schema version；
- reader 用 field presence/filename/长度猜历史版本；
- 同一 bytes shape 在新版本改变 meaning，却没有 identity；
- unknown future representation 被 best-effort 当 current schema parse。

## 不要误判
- value 生命周期严格不跨 process/deployment；
- 已有显式 schema identity 与 deterministic read/migrate/reject；
- deployment version 不等于 schema version，除非两者语义严格绑定；
- ephemeral debug dump 不作为 compatibility surface。

## 刀口
问：**未来代码拿到这份 bytes，在解释第一字段之前，凭什么知道它是哪套 semantics？** 若答案是“看起来像”，versioning 已缺席。

## 提醒
Durable bytes 是写给未来代码的消息。消息必须自带语言身份，不能要求未来 reader 猜作者当时说哪种方言。

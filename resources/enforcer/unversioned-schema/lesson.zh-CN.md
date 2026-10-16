# unversioned-schema — Lesson

## 评价

### 定义

Durable/cross-version bytes 没有 schema identity 时，未来 reader 必须从 shape 猜“当初是哪种语言写出来的”。这不是缺一个数字字段的小问题，而是 temporal communication 没有 grammar identity。

Persistence 就是跨时间通信：旧 deployment 是 producer，未来 deployment 是 consumer。没有 version，compatibility 变成考古。

### 支配原则

Persistence 创造跨时间的通信。旧代码是 producer，未来代码是被 deployment 而非机器分隔开的 consumer。没有 schema version，consumer 必须推断是哪种 grammar 与 semantics 产生了数据，把 compatibility 变成猜测。Versioning 让历史解释变成对 evidence 的分派，而不是对 shape 的启发式。

### 何时触发

- event/file/wire/cache 跨 deployment 存活，却不携 semantic schema version；
- reader 用 field presence/filename/长度猜历史版本；
- 同一 bytes shape 在新版本改变 meaning，却没有 identity；
- unknown future representation 被 best-effort 当 current schema parse。

### 不应触发

- value 生命周期严格不跨 process/deployment；
- 已有显式 schema identity 与 deterministic read/migrate/reject；
- deployment version 不等于 schema version，除非两者语义严格绑定；
- ephemeral debug dump 不作为 compatibility surface。

### 与相邻规则区分

`guessed-migration` 是下游恢复缺失历史语义的尝试；`stale-documentation` 是 spec 里的分歧。Tie-break：如果时间歧义来自未能识别 schema 本身，用本规则；如果 code 因为 version 缺失而从 shape 推断历史意义，修复仍从这里开始，而不是单独归 `guessed-migration`。

### 判定程序

问：未来代码拿到这份 bytes，在解释第一字段之前，凭什么知道它是哪套 semantics？若答案是“看起来像”，versioning 已缺席。

### 例子

- positive：event log 增加字段并改变 meaning，没有 version byte；readers 从 field presence 推断。
- near-miss：每个 event 携带 `schemaVersion`，unknown versions 以 typed compatibility error 失败。
- counterexample：docs 仍描述旧 event shape，而 code 写新 shape——那是 `stale-documentation`。

## 建议

### 现在该做什么

在 semantic persistence boundary 增加显式 schema identity，并为每个 supported version 定义 deterministic read / pure migration / typed rejection。Unknown future version fail closed，不做 shape archaeology。

### 为什么重要

没有 version 时，future reader 只能从“有没有某字段”“文件叫什么”“长度像不像”推断历史语义。这种 heuristic 在原 writer 已消失后最危险，也最难证明。

### 修复策略

在语义边界 version，而不只是按 deployment number。保持 migrations 纯净，并用历史 fixtures 测试它们；对 unknown future versions 拒绝，而不是 best-effort 把它们解析进当前意义。

### 决策分支

- 若 bytes 可能被更新或更旧的 deployment 读取，添加显式 schema identity 与 compatibility rules。
- 若 value 从不离开单个 process/deployment，不要为它发明一个 versioned store。

### 常见假修复

- 用 field presence 猜 version。
- 用 deployment/build number 冒充 schema semantics。
- unknown version 尽量 parse 成 current type。
- version 每次 release 都涨，即使 schema semantics 没变；version identity 应表达语言变化，不是日历。

### 验证

保存真实 historical fixtures。每份 bytes 在读任何业务字段前先识别 version，然后 deterministic read/migrate；未知 version 必须得到明确 compatibility outcome。

### 完成条件

任何跨版本 durable value 都携带足够证据，让未来代码先知道“这是哪种 schema”，再开始解释内容。

### Nudge

Durable bytes 是写给未来代码的消息。消息必须自带语言身份，不能要求未来 reader 猜作者当时说哪种方言。

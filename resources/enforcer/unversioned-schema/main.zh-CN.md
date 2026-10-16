# unversioned-schema — Main 中文版

## 现在该做什么

在 semantic persistence boundary 增加显式 schema identity，并为每个 supported version 定义 deterministic read / pure migration / typed rejection。Unknown future version fail closed，不做 shape archaeology。

## 为什么重要

没有 version 时，future reader 只能从“有没有某字段”“文件叫什么”“长度像不像”推断历史语义。这种 heuristic 在原 writer 已消失后最危险，也最难证明。

## 修复策略

在语义边界 version，而不只是按 deployment number。保持 migrations 纯净，并用历史 fixtures 测试它们；对 unknown future versions 拒绝，而不是 best-effort 把它们解析进当前意义。

## 决策分支

- 若 bytes 可能被更新或更旧的 deployment 读取，添加显式 schema identity 与 compatibility rules。
- 若 value 从不离开单个 process/deployment，不要为它发明一个 versioned store。

## 常见假修复

- 用 field presence 猜 version。
- 用 deployment/build number 冒充 schema semantics。
- unknown version 尽量 parse 成 current type。
- version 每次 release 都涨，即使 schema semantics 没变；version identity 应表达语言变化，不是日历。

## 验证

保存真实 historical fixtures。每份 bytes 在读任何业务字段前先识别 version，然后 deterministic read/migrate；未知 version 必须得到明确 compatibility outcome。

## 完成条件

任何跨版本 durable value 都携带足够证据，让未来代码先知道“这是哪种 schema”，再开始解释内容。
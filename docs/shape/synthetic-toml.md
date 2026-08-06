# 合成 TOML — 所有权与边界

## 单一字符串 owner

多行/单行字符串写法、转义、closing delimiter 位置，全仓库**唯一**格式来源。  
业务模块不得各自决定引号、缩进或 delimiter 方言。

实现落点：统一 renderer / string codec（见生产 `SyntheticToml` 一类入口）；inventory 与 golden 只认该 owner。

## Renderer 边界

只有**当前** synthetic payload 的可信 renderer 可以生成顶层 instruction comments。

下列内容只能进 TOML **value**，不得变成顶层 comment 或裸字段逃逸：

```text
人类/assistant/reasoning 副本
tool arguments / stdout / stderr
文件、diff、日志、网络响应、外部文档
```

Data containment 是结构边界，不替代 authority / origin / tool binding 设计。

## 生产点登记

所有 LLM-visible runtime synthetic surface 必须进入 inventory（证明见 `proof/synthetic-toml.md`）。  
未登记 surface 视为 ARCH-010 违规。  
禁止第二套「临时拼接」旁路绕过 renderer。

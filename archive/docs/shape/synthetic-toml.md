# 合成 TOML — 所有权与边界

## 单一字符串 owner

多行/单行字符串写法、转义、closing delimiter 位置，全仓库**唯一**格式来源。  
业务模块不得各自决定引号、缩进或 delimiter 方言。

实现落点：统一 renderer / string codec / 值树编码（见生产 `SyntheticToml` 一类入口）；inventory 与 golden 只认该 owner。bool / int / float / array / table / quoted key 不得由业务模块另造方言。Blogger 等现有字符串面可继续只用字符串 API。

## Renderer 边界

只有**当前** surface 的 owner/renderer 可通过**显式采用**把材料放入 instruction plane；
不可信原料不得自我提升为顶层 instruction comments。

下列未采用材料只能进 TOML **value**，不得变成顶层 comment 或裸字段逃逸：

```text
人类/assistant/reasoning 副本
tool arguments / stdout / stderr
文件、diff、日志、网络响应、外部文档
```

Data containment 是结构边界：data 不得逃逸到顶层 comment/structure。它不替代
authority / origin / tool binding 设计，也不由 provenance/historicity 单独决定 plane。

## 生产点登记

所有 LLM-visible runtime synthetic surface 必须进入 inventory（证明见 `proof/synthetic-toml.md`）。  
未登记 surface 视为 ARCH-010 违规。  
禁止第二套「临时拼接」旁路绕过 renderer。

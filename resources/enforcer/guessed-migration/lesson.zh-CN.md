# guessed-migration — Lesson

## 评价

### 定义

Guessed migration 的危险，不是 migration 写得“heuristic”，而是今天的代码看着一份 old durable bytes，凭 field presence、shape、timestamp、filename、parse success 猜它**过去说的是什么语言**。

Persistence 把 representation 变成历史。一旦 bytes 跨版本存活，meaning 必须由 durable provenance 决定。`field X 存在，所以大概是 v2` 只是形状相似，不是版本证据。最危险的 migration 恰恰是“成功”——它产出一份合法新对象，但历史解释是编出来的。

### 支配原则

Persistence 把 representation 变成历史。一旦 bytes 跨版本存活，它们的 meaning 不能只靠今天的 shape 安全恢复。“if field X exists, this was probably v2”这类 heuristic 把 ambiguity 变成编造的 certainty。Migration 必须是从已知旧语言到已知新语言的函数，而不是考古乐观主义。

### 何时触发

以下情形触发：

- 没 schema version，recovery 靠字段组合猜版本；
- unknown record 默认按 latest parser 尝试；
- mtime/filename 被当 old format identity；
- “能被新 type deserialize”就视为本来就是新 schema；
- mixed historical data 靠 best-effort branch 自动升级；
- 每次 startup 都重新做同一 heuristic archaeology。

### 不应触发

不要误杀 operator-authorized one-off import。若 source dump、假设、scope 都明确，人类正式决定“这批 bytes 按旧系统 X 的语义解释”，然后转换并写入明确新 version，这可以是合法 migration。关键是 uncertainty 没有被 runtime 静默伪装成 certainty。

### 与相邻规则区分

与 `unversioned-schema` 区分：那里是 writer 从一开始没记录 schema identity；本规则是 recovery **面对这份历史债时选择了猜**。与 `partial-write-assumption` 区分：那条幻想 storage physical outcome，这条幻想 historical language。

### 判定程序

决定性问题：**我能从 durable evidence 证明 old schema version 吗？** 不能，就只能 fail closed、要求显式 operator decision，或先保留 unknown；不能因为“这个 shape 最像 v2”就替历史补一段从未记录的 provenance。

### 例子

- positive：Recovery 把“field X present”当作 v2 静默升级，没有任何 stored version。
- near-miss：一次文档化的 one-off import 在 operator 批准下映射已知 dump format，然后存下显式 version。
- counterexample：bytes 携带 version tag，纯函数迁移每个 supported predecessor。

## 建议

### 现在该做什么

停止 runtime archaeology，先建立 schema provenance。

理想状态是 durable record 自己带明确 version/generation，migration 只接受一个已知 predecessor：

```text
V1 bytes --migrateV1toV2--> V2
V2 bytes --migrateV2toV3--> V3
```

每个 transform 都应 deterministic、可用真实历史 fixture 测试，并在成功后把新 version 一起 durable 写回。

### 为什么重要

Durable data 是与过去代码的对话。如果过去没有记录自己说的是哪种语言，现在的代码就不能从相似性安全推断 meaning。静默猜测可以产出看起来合法、provenance 却是假的状态——这是最危险的 corruption，因为 recovery 看起来成功了。

### 修复策略

如果 old bytes 本身没有 version，不要继续堆 shape heuristic。选择更诚实的路径：

- 能从其他 authoritative metadata 证明版本，就用那份 evidence；
- 能限定成一批 known export/import，做一次 operator-authorized conversion；
- 仍然 ambiguous，就 fail closed / quarantine / require explicit decision。

### 决策分支

- 如果 durable evidence 命名了旧版本，应用匹配的 deterministic migration，并持久化新版本。
- 如果旧语言无法证明，fail closed，或执行一次显式的 one-time authorized conversion；不要在每次 recovery 时猜。

### 常见假修复

常见假修复：

- 再加几个 `if field exists` 让 fixtures 都能过；
- latest parser 能 parse 就当 latest schema；
- unknown 默认映成 newest version；
- migration 出错时吞掉 field、补 default，产出“看起来完整”的对象；
- 每次 recovery 都 best-effort 猜，而不是一次转换后固定 provenance；
- 把 heuristic 结果写进 cache，却不更新真正 durable schema identity。

### 验证

验证要使用**真实旧版本 bytes**，而不是根据今天 type 反向生成一个自称 v1 的 fixture。每个 supported historical version 都应有代表性 artifact 与预期 semantic result。

还要专门测 ambiguous shape：两个版本都能 parse 的 bytes 必须由 version identity 决定，不能看哪个 parser 先成功。Unknown version 应明确失败，而不是误读成“最接近的 known version”。

如果历史债来自 `unversioned-schema`，修当前 writer 同时记录 version，防止继续新增 ambiguous data；旧债则单独 migration，不要让 compatibility 永久污染 hot recovery path。

### 完成条件

完成时每次 migration 都能回答“我从哪个已证明的旧语言出发”，而不是“我看这些字段像某个版本”。

> 历史允许缺信息，但不允许今天的代码为了顺利启动就替过去编造信息。

### Nudge

> Migration 是从已知旧语言到已知新语言的函数。版本不知道时，不存在“自动迁移”，只存在自动猜测。

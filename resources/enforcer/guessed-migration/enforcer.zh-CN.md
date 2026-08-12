# guessed-migration — Enforcer

Guessed migration 的危险，不是 migration 写得“heuristic”，而是今天的代码看着一份 old durable bytes，凭 field presence、shape、timestamp、filename、parse success 猜它**过去说的是什么语言**。

Persistence 把 representation 变成历史。一旦 bytes 跨版本存活，meaning 必须由 durable provenance 决定。`field X 存在，所以大概是 v2` 只是形状相似，不是版本证据。最危险的 migration 恰恰是“成功”——它产出一份合法新对象，但历史解释是编出来的。

以下情形触发：

- 没 schema version，recovery 靠字段组合猜版本；
- unknown record 默认按 latest parser 尝试；
- mtime/filename 被当 old format identity；
- “能被新 type deserialize”就视为本来就是新 schema；
- mixed historical data 靠 best-effort branch 自动升级；
- 每次 startup 都重新做同一 heuristic archaeology。

不要误杀 operator-authorized one-off import。若 source dump、假设、scope 都明确，人类正式决定“这批 bytes 按旧系统 X 的语义解释”，然后转换并写入明确新 version，这可以是合法 migration。关键是 uncertainty 没有被 runtime 静默伪装成 certainty。

与 `unversioned-schema` 区分：那里是 writer 从一开始没记录 schema identity；本规则是 recovery **面对这份历史债时选择了猜**。与 `partial-write-assumption` 区分：那条幻想 storage physical outcome，这条幻想 historical language。

决定性问题：**我能从 durable evidence 证明 old schema version 吗？** 不能，就只能 fail closed、要求显式 operator decision，或先保留 unknown；不能因为“这个 shape 最像 v2”就替历史补一段从未记录的 provenance。

> Migration 是从已知旧语言到已知新语言的函数。版本不知道时，不存在“自动迁移”，只存在自动猜测。
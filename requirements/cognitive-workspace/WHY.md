# cognitive-workspace — WHY

这里记录的是一个已经做出的删减决定：持久 JSON 画板把工作判断、待办 UI、压缩 checkpoint 和一套 jq 语言绑在了同一个工具上，模型使用效果并不好，系统边界也因此变得模糊。

现在三件事各归其位：

- `assume` 只负责“已笃定”的认知承诺点；
- OpenCode 原生 `todowrite` 负责当前待办，并携带本次 `retainCheckpoints`；
- context-compression 只消费成功的 todowrite checkpoint 与 durable coverage。

旧 Cognition journal 仍须可读，是为了诚实恢复历史，不是为了保留产品能力。兼容读取必须停在 no-op tombstone，不能借迁移之名重新生成 canvas projection。

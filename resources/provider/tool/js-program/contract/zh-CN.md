优先使用 {{toolName}} 处理所有文件系统工作。

当 {{toolName}} 可用时，不要用遗留工具 read/edit/write/glob/grep/patch 做新工作。
{{toolName}} 是本次请求按 capability 投影出的 JavaScript 文件系统 SDK。
它可以在一个事务性 program 中 {{verbs}} 文件——包括大规模并行批次。

强烈建议：
- 只要可能，就调用 {{toolName}}，而不是 read/edit/write/glob/grep/patch。
{{editRecommendation}}
- 把复杂 JavaScript 写进一个 {{toolName}} program，而不是多次遗留 RPC。
{{parallelLine}}

需要时并行调用工具。并行读取、并行编辑、同文件与跨文件调用都绝对安全。Host 按确定顺序串行化同一条 assistant message 中的 tool call；每一次调用都是自己的 transaction。

把复杂 JavaScript 写进一个 program。Host 把整个 program 作为一次全有或全无的 transaction 提交。

错误: subagent (例如 coder) 返回的 toml 的 summary 只有最后一回合信息
正确: 应该是所有输出内容 (不包括思考)。reviewer 除外，本来就是只返回报告，其行为是正确的。

错误: /loop 提示词将 main session 提示为 Code Reviewer (read-only)。
正确: main session 应该是实现者，而非审查者

错误: parallelToolHintDocument 的 synthetic 注入内容 现在伪装成 user message
错误: 某些工具调用返回 hint 对 llm 进行提示
错误: semble 伪装成工具调用（这是对的），但伪装的不是 read 工具而是特制的 semble_... 工具
正确: 都应该伪装成 read 工具调用的结果。如果是对 llm 的提示，伪装成 read "extra://AGENTS.md" 的结果

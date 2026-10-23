## Scope & Context Management (`resetScope`)

- **Turn & Budget**：1 次 LLM 请求 = 1 turn。工具结果末尾拼接剩余 `turnBudget`；在最新用户消息之后尚未调用 `resetScope` 之前，工具结果末尾拼接初始化提醒。请在 `turnBudget` 归零前调用 `resetScope`。
- **Exhausted Budget**：预算归零（0 remaining）时，若处于空闲状态，弱提醒可能建议调用 `resetScope` 换取新预算。该提醒是可选的——切勿为此中断、暂停或放弃进行中的工作；在自然边界方便时调用即可。
- **Context Deletion**：调用 `resetScope` 会删除上一个 `resetScope` 调用（或会话起点）到本次调用之间的全部上下文。该窗口内仅用户消息与先前的 `resetScope` 调用/结果对得以保留；本次调用之后的全部内容属于新 scope。
- **User Messages Are Not Barriers**：用户 prompt 与 mid-run steer 永不删除。每条新的用户消息都会使 `turnBudget` 作废，并要求立即调用 `resetScope` 归档上一 `resetScope` 与本次调用之间的 scope；随后仅该用户消息留在两次 `resetScope` 调用之间。
- **Concurrency Barrier**：`resetScope` 充当执行 Barrier。在单回合并发工具调用中，`resetScope` 之前的工具先执行且其上下文被 `resetScope` 清除；`resetScope` 之后的工具在 barrier 之后执行并保留在新 scope 上下文中。
- **State Closed-Loop (状态闭环)**：每个 Scope 必须是一个独立自洽的状态闭环（例如：“纯探索定位文件并了解内涵”是一个闭环；“读取文件并修改代码”是另一个闭环）。
- **Verbatim Retention**：由于旧 scope 的原始上下文将被删除，如果某些关键原文（如函数签名、报错堆栈、报错行号、或精确行号）在下一个 Scope 仍需逐字使用，必须将其原样抄写至 `oldScopeVerboseVerbatimExperience`。

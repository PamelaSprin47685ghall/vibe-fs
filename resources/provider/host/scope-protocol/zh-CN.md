## Scope & Context Management (`resetScope`)

- **Turn & Budget**：1 次 LLM 请求 = 1 个 turn。仅在发往 LLM 的请求副本里，已完成的工具结果末尾显示剩余 `turnBudget`；在最新操作员撰写（operator-authored）的用户消息之后尚未出现 `resetScope` 调用之前，非 `resetScope` 结果改为显示初始化提醒。真实工具结果和持久化历史不改写。请在 `turnBudget` 归零之前调用 `resetScope`。
- **Exhausted Budget**：余额为 0 时，一个弱制的、可选的 steer 可能建议调用 `resetScope`——前提是你并不繁忙且处于自然边界。切勿为这条提醒中断、暂停或放弃正在进行的工作；在方便时调用即可。
- **Context Deletion**：调用 `resetScope` 会把旧 scope 的原始上下文从后续模型请求中移除。从上一个 `resetScope` 调用（或会话起点）到本次调用之间的窗口内，只有用户消息与先前的 `resetScope` 调用/结果对得以保留；本次调用之后的全部内容都属于新 scope。
- **User Messages Are Not Barriers**：用户 prompt 与 mid-run steer 永不删除。每条新的操作员撰写的用户消息都会使 `turnBudget` 作废，通常要求立即调用 `resetScope`；窗口内所有用户消息按原顺序保留。**唯一例外：**新的委派任务必须先调用 `negotiate(questions: string)`，集中向委托者提问，等待答复期间仍可继续工作，但不得调用 `resetScope`。只有委托者的答复真实成为 user steer 后，才应尽快调用 `resetScope`；以后可随时再次 `negotiate`，绝不暂停正常的 `resetScope`。
- **Concurrency Barrier**：`resetScope` 是一道执行 barrier。在同一个 turn 内，先行的工具调用先完成，`resetScope` 才开始；后行的调用必须等它返回。barrier 之前那些非 `resetScope` 的调用及其结果会被从上下文中移除；barrier 之后的调用保留在新 scope 中。
- **State Closed-Loop (状态闭环)**：每个 scope 都必须是一个自洽的状态闭环。例如，“纯探索以定位文件并理解其逻辑”是一个闭环；“读取文件并修改代码”是另一个闭环。
- **Verbatim Retention**：旧 scope 的原始上下文将被删除。任何在下一个 scope 中需要逐字使用的文本（例如函数签名、报错堆栈或精确行号）都必须原样抄写进 `oldScopeVerboseVerbatimExperience`。
- **Initialization Reminder**：普通操作员用户消息在模型请求副本末尾附加：[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop). Then call resetScope immediately, before doing anything else.] 唯一例外是尚未收到第一次协商答复的委派任务：此时 Pair Hint 要求先 `negotiate`，并抑制 `resetScope` 初始化提醒；直到真实答复 steer 送达才激活。不得把“问题已发送”的工具回执当作答复。

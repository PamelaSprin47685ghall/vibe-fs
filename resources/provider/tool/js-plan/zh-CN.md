规划底稿 `P` 的编程面。本工具只读写 `P` 这一个文件，路径由系统绑定。`file`、`edit`、`rewrite` 都不接收路径参数；传入路径以 `PATH_DENIED` 失败。

本编程面没有 glob、grep，也不访问仓库文件。需要仓库事实时用 `devops`。

每一任期在本任调用 `review` 之前，`todowrite-for-plan` 对底稿严格只读。此前调用 `edit` 或 `rewrite` 会被拒绝，并要求先调用 `review`。评审之后写权限解锁。接力开启的下一任期，在它自己的 `review` 之前重新锁定为只读。

程序内 `await js.ask(question)` 向用户提问并暂停：本次工具调用返回 parked 载荷并中断 attempt；用户答复后用 `{ unpark: { handle, value } }` 恢复，await 的值就是注入值。只有 S1 阶段可以提问；其他阶段以 `ASK_REFUSED` 拒绝。

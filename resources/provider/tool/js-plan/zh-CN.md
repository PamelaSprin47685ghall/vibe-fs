规划底稿 `P` 使用同一套编程面。`PLAN_PATH` 是唯一允许读写的底稿路径常量；用 `this.cat(PLAN_PATH)` 读取、`this.where` / `this.whereAll` 定位、切片重组后用 `this.write(PLAN_PATH, content)` 写回。其它路径（含绝对路径与上级目录）以 `PATH_DENIED` 拒绝。

本编程面没有 glob、grep，也不访问仓库文件。需要仓库事实时用 `devops`。

每一任期在本任调用 `review` 之前，`todowrite-for-plan` 对底稿严格只读。此前调用 `write` 会被拒绝，并要求先调用 `review`。评审之后写权限解锁。接力开启的下一任期，在它自己的 `review` 之前重新锁定为只读。

程序内 `await this.ask(question)` 向用户提问并暂停：本次工具调用返回 parked 载荷并中断 attempt；用户答复后用 `{ unpark: { handle, value } }` 恢复，await 的值就是注入值。只有 S1 阶段可以提问；其他阶段以 `ASK_REFUSED` 拒绝。

调用还可带可选 `view: { prefixUntil, thenMap }`。一旦提供 view，两个字段都必填，遵循统一的锚点区间规则。用 thenMap 的摘要与 null 表态保留交接所需内容；声明会记录交接 checkpoint。prefixUntil 只保护本轮前缀，不恢复已压缩内容，也不是交付边界。

deliver(view) — 以一份 view 声明结束本程序。调用之后程序不再继续：交付会展开调用栈，把 `view` 记入本会话的交付格，调用方拿到的交接材料由该 view 渲染而成。subagent 程序必须显式 deliver；普通 `return` 不会写入交付格，子工作会以 `undelivered` 结算（在 `dag` 中触发 `node-undelivered` park）。

调用之前，先在助手正文写好本次工作的散文：做成了什么、证据是什么、还有什么未决、继任者需要什么。`view` 是 `{ prefixUntil, thenMap }` 声明（与其他入口同一视图规则），必须指向本次工作欠调用方的全部材料——包括这份散文和它依赖的上下文。不要把散文复制进 deliver 参数，也不要留到调用之后再写：调用之后没有以后。`deliver(undefined)` 不是交付，以 `INVALID_RETURN_VALUE` 失败；view 没有选中任何材料时交接渲染为空。

捕获内部交付哨兵会让程序继续存活——之后的 return 值才是结果——所以捕获者是在知情地取消这次交付。

deliver(reportMapping) — 以一份报告映射结束本程序。调用之后程序不再继续：交付会展开调用栈，把这份映射记入本会话的交付格，调用方拿到的交接材料由它渲染而成。宿主把 reportMapping 读作 `{ prefixUntil: 0, thenMap: reportMapping }`：一份 `Record<string, null|string>`，把锚点编号映到摘要字符串或 `null`（保留）。这里不接受 `{ prefixUntil, thenMap }` 包装对象。subagent 程序必须显式 deliver；普通 `return` 不会写入交付格，子工作会以 `undelivered` 结算（在 `dag` 中触发 `node-undelivered` park）。

调用之前，先在助手正文写好本次工作的散文：做成了什么、证据是什么、还有什么未决、继任者需要什么。报告映射必须指向本次工作欠调用方的全部材料——包括这份散文和它依赖的上下文。不要把散文复制进 deliver 参数，也不要留到调用之后再写：调用之后没有以后。`deliver(undefined)` 不是交付，以 `INVALID_RETURN_VALUE` 失败；报告映射没有选中任何材料时交接渲染为空。

捕获内部交付哨兵会让程序继续存活——之后的 return 值才是结果——所以捕获者是在知情地取消这次交付。

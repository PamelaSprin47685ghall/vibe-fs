deliver(reportMapping) — 以一份报告映射结束本程序。调用之后程序不再继续：交付会展开调用栈，把这份映射记入本会话的交付格，调用方拿到的交接材料由它渲染而成。宿主把 reportMapping 读作 `{ prefixUntil: 0, thenMap: reportMapping }`：一份 `Record<string, null|string>`，把锚点编号映到摘要字符串或 `null`（保留）。自己递交 `{ prefixUntil, thenMap }` 包装对象的形式已撤销：以 `VIEW_DECLARATION_INVALID` 拒绝。subagent 程序必须显式 deliver；普通 `return` 不会写入交付格，子工作会以 `undelivered` 结算（在 `dag` 中触发 `node-undelivered` park）。

调用之前，先在助手正文写好本次工作的散文：做成了什么、证据是什么、还有什么未决、继任者需要什么。报告映射必须指向本次工作欠调用方的全部材料——包括这份散文和它依赖的上下文。不要把散文复制进 deliver 参数，也不要留到调用之后再写：调用之后没有以后。`deliver(undefined)` 不是交付：缺 reportMapping，或 reportMapping 形状非法，一律被拒绝——调用返回 `{ ok: false, code: "VIEW_DECLARATION_INVALID", reason }`，这份拒绝不构成交付，程序继续运行；空映射 `{}` 是合法的，等于不做任何替换性投影。

捕获内部交付哨兵会让程序继续存活——之后的 return 值才是结果——所以捕获者是在知情地取消这次交付。

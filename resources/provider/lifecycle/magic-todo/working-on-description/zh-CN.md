当前正在实际推进的唯一 obligation 名称。它必须精确匹配某个 `obligations[].name`。当 `obligations` 为空时，使用空字符串。它只是用于 Host 进度显示的当前焦点指针，不是 obligation status 或完成信号。实际工作焦点一旦切换，就同步更新它。

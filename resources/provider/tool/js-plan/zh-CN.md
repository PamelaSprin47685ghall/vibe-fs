规划底稿 `P` 是本编程面唯一可读写的文件。用共享的 `cat`、`stat`、`where`、`whereAll` 和 `write` 方法操作 `PLAN_PATH`；其他路径以 `PATH_DENIED` 拒绝。本工具没有 file/edit/rewrite 别名、目录列表、目录创建、仓库检索或命令执行。仓库事实由 `devops` 调查。

写入前先在本任调用 `review`。每次写入都重新检查当前任期；接力后的任期必须取得自己的评审事实。

`await this.ask(question)` 持久记录用户问题、通知用户并挂起程序。只有 S1 可以提问；其他阶段以 `ASK_REFUSED` 拒绝。真实用户答复后，用 `{ unpark: { handle, value } }` 恢复。

工具还可同时携带可选顶层参数 `prefixUntil` 和 `thenMap`，遵循共享 ANCHOR 视图合同。Plan 用户原文保持原样。编号与替换历史只在本进程有效，持久调用 checkpoint 不保存视图；须长期保留的知识写入 P。

# output-distillation — WHAT

本文件是 `output-distillation` 的**废止声明与演进记录**。本包所有业务功能与角色规范均已撤销或迁出，不再作为活跃生产系统的执行规范。

---

## DISTILL-014: 任意输出规模零 Distiller 模型会话与彻底去角色化

在任意命令执行、PTY 会话、Spool 溢出或工具执行场景下，全系统启动的 Distiller 模型子会话数量必须严格为 0。Distiller 不得作为 ManagedAgent、Persona、Role 或内部运行时被解析、生成或启动。

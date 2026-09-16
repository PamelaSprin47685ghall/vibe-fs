# output-distillation — WHAT

本文件是 `output-distillation` 的**废止声明与演进记录**。本包所有业务功能与角色规范均已撤销或迁出，不再作为活跃生产系统的执行规范。

---

## DISTILL-001: [已撤销] 模型蒸馏与 Distiller 角色撤销（迁入 PROC-013 / PROC-014 / PROC-015）

模型蒸馏机制与 Distiller 角色彻底撤销。执行输出不再启动 Distiller 模型子会话，全面改为确定性的预算内原始留尾截断。系统如实承认能力损失：尾部不保证包含全部关键错误，旧「关键错误必定保留」承诺不再保留。有效输出合同由 `process-execution`（PROC-013、PROC-014、PROC-015）统一定义。

## DISTILL-002: [已撤销] 关键事实提取转为原始尾部保留（迁入 PROC-013）

原「通过模型提炼优先保留关键错误与断言」条款撤销。超大输出直接保留预算内的未修改原始尾部字节，由 `process-execution`（PROC-013）承接。

## DISTILL-003: [已撤销] 局部观察不代表全局成功与截断声明（迁入 PROC-014）

原条款已迁入 `process-execution`（PROC-014）。当物理输出超出预算发生截断时，由执行拥有者直接生成明确的截断声明与必要定位信息，程序事实不随尾部截断而丢失。

## DISTILL-004: [已撤销] 模型子会话并发与 reduce 禁令

条款撤销。随着 Distiller 角色与模型蒸馏机制的彻底删除，蒸馏模型会话数恒为 0，不再存在任何 map/reduce 或模型层级。

## DISTILL-005: [已撤销] 摘要自包含与可定位性要求（迁入 PROC-014）

原条款撤销并迁入 `process-execution`（PROC-014）。面向调用方的输出结果直接呈现真实程序退出状态、显式截断声明与最近原始尾部。

## DISTILL-006: [已撤销] Distiller 失败降级与物理取消

条款撤销。系统不再创建、等待或取消 Distiller 代理会话。真实进程的取消与回收由 `process-execution`（PROC-006）独立管辖。

## DISTILL-007: [已撤销] Spool 固定窗口消费（迁入 PROC-015）

原条款撤销并迁入 `process-execution`（PROC-015）。流式落盘与有界读取继续作为物理输出机制，但读取上限由显式输出预算参数决定，不再无条件绑定 `Spool.ChunkSizeBytes`。

## DISTILL-008: [已撤销] Distiller 定向 await 与准入门

条款撤销。系统不再维护 Distiller 会话等待、恢复准入门或 FamilyWaiting 状态。

## DISTILL-009: [已撤销] Distiller 内部叶子运行时与 Companion 约束

条款撤销。Distiller 角色已从系统角色目录中彻底移除，不存在任何公开或私有 Distiller 实体。

## DISTILL-010: [已撤销] Distiller 权能限制

条款撤销。Distiller 角色已完全删除。

## DISTILL-011: [已迁出] Large Gate 大输出单持有者互斥门禁（迁入 PROC-016）

原 Large Gate 预算与互斥合同已迁入 `process-execution`（PROC-016）拥有。大输出进程继续受单持有者 Large Gate 互斥与 FIFO 取消队列约束。

## DISTILL-012: [已迁出] 自定义工具文本结果确定性留尾截断（迁入 PROC-017）

原 ToolResultBound 留尾截断合同已迁入 `process-execution`（PROC-017）拥有。

## DISTILL-013: [已撤销] 机械分块仪表盘禁令

条款撤销。随着模型蒸馏删除，相关机械仪表盘禁令随之失效，输出格式由 `process-execution` 的标准程序事实与原始尾部定义。

## DISTILL-014: 任意输出规模零 Distiller 模型会话与彻底去角色化

在任意命令执行、PTY 会话、Spool 溢出或工具执行场景下，全系统启动的 Distiller 模型子会话数量必须严格为 0。Distiller 不得作为 ManagedAgent、Persona、Role 或内部运行时被解析、生成或启动。

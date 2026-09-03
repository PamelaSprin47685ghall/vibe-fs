# process-execution — WHAT

## PROC-001: 终端四动词四 Contract

终端交互面划分为四个具有独立契约的物理动作：`open-terminal`（打开）、`send-terminal`（写入）、`read-terminal`（读取增量）与 `signal-terminal`（发送信号），严禁将不同物理动作合并为模糊的单一口令。

## PROC-002: Command 与 Signal 为物理 Act 且 Stdout 为 Observation

向进程发送命令与信号属于直接作用于物理世界的动作（act）；标准输出与标准错误仅为动作产生的物理观察（observation），绝不得作为任务完成或下一动作的权威凭证。

## PROC-003: 物理完成仅由 Backend Exit 确立且 Kill 不等于 Exit

终端进程的完成状态必须且仅能由底层操作系统的实际退出事件（`onExit`）确立，严禁使用输出启发式推断。发送终止信号（Kill）属于控制操作，绝不代表进程已经退出。

## PROC-004: 有界执行之 Finite Hard Limit 与确定性超时

任何物理进程执行必须显式配置有限的硬性上限（Hard Limit）；超时执行必须转入确定性的失败路径，有效超时时限严格按预估与硬顶的最小值应用。

## PROC-005: Process Request 类型化与无效 Budget 拒绝

进程执行请求必须由强类型结构表达。所有非法的预算参数（如 NaN、零或负数运行时限、负输出预算）必须在物理生成（spawn）前直接拒绝。

## PROC-006: Cancellation 彻底收束进程组且不挂死

执行中途取消时，必须向整个进程组发送终止信号并立即向等待方返回取消结果，严禁在等待进程退出的过程中发生死锁或无界挂死。

## PROC-007: 持续终端进程与一次性执行严格分型

持续交互的终端会话（支持多轮写入、读取与信号交互）与单次执行命令（`run`）属于两种互斥的物理形态，各自拥有独立的交互契约，不得混用。

## PROC-008: 完成事实双通道之 Agent Pulse 与 PTY Publish

进程执行系统与代理系统实行完成事件双通道隔离：代理完成事件仅投递轻量唤醒信号并由持久化日志读取事实，终端进程完成事件则通过专用通道投递物理执行结果。

## PROC-009: 物理输出捕获有界与 Spool 机制

标准输出与标准错误采集实行严格的内存缓冲预算封顶；一旦输出体积跨越预算阈值，系统必须立即切换为外部流式文件（spool）存储，防止内存无界积压。

## PROC-010: Terminal 与 Run 完成投影为 ExitCode 与输出

终端交互与单次运行的完成结果严格投影为真实退出码（`exit_code`）与关联输出内容，退出码必须忠实反映底层操作系统的实际退出状态。

## PROC-011: Run 为 DevOps 有界执行且非 Distiller Office

`run` 工具代表单次有界执行的物理能力，严格校验参数并在前置拦截非法输入，其职责仅限于物理命令执行，绝不承担内容摘要或结果蒸馏职责。

## PROC-012: process与PTY contract只发布纯词汇和窄capability type

process request/outcome/error、one-shot capability type及owner-pure PTY request/result vocabulary必须与Node child-process/PTY adapter、spool/output runtime分居。contract不得携带`ManagedAgent`、`TaskCompletionSource`、mutable handle、capability value/factory或Node import。Node process与Node PTY各由唯一adapter实现；delegation与其它runtime只消费composition注入的窄capability，PTY adapter不得反向引用delegation Host/Fork runtime。

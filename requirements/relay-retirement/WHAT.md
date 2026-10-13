# relay-retirement — WHAT

结束工具名是 deliver。一段与两段的主合同在 requirements/work/ 的 WHAT[work-005] 与 WHAT[work-006]。本文件保留 surface 仍引用的编号。

## [003] 资源阻塞

唯一业务 blocker 是当前 tenure 直接或递归拥有的 live child、background job、PTY/process、active tool、side-effect/execution lease、同步 descendant provider work，以及尚未观察到 terminal 的 cancel/join。

## [005] 两段式结束确认

sequence 为 S4 时，deliver 须经两次调用。第一次只落 ConfirmationCommitted，不结束。同一精确 (providerRun, toolCall) 的重放不第二次结束。另一个 toolCall 才进入退休。sequence 为 S1、S2、S3 时不套这两段。

## [007] 原子退休

结果为 Relayed 时，退休与后任 IncumbencyOpened 放进同一次追加。结果为 Delivered 时只写 IncumbencyRetired。崩溃后不得永久处于已退休但缺 outcome 或 cut 的状态。循环信号只从 Relayed 与 Delivered 派生。清理之后可以再调用 deliver。退休出清本任义务。新任按最新物理世界与输入建立自己的义务。

## [009] Work 资源交接

Relayed 只注销旧 Manager 的派工与控制租约。固定 DevOps、已接收工作及其持久后台进程继续运行，留给后任，不得隐式孤儿化或强制销毁。Work 最终关闭、Delivered 收束或致命异常时，按资源责任规则完成物理收束。不得遗留无主资源，也不得假定并发 Manager 分身。

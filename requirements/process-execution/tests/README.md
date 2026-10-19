# process-execution 测试范围

本轮保留大部分真实进程、缓冲、端口及排队回归，精简规范而不删除稳定行为。测试设施、当前数值与实现落点不构成第二份 WHAT。

- 001—007 包含真实端口/等待控制与受控 backend；002 也创建受控子进程并实际发信号。childCreate 提供受控退出事件，能验证生产等待逻辑，但不是操作系统进程。launcher 抛错或回调取消同样不单独证明进程组全部退出。部分源码结构检查仍是局部证据。
- 004/005/009 接回原 time-capability 002 的进程估计、默认上限及输出阈值回归。一小时是当前默认策略，不是所有时间能力的必需值。
- 008 真实 PTY mailbox 保持顺序并排空；尚未证明 Agent pulse 与 PTY 的完整双通道装配。
- 009 真实输出计量、转 spool 和文件往返；补清理由缓冲清空测试留下的 spool。010 实际命令验证标准流与非零退出。
- 011 验证 `js.run` 的不完整 Runnable 规约与非法命令描述用会产生文件副作用的真实命令，确认在执行前拒绝、未 spawn；另验证宿主 context codec 的只读快照。直接调用工具规约仍不证明插件角色准入。
- 012 保留真实请求/PTY契约的独立 Fable 正负编译，并拒绝从纯契约访问 JsRunLoop。旧 GitGateway 专属消费者随其 owner 退役，不能复活该路径代证其他消费者。
- 013—015 通过新 js.run 和实际注册工具检查模型计数、双语原文、总编码预算、minimum−1 零启动及 minimum 下的原生事实；onExit/watch 的直接返回也有反例。当前验收进度见[SM-04迁移门禁](../../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)，不以纯留尾函数代证完整 wire。
- 016 保留FIFO、排队及交接取消、失败释放和后继运行；launcher保留显式EndEvidence，不把抛错变非零退出。
- 017 验证 Host 原生 `run`/四个 PTY 工具及其资源确实不存在，并证明 `js.run` 接受多分钟 `maxDuration` 且无人工 10 秒上限地完成快速命令；这是退役边界与时限回归，不证明完整 Long Stroke。
- 004/006 新增同步回调死循环、park 后物理期限、脚本期限借用归还、真实 Host 硬顶/预取消/排队/丢启动回执/关闭及 finalizer。受控 Unknown 用例只证明归属协议，另有实际插件进程验证，不能互相代替。
- 018 验证真实分路 UTF-8、BOM/CRLF、字节计数、EOF、exit143/signal、spool 故障及后台 KILL 丢终结回执；已知前台终结仍须保留。

repository-programming-052 需要真实 GNU wget。当前本机未安装该工具；施工证据使用 GNU 官方 1.25.0 源码在 `/tmp` 的临时构建，测试 PATH 显式加入其目录，不进入运行依赖或发行包。

已修复的两项结果表达偏差见 GAP-093；合同/实现与范围缺口见 GAP-091/092。正式构建后运行本包、requirement-system、相关时间和 Provider 投影用例；发布节点保留 TODO。

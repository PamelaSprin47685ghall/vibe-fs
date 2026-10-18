# process-execution 测试范围

本轮保留大部分真实进程、缓冲、端口及排队回归，精简规范而不删除稳定行为。测试设施、当前数值与实现落点不构成第二份 WHAT。

- 001—007 包含真实端口/等待控制与受控 backend；002 也创建受控子进程并实际发信号。childCreate 提供受控退出事件，能验证生产等待逻辑，但不是操作系统进程。launcher 抛错或回调取消同样不单独证明进程组全部退出。部分源码结构检查仍是局部证据。
- 004/005/009 接回原 time-capability 002 的进程估计、默认上限及输出阈值回归。一小时是当前默认策略，不是所有时间能力的必需值。
- 008 真实 PTY mailbox 保持顺序并排空；尚未证明 Agent pulse 与 PTY 的完整双通道装配。
- 009 真实输出计量、转 spool 和文件往返；补清理由缓冲清空测试留下的 spool。010 实际命令验证标准流与非零退出。
- 011 验证 run 的非法预算用会产生文件副作用的真实命令，确认在执行前拒绝、未 spawn。直接调用工具规约仍不证明插件角色准入。
- 012 独立Fable编译真实ProcessRequest/PtyTypes闭包，正例可注入RunProcess；物理Host/Runner/Spool和错误终结类型负例须产生相应诊断，另编译真实Gateway消费者。
- 013 真实子进程验证原始尾部且注入session能力的任何访问抛错；实际已注册DevOps插件核对model/session/prompt计数不增加，此证明不扩展到全部工具。
- 014 实测伪指令日志不会变成结构字段；真实 spooled 输出按原值精确往返，包括尾换行、CRLF、裸 CR、引号、反斜杠、Unicode 与控制字符。截断说明为本地化数据字段 output_notice，不进入指令面，原始输出不经 trim，退出码保持真实程序事实。
- 015 验证UTF-8后缀及完整wire字节/行数，双语minimum-1零启动、minimum的正常/信号/停止结果及真实registerBounded；未知启动和故障诊断有界且不宣称已执行。
- 016 实际LargeGate验证FIFO、排队及交接取消、失败释放和后继运行。单次执行取消为NotStarted或Observed中的停止请求；launcher使用显式EndEvidence，不把抛错变非零退出。

已修复的两项结果表达偏差见 GAP-093；合同/实现与范围缺口见 GAP-091/092。正式构建后运行本包、requirement-system、相关时间和 Provider 投影用例；发布节点保留 TODO。

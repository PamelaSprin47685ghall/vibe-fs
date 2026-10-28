# dispatch-protocol — WHY

发送跨越不可靠的 Host 边界。“对方接纳了调用”“物理消息已经落地”“执行已经完成”是不同事实。把回执当消息身份会提前授予权限，把结果未知当未发送则可能再次触发同一副作用。

持久 claim 给发送留下可追踪的意图，确定性 PromptKey 让之后的物理证据能归回同一动作；sequence 又让同文的两次独立动作不被误删。未知时暂停会损失可用性，但比凭猜测重复执行诚实，不能把这个取舍包装成物理 exactly-once。

并发观察者可能同时发现同一次提醒。共享 flight 与 durable evidence 分别约束当前竞争和之后的判断，时间窗口或次数不能替代动作身份。

调度负责消息的发送证据，执行准入负责模型、容量和实际运行。Detached 及时返回使发送意图不被容量排队困住；二者混为一体，会让恢复 claim 意外重启执行。构造和恢复也需分开，以免持久存储尚未就绪时先作历史判断。

Host 的 JavaScript 输入不自带类型保证。载体冲突、隐式转换或继承字段若被当作身份，会把真实消息挂到错误会话。身份应先在边界闭合，再进入 authority 与 execution。

本包依赖 interaction-authority 的完整身份、effect-accounting 的动作事实、host-boundary 的物理证据及 durable-events 的提交语义。managed-chat-execution 拥有消息级生命周期，execution-model-routing 拥有资源裁决；进程 fatal 不能代替任何一方的事实结算。

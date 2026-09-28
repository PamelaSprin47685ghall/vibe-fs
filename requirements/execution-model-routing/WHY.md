# execution-model-routing — WHY

角色决定职责，模型只是执行该职责的远端资源。把模型池和并发偏好写进核心运行时，会使日常调度策略变成产品代码；让多份配置竞争，又会让实际选择无法解释。因此策略有单一来源，运行时维护真实占用和资源归属。

会话不是容量单位。一条物理消息可能经过多个 provider step，并同步等待借用容量的后代；按会话长期占槽或在工具等待期间持槽都会阻塞工作。绑定与 token 分开，显式借贷、因果交接与公平排队，才能在不虚增容量的前提下继续执行。

重试必须认准原执行，结束与迟到事件不能释放后来者的资源。Exact fence、单次 witness 和持久接受的先后边界，使失败有确切归属；未知结果保持未知，诊断只报告而不“修好”状态。模型切换也不能暗中更换 participant 或职责。

本包依赖 participant-identity 的身份、managed-chat-execution 的持久准入、managed-session-lifecycle 的清退信号、host-boundary 的物理观测及 execution-failure-policy 的结算授权。容量共享限同一进程；重启后重新建立本地资源，不能宣称跨进程共享内存事实。固定道路 DevOps 的持久模型约束则另跨执行保留。

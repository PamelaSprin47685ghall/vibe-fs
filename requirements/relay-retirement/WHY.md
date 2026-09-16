# relay-retirement — WHY

离场判断只需要回答一个问题：当前迭代是否还持有属于当前任期的未完结执行资源。把质量仲裁混进离场，系统就不得不用第二轮判断阻塞退出，再用复活旧身份来弥补。Retirement 只检查当前迭代递归拥有的 live child（例如未完成的临时 Engineer 委派）、未终结的 tool 执行与 lease；分数、测试、义务数量与 worktree 脏污一概不拦路。

对于道路绑定的唯一固定 DevOps 及其长生命周期后台进程，其所有权属于 Road 级别，不因单次 Manager 迭代的 Continue 退休而被强制销毁或误判为阻塞项；旧任离场注销控制租约，工作与进程平滑保留给新任 Manager。只有在道路最终关闭或遇到终结性致命事件时才对固定 DevOps 及其衍生进程进行物理收束。

离场提交的 closed outcome（Continue 或 Accepted + 证书）同时决定 Road 的去向：继续则同一 LogicalRun 等待下一迭代，接受则当前迭代关闭，证书有效期间不开启新迭代，后续显式证书失效后允许普通新迭代。

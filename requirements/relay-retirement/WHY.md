# relay-retirement — WHY

离场判断只需要回答一个问题：当前迭代是否还持有真实执行资源。把质量仲裁混进离场，系统就不得不用第二轮判断阻塞退出，再用复活旧身份来弥补。Retirement 只检查递归拥有的 live child、后台任务、PTY、未终结的 tool 执行与 lease；分数、测试、义务数量与 worktree 脏污一概不拦路。离场提交的 closed outcome（Continue 或 Accepted + 证书）同时决定 Road 的去向：继续则同一 LogicalRun 等待下一迭代，接受则当前迭代关闭，证书有效期间不开启新迭代，后续显式证书失效后允许普通新迭代。

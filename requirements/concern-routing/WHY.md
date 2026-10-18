# concern-routing — WHY

这里记录的是 concern 邮箱机制的退役决定：按关注点寻址的异步邮筒（`subscribe`/`publish`、自动订阅与 Pair Hint 捎带投递）在结构化委派之外维护了第二套通信与代次状态机，既增加了认知负担，也模糊了责任边界。

现在所有跨参与者通信与用户沟通均通过编程面与显式生命周期工具完成：

- 上下级委派与结果汇合由 `js.fork` / `js.join` 与 `js.deliver` 承载；
- 向人类用户提问与澄清由 `js.ask` / `ask` 承载；
- 系统不再维护 `Interaction/Concern` 邮箱投影、自动订阅或 Pair Hint 消息捎带。

## DEPENDS ON

- `participant-identity`
- `delegation`
- `repository-programming`

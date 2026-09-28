# relay-retirement 测试证明范围

WHAT保留评审前提、资源收束和不可逆退休，并承接新上游的完整历史及同session画板保留。义务出清不表示清空画板；未完工作也不应被账本状态堵住退场。

- `002`：真实review/suicide与journal，未评估时拒绝；REVISE后dirty工作区不阻塞，工具体不abort。
- `003/004`：实际派发一个未结束Engineer，验证资源blocker和cleanup后禁止新派工；没有覆盖全部PTY、递归、lease与并发fence。
- `007`：真实Decision/Fold的Continue、Accepted、快照冲突和CleanupBlocked转换；纯fold不证明磁盘原子性或画板与义务的完整交接。
- `008`：真实Accepted退出与Continue后继的transform，观察旧请求清空、一次中断/派发和后继历史保留。最终观察顺序不能代替受控暂停端口证明“中断完成前绝不派发”；该义务仍TODO。
- `009`：保留上游真实ToolRuntimeScope资源登记/分类的三项正反例，包括名称带devops的Engineer PTY。没有启动真实进程，也不证明跨任期物理交接与最终收束。旧 `decideWithRoadResources` 自建决策断言未沿用。

GAP-197按上述边界保留。兼容生产API的存在不计行为证据。相关schema/fold与执行入口均需统一新构建后测试；本轮完成语法检查，尚未行为验收。

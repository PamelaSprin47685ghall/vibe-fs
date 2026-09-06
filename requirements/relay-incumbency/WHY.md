# relay-incumbency — WHY

一条用户道路是一段需要持续推进的真实工作：需求会演化，工作区会变化，质量判断必须反复接受独立检验。把这段工作交给来来去去的物理 session 去解释，崩溃恢复就只能猜“该叫谁回来”，同一质量事实也会被多个身份各自解读一次。

Relay 把一条道路上的生产身份收敛成一次只存在一个的当前迭代。每一次迭代都从相同的权威起点出发：当前的用户需求原文与当前的工作区快照。迭代之间不传递私有上下文，不复活前任，不为“第一任”或“后继”设立不同的状态机。每一任都做一次独立的 assessment；发现问题的人原位接责，确认无问题的工作以证书收尾，离场只受真实资源 closure 约束。

Road 的需求本身也会继续演化。追加要求不能只是给当前物理 session 多发一句 prompt：那样 durable 状态仍认为旧需求有效，证书与后续 projection 也无法知道 authority 已改变。追加要求必须推进 Road 的 `AuthorityRevision`；若已有 active 迭代，还要把新 revision 与新的 workspace snapshot 一起绑定到该迭代，保留精确 authority message 作为后续迭代可见的权威历史。

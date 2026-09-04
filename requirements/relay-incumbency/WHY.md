# relay-incumbency — WHY

旧 Manager/Reviewer 双角色把质量判断、实现责任、离场和恢复拆成互相回调的状态机：Reviewer 只能挑错，旧 Manager 被复活修复，Finality 再用第二轮判断阻塞退出。结果是逻辑身份与物理 session 混淆，崩溃恢复必须猜“该叫谁回来”，同一质量事实被多个 owner 解释。

Relay 把一条用户道路上的生产身份收敛成唯一当前任 Manager。每任从只读 audit 开始，做一次独立 assessment；发现问题的人原位接责，离场只受真实资源 closure 约束。退役事实不可逆，后续变化只创建普通下一任，不复活前任。

Road 的需求本身也会继续演化。追加要求不能只是给当前物理 session 多发一句 prompt：那样 durable Relay 仍认为旧需求有效，证书和后续 projection 也无法知道 authority 已改变。追加要求必须推进 Road 的 `AuthorityRevision`；若已有 active incumbent，还要把新 revision 与新的 workspace snapshot 一起绑定到该任，保留精确 authority message 作为后续接力可见的权威历史。

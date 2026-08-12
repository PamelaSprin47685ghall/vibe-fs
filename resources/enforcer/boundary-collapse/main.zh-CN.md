# boundary-collapse — Main

先恢复 semantic ownership，再恢复代码形状。

对每一条跨界依赖问：对方真正需要知道的是哪一个**稳定事实或能力**？把那一小部分做成明确 contract；private representation、storage、lifecycle detail 留回 owner 内部。

常见 repair 不是“加 interface”这么机械，而是：

- foreign writer → 改成 command/intention，由 owner 决定 transition；
- shared mutable master model → 改成 context-local type + explicit translation；
- direct storage access → 改成 owner 提供的 query/port；
- private lifecycle polling → 改成 stable event/result/capability；
- internal ID/flag 泄漏 → 边界只暴露 caller 真正需要的 semantic identity/fact。

Adapter/anti-corruption layer 必须真正改变知识边界。如果它只是把同一个 DTO 原样转发、两边仍都知道 private fields，就只是多了一层门框，墙仍然是空的。

常见假修复：

- 为每个 internal class 造一个同字段 interface；
- 把 direct field access 换成 getter/setter，foreign context 仍拥有同样知识；
- 复制 internal DTO 到另一 package，却保留完全相同的 private semantics；
- 用 facade 隐藏 reach-through，内部 dependency graph 不变；
- 两边都保留 writable copy，再加 synchronization 修 contradiction；
- 为了测试方便继续 export internal path，让 test layer 反向固化边界破口。

验证的重点是 independent change。保持 declared contract 不变，重命名/替换 owner 的 storage shape、internal type、private lifecycle、implementation module。Foreign context 应无需修改、无需新 fixture、无需知道迁移细节。

再做 authority test：foreign side 尝试直接改变 owner 的 invariant-bearing state，必须不可能；它只能发送 contract 允许的 intent。Owner 返回的 observation 也只包含 caller 有资格依赖的事实。

如果两个 context 其实已经长期共享同一 invariant、每次变化都必须一起改，也可能说明原来的“两个 context”是假边界。那就诚实合并 ownership，而不是保留形式分层。Boundary 的目标是对应真实责任差异，不是数量越多越好。

完成时，每条 crossing 都能解释“穿过这里以后，知识/authority/representation 发生了什么变化”；如果什么都没变，这条 boundary 要么没修好，要么根本不该存在。

> 独立演化不是目录结构带来的，是一侧不再拥有另一侧私有知识带来的。
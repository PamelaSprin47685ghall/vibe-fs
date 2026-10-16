# boundary-collapse — Lesson

## 评价

### 定义

Boundary collapse 不是“两个模块互相调用”这么浅，而是两个本应各自拥有 invariant 的 context，开始直接伸手进对方的 representation、state 或 lifecycle，于是 private fact 变成了 foreign caller 的隐形义务。

### 支配原则

Boundary 真正的价值不是多一层 interface，而是**让一边有权不知道另一边的内部事实**。一旦跨界代码直接依赖 storage shape、mutable field、internal ID、callback timing、recovery state、private type，文件夹也许还分着，change graph 已经变成一整个对象：本地改动会打破远处没有 contract 记录的假设。

### 何时触发

以下情形触发：

- 一个 context 直接 mutate 另一个 context 的 state；
- foreign layer import internal type/storage row/implementation helper；
- 两个 bounded context 共用一个 mutable master model；
- caller 绕过应该存在的 adapter/anti-corruption translation；
- 一侧开始根据另一侧的 private lifecycle phase 决定自己的 policy；
- integration 只能靠“大家都知道这个字段现在怎么用”维持。

### 不应触发

不要误杀正常 public contract。若某个 surface 就是明确 exported、稳定、由 owner 正式承诺的 contract，双方依赖它完全合法。Translation adapter 把 contracted facts 拷贝过去也不是 collapse；关键是 private representation 是否越界获得了 semantic authority。

### 与相邻规则区分

与 `cross-layer-internal-import` 区分：后者是一条具体的 source dependency——foreign layer 直接 import 私有实现；`boundary-collapse` 更广，即使没有 import，shared mutable model、lifecycle reach-through、直接 storage access 一样会让 context 失去隔离。

与 `context-model-leak` 区分：那条专门管一个 data model 被多个 context 当成自己的概念；本规则还包括 state/lifecycle/authority 的直接穿透。

### 判定程序

诊断问题：**如果 owner 明天完全替换内部 representation，但保持 declared contract 不变，foreign context 是否应该完全不用改？** 如果现实答案是“不行，它知道太多”，boundary 已经塌了。

### 例子

- positive：billing mutate 了 auth session object 的 private fields，或 import 内部 persistence type 来“省一次 round trip”。
- near-miss：billing 消费已发布的 `PaymentDue` DTO，从不碰 auth 的 storage 或 lifecycle。
- counterexample：每个 context 拥有自己的 model；显式 translator 在 crossing 处只拷贝 contracted facts。

## 建议

### 现在该做什么

先恢复 semantic ownership，再恢复代码形状。

对每一条跨界依赖问：对方真正需要知道的是哪一个**稳定事实或能力**？把那一小部分做成明确 contract；private representation、storage、lifecycle detail 留回 owner 内部。

### 为什么重要

架构是关于允许的知识的理论。当一个 context 能依赖另一个的 internal 时，私有实现选择就意外变成公共义务。代价在后来以同步 migration、防御性 adapter、无法本地推理的改动形式出现。

### 修复策略

常见 repair 不是“加 interface”这么机械，而是：

- foreign writer → 改成 command/intention，由 owner 决定 transition；
- shared mutable master model → 改成 context-local type + explicit translation；
- direct storage access → 改成 owner 提供的 query/port；
- private lifecycle polling → 改成 stable event/result/capability；
- internal ID/flag 泄漏 → 边界只暴露 caller 真正需要的 semantic identity/fact。

Adapter/anti-corruption layer 必须真正改变知识边界。如果它只是把同一个 DTO 原样转发、两边仍都知道 private fields，就只是多了一层门框，墙仍然是空的。

### 决策分支

- 如果一个 context mutate 或 import 另一个的 internal，恢复 declared interface，切断 private reach。
- 如果 crossing 是 shared mutable model，拆分 ownership，用翻译事实代替共享 representation。
- 如果现有 surface 只导出 contracted facts，不要发明多余的 wrapping。

### 常见假修复

常见假修复：

- 为每个 internal class 造一个同字段 interface；
- 把 direct field access 换成 getter/setter，foreign context 仍拥有同样知识；
- 复制 internal DTO 到另一 package，却保留完全相同的 private semantics；
- 用 facade 隐藏 reach-through，内部 dependency graph 不变；
- 两边都保留 writable copy，再加 synchronization 修 contradiction；
- 为了测试方便继续 export internal path，让 test layer 反向固化边界破口。

### 验证

验证的重点是 independent change。保持 declared contract 不变，重命名/替换 owner 的 storage shape、internal type、private lifecycle、implementation module。Foreign context 应无需修改、无需新 fixture、无需知道迁移细节。

再做 authority test：foreign side 尝试直接改变 owner 的 invariant-bearing state，必须不可能；它只能发送 contract 允许的 intent。Owner 返回的 observation 也只包含 caller 有资格依赖的事实。

如果两个 context 其实已经长期共享同一 invariant、每次变化都必须一起改，也可能说明原来的“两个 context”是假边界。那就诚实合并 ownership，而不是保留形式分层。Boundary 的目标是对应真实责任差异，不是数量越多越好。

### 完成条件

完成时，每条 crossing 都能解释“穿过这里以后，知识/authority/representation 发生了什么变化”；如果什么都没变，这条 boundary 要么没修好，要么根本不该存在。

> 独立演化不是目录结构带来的，是一侧不再拥有另一侧私有知识带来的。

### Nudge

> 好 boundary 不是阻止交流，而是规定交流后哪一部分知识有资格留下。

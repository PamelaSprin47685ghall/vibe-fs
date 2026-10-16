# boundary-collapse — Enforcer

## 定义

Boundary collapse 不是“两个模块互相调用”这么浅，而是两个本应各自拥有 invariant 的 context，开始直接伸手进对方的 representation、state 或 lifecycle，于是 private fact 变成了 foreign caller 的隐形义务。

## 支配原则

Boundary 真正的价值不是多一层 interface，而是**让一边有权不知道另一边的内部事实**。一旦跨界代码直接依赖 storage shape、mutable field、internal ID、callback timing、recovery state、private type，文件夹也许还分着，change graph 已经变成一整个对象：本地改动会打破远处没有 contract 记录的假设。

## 何时触发

以下情形触发：

- 一个 context 直接 mutate 另一个 context 的 state；
- foreign layer import internal type/storage row/implementation helper；
- 两个 bounded context 共用一个 mutable master model；
- caller 绕过应该存在的 adapter/anti-corruption translation；
- 一侧开始根据另一侧的 private lifecycle phase 决定自己的 policy；
- integration 只能靠“大家都知道这个字段现在怎么用”维持。

## 不应触发

不要误杀正常 public contract。若某个 surface 就是明确 exported、稳定、由 owner 正式承诺的 contract，双方依赖它完全合法。Translation adapter 把 contracted facts 拷贝过去也不是 collapse；关键是 private representation 是否越界获得了 semantic authority。

## 与相邻规则区分

与 `cross-layer-internal-import` 区分：后者是一条具体的 source dependency——foreign layer 直接 import 私有实现；`boundary-collapse` 更广，即使没有 import，shared mutable model、lifecycle reach-through、直接 storage access 一样会让 context 失去隔离。

与 `context-model-leak` 区分：那条专门管一个 data model 被多个 context 当成自己的概念；本规则还包括 state/lifecycle/authority 的直接穿透。

## 判定程序

诊断问题：**如果 owner 明天完全替换内部 representation，但保持 declared contract 不变，foreign context 是否应该完全不用改？** 如果现实答案是“不行，它知道太多”，boundary 已经塌了。

## 例子

- positive：billing mutate 了 auth session object 的 private fields，或 import 内部 persistence type 来“省一次 round trip”。
- near-miss：billing 消费已发布的 `PaymentDue` DTO，从不碰 auth 的 storage 或 lifecycle。
- counterexample：每个 context 拥有自己的 model；显式 translator 在 crossing 处只拷贝 contracted facts。

## Nudge

> 好 boundary 不是阻止交流，而是规定交流后哪一部分知识有资格留下。
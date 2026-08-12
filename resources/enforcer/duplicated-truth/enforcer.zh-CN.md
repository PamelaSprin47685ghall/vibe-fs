# duplicated-truth — Enforcer

Duplicated truth 的病，不是“同一个事实出现了两份 copy”，而是**两份 representation 都能被独立写入，而且两边都声称自己 authoritative**。

一个 fact 可以有很多 projection：cache、read model、API view、index、UI state、snapshot。只要它们严格从一个 source 派生、坏了可 rebuild，就没有问题。真正的裂缝出现在第二个地方也获得 write authority：从此 disagreement 不再是 bug，而是类型系统允许的正常状态。

一旦两个 owner 都能写，所有 reader 都被迫回答一个本不该存在的问题：

> 两边不一致时谁赢？

于是同步代码、last-write-wins、reconciliation job、双向 mapper、startup repair 一个接一个出现。它们不是在消灭 duplicated truth，而是在给已经被允许的 contradiction 写越来越复杂的外交协议。

以下情形触发：

- config 同一 setting 在 DB 与 file/env 两边都可修改；
- current status 同时存在 journal projection 与 independently writable table；
- cache miss/refresh path 也能反向覆盖 source；
- old/new model dual-write 后两边都可继续被业务写；
- UI/client local state 被当 authority 回写 server，而没有 version/command semantics；
- 一个事实有两套 “source of truth” 文档，各自只在不同团队眼里成立。

不要误杀 read-only projection。Derived cache、materialized view、display copy、log、snapshot 都可以重复事实，只要写方向单向、source 身份明确、projection 可丢弃重建。Duplication of representation 不是 duplication of authority。

与 `snapshot-as-truth` 区分：那条是 derivative 被抬高，开始在冲突时反过来裁决 source；本规则更一般，只要两个 writable owner 平权存在就已经成立。与 `compatibility-cruft` 区分：dual representation 若只是 bounded migration 且旧侧已 decode-only/read-only，可以合法；若两边继续写，就变 duplicated truth。

最决定性的 test 是制造 disagreement。把两个 representation 故意写成不同值，然后问系统有没有**唯一、无需猜测的 authority**能裁决，并且另一份能从它重新 derivation。若答案是“看谁更新/哪个模块在读/启动时 merge”，truth 已经被复制成多主。

> Fact 可以被投影很多次，但“谁有资格改变它”不能被复制。
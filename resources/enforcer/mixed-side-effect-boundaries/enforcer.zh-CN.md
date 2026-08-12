# mixed-side-effect-boundaries — Enforcer

Mixed side-effect boundaries 的问题，不是“一个函数调用了多个 API”，而是**不同 external world 的 failure/lifetime/retry/authority law 与 business policy 混在同一个 imperative owner 里**。

Database transaction、Git process、HTTP request、filesystem mutation、UI update、terminal/session 各自对“完成”“失败”“取消”“重试”“幂等”有不同含义。把它们直接揉进一段 policy branch，domain rule 很快就和这些机械差异绑死：判断一个业务条件，需要同时知道 DB rollback、process exit、network timeout、file cleanup。

以下情形触发：

- 同一个 service method 一边决定 policy，一边 DB write + shell Git + HTTP publish；
- catch block 把不同 effect 的 failure 全压成一个 generic error，再在业务层猜；
- retry policy 因多个 effect 混在一起，无法说清到底重试哪一段；
- resource lifetime、transaction scope、external acknowledgement 与 domain transition 交错；
- 测一个业务 rule 必须启动/模拟多个无关 external system；
- 一种 effect 的 SDK/schema 变化迫使纯业务 rule 跟着改。

不要误杀 thin application shell。一个 orchestrator 如果只执行 core 已决定好的 commands，明确 sequence typed outcomes，自身不重新决定 domain policy，它可以合法接触多个 adapter。这里的关键不是“effect 数量”，而是**effect law 是否与 policy 共用一个 owner**。

相关操作也可以共用一个 port，例如同一 store 的多个 query；它们属于同一 failure/lifetime contract，不必人为拆散。

与 `impure-core` 区分：core 只要抓一个 DB/time source 作 decision 就已经 impure；本规则强调多个不同 external contract 在同一 imperative body 纠缠。与 `god-module` 区分：god module 责任更广；mixed effects 可以发生在一个不大的函数里。

判定时把每个 effect 列出来，并写出它的 commit/failure/cancel/retry semantics。若这些不同 law 只有通过阅读同一大段 branch 才能理解，而且里面还在作业务判断，boundary 已经混掉。

> 不同 external world 可以被同一个 workflow 协调，但不应被同一段 policy 当成一种“副作用”揉成无名泥浆。
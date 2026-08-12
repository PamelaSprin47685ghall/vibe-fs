# mutable-public-state — Enforcer

Mutable public state 的问题，不是“field 必须 private”这种 OOP 口号，而是**write authority 被分给了 caller，invariant knowledge 却没有一起分过去**。

一个 Order 若要求 “Shipped 之前必须 Paid”、一个 Session 若要求 “Closed 后不能再 publish”、一个 Lease 若要求 “Expired 后不能 renew”，这些规则应该由拥有 transition 的边界证明。若 caller 可以直接：

```text
order.status = Shipped
session.closed = false
lease.expiresAt = later
```

那么每个 caller 都成了半个 domain owner，却未必知道 authorization、event emission、durability、side effect、cross-field invariant、lifecycle predecessor。

以下情形触发：

- externally reachable code 能直接 assign invariant-bearing field；
- generic setter/patch 可绕过 named transition；
- caller 自己先 validate 再 mutate authority；
- state change 应产生 event/audit/side effect，但直接 assignment 不会；
- permission/ownership rule 只存在于某个 service method，可 caller 仍能绕过去写 field；
- public mutable collection 暴露内部 state，caller 可随手 add/remove 破坏约束；
- test fixture 习惯直接改 production object 进入某个 lifecycle state，掩盖真实 transition path。

不要误杀所有 public mutation。Pixel buffer、byte array、low-level builder、DTO、plain data structure 若 contract 本来就是 unrestricted mutation、没有更高 invariant，公开可写完全合理。关键不是 encapsulation aesthetics，而是**有没有一条 domain law 应该只在一个地方被证明**。

与 `in-place-mutation` 区分：本规则关心谁有 write authority；即使 setter 最后构造 immutable next value，只要任何 caller 都能任意指定 domain state，仍是 public authority 泄漏。反过来，write authority 已私有，但 owner 自己原地破坏 shared identity，则属于 `in-place-mutation`。

`illegal-state-representable` 关注类型本身允许哪些组合；即使 public write 被封住，constructor 仍能造 invalid state，那条规则仍可能成立。

判定问题：列出每次 authoritative write 必须守住的 invariant。若 caller 可以在不经过拥有这些 invariant 的 operation 下直接改变相关 state，write authority 就放得太宽。

> Encapsulation 的价值不是把字段藏起来，而是把“谁有资格改变事实、改变时必须证明什么”集中在一个可审计的地方。
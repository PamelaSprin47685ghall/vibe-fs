# mutable-public-state — Lesson

## 评价

### 定义

Mutable public state 的问题，不是“field 必须 private”这种 OOP 口号，而是**write authority 被分给了 caller，invariant knowledge 却没有一起分过去**。

一个 Order 若要求 “Shipped 之前必须 Paid”、一个 Session 若要求 “Closed 后不能再 publish”、一个 Lease 若要求 “Expired 后不能 renew”，这些规则应该由拥有 transition 的边界证明。若 caller 可以直接：

```text
order.status = Shipped
session.closed = false
lease.expiresAt = later
```

那么每个 caller 都成了半个 domain owner，却未必知道 authorization、event emission、durability、side effect、cross-field invariant、lifecycle predecessor。

### 支配原则

Encapsulation 不是保密，而是证明的集中。如果每个 caller 都能直接写 field，每个 caller 都要负责维护 object 的 invariant，证明义务被复制到整个 codebase。把 mutation 限制在保持 invariant 的 operation 上，让一个地方有权决定哪些 transition 合法。

### 何时触发

以下情形触发：

- externally reachable code 能直接 assign invariant-bearing field；
- generic setter/patch 可绕过 named transition；
- caller 自己先 validate 再 mutate authority；
- state change 应产生 event/audit/side effect，但直接 assignment 不会；
- permission/ownership rule 只存在于某个 service method，可 caller 仍能绕过去写 field；
- public mutable collection 暴露内部 state，caller 可随手 add/remove 破坏约束；
- test fixture 习惯直接改 production object 进入某个 lifecycle state，掩盖真实 transition path。

### 不应触发

不要误杀所有 public mutation。Pixel buffer、byte array、low-level builder、DTO、plain data structure 若 contract 本来就是 unrestricted mutation、没有更高 invariant，公开可写完全合理。关键不是 encapsulation aesthetics，而是**有没有一条 domain law 应该只在一个地方被证明**。

### 与相邻规则区分

与 `in-place-mutation` 区分：本规则关心谁有 write authority；即使 setter 最后构造 immutable next value，只要任何 caller 都能任意指定 domain state，仍是 public authority 泄漏。反过来，write authority 已私有，但 owner 自己原地破坏 shared identity，则属于 `in-place-mutation`。

`illegal-state-representable` 关注类型本身允许哪些组合；即使 public write 被封住，constructor 仍能造 invalid state，那条规则仍可能成立。

### 判定程序

判定问题：列出每次 authoritative write 必须守住的 invariant。若 caller 可以在不经过拥有这些 invariant 的 operation 下直接改变相关 state，write authority 就放得太宽。

### 例子

- positive：Order status 是 public field；caller 直接设 `Cancelled`，没跑 cancellation rules。
- near-miss：pixel buffer 暴露 mutable bytes，因为 unrestricted mutation 就是整个 contract。
- counterexample：status 只能通过 `cancel()` / `ship()` 改变，这些方法 validate 并发 event。

## 建议

### 现在该做什么

把 authoritative write 收回 named transition owner。

先列出合法 state changes 的业务名字：`cancel`、`ship`、`renew`、`accept`、`close`、`publish`。让 caller 表达 **intent**，而不是直接指定最终 field value。Transition owner 再集中处理 validation、authorization、current-state check、event/durability、side effect。

Healthy surface 往往像：

```text
result = order.ship(actor, asOf)
```

而不是：

```text
order.status = "shipped"
```

或一个同样空洞的包装：

```text
order.setStatus("shipped")
```

Private setter 若接受任意值、没有 domain decision，只是把 public field 换成 ceremony，并没有真正集中 authority。

### 为什么重要

Public mutation 在没分发完整知识的情况下分发 authority。每个 caller 都能造出 object 自己绝不会选的状态，然后每个 downstream consumer 都要防御这些可能性。Encapsulation 在 transition boundary 一次性支付推理成本。

### 修复策略

保持 state immutable 或 privately owned，为合法 transition 定义 named operations，让每个 operation 返回新值或 typed failure。当 field 有 domain meaning 时，避免 generic setters。

### 决策分支

- 如果 caller 能 assign invariant-bearing field，藏起 write，暴露 enforce rules 的 named transitions。
- 如果 structure 的 contract 就是 unrestricted mutation、没有更高 invariant，让它保持 public，不要发明假的 encapsulation。

### 常见假修复

常见假修复：

- field private，但暴露 `update(patch)` / `set(key,value)`，仍可绕过 transition；
- 把 validation helper 公开，让每个 caller 先 `validate()` 再自己 mutate；
- named method 只赋 field，却忘记 event/durable side effect；
- 为 test 保留 “unsafeSetStateForTests”，最后 production fixture/repair path 也开始依赖；
- 返回 live mutable collection，caller 通过 collection reference 仍能改 authoritative state；
- 把 state copy 给 caller，但 caller 改 copy 后又能无条件 `save(copy)` 覆盖 current version；
- 为“灵活性”保留 generic admin bypass，却没有明确 capability/audit boundary。

### 验证

观察可以尽量开放：immutable view、copy、snapshot、read-only projection 都可以让 caller 自由读。要收紧的是 authoritative mutation capability，不是信息本身。

验证要从 bypass 角度做。尝试绕过 named operation 直接制造一个过去非法 transition：API/type 应让它不可达，或者明确走受授权的 migration/admin boundary。再故意破坏 transition precondition，确认 owner 拒绝且没有任何 durable/external side effect 逃逸。

若 persistence 是另一个 boundary，还要保证 repository `save` 不重新暴露任意 state replacement。常见健康模式是 repository 只保存 aggregate owner 已构造/版本化的 transition result，或 CAS current version，而不是给任意 caller 一个“把这份 record 覆盖进去”的超级权限。

### 完成条件

完成时，每个 authoritative write 都能回答：哪个 named operation 发起、谁有 authority、哪些 invariant 在这里被证明、哪些 event/effect 随之发生。Caller 不再通过 assignment 偷走这些职责。

> Public read 是信息共享；public write 是主权共享。后者必须有非常明确的理由。

### Nudge

> Encapsulation 的价值不是把字段藏起来，而是把“谁有资格改变事实、改变时必须证明什么”集中在一个可审计的地方。

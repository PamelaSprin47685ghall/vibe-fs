# mutable-public-state — Main

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

常见假修复：

- field private，但暴露 `update(patch)` / `set(key,value)`，仍可绕过 transition；
- 把 validation helper 公开，让每个 caller 先 `validate()` 再自己 mutate；
- named method 只赋 field，却忘记 event/durable side effect；
- 为 test 保留 “unsafeSetStateForTests”，最后 production fixture/repair path 也开始依赖；
- 返回 live mutable collection，caller 通过 collection reference 仍能改 authoritative state；
- 把 state copy 给 caller，但 caller 改 copy 后又能无条件 `save(copy)` 覆盖 current version；
- 为“灵活性”保留 generic admin bypass，却没有明确 capability/audit boundary。

观察可以尽量开放：immutable view、copy、snapshot、read-only projection 都可以让 caller 自由读。要收紧的是 authoritative mutation capability，不是信息本身。

验证要从 bypass 角度做。尝试绕过 named operation 直接制造一个过去非法 transition：API/type 应让它不可达，或者明确走受授权的 migration/admin boundary。再故意破坏 transition precondition，确认 owner 拒绝且没有任何 durable/external side effect 逃逸。

若 persistence 是另一个 boundary，还要保证 repository `save` 不重新暴露任意 state replacement。常见健康模式是 repository 只保存 aggregate owner 已构造/版本化的 transition result，或 CAS current version，而不是给任意 caller 一个“把这份 record 覆盖进去”的超级权限。

完成时，每个 authoritative write 都能回答：哪个 named operation 发起、谁有 authority、哪些 invariant 在这里被证明、哪些 event/effect 随之发生。Caller 不再通过 assignment 偷走这些职责。

> Public read 是信息共享；public write 是主权共享。后者必须有非常明确的理由。
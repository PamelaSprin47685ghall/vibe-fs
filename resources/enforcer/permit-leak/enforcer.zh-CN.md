# permit-leak — Enforcer

Permit leak 不是普通意义上的“忘记 cleanup”，而是有限 capacity 的守恒被破坏了。

Semaphore slot、lock token、lease、gate entry、pool permit 这类东西不是普通数据。`acquire` 一次，就等于从系统的总容量里拿走一份线性权利；这份权利必须最终**恰好归还一次**，或者被显式 transfer 给唯一的新 owner。

真正的危险是 lifetime 靠 control-flow path 记忆：

```text
acquire permit
await work
release permit
```

代码看起来成对，但只要 `work` throw、cancel、timeout、early return，后面的 release 就可能永远到不了。更糟的是“补救式 cleanup”还可能制造 double-release：success path 放一次，timeout callback 又放一次，结果系统凭空多出 capacity。

以下情况触发：

- acquire 与 release 是手工分开的两个 statement；
- exception/cancellation/timeout 分支各自记得或忘记归还；
- permit 被塞进 callback/future，owner 已经说不清；
- transfer 后旧 owner 仍可能 release；
- pool/semaphore 可用容量随着运行时间只减不回；
- 单次失败不会挂系统，但累积失败最终把并发度耗成 0；
- 测试只证明 happy path 会 release，没有强制 fault/cancel。

不要把它扩大成“所有 resource 都是 permit”。File/socket/process 属 `resource-not-scoped` 的更一般 lifetime；permit 的特殊性在于它代表**有限并发/容量权利**，泄漏会让系统越来越不能开始新工作，而不是只留下一个孤儿资源。

也不要误杀真正 scoped/linear transfer。`using/defer/finally/bracket` 若机械保证每次 acquisition 正好一次 release，就是正确形状；显式 ownership transfer 若能证明旧 owner 失去释放义务，也可以合法逃出 lexical scope。

最决定性的 invariant 不是“release 被调用过”，而是：

```text
capacity_after = capacity_before
```

无论 success、throw、cancel、timeout、partial initialization、ownership transfer，完成后守恒都必须成立。

> Permit 是线性 capacity，不是一个随手拿、以后记得还的 boolean。一次 acquire，就产生一份必须且只能 discharge 一次的债。
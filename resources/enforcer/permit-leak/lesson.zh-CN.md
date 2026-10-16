# permit-leak — Lesson

## 评价

### 定义

Permit leak 不是普通意义上的“忘记 cleanup”，而是有限 capacity 的守恒被破坏了。

Semaphore slot、lock token、lease、gate entry、pool permit 这类东西不是普通数据。`acquire` 一次，就等于从系统的总容量里拿走一份线性权利；这份权利必须最终**恰好归还一次**，或者被显式 transfer 给唯一的新 owner。

真正的危险是 lifetime 靠 control-flow path 记忆：

```text
acquire permit
await work
release permit
```

代码看起来成对，但只要 `work` throw、cancel、timeout、early return，后面的 release 就可能永远到不了。更糟的是“补救式 cleanup”还可能制造 double-release：success path 放一次，timeout callback 又放一次，结果系统凭空多出 capacity。

### 支配原则

Permit 是线性资源：一次 acquisition 创造恰好一个 release 义务。任何有多个 exit 的 control flow 都在威胁这条守恒律，除非 lifetime 被结构性编码。手工 acquire/release 成对之所以脆弱，不是因为开发者常常忘记，而是因为 exception、cancellation、early return 不断创造必须保持同一 accounting identity 的新路径。

### 何时触发

以下情况触发：

- acquire 与 release 是手工分开的两个 statement；
- exception/cancellation/timeout 分支各自记得或忘记归还；
- permit 被塞进 callback/future，owner 已经说不清；
- transfer 后旧 owner 仍可能 release；
- pool/semaphore 可用容量随着运行时间只减不回；
- 单次失败不会挂系统，但累积失败最终把并发度耗成 0；
- 测试只证明 happy path 会 release，没有强制 fault/cancel。

### 不应触发

不要把它扩大成“所有 resource 都是 permit”。File/socket/process 属 `resource-not-scoped` 的更一般 lifetime；permit 的特殊性在于它代表**有限并发/容量权利**，泄漏会让系统越来越不能开始新工作，而不是只留下一个孤儿资源。

也不要误杀真正 scoped/linear transfer。`using/defer/finally/bracket` 若机械保证每次 acquisition 正好一次 release，就是正确形状；显式 ownership transfer 若能证明旧 owner 失去释放义务，也可以合法逃出 lexical scope。

### 与相邻规则区分

与 `resource-not-scoped` 区分：那条管一般 acquire/dispose 资源。与 `lost-update` 区分：那条管并发写。如果有限并发/容量权利能跨 exit path 泄漏，用本规则；如果任何 disposable resource 缺 scoped lifetime，用 `resource-not-scoped`；如果并发写丢版本，用 `lost-update`。

### 判定程序

最决定性的 invariant 不是“release 被调用过”，而是：

```text
capacity_after = capacity_before
```

无论 success、throw、cancel、timeout、partial initialization、ownership transfer，完成后守恒都必须成立。

### 例子

- positive：`sem.acquire()` 后 `await work()` 再 `sem.release()`，一个 throw 让 slot 永远不还。
- near-miss：`await using lease = await pool.acquire()` 在 throw、cancel、return 时都 release。
- counterexample：mutex 用 `with lock:` / `defer unlock`，没有手工配对。

## 建议

### 现在该做什么

把 permit lifetime 从 control flow 记忆搬进结构。

优先用 `using`、`defer`、`finally`、bracket、scoped lease 等机制，让 acquire 与 release 变成同一个 ownership construct：只要 scope 退出，不管是正常 return、exception、cancel 还是 timeout，归还义务都会执行。

核心不变量是**capacity conservation**：每次 acquisition 恰好对应一次 release；不能 0 次，也不能 2 次。

如果 permit 必须跨 scope transfer，就把 transfer 做成真正 ownership move：新 owner 接受 obligation，旧 owner 失去 obligation。不要让两个地方都“为了保险”各自可能 release。

### 为什么重要

泄漏的 permit 是没有证据就消失的 capacity。泄漏足够多，有界并发会变成最终 deadlock 或 starvation，而且往往离那个没 release 的 operation 很远。根问题是藏在 control flow 里的 lifetime accounting。

### 修复策略

用 `using`/`defer`/`finally`/bracket 或语言专属的 scoped primitive。把 acquired token 留在该 lexical lifetime 内；除非 transfer 在 type/protocol 里显式，否则不要转移它。

### 决策分支

- 如果 release 依赖到达后面某个 statement，把 acquire 包进每个 exit 都会执行的 scoped/bracketed construct。
- 如果需要 transfer，让 transfer 是 linear/explicit，保证恰好一个 owner 仍有 release 义务。

### 常见假修复

常见假修复：

- 在每个已知 `catch` 分支手工补 `release()`；下一条新 exit path 还会重新漏；
- success path 与 timeout callback 都 release，最后 double-release；
- `finally` 里 release，但某些 acquisition 失败前根本没拿到 permit，又无条件归还；
- 把 permit 存进 global registry，依赖 shutdown sweep 最后统一归还；
- timeout 后立即 release，但 underlying work 其实仍在跑，导致实际并发超过 bound；
- 只检查 semaphore count，没有验证 leaked work/owner 是否仍持有 capability。

### 验证

验证要覆盖真正危险的退出方式：

- acquire 后立即 throw；
- work 中途 cancel；
- timeout 与 completion 竞争；
- partial setup：permit 已拿到，后续 resource acquire 失败；
- ownership transfer；
- cleanup 自己抛错；
- repeated cancel/dispose，确认不会重复 release。

测试不能只 spy `release()` call count。更强的证据是：故障结束后，新的 work 仍能拿到完整预期 capacity；同时旧 work 已经不再使用那份 permit 所保护的资源。

特别注意 timeout。只有当 timeout 同时终止/转移 underlying work 的 ownership 时，permit 才能归还；如果只是 caller 不再等待，却先把 slot 放回 pool，系统表面没有 leak，实际却突破了并发上限。

### 完成条件

完成时，permit accounting 不再需要审计每一条 branch。结构本身就能证明：拿走一份 capacity，最终恰好还回一份。

> 对有限资源而言，double-release 与 leak 是同一条守恒定律的两个方向：一个凭空造容量，一个永久吃容量。两者都不该靠运气避免。

### Nudge

> Permit 是线性 capacity，不是一个随手拿、以后记得还的 boolean。一次 acquire，就产生一份必须且只能 discharge 一次的债。

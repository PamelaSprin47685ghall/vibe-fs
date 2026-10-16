# permit-leak — Main

## 现在该做什么

把 permit lifetime 从 control flow 记忆搬进结构。

优先用 `using`、`defer`、`finally`、bracket、scoped lease 等机制，让 acquire 与 release 变成同一个 ownership construct：只要 scope 退出，不管是正常 return、exception、cancel 还是 timeout，归还义务都会执行。

核心不变量是**capacity conservation**：每次 acquisition 恰好对应一次 release；不能 0 次，也不能 2 次。

如果 permit 必须跨 scope transfer，就把 transfer 做成真正 ownership move：新 owner 接受 obligation，旧 owner 失去 obligation。不要让两个地方都“为了保险”各自可能 release。

## 为什么重要

泄漏的 permit 是没有证据就消失的 capacity。泄漏足够多，有界并发会变成最终 deadlock 或 starvation，而且往往离那个没 release 的 operation 很远。根问题是藏在 control flow 里的 lifetime accounting。

## 修复策略

用 `using`/`defer`/`finally`/bracket 或语言专属的 scoped primitive。把 acquired token 留在该 lexical lifetime 内；除非 transfer 在 type/protocol 里显式，否则不要转移它。

## 决策分支

- 如果 release 依赖到达后面某个 statement，把 acquire 包进每个 exit 都会执行的 scoped/bracketed construct。
- 如果需要 transfer，让 transfer 是 linear/explicit，保证恰好一个 owner 仍有 release 义务。

## 常见假修复

常见假修复：

- 在每个已知 `catch` 分支手工补 `release()`；下一条新 exit path 还会重新漏；
- success path 与 timeout callback 都 release，最后 double-release；
- `finally` 里 release，但某些 acquisition 失败前根本没拿到 permit，又无条件归还；
- 把 permit 存进 global registry，依赖 shutdown sweep 最后统一归还；
- timeout 后立即 release，但 underlying work 其实仍在跑，导致实际并发超过 bound；
- 只检查 semaphore count，没有验证 leaked work/owner 是否仍持有 capability。

## 验证

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

## 完成条件

完成时，permit accounting 不再需要审计每一条 branch。结构本身就能证明：拿走一份 capacity，最终恰好还回一份。

> 对有限资源而言，double-release 与 leak 是同一条守恒定律的两个方向：一个凭空造容量，一个永久吃容量。两者都不该靠运气避免。
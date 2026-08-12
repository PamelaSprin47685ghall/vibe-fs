# translator-layer-bloat — Enforcer 中文版

## 定义
中间层不是因为“薄”就有罪。真正的 translator-layer bloat 是：caller 每跨一层，都没有获得新的语义保证、表示转换、authority 限制、failure isolation 或 lifecycle ownership，只是多了一次转发和一次改名。

一个 layer 必须改变“跨过去以后可以相信什么”。如果去掉它，两个邻居直接相连，所有 invariant 完全不变，那么这层不是 abstraction，只是距离。

## 何时触发
- `Manager -> Service -> Coordinator -> Adapter` 方法一一对应、DTO 一样；
- 中间对象只把 `foo(x)` 转成 `inner.foo(x)`；
- 每一层都有 interface/mock/test，却没有独立 contract；
- generic orchestration noun 很多，但问“这层独有地保护什么”没人能回答；
- stack trace、debug、修改路径被 forwarding hops 拉长，却没有 information hiding。

## 不要误判
- anti-corruption layer 真正转换 domain language / IDs / units / errors；
- adapter 隔离 third-party failure/lifetime；
- authorization/capability narrowing 在这一层发生；
- transaction、batching、cache consistency、protocol framing 等 invariant 由它独占；
- generated stub 就是实际 wire contract，不应为“少一层”而手写替代。

## 刀口
把这层从白板上擦掉。**有什么事实会因此失去 owner？有什么错误会因此无法被隔离？有什么 representation 会直接泄漏？**

答案是“没有，只是 caller 少调一个方法”，删它。

## 与近邻区分
`facade-hides-mess` 是漂亮 surface 掩盖内部坏 ownership；`translator-layer-bloat` 可以发生在内部本来就不乱，只是多了无意义转发。

`framework-tax` 是框架整体带来的 ceremony；这里聚焦于一个不改变语义的 hop。

## 例子
- 正例：`UserManager.get()` 只调用 `UserService.get()`，后者只调用 `UserRepository.get()`，前两层无 policy。
- 近邻：adapter 把 provider `string status + error prose` 转成 domain union。
- 反例：删一层后 authorization check 会消失——那层确实有 contract，应该留下并按 invariant 命名。

## 提醒
抽象的价值不是“看起来有层次”，而是让某些知识在边界另一侧变得不必知道。

# translator-layer-bloat — Lesson

## 评价

### 定义

中间层不是因为“薄”就有罪。真正的 translator-layer bloat 是：caller 每跨一层，都没有获得新的语义保证、表示转换、authority 限制、failure isolation 或 lifecycle ownership，只是多了一次转发和一次改名。

一个 layer 必须改变“跨过去以后可以相信什么”。如果去掉它，两个邻居直接相连，所有 invariant 完全不变，那么这层不是 abstraction，只是距离。

### 支配原则

一层只有改变“推理在什么前提下成立”才配存在：翻译协议、执行 authorization、规范化表示、批处理、隔离失败，或者拥有 policy。纯转发只是多了一个要搜索的地方、多了一套要学的词汇，两侧保留的却是同一份知识。没有 information hiding 的间接层是距离，不是抽象。

### 何时触发

- `Manager -> Service -> Coordinator -> Adapter` 方法一一对应、DTO 一样；
- 中间对象只把 `foo(x)` 转成 `inner.foo(x)`；
- 每一层都有 interface/mock/test，却没有独立 contract；
- generic orchestration noun 很多，但问“这层独有地保护什么”没人能回答；
- stack trace、debug、修改路径被 forwarding hops 拉长，却没有 information hiding。

### 不应触发

- anti-corruption layer 真正转换 domain language / IDs / units / errors；
- adapter 隔离 third-party failure/lifetime；
- authorization/capability narrowing 在这一层发生；
- transaction、batching、cache consistency、protocol framing 等 invariant 由它独占；
- generated stub 就是实际 wire contract，不应为“少一层”而手写替代。

### 与相邻规则区分

`facade-hides-mess` 是漂亮 surface 掩盖内部坏 ownership；`translator-layer-bloat` 可以发生在内部本来就不乱，只是多了无意义转发。

`framework-tax` 是框架整体带来的 ceremony；这里聚焦于一个不改变语义的 hop。

### 判定程序

把这层从白板上擦掉。**有什么事实会因此失去 owner？有什么错误会因此无法被隔离？有什么 representation 会直接泄漏？**

答案是“没有，只是 caller 少调一个方法”，删它。

### 例子

- 正例：`UserManager.get()` 只调用 `UserService.get()`，后者只调用 `UserRepository.get()`，前两层无 policy。
- 近邻：adapter 把 provider `string status + error prose` 转成 domain union。
- 反例：删一层后 authorization check 会消失——那层确实有 contract，应该留下并按 invariant 命名。

## 建议

### 现在该做什么

逐层标注每个 hop 唯一拥有的 invariant。没有 invariant 的 forwarding layer 删除；有真实 contract 的层保留，并以它保护的语义命名，而不是继续叫 `Manager/Service/Coordinator`。

### 为什么重要

每一层都有长期税：多一个文件、接口、mock、stack frame、constructor dependency、修改点和“policy 到底在哪”的搜索空间。只有当这层真正减少另一侧必须知道的东西时，这笔税才值得。

空转发尤其危险，因为它制造虚假的架构感。图看起来整齐，实际所有层知道的是同一套事实。

### 修复策略

- 把 method 分成：semantic transform / authority enforcement / lifecycle owner / failure isolation / pure forwarding；
- 前四类留在真正 owner；最后一类直接 collapse；
- DTO mirror 若没有边界语义，删除；
- surviving layer 用其 invariant 命名；
- 删除随空层产生的无意义 mocks/interfaces/tests。

### 决策分支

若移除这层不会失去任何 invariant、information boundary 或 failure model，删除它。
若这层拥有真实的翻译或 policy，把那个 invariant 命名出来，让它成为这层可见的 contract。

### 常见假修复

- 把 `Manager` 改名 `Facade`。
- 自动生成 forwarding boilerplate，使税更便宜但仍然存在。
- 合并两个空层，再新建一个 `Orchestrator`。
- 为了证明层有用，往里面随便塞 logging/validation；不要给无主权的层制造工作。
- 把真正 domain policy 搬进 translator，只为让它“有事可做”。

### 验证

对每个 surviving hop 应能完成一句：

> 跨过这一层以后，caller 不再需要知道 X，因为这一层独占地保证 Y。

如果说不出来，继续删。

### 完成条件

每个中间层都购买了真实 boundary capability；纯 forwarding 不再作为 architecture 本身存在。

### Nudge

抽象的价值不是“看起来有层次”，而是让某些知识在边界另一侧变得不必知道。

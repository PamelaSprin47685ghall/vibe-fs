# translator-layer-bloat — Main 中文版

## 现在该做什么
逐层标注每个 hop 唯一拥有的 invariant。没有 invariant 的 forwarding layer 删除；有真实 contract 的层保留，并以它保护的语义命名，而不是继续叫 `Manager/Service/Coordinator`。

## 为什么这很重要
每一层都有长期税：多一个文件、接口、mock、stack frame、constructor dependency、修改点和“policy 到底在哪”的搜索空间。只有当这层真正减少另一侧必须知道的东西时，这笔税才值得。

空转发尤其危险，因为它制造虚假的架构感。图看起来整齐，实际所有层知道的是同一套事实。

## 修复策略
- 把 method 分成：semantic transform / authority enforcement / lifecycle owner / failure isolation / pure forwarding；
- 前四类留在真正 owner；最后一类直接 collapse；
- DTO mirror 若没有边界语义，删除；
- surviving layer 用其 invariant 命名；
- 删除随空层产生的无意义 mocks/interfaces/tests。

## 常见假修复
- 把 `Manager` 改名 `Facade`。
- 自动生成 forwarding boilerplate，使税更便宜但仍然存在。
- 合并两个空层，再新建一个 `Orchestrator`。
- 为了证明层有用，往里面随便塞 logging/validation；不要给无主权的层制造工作。
- 把真正 domain policy 搬进 translator，只为让它“有事可做”。

## 验证
对每个 surviving hop 应能完成一句：

> 跨过这一层以后，caller 不再需要知道 X，因为这一层独占地保证 Y。

如果说不出来，继续删。

## 完成条件
每个中间层都购买了真实 boundary capability；纯 forwarding 不再作为 architecture 本身存在。

# god-module — Main

## 现在该做什么
拆 sovereignty，不要拆行数。

找出 module 内彼此独立的 invariants、policy owners、state lifecycles、effect boundaries。每个 coherent cluster 给独立 owner，只保留 correctness 真正要求 joint authority 的 coordination。

不要先决定“每个文件最多多少行”。

## 为什么重要
God module 会把 local change 变成 systemic risk。

因为 unrelated responsibilities 共享一个 owner，它们很快也会共享 dependencies、mutable state、initialization order、error handling 与 tests。改 retry policy 可能破 session lifecycle；加 billing field 却要构造 Git fixture；persistence refactor 会碰 authorization code，只因为它们都住在一个 giant context。

Module 还会形成 organizational gravity：新工作继续往这里加，因为有用 dependencies 都已经在这里。于是下一次 change 更有理由继续加，convenience 形成正反馈。

## 修复策略
做一张 sovereignty map：

1. 枚举 decisions、mutable state、owned resources、side effects；
2. 按 invariant/reason-to-change 分组；
3. 找 groups 间 dependencies，区分 real causality 与 convenience access；
4. 为 independent groups 提取 owners，只给它们最小 capabilities；
5. State 跟着控制其 lifecycle 的 owner 走；
6. Cross-owner coordination 放到 narrow workflow/composition layer，但这个 layer 不偷 policy；
7. 用 explicit ports/values 替代 broad context/service access；
8. Tests 能单独 exercise owner，不必构造 unrelated world。

最终可能得到一个仍然很大的 module，加几个很小 modules。完全没问题。目标是 coherent authority，不是视觉对称。

## 决策分支
- **One coherent state machine 很大：**保持一起，改 representation/test，不做 arbitrary split。
- **多个 independent mutable resources 共用一个 context：**每个 lifecycle 给 owner，borrowing 显式化。
- **Composition root 只负责 wiring：**保留 composition，不要把 construction knowledge 误判为 policy ownership。
- **Workflow 真正协调多个 owners：**orchestration 保持 narrow/declarative，decision 留在 owners。
- **两个 responsibilities 因同一 invariant 必须永远一起变：**即使名字不同，也应共 owner。
- **Extraction 产生 cycles：**重新审 ownership；不要用 reciprocal references 或新的 shared god context 解决。

## 常见假修复
- 1000 行文件拆成 `Part1/Part2/Helpers/Context`，每一部分仍能访问全部 shared state。
- 在 god module 外套新 facade，sovereignty 完全不动。
- Methods 搬进不同 classes/modules，但仍把 giant dependency container everywhere 传递。
- 只 extract pure helpers，所有 policy/state 仍 centralize。
- 强制“max 200 lines”并庆祝 compliance。十个文件组成的 distributed god object 仍然是 god object。
- 每个 call 都经 mediator/event bus，让 dependency 变 invisible 而不是减少。
- 把没有 semantic independence 的 responsibilities 切成 microservices。Network boundary 不会自动制造 ownership quality。

## 验证
选几种过去会碰 unrelated regions 的代表性 changes。

修复后应满足：

- change 主要只触碰相关 invariant 的 owner；
- owner tests 不需要 unrelated fixtures/resources；
- 每个 owner 得到的 capabilities 更窄；
- mutable state 有 clear lifecycle/writer；
- coordination code 读起来是 sequencing，不是第二套 policy engine；
- 删除一个 owner 不需要理解所有其他 owners 的 internals。

Invariant：

> Things share an owner because correctness requires joint authority, not because a central object made access easy.

## 完成条件
Architecture 可以通过新增/修改一个 sovereignty 成长，而不必每次重新打开整个帝国。

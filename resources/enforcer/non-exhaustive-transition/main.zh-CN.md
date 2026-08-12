# non-exhaustive-transition — Main

## What To Do Now
把有限 transition relation 写完整。每个 reachable `state × event` pair 都必须明确得到：命名 successor、刻意 idempotent/no-op、typed rejection，或者被类型机械证明为不可达。

Transition function 才是这些 cell 的 owner。Wildcard/default 不是 owner，只是在藏无人决策的格子。

## Why This Matters
最贵的 state-machine bug 往往不是“某个 branch 写错了”，而是**这个 branch 从来没人设计过**。

`_ -> keep state` 看起来很稳健，直到新 event 加进来。compiler 不报错、旧 tests 可能照绿，而新事件悄悄继承昨天的 fallback。一次 domain/product ontology 变化就这样绕过了 policy review。

Exhaustive match 的意义，就是把 ontology growth 变成可见工作：世界一旦新增 case，所有依赖旧 case set 的 finite policy 都必须回答“这个新东西在我这里意味着什么”。

## Repair Strategy
1. 枚举 reachable states 与 event/input cases。
2. 建 `state × event` 表。
3. 每格标成 successor / no-op / rejection / unreachable。
4. 用 exhaustive matching 或同样 total、可读的 declarative relation 编码。
5. `IllegalTransition` / `NoOp` 用 typed result，不要都伪装成“保持当前状态”。
6. 用 table/property test 覆盖所有 finite pair。
7. 删除只为让 compiler 安静而存在的 wildcard/default。

## Decision Branches
- closed domain：要求穷尽 policy。
- intentionally extensible protocol：单独定义 unknown-case law，并与 closed domain transition 隔离。
- 多个 pair 确实同语义：显式 group，它们的 membership 不应由 wildcard 偶然决定。
- 某 pair construction 上不可能：用 type/constructor 证明，不要靠 “cannot happen” 注释。

## Common Wrong Fixes
- 把 `_ -> state` 换成 `_ -> Illegal`，但未来 case 仍能未经 review 自动落入这里。
- default 里加 log，就称为“显式处理”。
- partial map 缺 key 后统一一个含义。
- 生成一张 technically exhaustive、但没人能读懂其 domain policy 的巨表；mechanical totality 不是 semantic clarity。
- 在 wildcard 旁写注释列 ignored cases，而不是让 case set 本身成为 compile-time obligation。

## Verification
对所有 finite pair 做 table/property test，断言准确 successor/no-op/rejection category。

然后临时增加一个 event case。build/test 必须失败，或暴露一个明确 unclassified cell，直到有人做 semantic decision。若新 event 自动拿到旧 fallback，修复未完成。

Invariant：**closed transition relation 的每个 cell 都是明确 domain decision。**

## Done When
Transition function 本身就是完整可读 policy；ontology 增长会制造显式 review obligation；任何 reachable pair 都不会仅因为 control flow fallthrough 而获得语义。
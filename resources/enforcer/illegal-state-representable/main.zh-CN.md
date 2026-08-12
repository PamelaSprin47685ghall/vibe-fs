# illegal-state-representable — Main

## What To Do Now
直接建模合法状态，把 invariant 移到**第一次拥有足够信息完成证明的 constructor**。用闭合 case、state-specific record 或 atomic validated constructor 替换 flag/nullable field 的松散乘积，使非法组合在进入 domain 前就不存在。

真正 owner 是 construction boundary；下游 guard 只是 invariant 的消费者，不应承担 owner 没做完的证明。

## Why This Matters
类型一旦允许一个现实不存在的值，这个值就会成为全仓库的长期工作：reader 要防、serializer 要兜、test 要覆盖、recovery 要解释。系统开始认真维护一个自己从来不希望存在的世界。

这不是 robustness，而是把一次证明拆成无数次重复怀疑。

强一点的 construction 会直接缩小 reasoning surface。`Paid of ReceiptId` 一旦构造成功，后续代码就可以依赖 receipt 存在，不必每一层都问“会不会偏偏是 None”。

## Repair Strategy
1. 不看现有字段，先列出业务上真正合法的状态。
2. 标出哪些数据只属于某个状态。
3. 把 Cartesian product 改成闭合 sum / state-specific type / validated constructor。
4. 让 transition 函数只产生合法 successor。
5. 不可信 DTO 留在 adapter；一次 parse 后再进入 domain。
6. construction 变强后，删除已经不再可能触发的 defensive branch。

## Decision Branches
- 状态有限且有名字：直接用命名 case，并把专属数据挂在对应 case 上。
- 合法性依赖 runtime fact：用一个 atomic constructor 返回 `Result`，不要让 invalid value 先存在再失败。
- wire compatibility 必须保留松散 shape：把它锁在 adapter，不得泄漏到 policy code。
- 如果所有组合真的都有语义，不要动。状态多不是罪，**虚构状态**才是。

## Common Wrong Fixes
- 增加 `validate()`，然后要求每个 caller “记得先调”。
- 在 consumer 到处加 `assert`，constructor 完全不变。
- 用 helper/facade 把同一个非法 record 包起来。
- 再加一个 status flag 去解释前几个 flag 造出的矛盾。
- 表面上 newtype，实际 public constructor 仍允许 contradictory fields。

## Verification
从所有 public construction path（包括 deserialize / recovery）尝试构造过去的非法组合。它们必须在类型层不可表达，或在唯一 ingress boundary 被拒绝，不能先生成 domain value 再靠后续 guard 发现。

再验证下游：去掉一个过去用于防 contradiction 的 branch，程序仍应安全，因为坏状态已经到不了那里。

Invariant：**representable domain state = legitimate domain state**。

## Done When
非法组合无法进入 domain/application logic；状态转移持续保持这一性质；下游不再花分支证明自己的输入是不是被 coherent 地构造出来。
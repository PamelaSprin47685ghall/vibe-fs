# type-erosion-at-boundary — Main

## What To Do Now
把 parse、validation、normalization、unchecked operation 全部收回真正拥有外部协议的 adapter，让它返回闭合的 domain/application value。Raw wire shape 可以属于 ingress；不应属于每个下游 policy function。

## Why This Matters
Cast 不会消除 uncertainty，只会把 uncertainty 搬到一个上下文更差的地方。

在 ingress，你还知道 provider、schema version、raw bytes、request identity、failure semantics。十层之后只剩一个可疑 `obj` 和一句“这里应该有这个字段”。于是 bug 在 provenance 已丢失之后才爆发，每个模块都重复支付同一笔理解成本。

Strong boundary 本质上是在压缩推理：把“这个 unknown object 大概有这些字段并满足这些组合”压成“这是 `CompletedToolCall`”。后面的代码就能思考 policy，而不是做 transport 考古。

## Repair Strategy
1. 找到 raw protocol owner 和真正 inward boundary。
2. 在那里 decode `unknown` / dynamic value。
3. 校验 required field、enum/case、cross-field invariant、version semantics。
4. 规范 casing、alias、nullable encoding、provider-specific status 等 transport accident。
5. 返回闭合 typed case；需要保留的 raw evidence 独立保存。
6. 删除 inward caller 的 dynamic lookup / cast / wire-field knowledge。

## Decision Branches
- malformed shape 需要表示：返回 typed decode failure，不要返回 half-decoded object。
- 协议必须容忍未来 unknown variant：显式建 `Unknown of RawEvidence`，不要让每个 caller 自己看任意属性。
- reflection 是 framework glue 的真实需要：把 reflection 留在 glue 内，对内暴露窄 contract。
- parse 后仍只是过弱 primitive：再处理 `primitive-obsession`，不要把 nominal identity 和 decoding 混成一件事。

## Common Wrong Fixes
- 把 `any` 重命名为 `Payload` 就宣布 typed。
- 把 cast 集中进 `asFoo()` helper，然后 domain 到处调用。
- 只校验一个字段，cross-field contradiction 仍能进入内部。
- 造一个所有字段都 optional 的 DTO 号称 compatibility-safe，再让下游继续判断。
- 测试绕过 decoder，直接制造 production 不可能构造的 typed value。

## Verification
搜索所有 inward layer 的 dynamic lookup、unchecked cast/unbox、wire field name、重复 shape guard。它们应消失，或只剩明确 generic 的 infrastructure。

用真实 ingress 喂 malformed / unknown variant：必须在那里失败，或变成显式 `Unknown`。合法输入进入内部后，不应再需要 shape re-validation。

Invariant：**type uncertainty 只有一个 ingress owner，不得向内泄漏成重复 proof obligation。**

## Done When
外部不确定性被局部化；policy code 收到的是已证明事实而不是可能性袋子；transport representation 改名或重排时，semantic code 不会仅因字段访问变化而被迫一起修改。
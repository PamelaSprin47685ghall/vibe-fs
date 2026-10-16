# type-erosion-at-boundary — Lesson

## 评价

### 定义

当本应在外部边界被消耗掉的不确定性穿过 adapter，继续以 `any`、`obj`、reflection、unchecked cast、string-key map、弱 DTO 等形式在 domain/application 内部流动时，类型发生了 erosion。

根因是**没付清 parsing debt**：边界没有把“这个 raw shape 到底证明了什么”变成 constructor 能保证的类型，于是每个下游 caller 都要重新猜一遍。

### 支配原则

外部数据当然可以弱。网络、plugin、文件、数据库 row、动态 Host API 总要在某处从不可信 bytes 开始。

问题不是 runtime parse；**runtime parse 本来就是 ingress 的职责**。问题是 parse 完以后，系统仍把 uncertainty 当作内部通行证。

好的 adapter 会消耗模糊性：校验 shape、规范表示、映射外部 variant、拒绝矛盾组合，然后只把下游有资格依赖的事实放出去。

坏的 adapter 只换名字：`any` 改叫 `Payload`，`obj` 换成 `Map<string,obj>`，或者造一个所有字段都 optional 的“typed DTO”。不确定性没消失，只是穿上了类型注解。

### 何时触发

以下情形触发：

- domain/application 根据 dynamic property、reflection、unchecked `unbox`/cast 做 semantic decision；
- 多个 inward caller 重复 `typeof` / null / property-exists 检查；
- policy code 知道 provider/JSON 的 wire field name；
- cast 的唯一证明是“Host 一般就发这个 shape”；
- malformed input 能穿过几层后才炸；
- test 直接造 dynamic bag，绕过 production decoder。

### 不应触发

- dynamic representation 完全困在 serializer/adapter，返回的是 validated domain value；
- ingress 需要 runtime validation——这不是缺陷，**让未校验输入继续向内走**才是；
- reflection 本身就是 generic framework boundary 的真实能力，且 domain semantics 不依赖它；
- raw payload 被保留作 evidence，但控制流使用的是另外构造出的 typed interpretation。

### 与相邻规则区分

`weak-boundary-parsing` 管“边界虽然 parse 了，但校验仍不完整或 fail-open”；`primitive-obsession` 管静态 primitive 丢失 domain identity；`stringly-typed-error` 是人类 prose 被当机器 identity 的特例。

Tie-break：主要问题是弱/dynamic representation 泄漏进内部，用本规则；decoder 存在但接受坏 shape，用 `weak-boundary-parsing`；已经是静态 `string` 但 sibling concept 可互换，用 `primitive-obsession`。

### 判定程序

找到**最后一个合法需要 raw representation 的地方**。从它往内，所有代码都应该拿到一个 constructor 已证明 assumptions 的类型。

对每个 cast / dynamic lookup 问：这里偷偷假设了哪条 proposition？把这条 proposition 的证明搬回 adapter，并让 returned type 表达它。

### 例子

- positive：OpenCode hook object 以 `obj` 穿过多个 application service，各模块自己读 `sessionID` / `tool` 并重复假设 shape。
- positive：JSON 被读成 `Dictionary<string,obj>`，workflow 深处继续按字符串字段做 domain 分支。
- near-miss：adapter 接收 `unknown`，decoder 校验后返回 `ProviderEvent`；raw payload 仅另存作诊断证据。
- counterexample：`UserId` 内部仍存 string，但 ingress 已验证并构造强类型；representation primitive 不等于 semantics primitive。

## 建议

### 现在该做什么

把 parse、validation、normalization、unchecked operation 全部收回真正拥有外部协议的 adapter，让它返回闭合的 domain/application value。Raw wire shape 可以属于 ingress；不应属于每个下游 policy function。

### 为什么重要

Cast 不会消除 uncertainty，只会把 uncertainty 搬到一个上下文更差的地方。

在 ingress，你还知道 provider、schema version、raw bytes、request identity、failure semantics。十层之后只剩一个可疑 `obj` 和一句“这里应该有这个字段”。于是 bug 在 provenance 已丢失之后才爆发，每个模块都重复支付同一笔理解成本。

Strong boundary 本质上是在压缩推理：把“这个 unknown object 大概有这些字段并满足这些组合”压成“这是 `CompletedToolCall`”。后面的代码就能思考 policy，而不是做 transport 考古。

### 修复策略

1. 找到 raw protocol owner 和真正 inward boundary。
2. 在那里 decode `unknown` / dynamic value。
3. 校验 required field、enum/case、cross-field invariant、version semantics。
4. 规范 casing、alias、nullable encoding、provider-specific status 等 transport accident。
5. 返回闭合 typed case；需要保留的 raw evidence 独立保存。
6. 删除 inward caller 的 dynamic lookup / cast / wire-field knowledge。

### 决策分支

- malformed shape 需要表示：返回 typed decode failure，不要返回 half-decoded object。
- 协议必须容忍未来 unknown variant：显式建 `Unknown of RawEvidence`，不要让每个 caller 自己看任意属性。
- reflection 是 framework glue 的真实需要：把 reflection 留在 glue 内，对内暴露窄 contract。
- parse 后仍只是过弱 primitive：再处理 `primitive-obsession`，不要把 nominal identity 和 decoding 混成一件事。

### 常见假修复

- 把 `any` 重命名为 `Payload` 就宣布 typed。
- 把 cast 集中进 `asFoo()` helper，然后 domain 到处调用。
- 只校验一个字段，cross-field contradiction 仍能进入内部。
- 造一个所有字段都 optional 的 DTO 号称 compatibility-safe，再让下游继续判断。
- 测试绕过 decoder，直接制造 production 不可能构造的 typed value。

### 验证

搜索所有 inward layer 的 dynamic lookup、unchecked cast/unbox、wire field name、重复 shape guard。它们应消失，或只剩明确 generic 的 infrastructure。

用真实 ingress 喂 malformed / unknown variant：必须在那里失败，或变成显式 `Unknown`。合法输入进入内部后，不应再需要 shape re-validation。

Invariant：**type uncertainty 只有一个 ingress owner，不得向内泄漏成重复 proof obligation。**

### 完成条件

外部不确定性被局部化；policy code 收到的是已证明事实而不是可能性袋子；transport representation 改名或重排时，semantic code 不会仅因字段访问变化而被迫一起修改。

### Nudge

动态数据可以进入系统，但不该取得永久居留权。

**在 provenance 和 raw shape 还在手边时一次花掉 uncertainty。向内流动的应该是事实，不是重复 cast 的希望。**

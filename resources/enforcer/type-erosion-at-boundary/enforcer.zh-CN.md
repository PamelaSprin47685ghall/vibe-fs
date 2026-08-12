# type-erosion-at-boundary — Enforcer

## Definition
当本应在外部边界被消耗掉的不确定性穿过 adapter，继续以 `any`、`obj`、reflection、unchecked cast、string-key map、弱 DTO 等形式在 domain/application 内部流动时，类型发生了 erosion。

根因是**没付清 parsing debt**：边界没有把“这个 raw shape 到底证明了什么”变成 constructor 能保证的类型，于是每个下游 caller 都要重新猜一遍。

## Governing Principle
外部数据当然可以弱。网络、plugin、文件、数据库 row、动态 Host API 总要在某处从不可信 bytes 开始。

问题不是 runtime parse；**runtime parse 本来就是 ingress 的职责**。问题是 parse 完以后，系统仍把 uncertainty 当作内部通行证。

好的 adapter 会消耗模糊性：校验 shape、规范表示、映射外部 variant、拒绝矛盾组合，然后只把下游有资格依赖的事实放出去。

坏的 adapter 只换名字：`any` 改叫 `Payload`，`obj` 换成 `Map<string,obj>`，或者造一个所有字段都 optional 的“typed DTO”。不确定性没消失，只是穿上了类型注解。

## Trigger When
以下情形触发：

- domain/application 根据 dynamic property、reflection、unchecked `unbox`/cast 做 semantic decision；
- 多个 inward caller 重复 `typeof` / null / property-exists 检查；
- policy code 知道 provider/JSON 的 wire field name；
- cast 的唯一证明是“Host 一般就发这个 shape”；
- malformed input 能穿过几层后才炸；
- test 直接造 dynamic bag，绕过 production decoder。

## Do Not Trigger When
- dynamic representation 完全困在 serializer/adapter，返回的是 validated domain value；
- ingress 需要 runtime validation——这不是缺陷，**让未校验输入继续向内走**才是；
- reflection 本身就是 generic framework boundary 的真实能力，且 domain semantics 不依赖它；
- raw payload 被保留作 evidence，但控制流使用的是另外构造出的 typed interpretation。

## Distinguish From
`weak-boundary-parsing` 管“边界虽然 parse 了，但校验仍不完整或 fail-open”；`primitive-obsession` 管静态 primitive 丢失 domain identity；`stringly-typed-error` 是人类 prose 被当机器 identity 的特例。

Tie-break：主要问题是弱/dynamic representation 泄漏进内部，用本规则；decoder 存在但接受坏 shape，用 `weak-boundary-parsing`；已经是静态 `string` 但 sibling concept 可互换，用 `primitive-obsession`。

## Decision Procedure
找到**最后一个合法需要 raw representation 的地方**。从它往内，所有代码都应该拿到一个 constructor 已证明 assumptions 的类型。

对每个 cast / dynamic lookup 问：这里偷偷假设了哪条 proposition？把这条 proposition 的证明搬回 adapter，并让 returned type 表达它。

## Examples
- positive：OpenCode hook object 以 `obj` 穿过多个 application service，各模块自己读 `sessionID` / `tool` 并重复假设 shape。
- positive：JSON 被读成 `Dictionary<string,obj>`，workflow 深处继续按字符串字段做 domain 分支。
- near-miss：adapter 接收 `unknown`，decoder 校验后返回 `ProviderEvent`；raw payload 仅另存作诊断证据。
- counterexample：`UserId` 内部仍存 string，但 ingress 已验证并构造强类型；representation primitive 不等于 semantics primitive。

## Nudge
动态数据可以进入系统，但不该取得永久居留权。

**在 provenance 和 raw shape 还在手边时一次花掉 uncertainty。向内流动的应该是事实，不是重复 cast 的希望。**

# primitive-obsession — Enforcer

## 定义
当一种只说明**“值怎么存”**的 primitive representation，被迫承载 domain correctness 真正依赖的 distinction 时，就是 primitive obsession。

最典型的情况，是若干 `string`、`int`、`decimal` 在现实中绝不可以互换，程序却认为它们完全兼容：`UserId` 与 `OrderId`、cents 与 basis points、absolute path 与 workspace-relative path、trusted capability 与 untrusted token、digest 与 arbitrary text。

问题不是 primitive 本身。Primitive 是非常好的 representation。真正 defect 是：在 identity 明明会改变 behavior 的 boundary，representation 把 identity 擦掉了。

## 支配原则
Type 是 construction 完成后，所有 caller 都能免费使用的一条 proposition。

`string` 只能证明“这是一段文本”。它不能证明“这段文本命名一个 session”“这是验证过的 SHA-256 digest”“这是 authority 已接纳的 capability”。这些 proposition 一旦重要，却只存在 variable name/comment 里，compiler 就无法拒绝 category error，每个 consumer 都必须靠记忆遵守隐形 law。

但 strong typing 也很容易变成表演。把每个 string 都套成 one-field type，却不改变 construction、validation、boundary semantics，只是在移动标点。Domain type 只有在能拒绝真实 substitution、集中真实 invariant、或明确真实 boundary 时才值得存在。

## 何时触发
当共享同一 primitive representation 的 values 有不同 semantic identity，而且错误 sibling value 能穿过 meaningful boundary 而不被 type/construction 拒绝时触发。例如：

- 多种 ID 都是 plain string，sibling ID 可以传错 API；
- money、percentage、duration、byte count、timestamp、unit 共用 number，却语义不可互换；
- raw path string 模糊 absolute/relative/normalized/workspace-scoped distinction，影响安全；
- validated 与 unvalidated input 在 admission 后仍用同一个 type；
- capability/security token 进入 domain 后仍与 arbitrary text 无区别；
- hash/digest/version identifier 被当 generic string，到处重复 reparse；
- call site 有多个相邻 same-typed primitives，必须靠参数顺序记住 meaning。

## 不应触发
- 这个 boundary 真正只把 value 当 generic text/number，domain identity 在这里无关，例如 semantic decision 已完成后的 log rendering。
- Primitive 只存在于很小 local expression/helper，不会跨 semantic boundary 产生混淆。
- Value 只是 transport/wire form，进入 policy code 之前立刻被 parse 成 strong domain value。
- 两个概念虽然共 representation，但 contract 本来就允许互换。
- 新 type 不能拒绝任何 substitution、集中任何 validation/unit/ownership 信息。Newtype 数量不是质量指标。

## 与相邻规则区分
`type-erosion-at-boundary` 是 strong/static information 因 dynamic/unchecked representation 泄漏而丢失。`primitive-obsession` 在完全 static typed code 里也能发生：只是 static type 本身太弱，表达不了 domain identity。

`boolean-blindness` 是 boolean 特别容易丢 named choices 的子类。`illegal-state-representable` 关注 impossible combinations，而不是同 representation sibling values。`misleading-name` 是 vocabulary 错位，type 可能仍然 sound。

Tie-break 看真正被破坏的 proposition：如果 bug 是“错误 semantic category 的 value 仍然 type-check”，用本规则。

## 判定程序
命名 boundary，然后做 substitution test：

> 我能不能传入另一个 primitive representation 完全一样、但现实 domain 明确禁止的 sibling value，而 construction/type checking 仍接受？

如果可以，再问合法 value 与错误 value 到底差哪条 proposition：identity、unit、validation state、trust level、namespace、coordinate system、lifecycle stage。

这条 proposition 就是候选 type boundary。

最后做 anti-theater 检查：新 type 真能拒绝 substitution 或集中 invariant 吗？如果不能，不要为了显得 domain-driven 而造 wrapper。

## 例子
- positive：`loadSession(userId: string)` 完全能 compile，因为 `UserId` 与 `SessionId` 都是 string。
- positive：`retryAfter: number` 有时代表 milliseconds、有时代表 seconds；程序不 crash，只表现成“神秘延迟”。
- positive：filesystem delete API 在别处 validation 之后又接受 arbitrary string，于是 raw path 可以绕过 validated workspace path concept。
- positive：`ValidatedEmail` 创建后立刻 `.value` 回 string，所有 downstream APIs 仍只收 string，validation state 实际被擦掉。
- near-miss：JSON serializer 接受任意 string，因为 semantic identity 已决定，serialization 有意保持 generic。
- near-miss：某语言只能做 `UserId = string` alias，alias 不具 nominal separation；这可能很弱，但规则应攻击真实危险 boundary，而不是 alias syntax 本身。
- counterexample：`SessionId`、`UserId`、`WorkspacePath` 有 distinct construction API，不能在 domain call 中互换。

## Nudge
Representation 回答“这些 bits 是什么形状”。

Domain type 回答“这些 bits 代表什么事实”。

只有程序真的需要知道区别时才引入后者；一旦引入，就让这个区别再也不会被随手忘掉。

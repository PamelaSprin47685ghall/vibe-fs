# primitive-obsession — Main

## 现在该做什么
把 semantic distinction 放回真正拥有 value 的 type boundary。

创建 distinct domain type、validated constructor、unit-bearing value 或同等强度的 representation，让危险 sibling substitution 直接失败。Inward-facing callers 全部迁到 strong type；raw primitive conversion 只保留在真实 ingress/egress adapter。

不要开始 newtype 人口普查。只修那些 identity 被擦掉后确实会造成 category error 的 boundary。

## 为什么重要
Primitive obsession 让 correctness 依赖记忆。

人看到 `accountId`、`orderId`、`sessionId`，自然理解三个 concept。Type checker 如果只看到三个 strings，只会看见同一个 concept 重复三次。正是在这个差距里，category mistake 很容易穿过 review：value 看起来合法，serialization 成功，log 也正常，直到很远以后碰错 object 才失败。

Unit 更危险，因为错值往往仍然“数字上合理”。Milliseconds 当 seconds 不一定 crash，只会制造神秘 latency；cents 当 dollars 不违反 arithmetic，只违反 meaning。

好的 domain type 会把一个昂贵 distinction 变成未来每次调用都免费的安全条件。

## 修复策略
从 boundary 向内修：

1. 找出共享 primitive、但不可互换的 sibling concepts；
2. 写出各自必须携带的 proposition——identity、unit、validation、trust、namespace、coordinate frame；
3. 建立唯一 construction boundary 来证明这条 proposition；
4. domain/application code 全程使用 strong value；
5. 只有 external protocol/storage/runtime 真要求 primitive 时才转换；
6. 删除 strong type 已经取代的 downstream reparsing/revalidation；
7. 加 compile-time/constructor test，明确证明 sibling substitution 会失败。

Type API 保持小而直接。Nominal distinction 不需要自动升级成 object hierarchy，除非 behavior 真属于它。

Numeric units 如果语言有自然的 unit-of-measure，就优先使用；否则 distinct value type + explicit conversion 也能守住 law。

## 决策分支
- **Sibling identifiers 容易传错：**各自拥有 nominal identity，并在 ingress construction。
- **Same number、different unit：**编码 unit 或 distinct value types；conversion 必须显式命名。
- **Validated vs raw input：**constructor 返回 strong validated type，不要立刻 erase 回 primitive。
- **Trusted/capability-bearing value：**admitted capability 与 untrusted token text 分离；construction 应对应真实 authority check。
- **这个 boundary 真只需要 generic value：**保持 primitive。Strong typing 应跟 semantic distinction 走，不跟潮流走。
- **语言无法低成本 nominally separate：**用最强可用 constructor/module boundary + tests；不要假装一个 alias 能拒绝它实际不会拒绝的 substitution。

## 常见假修复
- 只 rename variables，所有 API 仍 primitive typed。名字帮助 reader，不保护 substitution。
- 造 one-field wrapper，又到处 `.value`，domain API 最终仍只吃 primitive。
- 每个 caller 都加 `assert(isUserId(x))`，而不是 admission 时证明一次。
- 造“universal ID” wrapper：string + `kind`，把原 category error 往后一层重建。
- Repository 每个 string/number 都包起来，不管 risk。Ceremony 不是 semantic precision。
- 用 overloaded operator 隐藏 implicit unit conversion，让 type 看起来强、meaning 却仍可 silent change。

## 验证
重新尝试原本的错误：

- `OrderId` 传给 `AccountId`；
- seconds 传给 milliseconds；
- raw input 传给 validated input；
- arbitrary token 传给 admitted capability。

程序应在 compile/construction/boundary time 拒绝，而不是在深层 policy code 才发现。

同时验证 adapter 仍能忠实 serialize/deserialize external primitive form。

Invariant：

> 在 identity 混淆会改变 behavior 的每个 boundary，domain identity 都不会丢失。

## 完成条件
Program 不再依赖 parameter name、comment 或人的小心，来区分现实里本来就不同的 values。

同样重要：没有新增一个唯一成就是“让 type 列表更长”的 wrapper。

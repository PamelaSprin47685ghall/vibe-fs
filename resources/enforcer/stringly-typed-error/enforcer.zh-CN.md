# stringly-typed-error — Enforcer

## Definition
当机器行为取决于**给人看的错误句子怎么写**时，error identity 已经被 stringly typed。

根因是把 semantic identity 塞进 prose：retry、authorization、recovery、routing、status 通过 substring、regex、exact text、标点、localization 或 exception message 决定，而不是通过 producer 拥有的稳定 typed case/code。

## Governing Principle
人需要解释，机器需要身份。

这两个 surface 的演化方向正好相反。人类文本应该越来越清楚、带更多上下文、可以本地化；机器 identity 应该闭合、明确、稳定。把两件事压在一个字符串里，文案优化就会变成 protocol breaking change。

`message.includes("timeout")` 不是 error model，而是在偷偷解析一份从未承诺稳定的 prose。

更危险的是词语碰撞：一条错误可能提到 “timeout”，却是在解释“本次不是 timeout”；一个 regex 无法从单词出现恢复 producer 原本应该给出的事件身份。

## Trigger When
以下情况触发：

- control flow 匹配 `error.message`、rendered TOML/prose、stderr、log text、provider wording；
- 切换 locale 可能改变 retry/routing；
- test 必须冻结整句错误文本，因为 caller 暗中依赖它；
- 多个模块各维护一套同类 error regex/substring；
- provider error 以文本向内传播，并在不同层重复分类；
- 改一个标点就可能让 recovery 走另一条路。

## Do Not Trigger When
- 机器先匹配 typed error case，再给人渲染 prose；
- 外部 provider 根本不给 structured identity，adapter 被迫读文本，但只在一个边界做一次、映射成 typed internal case，并保留 raw text 作 evidence；
- copy 本身就是产品要求，test 单独检查文案，但控制语义不依赖文案；
- log 只是记录，没有机器后来再解析它做决策。

## Distinguish From
`weak-boundary-parsing` 管一般 external payload shape；`expected-failure-as-exception` 管 expected outcome 走错控制通道；`type-erosion-at-boundary` 管 dynamic representation 泄漏。

Tie-break：如果**人类 prose 本身被当成机器 identity**，用本规则；error 已有稳定类型，只是本应作为普通结果却被 throw，用 `expected-failure-as-exception`。

## Decision Procedure
对每个读字符串的 branch，先写出它真正想识别的 semantic distinction。再问：哪个 producer 最早在**不读 prose**的情况下就知道这件事？在那里定义 typed case/code。

若 upstream 只能给文本，就在 adapter 一次映射，并保留 `Unknown raw`。

测试时只改错误句子的措辞。如果 identity 没变但控制流变了，stringly protocol 还活着。

## Examples
- positive：retry 逻辑靠 `e.message.toLowerCase().includes("timeout")`。
- positive：plugin 从 tool output 的 “permission denied” 文字决定是否升级 authority。
- near-miss：上游无 structured code，一个 adapter 把 documented phrase 映射成 `RateLimited | AuthFailed | Unknown raw`，内部再也看不到那些 phrase。
- counterexample：`Timeout { deadline; operation }` 驱动 recovery，而 formatter 可自由输出 EN/zh-CN。

## Nudge
如果改一句话能改变控制流，这句话已经意外变成 API。

**机器用 case，人用 prose。别让一句错误文案同时冒充 protocol identity。**

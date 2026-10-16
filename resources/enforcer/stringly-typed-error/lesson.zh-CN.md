# stringly-typed-error — Lesson

## 评价

### 定义

当机器行为取决于**给人看的错误句子怎么写**时，error identity 已经被 stringly typed。

根因是把 semantic identity 塞进 prose：retry、authorization、recovery、routing、status 通过 substring、regex、exact text、标点、localization 或 exception message 决定，而不是通过 producer 拥有的稳定 typed case/code。

### 支配原则

人需要解释，机器需要身份。

这两个 surface 的演化方向正好相反。人类文本应该越来越清楚、带更多上下文、可以本地化；机器 identity 应该闭合、明确、稳定。把两件事压在一个字符串里，文案优化就会变成 protocol breaking change。

`message.includes("timeout")` 不是 error model，而是在偷偷解析一份从未承诺稳定的 prose。

更危险的是词语碰撞：一条错误可能提到 “timeout”，却是在解释“本次不是 timeout”；一个 regex 无法从单词出现恢复 producer 原本应该给出的事件身份。

### 何时触发

以下情况触发：

- control flow 匹配 `error.message`、rendered TOML/prose、stderr、log text、provider wording；
- 切换 locale 可能改变 retry/routing；
- test 必须冻结整句错误文本，因为 caller 暗中依赖它；
- 多个模块各维护一套同类 error regex/substring；
- provider error 以文本向内传播，并在不同层重复分类；
- 改一个标点就可能让 recovery 走另一条路。

### 不应触发

- 机器先匹配 typed error case，再给人渲染 prose；
- 外部 provider 根本不给 structured identity，adapter 被迫读文本，但只在一个边界做一次、映射成 typed internal case，并保留 raw text 作 evidence；
- copy 本身就是产品要求，test 单独检查文案，但控制语义不依赖文案；
- log 只是记录，没有机器后来再解析它做决策。

### 与相邻规则区分

`weak-boundary-parsing` 管一般 external payload shape；`expected-failure-as-exception` 管 expected outcome 走错控制通道；`type-erosion-at-boundary` 管 dynamic representation 泄漏。

Tie-break：如果**人类 prose 本身被当成机器 identity**，用本规则；error 已有稳定类型，只是本应作为普通结果却被 throw，用 `expected-failure-as-exception`。

### 判定程序

对每个读字符串的 branch，先写出它真正想识别的 semantic distinction。再问：哪个 producer 最早在**不读 prose**的情况下就知道这件事？在那里定义 typed case/code。

若 upstream 只能给文本，就在 adapter 一次映射，并保留 `Unknown raw`。

测试时只改错误句子的措辞。如果 identity 没变但控制流变了，stringly protocol 还活着。

### 例子

- positive：retry 逻辑靠 `e.message.toLowerCase().includes("timeout")`。
- positive：plugin 从 tool output 的 “permission denied” 文字决定是否升级 authority。
- near-miss：上游无 structured code，一个 adapter 把 documented phrase 映射成 `RateLimited | AuthFailed | Unknown raw`，内部再也看不到那些 phrase。
- counterexample：`Timeout { deadline; operation }` 驱动 recovery，而 formatter 可自由输出 EN/zh-CN。

## 建议

### 现在该做什么

用 producer 或 protocol adapter 拥有的闭合 error identity 替换 message parsing。内部只按 typed case/code 分支；人类可读文本在 semantic case 已知之后再渲染。

如果 upstream 只有 prose，就在**唯一 external boundary** 做一次分类，保留 raw message 作 evidence，并返回 `RateLimited | Unauthorized | Timeout | Unknown raw` 一类内部结果。

### 为什么重要

人类文案本来就应该改变：诊断会加上下文，localization 会整句变化，provider 会改措辞，标点也会调整。这些改进都不应该偷偷改变 retry、authorization、fallback 或 recovery policy。

Stringly error 把 copyediting 变成 control-plane mutation。

而且字符串分类很容易过度自信：一句话里出现 “timeout” 不等于事件身份就是 timeout。若 producer 没给 structured identity，adapter 最多只能做一个有边界、有 `Unknown` 的启发式分类，不能假装 prose 已经是可靠协议。

### 修复策略

1. 找出所有按 error prose 做机器分支的地方。
2. 命名这些 branch 真正需要区分的 semantic outcome。
3. 在最早知道该区分的 owner 定义闭合 case/code。
4. 外部 text-only error 只在 adapter 映射一次，并保留 `Unknown raw`。
5. formatting/localization 全部移到外层。
6. 控制流测试改断言 typed identity；只有 copy 本身是需求时才单独断言 prose。

### 决策分支

- 自己控制 producer：直接输出 structured identity。
- 不控制且 upstream 只有文本：把不可避免的 classifier 困在一个 adapter，并显式保留 uncertainty。
- outcome 本来就是 expected ordinary control flow：identity 修好后再看是否还有 `expected-failure-as-exception`。
- string 纯诊断、没人解析：不要动。

### 常见假修复

- 把所有 regex 搬进 `ErrorUtils` 就宣布解决。
- 冻结当前英文句子当永久 unofficial protocol。
- provider 每改一次文案，就继续追加 substring special case。
- 把所有 unknown message 都硬映射成某个 confident case；假确定性比 `Unknown` 更危险。
- typed identity 已存在后，控制流 test 仍冻结 exact human message。

### 验证

保持 typed case 不变，改标点、加上下文、切换 EN/zh-CN：机器行为必须完全相同。

再保持 prose 很相似，只改变 typed case：控制行为必须跟 case 走，而不是跟关键词碰撞走。

Invariant：**control semantics 与 human wording 独立。**

### 完成条件

内部机器决策不再依赖 rendered error prose；不可避免的外部文本分类只有一个明确 owner；错误文案可以自由改进和本地化而不影响 recovery、authorization、routing。

### Nudge

如果改一句话能改变控制流，这句话已经意外变成 API。

**机器用 case，人用 prose。别让一句错误文案同时冒充 protocol identity。**

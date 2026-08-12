# framework-tax — Main

## 现在该做什么
把 operation 剥回 host language 与 domain 真正需要的概念，然后只在能购买 concrete boundary capability 的地方重新引入 framework machinery。

不要再加一层 wrapper，只为了让 caller 看不见 framework tax。真正还债的方式，是删除不必要的 framework ownership。

## 为什么重要
Framework tax 最危险的地方，是每一小块单独看都很“合理”。

一个 interface 好像没成本；一个 provider 很 idiomatic；一个 decorator 很方便；一个 config entry 很标准；generated class 看起来还是“免费的”。但 architecture cost 会累积：behavior 最终变成一堆“为了 consistency 而存在”的 construct 共同涌现出来的结果。

系统于是变成：通过 framework 操作很容易，不通过 framework 理解却很困难。这已经不是普通 dependency，而是问题模型本身被 framework 占领。

Framework churn 只是让 debt 更显眼。即使 framework 永远不换，debugging、testing、onboarding、static reasoning、refactoring 每天都在交这笔税。

## 修复策略
找到 framework boundary，把它向外推：

1. 用普通 domain input/output 与 explicit effects 表达 core operation；
2. 找出真正携带 semantics 的 framework feature：transaction、request cancellation、host lifecycle、plugin discovery、authentication context 等；
3. 把这些 semantics 留在窄 adapter/port；
4. 删除仅因 convention 要求而存在的 registration/interface/provider；
5. ambient framework context 只用到很小一部分时，改传 explicit values；
6. 不让 framework exception/entity/DTO 泄漏进 domain；
7. decision 本身不依赖 framework 时，core tests 不应必须 boot framework。

Dynamic substitution 真的存在，就保留 abstraction。只有一个 implementation、没有 independent consumer 时，一个 named function/module 往往已经足够。

## 决策分支
- **Framework 拥有 real protocol/lifecycle：**保留 adapter，并明确 boundary。
- **Framework object 只因方便而漏进 core：**在 ingress extract/translate，只传 semantic values。
- **DI abstraction 只有一个 implementation、无 runtime substitution：**collapse 到 direct construction/reference；若 test isolation 需要 effect seam，则保留最小 explicit port。
- **Cross-cutting behavior 散在 hooks/middleware：**选择一个 semantic owner，或把 ordering/interaction 明确建模，不要依赖 framework invocation folklore。
- **Generated code 只是 declaration mirror：**把它当 build artifact，不要让工程师再把它当另一个 domain layer 思考。
- **移除 framework 会迫使你重写大量成熟、正确的 platform machinery：**保留。Framework 存在不等于 framework tax。

## 常见假修复
- 在 framework-heavy code 外再加一个“service layer”，但 real decisions 仍留在 hook/entity/controller。
- 自己发明 mini-framework 来抽象现有 framework。
- 每个 framework type 都造一个一对一 project type，却没有 semantic translation。
- 纯粹为了 mocking 造 interface。优先 test stable behavior，或注入真正 effect boundary。
- 一刀切禁止 framework API，然后低质量重造成熟 platform 能力。目标是 proportional ownership，不是 purity theater。
- 只是把 registration/config 搬到另一个 directory，就说 tax 下降了。

## 验证
一个 core behavior change 现在应主要用 domain terms 就能解释和 test。

检查：

- framework types 停在 intentional edges；
- 替换 framework adapter 不需要重写 domain decisions；
- 每个剩余 registration/hook/config item 都能命名自己购买的 capability；
- 重要 behavior ordering 不再依赖 undocumented framework magic；
- framework behavior 真正属于 contract 的 boundary，仍由 integration/e2e tests 覆盖。

Invariant：

> Framework machinery 只承担 framework responsibilities；domain machinery 只承担 domain meaning。

## 完成条件
Framework 重新只是系统使用的工具，而不是系统被迫用来解释自己的语言。

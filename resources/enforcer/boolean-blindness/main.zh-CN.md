# boolean-blindness — Main

## 现在该做什么
把承载 named choice 的 booleans 替换成真正列出这些 choices 的 type。

Flag cluster 则直接建模**合法 state space**，不要继续保留一个巨大 Cartesian product，再要求每个 consumer 记住哪些组合是虚构的。

真正 predicate 继续用 bool。目标是 semantic precision，不是禁用 boolean。

## 为什么重要
Boolean-heavy API 让 meaning 编码很便宜，恢复却很昂贵。

Writer 刚看完 signature，当然知道 `true` 是什么。Reader 几个月后看到 `configure(false, true, false)`，只能去别处重建 vocabulary。IDE 可以显示 parameter name，但 program 仍然允许 literals 被调换、contradictory combinations 被构造。

Flag cluster 更糟。每增加一个 boolean，representable state space 就翻倍，无论 domain 有没有翻倍。Invalid combinations 随后进入 persistence、tests、branching、migration、incident diagnosis，系统开始为用户永远不可能合法产生的 worlds 写 defensive code。

Named sum/enum/capability set 会让 valid alternatives 可见，也让未来变化有明确位置。

## 修复策略
从 domain alternatives 出发，不从现有 flags 出发：

1. 枚举 meaningful modes/states/actions；
2. 判断它们 mutually exclusive、independent，还是 hierarchical；
3. mutually exclusive 用 sum/enum；
4. state-specific data 跟着对应 case；
5. genuinely independent combinations 才用 capability/set model；
6. external wire/storage 必须用 boolean 时，在 boundary translate；
7. 删除 old boolean overload，防止 caller 绕过 named model；
8. 使用 exhaustive matching，让新增 alternative 产生 visible obligation。

Single parameter 如果承载 policy choice，`true/false` 会隐藏 intent，即使只有两个 case，也值得用 named two-case type。

## 决策分支
- **Mutually exclusive lifecycle states：**一个 closed state type，不要多条 `isX`。
- **Two-way policy alternative：**call-site meaning 重要时用 two-case enum/union。
- **Independent capabilities：**如果每个组合都 meaningful，可以用 set/bitset，但 capability 必须有名字。
- **Simple observation/predicate：**保留 `bool`；`isEmpty` 不需要强行 `Empty | NonEmpty`，除非状态后来承载 data/behavior。
- **External protocol 就是 boolean：**ingress decode 成 named domain choice，egress encode 回去。
- **Storage 已存在历史 contradictory flags：**先定义 migration/validation policy，不要 silent reinterpret。

## 常见假修复
- 只把 `flag` rename 成 `isSpecial`，caller 仍到处 literal。
- Comment 写明哪些 combination illegal，type 继续接受。
- 每个新 mode 再加一个 boolean，继续指数爆炸。
- 把 boolean 换成 free-form strings `"read"/"write"`，从 boolean blindness 变 stringly typing。
- “为了 convenience”保留 legacy boolean overload，让新 code 随时绕过 repair。
- 把 genuinely independent binary facts 塞进 giant enum，枚举所有组合。Independence 真实时应使用 named capabilities，而不是人为耦合。

## 验证
同时证明 readability 与 state-space closure：

- 搜索 policy/mode 位置 unexplained boolean literals；
- 尝试构造过去 contradictory flag combinations；
- 新 case 加入时 exhaustive match 应强制 review；
- wire/storage adapter 保持 external compatibility，但不让 bool 泄漏 inward；
- genuinely predicates 仍保持简单，没有 ceremony wrapper。

Invariant：

> Program 能表示的 policy/state choices，正好等于 domain 真正命名的 choices。

## 完成条件
Call site 不靠 editor hints 也能说清自己在做什么；type 不再承认那些唯一 semantics 是“理论上不该发生”的组合。

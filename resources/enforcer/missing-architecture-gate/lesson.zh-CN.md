# missing-architecture-gate — Lesson

## 评价

### 定义

关键 architecture rule 若机器可以廉价、确定地判断，却长期只靠 reviewer 记住，就是 missing architecture gate。问题不是“CI 规则越多越好”，而是**本可机械拒绝的 forbidden state 被委托给未来注意力**。

### 支配原则

没有 enforcement 的 invariant 是对未来注意力的请求。Architecture 尤其脆弱，因为违规在局部很方便：import 一个 internal module 常常让当前任务更容易，同时不可见地恶化全局 graph。静态 gate 把负担从记忆移到机制，让 forbidden state 在被引入的那一刻就昂贵，而不是等到日后 redesign 才昂贵。

### 何时触发

- forbidden import/layer edge 每次靠人工 review 抓；
- ownership/generated-file rule 有精确 structural predicate，却没有 standard check；
- 同一违规反复出现，原因只是局部写法很方便；
- architecture statement 已稳定，但 repository 对违反保持沉默。

### 不应触发

- 规则本质是高语义判断，静态扫描会大量 false positive；
- 一次性 review 意见还没成为 standing invariant；
- build/type system 已经机械禁止；
- 纯风格偏好无架构后果。

### 与相邻规则区分

`missing-invariant-documentation` 缺少规则的持久陈述；`false-gate` 有一个实际不会失败的 check。Tie-break：如果 invariant 已知且可机械判定、却交给纪律，用本规则；如果缺的是措辞，用 `missing-invariant-documentation`；如果 check 存在但无效，用 `false-gate`。

### 判定程序

能否写出一个便宜、deterministic、low-noise 的 predicate 来识别违反？能，而 merge pipeline 不执行它，就是把机器能做的工作留给记忆。

### 例子

- positive：reviewers 反复抓跨包 `internal/` imports，但 CI 没有 import-graph check。
- near-miss：边界是“不要把 domain meaning 泄漏进 adapters”，没有廉价静态 check 能判定。
- counterexample：既有 dependency-cruiser rule 已在 CI 运行的同一命令里让 forbidden edges 失败。

## 建议

### 现在该做什么

把 rule 写成最小 deterministic check，接入 CI/标准 `check` 入口，并用 known-bad fixture 证明它真的能红。若 property 无法低噪机械判断，就不要造假 gate；清楚记录 invariant，留给 human judgment。

### 为什么重要

纯 review enforcement 把成本放错时间：违规提交时很便宜，几个月后 dependency graph 腐烂再治理非常贵。机械 gate 把成本前移到第一条 forbidden edge 出现时。

### 修复策略

精确定义 forbidden graph edge 或 structural condition，实现最小的 static check，并包含一个 known-bad fixture 或 self-test 证明 gate 能变红。

### 决策分支

- 若便宜、deterministic 的 predicate 能识别 forbidden graph/ownership shape，把它加进 standard gate，带 known-bad fixture。
- 若规则是语义性的、会 fail open 或淹没在 false positives 里，记录 invariant，而不是假装 gate 存在。

### 常见假修复

- 增加文档/checklist，机器仍可接受违规。
- gate 扫空目录或 fail-open；这会变成 `false-gate`。
- 只放一个没人运行的 local script。
- 为不可机械判断的语义原则写粗糙 regex，制造大量误杀。

### 验证

同一条 standard command 对代表性 known-bad fixture 必须 red，对合法结构保持 low-noise green。

### 完成条件

可机械决定的关键 architecture invariant 不再依赖 reviewer 记忆；不可机械决定的规则也不假装已经自动化。

### Nudge

Architecture 最脆弱的地方恰好是违规通常“局部更方便”。能让机器拒绝的边界，不要要求所有未来人永远自律。

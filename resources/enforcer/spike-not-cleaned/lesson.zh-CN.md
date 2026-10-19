# spike-not-cleaned — Lesson

## 评价

### 定义

Spike 的成功只证明“这条路可能走得通”，不证明“这就是应该长期维护的 architecture”。Spike-not-cleaned 是把为学习速度而允许的 hardcode、global state、fake boundary、缺失 failure/recovery contract 直接接进 production。

Prototype 是 epistemic instrument；production 是长期承诺。把前者直接晋升，等于让实验时期的偶然假设因为“demo 成功了”获得永久地位。

### 支配原则

Prototype 为认知速度而优化：它问“这能行吗？”Production 为持久正确性而优化：“在哪些 contract 下它会持续工作？”混淆这两个目标会把收集证据的 scaffold 晋升为设计。Hard-coded inputs、跳过的 failure semantics、global state 与被敷衍的 ownership 于是像被认真选择过一样存活下来。

### 何时触发

- POC 代码直接成为 shipping implementation；
- hardcoded/in-memory/fake transport 只被换成真实 endpoint，其余假设未重审；
- failure/cancellation/recovery/ownership 在 spike 中被跳过，production 仍跳过；
- “先让它跑起来”的 shortcut 没有被重新证明；
- prototype file 名没了，但 prototype model 原封不动进入正式层。

### 不应触发

- spike 保持隔离、不可进入 release path；
- 探索后发现最小设计本来就足够，且所有 production contracts 已明确验证；
- 临时辅助脚本遗留在正式实现旁，应归 `leftover-scaffolding`；
- 某一局部 workaround 不是学习 prototype，应归 `dirty-hack`。

### 与相邻规则区分

`leftover-scaffolding` 管真实实现旁的临时支持 artifact；`dirty-hack` 是从来不是学习 spike 的局部 workaround；`half-finished-refactor` 在必需迁移后留下双架构。本规则是认知 prototype 被晋升进系统持久设计。Tie-break：如果 prototype 假设以 architecture 形态 shipping，这个案子归本规则。

### 判定程序

列出 spike 阶段曾允许的所有假设。哪些只是为了快速回答 feasibility，哪些已被生产证据重新证明？未证明的 assumption 不能因为代码已经存在就自动毕业。

### 例子

- positive：hardcoded in-memory spike 被接上 production HTTP，仍跳过 failure、ownership 与 recovery。
- near-miss：spike 目录存在，但 shipping binary 不引用它。
- counterexample：实验 idea 在真实 boundaries 后重写，prototype files 被删除。

## 建议

### 现在该做什么

把 spike 当研究笔记而不是实现基线：提取它证明的事实，列出 temporary assumptions，然后围绕真实 boundary、ownership、failure、recovery、security、lifetime 重建最小 production design。知识迁移完后删 prototype。

### 为什么重要

Prototype 优化的是学习速度，因此“先 hardcode”“先全局变量”“先不处理 crash”在那个阶段完全合理。问题出在这些选择没有经过 production 标准重新决策，却因为代码可运行而被误认成设计结论。

### 修复策略

提取核心 idea，丢弃 hard-coded 与 exploratory structure，然后围绕真实 boundaries 与 invariants 重建最小 production design。知识迁移完成后删除 spike。

### 决策分支

- 若 spike 已在 production path 上，围绕 contracts 重建并删除实验 shortcuts。
- 若 spike 保持隔离，让它离开 release path，或在 lesson 记录后删除。
- 若 wrapper 只是调用 spike，用一份 production implementation 替换 wrapper+spike 对。

### 常见假修复

- 只 rename `spike` 文件并搬进 `src`。
- 给 prototype 加更多 tests，却不重审 model/boundary。
- 一边保留 spike path，一边另写 production path，形成 dual architecture。
- 认为“rewrite 浪费”所以所有 exploratory choices 都必须保留。

### 验证

每个曾经的 spike assumption 都应有一个结局：被真实 contract 证明、被 production design 删除，或被明确建模成限制。不能再有“当时为了快所以这样”的 silent dependency。

### 完成条件

production 保留 spike 学到的知识，不保留 spike 为了学习而欠下的假设与捷径。

### Nudge

Demo green 证明 idea 有可能，不证明 prototype 有资格成为 architecture。

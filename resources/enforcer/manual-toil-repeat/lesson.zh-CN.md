# manual-toil-repeat — Lesson

## 评价

### 定义

Manual toil repeat 的病，不是“人还在敲命令”，而是一套**机械、可判定、反复出现、失败有真实代价**的步骤仍然靠人类短期记忆维持。

第一次手工操作是学习；第二三次可能仍在探索。等步骤已经稳定成 ritual——检查同样文件、复制同样字段、跑同样命令、按同样规则更新 baseline、做同样 release prep——继续手工就不再买理解，只是在重复支付注意力税。

### 支配原则

重复揭示算法。当同一条人类序列被执行几次之后，组织就是在用注意力当解释器，运行一份它拒绝写下来的程序。Human execution 增加 variance，消耗稀缺的 judgment，去执行一件“judgment 已不再需要”正是其定义特征的工作。

### 何时触发

以下情形触发：

- 每次 release 都有同一套复制/校验/生成步骤；
- PR review 反复人工搜索同一种机械违规；
- migration/fixture/schema 更新每次手工同步几处；
- “记得还要做 X” 是流程可靠性的关键；
- 漏一步会造成生产/兼容/证据问题，而机器完全能判断是否漏；
- 新人必须背 checklist 才能不破坏 contract。

### 不应触发

不要误杀 judgment。架构取舍、semantic review、RuleBook prose 质量、migration ambiguous meaning、是否值得重构，本来就不能因为“重复出现”自动脚本化。Automation 只适合 deterministic mechanics，不应把需要理解的选择伪装成规则。

### 与相邻规则区分

与 `missing-architecture-gate` 区分：那条已经有明确 architecture invariant，却缺 machine enforcement；本规则更广，可能只是 workflow 自动化机会。与 `unrecorded-decision` 区分：toil 是重复执行，decision 是为什么这样做。

### 判定程序

判定问题：**一个新工程师如果完全理解规则，执行时还需要创造 judgment 吗？** 如果不需要，只需准确重复步骤，machine 比 human 更适合拥有这份责任。

### 例子

- positive：每次 release，一个人复制同样五个文件、编辑同样 version strings、跑同样 checklist。
- near-miss：一次性 migration 还在被发现中；下一次会不同。
- counterexample：一条 maintained command 接受同样 inputs、产出同样 outputs；人类只 review exceptions。

## 建议

### 现在该做什么

把重复机械步骤搬给 machine，把真正 judgment 留给人。

先把 ritual 拆成两类：

- deterministic：输入固定后，正确结果唯一；
- judgmental：需要理解语义、权衡 tradeoff、处理 ambiguity。

只自动化前者。把它接进正常开发入口：build/check/generator/migration tool/release script，而不是再造一个“记得偶尔手工运行”的脚本。

### 为什么重要

Manual repetition 是存在 muscle memory 里的可执行知识。它靠消耗注意力来扩展，靠跳过步骤、局部差异、遗忘 context 来退化。Automation 让 procedure 可检查、可重复、可 review、人人可用。

### 修复策略

捕获最小的稳定算法，保持 configuration 显式，并集成进正常 workflow。在真正需要 judgment 的决定上保留 human review，而不是自动化 ambiguity。

### 决策分支

- 如果 inputs、steps、outputs 稳定且已反复出现，把 procedure 编码成 maintained tool。
- 如果每个实例仍需要未陈述的 judgment，不要自动化；等算法存在再说。

### 常见假修复

常见假修复：

- 把 checklist 写得更详细；
- 新增一个脚本，但仍靠人记得何时跑；
- automation 失败只 warning，最后仍靠 reviewer 人肉判断有没有漏；
- 把需要 semantic judgment 的内容硬编码成 regex/keyword gate；
- 自动修改 baseline/snapshot 直到 check green，把 evidence gate 变成自我赦免；
- 只自动 happy path，失败后仍需要复杂手工 cleanup，却没有明确 ownership。

### 验证

好 automation 应 fail closed、输出 precise cause，并尽可能 idempotent/reproducible。若会写文件，明确 generated ownership，避免 generated 与 hand-edited 两个 truth。

验证 automation 最好的方式是故意漏步骤/制造 stale artifact：标准 check 应自己发现或生成正确结果，不需要 reviewer 记住隐藏 ritual。Clean checkout 也应能执行，不依赖某个人机器上的 shell history。

对不能自动化的 judgment，不要因为“想减少 toil”就伪结构化。可以提供 evidence collection、diff、summary，降低人类搜索成本，但最后 verdict 仍归真正 owner。

### 完成条件

完成时重复劳动要么消失，要么只剩确实需要人类理解的部分；“请记得每次手工做 X”不再承担 correctness。

> 最好的 automation 不是让机器替人思考，而是让人终于不用把注意力浪费在不需要思考的地方。

### Nudge

> 人类注意力应该花在新事实和新判断上，不该被永久征税去重复一套早已确定的机械仪式。

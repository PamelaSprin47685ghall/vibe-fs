# manual-toil-repeat — Enforcer

Manual toil repeat 的病，不是“人还在敲命令”，而是一套**机械、可判定、反复出现、失败有真实代价**的步骤仍然靠人类短期记忆维持。

第一次手工操作是学习；第二三次可能仍在探索。等步骤已经稳定成 ritual——检查同样文件、复制同样字段、跑同样命令、按同样规则更新 baseline、做同样 release prep——继续手工就不再买理解，只是在重复支付注意力税。

以下情形触发：

- 每次 release 都有同一套复制/校验/生成步骤；
- PR review 反复人工搜索同一种机械违规；
- migration/fixture/schema 更新每次手工同步几处；
- “记得还要做 X” 是流程可靠性的关键；
- 漏一步会造成生产/兼容/证据问题，而机器完全能判断是否漏；
- 新人必须背 checklist 才能不破坏 contract。

不要误杀 judgment。架构取舍、semantic review、RuleBook prose 质量、migration ambiguous meaning、是否值得重构，本来就不能因为“重复出现”自动脚本化。Automation 只适合 deterministic mechanics，不应把需要理解的选择伪装成规则。

与 `missing-architecture-gate` 区分：那条已经有明确 architecture invariant，却缺 machine enforcement；本规则更广，可能只是 workflow 自动化机会。与 `unrecorded-decision` 区分：toil 是重复执行，decision 是为什么这样做。

判定问题：**一个新工程师如果完全理解规则，执行时还需要创造 judgment 吗？** 如果不需要，只需准确重复步骤，machine 比 human 更适合拥有这份责任。

> 人类注意力应该花在新事实和新判断上，不该被永久征税去重复一套早已确定的机械仪式。
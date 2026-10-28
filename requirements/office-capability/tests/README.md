# office-capability 测试说明

本包 WHAT 定义有权交付的后果。权限投影、实际准入、Provider 行为和自然语言职责理解不能互相冒充。

- 003/007/012/015/016/017/018 调用生产权限与角色目录边界，验证已表达的允许/禁止集合及投影不反向修改权能。它们不等于工具已经执行、调用方理解正确或调度覆盖完整。
- 007 的 DevOps 模型绑定用例确实执行重复核验和冲突拒绝，并检查拒绝后仍保留原绑定；没有模拟 Manager 接力，不声称证明接力连续性。
- 006 只检查 `eval/provider-office-boundary` 中合成样例的判别器。新增反例修复了 Manager 一边委派一边亲自修改仍能通过、旧 coder 被当作 Engineer、DevOps 只运行不修复也能通过的问题。判别器仍是有限样例设施，不是生产门禁，也未接入真实 Provider trace。
- 001/004/005 的语义所有权需要正式人工审阅及真实投影追踪；不以提示词中出现某句比喻或几个关键词作证明。
- 011 的全任期权能与状态准入分开：新上游明确允许当前事实收窄动作；Manager 评审只读与派工门禁由 capability-enforcement 025/026 具体证明。
- 012/015/017/018 保留调度、内部隔离、实际修复及 Sphinx 接入标准 Engineer 的行为 TODO。018 不再恢复旧的只读 Engineer 限制，角色矩阵也不能证明真实工作流已采用标准权限。

撤下的假 integration 用例分别只是比较两个相同对象、重复读同一绑定、读取空 Fission 状态、关闭空 PTY。Fission 参数解析的有效用例已由 intra-participant-parallelism-002 覆盖，不在本包重复。真实 assignment 幂等、接力状态连续、历史 Fission 不复活及在途 PTY 清理，仍须在各行为所有者处核验，不能从这些旧用例推出已完成。

合成语义判别器还有明确局限：执行记录不包含成功结果、Manager 的 resume 名字没有固定 DevOps 的正式身份依据、工程选择与产品/架构选择无法只从 edit 调用判断。它们只能发现列明的样例违约，不能验收完整任务。

正式验证先构建，再通过 verification-system/tests/run.mjs 运行本包及需求结构检查。TODO 不算通过，office-capability-cutover 维持 TODO。双语真实 Provider 行为及完整资源分发审阅仍待现场证明。

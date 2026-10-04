# cognitive-environment 测试范围

编号与 WHAT 保持对应。本轮撤下散文关键词、源码辅助函数名称和目录计数冒充行为证明的断言；TODO 表示仍有规范义务，不能计为通过。

- 003 检查实际生成的中英 System Prompt：完整 Common Law、Role Law 及继承书籍的位置。包括内部 Bookkeeper；没有把当前各角色的书单固定成新规则。工具、生命周期与 Mission 的实际请求通道仍待证明。
- 005 检查生成提示中不得出现的 fast/deep 路由名称。这只证明当前输出的字面边界，不证明真实模型更换后的身份稳定；capability-enforcement 004 另有旧 tier 输入不改变规划器选择的局部证据。
- 012 检查 Manager 的完整 Role Law 与 Quality Ledger 资源确实进入生成提示；不能据此断言评审独立、诚实或双语语义一致。
- 原 002 的异地工作目录加载测试迁到 provider-language 009，并比较完整加载结果。原 006 的成对资源扫描由 provider-language 006 的实际门禁承担；汉字比例不是翻译质量门槛。
- 001/002/004/006—011/013/016 涉及权威归属、语义分工或实际 Agent 行为，当前没有可区分的执行证据。原单词禁令会误伤“不要越权”的合规表达，固定长句也会阻止合规改写，因此不再用它们宣称满足。
- 015 的直接 Surface 五例调用真实 `maybeInject`，读取 committed exact lease 和 EventStore companion 投影，证明白名单命中/未命中、同一转换输出去重、journal 字节不变及英文绑定。另有独立 durable marker oracle 正反例：读取真正的 Git-private `.git/wanxiang/events`，插入提示文本必须被检出，不能跳过 `.git` 把“没有持久化”测成假绿。五项静态检查只证明资源文字与源码形状，不计作执行注入的证明。

015 的 registered Host R1—R6 **全部仍为 TODO**，断言保留：R1 注入恰一条且不污染 Host 历史/journal；R2 非 companion 零注入；R3 同 occurrence 重放不重复且 digest id 稳定；R4 后续请求不携带旧 marker；R5 缺 exact committed lease 必须 typed fail-closed；R6 非白名单模型零注入。注册 hook 经完整 normalTransform，夹具增加 durable BloggerRequest 链与 SDK user/未完成 assistant 消息对，但尚未证明同时具备该请求的所有实际前提。已观察到缺 exact physical 绑定时 R1/R3/R4 不能注入，R2/R6 的零注入也可能仅由该缺失造成，不能归因于 companion/model 门禁；这些夹具结果不能认定生产 admission 结构性无法绑定。此前相互否定的“快照不同源”“session-only admission”等诊断不作为结论。

解封须经正式行为回归证明同一请求的 durable Accepted、BloggerRequest/PromptKey、可绑定 SDK assistant 与 exact committed admission 均成立，并核对 continuation 后真正被注入门禁读取的 frontier；不靠在 reader 补 lease，也不在 admission 已拥有租约后重复 acquire 来掩盖前提缺失。R2/R6 还须证明其它门禁前提已满足，零注入才可归因于被测门禁；正例和对应变异必须能红。R5 应保留 Accepted 等其它前提，仅撤销 admission 颁发的 exact lease，证明 CommittedAdmissionUnavailable 在公开注册 hook 的可观察 typed 拒绝及零注入，不能把安静完成或其它 setup 错误算通过。

GAP-077 保持 PARTIAL。上述 Surface 和 oracle 证据不等于注册 Host 链闭合，真实 provider wire 字节（Long Stroke canary）及空模型身份仍未证明。双语文案位于 `resources/provider/cognitive-environment/blogger-chronicle-text/`。

本文件不新增产品义务。分类依据与必要一致性以 WHAT 为准；没有把 README 中的 fixture、资源路径或检查手法设为唯一实现。待决与人工审阅线索见[迁移记录](../../../proposals/archive/2026-10-03/20模块迁移-环境与权限-2026-09-28.md)。

通过正式验证入口选择本包编号测试及 requirement-system；联动选择 provider-language 009。先完成正式构建，再运行检查。TODO 会使正式入口返回非零；测试执行完毕不等于本包验收完成。

# Sphinx 与 Manager 六包迁移记录

> **已归档（2026-10-03）：已结束批次的历史记录。** 后续施工从[现行 TODO 总计划](../../TODO施工总计划-2026-10-03.md)接手，历史定位见[归档索引](../README.md)。下文状态与结果绑定原基线；归档不关闭尚未证明的义务，当前缺口见[GAP](../../../requirements/GAP.md)。

日期：2026-09-28。工作区：`requirements-upstream/vibe-fs`，分支 `codex/requirements-upstream-remainder`。本轮从35模块PR提交后的 `83cd4bf` 出发，对照 upstream `1450f49d`；旧施工备份为 `1d7098a38f8419fa8586a6f695af61a18125f959`。

本轮整理47、50—54，sphinx-v2只作为取代47的必要依赖。旧根工作区未写入，未提交、推送或执行全仓构建；公共GAP、INDEX、Surface登记与工程依赖交由主任务统一处理。

## 合同迁移

| 模块 | 本轮处理 | 保留边界 |
|---|---|---|
| 47 epistemic-reasoning | 保留上游明确标记SUPERSEDED的WHAT/WHY历史全文；不复活旧内核、价格公式、阶段工具或APPLIES路径。补归档和v2对应说明。 | 旧文不是当前验收权威。有效数学、幂等、取消、持久化义务按SUPERSEDES迁入v2审查，旧测试通过不能证明新运行时。 |
| 50 obligation-ledger | 按上游7条UI投影合同重写简短WHAT/WHY；旧28条的承诺、T1、BlindPlan、lag-1不再迁回。 | 输入规范化、完整替换、desired/applied、迟到owner、失败诚实、无裁决权与历史只读仍明确。认知事务归cognitive-workspace。 |
| 51 relay-incumbency | 保留旧施工的简洁表达和真实Decision/Fold覆盖，006改为完整历史；工作记忆名称与新画板一致。 | 唯一任期、退休不可逆、authority修订、证书失效、固定DevOps及ordinal不减弱。cut不再是上下文删除下界。 |
| 52 relay-assessment | 009承接Manager可亲自用评审专用只读工具或委派只读Engineer；WHY保留独立证据目的。 | 只读且独立，不强制恢复“必须委派”。精确重放与新评审拒绝仍分开验证。 |
| 53 relay-context-projection | 按完整物理历史重写9条和WHY；明确cut只识别旧请求，保留历史不产生authority。 | 前任suicide、结果、wake、迟到parts可见；实际工作区由继任调查，不靠projection摘要替其判断。 |
| 54 relay-retirement | 保留简洁规则，承接完整历史和本任义务出清；同session画板按新所有者保留。 | 评审前提、freeze-before-check、当前资源blocker、Continue/Accepted、物理中断和道路资源仍各有明确义务。 |

## 证明迁移与必要接缝

50新增真实AssumeAdmission正反输入和TodoSink输出用例：必填项、四种status、默认priority、旧顶层字段拒绝、重复行、顺序、多行和空列表。`TodoSinkSurface.projectArgs`只调用生产parser、构造typed rows并调用生产sink；不补未知值、不复制业务算法。它不写Host，不能证明UI交付。当前仓库没有TodoSink的生产调用者；新增测试暴露这一范围，而非宣布完成同步。

51的`Surface.roadDevOps`不再补造缺失绑定，返回实际投影；增加缺失Road与显式绑定对照。生产Fold仍有`devops:<road>`逻辑初始身份，这是逻辑事实，不能当作已创建的物理执行者。当前函数覆盖不证明真实resume/join授权、接收未知恢复或后台进程交接。

53新增兼容的`ProjectionSurface.apply`，只调用真实`RelayNarrativeTransform.apply`并记录其interrupt选择。夹具通过真实review、suicide、journal与插件transform取得实际后继prompt，再测试完整历史、同session、确定性、旧请求拒绝及假user wake不能成为authority。旧`projectMessages` API保留兼容，但仅回传输入，已不再被本组活动测试当作证据。回调记录不等于实际Host进程终止。

54合并Continue/Accepted的真实插件路径，保留新上游ToolRuntimeScope资源登记/分类三项正反例，包括Engineer PTY名字含devops的情形。原自建`decideWithRoadResources`断言未沿用；未启动实际PTY，不宣称物理交接完成。中断结束前禁止派发的严格时序仍需受控在途端口验证。

v2纠正错配锚点：usage归004，意图恢复分类归010，空工作分类归017，abstain/tie解码归025，Bayes局部模型计算归026。旧021的Surface自行排名和错误调用签名不再充当两阶段解释证明。保留真实选择、数值和恢复分类，新增畸形likelihood反例、七件套名单与旧别名拒绝；实际结果幂等、同轮乱序、两阶段持久恢复等继续TODO。

另修正v2-001迁移夹具：旧代码把Goal的list字段造为Set，并给amend传裸JS数组；现从真实stateOfCreate取得typed Goal，约束经listOfItems构造，增加空authorizer拒绝对照。这是测试接口适配，不把夹具类型错误记成产品违约。47历史WHAT/WHY只去掉重复标题，不改历史正文。

新增/调整源码为：

- `Participant/Cognition/TodoSinkSurface.fs/.fsi`：新增obj输入输出的真实parser/sink接缝。
- `Mission/Relay/Surface.fs`：删除投影读取中的伪默认值。
- `Mission/Relay/ProjectionSurface.fs/.fsi`：兼容新增真实NarrativeTransform入口，保留旧API。

主任务登记TodoSinkSurface到现有ExecutorToolSurface shard和Surface manifest；RelayProjection增加NarrativeTransform、JournalSurface工程引用并调整编译顺序。v2 Wire/Surface登记只为真实工具名单测试，不把该Surface其余模板返回当运行证明。

## 未完成实现与待集中裁决

以下为交给主任务的GAP建议，未在此新分配全局编号。

1. **新Sphinx实际入口尚未闭环。** 生产MCP的七个handler都忽略args，查询空inquiry的status；Wire Surface的start/submit/status返回模板；OpenCode adapter的ReadStatus、ReadResult、Reconcile仍返回固定值。恢复分类器和工具名单都不能证明Runtime已经接通。另见该adapter重复局部定义的上游迁移痕迹。本轮不扩大实现，不恢复旧内核；建议新建v2实际入口/持久状态/Host生命周期缺口，而非原样搬回GAP-170—175。
2. **v2有效旧义务的正式落点。** SUPERSEDES称旧Bayes合格因子条件、标准算法退化、全链取消部分保留，但新WHAT的025主要规定BTL、026规定保证不升级，034侧重真实receipt。旧条件哪些属于仍必要一致性、哪些已被新依赖/资源模型替代，需要在新所有者逐项明确。不得仅把旧测试改锚点就认为合同已完全承接。本轮保留数学局部事实和全链TODO，没有自行恢复旧因子选取政策或价格模型。
3. **UI同步及历史读取。** 新50的desired/applied、旧owner迟到、Host失败pending/applied、纯修复和受控历史读取尚无完整生产证明。现legacy接口只是拒绝，不能称完成审计读取。旧GAP-190/191必须按已替代协议拆分处理；不恢复旧MagicTodo事件写入或切点。
4. **Manager现有缺口。** GAP-192保留并发opening、实际控制权、在途DevOps、恢复、ordinal投递边界；GAP-193保留完整binding、独立调查、信息可见面与快照变化链；GAP-194旧精确review重放失败保留可执行TODO，须在新基线复验，不能从纯fold成功推断真实tool成功。
5. **全历史与旧反例。** GAP-195按新全历史和请求身份合同重审；GAP-196旧“cut删除后继输入”反例因上游取消消息过滤而被取代，不能标作新实现已通过。本轮有对应真实轨迹测试但尚未运行。GAP-197继续涵盖递归资源、freeze race、崩溃原子性、中断严格次序、画板/义务交接及物理终结。
6. **独立MCP与Host适配边界。** v2独立MCP接入不要求在OpenCode原生命令配置中注入MCP，可以与host-boundary-026并存。本轮不修改Host配置。

## 历史输入安全保留

自动审批拒绝了批量删除旧测试，理由是可能丢失验证覆盖；改用保留内容的归档方案，没有拆分重试删除。47共15份、50共19份输入移入各自`historical-tests`，活动入口扩展名变为`.mjs.txt`，原README保留供审阅。旧47-001与50-007是含原三方合并标记的输入，以无损gzip保存，避免当成当前未解代码。

34份输入的解压后SHA-256逐项与归档前一致，清单已提交在两个`historical-tests/SHA256SUMS`。核对清单的原始组合摘要为`75f9c4a45db459de7c7c7eb4f1cf52e5b46ed6197bfa2580f6ed5a439b391ad8`；逐文件清单是复核依据。完整旧施工仍在备份提交中。归档不等于新证明充分替代旧证明；新归属、退出范围与未闭合义务均见各README。

直接替换ProjectionSurface API也被自动审批拒绝，担心调用兼容性；改为兼容添加真实apply入口，保留原API。本轮未绕过审批移除兼容API，也未用其存在制造行为证明。

## 本轮验证

- 64个活动`.mjs`逐文件`node --check`通过；归档文本/压缩文件不符合`.test.mjs`发现条件。
- 5个新增/修改F#文件经Fantomas检查，唯一格式问题在ProjectionSurface.fsi，已单文件格式化后复核。
- 当前相关文件`git diff --check`通过；34份归档解压内容的hash全相同。
- 工作区没有对应Cognition、Relay和Sphinx/V2新dist，未执行这些行为测试，不把加载缺失记作业务断言失败。
- 主任务分别实际尝试TodoSink所在ExecutorToolSurface、RelayProjection的独立Fable编译，均被上游闭包依赖/顺序问题阻断，包括Workspace→CanvasCodec、Owner→AssumeFactCases、ChatExecutionFact→AgentFact。日志为`/private/tmp/vibe-fs-remainder-compile-1.log`、`/private/tmp/vibe-fs-remainder-compile-2.log`。没有TodoSinkSurface自身报错不等于编译或验收通过。

后续统一新构建后应运行本六包的活动测试及sphinx-v2/tests，并复核新增Surface登记、跨包权限测试和正式runner。已知TODO不计入通过；本报告交付的是规范/证据范围整理，不是整套新实现通过证书。

# Sphinx 与 Manager 六包迁移记录

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

## 2026-10-02 设计决定补记：持久化与工作生命周期

以下两项路径已定，仍是实现中的设计，不是实现完成或运行通过报告。此处只保存取舍理由和复验条件，不替代 requirements，也不改写上文 2026-09-28 的迁移证据。[requirement-system WHAT 007](../requirements/requirement-system/WHAT.md#007-what-是唯一权威) 仍规定 WHAT 是唯一规范权威；条款变更由其 owner 处理，不能借这份补记完成。

### 决定一：公开 decoder 与真正持久化边界分开处理

**约束与依据。** [sphinx-v2 WHAT](../requirements/sphinx-v2/WHAT.md) [008]/[020] 规定哈希的计算对象，[009]/[016] 规定唯一状态与确定性 fold，[019] 规定原子 TransitionBatch，[033]/[034]/[036] 规定真实持久化、Host receipt 与七工具入口。[durable-events WHAT](../requirements/durable-events/WHAT.md) [002]/[003] 约束已发布载荷和 canonical identity，[013]/[019]/[023] 约束 Current、共享 Integrator 和分层；[durable-convergence WHAT](../requirements/durable-convergence/WHAT.md) [001]—[007] 约束事实保留、因果归并及显式冲突。这些条款是依据，不在此另抄一份规范。

**选定路径。**

- `src/Wanxiangshu/Sphinx/V2/Hosts/Mcp/Tool.fsi` 已公开 decoder；其 Fable 导出名不是私有泄漏，不为测试新增平铺 facade。
- 真正的 Sphinx 持久化修复在 `src/Wanxiangshu/Sphinx/V2/Persistence/Codec.fs`、`src/Wanxiangshu/Sphinx/V2/Core/Reducer.fs` 与 `src/Wanxiangshu/Sphinx/V2/Persistence/Integrator.fs` 的边界完成：Codec 持有载荷表示，唯一 Reducer 校验并折叠业务状态，业务规则注册到共享 canonical Integrator；不在调用方再建一份历史 fold 或 Current。
- 已发布的 `sphinx/v2-transition@1` 载荷与 canonical 字节不改；新严格 30-case DTO 另用 `sphinx/v2-transition@2`。这是追加事件词汇，不是给 envelope/store 加迁移代次，也不是把旧事件重新解释成新 DTO。现行 sphinx-v2 [019] 对 `sphinx/v2-transition@1` 的记载仍保留，不能以本补记宣称新增词汇的合同、注册和迁移证明已经对齐。
- 一次逻辑迁移的 batch 整体验证、原子接纳，一 batch 对应一次 inquiry revision 推进与一致 receipt；非法 batch 不部分推进。revision 是状态进度，不是并发分支的胜负依据。
- 合法分支全部保留，投影显式给出 `DomainConflict`，不按到达、时间或较大 revision 选胜；冲突收拢依赖覆盖全部竞争 Heads 的显式裁决。
- post-state digest 覆盖完整、显式的物化状态，不以事件条数或局部摘要代替。EventId 的 identity 输入不包含该 post-state hash，避免状态含事件身份、事件身份又依赖状态哈希的自引用。trace/state/semantic 三种计算对象仍由 sphinx-v2 [008]/[020] 区分。
- 拟新增的窄持久化 Surface 是正式生产能力边界：提供 typed 提交、结果与 Current 观察，经过真实 Codec/Reducer/Integrator。它不是内部对象反射，也不是复制算法的测试替身。

**可信备选与拒绝理由。** 单独平铺 decoder facade 可以让 JS 测试少见 Fable 形状，但公开签名已经给出该边界，另加转发层只重复接口，不修持久化；应在公开边界验证，而不是从内部字段/tag 反射取值。原地增强 `sphinx/v2-transition@1` 省去新词汇登记，却会改变已发布载荷的含义或字节，破坏 immutable identity。另建私有 journal/fold 看似减少共享依赖，却制造第二真源。仅保留先到或最大 revision 的后继能简化 Current，却丢掉合法事实。让 EventId 纳入 post hash 看似把所有内容绑定得更紧，却形成自引用；局部状态哈希则无法证明完整重放。

**后果与复验触发。** 接受新增 DTO、词汇登记与生产 Surface 的工程成本，不用 facade 或兼容反射掩盖它。需要在最后源码状态复验严格 30-case 编解码、已发布字节保真、非法 batch 零部分提交、receipt/revision 一致、cold replay 完整状态 digest，以及合法分叉在不同输入顺序下保留全部事实并给出一致冲突。随后还须接通真实 Host 与七工具，覆盖创建、派发、取消、恢复和结果交付；持久化局部证据不能关闭真实入口缺口。若公开签名实际不再提供所需 decoder、共享 Integrator 无法表达该原子接纳，或反例证明 identity/hash 仍存在自引用，再凭该新证据重开相应边界判断；不能仅因 Fable 导出名或改动量重开。

### 决定二：稳定身份不承担每次工作的 completion/consume 状态

**约束与依据。** [managed-session-lifecycle WHAT](../requirements/managed-session-lifecycle/WHAT.md) [004]—[008]/[012]—[015]/[024] 区分复用、完成消费、物理生命周期与稳定 handle；[delegation WHAT](../requirements/delegation/WHAT.md) [003]/[006]/[024]—[027] 定义既有道路续做、exact work completion、接纳证据与前工完成即可承接后工。[interaction-authority WHAT](../requirements/interaction-authority/WHAT.md) [003]/[004]/[011]/[018] 与 [participant-identity WHAT](../requirements/participant-identity/WHAT.md) [001]—[004]/[008]—[010] 持有 exact root、身份与 closure 的边界；历史材料处置依 durable-events [001]/[002]/[026]，不由生命周期补写授权。

**选定路径。**

- 稳定 handle、participant 与 executor identity 同每次 work 的完成、消费分开。此处 executor continuity 不把可变物理 target/lease 变成 participant identity，也不绕过 exact logical-run closure 后才能安装 fresh identity 的要求。
- 每个 scoped work 依据已有、exact accepted AuthorityRoot 的真实准入发行，完成只认该 work 接受的因果身份。重复 HandleLinked 只证明关联重放，不能当成新 work 准入或使已结束 work 复活。
- 首选由 canonical admission 导出 work 证据，避免复制准入真源。只有现有事实不足以无歧义关联而确需补事实时，才追加引用既有准入的 work 关联事件；关联事件不另造 AuthorityRoot、授权或并行状态机。具体载荷由实现 owner 对既有准入证据核对后确定。
- A 完成后可接纳 B，不以 A 是否已 join 消费作为 B 准入的等待条件。随后消费 A 只退休 A 的 scoped work，不关闭 B 或稳定身份；A、B 两份 completion 都保留，分别单赋值、分别幂等消费，迟到 A 不能结算 B。
- 无 root 的旧事实不自动升级为新准入。有歧义的旧多轮历史 fail-closed；仅可经显式离线材料迁移保留其历史证据，不能把导入事实变成可运行授权。真正的新 work 仍需现行 canonical admission。

**可信备选与拒绝理由。** 以稳定 handle 上唯一 completion cell/退休墓碑承载全部工作，状态少，却使 A 的消费误伤 B 或被重复 link 复活。一律禁止 resume 可以回避该错误，却破坏既有道路和固定执行者的连续性。为每次 work 无条件另加准入事件，看似审计充分，却复制已有 root acceptance，产生第二写者；只在确需关联时追加引用事实。以自增版本、时间或到达顺序选“当前工作”，能区分表面轮次，却不能证明 terminal 属于哪次 accepted work。运行时给 rootless 历史补 root 或挑一轮映射，则凭空创造授权、抹掉歧义。

**后果与复验触发。** 读取、完成、join、恢复必须携带 scoped work 的 exact evidence，而身份复用不再意味着工作仍在跑。最低反例是 A 完成 → B 接纳 → A 消费，随后 B 仍可完成且两份结果均保留；还须复验重复 link、重复/迟到 terminal、消费提交不确定及重启后的对应状态。只有 canonical admission 确实缺少无歧义关联所需事实，才启用引用准入的关联事件方案；若实际关联存在多解，不改用时序猜测，而保持 fail-closed 并交回历史迁移的显式裁定。

### 开放问题：query-shell 的当前产品归属尚未裁定

[process-execution WHAT 011](../requirements/process-execution/WHAT.md#011-单次执行参数一致) 定义 run/query-shell 的命令参数合同；[action-affordance WHAT 006](../requirements/action-affordance/WHAT.md#006-run-与-query-shell-是实际动作而非运行时预测) 规定 query-shell 的只读观察描述与不适用用途，同包 [002] 又将工具存在性和授权留给动作/权能 owner。两者不是“外部工具”与“内部结果读取”两个同名接口。

[speculative-investigation WHY](../requirements/speculative-investigation/WHY.md) 中“为什么已退役工具 query-shell 不发协议分类，且未判定工具差集门禁以 `knownToolNames` 为唯一权威？”一节，讨论的是当前 Provider 工具名册的分类差集：以 `StaticTools.knownToolNames` 为输入，不为历史 AST 导出发协议定义。“当前 Provider 不可见”在此是分类范围与当前注册的解释，不是替 process-execution 作废合同的 normative 裁决。该节使用“已退役”及 query-shell-worker 的称呼，不能直接当作当前授权或精确 owner 的事实。

仍需产品 owner 明确：WHAT 011 的 query-shell 是否为现役可调用能力；若是，由哪个现行合法 Office 持有并怎样落实只读限制；若否，由规范 owner 明确处理其保留命题。在该裁决之前，不宣布退休或恢复授权，不新增 query_shell 兼容别名，也不从 WHY 或同名字符串推导权限。旧缺口继续引用 [GAP-078、GAP-091](../requirements/GAP.md)。

### 证据状态与旧 GAP 的保留

[GAP-132、GAP-216、GAP-219](../requirements/GAP.md) 均不因上述设计已定而关闭。GAP-132 对应稳定 handle 与一次工作墓碑的生命周期反例；GAP-216 包含已记录的全局 clean 构建事实，但该事实不证明本次变更后的独立闭包和行为；GAP-219 仍需真实入口、Host/Provider 与结果适配的 runtime oracle。后续证据必须对应最后实现状态，不能以 decoder 可导出、局部 Surface 可调用、模板返回或旧构建结果代替。

本补记只进行文档写入与回读，未运行命令、Git、编译或测试，也未改源码、测试、README、GAP 或 APPLIES-TO。


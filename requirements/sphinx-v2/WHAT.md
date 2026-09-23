# sphinx-v2 — WHAT

本包是 Sphinx clean-break 的唯一 normative owner。它取代 `epistemic-reasoning` 中属于旧内核的条款，取代关系见 `SUPERSEDES.md`。旧事件、旧 inquiry 数据与旧工具不删除，但不再进入新调用图。

## [001] 原目标只可由用户授权修订

GoalSpec 保存用户原文的字节精确副本。LLM 可以提出 `GoalInterpretation`、`ReframingProposal`、`MissingConstraint`，它们都是带来源的语义图节点，不改 GoalSpec。只有显式的用户 goal amendment 命令推进 `goalRevision`。目标版本变化使依赖旧目标的估值失效，但原始材料与观测不删除。测试：C-01、C-02、C-03。

## [002] 源码不写死语义评分表

生产源码不得出现 Why/How → 权重、方法名 → 收益、发现数量 → 答案质量、固定 gateway 增益一类的映射。未调查计划是 `Unestimated`，不得赋 0 冒充差或赋 1 冒充值得探索。工程数值默认（`metaDepth`、`fitMaxIterations`、`thetaL2`、`maxActivePlanCards`）可以与语义阈值共存，但必须与语义权重分开声明，各自进入 config hash。测试：X-01、X-02。

## [003] 工作身份、attempt 与 fence 严格分离

WorkId 标识一项工作；Attempt 标识第几次尝试；Fence 标识该次尝试的逻辑边界。retry 是新 Attempt、新 Fence、新物理调用身份；正常复测是新 WorkId。晚到的其它 attempt 只归档计费，不增加票数。一个已成功 work 不接受第二份语义结果。测试：C-04、C-05、H-06、Q-07。

## [004] 资源账按消耗与容量分开，且超支是事实

模型调用、token、金额是消耗；并发槽是容量；墙钟不是可加的账目项。`signedFree = authorizedLimit - settledUsage - outstandingReservations`；`availableForNewWork = max(0, signedFree)`、`observedOverrun = max(0, -signedFree)` 是导出值，不与三个事实分存。失败、取消、重复实际调用都计费；provider 不返回用量标 `usage-unresolved` 并保留预留，不写 0；超支记录事实，不靠拒绝 usage 维持表面守恒。测试：B-01～B-08。

## [005] 证书按地址寻址，保证分型，槽级合并

证书地址是 `(targetRef, valueSpaceId, scopeId, semanticsModelRef)`，不是 NodeId。同一候选在不同目标、预算、epoch、模型下可持多份证书，Core 不合并成"最新真值"。保证分型为 EmpiricalSummary / OrdinalObservation / ModelEstimate / PosteriorCredible / FrequentistCoverage / DeterministicBound / ExactWithinModel / ResidualOnly；后验 credible mass 与频率学 coverage 不可互换。槽 patch 带 `expectedSlotRevision`；同 base 冲突则拒绝，不最后写入获胜。测试：C-06～C-09、M-16。

## [006] 事件是运行事实，不是认识裁决

Core 事件表没有 `HypothesisTrue`、`EvidenceReliable`、`ReflectiveEquilibriumReached`。Core 对 `Kind`、`Relation`、schema ID 与 payload 只比较 identity/hash，不解释语义。测试：C-10、C-14、C-16。

## [007] 缺引用即拒绝，禁止无事件补洞

缺 graph 端点、缺 work、缺 scope、缺 round、缺节点一律返回具体错误。不得为让 fold 通过而创建 placeholder 节点、补 parent、生成缺失 ID 或重排业务事件。需要补建时，由明确命令在同一 batch 内先建后连。测试：C-14、R-02。

## [008] 三种哈希分开命名

`traceHash` 覆盖 accepted canonical envelopes 及其顺序；`stateHash` 覆盖完整物化状态含 physical bindings；`semanticHash` 只覆盖语义事实，排除 session ID、transport cursor、wall-clock timestamp，来源引用转为稳定逻辑 ID。三个哈希都不得只对数组长度哈希。测试：C-10、C-11、C-12、C-13。

## [009] 一个 Core reducer，一个 Runtime driver，一套持久状态

MCP、OpenCode、JS surface 共用同一 Runtime/Core。不得存在第二 registry、第二 history fold 或 caller-held Current。旧 `GecInquiry` 的 results-only 表、`SessionStore`、`GecStore` 的独立 fold 退出生产调用图。测试：X-01、I-01。

## [010] 先记录意图，再执行副作用

append 失败不得派发 provider/Host 工作。派发前 `BudgetReserved` + `DispatchRequested` 必须已持久化；receipt 未写入时按 dispatch intent 对账，不盲目重复创建。测试：R-05、H-05。

## [011] 结果接纳至多一次

结果 key 是 `inquiry + workId + attempt + logicalFence`。相同 key + 相同 payload 返回原 receipt；相同 key + 不同 payload 是 `RESULT_PAYLOAD_CONFLICT`。物理用量 receipt 单独幂等，重复不重扣。幂等 lookup 先于一般 stale revision 检查。测试：C-04、C-05、B-04、B-05。

## [012] 局部前置条件，不锁全局 revision

round 内 worker 用 `workId + attempt + logicalFence + ticketHash + scopeId` 作局部前置条件。同轮乱序回包都能接受，不得因全局 revision 变化拒绝合法后续票；控制命令（goal amend 等）才用严格 `expectedRevision`。测试：H-08、R-08。

## [013] 计划选择与工单装箱分离

`Decision` 按贡献估值选计划并生成 DecisionReceipt；`Agenda` 只检查 DAG、冲突、资源与容量可行性，不看语义理由，不给新计划打分。本批已选依赖不算"已满足"；同时派发集合必须是依赖已实际成功的节点。测试：D-01～D-10。

## [014] 同一 ballot 不重复算独立票

rank 展开 pair 必须保留 ballot cluster；MaxDiff 用联合 best–worst 似然；同 child 重复回答属同一 cluster；retry 是同一 work 的新 attempt；复测是新 work。三者不得互换。测试：N-09、N-10、N-11、Q-07。

## [015] schema 引用是真实内容哈希

SchemaRef 的 hash 是 canonical schema 文档的 SHA-256，不是 `sphinx-schema-v2` 这样的名字。两处引用必须可由 hash 区分。wire revision 是十进制字符串，实际计数用检查过的正安全整数。测试：W-01、W-02、X-03。

## [016] 确定性 fold

相同事件输入有相同 state。回放不联网、不生成新随机数、不重跑 LLM、不调用 provider。测试：C-01、R-02、R-03、R-07。

## [017] 完成与收敛分开

`Completed` 必须有 `AnswerCommitted`，且 AnswerCommitted 必须引用已接受的 renderer 结果。停止原因分 `model-ranked-stop` / `ordinal-stop` / `certified-within-model` / `resource-limited` / `no-executable-plan` / `user-cancelled` / `failed` / `suspended`，不得统一标 equilibrium。缓存删除后恢复结果不变。测试：I-05、D-09、R-02。

## [018] Worker 不写目标、预算、证书与事件

Worker 只交结果。`sphinx_work_submit` 接受的输入不含证书 patch、预算 debit、任意事件写入、目标修订。语义提议不扩大实际权限。测试：W-05、Q-11。

## [019] 一个 canonical envelope 承载一个原子 TransitionBatch

一个 inquiry 逻辑迁移的多个事件封装在一个 canonical EventEnvelope 中；Integrator 对 batch 先完整验证并 fold 到临时值，再整体接受。batch 内任一项非法，整个 batch 不成为 accepted current。事件类型 `sphinx/v2-transition@1`。测试：C-16、R-01。

## [020] traceHash / stateHash / semanticHash 不可混称

三个哈希的计算对象、排除字段和语义含义如 [008] 所定义。删除旧 `GecSurface.replay(array)` 的长度哈希重载；同 length 不同内容必须给不同 hash。测试：C-10、C-13、X-01。

## [021] 观测与解释分成两个可恢复阶段

第一事务记录结构合法的 `ResultAccepted`、工作完成、可验证用量与 `InterpretationPending`。第二事务运行纯 Observe，记录 Graph/Certificate delta 与 `InterpretationApplied`。插件异常不重调 LLM；原始回答永久保留；修复代码产生新实现 hash/版本，在显式的新派生 inquiry 中重处理。测试：P-05、R-03。

## [022] 测量与干预分开记录

同一快照下的换序、标签遮蔽是 measurement；加入新理由、反例、候选是 intervention。问前问后都留回答与可见输入。问后改变不自动等于改善，问后不变不自动等于正确。测试：Q-04、Q-06、Q-12。

## [023] 随机化与协议 manifest 持久化实际值

处理分配、label map（host-private）、order、seed、算法版本、模型/provider/config fingerprint、independence unit、expected completion set、missingness 处理逐项持久化。不默认"第一个合法回答胜出，余下取消"。worker 收到 opaque label，不收到 label→真实作者映射。测试：Q-01、Q-03、Q-05、Q-08。

## [024] BIBD 只在满足条件时命名

只有实际验证 `vr=bk`、`r(k−1)=λ(v−1)` 及所有成对出现次数时才可称 BIBD；否则记录真实 exposure 矩阵与不平衡，不美化名称。Borda 只作描述性基线，除 complete equal-exposure 外必须标明扩展与 exposure。测试：Q-09、N-14。

## [025] BTL 与 tie-aware 似然的实现契约

固定 gauge；稳定 `logSigmoid=-softplus(-x)`、`logSumExp`；返回完整协方差或可查询协方差算子，不返回每项 `1/sqrt(N)`；`Var(theta_i-theta_j)=Sigma_ii+Sigma_jj-2Sigma_ij`；tie 用声明的三类 softmax 模型，不拆成相反票；`abstain` 不进入方向似然；capped/line-search-failed 标 `Converged=false`；不连通返回 typed unidentifiable；原始 ballot 永久保留，算法升级从原始数据重拟合。测试：N-01～N-14。

## [026] 局部保证不自动跨算子升级

pairwise/ranking 不可转为 ProvenBound；Bayes 模型后验不等于"概率=实际正确率"；A* bound 只在其声明模型内；MCTS sample 的经验半径不得充当 fixed-time 覆盖；语义反例不可让 Core 无条件删候选。测试：M-15、M-16、C-06、C-07。

## [027] 固定点与收敛只声明可证明的部分

closure 在 finite DAG、monotone/Scott-continuous、或明确 contraction modulus 下才可称收敛；否则报 bounded iteration + residual。不得用"循环跑到 20 次"伪装数学收敛。无变化不刷 inquiry revision。测试：D-10。

## [028] Graph 多角色共用一个存储形式，不混语义

epistemic / work-dependency / plan-tree / refiner-state 四种逻辑角色由 node 的 `Role` 区分，共用一个图存储。认识图可有循环；工作依赖 DAG 必须无环。不得为让工作调度拓扑排序通过而删除认识图里的语义循环。测试：C-14。

## [029] answer.now 必须进入候选集合

每次 inquiry 预留成稿资源；比较其它计划时考虑其资源消耗及后续成稿可能性。连成稿都不可执行时返回 `INSUFFICIENT_RENDER_BUDGET` 或已有 draft，不凭空合成。"现在作答"与"再做一次综合修订"可以是不同计划，但须解释差别，不得让同一成稿动作以两个名称重复占优。测试：D-05、I-01。

## [030] 停止依据只覆盖已列出的计划

停止证据不证明没有尚未生成的更优路径。缺 VOC（value-of-computation）证据是缺失，不是检查通过。缺证据时按操作结束记录，不声称认识收敛。测试：D-05、I-05。

## [031] 探针必须真的可被提出、派发、吸收和重新估值

每个启用探针至少有一条公开 API 可达轨迹。`not-applicable`、空发现、未找到反例都是成功完成的合法结果，不重试到模型说出预设答案。探针 manifest 禁止 `qualityWeight`、`methodUtility`、`expectedRootGainDefault` 字段；可有实际成本统计、返回 token 上限、所需资料类型。测试：Q-10、I-02、I-03。

## [032] 动态问题不是任意代码

LLM 可提出库外问题，封装为 `sphinx.probe.open-question@2`：自然语言问题 + 可见材料 + 已注册通用 response schema + 已授权工具。不能提交新插件、动态 import 或任意工具脚本。重复有效的动态问题可在后续版本成为新模板；本 inquiry 内不自动改 plugin lock。测试：Q-11。

## [033] 生产入口必须有 durable store

未配置持久化时启动失败，不退回内存旧内核。内存端口只在显式测试 profile 下可用。测试：W-06、X-04。

## [034] Host receipt 必须来自实际 adapter

Runtime 生成的 intent hash 不得命名为 childSessionId。abort 未确认前状态是 `cancelling`，不得报 `cancelled` 或 `drained=true`。`session.idle` 不是有效结果存在的证据，必须实读 message/tool-result 并核对 work token、schema、attempt。测试：H-01～H-04、H-06、H-07。

## [035] MCP 协议版本与 Sphinx API 版本分开

Sphinx `apiVersion="2"` 独立于 MCP 协议版本。不得只改 `protocolVersion` 字符串冒充协议升级。业务 `awaiting_results` 作为已完成 tool result 的内容，不误用协议级 input-required。测试：W-01、W-04。

## [036] 工具白名单是 v2 七件套

`sphinx_inquiry_start`、`sphinx_work_next`、`sphinx_work_submit`、`sphinx_inquiry_status`、`sphinx_inquiry_cancel`、`sphinx_inquiry_export`、`sphinx_goal_amend`。不保留旧 assess/propose/investigate/synthesize 阶段接口，不用同名 alias 调用旧 Policy。status/export 不创建 lease、不调用模型、不改变业务状态。测试：W-01～W-05。

# sphinx-v2 — WHAT

Sphinx V2 是调查运行时的规格：从用户给定的目标出发，选计划、派活、收结果、成稿。MCP、OpenCode 宿主与 JS surface 共用同一 Runtime/Core，代码在 `src/Wanxiangshu/Sphinx/V2/`。以下条款只写可观察行为与边界。

## [001] 原目标只可由用户授权修订

GoalSpec 保存用户原文的字节精确副本。模型可以提出 `GoalInterpretation`、`ReframingProposal`、`MissingConstraint`，它们都是带来源的语义图节点，不改 GoalSpec。只有显式的用户 goal amendment 命令推进 `goalRevision`；授权者为空即拒绝，原目标不动。目标版本变化使依赖旧目标的估值失效，原始材料与观测不删除。

## [002] 源码不写死语义评分表

生产源码不得出现 Why/How → 权重、方法名 → 收益、发现数量 → 答案质量、固定 gateway 增益一类的映射。未调查计划是 `Unestimated`：不得赋 0 冒充差，也不得赋 1 冒充值得探索；补上 Rank 或数值位置也不会变成 usable。工程数值默认（`metaDepth`、`fitMaxIterations`、`thetaL2`、`maxActivePlanCards`）与语义阈值分开声明，各自进入 config hash。

## [003] 工作身份、attempt 与 fence 严格分离

WorkId 标识一项工作；Attempt 标识第几次尝试；Fence 标识该次尝试的逻辑边界。retry 是新 Attempt、新 Fence、新物理调用身份；正常复测是新 WorkId。晚到的其它 attempt 只归档计费，不增加票数。一个已成功的 work 不接受第二份语义结果。

## [004] 资源账按消耗与容量分开，且超支是事实

模型调用、token、金额是消耗；并发槽是容量；墙钟不是可加的账目项。`signedFree = authorizedLimit - settledUsage - outstandingReservations`；`availableForNewWork = max(0, signedFree)`、`observedOverrun = max(0, -signedFree)` 是导出值，不与三个事实分存。失败、取消、重复实际调用都计费。provider 不返回用量时该 receipt 记 `UsageUnresolved` 并保留预留，不写 0。超支记录事实，不靠拒绝用量维持表面守恒。

## [005] 证书按地址寻址，保证分型，槽级合并

证书地址是 `(targetRef, valueSpaceId, scopeId, semanticsModelRef)`，不是 NodeId。同一候选在不同目标、作用域或模型下可持多份证书，Core 不合并成"最新真值"。保证分型为 EmpiricalSummary / OrdinalObservation / ModelEstimate / PosteriorCredible / FrequentistCoverage / DeterministicBound / ExactWithinModel / ResidualOnly；后验 credible mass 与频率学 coverage 不可互换。槽 patch 带 `expectedSlotRevision`；同 base 冲突则拒绝，不最后写入获胜。

## [006] 事件是运行事实，不是认识裁决

Core 事件表没有 `HypothesisTrue`、`EvidenceReliable`、`ReflectiveEquilibriumReached`。Core 对 `Kind`、`Relation`、schema ID 与 payload 只比较 identity/hash，不解释语义；Applied delta 在 Core 侧始终是记录下来的 opaque envelope。图存储用 node 的 `Role` 区分 epistemic / work-dependency / plan-tree / refiner-state 四种逻辑角色：工作依赖必须无环，认识图允许循环，不得为让调度通过而删除语义循环。

## [007] 缺引用即拒绝，禁止无事件补洞

缺 graph 端点、缺 work、缺 scope、缺 round、缺节点一律返回具体错误。不得为让 fold 通过而创建 placeholder 节点、补 parent、生成缺失 ID 或重排业务事件。需要补建时，由明确命令在同一 batch 内先建后连。

## [008] 三种哈希分开命名

`traceHash` 覆盖 accepted canonical envelopes 及其顺序；`stateHash` 覆盖完整物化状态含 physical bindings；`semanticHash` 只覆盖语义事实，排除 session ID、transport cursor、wall-clock timestamp，来源引用转为稳定逻辑 ID。三个哈希都不得只对数组长度哈希。

## [009] 一个 Core reducer，一个 Runtime driver，一套持久状态

MCP、OpenCode、JS surface 共用同一 Runtime/Core，不得存在第二 registry、第二 history fold 或 caller-held Current。同一依赖在 registry 里绑定一次并按依赖顺序交付；菱形依赖不重复派发。

## [010] 先记录意图，再执行副作用

append 失败不得派发 provider/Host 工作。派发前 `BudgetReserved` + `DispatchRequested` 必须已持久化；receipt 未写入时按 dispatch intent 对账，区分"未发出"与"未记录"，不盲目重复创建。终态 inquiry 拒绝新的派发事实。

## [011] 结果接纳至多一次

结果 key 是 `inquiry + workId + attempt + logicalFence`。相同 key + 相同 payload 返回原 receipt；相同 key + 不同 payload 是 `RESULT_PAYLOAD_CONFLICT`。物理用量 receipt 单独幂等，重复不重扣。幂等 lookup 先于一般 stale revision 检查。

## [012] 局部前置条件，不锁全局 revision

round 内 worker 用 `workId + attempt + logicalFence + ticketHash + scopeId` 作局部前置条件。同轮乱序回包都能接受，不得因全局 revision 变化拒绝合法后续票；控制命令（goal amend 等）才用严格 `expectedRevision`。

## [013] 计划选择与工单装箱分离

用户已选计划只做形状与资源核对，不再被语义估值覆盖。按贡献估值选计划并生成 DecisionReceipt 是独立能力；派发路径按用户给的计划推进。本批已选依赖不算"已满足"：只有依赖实际成功的节点才进入同时派发集合。父步骤只是被选中、尚未成功时子步骤不派发；父失败子步骤保持未执行；父成功后子步骤恰好派发一次。

## [014] 同一 ballot 不重复算独立票

rank 展开成 composite pair 必须保留 ballot cluster；MaxDiff 用联合 best–worst 似然；同 child 的重复回答属同一 cluster；retry 是同一 work 的新 attempt；复测是新 work。这些身份不得互换。abstain 既不是一票，也不是缺失数据。

## [015] schema 引用是真实内容哈希

SchemaRef 的 hash 是 canonical schema 文档的 SHA-256，不是 `sphinx-schema-v2` 这样的名字；两份不同文档必须可由 hash 区分。声明的 plugin lock 与实际可执行 manifest 的每个锁定字段必须一致，不一致拒绝。wire revision 是十进制字符串，实际计数用检查过的正安全整数。

## [016] 确定性 fold

相同事件输入有相同 state。回放不联网、不生成新随机数、不重跑 LLM、不调用 provider。

## [017] 完成与收敛分开

`Completed` 必须有 `AnswerCommitted`，且 AnswerCommitted 必须引用已接受的 renderer 结果。停止原因分 `model-ranked-stop` / `ordinal-stop` / `certified-within-model` / `resource-limited` / `no-executable-plan` / `user-cancelled` / `failed` / `suspended`，八者保持可区分，不得统一标成一个。缓存删除后恢复结果不变。

## [018] Worker 不写目标、预算、证书与事件

Worker 只交结果。`sphinx_work_submit` 接受的输入不含证书 patch、预算 debit、任意事件写入、目标修订；越权提交被拒绝，且该拒绝本身不携带业务事实。语义提议不扩大实际权限。

## [019] 一个 canonical envelope 承载一个原子 TransitionBatch

一个 inquiry 逻辑迁移的多个事件封装在一个 canonical EventEnvelope 中；Integrator 先完整验证 batch 并 fold 到临时值，再整体接受。batch 内任一项非法，整个 batch 不成为 accepted current。当前严格 canonical 载体的事件类型为 `sphinx/v2-transition@2`；历史 `sphinx/v2-transition@1` 只读入即报 `LEGACY_TRANSITION_UNSUPPORTED` 并形成语义 cut，不自动补齐缺失的 body 字段，也不恢复为新 current。

## [020] 三哈希不得混称

计算对象与排除字段如 **[008]** 所定义。同长度不同内容必须给出不同 hash；wire 与 JS surface 不得把三者当同一个值传递。

## [021] 观测与解释分成两个可恢复阶段

第一事务记录结构合法的 `ResultAccepted`、工作完成、可验证用量与 `InterpretationPending`。第二事务运行纯 Observe，记录 Graph/Certificate delta 与 `InterpretationApplied`。插件异常不重调 LLM；原始回答永久保留；修复代码产生新实现 hash/版本，在显式的新派生 inquiry 中重处理。

## [022] 测量与干预分开记录

同一快照下的换序、标签遮蔽是 measurement；加入新理由、反例、候选是 intervention。问前问后都留回答与可见输入。问后改变不自动等于改善，问后不变不自动等于正确。

## [023] 随机化与协议登记实际值

每轮登记模板 ref 与 hash、呈现集合与省略集合、分配、`ClusterId`（同 cluster 的回答不是独立票）、预期回答数、最大尝试数与缺失处理策略；再加 seed、算法版本、模型/provider/config fingerprint。worker 只收到 opaque label，收不到 label→真实作者映射；集合外的 label 拒绝。不默认"第一个合法回答胜出，余下取消"。

## [024] 设计矩阵的名称只写实际验证过的

Borda 只作描述性基线，不是似然，除 complete equal-exposure 外必须标明扩展与实际 exposure。不满足平衡条件时记录真实 exposure 与不平衡，不美化名称。程序目前没有 BIBD 实现；若将来引入，只有实际验证 `vr=bk`、`r(k−1)=λ(v−1)` 及全部成对出现次数后才可称 BIBD。

## [025] 序数似然的实现契约

成对与排序似然在 log 空间计算：`logSigmoid = -softplus(-x)`、`logSumExp` 归一，极端比较不溢出。tie 用声明的三向 softmax（win/lose/tie）建模，不拆成两张相反票；`abstain`、`tie`、`conditional` 是三种各自可解码的回答，不折叠成数值。原始 ballot 永久保留，算法升级从原始数据重拟合。

【程序待改】gauge 固定、完整协方差（或可查询的协方差算子）与 `Var(theta_i-theta_j)` 组合、未连通图返回 typed unidentifiable、迭代触顶时标 `Converged=false`——这些在 `wanxiangli/ordinal` 与 `Plugins/Ordinal` 里都没有实现；当前库只交付给定 theta 的 log 空间似然与描述性计数，没有拟合器，也没有不确定度输出。

## [026] 局部保证不自动跨算子升级

pairwise/ranking 的结果不可写成已证上界；模型后验不等于"概率=实际正确率"；`ExactWithinModel` 只在其声明模型内成立。证书 wire 解码必须让 posterior 类保证与外部正确性类保持两个不可互换的类别。语义反例不可让 Core 无条件删候选。

【程序待改】A* 界与 MCTS 覆盖半径在本程序没有对应算子；`DeterministicBound` 作为证书保证类型存在但暂无生产者。要么实现有界搜索并交付该保证，要么连类型一起删。

## [027] 固定点与收敛只声明可证明的部分

closure 跑到声明上限就报上限与残余，不得用"循环跑到 20 次"伪装数学收敛。无变化不刷 inquiry revision。

## [029] answer.now 必须进入候选集合

成稿（`answer.now`）与其它计划一样参与估值：绝不免费、绝不零贡献，也不作为"没有别的可跑"时才出现的兜底；比较其它计划时考虑其资源消耗与后续成稿可能性。成稿不可执行时按 `no-executable-plan` 或资源边界记录，或交回已有成稿，不凭空合成答案。"现在作答"与"再做一次综合修订"可以是不同计划，但须解释差别，不得让同一成稿动作以两个名称重复占优。

【程序待改】没有独立的渲染预算拒绝码；渲染失败目前只有 `BlankAnswerText`、`UnknownArtifactRef`、`UnresolvedBeyondScope` 三类 fault。

## [030] 停止依据只覆盖已列出的计划

停止证据不证明没有尚未生成的更优路径。缺 value-of-computation 数据是缺失，不是检查通过。缺证据时按操作结束记录，不声称认识收敛。

## [031] 探针必须真的可被提出、派发、吸收和重新估值

每个启用探针都在 catalog 里登记，可经公开 API 派发并被吸收、可重新估值。`not-applicable`、空发现、未找到反例都是成功完成的合法结果，不重试到模型说出预设答案。ProbeManifest 记录响应 schema 的 id 与 hash、额外响应字段、所需工具能力（请求不等于授权）、默认写权限（探针默认只读）、响应字节上限、finding 转图 delta 的形状；不含 `qualityWeight`、`methodUtility`、`expectedRootGainDefault` 一类的先验收益字段。

库外问题走已登记的 `sphinx.probe.open-question@2`：自然语言问题 + 可见材料 + 已注册通用 response schema + 已授权工具。不能提交新插件、动态 import 或任意工具脚本。重复有效的动态问题可在后续版本成为新模板；本 inquiry 内不自动改 plugin lock。

## [033] 生产入口必须有 durable store

生产入口用 `SPHINX_COMMON_DIR` 选定持久工作区；缺失或空白即启动失败并给出原因，绝不静默退回内存 store。内存端口只在显式测试 profile 下可用。

## [034] Host receipt 必须来自实际 adapter

Runtime 生成的 intent hash 不得命名为 childSessionId。abort 未确认前状态是 `cancelling`，不得报 `cancelled` 或 `drained=true`。`session.idle` 不是有效结果存在的证据，必须实读 message/tool-result 并核对 work token、schema、attempt。

## [035] MCP 协议版本与 Sphinx API 版本分开

Sphinx `apiVersion = "2"` 是业务契约，独立于 MCP 协议版本。不得只改 `protocolVersion` 字符串冒充协议升级。业务 `awaiting_results` 作为已完成 tool result 的内容返回，不误用协议级 input-required。

## [036] 工具白名单只公布真正可用的五件

`sphinx_inquiry_start`、`sphinx_work_submit`、`sphinx_inquiry_status`、`sphinx_inquiry_cancel`、`sphinx_inquiry_export`。start 落盘用户计划并派发依赖已实际成功的步骤，最后一步的结果成为答案；submit 交回 worker 持有的步骤结果，重复或迟到的回执不重复计票、不重复派发；status 读出下一步（`nextStep`）与答案，不创建 lease、不调用模型、不改变业务状态；cancel 停止新派发并关闭未了工作；export 只读。

未接入驱动的能力不公布：下一步由 status 的 `nextStep` 给出，没有单独的取工单工具；start 只收 `authorizationRef` 作为用户授权引用，没有独立成件的 goal amendment 工具，就不广告为可用。旧的 assess/propose/investigate/synthesize 阶段接口与 `sphinx` 原生工具面都不存在，也不以同名 alias 复用。

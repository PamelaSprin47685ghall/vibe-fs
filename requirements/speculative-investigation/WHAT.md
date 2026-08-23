# speculative-investigation — WHAT

## SPEC-INV-001: 优化目标与零影响基线

投机机制仅用于在符合条件的 deep Work provider 请求前进行机械只读调查。在投机禁用、熔断触发、证据不足或策略判定为 K0 时，普通 Work Session 的 provider 可见字节、工具权限、Fallback 流程、评审终结逻辑与控制流必须与无投机状态完全一致，投机绝不作为任务正确性的必要条件。

## SPEC-INV-002: Eligible Opportunity 判定

实际投机仅在同时满足以下条件时被允许：根会话为 `SessionExecutionClass.Work`；请求类型为 `ProviderRequestKind.WorkMain`；角色属于 `{Coder, Inspector, DevOps, Inquiry}`；Authority 选择 Deep 且 `EffectiveAgent = SelectedAgent`；非 Fallback 分支、非交互修复、非前缀探测、非 Reviewer/Finality、非 Attached/InternalLeaf；Owner 未取消；可唯一绑定即将消费输入的 `TargetProviderRun`；存在同角色的 fast peer 且显式成本模型判定收益为正；EventStore 与宿主 Canary 均健康。任一条件未知或不满足立即判定为 K0。

## SPEC-INV-003: 预算单位 K

投机预算 `StrengthBudget` 取值属于 `{K0, K1, K2}`，其中 K 严格代表 **Replica 的 provider request 次数**，而非工具调用次数。单个 provider request 可并发产生多个允许的工具调用，且全部 call/result 完整配对后构成一个 batch。宿主在收割第 K 个 request 结果后物理阻止第 K+1 次外发；Replica 返回纯文本补全时立即终止投机，其文本正文严禁注入主模型。

## SPEC-INV-004: Replica Authority 结构约束

Replica 构造为 `InternalLeaf × Attached(owner, StrengthReplica)`，使用 `fast-<owner-role>`；继承 Owner 的 SessionPersona 与 SessionProviderLanguage，仅切换执行绑定至 fast EffectiveAgent，其物理执行目标由调度器解析。Replica 拥有短生命周期，完成即释放，不跨决策复用；无 Companion、无嵌套投机、无深度 Fallback 或权限交互。其可见工具 Schema 与底层执行门禁严格同源且仅允许 `read/glob/grep`，任何其他工具调用直接 fail closed。

## SPEC-INV-005: Candidate Frame 确定性规范化

投机仅保留真实的宿主工具调用与结果交换，不保留 Replica 的分析推理文本。每个候选帧保留 request batch 边界、确定性排序、规范化参数、真实执行结果与内容 digest；call/result 严格一对一配对。Owner 侧的合成调用标识必须由 Owner SessionId、DecisionId、序号与语义 digest 确定性派生，严禁引入随机数或时间戳；超出硬性字节上限的投机结果整体丢弃为 K0。

## SPEC-INV-006: Prepared Candidate 不等于历史

可用的候选帧在主模型真正读取前必须先向 EventStore 写入 `StrengthCandidatePrepared` 事件并持久化引用；大对象仅通过 payload_refs 关联，禁止引入私有存储。未被 Promote 的 Prepared Candidate 严禁进入 XTrace、Companion 或未来持久化历史。Prepared 写入明确失败时安全降级为 K0；写入状态未知时必须重新校验，无法证明已提交时禁止外发目标请求。

## SPEC-INV-007: Promotion 仅由消费证据产生

只有当协调后的轮次证据明确证明 `turn.ProviderRun = Candidate.TargetProviderRun` 且该运行产生了真实的非空输出时，方可追加 `StrengthCandidatePromoted` 事件。请求尚未发起、纯传输错误、空失败或已终止的运行严禁执行 Promotion。Promoted 必须引用与 Prepared 完全一致的 digest 与材料；Promotion 写入状态未知时必须重新解析，未证明前后续 continuation 保持 fail closed。

## SPEC-INV-008: Replay 与 XTrace 闭包

当前目标请求中的 Candidate 不进入 XTrace 捕获范围。Promotion 完成后的下一次主变换必须在 XTrace 捕获前将 Promoted frames 确定性重建至其因果位置（目标 assistant 输出之前）；随后的 XTrace 捕获将其纳入持久化时间线并记录 `StrengthFramesTraced` 游标范围。Promoted frames 在被后续压缩机制完整覆盖前必须保持可 raw replay。

## SPEC-INV-009: Projection 与 No-Reflection 规则

Replica 的 provider 消息基础采用 Owner 冻结点上的语义投影与本决策已完成的局部 batches；Owner 的内部 ToolCallId 严禁直接进入 Replica wire，必须确定性重定位为决策内局部标识并保证语义不变。当前 Candidate 在冻结之后产生，严禁反射回当前 Replica 会话；新决策不复用旧 Replica 上下文。

## SPEC-INV-010: Predictor 与 Deterministic Control

系统默认处于 Shadow 模式（只预测、保持 K0 并观察后续主请求）。仅在显式成本模型、宿主 Canary 指纹、确定性 Control 组与充足样本证据同时就绪时方可激活 K1 treatment。Treatment 开启后仍保留基于不可变事实计算的确定性 control holdout；训练标签仅来源于 Shadow/Control 组的主模型真实请求序列，Replica 的干预请求严禁作为反事实标签。

## SPEC-INV-011: 失败、取消与熔断

Replica 的普通执行失败仅终止当前投机决策，主会话正常继续。Owner 取消或删除时级联取消并释放 Replica，未消费的 Candidate 不执行 Promotion。Replica 生命周期的终止只能来自显式因果事件：达到 K budget、真实 provider turn terminal、owner 的取消/删除，或 DryRun 所绑定的 exact target provider run 已终结；不得以 elapsed time、deadline race 或超时先后来决定是否收集/取消 Strength。Treatment 既然显式开启，就等待 Replica 的真实因果终态；operator/owner abort 仍可显式取消。出现持久化歧义、投影冲突、权限不匹配或 Canary 失败时，进程全局熔断（新决策全为 K0），熔断在当前进程生命周期内保持生效。已完成的 Promoted 历史不受熔断影响，继续提供正常恢复与重放。

## SPEC-INV-012: 模型不可见与系统可审计

主模型与 Replica 的可见交互文本中严禁包含任何关于投机、副本、预读或副驾的机制提示；Replica 亦不接收辅助预读的身份设定。宿主与 EventStore 审计层保留 DecisionId、ReplicaSessionId、TargetProviderRun、预算 K、digest、预测器特征得分、成本评估与失败原因等完整审计字段。

## SPEC-INV-013: DryRun 可见非阻塞 Shadow 执行

显式 DryRun 模式创建并运行真实的 `StrengthReplica` 物理子会话，作为可观察的内部执行暴露给宿主环境。Owner 主路径启动 DryRun 后立即继续推进，**绝不等待 DryRun 完成或其超时**。DryRun 可真实执行只读请求并记录宿主审计日志，但其产物不映射回 Owner 上下文，不生成 `StrengthCandidatePrepared` 或 `StrengthCandidatePromoted` 事件，不影响主会话的恢复与终结状态。

DryRun 自身不拥有 wall-clock deadline。它先由自己的 K gate 或真实 Replica terminal 收口；若这些尚未发生而 Owner 的 exact `TargetProviderRun` 已经终结，则以该 target terminal 作为 observation horizon 的因果结束点并取消仍活着的 Replica。谁先发生谁收口，不比较毫秒。Harness watchdog 仅负责识别整个物理系统失去进展，不参与 Strength 业务语义。

# speculative-investigation — WHAT

## [001] 零影响基线

投机只优化 Work provider 请求前的机械只读调查，不作为正确性的前提。禁用、熔断、证据不足或 K0 时，Work Session 的 provider 可见字节、工具权限、retry、评审终结与控制流须与无投机时一致。

## [002] Eligible Opportunity

只有以下条件全部确定成立才允许投机，否则为 K0：根会话是 Work，请求是 WorkMain；冻结 Authority 的 Role 属于 `{coder, inspector, devops, inquiry}` 且 participant 一致；不是交互修复、前缀探测或 Attached/InternalLeaf；Owner 未取消；即将消费输入的 TargetProviderRun 唯一确定；固定 Role 的唯一远端模型可用，显式成本模型判定收益为正；EventStore 与 Host Canary 健康。

## [003] StrengthBudget

K 仅取 K0、K1、K2，计量 Replica 的 provider request 次数。一次 request 的所有允许工具调用及结果完整配对为一个 batch；收割第 K 次结果后，必须物理阻止第 K+1 次外发。纯文本补全立即结束投机，正文不得注入主模型。

## [004] Replica Authority

Replica 是 `InternalLeaf × Attached(owner, StrengthReplica)`，通过 owner-derived evidence 继承 Owner logical run 的 participant、固定 Role、Persona 及 provenance/version、SessionProviderLanguage。只使用该 Role 的唯一远端模型，不切换为 predictor 或其他角色；每次决策新建、完成即释放，无 Companion、嵌套投机、provider failure budget 或权限交互。工具 Schema 与执行门禁同源，仅允许 read/glob/grep，其余一律拒绝。

## [005] Candidate Frame

Candidate 仅含真实工具调用与结果，不含 Replica 推理文本；保留 request batch 边界、确定性顺序、规范化参数、真实结果及 digest，call/result 一一配对。Owner 调用标识由 Owner SessionId、DecisionId、序号和语义 digest 确定性派生，不使用随机数或时间戳。结果超过硬性字节上限时整体丢弃为 K0。

## [006] Prepared Candidate

主模型读取 Candidate 前须持久化 `StrengthCandidatePrepared` 及其引用，大对象仅用 EventStore 的 payload_refs。未 Promote 的材料不进入 XTrace、Companion 或未来持久历史。Prepared 明确写入失败时降为 K0；状态未知时重新核实，不能证明已提交则不外发目标请求。

## [007] Promotion

仅当协调后的轮次证据证明 `turn.ProviderRun = Candidate.TargetProviderRun` 且产生真实非空输出时，才追加 `StrengthCandidatePromoted`；未发起请求、纯传输错误、空失败及已终止运行不得 Promote。Promoted 的 digest 与材料须与 Prepared 一致；写入状态未知时重新核实，不能证明则拒绝后续 continuation。

## [008] Replay 与 XTrace

当前目标请求中的 Candidate 不进入 XTrace。Promotion 后的下一次主变换须在 XTrace 捕获前，将 frames 确定性重建到目标 assistant 输出之前，并以 `StrengthFramesTraced` 记录捕获的游标范围；完整压缩覆盖前保持 raw replay 能力。

## [009] Projection 与 No-Reflection

Replica 输入仅来自 Owner 冻结点的语义投影与本决策已完成的局部 batches。Owner ToolCallId 须确定性重定位为决策内标识并保持语义；冻结后产生的当前 Candidate 不得反射回当前 Replica，新决策不复用旧 Replica 上下文。

## [010] Predictor 与 Deterministic Control

默认 Shadow：只预测、维持 K0、观察后续主请求。显式成本模型、Host Canary 指纹、确定性 Control 组与充足样本证据全部就绪，才可开启 K1 treatment；开启后仍保留基于不可变事实的确定性 control holdout。训练标签只取自 Shadow/Control 的主模型真实请求序列，不使用 Replica 干预请求充当反事实标签。

## [011] 失败、取消与熔断

Replica 普通失败只终止该决策，Owner 继续；Owner 取消或删除时级联取消并释放 Replica，未消费 Candidate 不 Promote。Replica 只因 K budget、真实 provider terminal、Owner 取消/删除或 DryRun 的 exact target terminal 结束，不以时长、deadline 或超时竞争裁决。Treatment 等待真实因果终态，operator/owner 可显式取消。

持久化歧义、投影冲突、权限不匹配或 Canary 失败时，进程全局熔断且本进程内不恢复，新决策全为 K0；已 Promoted 历史仍正常恢复、重放。

## [012] 可见性与审计

主模型与 Replica 可见交互不含投机、副本、预读或副驾机制提示，Replica 不接收辅助预读身份。Host 与 EventStore 保留 DecisionId、ReplicaSessionId、TargetProviderRun、K、digest、预测特征得分、成本评估及失败原因等审计材料。

## [013] DryRun

显式 DryRun 创建并运行 Host 可见的真实 StrengthReplica 子会话。Owner 启动后立即继续，不等待 Replica 结束；Replica 可执行只读请求并留审计，其产物不回写 Owner、不产生 Prepared/Promoted、不影响 Owner 的恢复与终结。

DryRun 无 wall-clock deadline，由自身 K gate、真实 Replica terminal 或 Owner exact TargetProviderRun terminal 中先发生的因果事件结束；后者取消仍活着的 Replica。Harness watchdog 只监督系统失去进展，不裁决业务终态。

## [014] Ablation 优先

`feature-ablation` 将本节点设为 ablated 时强制 Off，优先于 `WANXIANGSHU_STRENGTH_MODE`；borrowed/active 时仍由该设置控制 Shadow、DryRun、Treatment（见 `feature-ablation` [002]）。

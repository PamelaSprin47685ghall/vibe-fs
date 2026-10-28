# 规范包索引 (Package Index)

当前索引收录 **47 个功能说明包**与 **1 个存档包**（`epistemic-reasoning`，已被 `sphinx-v2` 取代，仅存归档索引）。
包的划分服务于理解总流程中的各个子领域，不是官僚条款。

## 1. 规范与消融 (Requirement & Ablation)
| Package | 说明 |
|---|---|
| `verification-system` | 测试分层与调度说明。 |
| `feature-ablation` | 模块开关与消融机制。 |
| `js-semantic-surface` | JS 语义入口契约。 |

## 2. 流程与时间 (Programming & Causality)
| Package | 说明 |
|---|---|
| `structured-workflow` | 普通代码流程表达，避免自建执行器。 |
| `time-capability` | 物理时间与显式等待时限。 |
| `causal-wait` | 结构化因果等待与诊断。 |

## 3. 会话与宿主 (Session & Host)
| Package | 说明 |
|---|---|
| `session-ontology` | 会话分类、所有权与拓扑正交。 |
| `managed-session-lifecycle` | 托管会话生命周期与收束。 |
| `host-boundary` | 宿主交互边界与物理能力隔离。 |

## 4. 参与者与权限 (Participant & Capability)
| Package | 说明 |
|---|---|
| `participant-identity` | 身份定义：名分、人格与物理绑定分离。 |
| `execution-model-routing` | 调度器根据角色与负载分派模型。 |
| `office-capability` | 职位边界与后果权限。 |
| `capability-enforcement` | 工具面与运行时门禁一致性。 |
| `participant-horizon` | 上下文视界与最小必要事实。 |
| `cognitive-environment` | 长期背景与执行状态隔离。 |
| `cognitive-workspace` | 认知边界：什么进持久状态（待办、事实），什么只留在对话里（assume）。 |
| `attention-regulation` | 注意力调节：assume 与 scope 文本各管什么。 |
| `action-affordance` | 决策动作与参数语义。 |
| `provider-language` | 单会话语言固定与协议标识符约定。 |
| `provider-projection` | 语义意图到模型呈现的投影。 |

## 5. 交互与持久化 (Interaction & Durability)
| Package | 说明 |
|---|---|
| `concern-routing` | 交互路由边界：用户关切如何进入编程面委托。 |
| `interaction-authority` | 逻辑交互的权限与因果推进。 |
| `managed-chat-execution` | 消息级物理执行与状态。 |
| `dispatch-protocol` | 消息派发与幂等性保障。 |
| `durable-events` | 事件日志持久化与重放底座。 |
| `effect-accounting` | 外部副作用三态记账。 |
| `durable-convergence` | 状态收敛法则。 |

## 6. 协作与执行 (Work & Execution)
| Package | 说明 |
|---|---|
| `delegation` | 任务派工与责任移交。 |
| `advisor` | 专席顾问协商与散文反馈。 |
| `intra-participant-parallelism` | Fission 同一参与者多路并发与收敛。 |
| `process-execution` | 外部进程执行与时限管理。 |
| `change-integration` | 代码提交与集成短门。 |

## 7. 连续性与恢复 (Continuity & Recovery)
| Package | 说明 |
|---|---|
| `semantic-trace` | 原始交互轨迹留痕。 |
| `context-compression` | 上下文重置与压缩机制。 |
| `prefix-stability` | 前缀逐字节稳定性保障。 |
| `execution-failure-policy` | 失败分类与应对策略。 |
| `provider-attempt-recovery` | 模型调用异常时的换绑与恢复。 |
| `host-provider-failure-ownership` | 接管宿主失败，统一恢复所有权。 |
| `crash-reconciliation` | 进程崩溃现场恢复与状态调和。 |
| `latency-degeneration` | 按 session 的请求耗时看门狗：超阈值中断一次并续跑。 |

## 8. 任务与编程面 (Work & Programming)
| Package | 说明 |
|---|---|
| `obligation-ledger` | 编程面待办管理约定。 |
| `work` | 任务阶段接力、长程连续性与结果交付。 |
| `repository-investigation` | 本地事实调查纪律。 |
| `knowledge-reuse` | 案例经验参考与复用。 |
| `repository-programming` | JS 编程面核心操作规范。 |
| `sphinx-v2` | Sphinx 结构化问答与 MCP 适配。 |
| `distribution` | 安装包分发与依赖闭合。 |
| `epistemic-reasoning` | （存档包）已被 sphinx-v2 取代，仅存归档索引，不作为规范。 |

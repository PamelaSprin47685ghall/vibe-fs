# Package index

当前设计得到 **57 张 boundary card**。57 不是目标，也不是稳定 API；它只是当前按独立 WHY、failure meaning 与 independent-change test 得出的结果。后续全仓反向覆盖若发现 ORPHAN / OVERLAP / GARBAGE，应继续拆并。

## 1. Requirement system

| Package | 一句话 WHY |
|---|---|
| `requirement-system` | 当前接受的产品真理必须有唯一 package owner、显式依赖与唯一 proof ownership。 |
| `verification-system` | requirement acceptance 必须由分层、可失败、可重放的证据体系定义，而不是测试类型或人工印象。 |
| `feature-ablation` | 巡检与渐进验收需要正式消融开关与 DAG，使下游未审机制可零影响关停而不改源码；消融目录与新角色体系严格同步。 |
| `js-semantic-surface` | 语义测试只能经正式、稳定、JS-native 的 semantic surface 进入；Fable runtime representation 不属于 semantic contract。 |

## 2. Programming / causality

| Package | 一句话 WHY |
|---|---|
| `structured-workflow` | 业务流程应由宿主语言结构直接表达，不能在领域层再造第二程序计数器/runtime。 |
| `time-capability` | 时间与等待的物理能力必须显式进入系统，不能由 ambient clock/timer 偷渡业务判断。 |
| `causal-wait` | 等待必须可诊断、可观测，但诊断观测不能升级为业务 authority。 |

## 3. Session / Host substrate

| Package | 一句话 WHY |
|---|---|
| `session-ontology` | execution class、ownership、attachment 与 personhood 必须正交，否则 runtime topology 会冒充业务身份。 |
| `managed-session-lifecycle` | managed session 的创建、复用、取消、retire、replacement 与 owner closure 必须有单一生命周期合同；旧身份显式收束，固定 DevOps 崩溃恢复维持单一执行权威。 |
| `host-boundary` | 外部 Host 只有提供一组最小、可验证的物理能力与稳定观察边界，业务语义才不依赖传输噪声或私有实现。 |

## 4. Participant / provider world

| Package | 一句话 WHY |
|---|---|
| `participant-identity` | Role、Persona、ExecutionBinding 必须分离，使换执行者不等于换人；活跃身份解析与历史身份隔离解码。 |
| `execution-model-routing` | 固定 Role 身份与物理模型策略必须分离；唯一 MJS scheduler 以 `role + running` 决定 ModelTarget，固定 DevOps 模型绑定持久锁定。 |
| `office-capability` | office 必须由有资格产生的后果定义，而不是 persona 名或工具白名单；确立 Engineer（独占 Fission）、DevOps（固有自修与真实执行）、Manager、Orchestrator 四大核心角色。 |
| `capability-enforcement` | provider 看见的 capability 与 runtime 真能执行的 capability 必须同源且不扩大 office entitlement；Fission 独占 Engineer，DevOps 固有自修无需逐次开关，Fork/Resume 权能分离。 |
| `participant-horizon` | machine knowledge 大于 participant experience；只有会改变合法行动的最小事实应穿过 horizon；Manager 并行来自派出多名 Engineer 而非自身分身。 |
| `cognitive-environment` | 世界观、身份、自我职责与继承知识必须按稳定认知层组织，瞬时 runtime/mission 不能伪装成长期身份。 |
| `attention-regulation` | participant 必须能显式结束 evidence churn、解除自创心理债、延后非阻塞旁支，而不把这些 speech act 冒充事实或 obligation。 |
| `action-affordance` | participant 在采取一个 action 的决策点必须知道该 act 的正边界、负边界、成功后果与参数意义。 |
| `provider-language` | 一个 participant life 必须生活在单一、稳定的自然语言世界中，而 protocol identity 保持语言不变；核心角色双语 Prompt 语义同源一致。 |
| `provider-projection` | 已决定可见的 typed semantic intent 必须经唯一确定性投影变成 provider representation，表示不能反向创造 authority。 |
| `external-investigation` | [已撤销] Browser 角色与专属集成全链撤销且无替代代理，外部调查职责不转移且禁止通过通用执行工具复活。 |

## 5. Interaction / effect / durability

| Package | 一句话 WHY |
|---|---|
| `concern-routing` | participant 之间按 concern-addressed mailbox 通信；发送者不依赖身份拓扑，消息只在自然 Pair Hint 边界打断注意力。 |
| `interaction-authority` | 物理 user-shaped message 不等于 authority；历史事件保持原样且旧身份不升权，DevOps 恢复与续行锁定固定模型与单一执行权威。 |
| `managed-chat-execution` | 每个物理消息的 durable acceptance、provider start、唯一终态与 exact settlement 必须由消息级执行 owner 统一管理。 |
| `dispatch-protocol` | 已获授权的 interaction 穿过不可靠 Host 时必须避免 uncertain outcome 复制逻辑效果。 |
| `durable-events` | durable truth 必须以不可变事实、原子提交与确定性 fold 形成单一可重放 substrate。 |
| `effect-accounting` | 外部副作用的请求、物理发生与确认必须分型；unknown outcome 不能伪装成未发生或成功。 |
| `durable-convergence` | 多个各自合法发展的 durable replicas 必须按对象语义收敛，而不是靠 wall-clock/LWW 猜赢家。 |

## 6. Work / execution

| Package | 一句话 WHY |
|---|---|
| `delegation` | 一项语义工作交给另一 participant 时，authority、charge、owner 与返回后果必须明确；Manager 派发 Engineer 与续做固定 DevOps，禁止跨角色向后差遣；Sphinx 程序内部同步只读 Engineer 调研。 |
| `intra-participant-parallelism` | 同一个 participant（仅限 Engineer）可拥有多个 coequal execution presents，而 identity/authority/responsibility 与最终 completion 仍保持一个。 |
| `process-execution` | participant 控制真实进程/PTY 时必须得到有界、可终止、物理完成可信的 execution semantics，承接大输出零 Distiller 留尾截断与 Large Gate 门禁。 |
| `output-distillation` | [已撤销] 任意输出规模零 Distiller 模型会话与彻底去角色化，大输出留尾截断与程序事实迁入 process-execution。 |
| `change-integration` | 独立 Git 工作道路进入共享 ref 时必须在短原子门内发布，长 review/repair 不应被全局串行化；DevOps 自修推进快照触发证书失效与独立重评。 |

## 7. Context continuity

| Package | 一句话 WHY |
|---|---|
| `semantic-trace` | participant life 中不可丢失的原始语义历史必须有 append-only、可定位的事实表示；Fission 多 present 确定性 keyed 汇聚，独立 invocation 范围与 resume 边界隔离。 |
| `work-record` | 跨 participant/review/finality 传递的一段 work 必须有 bounded canonical statement（LWR）；Fission 汇聚生成单次 Invocation 唯一 Canonical Record。 |
| `context-compression` | 当历史过长时，只能以受控、证据边界明确的 semantic memory 替代可压缩部分。 |
| `prefix-stability` | 同一 semantic epoch 内已呈现给 provider 的前缀必须保持稳定；冷边界只能由事实驱动。 |

## 8. Failure / recovery

| Package | 一句话 WHY |
|---|---|
| `execution-failure-policy` | 执行失败必须先收敛为封闭类型，再由唯一纯策略一次性裁决 retry、fallback、capacity、message 与 fatal 后果。 |
| `provider-attempt-recovery` | 单次 provider attempt 已失败后，可在不改变 authority/personhood 的前提下有界换执行绑定继续。 |
| `host-provider-failure-ownership` | 万象术启用时无条件拥有 provider 失败恢复；Host 重试归零，claimed 错误抑制默认弹窗并由万象术逐 provider 恢复。 |
| `crash-reconciliation` | 进程/插件中断后只能从 durable facts 与可信物理观察重新进入普通程序，不能从临时内存或猜测恢复；固定 DevOps 崩溃恢复保持单一执行权威与命令去重。 |
| `degeneration-guard` | 尚未结束的 attempt 若 token 多样性越出正常语料经验边界，应在污染更多历史前主动终止并由本包自行要求改写。 |

## 9. Mission / relay

| Package | 一句话 WHY |
|---|---|
| `obligation-ledger` | 长期 mission 必须持续维护当前仍欠世界什么，而不是用 phase/status 伪装工作进度。 |
| `relay-incumbency` | 每一轮都在共享工作区上从权威用户消息重新开始并独立评估；同一 Road 至多一个 active 迭代，退休永不恢复；固定 DevOps 跨任期连续。 |
| `relay-assessment` | 每任至多一次八维整数评审；独立评估由只读 Engineer 支持，低分原位接责，DevOps 自修使旧快照证书失效并由后任独立重评。 |
| `relay-retirement` | 退出是唯一正常出口；只有递归 live 资源能阻塞退休，固定 DevOps 跨任期连续且在退休中受明确收束边界保护。 |
| `relay-context-projection` | 审计保留全量历史，provider 消息上下文只含权威消息与本轮消息，并在共享工作区上执行；固定 DevOps 执行事实通过客观记录感知、前任私有上下文隔离。 |

旧 `review-judgement` / `review-assurance` / `finality` 已按 Manager 循环语义 clean break：评审并入 `relay-assessment`，终结并入 `relay-retirement`，上下文切段并入 `relay-context-projection`。

## 10. Feedback

| Package | 一句话 WHY |
|---|---|
| `behavior-diagnosis` | 工程病理只能在满足明确 trigger / negative / distinction 的证据上成立。 |
| `guidance-delivery` | diagnosis 成立不等于必须立刻重复告知；反馈需要独立的 occurrence、coverage、dedupe 与 horizon-relative delivery 语义。 |
| `institutional-learning` | celebrate/regret 必须把一次经历压成 ABSORB/BIRTH/DISCARD，使成功与教训能改变 canonical Enforcer 而不让规则库只增不减。 |

## 11. Repository knowledge / programming

| Package | 一句话 WHY |
|---|---|
| `repository-investigation` | repository claim 必须由可定位、可追溯的真实观察建立，reasoning 不能冒充 evidence acquisition。 |
| `knowledge-reuse` | 过去的 repository knowledge 可作为 best-effort cache/hint 复用，但不能冒充当前证明；双基线引用与真实 diff 驱动 Bookkeeper 刷新，废除严格 replay 循环。 |
| `repository-programming` | repository 变换需要能力投影、可组合、sandboxed、all-or-nothing 的 programming surface，而不是多套漂移 RPC；事务 ReadSnapshots 与案例实质访问严格分离，统一直接文件与编程工具。 |
| `requirement-grounding` | 代码路径触碰时，适用 requirement package 必须在 effect 前以可重放 read 语义进入当前 participant horizon。 |

## 12. Optional optimization / epistemics

| Package | 一句话 WHY |
|---|---|
| `speculative-investigation` | 可丢弃 speculation 只有在 authoritative world 零影响时才可换取调查成本下降。 |
| `epistemic-reasoning` | Sphinx 探究流程全程序控制，无 Inquiry 角色与模型驾驶层；认识状态区分 proposal/evidence、保留依赖与不确定性；内部只读 Engineer 同步调研。 |

## 13. Delivery

| Package | 一句话 WHY |
|---|---|
| `distribution` | 可安装 artifact 必须携带运行所需代码与 semantic resources，同时排除不属于交付面的源码/开发资产；打包资源与活动注册严格同步。 |

# 关键拆分裁决

本轮相对旧 36 工作集作出这些结构变化：

- `participant-guidance` → `cognitive-environment` + `action-affordance`：长期自我/知识环境与调用时 action contract 可独立重大变化。
- 新增 `provider-language`：语言绑定不是 identity、horizon 或 renderer 的附属字段。
- `durable-events` 中抽出 `effect-accounting`：事件 substrate 与外部 effect 的 Requested/Accepted/Unknown 语义有不同 failure meaning。
- `process-execution` 中抽出 `output-distillation`：控制真实进程与压缩过大观察是两个 WHY。
- `recovery` → `provider-attempt-recovery` + `crash-reconciliation`：业务 attempt 失败与进程丢失临时状态不是同一故障。
- `review-protocol` → `review-judgement` + `review-assurance`：判断标准可以整体重写而不改变 witness/seal 协议，反之亦然。
- `sphinx` → `epistemic-reasoning`：组件名与 A*/MCTS 等当前算法降为 HOW/proof；package 只保留不可替代的认识论合同。
- 新增 `capability-enforcement`：office consequence 与 schema/runtime gate 同构是两个不同 WHY。
- 新增 `external-investigation`：Browser 角色与专属集成已全链撤销，无替代代理且外部调查职责不转移。
- 废止 `output-distillation`：模型蒸馏与 Distiller 角色彻底撤销，大输出确定性留尾截断与 Large Gate 迁入 `process-execution`。
- 新增 `work-record`：canonical bounded work statement 被 delegation、process review、Finality 共用，不能继续藏在 Companion/Review 下。
- 新增 `requirement-grounding`：路径命中的规范与测试需要自动、去重、可重放地进入开发上下文，弱介入不阻断 mutation。
- 新增 `attention-regulation` / `concern-routing` / `institutional-learning`：最终微原语不是一个“大认知工具包”。`enough/abandon/defer`、`subscribe/publish`、`celebrate/regret → Enhancer` 分属注意力、通信、制度学习三个独立 failure domain；既有 `assume` 保持在 `cognitive-environment`，不重复设计。

# 规范条款索引

本节汇总全仓 **57 个规范包当前全部活跃条款**（以各包 `WHAT.md` 实际文本为准）：

| 序号 | 规范包 (`Package`) | 活跃条款数 | 活跃条款清单与演进导航 |
|---|---|---|---|
| 1 | `requirement-system` | 18 | REQUIREMENT-SYSTEM-001 ~ 018 |
| 2 | `verification-system` | 16 | VERIFICATION-SYSTEM-001 ~ 006、007 ~ 016 |
| 3 | `feature-ablation` | 12 | ABL-001 ~ 009、ABL-010（Primary agent 与 MCP 消融隔离）、ABL-011、ABL-012（角色/能力目录同步与独立开关保证） |
| 4 | `js-semantic-surface` | 6 | JS-SEMANTIC-SURFACE-001 ~ 006 |
| 5 | `structured-workflow` | 17 | STRUCTURED-WORKFLOW-001 ~ 017 |
| 6 | `time-capability` | 8 | TIME-001 ~ 008 |
| 7 | `causal-wait` | 9 | CAUSAL-001 ~ 009 |
| 8 | `session-ontology` | 15 | SESSION-ONTOLOGY-001 ~ 015 |
| 9 | `managed-session-lifecycle` | 24 | MANAGED-SESSION-001 ~ 022、MS-023（身份替换后旧活跃会话显式收束）、MS-024（固定 DevOps 崩溃恢复单一权威与进程排空） |
| 10 | `host-boundary` | 31 | HOST-BOUNDARY-001 ~ 031 |
| 11 | `participant-identity` | 10 | PID-001 ~ 009、PID-010（活跃身份解析与历史身份隔离解码） |
| 12 | `execution-model-routing` | 19 | EMR-001 ~ 017、EMR-018（新角色集合模型路由解耦）、EMR-019（固定 DevOps 模型绑定持久性与禁止借 resume 换模型） |
| 13 | `office-capability` | 12 | OFF-001/003/004/005/006/007/011/012/015、OFF-016（Engineer 职责与独享 Fission）、OFF-017（DevOps 执行与固有非架构级自修授权）、OFF-018（Sphinx 程控探究与内部只读 Engineer 调研）；OFF-002/008/009/010/013/014 已正式撤销归档 |
| 14 | `capability-enforcement` | 24 | ENF-001 ~ 021、ENF-022（Fission 仅 Engineer 准入 fail-closed）、ENF-023（DevOps 固有自修授权禁 allowRepair 逐次开关）、ENF-024（Fork 与 Resume 权能分离） |
| 15 | `participant-horizon` | 15 | PARTICIPANT-HORIZON-001 ~ 014、PARTICIPANT-HORIZON-015（Manager 并行来自派出多名 Engineer 而非自身分身） |
| 16 | `cognitive-environment` | 16 | COGNITIVE-ENVIRONMENT-001 ~ 016 |
| 17 | `attention-regulation` | 6 | ATTENTION-REGULATION-001 ~ 006 |
| 18 | `action-affordance` | 14 | ACTION-AFFORDANCE-001 ~ 014 |
| 19 | `provider-language` | 12 | PROVIDER-LANGUAGE-001 ~ 011、PL-012（核心角色双语 Prompt 语义一致与同源认知） |
| 20 | `provider-projection` | 14 | PROVIDER-PROJECTION-001 ~ 014 |
| 21 | `external-investigation` | 2 | EI-012（Browser 角色与专属集成全链撤销且无替代代理）、EI-013（外部调查职责不转移且禁止通用工具复活）；001~011 已撤销 |
| 22 | `concern-routing` | 7 | CONCERN-ROUTING-001 ~ 007 |
| 23 | `interaction-authority` | 22 | INTERACTION-AUTHORITY-001 ~ 020、IA-021（历史事件不可变与旧身份不升权）、IA-022（DevOps 恢复与续行锁定固定模型与执行权威） |
| 24 | `managed-chat-execution` | 14 | CHATEXEC-001 ~ 014 |
| 25 | `dispatch-protocol` | 0 | 注：派发协议条款统一收敛于各交互子系统 |
| 26 | `durable-events` | 25 | DURABLE-EVENTS-001 ~ 025 |
| 27 | `effect-accounting` | 12 | EFFECT-ACCOUNTING-001 ~ 012 |
| 28 | `durable-convergence` | 11 | DURABLE-CONVERGENCE-001 ~ 011 |
| 29 | `delegation` | 31 | DELEG-001 ~ 017、019 ~ 030、DELEG-031（reusable completion checkpoint 闭合收口）、DELEG-032（Engineer 完成即返回，禁跨角色向后差遣） |
| 30 | `intra-participant-parallelism` | 17 | IPP-001 ~ 011、IPP-012（订正：eligibility 单一 consequence source）、IPP-013 ~ 016、IPP-017（Fission 准入判定公式与主体边界） |
| 31 | `process-execution` | 17 | PROC-001 ~ 012、PROC-013（大输出零 Distiller 与预算留尾截断）、PROC-014（程序事实不从日志推断与截断声明）、PROC-015（字节预算与 UTF-8 边界对齐）、PROC-016（Large Gate 互斥门禁）、PROC-017（ToolResultBound 留尾截断） |
| 32 | `output-distillation` | 1 | DISTILL-014（任意输出规模零 Distiller 模型会话与彻底去角色化）；DISTILL-001~010/013 已撤销，011/012 迁入 PROC-016/017 |
| 33 | `change-integration` | 17 | CHGINT-001 ~ 014、CHGINT-015（修复改变工作树后必须重新验证与证书失效）、CHGINT-016（并行协调隔离）、CHGINT-017（多道路汇聚后必须重新验证） |
| 34 | `semantic-trace` | 12 | SEMANTIC-TRACE-001 ~ 010、ST-011（Fission keyed convergence 与多 Present 轨迹归并）、ST-012（独立 Invocation 范围与 Resume 边界） |
| 35 | `work-record` | 17 | WORK-RECORD-001 ~ 016、WR-017（Fission 汇聚生成单次 Invocation Canonical Record） |
| 36 | `context-compression` | 27 | CONTEXT-COMPRESSION-001 ~ 027 |
| 37 | `prefix-stability` | 15 | PREFIX-STABILITY-001 ~ 015 |
| 38 | `execution-failure-policy` | 14 | EXECFAIL-001 ~ 010、010b、011 ~ 013 |
| 39 | `provider-attempt-recovery` | 23 | PAR-001 ~ 023 |
| 40 | `host-provider-failure-ownership` | 7 | HOSTFAIL-001 ~ 007 |
| 41 | `crash-reconciliation` | 20 | CRASH-001 ~ 019、CRASH-020（固定 DevOps 崩溃恢复单一逻辑权威与命令去重） |
| 42 | `degeneration-guard` | 13 | DG-001 ~ 013 |
| 43 | `obligation-ledger` | 28 | OBLIGATION-LEDGER-001 ~ 028 |
| 44 | `relay-incumbency` | 11 | RELAY-001 ~ 006、008 ~ 009、RELAY-010（道路唯一逻辑 DevOps 与控制权交接）、RELAY-011（任期连续性与归属明确）、RELAY-012（固定 DevOps 初始绑定与恢复唯一性）；RELAY-007 已删除 |
| 45 | `relay-assessment` | 10 | ASSESS-001 ~ 008、ASSESS-009（独立评估由只读 Engineer 支持且实现者不自定答案）、ASSESS-010（DevOps 自修改变快照使旧评估与证书失效且不可冒充新改动验证） |
| 46 | `relay-retirement` | 7 | RETIRE-001 ~ 004、007 ~ 008、RETIRE-009（固定 DevOps 与跨任期资源在退休中的交接与收束边界）；RETIRE-005/006 已删除 |
| 47 | `relay-context-projection` | 9 | PROJ-001 ~ 008、PROJ-009（固定 DevOps 执行事实与前任上下文隔离） |
| 48 | `behavior-diagnosis` | 19 | BD-001 ~ 019 |
| 49 | `guidance-delivery` | 12 | GD-001 ~ 012 |
| 50 | `institutional-learning` | 8 | INSTITUTIONAL-LEARNING-001 ~ 008 |
| 51 | `repository-investigation` | 9 | REPOSITORY-INVESTIGATION-001 ~ 009 |
| 52 | `knowledge-reuse` | 15 | KNOWLEDGE-REUSE-001 ~ 013、KR-014（预算与截断诚实性）、KR-015（废止严格 Replay 与稳定性校验循环） |
| 53 | `repository-programming` | 27 | REPOSITORY-PROGRAMMING-001 ~ 025、RP-026（事务 ReadSnapshots 与案例实质访问严格分离）、RP-027（Engineer 与 DevOps 统一文件工具与编程面生成） |
| 54 | `requirement-grounding` | 12 | REQUIREMENT-GROUNDING-001 ~ 012 |
| 55 | `speculative-investigation` | 14 | SPEC-INV-001 ~ 014 |
| 56 | `epistemic-reasoning` | 34 | EPI-001 ~ 030、EPI-031（Sphinx 探究流程全程序控制，无 Inquiry 角色）、EPI-032（内部 Engineer 调研只读同步有预算可取消）、EPI-033（结果接纳幂等防重复购买）、EPI-034（取消全链贯穿父工具与子 Engineer） |
| 57 | `distribution` | 10 | DISTRIBUTION-001 ~ 009、DIST-010（打包资源与活动注册同步） |

# 依赖骨架

这不是权威优先级，只表示定义所需 guarantee。精确 hard edge 以各 boundary card 的 `DEPENDS ON` 为准；本表是当前完整邻接清单（146 edges，按本 code block 逐项机器计数）。

```text
requirement-system       → 无
verification-system      → requirement-system
feature-ablation         → requirement-system, verification-system
js-semantic-surface      → requirement-system, verification-system
structured-workflow      → 无
time-capability          → 无
causal-wait              → 无
session-ontology         → 无
managed-session-lifecycle→ session-ontology, crash-reconciliation, managed-chat-execution, interaction-authority, participant-identity
host-boundary            → 无
participant-identity     → session-ontology
execution-model-routing  → participant-identity, managed-session-lifecycle, managed-chat-execution, execution-failure-policy, host-boundary
office-capability        → participant-identity
capability-enforcement   → office-capability, participant-identity, attention-regulation, concern-routing, institutional-learning
participant-horizon      → 无
cognitive-environment    → participant-identity, office-capability, attention-regulation, concern-routing, institutional-learning
attention-regulation     → participant-identity, durable-events
action-affordance        → office-capability, participant-horizon
provider-language        → session-ontology
provider-projection      → participant-horizon, provider-language
external-investigation   → office-capability, participant-horizon, host-boundary
concern-routing          → participant-identity, participant-horizon, durable-events
interaction-authority    → participant-identity, session-ontology
managed-chat-execution   → durable-events, interaction-authority, participant-identity, execution-model-routing, execution-failure-policy, host-boundary
dispatch-protocol        → interaction-authority, effect-accounting, host-boundary, durable-events, managed-chat-execution
effect-accounting        → durable-events
durable-events           → 无
durable-convergence      → durable-events
delegation               → office-capability, session-ontology, managed-session-lifecycle, participant-horizon
intra-participant-parallelism → participant-identity, session-ontology, managed-session-lifecycle, office-capability, capability-enforcement, participant-horizon, work-record, process-execution, durable-events, crash-reconciliation
process-execution        → time-capability, host-boundary, participant-horizon
output-distillation      → process-execution, participant-horizon
change-integration       → effect-accounting, durable-events, crash-reconciliation
semantic-trace           → durable-events
work-record              → semantic-trace, context-compression, participant-horizon
context-compression      → semantic-trace, provider-projection
prefix-stability         → provider-projection, context-compression, provider-language, participant-identity
execution-failure-policy → 无
provider-attempt-recovery→ participant-identity, execution-failure-policy, execution-model-routing, interaction-authority, context-compression, prefix-stability
host-provider-failure-ownership → execution-failure-policy, provider-attempt-recovery, host-boundary
crash-reconciliation     → durable-events, effect-accounting, structured-workflow, host-boundary
degeneration-guard       → interaction-authority, dispatch-protocol, host-boundary
obligation-ledger        → durable-events, effect-accounting, semantic-trace
relay-incumbency         → obligation-ledger, participant-identity, durable-events, interaction-authority
relay-assessment         → relay-incumbency, obligation-ledger, participant-identity
relay-retirement         → relay-incumbency, relay-assessment, relay-context-projection, delegation, managed-chat-execution, provider-attempt-recovery
relay-context-projection → relay-incumbency, participant-identity, provider-projection, host-boundary
behavior-diagnosis       → semantic-trace, durable-events, prefix-stability, managed-session-lifecycle
guidance-delivery        → behavior-diagnosis, participant-horizon, durable-events, concern-routing
institutional-learning   → attention-regulation, behavior-diagnosis, durable-events
repository-investigation → office-capability, participant-horizon
knowledge-reuse          → repository-investigation, durable-events, durable-convergence
repository-programming   → office-capability, capability-enforcement, effect-accounting, durable-events, participant-horizon
requirement-grounding    → requirement-system, host-boundary, participant-horizon, provider-projection, interaction-authority, semantic-trace, prefix-stability, repository-programming
speculative-investigation→ repository-investigation, participant-identity, execution-model-routing, participant-horizon, provider-projection, semantic-trace
epistemic-reasoning      → participant-horizon, durable-events, delegation, execution-model-routing
distribution             → 特殊：所有声明 runtime resource 的 semantic packages（不获其语义 ownership）
```

Phase E 审计结论：3 条 coupling edge 已删（见 `AUDIT.md` Phase E）：

```text
structured-workflow  → causal-wait         删（CE builder 是实现耦合，非定义前提）
time-capability      → causal-wait         删（deadline 是可选 escape，条件依赖非 hard）
guidance-delivery    → provider-projection 删（渲染是下游机制）
finality             → participant-horizon 删（包已退役为 relay-*；隐藏 Reviewer 机制随之删除）
```

当前 149 edges 均为 semantic prerequisite（A 的 WHAT 定义需要 B 已提供的 guarantee），无 implementation/presentation/proof coupling。`epistemic-reasoning` 的 durable inquiry、受管 blind branch 与 capacity-safe OpenCode dispatch 分别直接依赖 `durable-events`、`delegation` 与 `execution-model-routing` 的 guarantee；这些不是存储、Host 或 proof 的偶然耦合。

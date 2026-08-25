# Package index

当前设计得到 **53 张 boundary card**。53 不是目标，也不是稳定 API；它只是当前按独立 WHY、failure meaning 与 independent-change test 得出的结果。后续全仓反向覆盖若发现 ORPHAN / OVERLAP / GARBAGE，应继续拆并。
## 1. Requirement system

| Package | 一句话 WHY |
|---|---|
| `requirement-system` | 当前接受的产品真理必须有唯一 package owner、显式依赖与唯一 proof ownership。 |
| `verification-system` | requirement acceptance 必须由分层、可失败、可重放的证据体系定义，而不是测试类型或人工印象。 |
| `js-semantic-surface` | 语义测试只能经正式、稳定、JS-native 的 semantic surface 进入；Fable runtime representation 不属于 semantic contract。 |
| `migration-ledger` | 63 节点 DAG 施工事实必须由机械门禁守护状态机、分类机、证据机、证明机、提交机、变更机、依赖机、覆盖机与基线机。 |
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
| `managed-session-lifecycle` | managed session 的创建、复用、取消、retire、replacement 与 owner closure 必须有单一生命周期合同。 |
| `host-boundary` | 外部 Host 只有提供一组最小、可验证的物理能力与稳定观察边界，业务语义才不依赖传输噪声或私有实现。 |

## 4. Participant / provider world

| Package | 一句话 WHY |
|---|---|
| `participant-identity` | Role、Persona、ExecutionBinding 必须分离，使换执行者不等于换人。 |
| `execution-model-routing` | EffectiveAgent 与物理模型策略必须分离；唯一 MJS scheduler 以 `role + running` 决定 ModelTarget，runtime 只维护 lease occupancy。 |
| `office-capability` | office 必须由有资格产生的后果定义，而不是 persona 名或工具白名单。 |
| `capability-enforcement` | provider 看见的 capability 与 runtime 真能执行的 capability 必须同源且不扩大 office entitlement。 |
| `participant-horizon` | machine knowledge 大于 participant experience；只有会改变合法行动的最小事实应穿过 horizon。 |
| `cognitive-environment` | 世界观、身份、自我职责与继承知识必须按稳定认知层组织，瞬时 runtime/mission 不能伪装成长期身份。 |
| `attention-regulation` | participant 必须能显式结束 evidence churn、解除自创心理债、延后非阻塞旁支，而不把这些 speech act 冒充事实或 obligation。 |
| `action-affordance` | participant 在采取一个 action 的决策点必须知道该 act 的正边界、负边界、成功后果与参数意义。 |
| `provider-language` | 一个 participant life 必须生活在单一、稳定的自然语言世界中，而 protocol identity 保持语言不变。 |
| `provider-projection` | 已决定可见的 typed semantic intent 必须经唯一确定性投影变成 provider representation，表示不能反向创造 authority。 |
| `external-investigation` | 外部/public-web facts 必须以 provenance、source quality 与 disagreement-aware observation 建立，不能由可达性或外部可能性冒充本地义务。 |

## 5. Interaction / effect / durability

| Package | 一句话 WHY |
|---|---|
| `concern-routing` | participant 之间按 concern-addressed mailbox 通信；发送者不依赖身份拓扑，消息只在自然 Pair Hint 边界打断注意力。 |
| `interaction-authority` | 物理 user-shaped message 不等于 authority；只有 typed provenance 能创建或继续 logical interaction。 |
| `dispatch-protocol` | 已获授权的 interaction 穿过不可靠 Host 时必须避免 uncertain outcome 复制逻辑效果。 |
| `durable-events` | durable truth 必须以不可变事实、原子提交与确定性 fold 形成单一可重放 substrate。 |
| `effect-accounting` | 外部副作用的请求、物理发生与确认必须分型；unknown outcome 不能伪装成未发生或成功。 |
| `durable-convergence` | 多个各自合法发展的 durable replicas 必须按对象语义收敛，而不是靠 wall-clock/LWW 猜赢家。 |

## 6. Work / execution

| Package | 一句话 WHY |
|---|---|
| `delegation` | 一项语义工作交给另一 participant 时，authority、charge、owner 与返回后果必须明确而不泄漏 runtime topology。 |
| `intra-participant-parallelism` | 同一个 participant 可拥有多个 coequal execution presents，而 identity/authority/responsibility 与最终 completion 仍保持一个。 |
| `process-execution` | participant 控制真实进程/PTY 时必须得到有界、可终止、物理完成可信的 execution semantics。 |
| `output-distillation` | 过大执行输出需要有损但诚实地压成可继续使用的观察，而不能把 fragment 当整体成功或发明因果。 |
| `change-integration` | 独立 Git 工作道路进入共享 ref 时必须在短原子门内发布，长 review/repair 不应被全局串行化。 |

## 7. Context continuity

| Package | 一句话 WHY |
|---|---|
| `semantic-trace` | participant life 中不可丢失的原始语义历史必须有 append-only、可定位的事实表示。 |
| `work-record` | 跨 participant/review/finality 传递的一段 work 必须有 bounded canonical statement，而不是 session-head summary 或固定 report DTO。 |
| `context-compression` | 当历史过长时，只能以受控、证据边界明确的 semantic memory 替代可压缩部分。 |
| `prefix-stability` | 同一 semantic epoch 内已呈现给 provider 的前缀必须保持稳定；冷边界只能由事实驱动。 |

## 8. Failure / recovery

| Package | 一句话 WHY |
|---|---|
| `provider-attempt-recovery` | 单次 provider attempt 已失败后，可在不改变 authority/personhood 的前提下有界换执行绑定继续。 |
| `crash-reconciliation` | 进程/插件中断后只能从 durable facts 与可信物理观察重新进入普通程序，不能从临时内存或猜测恢复。 |
| `degeneration-guard` | 尚未结束的 attempt 若 token 多样性越出正常语料经验边界，应在污染更多历史前主动终止并由本包自行要求改写。 |

## 9. Mission / judgement / finality

| Package | 一句话 WHY |
|---|---|
| `obligation-ledger` | 长期 mission 必须持续维护当前仍欠世界什么，而不是用 phase/status 伪装工作进度。 |
| `review-judgement` | PERFECT/REVISE 的意义必须来自有区分力、按比例、证据驱动的判断，而不是表演式谨慎或固定 checklist。 |
| `review-assurance` | 一个 review judgement 何时有资格被消费，必须由 bounded evidence、fresh witness 与因果确认建立。 |
| `finality` | mission 的不可逆结束资格必须在当前义务、当前 tree 与合格 review 证据上建立，而不能由 participant 自宣告完成。 |

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
| `knowledge-reuse` | 过去的 repository knowledge 可作为 best-effort cache/hint 复用，但不能冒充当前证明。 |
| `repository-programming` | repository 变换需要能力投影、可组合、sandboxed、all-or-nothing 的 programming surface，而不是多套漂移 RPC。 |
| `requirement-grounding` | 代码路径触碰时，适用 requirement package 必须在 effect 前以可重放 read 语义进入当前 participant horizon。 |

## 12. Optional optimization / epistemics

| Package | 一句话 WHY |
|---|---|
| `speculative-investigation` | 可丢弃 speculation 只有在 authoritative world 零影响时才可换取调查成本下降。 |
| `epistemic-reasoning` | 认识状态必须区分 proposal/evidence、保留依赖与不确定性，并由受约束 policy 决定下一信息动作与停止。 |

## 13. Delivery

| Package | 一句话 WHY |
|---|---|
| `distribution` | 可安装 artifact 必须携带运行所需代码与 semantic resources，同时排除不属于交付面的源码/开发资产。 |

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
- 新增 `external-investigation`：Browser 的 provenance-bearing external evidence 不能塞进 local repository investigation。
- 新增 `work-record`：canonical bounded work statement 被 delegation、process review、Finality 共用，不能继续藏在 Companion/Review 下。
- 新增 `requirement-grounding`：路径命中的规范与测试需要自动、去重、可重放地进入开发上下文，且首次 mutation 必须先 grounding。
- 新增 `attention-regulation` / `concern-routing` / `institutional-learning`：最终微原语不是一个“大认知工具包”。`enough/abandon/defer`、`subscribe/publish`、`celebrate/regret → Enhancer` 分属注意力、通信、制度学习三个独立 failure domain；既有 `assume` 保持在 `cognitive-environment`，不重复设计。

# 依赖骨架

这不是权威优先级，只表示定义所需 guarantee。精确 hard edge 以各 boundary card 的 `DEPENDS ON` 为准；本表是当前完整邻接清单（131 edges，0 cycle，按本 code block 逐项机器计数）。

```text
requirement-system       → 无
verification-system      → requirement-system
js-semantic-surface      → requirement-system, verification-system
structured-workflow      → 无
time-capability          → 无
causal-wait              → 无
session-ontology         → 无
managed-session-lifecycle→ session-ontology, crash-reconciliation
host-boundary            → 无
participant-identity     → session-ontology
execution-model-routing  → participant-identity, managed-session-lifecycle, host-boundary
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
dispatch-protocol        → interaction-authority, effect-accounting, host-boundary, durable-events
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
provider-attempt-recovery→ participant-identity, execution-model-routing, interaction-authority
crash-reconciliation     → durable-events, effect-accounting, structured-workflow, host-boundary
degeneration-guard       → interaction-authority, dispatch-protocol, host-boundary
obligation-ledger        → durable-events, effect-accounting, semantic-trace
review-judgement         → cognitive-environment, participant-horizon
review-assurance         → review-judgement, semantic-trace, durable-events, causal-wait
finality                 → obligation-ledger, review-assurance, participant-horizon
behavior-diagnosis       → semantic-trace, durable-events, prefix-stability, managed-session-lifecycle
guidance-delivery        → behavior-diagnosis, participant-horizon, durable-events, concern-routing
institutional-learning   → attention-regulation, behavior-diagnosis, durable-events
repository-investigation → office-capability, participant-horizon
knowledge-reuse          → repository-investigation, durable-events, durable-convergence
repository-programming   → office-capability, capability-enforcement, effect-accounting, durable-events, participant-horizon
requirement-grounding    → requirement-system, host-boundary, participant-horizon, provider-projection, interaction-authority, semantic-trace, prefix-stability, repository-programming
speculative-investigation→ repository-investigation, participant-identity, execution-model-routing, participant-horizon, provider-projection, semantic-trace
epistemic-reasoning      → participant-horizon
migration-ledger         → requirement-system, verification-system, semantic-trace
distribution             → 特殊：所有声明 runtime resource 的 semantic packages（不获其语义 ownership）
```

Phase E 审计结论：3 条 coupling edge 已删（见 `AUDIT.md` Phase E）：

```text
structured-workflow  → causal-wait         删（CE builder 是实现耦合，非定义前提）
time-capability      → causal-wait         删（deadline 是可选 escape，条件依赖非 hard）
guidance-delivery    → provider-projection 删（渲染是下游机制）
finality             → participant-horizon 保留（隐藏机制=信息准入边界，与 delegation 同型）
```

当前 131 edges 均为 semantic prerequisite（A 的 WHAT 定义需要 B 已提供的 guarantee），无 implementation/presentation/proof coupling。旧正文曾写“110 edges”，但旧邻接表实际已含 112；本轮以邻接表本身为准纠正计数漂移，并新增 19 edges。

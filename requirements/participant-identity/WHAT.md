# participant-identity — 可观察合同

本文件是 `participant-identity` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。
证据指针 → `PROOF.md`。边界 → `HOW.md`「边界与弃权」。

## PID-001：Role 是 office 身份；Tier 只改 ExecutionBinding

`Role` 是可数个固定 office 身份（`src/Wanxiangshu/Kernel/Roles.fs`：Manager / Orchestrator / Coder /
Inspector / DevOps / Browser / Inquiry / Reviewer / Distiller / Blogger），`AgentTier` 只有 Fast/Deep。
Tier **只**改变 ExecutionBinding 的机器档位（EffectiveAgent；具体物理 ModelTarget 由 `execution-model-routing` 的 scheduler/lease 决定，AGENT-029），不产生新 Role、不改变 Role Law、不改变工具权限。

含义/动机：若 tier 参与身份，fast/deep 会演化成两套产品；Peer Fallback 换 tier 时角色漂移。

边界：Role→工具权限的**内容**（`permissions` 矩阵、system prompt 文案）归 `office-capability` /
`capability-enforcement`；本包只拥有「Role 是身份轴、Tier 是绑定轴」这个正交事实。Bookkeeper 不进
public Role DU → `session-ontology`（InternalLeaf）。

证据：`catalog.test.mjs` `AGENT_001_catalog_has_exactly_ten_canonical_roles_and_two_tiers`。

## PID-002：Role ≠ Persona ≠ ExecutionBinding 三轴分离

Role = 职责 office（session 内不变）；Persona = 自我模型（session 创建时一次绑定，不可变）；
ExecutionBinding = EffectiveAgent/tier + 其物理执行 lease（可随 Peer Fallback / Strength 切换 EffectiveAgent；具体模型资源路由归 `execution-model-routing`）（AGENT-029）。

含义/动机：三轴合一的历史病理（Bookkeeper 用 inspector binding 却收 inspector prompt）在
历史 agent 条款 有完整事故记录；分离使「换执行者」与「换人」在类型层面就是不同操作。

边界：personhood 的 durable 归类（谁算 participant）由 `session-ontology` 的 attachment/ownership
定义；本包只保证三条轴不会互相冒充。

证据：`catalog.test.mjs`（Role/Tier 轴）+ `session-persona.test.mjs`（Persona 轴）+
`requirements/prefix-stability/tests/system-prompt-stability.test.mjs` `PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes`。

## PID-003：Persona 一次冻结，创建时 resolve-once，之后不可变

`Role × initial tier → SessionPersona` 在 session 创建路径一次绑定（AGENT-028）；同值重绑幂等成功，
不同值重绑失败（`SessionPersona.bindOnce` 返回 `Error`，`/already bound/`）。Fallback / Strength /
Peer / mid-life 一律不得重绑（AGENT-029、PROMPT-014 禁止清单）。

含义/动机：自我模型是 participant 的稳定自称；允许 mid-life 改写 = 换执行者偷偷换人。

边界：Persona display 名表（Integrator/Director/Coordinator/…）是 HOW（COVERAGE：命名除非是 public
contract）；「冻结」机制本身是 WHAT。child 继承见 PID-010。

证据：`session-persona.test.mjs` `AGENT_028_SessionPersona_bind_once_and_inherit`（bindOnce 冲突失败）。

## PID-004：换执行者 ≠ 换人；Fallback/Strength/Peer 只改 ExecutionBinding

Peer Fallback、Strength replica、assistance escalation（fast→deep）都只改变物理执行绑定；
Persona 不变，system prompt 身份字节不变（AGENT-029、FALLBACK-014、PROMPT-014 禁止清单）。

含义/动机：历史 agent 条款「Peer Fallback 换模型时半途换人」是真实失败模式；换 binding 必须是
「同一 participant 换执行者」，不是新 participant。

边界：system prompt **字节**稳定本身由 `prefix-stability` 拥有（byte invariant）；本包拥有「身份不随
binding 变」这一语义。fallback 的预算/算法归 `provider-attempt-recovery`。

证据：`requirements/prefix-stability/tests/system-prompt-stability.test.mjs`
`PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes`。

## PID-005：system prompt identity 是 CanonicalRole 的函数，tier/EffectiveAgent 不参与

`systemPromptIdFor role`（`src/Wanxiangshu/Domain/PromptAuthority.fs`）只依赖 CanonicalRole；
prompt identity 值不含 `fast`/`deep` 标记（`session-persona.test.mjs` `FALLBACK_014_...` 断言
`doesNotMatch /fast|deep/i`）。fast-ROLE 与 deep-ROLE 共享同一 system prompt（AGENT-001）。

含义/动机：若 prompt identity 参与 tier，`permissions(fast)=permissions(deep)`（AGENT-010）就无法
结构性成立；这是身份轴与绑定轴分离的编译期证据。

边界：`systemPromptIdFor` 的 byte 输出稳定性 → `prefix-stability`；本包只拥有「identity 函数不含
tier/binding 输入」这个事实。

证据：`session-persona.test.mjs` `FALLBACK_014_system_prompt_id_follows_canonical_role_not_effective_agent_tier`。

## PID-006：`fast-*`/`deep-*` 是机器路由身份，不冒充 Persona 自称、不穿过 horizon

`fast-coder` / `deep-coder` 是 ExecutionBinding 的机器名（wire 名），不是 provider 可自称的身份；
不得把 Binding 名冒充 Persona（AGENT-029）。prompt identity 不含 binding 名（PID-005）是该律的直接
投影。

含义/动机：历史 agent 条款「`fast-*`/`deep-*` 是机器路由身份，不是模型可见自称」；让模型自称
「我是 fast-coder」等于把内部拓扑暴露成身份。

边界：**什么信息有资格进入 horizon**（admission filter，含 `fast-`/`deep-` token 的泄漏拦截）由
`participant-horizon` 拥有（Gate B）；本包拥有「binding 名不是身份事实」这一语义。

证据：`session-persona.test.mjs` `FALLBACK_014_...`（identity 值不含 fast/deep）。

## PID-007：Peer 配对本体：peer(fast-ROLE)=deep-ROLE，对称且启动可证明

`peer(fast-ROLE) = deep-ROLE`、`peer(deep-ROLE) = fast-ROLE`（AGENT-003）；peer 名必须在 managed catalog 中存在且对称。Bookkeeper pair 同律（`fast-bookkeeper` ↔ `deep-bookkeeper`）。

含义/动机：fallback 消费 peer 的前提是 peer 确实存在；pair 是「同一 office 的另一个执行档」，不是另一身份。物理模型是否相同不参与 peer 本体——A/B 可以最终解析到同一 model。

边界：fallback 何时/如何消费 peer → `provider-attempt-recovery`；EffectiveAgent→MJS scheduler→ModelTarget、lease occupancy 与等待 → `execution-model-routing`；「恰好 22 名」的精确目录是 HOW/GARBAGE（见 `HOW.md` 历史与弃权）；本包只拥有配对本体。

证据：`catalog.test.mjs` `AGENT_003_peer_is_same_role_opposite_tier_and_symmetric`。

## PID-008：managed session 冻结 agent 档位；user-facing 由最近真实用户请求决定 EffectiveAgent；model 不属于用户 binding authority

- 有 parent 的 managed session：创建时冻结 base EffectiveAgent；hook / prompt / continuation 必须保持该 agent，除 typed override 外请求字段与 Host default 都不能重绑。
- 无 parent 的 user-facing session：base EffectiveAgent 由最近一次真实外部用户请求决定；插件自产 prompt / hook / continuation 不是用户重绑证据；普通 `Preserve` 沿用最近观测到的 agent。
- 物理 model 不由 Host/user message 决定。`(SessionId, EffectiveAgent)` 的 ModelTarget 由 `execution-model-routing` 获取并在 live session 内稳定；Host 实际 provider model 与 lease 不一致 → fail closed。
- 显式换档（Fallback / Assistance）只允许经 typed `ExplicitExecutionOverride` 做**单次** EffectiveAgent override；override 不改变 frozen base，下一次普通发送恢复 base。override 对应的 ModelTarget 由目标 EffectiveAgent 的 lane lease 解析（PROMPT-006）。

含义/动机：execution binding 是身份轴的一部分（PID-002）；外部用户可以选择 managed agent/tier，但不能用 Host model 字段绕过 Wanxiangshu 的资源路由。内部路径也不能用 model 漂移冒充换档。

边界：发送海关机制（Preserve/override 的 wire 语义、fail-closed 的物理实现）与 `dispatch-protocol` / `provider-attempt-recovery` 共用；模型 lease 与容量由 `execution-model-routing` 拥有；本包只拥有 EffectiveAgent 选择与身份不变。

证据：现有 `session-execution-binding.test.mjs` 的 agent/override 断言继续承载；model authority 迁移由 `execution-model-routing` GAP-016 新 oracle 接管。

## PID-009：内部身份有机器身份 + Persona + peer，但不进 public Role DU

Bookkeeper（InternalLeaf）有 `fast-bookkeeper`/`deep-bookkeeper` 机器身份、Clerk/Curator Persona
（AGENT-028 表）、对称 peer（PID-007），但**不**进入 public `Role` DU、不进 Manager fork 面
（AGENT-002/008 的 identity 侧）。

含义/动机：Bookkeeper 是运行时合成路径，模型不得选择它；身份轴必须有它的位置，但不在公开 office
选择面（后者归 `participant-horizon` 的 admission）。

边界：InternalLeaf + Attached 的 execution class 分类 → `session-ontology`；「不进 provider 可见
enum」的可见性过滤 → `participant-horizon`。

证据：`catalog.test.mjs`（`AGENT_001` public/internal 划分、bookkeeper 名/peer）+ `session-persona.test.mjs`
（`PersonaCatalog_bookkeeperPersona` = Clerk/Curator）。

## PID-010：personhood 连续性：child/attached/InternalLeaf persona 继承 owner persona

子 session（child / attached / InternalLeaf）的 Persona 继承 owner Persona，不重新按自身 tier 解析
（`PersonaCatalog.inheritFrom` / `SessionPersona.inheritFromOwner`）。

含义/动机：派生执行上下文属于同一 participant life；若 child 重新解析 Persona，同一人会在子上下文
里换自称（AGENT-028 inherit 语义、STRENGTH-004 交叉）。

边界：child 的 session 生命周期归 `managed-session-lifecycle`；本包只拥有「派生上下文沿用同一
personhood」这一连续性判据。

证据：`session-persona.test.mjs` `AGENT_028_SessionPersona_bind_once_and_inherit`
（`inheritFromOwner` 后 `tryGet(replica) = 'Engineer'`）。

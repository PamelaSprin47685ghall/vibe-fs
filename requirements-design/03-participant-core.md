# Participant core

## `participant-identity`

**WHY**  
执行机制可以换，但“是谁在行动”不能因此漂移。否则 fallback、replica、attached execution 会把机器拓扑升级成人格变化。

**OWNS**
- `Role ≠ Persona ≠ ExecutionBinding`。
- Role = office identity；Persona = participant self-model；ExecutionBinding = 当前物理执行者/model/config。
- 同一 participant life 内 Persona/office identity 稳定。
- execution binding 变化不自动产生新 participant。
- identity inheritance/continuity 的领域判据。

**DOES NOT OWN**
- office 能产生什么后果、tool permission matrix、Role Law/Library 内容。
- session kind/lifecycle、fallback/Strength 算法。
- 当前 `fast-*` / `deep-*` 命名、22-agent catalog、Persona 名字表。
- provider language。

**DEPENDS ON**
- `session-ontology`

**PROVIDES**
- “same participant / different participant”的稳定 guarantee。

**FAILURE MEANING**  
RED = 换 model/execution context 能偷偷改变 responsibility/self-model，或新 participant 与同一人的另一 execution context 无法区分。

**INDEPENDENT CHANGE**  
Persona 从 `Role × initial tier` 改为显式创建时选择，同时 office capability 与 provider renderer 完全不动。

**CURRENT EVIDENCE**  
`docs/why/agent.md`；AGENT-028/029；type `Domain/PersonaCatalog.fs`、`Kernel/Roles.fs`、`Session/AgentRoleIdentity.fs`、`Domain/ManagedAgentCatalog.fs`；`session-persona.test.mjs`；prompt-stability proofs。

---

## `office-capability`

**WHY**  
名字、persona、工具可达性都不能决定 authority。office 必须由有资格产生的后果定义，否则“看起来能做”会冒充“有权做”。

**OWNS**
- 每类 office 的 canonical entitled consequence 与明确 non-consequence。
- 同一 office 跨 Persona/ExecutionBinding 时 authority 不变。
- capability 是 consequence model，不是 tool whitelist 的口语转写。

**DOES NOT OWN**
- identity ontology、当前 permission/tool list、delegation protocol。
- `fork`/Role Law 的呈现文案、horizon/rendering。
- 当前五 Office 必须永久保持五分法；它们是证据，可重构。

**DEPENDS ON**
- `participant-identity`

**PROVIDES**
- delegation/action surface 可引用的唯一 consequence-level authority model。

**FAILURE MEANING**  
RED = office authority 不清、互相重叠，或能产生自己无资格产生的后果。

**INDEPENDENT CHANGE**  
重画 Inspector 与 DevOps 的 existing-evidence/new-behavior 边界，不改 Persona、projection、dispatch。

**CURRENT EVIDENCE**  
ARCH-017；type `Kernel/Roles.fs`；resource `resources/provider/role/*/`；`OFFICE_CAPABILITY_ANCHORS`；Manager/fork capability projections。

---

## `participant-horizon`

**WHY**  
machine state 远多于 participant 应体验的世界。若全部暴露，participant 被迫解码 Host DTO/拓扑而不是依据后果行动。

**OWNS**
- participant-visible information admission filter。
- already-known/echo/correlation/debug-only 信息省略。
- internal state 优先转成 action-relevant consequence。
- 需要 raw measurement 时只给必要 observation，不给 Host judgement。
- 虚假 affordance、不可达路径、无行动价值内部 identity 不得穿过 horizon。
- internal diagnostics 留机器侧，经验层接收真实 consequence。

**DOES NOT OWN**
- office authority、participant identity、guidance/Role Law 内容。
- language/localization、TOML/JSON/wire layout、ProjectionIntent order。
- 当前 `SessionId/status/code/error/...` blacklist 作为永久 taxonomy；它们是 proof fixtures。

**DEPENDS ON**  
可独立定义；实际 projection 可读取 identity/capability facts。

**PROVIDES**
- `provider-projection`、`guidance-delivery` 可消费的“什么有资格被看见”保证。

**FAILURE MEANING**  
RED = participant 看见无行动价值的机器状态/虚假 affordance，或真正影响下一合法行动的事实被裁掉。

**INDEPENDENT CHANGE**  
允许一种新的 runtime measurement 进入 horizon，而 renderer 与 office capability 不动。

**CURRENT EVIDENCE**  
ARCH-014；type `Domain/{ProjectionIntent,ProviderProjection,ToolResultBound}.fs`；`scripts/checks/provider-leak-gate.mjs`；`provider-leak-gate.test.mjs`、`horizon-surface.test.mjs`。

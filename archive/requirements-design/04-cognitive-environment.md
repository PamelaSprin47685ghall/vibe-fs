# `cognitive-environment`

**WHY**  
participant 必须稳定区分世界常识、自我职责、继承知识、当前发生的事与当前任务；否则瞬时 runtime/mission 会污染身份，知识会偷渡 authority。

**OWNS**
- 长期 cognition 的语义层：World / Role / inherited knowledge。当前 Common Law / Role Law / Office Library 是证据，不要求保留名称。
- knowledge ≠ authority；craft 可跨 authority boundary 流动，authority 不随知识流动。
- enduring cognition 与 Runtime / Mission 的边界。
- Role self-model 不枚举全部瞬时 capability state。
- 同 role 不因 execution strength 获得两套冲突的思想传统。
- 其它 package 已拥有 canonical fact 时，本包只要求引用/呈现，不复制第二 normative source。

**DOES NOT OWN**
- office consequence、Persona identity、action contract。
- mission/lifecycle/todo/review/finality 事实。
- provider language、wire rendering、prefix byte stability。
- 所有 provider prose 的业务意义；meaning 仍属各 semantic owner。

**DEPENDS ON**
- `participant-identity`
- `office-capability`（只引用 authority facts）

**PROVIDES**
- participant-facing 长期 cognition 的组织边界。

**FAILURE MEANING**  
RED = 长期 self/world model 被瞬时阶段、能力、任务或外来知识重写，或继承知识创造原本不存在的 authority。

**INDEPENDENT CHANGE**  
全面重写 Office Library inheritance / Common Law 结构，而 authority、Persona、action contracts 不动。

**CURRENT EVIDENCE**  
PROMPT-015/016；wiring `Infrastructure/Resources/PromptResources.fs`；resource `resources/provider/{world,role,library}/**`；Prompt Restoration；Role semantic-depth proof。

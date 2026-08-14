# `action-affordance`

**WHY**  
正确长期 world model 仍不足以保证调用瞬间选对语义动作；每个非平凡动作都必须把最容易混淆的邻近边界带到实际 decision surface。

**OWNS**
- 动作合同回答：做什么、何时适用、最容易混淆的相邻行为是什么、成功后建立什么、非显然参数意味着什么。
- 动作名称表达 semantic act，不表达 runtime topology。
- capability choice 不能退化成没有语义的裸 enum。
- canonical fact 可以在多个 decision boundaries 被镜像；多处呈现不产生多处 semantic ownership。
- caller-facing boundary mirror 的完整性。

**DOES NOT OWN**
- 被镜像的 office/review/delegation/product fact。
- runtime capability enforcement。
- 长期 Role Law/Library。
- provider layout/localization。
- 当前动作名清单与高风险 allowlist。

**DEPENDS ON**
- `office-capability`
- `participant-horizon`

**PROVIDES**
- participant-visible decision surface 的局部认知合同。

**FAILURE MEANING**  
RED = participant 必须靠名字或猜测才能知道一个动作真正会做什么、不会做什么、成功意味着什么。

**INDEPENDENT CHANGE**  
重写所有动作说明与参数语义，而长期 cognition 与 office capability model 不动。

**CURRENT EVIDENCE**  
PROMPT-020/021；`TOOL_DESCRIPTION_ANCHORS`；`prompt-semantic-depth.test.mjs`；Inspector/Coder/DevOps caller-boundary incidents。

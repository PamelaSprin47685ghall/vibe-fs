# participant-identity

**一句话 WHY**：执行机制可以换，但「是谁在行动」不能因此漂移——Role、Persona、ExecutionBinding
三者分离，换执行者 ≠ 换人。

## WHAT 概览

本包保证：角色（office 身份）、Persona（自我模型）、ExecutionBinding（物理执行者）是三条独立轴；Persona 一次冻结不可变；换 tier/peer 只改 EffectiveAgent，具体物理 ModelTarget 由 `execution-model-routing` 的 session lease 解析；`fast-*`/`deep-*` 是机器路由身份，不是 provider 可见自称；managed session 的 base EffectiveAgent 创建即冻结，用户面 EffectiveAgent 由最近真实用户请求决定。全部命题见 `WHAT.md`（`PID-001..010`）。

## HOW 概览

- 类型：`src/Wanxiangshu/Kernel/Roles.fs`（`Role` DU、`AgentTier`）、`Session/PersonaCatalog.fs`
  （`PersonaCatalog.persona`、`SessionPersona.bindOnce`）、`Domain/ManagedAgentCatalog.fs`
  （名称/peer/角色分组公式）、`Domain/PromptAuthority.fs`（`parseAgentNameTyped`、`systemPromptIdFor`、
  `buildAttemptExecutionProfile`）。
- 发送海关：`Infrastructure/OpenCode/Host/Sessions.fs` + `ChatParamsHook.fs`（`BindingIntent`）。
- 详见 `HOW.md`；非 normative。

## proof 概览

- `tests/catalog.test.mjs`（自 `tests/unit/agent/` 移入）：Role DU、22 名目录、peer 对称、旧名拒绝。
- `tests/session-persona.test.mjs`（自 `tests/unit/prompt/` 移入）：Persona 矩阵、bind-once、继承、
  prompt identity 不随 tier。
- REUSE：`requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（Persona/字节稳定性）、
  `requirements/participant-identity/tests/session-execution-binding.test.mjs`（PROMPT-006 binding 解析律）。
- 落点表见 `PROOF.md`。

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样。
2. `WHAT.md` —— 唯一 normative 合同。
3. `HOW.md` —— 实现模型 + 历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点与跑法。

## 边界（不归我）

- office 有资格产生什么后果 → `office-capability`。
- provider 看见的与可执行的 capability 同源不扩权 → `capability-enforcement`。
- session 的 execution class / ownership / attachment → `session-ontology`（本包 DEPENDS ON 它）。
- attempt 失败后有界换 binding（fallback 算法）→ `provider-attempt-recovery`。
- EffectiveAgent→MJS scheduler→ModelTarget、lease occupancy 与等待 → `execution-model-routing`。
- 已呈现前缀字节稳定性 → `prefix-stability`；provider 语言绑定 → `provider-language`。

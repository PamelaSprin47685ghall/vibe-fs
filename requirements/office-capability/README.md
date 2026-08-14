# office-capability

**一句话 WHY**：office 必须由「有资格产生的后果」定义，而不是 persona 名、工具名或权限清单的口语
转写——否则「看起来能做」会冒充「有权做」。

## WHAT 概览

本包保证：每类 office 有 canonical entitled consequence 与明确 non-consequence（ARCH-017 五分法）；
同一 office 跨 Persona/ExecutionBinding authority 不变；capability 是 consequence model，不是 tool
whitelist；单一语义所有权、多处投影（Manager Role Law / fork description / 各 office Role Law /
caller-facing tool）同 ID 命中、consequence 不漂移；offices 不可互换。全部命题见 `WHAT.md`
（`OFF-001..014`）。

## HOW 概览

- 权威模型：`archive/docs/what/architecture.md` ARCH-017（Office Capability Model）。
- 语义锚点目录：`scripts/checks/semantic-anchors.mjs` `OFFICE_CAPABILITY_ANCHORS`（5 id，本包拥有）
  与 `OFFICE_CAPABILITY_NEGATIVES`；Gate F 机制在 `scripts/checks/language-parity-gate.mjs`
  `scanOfficeCapabilityIntegrity`。
- 五类 office 的域事实：`src/Wanxiangshu/Domain/ManagedAgentCatalog.fs` `managerForkableRoles`。
- 投影资源：`resources/provider/role/manager/`、`resources/provider/tool/fork/description/`、
  `resources/provider/role/{coder,inspector,devops,browser,inquiry}/`、`tool/inspect/description/`。
- 详见 `HOW.md`；非 normative。

## proof 概览

- `tests/office-capability-integrity.test.mjs`（NEW，本包自有）：live-repo canary——五分法、
  五个 consequence 在 manager law + fork description 双语文档命中、各 office Role Law 携带
  consequence、offices 不可互换、calling 名只差 persona/depth。
- REUSE：`requirements/office-capability/tests/office-capability-gate.test.mjs` `gate_f_*`（Gate F fixture 测试）、
  `tests/eval/provider-office-boundary/`（四个 office-boundary oracle）、
  `requirements/capability-enforcement/tests/integration/plugin/manager-tool-contract.test.mjs`（fork description = office capability map）。
- 落点表见 `PROOF.md`。

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样。
2. `WHAT.md` —— 唯一 normative 合同。
3. `HOW.md` —— 实现模型 + 历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点与跑法。

## 边界（不归我）

- 谁在行动（Role/Persona/Binding 三轴）→ `participant-identity`（DEPENDS ON）。
- 可见/可执行 capability 同源不扩权的 enforcement → `capability-enforcement`。
- 委托动作本身（entrust by consequence 的调用语义）→ `delegation`。
- 外部事实如何建立（provenance 合同细节）→ `external-investigation`；认识状态求解 →
  `epistemic-reasoning`。
- 什么信息有资格被看见（admission）→ `participant-horizon`。

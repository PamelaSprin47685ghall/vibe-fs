# office-capability — 实现模型与约束（非 normative）

## 实现模型

| 面 | 实现 | 说明 |
|----|------|------|
| canonical 模型（normative 定义处） | 本包 WHAT OFF-002/005（历史 ARCH-017 立法） | 五分法 + 每 office entitled consequence / non-consequence 表；权威在语义层，不在代码单点 |
| 语义锚点目录 | `scripts/checks/semantic-anchors.mjs` `OFFICE_CAPABILITY_ANCHORS`（5 id）+ `OFFICE_CAPABILITY_NEGATIVES` | Gate F 的 oracle；id 归本包（见 PROOF.md 清单） |
| Gate F 机制 | `scripts/checks/language-parity-gate.mjs` `scanOfficeCapabilityIntegrity` | 读 `role/manager/{en,zh-CN}.md` + `tool/fork/description/{en,zh-CN}.md`，五个 consequence 同 ID 双语命中；negatives 检查 |
| 五分法域事实 | `src/Wanxiangshu/Domain/ManagedAgentCatalog.fs` `managerForkableRoles` | `[Coder; Inspector; DevOps; Browser; Inquiry]`——与 ARCH-017 表一致 |
| 投影 1：Manager Role Law | `resources/provider/role/manager/{en,zh-CN}.md`「Entrust by consequence / 按后果托付」 | 世界观：按后果选择 office；negatives（不可互换）同文档 |
| 投影 2：fork description | `resources/provider/tool/fork/description/{en,zh-CN}.md` | 调用瞬间的可行动选择；calling 名只差 persona/depth |
| 投影 3：各 office Role Law | `resources/provider/role/{coder,inspector,devops,browser,inquiry}/{en,zh-CN}.md` | 自我模型：consequence + non-consequence |
| 投影 4：caller-facing tool | `resources/provider/tool/inspect/description/{en,zh-CN}.md` | 调用方必须看见的边界镜（Inspector 是见证者） |
| 行为 oracle | `tests/eval/provider-office-boundary/` | 4 个合成 trace oracle（office-boundary-eval），不接生产 filter |

## 边界与弃权

### 不归本包（引用其它包）

- 身份轴（Role/Persona/Binding）→ `participant-identity`（DEPENDS ON）。
- 矩阵/gate 同构与权限投影 → `capability-enforcement`。
- 委托动作语义（entrust by consequence 的调用律）→ `delegation`（锚点 `entrust-by-consequence` /
  `choose-by-return` / `no-omnipotent-charge` 已由 delegation 声明 owner，本包不重复声明）。
- 外部事实的 provenance 合同细节 → `external-investigation`；认识状态求解 → `epistemic-reasoning`。
- 什么信息有资格被看见 → `participant-horizon`；act 五问 → `action-affordance`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| 「当前五 Office 必须永久保持五分法」 | HOW（可重构证据） | boundary card DOES NOT OWN：五分法是当前证据，可随重画边界变化 |
| Persona 双名（Navigator/Researcher 等 calling 名） | HOW | 命名除非成为 public contract；「两个名只差 persona/depth」才是 WHAT（OFF-003） |
| 各 office Role Law 的散文 craft 内容（非 consequence 部分） | HOW → `cognitive-environment` | 自我模型/世界观的长期认知内容归认知层；本包只拥有其中 consequence/non-consequence 事实 |
| Gate F 的 fixture 参数（`{0,120}`/`{0,80}` 跨度） | HOW | 正则实现细节；「同 ID 双语命中」才是 WHAT |

## 历史（考古摘要）

- 历史 ARCH-017：office capability model 立法，含「单一语义所有权，多处投影」表。
- 历史 why/architecture「Office 认知」备选与被拒：拒「fork 枚举 calling 名即足够」；真实事故
  （Coder 按 inspect tooltip 把修复交给 Inspector）催生边界镜投影。
- 历史 AGENT-009/011…016/023：各 office 的 consequence / non-consequence 明细条款。
- 历史 PROMPT-021：critical semantic redundancy——关键区别出现在每个会改变行动的决策面。
- 语义锚点 Gate F 现状：`OFFICE_CAPABILITY_ANCHORS` 5 id 双语文档命中由 `language-parity-gate.test.mjs`
  `gate_f_*` fixture 测试与 `scripts/check.mjs` 的 `language-parity-gate.mjs` 双重守护。

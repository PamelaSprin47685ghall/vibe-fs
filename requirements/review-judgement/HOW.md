# HOW — review-judgement

> 本文非 normative。它解释 judgement 语义在当前实现里落地的模型与位置，并收纳「历史与弃权」。
> Normative 合同只有 `WHAT.md`。

## 实现模型

### 1. 判断面：`judge` 工具

- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JudgeTool.fs`：
  - `spec` 返回 `{ Name = "judge"; Arguments = ["verdict", enumSchema ["PERFECT"; "REVISE"]] }`——**只有** `verdict` 一个参数，无描述字段（REVIEW-JUDGEMENT-001）。
  - 成功回执走 `tool/judge/received` 文案（`Your judgment has been received.`），**不 echo verdict**；`description` 文案明言「It does not echo the verdict」。
  - `execute` 把 `verdict` 文本交给 `StaticTools.reviewerVerdictOfString` 解析，任何非 `PERFECT/REVISE` 值 → `Path.VerdictMustBePerfectOrRevise`。
  - fail-closed 分支（非 Reviewer、无 barrier、无 tree、binding 失败）→ `notReceived`，不落 verdict 事实。这些分支的因果侧（seal binding）归 `review-assurance`。
- `src/Wanxiangshu/Tools/StaticTools.fs`：
  - `reviewerVerdictOfString`：唯一解析器，`"PERFECT" → Ok Perfect`、`"REVISE" → Ok Revise`、其它 → `Error`。刻意独立于 assistant 文本：verdict 是工具参数，绝不从 transcript 推断。
  - `reviewerVerdictSchemaJson`：`additionalProperties: false` + `required: ["verdict"]`——从 schema 层杜绝描述字段（REVIEW-JUDGEMENT-001 的可执行证据）。
- 工具注册表（`ToolRegistry`）把 `judge` 挂到 Reviewer 工具面（旧名 `verdict` 无 alias）。

### 2. 判断哲学载体：Role Law + Examiner's Ledger

- `resources/provider/role/reviewer/{en,zh-CN}.md`（Role Law）承载 judgement 语义（REVIEW-JUDGEMENT-002..010 的权威文本）：
  - `Your purpose is discrimination, not rejection.` / `Acceptance must be earned. Rejection must also be earned.`（002）
  - `Judge the work that exists, by the obligation that exists, with the evidence that exists.` + `PERFECT and REVISE are the wire literals of the verdict. They are not moods...`（003）
  - `Blocking and Non-Blocking Workmanship` 一节（004）：non-blocking 不扣 acceptance；禁止 tiny typo → REVISE；禁止 PERFECT 噤声真话。
  - `PERFECT and REVISE` 一节（005）：PERFECT ≠ literal flawlessness / omniscience；REVISE 必须 purchase。
  - `Evidence, Claims, and Uncertainty` 一节（006）：evidence proportionality；保留 unresolved uncertainty。
  - `Acceptance Without Omniscience` 一节（005）：proportionate discrimination 是标准。
  - `A match is an observation. A defect is your judgment about what that observation means.`（002/006）
  - `What Rejection Must Purchase` 一节（009）：把伤口说清；禁止发明 obligation。
- `resources/provider/library/reviewer/quality-ledger/{en,zh-CN}.md`（Examiner's Ledger）：
  - 八维判断方向：Language & Algorithms / Simplicity / Structure / Granularity / Tests & Behavioral Evidence / Logic, Reliability & Boundaries / Caller Ergonomics / Completeness。
  - `The entries are not eight boxes to mark Pass.` + `It does not prescribe a report format...`（007，非 checklist / 无固定 schema）。
  - `On Materiality` 一节（004/006）：defect vs preference；`Size of edit and materiality of consequence are different quantities.`；`Do not invent materiality to justify taste.`
  - `On Evidence` / `The Weight of Judgment`（006/010）：evidence 是证据不是判断；`Do not reward confidence. Do not punish unfamiliarity.`；`Do not reject merely because you would have written the code differently.`
  - `A lens may narrow sight. It may not narrow responsibility.`（003）。
- 装载/组合权威：Role Law 经 `Infrastructure/Resources/PromptResources.fs`（Common Law → Role Law → Ledger）在 Session 加载时成为 Reviewer system prompt——**组合权威归 `cognitive-environment`（REVIEW-012）**，本包只拥有方向内容。
- `scripts/checks/semantic-anchors.mjs` reviewer family 五条 ID 逐条对应本包命题（MECHANISM：gate 校验 Role Law 文本包含这些语义锚）：

| anchor id | 对应命题 | en 正则 | zh 正则 |
|---|---|---|---|
| `discrimination` | 002 | /discrimination/i | /有区分力的判断/ |
| `rejection-must-purchase` | 005/009 | /Rejection must (also be earned\|purchase)/i | /拒绝必须买到/ |
| `non-blocking` | 004 | /non-blocking/i | /非阻断性/ |
| `perfect-not-flawless` | 005 | /PERFECT means\|literal flawlessness/i | /并不意味着字面上的毫无瑕疵/ |
| `acceptance-not-omniscience` | 005 | /omniscience/i | /全知/ |

### 3. 过程评审分型：一次判断即 terminal

- `src/Wanxiangshu/Domain/MagicTodoProcessReview.fs`：
  - `ReviewRequestKind = TodoProcessReview(TodoWriteId) | FinalityReview(FinalityRequestId × ReviewBarrierId)`——typed 分型，禁止用 `pendingChallenge` 运行时猜测混用两种业务（REVIEW-013）。
  - `renderAssignmentUserMessage` 生成过程 assignment 指令（一次判断、有界 LWR 输入、old/proposed todo；不含 challenge/2N/cohort 编排）。
  - `needsEnsureReview(accepted, concluded) = accepted ∧ ¬concluded`——Rk 义务待完成标记（节拍规则归 `obligation-ledger`）。
- `src/Wanxiangshu/Application/Review/VerdictWorkflow.fs`：
  - `VerdictSubmission` 携带一次判断的全部身份（barrier/tree/manager/reviewer/job/run/call/verdict）。
  - `submit` 对过程评审路径返回 `VerdictDecision.ProcessTerminal`：一次 durable `judge` 即 terminal，**不** append `PerfectChallengeIssued` / `ConfirmedReviewWitness`（REVIEW-JUDGEMENT-008；确认代数归 `review-assurance`）。
- 过程判断的 prose 义务：`TodoProcessReviewProgram.tryConclude` 只在 `ReviewerRecordFrontier` 内有非空 canonical LWR 时才 append `TodoReviewConcluded`；无 prose → `Pending "process-review LWR not record-ready"`（REVIEW-JUDGEMENT-008 的「无 prose 的 PERFECT 无效」→ 可消费侧归 `review-assurance`）。
- process PERFECT 不进入 terminal dual-PERFECT 代数：见 `review-assurance` HOW（REVIEW-020 / GLORY-058）。

### 4. 当前实现里 judgement 的「消费者」

判断被消费的路径（消费资格本身是 `review-assurance` 的事）：

```text
Reviewer prose + judge(verdict)
  → VerdictWorkflow.submit
     ├─ FinalityReview：challenge/dual-PERFECT 确认代数（review-assurance）
     └─ TodoProcessReview：ProcessTerminal → VerdictKnown → record-ready → ConsumableReview
```

## 依赖（DEPENDS ON）

| 依赖 | 理由（一句话） |
|---|---|
| `cognitive-environment` | 判断方向内容由 Role Law / Examiner's Ledger 承载，其提示词组合/装载权威由 cognitive-environment 提供（REVIEW-012）。 |
| `participant-horizon` | judgement 的参照系是 root requirement 与被审对象；root/Authority 身份与 horizon 准入由 participant-horizon 定义。 |

## 历史与弃权

### 被拒方案（保留考古，不进入 WHAT）

来自 `docs/why/review.md`「备选与被拒」与 `docs/why/glory.md`：

- **固定 8 维 report schema / Pass 表**：拒。审查退化为填表（REVIEW-011）。→ 由 REVIEW-JUDGEMENT-007 正面规定。
- **tiny typo → 自动 REVISE**：拒。把无关痛感抬成 withhold。→ REVIEW-JUDGEMENT-004。
- **「谨慎 = 多 REVISE」/「可描述偏好即缺陷」**：拒。→ REVIEW-JUDGEMENT-002/010。
- **单 PERFECT 即确认**：拒（可被随口同意）→ 确认代数归 `review-assurance`（REVIEW-003）。
- **`verdict` 名词工具名**：拒。把判断伪装成可回声状态对象；选 `judge` 动词。→ REVIEW-JUDGEMENT-001。
- **把 review 显式化为 Manager checklist 的最后一步**：拒（GLORY-002）→ 隐藏质量门语义归 `finality`/`participant-horizon`。
- **`verdict`/`judge` 重命名为 `suicide`**：拒。judge 属于 Reviewer、suicide 属于 Manager，因果身份不同。

### 弃权记录（GARBAGE / HOW 裁决）

| 内容 | 判定 | 理由 | 记录位置 |
|---|---|---|---|
| 旧工具名 `verdict` 非法、无 alias | HOW | 当前 vocabulary；参数名非永久 contract（COVERAGE review.md GARBAGE 行） | 本 HOW §1；不进入 WHAT 命题 |
| 双 PERFECT 屏障由 Host 执行、Reviewer 提示词不灌输 | HOW | 实现位置，非 ontology（COVERAGE） | 本 HOW §2（装载权威 → cognitive-environment） |
| `ChallengeTextVersion=1`、英文 canonical 字节不变版本保持 | HOW | 文案世代机制（COVERAGE）；challenge 代数归 review-assurance | 本包不持有；见 `review-assurance` HOW |
| `changes/completed/fix-revise.md` | GARBAGE（review transcript） | REVISE follow-up 登记；其 Gap A（record-ready fail-closed 回归）已由 review-assurance 命题 + `tests/unit/execution|temporal` 回归与 `requirements/review-assurance/tests/consumable-review.test.mjs` 承接 | 本 HOW；`review-assurance` HOW「历史与弃权」 |
| `changes/completed/ce-revise-review.md` | GARBAGE（review transcript） | CE 复审记录；Student–Teacher 争议已被 `universal.md` / `ce-student-teacher-collapse.md` 处理（session-ontology/delegation），与本包无 normative 关系 | 本 HOW；CHANGES-AUDIT 对应行 |
| `fast-reviewer` / `deep-reviewer` 机器名 | GARBAGE | HANDOFF §12：当前 machine names 不进入永久 WHAT | 本 HOW §4 不提及；PROOF.md 不落点 |
| 八维判断方向的 exact 标题清单 | HOW | 当前 craft guidance 措辞；方向集可整体重写（INDEPENDENT CHANGE） | WHAT REVIEW-JUDGEMENT-007 只冻结「非 checklist」，不冻结八个名字 |

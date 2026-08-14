# review-judgement

> PERFECT/REVISE 的意义，必须来自有区分力、按比例、证据驱动的判断——不是表演式拒绝，不是固定 checklist。

## 一句话 WHY

一个系统如果让 Reviewer 可以凭「多拒绝 = 更谨慎」的表演、固定八维打分的 checklist、或无可辩护的偏好去接受/拒绝工作，质量门就会退化成仪式。`review-judgement` 定义 `judge` 这个动作到底**意味着什么**：acceptance 与 rejection 都必须被挣得，material defect 才能扣住 acceptance，PERFECT 不承诺全知。

## WHAT 概览（详见 WHAT.md）

- `judge` 工具是 typed judgment surface：`verdict ∈ {PERFECT, REVISE}`，无描述字段，成功回执不 echo。
- 判断相对 root requirement 与当前被审对象，不是 reviewer mood。
- material defect 才能 withhold；non-blocking workmanship 与 acceptance 共存。
- PERFECT ≠ 字面无瑕/全知；REVISE 必须购买实质更好/更真的结果。
- match 是 observation，defect 是 judgement；evidence 强度与 claim 相称。
- Examiner's Ledger / Rulebook 是判断方向，不是 checklist / 固定 report schema。
- 过程评审（TodoProcessReview）的 verdict 也是一次真实判断：一次 durable judge 即 terminal，无 challenge、无 dual-PERFECT。

## HOW 概览（详见 HOW.md）

- 判断面：`src/Wanxiangshu/Infrastructure/OpenCode/Tools/JudgeTool.fs` + `src/Wanxiangshu/Tools/StaticTools.fs`。
- 判断哲学载体：`resources/provider/role/reviewer/{en,zh-CN}.md`（Role Law）+ `resources/provider/library/reviewer/quality-ledger/{en,zh-CN}.md`（Examiner's Ledger）。
- 过程判断分型：`src/Wanxiangshu/Domain/MagicTodoProcessReview.fs`、`src/Wanxiangshu/Application/Review/VerdictWorkflow.fs` 的 `ProcessTerminal` 路径。

## proof 概览（详见 PROOF.md）

| 文件 | 覆盖 |
|---|---|
| `tests/judge-tool-contract.test.mjs` | REVIEW-JUDGEMENT-001（工具形态、schema 无描述字段、不 echo） |
| `tests/discrimination-fixtures.test.mjs` | REVIEW-JUDGEMENT-002..007/009/010（discrimination / materiality / checklist 禁令） |
| `tests/process-review-judgement.test.mjs` | REVIEW-JUDGEMENT-008（过程一次判断 terminal） |
| `tests/unit/tools/verdict-tool.test.mjs`（REUSE） | 工具执行面 fail-closed（REVIEW-001 交叉） |

## 阅读顺序

1. `WHY.md` —— 为什么这个包必须独立存在、RED 长什么样。
2. `WHAT.md` —— 唯一 normative 合同，10 条命题。
3. `HOW.md` —— 实现落在哪些文件；历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点与运行命令。
5. `tests/` —— 可执行 proof。

## 边界（DOES NOT OWN）

- 一次 judgement 是否被因果确认/可消费 → `review-assurance`（witness/seal/challenge/record-ready）。
- dual-PERFECT 的计数代数、tree invalidation、attempt identity → `review-assurance`。
- 1:1 lag-1 过程评审节拍、Rk 义务派生 → `obligation-ledger`。
- 终末 cohort / rejection / blessing / rest → `finality`。
- Reviewer 提示词的组合权威（Common Law → Role Law → Ledger）→ `cognitive-environment`。
- Reviewer hidden session 生命周期 → `managed-session-lifecycle`。
- Manager 可见面（outcome/report 窄例外）→ `participant-horizon`。

## 依赖

`DEPENDS ON: cognitive-environment, participant-horizon`（理由见 HOW.md）。

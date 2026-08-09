# Fix REVISE follow-up（Work origin）

> 本文件是变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。

## Work origin

本文件由审查结论派生，指向 `changes/completed/fix.md` 末尾的 REVISE follow-up。后续 DevOps
CLOSE_READY + Final outcome（归档提交 `2a67f451`）已取代 fix.md 与 corrective.md 的中间 OPEN
快照；本次 REVISE 仅登记审查后仍存的技术缺口。用户明确授权继续本 REVISE（GOV Work origin），
冻结范围仅限该 follow-up 列出的两项缺口，不扩大。

## Scope (frozen)

仅以下两项（完整描述见 `changes/completed/fix.md` 的 REVISE follow-up）：

1. **Finality record-ready 拒绝/崩溃恢复回归**：补 Blogger abandonment 与 waiter-crash 场景专项
   回归，满足 `docs/proof/glory.md` §29 的 Closing work 3 覆盖要求。
2. **B 类零轮询闭合**：对齐 `docs/proof/dsl-structured-program.md` 的 B 类零轮询措辞，撤销
   「timerTask→re-probe 形状在 dsl-ownership 静态判 RED」的 overclaim。闭合改为三合一证明：
   生产 `AwaitJournalChangeFrom`/`AgentJournal.awaitChangeFrom`、行为 callOrder（
   `tests/unit/execution/executor-summarize.test.mjs`）、`ExecutorSummarize` 形裸 `mutable` 对抗
   fixture（mutable gate RED）。不新增 timer gate 到 dsl-ownership.mjs。

## Remaining work

1. Finality record-ready 拒绝/崩溃恢复回归 — **DONE**（Gap A）：`tests/unit/execution/
   finality-cohort-law.test.mjs` 补 `GLORY_074`（Blogger abandonment 期间 record-ready →
   `Undecided`，无 partial `FinalityRejected`/缺 `# Work log` 的 WorkRecordRef）与 `GLORY_075`
   （waiter crash → `resumeDurableRevise` 从 durable evidence 续等，appendBlogCoverage 后唯一
   `FinalityRejected` 引用非空 `# Work log`，无 timer/sleep re-probe）。DevOps 实测该套件
   8/8 通过（含 GLORY_074/075）。
2. B 类零轮询 docs 对齐 — **DONE**（Gap B）：`docs/proof/dsl-structured-program.md` 措辞已收窄，
   撤销静态 timerTask RED overclaim，改为三合一证明；不新增 dsl-ownership timer gate。

## Completion criteria

- Gap A（Finality 回归）实现并以可失败测试闭环后才可标 DONE；Gap B（docs 措辞对齐）编辑完成即
  标记 DONE，不新增 dsl-ownership timer gate。
- 未实现前不得在 `changes/completed/fix.md` 的 REVISE follow-up 中标记 DONE。

### 状态

- Gap A — **DONE**：`GLORY_074`（abandonment → `Undecided`，AbandonedAt 触发，无 partial
  rejection）与 `GLORY_075`（waiter crash → durable resume → 唯一带 `# Work log` 的
  `FinalityRejected`）已落盘并通过运行时验证；`finality-cohort-law.test.mjs` 8/8 pass。
- Gap B — **DONE**：docs 措辞收窄完成，B 类零轮询以三合一证明（事件驱动 `awaitChangeFrom` +
  行为 callOrder + mutable 对抗 fixture）闭环，未新增 dsl-ownership timer gate。
- 全量门禁 — **DONE**：DevOps 实测 `npm run check` 全量 exit 0（Lint / Build / 1837 unit /
  integration），`manager-unhappy-path` 与 `devops-mechanical-repair-loop` e2e exit 0。

## Blockers

- Blogger abandonment 与 waiter-crash 场景需先在 `docs/proof/glory.md` §29 对齐后再落回归，
  避免测试与规范措辞漂移。

### 状态

- **已消除**：GLORY_074（abandonment）与 GLORY_075（waiter-crash）按 `docs/proof/glory.md` §29
  落盘并通过，规范与回归无漂移。

---

# Final outcome

> 本文件是变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。

## Outcome

本 REVISE 冻结的两项技术缺口已全部闭环并经 DevOps 验证，现归档至 `changes/completed/`。

## Scope (frozen) 最终状态

1. **Finality record-ready 拒绝/崩溃恢复回归（Gap A）— DONE**：
   `tests/unit/execution/finality-cohort-law.test.mjs` 补两个专项回归并全部通过：
   - `GLORY_074_blogger_abandonment_during_record_ready_concludes_undecided_no_partial_rejection`：
     Blogger 放弃（AbandonedAt 触发，coverageCanAdvance 变 false）后，`recordReadiness` 判
     `RecordUnavailable`，`concludeRejection` fail-close 至 `FinalityUndecided`，绝不产生缺
     `# Work log` 的 `FinalityRejected`/`WorkRecordRef`。
   - `GLORY_075_waiter_crash_resumes_from_durable_evidence_no_timer_poll`：本地 waiter 死亡后，
     `resumeDurableRevise` 以相同 ToolCallId 从 durable evidence 续等，经 `awaitChangeFrom`
     唤醒（无 timerTask/sleep re-probe），appendBlogCoverage 后唯一 `FinalityRejected` 引用
     非空 `# Work log`，且不重开/re-enlist cohort。
2. **B 类零轮询闭合（Gap B）— DONE**：`docs/proof/dsl-structured-program.md` 措辞收窄，撤销
   「timerTask→re-probe 形状在 dsl-ownership 静态判 RED」overclaim；B 类零轮询以三合一证明
   闭环（生产 `AgentJournal.awaitChangeFrom`、`executor-summarize.test.mjs` 行为 callOrder、
   `ExecutorSummarize` 形裸 `mutable` 对抗 fixture），未新增 dsl-ownership timer gate。

## Completion criteria

- Gap A（Finality 回归）以可失败测试闭环 — **DONE**：GLORY_074/075 落盘并通过（8/8）。
- Gap B（docs 措辞对齐）— **DONE**：编辑完成，未新增 dsl-ownership timer gate。
- 全量门禁 — **DONE**：`npm run check` 全量 exit 0（Lint / Build / 1837 unit / integration）；
  `manager-unhappy-path` 与 `devops-mechanical-repair-loop` e2e exit 0。

## Verification

- 2026-08-09：`finality-cohort-law.test.mjs` 8/8 pass（含 GLORY_074/075）；
  `npm run check` 全量 exit 0；`manager-unhappy-path` 与 `devops-mechanical-repair-loop`
  两项 e2e exit 0。

# finality — PROOF

行为合同：`WHAT.md`（FINALITY-001..028）。实现模型：`HOW.md`。

## 测试资产

### 本包 tests/（`requirements/finality/tests/`）

| 文件 | 来源 | 类型 | 断言数 |
|---|---|---|---|
| `manager-finality-disposition.test.mjs` | NEW | `ManagerFinality.classifyEnding` / `admitLabor` 纯代数 + `Roles_isAllowed` + `ReviewerOutcome` 分型 | 19 |
| `manager-job-no-resurrection.test.mjs` | NEW | FINALITY-028：terminal 不复活 / active 同 session·worktree 续做 | 4 |
| `finality-background-obligation.test.mjs` | NEW | FINALITY-027：`TerminalPolicy.outstandingBackground` 的 Manager join 义务谓词 | 2 |

`node --test requirements/finality/tests/manager-finality-disposition.test.mjs` 单独跑绿。
`node --test requirements/finality/tests/manager-job-no-resurrection.test.mjs` 单独跑绿。
`node --test requirements/finality/tests/finality-background-obligation.test.mjs` 单独跑绿。

### REUSE（留在原处；glory 族按 PROOF-MAP KEEP，多 owner 交叉 SPLIT@cutover）

| 文件 | 锚点 | 本包拥有的断言 | SPLIT@cutover |
|---|---|---|---|
| `requirements/finality/tests/lifecycle.test.mjs` | `WHAT[FINALITY-021] LifeOpened opens the first life`、`WHAT[FINALITY-022] a second life cannot open while one is active`、`WHAT[FINALITY-008] FinalityRequested is rejected while a request is open`、`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`、`WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`、`WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life`、`WHAT[FINALITY-017] isLifeArchived true only after life completed`、`WHAT[FINALITY-026] FinalityUndecided closes the request without a wound record`、`WHAT[FINALITY-011] a revise closes finality without confirming the life`、`WHAT[FINALITY-021] lifecycle facts round trip through ndjson`、`WHAT[FINALITY-019] idle encouragement golden bytes`、`WHAT[FINALITY-026] host undecidable golden bytes`、`WHAT[FINALITY-012] finality rejection renders work record as guidance comments`、`WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary`、`WHAT[FINALITY-013] finality three experiences`、`WHAT[FINALITY-022] reawakening golden bytes`、`WHAT[FINALITY-004] first birth golden bytes`、`WHAT[FINALITY-024] activation golden bytes` | lifecycle 事实代数、rejection 关闭、blessing 不结束、rest 归档、undecided 收束、三经验文案、idle、Reawakening | `GLORY_075`→`WHAT[PREFIX-STABILITY-007]`（prefix-stability）、`SURFACE_002`→`WHAT[PROVIDER-LANGUAGE-005]`（provider-language）、`SURFACE_005`（participant-horizon）、`SURFACE_006`（verification-system）、`GLORY_074`（obligation-ledger）、`GLORY_014/019/021`（GARBAGE legacy，迁移窗口后随 absence 政策退役） |
| `requirements/finality/tests/finality-cohort-law.test.mjs` | `rosterOf` / `graduatedReviewer` / enlistment 幂等 / `dropEphemeral` 恢复的 theorem 集 | roster 代数、graduate 推导、durable resolution 恢复 | witness/ConfirmedReviewWitness 的代数断言 → `review-assurance` |
| `requirements/finality/tests/rewrite-consistency.test.mjs` | `WHAT[FINALITY-023] opening rewrite is byte identical across requests`、`WHAT[FINALITY-022] host title request never opens a life`、`WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message`、`WHAT[FINALITY-024] work-time messages are never rewritten` | Opening 改写幂等；host title 请求不开 Life；工作期输入不改写 | ARCH-004 seal 断言 → `prefix-stability` |
| ~~`tests/unit/glory/manager-lifecycle-gate.test.mjs`~~ | `GLORY_018_in_progress_manager_turn_never_activates` | 生产 Activation 缺席（GARBAGE 侧回归） | 已 DELETE（Wave 2a）：仅证明迁移完成（PROOF-MAP 强制删除清单第 6 项） |
| `requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs` | `TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission`、`TODO-006 T2 prepare succeeds once T1 process review is Concluded` | drain 输入的 ConsumableReview gate | 其余 → obligation-ledger / effect-accounting / host-boundary |

### e2e（cutover 范围外，记录指针）

- ~~`tests/e2e/cases/manager-unhappy-path.test.mjs`~~：完整自杀/拒绝/继续/祝福/rest 剧本（glory.md proof 第 3 层；stroke 13 last_words 逐字 terminal；cases/ 已随 G4R cutover 删除，剧本并入 Long Stroke）。
- ~~`tests/e2e/cases/finality-cohort-law.test.mjs`~~：GLORY_074/075 record-ready 崩溃 canary（→ review-assurance 交叉；cases/ 已删除，canary 语义由本包 finality-cohort-law 测试承接）。
- `requirements/verification-system/tests/e2e/support/magic-todo-host-canary-plugin.mjs`：canary A/E/G/H 真实 Host 侧（→ host-boundary）。

## 命题 → 落点

| 命题 | 落点测试（文件 + 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| F-1 001 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-001] only the Manager holds ToolPermission.Finality` | NEW | `node --test requirements/finality/tests/manager-finality-disposition.test.mjs` |
| F-2 002 | F-3..F-7 组合（前置 + drain + 门禁）；总纲由各落点共同承担；`tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-002] finality eligibility is the combination of commitment, request, and experience typing`；REUSE lifecycle `GLORY_010/045/060` | NEW + REUSE | 见 F-1 |
| F-3 003 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-003] an open request resumes the same ToolCallId replay`、`WHAT[FINALITY-003] an open request with no enlisted members is recoverable`、`WHAT[FINALITY-003] a request already in motion waits for the current cohort`（受理失败路径零创建）；REUSE lifecycle `WHAT[FINALITY-008] FinalityRequested is rejected while a request is open` | NEW + REUSE | 见 F-1 |
| F-4 004 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-004] no accepted planComplete=true commitment stays at Planning Table`（无 durable plan commitment 时 `ContinuePlanning`，即使已有 accepted `planComplete=false` planning checkpoint 也不得进入 Finality）；REUSE lifecycle `WHAT[FINALITY-004] first birth golden bytes`；账本侧见 obligation-ledger commitment projection proof | REWRITE + REUSE | 见 F-1 |
| F-5 005 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-005] the rest-path suicide is a drain, not a new cohort`（二次 suicide 仍走 rest 而非新 cohort）；REUSE membrane `TODO-006 T2 prepare succeeds once T1 process review is Concluded`（drain 的 ConsumableReview gate） | NEW + REUSE | 见 F-1 |
| F-6 006 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-006] drain outcomes are two-typed: Revision (REVISE) vs Confirmed (PERFECT)`（REVISE 后不 BeginFinality 之前置）；REUSE lifecycle `WHAT[FINALITY-011] a revise closes finality without confirming the life` | NEW + REUSE | 见 F-1 |
| F-7 007 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-007] no mechanical terminal-todo completeness gate`（有 obligations 的 Life 无机械 completeness 判定）；HOW 记录机械 gate 缺失 | NEW | 见 F-1 |
| F-8 008 | REUSE lifecycle `WHAT[FINALITY-008] FinalityRequested is rejected while a request is open`、`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（request 生命周期 durable）；REUSE `tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-008] drop ephemeral preserves durable finality facts: no duplicate completion` | REUSE | lifecycle SPLIT@cutover |
| F-9 009 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-009] roster is ungraduated history plus exactly one new`、`WHAT[FINALITY-009] crash reentry reuses already created new slot exactly once`、`WHAT[FINALITY-009] historical enlist order confluent for roster`、`WHAT[FINALITY-009] drop ephemeral preserves open finality roster source` | REUSE | finality-cohort-law SPLIT@cutover |
| F-10 010 | REUSE `requirements/finality/tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-010] graduated reviewer excluded from roster`（graduate 只由 enlistment + witness 推导） | REUSE | 同上 |
| F-11 011 | REUSE lifecycle `WHAT[FINALITY-011] a revise closes finality without confirming the life`（REVISE 关 cohort 不落 FinalityRejected）；`WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（rejected 后新 suicide 开新 request） | REUSE | lifecycle SPLIT@cutover |
| F-12 012 | REUSE lifecycle `WHAT[FINALITY-012] finality rejection renders work record as guidance comments`（rejection evidence 渲染）；steer 双轨交付 e2e 见指针（cutover 范围） | REUSE | lifecycle SPLIT@cutover |
| F-13 013 | REUSE lifecycle `WHAT[FINALITY-013] finality three experiences`、`WHAT[FINALITY-012] finality rejection renders work record as guidance comments`、`WHAT[FINALITY-026] host undecidable golden bytes` | REUSE | lifecycle SPLIT@cutover |
| F-14 014 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-014] rejection keeps the same Life and a new suicide begins fresh Finality`（同 Life 继续；BeginFinality）；REUSE lifecycle `WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one`（Rejected 永不 blessing） | NEW + REUSE | 见 F-1 |
| F-15 015 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-015] a blessing keeps the enlisted process-review standing: no dispose`（Blessing 不 Dispose process duty）；REUSE lifecycle `WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`（Blessing 不结束 Life）；过程 duty 保留 → obligation-ledger O-20 | NEW + REUSE | 见 F-1 |
| F-16 016 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-016] a blessing leaves the Life open until the second suicide`；REUSE lifecycle `WHAT[FINALITY-016] a blessing leaves the life open until the second suicide`、`tests/finality-cohort-law.test.mjs` `WHAT[FINALITY-016] blessed exactly once: second completion rejected` | NEW + REUSE | 见 F-1 |
| F-17 017 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-017] the second suicide after a blessing is the rest path`（CompleteBlessedLife 分派）；REUSE lifecycle `WHAT[FINALITY-017] isLifeArchived true only after life completed`（归档 + CompletedTerminal）、`WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life` | NEW + REUSE | 见 F-1 |
| F-18 018 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-018] an open request owns the Life: Manager labor is deferred`（open request 停放劳动） | NEW | 见 F-1 |
| F-19 019 | REUSE lifecycle `WHAT[FINALITY-019] idle encouragement golden bytes`（鼓励正文）；`requirements/interaction-authority/tests/idle-continuation-authority.test.mjs::HOST_004_idle_manager_continuation_consumes_one_permit_and_claims_once`（同 Life、同 plan-commitment condition 的新 terminal 不再发送；获得 plan commitment 后才获得另一份有界预算）；open/completed 不发送由 Manager lifecycle 组合断言 | REUSE | `node --test requirements/finality/tests/lifecycle.test.mjs requirements/interaction-authority/tests/idle-continuation-authority.test.mjs` |
| F-20 020 | REUSE lifecycle `WHAT[FINALITY-020] manager surface has no forbidden words`（无隐藏机制词——admission 归 participant-horizon，本包引用其 proof）、`WHAT[FINALITY-020] manager role law does not name foreign tools`、`WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary`（rejection 渲染无机制解释） | REUSE | participant-horizon SPLIT@cutover |
| F-21 021 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-021] disposition never derives from narrative text`；REUSE lifecycle `WHAT[FINALITY-021] lifecycle facts round trip through ndjson`、`WHAT[FINALITY-021] LifeOpened opens the first life` | NEW + REUSE | 见 F-1 |
| F-22 022 | `requirements/finality/tests/life-admission.test.mjs` `WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history` + `WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id`；`requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-022] active HumanRoot profile does not make another user message a root`、`WHAT[FINALITY-022] host title request never opens a life`；`tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-022] a new Life inherits no blessing/roster/request and starts fresh Finality`；REUSE lifecycle `WHAT[FINALITY-022] a second life cannot open while one is active`、`WHAT[FINALITY-022] reawakening golden bytes` | NEW + REUSE | `node --test requirements/finality/tests/life-admission.test.mjs requirements/finality/tests/rewrite-consistency.test.mjs` |
| F-23 023 | REUSE `requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-023] opening rewrite is byte identical across requests`、`WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message` | REUSE | rewrite-consistency SPLIT@cutover |
| F-24 024 | `requirements/finality/tests/rewrite-consistency.test.mjs` `WHAT[FINALITY-024] work-time messages are never rewritten`；REUSE lifecycle `WHAT[FINALITY-024] activation golden bytes`（工作期输入不改写 → obligation-ledger O-17/O-25 交叉：Opening 不因工作期输入移动） | NEW + REUSE | 见 F-22 |
| F-25 025 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-025] a completed Life replays as AlreadyCompleted, never restarts`（completed 保持）；REUSE lifecycle `WHAT[FINALITY-021] lifecycle facts round trip through ndjson`（inert decode 回归） | NEW + REUSE | 见 F-1 |
| F-26 026 | `tests/manager-finality-disposition.test.mjs` `WHAT[FINALITY-026] a rejected request does not block labor: labor may continue`、`WHAT[FINALITY-026] resolved historical requests do not block labor`（undecided/resolved 不阻塞劳动——LaborMayContinue）；REUSE lifecycle `WHAT[FINALITY-026] FinalityUndecided closes the request without a wound record`、`WHAT[FINALITY-026] host undecidable golden bytes` | NEW + REUSE | 见 F-1 |
| F-27 027 | `tests/finality-background-obligation.test.mjs` `WHAT[FINALITY-027] Manager without journal or handles is never outstanding`、`WHAT[FINALITY-027] Manager with a listable child handle has a join obligation`（join 资源检查运行时体现）；REUSE lifecycle `WHAT[FINALITY-019] idle encouragement golden bytes`（idle 只在非 finality 情形） | NEW + REUSE | `node --test requirements/finality/tests/finality-background-obligation.test.mjs` |
| F-28 028 | `tests/manager-job-no-resurrection.test.mjs` `WHAT[FINALITY-028] a terminal ManagerJob is not active and does not resume` / `WHAT[FINALITY-028] later progress cannot reopen a terminal ManagerJob` / `WHAT[FINALITY-028] replaying ManagerJobCreated cannot re-enlist a terminal job` / `WHAT[FINALITY-028] an active owned job continues on the same session and worktree` | NEW | `node --test requirements/finality/tests/manager-job-no-resurrection.test.mjs` |

## 覆盖统计

- 命题 28 / 落点 28（NEW 3 文件；REUSE 4 文件族；GAP 0）。
- 移动文件：0（glory 族按 PROOF-MAP KEEP 保留原位，SPLIT@cutover 拆分见上表）。
- 新写文件：3（`manager-finality-disposition.test.mjs` 19 断言；`manager-job-no-resurrection.test.mjs` FINALITY-028；`finality-background-obligation.test.mjs` FINALITY-027）。
- 拆分：`manager-finality-disposition` 11→19（054/055、060/062、040、020 双命题拆分）；`lifecycle` 19→22（GLORY-060、GLORY-052 双命题拆分）；`rewrite-consistency` 4→5（GLORY-015 persisted 拆分 023+024）。

## semantic anchor id（semantic-anchors.mjs，MECHANISM 逐 ID 归包）

本包声明拥有 `scripts/checks/semantic-anchors.mjs` 中 manager 角色的下列 anchor id
（`ROLE_SEMANTIC_ANCHORS.manager`；机制文件在 cutover 时按此声明标注 owner）：

- `returned-record` —— 返回的记录只通过它所建立的事实改变 mission（FINALITY-012/016：
  rejection/blessing 的 LWR 是 evidence 不是新指令）。

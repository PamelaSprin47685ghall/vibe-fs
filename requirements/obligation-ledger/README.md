# obligation-ledger

> Manager 用同一份 owed-work ledger 横跨规划与执行；第一次 accepted `planComplete=true` 是不可逆托付承诺，而不是新状态机。

## 一句话 WHY

Manager 必须始终有一份持续诚实、可恢复、单一真相源的「当前仍欠什么」账本：commitment 前可记录完成计划仍欠的 planning work，commitment 后记录完成用户 mission 仍欠的 mission debt。第一次 accepted `planComplete=true` 只记录一项已经发生的业务承诺；它不可回退，但不编码“程序下一步”。查询从 O(1) projection 读，流程由 F# Direct CE 直接执行，恢复重入同一普通 workflow。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 统一账本 | OBLIGATION-LEDGER-001/002/003 | Pre-T1 = planning owed work；Post-T1 = mission debt；wire = `planComplete` + `workingOn` + `{name,work}`；`workingOn` 只指当前焦点，无 item status 枚举机 |
| 诚实义务 | OBLIGATION-LEDGER-004/005/006 | planning work 只在 commitment 前合法；任何阶段都拒绝空 placeholder；identity 不靠文本猜 |
| Admission | OBLIGATION-LEDGER-007/008/009 | 同 message 多 todowrite 全拒；replay 幂等；失败三态分型 |
| 账本真相 | OBLIGATION-LEDGER-010/011/015 | Accepted 立即 supersede；REVISE 不拥有账本；canonical 单真相源 |
| 评审节拍 | OBLIGATION-LEDGER-012/013/014/022 | 每 Accepted 派生一次 Rk；1:1 lag-1 消费；悬挂义务规则 |
| 生命周期 | OBLIGATION-LEDGER-016/017/019/020/021/023 | 首次 accepted true = T1；true 单调；BlindPlan；新 Life 空账；Dedicated 每 Life 一个；committed cutoff |
| 恢复与门禁 | OBLIGATION-LEDGER-018/024/025/026 | O(1) 增量 projection；Direct CE；Boot Fold 后重入；before/after effect shell fail-closed |

## HOW 概览（→ HOW.md）

- 类型：`src/Wanxiangshu/Domain/{MagicTodo,MagicTodoAdmission,MagicTodoAfter,MagicTodoFacts,MagicTodoObligationCodec,MagicTodoProcessReview,MagicTodoSurface}.fs`
- workflow：`src/Wanxiangshu/Application/Manager/ObligationLedgerWorkflow.fs`（目标形状：Direct `task {}` CE + 具名 capability；无 Command/Reply interpreter、无 durable stage）
- Host effect shell：`src/Wanxiangshu/Infrastructure/OpenCode/**` 只 decode/materialize/调用 workflow/投影 compatibility，不拥有业务顺序；现有 `Mission/Obligation/Todo/MagicTodoMembrane.fs` 在本次大修中缩退/拆分
- fact + O(1) projection：`src/Wanxiangshu/Journal/{MagicTodoProjection,MagicTodoFactCodec}.fs`；热路径只读增量字段，不扫描 Accepted 历史链
- review：`src/Wanxiangshu/Application/Review/{TodoProcessReviewProgram,DedicatedTodoReviewerRuntime}.fs`
- Host sink：`src/Wanxiangshu/Domain/MagicTodoSurface.fs`（compatibility TodoTable 投影；HOW 层，非永久需求）

## proof 概览（→ PROOF.md）

- MOVE（6 文件，38 断言全绿）：`tests/unit/domain/magic-todo*.test.mjs`（3）、`requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs`、`requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs`、`requirements/obligation-ledger/tests/opening-floor.test.mjs` → `requirements/obligation-ledger/tests/`
- REUSE：`requirements/obligation-ledger/tests/magic-todo-membrane.test.mjs`（admission/Accepted 双路径/REVISE 回灌；跨 effect-accounting/host-boundary）、`requirements/obligation-ledger/tests/integration/plugin/magic-todo-sink-canary.test.mjs`（compatibility sink 冻结）、`requirements/finality/tests/lifecycle.test.mjs`（GLORY_074 T1 交叉）
- NEW：无（命题全部已有可执行落点）

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、历史上 RED 过什么
2. `WHAT.md` —— 唯一 normative 合同（编号命题）
3. `HOW.md` —— 实现模型 + 历史与弃权
4. `PROOF.md` —— 每条命题的测试落点与运行命令

## DEPENDS ON

- `durable-events`：`TodoWritePrepared/Accepted` 事实的不可变、原子 append、先 commit 后 fold、O(1) projection 查询，是 canonical account + commitment 恢复的 substrate。
- `effect-accounting`：physical success 的 Requested/Accepted 双路径分型决定 `TodoWriteAccepted` 何时可落盘（live/recovery 收敛）。
- `semantic-trace`：ReviewFrontier / Opening 区间由 XTrace cursor 界定；过程 review 需要原始语义历史可定位。

## 边界（DOES NOT OWN）

- Reviewer judgement meaning（PERFECT/REVISE 的语义）→ `review-judgement`
- review evidence / witness / seal 的可消费性 → `review-assurance`
- Finality 接受资格与 cohort / blessed / rest → `finality`
- Host TodoTable / UI sink 的具体实现 → HOW（compatibility 不是永久需求）
- 当前 `todowrite` schema、`planComplete`/`name`/`work` 字段名、T1 文案具体 wording → HOW / `provider-projection` / `provider-language`
- Direct CE / 禁止第二 runtime / 恢复重入普通 workflow 的一般法则 → `structured-workflow`
- Manager Persona / Role Law → `participant-identity` / `office-capability`
- desired cutoff 的 PrefixEpoch seal 机制 → `prefix-stability`
- LWR 物化与三段标题 → `work-record`
- 隐藏 reviewer 的可见性 admission → `participant-horizon`
- infra fatal 的进程级处理 → `crash-reconciliation`

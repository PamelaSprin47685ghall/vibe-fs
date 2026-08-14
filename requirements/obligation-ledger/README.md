# obligation-ledger

> 长期 mission 必须持续维护当前仍欠世界什么，而不是用 phase/status 伪装工作进度。

## 一句话 WHY

Manager 的「当前还欠用户什么」必须有一个持续诚实、可恢复、单一真相源的账本。
若用 phase/status/meta-work 代替，系统会把计划、等待、评审等过程动作冒充用户债务，
崩溃后只能靠猜测恢复，REVISE 还能静默回滚已经生效的承诺。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 使命债务 | OBLIGATION-LEDGER-001/002/003 | CurrentObligations = mission debt；wire 只有 `{name,work}`；无 status 枚举机 |
| 诚实义务 | OBLIGATION-LEDGER-004/005/006 | meta-work 不是义务；可托付完整性；identity 不靠文本猜 |
| Admission | OBLIGATION-LEDGER-007/008/009 | 同 message 多 todowrite 全拒；replay 幂等；失败三态分型 |
| 账本真相 | OBLIGATION-LEDGER-010/011/015 | Accepted 立即 supersede；REVISE 不拥有账本；canonical 单真相源 |
| 评审节拍 | OBLIGATION-LEDGER-012/013/014/022 | 每 Accepted 派生一次 Rk；1:1 lag-1 消费；悬挂义务规则 |
| 生命周期 | OBLIGATION-LEDGER-016/017/019/020/021/023 | T1 commitment；BlindPlan；新 Life 空账；Dedicated 每 Life 一个；desired cutoff |
| 恢复与门禁 | OBLIGATION-LEDGER-018/024/025/026 | 只从 durable facts 恢复；V2 fail-closed；before/after 顺序合同 |

## HOW 概览（→ HOW.md）

- 类型：`src/Wanxiangshu/Domain/{MagicTodo,MagicTodoAdmission,MagicTodoAfter,MagicTodoFacts,MagicTodoObligationCodec,MagicTodoProcessReview,MagicTodoSurface}.fs`
- wiring：`src/Wanxiangshu/Application/Reconciliation/{MagicTodoMembrane,MagicTodoLocality}.fs`、`Application/Review/{TodoProcessReviewProgram,DedicatedTodoReviewerRuntime}.fs`、`Infrastructure/OpenCode/Codec/MagicTodoHostCodec.fs`
- fact + projection：`src/Wanxiangshu/Journal/{MagicTodoProjection,MagicTodoFactCodec}.fs`
- Host sink：`src/Wanxiangshu/Domain/MagicTodoSurface.fs`（compatibility TodoTable 投影；HOW 层，非永久需求）

## proof 概览（→ PROOF.md）

- MOVE（6 文件，38 断言全绿）：`tests/unit/domain/magic-todo*.test.mjs`（3）、`requirements/obligation-ledger/tests/magic-todo-event-store.test.mjs`、`requirements/obligation-ledger/tests/magic-todo-provider-boundary.test.mjs`、`requirements/obligation-ledger/tests/opening-floor.test.mjs` → `requirements/obligation-ledger/tests/`
- REUSE：`tests/unit/reconciliation/magic-todo-membrane.test.mjs`（admission/Accepted 双路径/REVISE 回灌；跨 effect-accounting/host-boundary）、`tests/integration/plugin/magic-todo-sink-canary.test.mjs`（compatibility sink 冻结）、`tests/unit/glory/lifecycle.test.mjs`（GLORY_074 T1 交叉）
- NEW：无（命题全部已有可执行落点）

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在、历史上 RED 过什么
2. `WHAT.md` —— 唯一 normative 合同（编号命题）
3. `HOW.md` —— 实现模型 + 历史与弃权
4. `PROOF.md` —— 每条命题的测试落点与运行命令

## DEPENDS ON

- `durable-events`：`TodoWritePrepared/Accepted` 事实的不可变、原子 append 与确定性 fold，是「canonical account + 恢复」的 substrate。
- `effect-accounting`：physical success 的 Requested/Accepted 双路径分型决定 `TodoWriteAccepted` 何时可落盘（live/recovery 收敛）。
- `semantic-trace`：ReviewFrontier / Opening 区间由 XTrace cursor 界定；过程 review 需要原始语义历史可定位。

## 边界（DOES NOT OWN）

- Reviewer judgement meaning（PERFECT/REVISE 的语义）→ `review-judgement`
- review evidence / witness / seal 的可消费性 → `review-assurance`
- Finality 接受资格与 cohort / blessed / rest → `finality`
- Host TodoTable / UI sink 的具体实现 → HOW（compatibility 不是永久需求）
- 当前 `todowrite` schema、字段名、T1 文案具体 wording → HOW / `provider-projection` / `provider-language`
- Manager Persona / Role Law → `participant-identity` / `office-capability`
- desired cutoff 的 PrefixEpoch seal 机制 → `prefix-stability`
- LWR 物化与三段标题 → `work-record`
- 隐藏 reviewer 的可见性 admission → `participant-horizon`
- infra fatal 的进程级处理 → `crash-reconciliation`

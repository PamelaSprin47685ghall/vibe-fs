# work-record

> 一段 bounded work 跨 participant/review/finality 传递时，必须有 canonical statement，
> 而不是 session-head summary 或固定 report DTO。

## 一句话 WHY

同一段工作要被 delegation（父→子 / 子→父）、process review、Finality 三方消费。
若改用 session head、terminal summary、receiver-relative 摘要或固定 report DTO，
会丢失 invocation 边界、复制 Opening、或让 Host 替 participant 解释工作。

## WHAT 概览（→ WHAT.md）

| 组 | 命题 | 保证 |
|---|---|---|
| 归属 | WORK-RECORD-001/002 | record 属于一段 work，不属于 receiver；边界是因果不是会话 |
| 表示 | WORK-RECORD-003/004/005 | Chronicle/Recent = representation coverage；reuse 不扩大下一次 record；Recent ≠ receiver-relative recentness |
| Opening | WORK-RECORD-006/007/008/009 | canonical 保留 Opening 即使投影省略；includeOpening 分向；Opening preserved 非重建；BlindPlan T1 constitutive |
| 单一协议 | WORK-RECORD-010/011/012 | one invocation one record everywhere；三段 + prose claim；无 Closing report / 固定 DTO |
| 诚实与分型 | WORK-RECORD-013/014/015 | LWR 禁 raw tool；RecordCoverage≠PrefixCoverage；WorkRecordStart 结构性 floor |
| 有界 | WORK-RECORD-016 | process/finality/sync 一律 request-range bounded，禁 session head |

## HOW 概览（→ HOW.md）

- 类型：`src/Wanxiangshu/Domain/LifecycleWorkRecord.fs`（`OpeningMaterial`/`LifecycleWorkRecord`/`materialize`）、
  `MagicTodoLwr.fs`（`BoundedRange`）、`SyncDelegatePrompt.fs`
- 物化：`src/Wanxiangshu/Application/Finality/LifecycleWorkRecordProjection.fs`
  （`lifecycleWorkRecordFromSnapshot` / `lifecycleWorkRecordBoundedFromSnapshot`）
- floor：`src/Wanxiangshu/Journal/ManagerOpeningFloor.fs`（WorkRecordStart 纯推导）

## proof 概览（→ PROOF.md）

- MOVE：`tests/unit/context/lifecycle-work-record*.test.mjs`（2 文件）→ `requirements/work-record/tests/`
- REUSE：`tests/unit/glory/lifecycle.test.mjs`（canonical LWR materializer）、
  `tests/unit/execution/**`（EXEC-028/031 SyncDelegate 交叉）、`tests/unit/todo/**`（TODO-008 交叉）
- NEW：`lwr-prose-claim-no-schema.test.mjs`、`lwr-record-coverage-vs-prefix-coverage.test.mjs`

## 阅读顺序

1. `WHY.md` —— 为什么必须独立存在
2. `WHAT.md` —— 唯一 normative 合同
3. `HOW.md` —— 实现模型 + 历史与弃权
4. `PROOF.md` —— 测试落点

## DEPENDS ON

- `semantic-trace`：LWR 从 XTrace 物化（`forOpening`/`forWorkRecord`/`sliceFrom`）；Opening 与 Recent work 都是 trace 区间。
- `context-compression`：Chronicle = Y frames 的覆盖表示；「哪些历史有资格被语义替换」的 guarantee。
- `participant-horizon`：record 交付给 receiver 时的信息准入边界（本包只拥有 record 本身的事实）。

## 边界（DOES NOT OWN）

- XTrace 的原始 capture/store → `semantic-trace`
- Y/Companion 如何生成 Chronicle → `context-compression`
- context compression policy → `context-compression`
- review judgement / assurance → `review-judgement` / `review-assurance`
- delegation / finality 何时请求 record → `delegation` / `finality`
- 当前 type/module/三段标题字面（可整体重写，只要 work-boundedness、preserved Opening、coverage 分型、prose-claim 与 projection semantics 不变）

# Todo — 证明

行为：`what/todo.md`。边界：`shape/todo.md`。程序：`how/todo.md`。

验证遵循 VERIFY-001 六层与 VERIFY-002 晋级阶梯。下列将 **每个 TODO 条款** 映射到 canary / unit / property / integration / static 义务；§47 release gate 与 one-stroke 路径为 P0 关闭条件（TODO-014）。

---

## 条款 → 证明义务

| 条款 | 证明焦点 | 义务类型 | 期望 |
|------|------|------|------|
| TODO-001 | BlindPlan Opening；无 Activation；LifeOpened 后立即工作；WorkRecordStart = OpeningBoundary（含 T1） | unit + property + e2e + static | 0 新 `ManagerWorkActivation` / 新业务 `WorkActivated`；OpeningMaterial byte-identical；Blogger/Y 不吞 Opening；不绑回 `WorkActivated`；Pre-T1 不钉死 WorkRecordStart |
| TODO-002 | tagged `kind:existing\|new` | unit + canary B | existing 无 id / new 带 id / unknown id / 重复 id 拒绝；definition 与 codec 同源 |
| TODO-003 | reviewing + completed 门禁 | unit | pending/in_progress/new→completed 拒；reviewing→completed 允；其它 transition Host 可过、真实性交 process review |
| TODO-004 | 单 admission；同 message 全拒；replay；V2 fail closed | unit + canary A/G/H + static + integration | 同 message 多 ToolCallId 全 fail closed；同 call 幂等；digest 冲突 fail closed；不同 call 同 list 新 checkpoint；无 hook parity 的 V2 Manager Attempt 构造失败 |
| TODO-005 | PERFECT=Pk；REVISE=merge | unit + property | union；progress-min；content/priority=proposed；单侧 cancelled 保 old.status；单一 merge owner |
| TODO-006 | Tk/Rk 1:1；lag-1 消费；ConsumableReview≡Concluded | unit + property + e2e | Accepted 数 = obligation 数；Tk 不待 Rk；Tk+1 待 Concluded(k)；仅 VerdictKnown 仍阻塞；无 prose LWR 不 Concluded；拒绝/非 admission 不建 review |
| TODO-007 | canonical vs sink + reconciliation | unit + integration + static | Host 非 recovery SSOT；REVISE 消费后 sink→settled；repair 不建 checkpoint/review；bridge 非 durable |
| TODO-008 | Dedicated + bounded LWR + coverage 分型 | unit + property + e2e + static | 每 Life 一 logical reviewer；RawGap 可进 review 不可进 prefix；RecordCoverage ⇏ PrefixCoverage；非 session-head LWR；无第二 renderer；Rk 可在 Y 落后时启动 |
| TODO-009 | PrefixEpoch seal；desired≠committed | unit + property + e2e + static | desired 自 Accepted 链；seal 前 commit TodoCheckpoint；provider 失败不回滚；无第二 ActivePrefixEpoch；cutoff=Before(T(k-1)) |
| TODO-010 | Finality tail drain；零 checkpoint fail closed；dedicated graduate | unit + e2e | first unblessed 无 Accepted → fail closed；drain latest Concluded；process REVISE 不进 Finality；无机械 todo-completeness gate；二次 suicide 仍 drain；首次 Finality ordinary enlist 后 graduate；process 留到 LifeCompleted；process PERFECT≠terminal；Blessing 不释放 process session |
| TODO-011 | legacy seed | unit + integration | 新 Life canonical 空；仅 legacy open Life 一次 seed 且在首次 Magic provider request 前注入带 ID list；后续 Life 不 adopt |
| TODO-012 | facts-only recovery；禁止 PC / 平行证据 | unit + static | crash matrix 全可恢复；无 TodoStage/Awaiting*/NeedRebase；无第二 LWR/epoch owner |
| TODO-013 | Manager surface / guideline；隐藏 reviewer | static + e2e + golden | MagicTodo guidance Manager-only；Manager 无 reviewer/session/barrier/witness/2N；可见 PERFECT/REVISE/report；ProcessReviewLWR safety-seal 不 regex；GLORY-030/SURFACE-005 窄例外锚本条 |
| TODO-014 | 跨层 ownership / §47 关闭 | static + release | 语义仅 what；shape/how/proof 不改合同；§47 37 项全绿 |
| TODO-015 | BlindPlan T1 commitment | unit + property + e2e + golden | 本 Life 首次 `TodoWriteAccepted` = T1；canonical result 含 entrustment；T1 call/result ∈ OpeningMaterial；Opening 关闭 → WorkRecordStart；system prompt / Persona / Role Law **字节不变**（Gate D）；每新 Life 再入 BlindPlan |

---

## BlindPlan / T1（TODO-001/015；Gate D 交界）

| 证明 | 期望 | 条款 |
|------|------|------|
| Pre-T1 | Planning Table；可调查；不得开始扛路；尚不关闭 Opening | TODO-015、GLORY-074 |
| T1 accepted | durable Accepted → canonical entrustment result → Opening closes；WorkRecordStart = OpeningBoundary | TODO-015、COMPANION-014 |
| T1 ∈ Opening | call + accepted result constitutive；不进 Recent work incidental 过滤 | TODO-015、COMPANION-014 |
| Prompt 稳定 | T1 前后 office system prompt byte-identical；交托只走 conversation tool result | TODO-015、PROMPT-014、ARCH-016 D |
| Reawakening | 新 Life 再入 BlindPlan；旧 Opening / obligations 不泄漏 | TODO-015、TODO-001/011 |
| Opening 永不压缩 | Manager BlindPlan Opening never compressed（§19.15） | TODO-001、COMPANION-014 |

---

## Host Canary（Phase 0 blocking）

实现 membrane 前必须真实 OpenCode contract（细节可落 HOST-017+；本表钉协议依赖）：

| ID | 断言 | 失败后果 | 关联 |
|----|------|------|------|
| A | before 改 args 达 executor，**不**改 durable pre-before ToolPart.input（无 alias 回写） | **停 membrane** | TODO-004/007 |
| B | 同时替换 parameters + jsonSchema → provider 见 V2，executor 仍走 original V1 decoder | 停 | TODO-002/004 |
| C | before 剥除 Magic `id`/`kind` 后 → original V1 decoder 仍成功（membrane 兼容投影） | 停 | TODO-002/004/007 |
| D | reviewing sink 全链路（TodoTable / todo.updated / API / UI）：passthrough 或降级 in_progress；canonical 不变 | 条件强制降级 | TODO-003/007 |
| E | after 改写 output → 本次模型可见且下一 provider history 同字节 | 停 | TODO-005/013 |
| F | execute throw 时 after 是否运行：冻结真实 Host 行为；协议不依赖 after 必跑；无 Accepted/obligation | 冻结后回归 | TODO-004 |
| G | after vs ToolPart durable completion 顺序：冻结真实顺序；Accepted 双路径收敛，不绑单一顺序公理 | 停 | TODO-004 |
| H | 仅 sessionID+callID → 完整 SDK snapshot 唯一定位 ToolPart / assistant message / provider run / ordinal / XTrace range；不能唯一 → fail closed | 停 | TODO-004 |
| I | reviewing 第五态消费者（承接 D）；UI 不稳则强制 compatibility in_progress | 条件强制降级 | TODO-003/007 |
| V2 | 无 hook parity → Manager Attempt fail closed（非 V2 裸奔） | build/check 红 | TODO-004 |

非法 tagged / unknown existing id / new-with-id / duplicate id 拒绝属 **unit**（TODO-002），不与 Canary C 混淆。

---

## Unit / Property（最低集）

路径建议：`tests/unit/todo/**`、`tests/unit/magic-todo/**`（随仓库惯例）。

### Identity / transition / merge

- kind 结构必需；new 确定性 id；existing 保 id；existing 无 id / new 带 id / unknown existing id / duplicate id 拒；重排/改 content 不改 identity（TODO-002）
- completed 门禁矩阵（TODO-003）
- merge：union、old-only、new-only、min status、proposed content/priority、PERFECT 整表替换、cancelled 保守策略；**单一** merge 符号被 before/preview/drain/test 引用（TODO-005/012）

### Cadence / admission / recovery

- Accepted ↔ obligation 一一；rejected 零 review（TODO-004/006）
- same ToolCallId replay 零第二 review；digest mismatch fail closed（TODO-004）
- Tk 只消费 R(k-1)；Concluded 缺省时 T(k+1) 阻塞；VerdictKnown 不足（TODO-006）
- PERFECT 无 prose ≠ ConsumableReview（TODO-006/008）
- Prepared+success→Accepted；Prepared+fail↛Accepted（TODO-004）
- infra failure 保持 outstanding，非 REVISE/PERFECT（TODO-006/012）

### Coverage / Opening / LWR / rebase

- OpeningMaterial 永 raw；WorkRecordStart = OpeningBoundary exclusive end（BlindPlan 含 T1）；LWR 不重复 Opening（TODO-001/008/015）
- desired cutoff 无 Requested fact；commit 在下一 seal 前；EvidenceKind=TodoCheckpoint 进既有 SSOT；失败不回滚（TODO-009）
- Tk cutoff = before T(k-1)；最新 interval 保持 raw X；restart 同投影（TODO-009）
- RecordCoverage ⇏ PrefixCoverage；RawGap ⇏ prefix replacement（TODO-008/009/012）
- ManagerCheckpointLWR 不越 ReviewFrontier；并发 post-Tk 工作不漏进 Rk；ProcessReviewLWR 排除 assignment prompt 与 R(k-1) history；dedicated reviewer head 不作 report；includeOpening=false；Rk 可在 Y 落后时启动（TODO-008）
- ReviewWorkStartCursor = assignment authority 落地后 exclusive end（TODO-006/008）
- Manager-facing LWR safety-seal，无 regex wash（TODO-013）
- 四段标题仅 Opening / Chronicle / Recent work / Closing report（COMPANION-003；TODO-001）

### Dedicated / Finality

- 一 Life 一 logical reviewer；replacement 仅 proven loss（TODO-008/010）
- process 输入 = OpeningMaterial（若投影）+ bounded LWR + Ck + Pk；生产 process/Finality `includeOpening=false`（TODO-008）
- first unblessed 无 Accepted fail closed；drain；REVISE 阻 FinalityRequest；REVISE→sink reconcile；无机械 completeness gate（TODO-007/010）
- D 首次 ordinary enlist + fresh dual-PERFECT；graduate 后 process 仍在；blessed 二次 suicide 无新 2N 仍 drain（TODO-010）

---

## Integration / E2E

### One-stroke（P0）

单一完整剧本（勿拆成互不关联碎片）：

```text
magic_todo_manager_unhappy_path_one_stroke
```

| Stroke | 剧情要点 | 主要条款 |
|--------|------|------|
| 1 | BlindPlan Opening；无 Activation，直接工作；Pre-T1 Planning Table | TODO-001/015 |
| 2 | T1 commitment → Opening 关；checkpoint + Dedicated + R1 一次；system 字节不变 | TODO-004/006/008/015、PROMPT-014 |
| 3 | R1 pending 时 Manager 续工；同 message 双 todowrite 全拒 | TODO-004/006 |
| 4 | T2 阻塞至 Concluded；REVISE→merge+sink；仅 VerdictKnown 仍阻 | TODO-005/006/007 |
| 5 | merge 拉低 progress 后 completed 被拒 | TODO-003/005 |
| 6–8 | reviewing 合法链；并行工作；PERFECT 后 completed | TODO-003/006 |
| 9 | provider wire lag-1：Opening 稳定；Y only through before T(k-1) | TODO-001/008/009 |
| 10 | suicide 等 R3；REVISE 不进 Finality | TODO-010 |
| 11 | 修至 process PERFECT | TODO-003/005 |
| 12 | first Finality N=3；D P/P 可 graduate；process session 保留 | TODO-010 |
| 13 | graduate 后 todowrite process 仍存活 | TODO-008/010 |
| 14 | second Finality；Blessed；process 至 LifeCompleted | TODO-010 |
| 15 | minor polish 后 process REVISE：非 rest in peace、非新 2N | TODO-010 |
| 16 | 最终 drain PERFECT → rest in peace；LifeCompleted 一次后释放 Dedicated process | TODO-010 |

### 其它 integration

- legacy open Life seed：首次 Magic request 前带 ID 注入；新 Life 空 canonical（TODO-011）
- crash matrix：Prepared/Accepted/Concluded/PrefixEpoch/VerdictKnown waiter（`how/todo.md` 表）全可恢复（TODO-012）
- V2 runner 拒绝路径（TODO-004）
- Manager surface 无隐藏编排词；tool result 含上一 ProcessReviewLWR（TODO-013）

---

## Static governance gates

永久静态门禁（生产源；legacy decoder 白名单除外）：

| Gate | 拒绝 | 条款 |
|------|------|------|
| 43.1 No Activation owner | 新业务引用 ManagerWorkActivation/PlanningTail/WorkActivated/ProtectedPrefixEnd | TODO-001/012 |
| 43.2 No Todo PC | TodoStage/ReviewStage/AwaitingTodoReview/NeedTodoRebase/NextTodoAction… | TODO-012 |
| 43.3 One merge owner | 多份 semanticMerge | TODO-005/012 |
| 43.4 One schema owner | definition/decoder/examples/renderer 分叉 | TODO-002/012 |
| 43.5 Hidden-review surface | Manager 面 reviewer/barrier/witness/2N/confirmation | TODO-013 |
| 43.6 V2 bypass | 未证明 parity 的 runner 绿建 | TODO-004 |
| 43.7 One LWR renderer | TodoProcessReviewEvidenceProjection 等第二工作记录 | TODO-008/012 |
| 43.8 Coverage split | RecordCoverage→prefix 或 PrefixCoverage→LWR gap 或 session-head LWR | TODO-008/012 |
| 43.9 Tagged + single admission | optional-id；同 message winner；同 list 去重跳过新 call；digest mismatch 仍 Accepted | TODO-002/004 |
| 43.10 Opening floor | Opening 保护绑回 WorkActivated；OpeningMaterial 重建；T1 改写 system prompt | TODO-001/015、PROMPT-014 |
| 43.11 Desired ≠ committed | Accepted 立即 PrefixRebaseCommitted；RebaseRequested Stage；缺 Epoch 字段旁路；第二 ActivePrefixEpoch；成功才 commit/失败回滚；先发后补 | TODO-009/012 |
| 43.12 ConsumableReview typing | 无 WorkRecordRef 的 Concluded；VerdictOnly Stage；VerdictKnown≡Consumable | TODO-006/012 |
| 43.13 Zero-checkpoint Finality | first unblessed 无 Accepted 进 Finality | TODO-010 |
| 43.14 Sink reconciliation | REVISE 后永久否决 Pk 留 Host；repair 写成 checkpoint/review | TODO-007 |

---

## §47 Release Gate 映射

Change Completed **当且仅当**下列全部成立（括号内主导条款 + 证明层）：

1. 无新 Activation/WorkActivated 业务路径（TODO-001；static+e2e）
2. LifeOpened 后 BlindPlan 可工作；T1 关 Opening（TODO-001/015；e2e S1–S2）
3. WorkRecordStart / OpeningMaterial 护 Opening；T1 ∈ Opening；system 字节稳定（TODO-001/015、PROMPT-014；unit+property）
4. MagicTodo guidance Manager-only（TODO-013；static+unit）
5. tagged kind union（TODO-002；unit+canary B）
6. 仅 new 分配 id；existing 已知 id（TODO-002；unit）
7. reviewing 正式；非法 completed 拒（TODO-003；unit）
8. Accepted↔obligation 1:1（TODO-006；property）
9. 非 admission 零 review（TODO-004/006；unit）
10. 同 message 多 todowrite 全拒（TODO-004；e2e S3）
11. replay 幂等 + digest 冲突 fail closed；不同 call 新开（TODO-004；unit+canary G/H）
12. lag-1 消费；Tk 不待 Rk；待 Concluded（TODO-006；e2e S4）
13. REVISE merge 裁决（TODO-005；unit）
14. PERFECT 整表替换（TODO-005；unit）
15. tool result 含上一 LWR + preview/规则（TODO-005/013；e2e+canary E）
16. review 期间 Manager 可续工（TODO-006；e2e S3/S7）
17. 每 Life 一 dedicated process，留到 LifeCompleted（TODO-008/010；e2e S12–16）
18. reviewer 输入 bounded LWR+todos（`includeOpening=false`；TODO-008；unit）
19. LWR 允许 RawGap；不等 Y 追平（TODO-008；unit）
20. process/Finality LWR 非 session head（TODO-008；unit+static）
21. desired 自 Accepted；seal 前 PrefixEpoch TodoCheckpoint；失败不回滚（TODO-009；e2e S9+unit）
22. 只换 proven Y；RawGap 不进 prefix；无第二 epoch SSOT（TODO-008/009/012；static+property）
23. OpeningMaterial raw byte-stable；LWR 不重复 Opening；四段新标题（TODO-001/008/015；property）
24. Prepared+success 可恢复 Accepted；mismatch/fail 永不（TODO-004/012；unit）
25. first unblessed 至少一 Accepted；drain；process REVISE 不进 Finality（TODO-010；e2e S10）
26. 无机械 terminal-todo gate（TODO-010；static+e2e）
27. Dedicated 首次 Finality 后 ordinary graduate（TODO-010；e2e S12–14）
28. process PERFECT≠terminal；enlist fresh dual-PERFECT（TODO-010；e2e）
29. Blessed 后可 todowrite；process session 不因 Blessing 释放（TODO-010；e2e S13–15）
30. second suicide 无新 2N，仍 drain（TODO-010；e2e S15–16）
31. 新 Life 空 canonical；legacy 一次 seed 且 request 前注入（TODO-011；integration）
32. Manager 无隐藏编排；LWR safety-seal（TODO-013；static+e2e）
33. Host 非 canonical recovery；REVISE 后 sink reconcile；bridge ephemeral（TODO-007；unit+static）
34. alias / after-vs-ToolPart / session+call canary + V2 fail closed（TODO-004 + canary A/G/H）
35. 无独立 process-review 工作记录投影；coverage 分型；PERFECT/REVISE 皆有 prose record（TODO-006/008/012；static+unit）
36. ConsumableReview≡Concluded；VerdictKnown 不可消费（TODO-006；unit+e2e S4）
37. one-stroke + docs + static gates + 全量 check 通过（TODO-014 + VERIFY）

---

## 代表路径（落地时对齐仓库）

```text
tests/canary/host/magic-todo-*.test.mjs          # A–I、V2
tests/unit/todo/**                               # algebra / merge / cadence
tests/unit/todo/recovery-matrix.test.mjs
tests/e2e/cases/magic-todo-manager-unhappy-path-one-stroke.test.mjs
tests/e2e/scenarios/magic-todo-manager-unhappy-path.toml
static gates: Activation / PC / merge / schema / surface / V2 / LWR / coverage /
              tagged-admission / Opening floor / epoch / ConsumableReview /
              zero-checkpoint / sink
```

未落地前，proof 义务仍以本表为 SSOT；实现不得缩减 §47 任一项（TODO-014）。

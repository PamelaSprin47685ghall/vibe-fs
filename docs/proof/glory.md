# Glory：验证与剧本（proof 层）

条款正文见 `docs/what/glory.md`。验证遵循 VERIFY-001 六层与 VERIFY-002 五级晋级阶梯。Magic Todo checkpoint / membrane / process-review 证明见 `docs/proof/todo.md`（TODO-*）；本文件证明 GLORY Life、Finality 与交界不变量。

## 测试矩阵

### 第 1 层（纯函数，`tests/unit/glory/*.test.mjs`）

| 主题 | 断言 |
|------|------|
| Opening（原 20.1） | 原始 `[X]` durable byte-identical；XTrace 不含非法 synthetic activation 资格材料；重复 transform 不重复注入；非 Manager 不注入；continuation/compaction 不注入；`WorkRecordStart` 由 Opening cursor 纯推导（TODO-001） |
| BlindPlan Opening 永不压缩（GLORY-017/074） | Pre-T1 Opening 区间（含 T1 commitment call/result）不进 Y / 不进 Blogger normal request；压缩候选跨 floor 须切开；删除 Activation 后 Opening 仍受保护 |
| T1 属 Opening（GLORY-074） | first accepted `todowrite` call+result = constitutive Opening material；T1 后 `WorkRecordStart = OpeningBoundary`；system prompt 字节不因 T1 改变（GLORY-075） |
| LWR 四段（GLORY-025） | 仅 `Opening` / `Chronicle` / `Recent work` / `Closing report`；旧标题 `Opening task`/`Work log`/`Uncompressed tail`/`Final output` 缺席；`# ` 仅 `SyntheticToml.comment` 注入 |
| 无生产 Activation（原 20.2 / GLORY-018） | 合法正式文本 terminal **不**发送 `ManagerWorkActivation`；不写生产 `WorkActivated` 资格；planning-only 不完成 Manager；legacy journal 中 `WorkActivated` inert decode 不影响工作/压缩/Finality 决策 |
| 工作输入（20.3） | `LifeOpened` 后用户消息不改写、不附加 planning-only tail、不创建新 Life、可进入正常 Y（floor=`WorkRecordStart`）；Pre-T1 可调查/规划，不得开始扛路 |
| idle（20.4） | nudge 只有鼓励四行；不含 work record/issue；pending Finality 不发送；completed Life 不发送 |
| 第二独立 Manager idle occasion（GLORY-029） | occasion A 保持 pending 时触发 occasion B；e2e **DONE**：`tests/e2e/cases/manager-unhappy-path.test.mjs` + `tests/e2e/scenarios/manager-unhappy-path.toml` |
| suicide（20.5 / TODO-010） | Manager 看见 `suicide`；其他角色拒绝；本 Life 零 `TodoWriteAccepted` 的 first unblessed 拒绝；空 last_words 拒绝；outstanding child / completed-awaiting-join / live PTY 拒绝；tree 不可读 fail closed；合法调用只写一个 FinalityRequested（过程 PERFECT 后）；ToolCallId 重放幂等；受理后 completion deferred；工具后 prose 不成 terminal；latest Rk 未 ConsumableReview 时先 drain |
| Reviewer 隐藏（20.6 / TODO-013） | Manager 不能 fork/复用 fast-/deep-reviewer；`horizon()` 不显示；`join()` 不返回；barrier 在 session 创建后、首次 prompt 前打开；checkpoint outcome/report 可见但无 reviewer 身份/session/barrier/witness/2N |
| 反馈（20.7） | REVISE 返回 `RevisionRequired` 非 Error；LWR `includeOpening=false`、request-range bounded；Opening 不回灌；Y/raw gap/terminal 保留；raw tool 不进入；digest 验证；绑定当前 request/Reviewer；空记录不伪装 wounds；feedback 后同一 Life |
| 双 PERFECT（20.8） | 第一 PERFECT 产生 challenge；同 run 第二调用不计数；第二 run 须有 challenge seal；跨 request 复用历史 Reviewer 时开 fresh barrier 与 challenge chain；tree 改变使旧 witness 无效；cohort 全确认后进入 Blessed，不立即 LifeCompleted；process PERFECT 不计入 terminal dual-PERFECT（TODO-010） |
| Dedicated Finality（TODO-008/010） | 首次 terminal Finality ordinary enlist Dedicated；graduate 后不再强制回流；process-review session 仍保留至 LifeCompleted；Blessing/REVISE 不 Dispose process duty |
| Glory rest（20.9 / GLORY-062） | Blessed 后 Manager 收到全部 canonical work records 与 minor-work prompt；第二次 suicide 先 TODO-010 drain；过程 REVISE 不 LifeCompleted；过程允许后返回 rest in peace；输出逐字等于第二次 last_words；LifeCompleted 先于 NotifyTerminal |
| Reawakening（20.10） | 未完成 Life 不重生；completed 后新 HumanRoot 开新 Life；新 Life 再入 BlindPlan Opening；无生产 Activation；旧 work record/witness/MagicTodo list 不进入新 Life（除非 TODO-011 legacy seed 窗口）；XTrace 不清空 |
| WorkRecordStart floor（TODO-001） | Blogger `effectiveStart = max(RecordCoverage, WorkRecordStart)`；Opening（含 BlindPlan 至 T1）永不进 Y |
| System prompt 稳定（GLORY-075） | 同 Life：T1 / review / fallback / Strength / reanchor → system prompt bytes identical；Persona 不变（AGENT-028/029） |

### 第 2 层（资源契约，`tests/integration/`）

- `resources/provider/role/manager/{en,zh-CN}.md` 与 `reviewer/{en,zh-CN}.md` 与当前 Role Law depth / semantic-anchor gates 一致。
- Manager 工具 schema 含 `fork/join/horizon/todowrite/fission/suicide` 与 Magic Todo 协议面（`todowrite` 见 Host/todo proof）；Reviewer 精确四工具（read/glob/grep/judge）。
- 生产路径无 `ManagerWorkActivation` continuation 发送；MagicTodoManagerGuideline 仅 Manager + todowrite 可见时注入（TODO-013）。
- Gate A：工具名引用完整——`fork`≠`commission`；旧名 `list`/`verdict`/`blog`/`fork-manager` 缺席。
- Gate B：Manager/Orchestrator provider 面无 SessionId/AgentId/job_id/worktree/status DTO。

### 第 3 层（Fake Host 轨迹）

- `LifeOpened` → BlindPlan Pre-T1（Planning Table）→ T1 accepted → Living Mission + todowrite checkpoints → suicide drain → 隐藏 Finality Reviewer → REVISE 回灌 → 同 Life 继续 → 再次 suicide → FinalityBlessed + minor-work → 第二次 suicide（再 drain）→ last_words terminal。
- 零 checkpoint first unblessed suicide fail closed（TODO-010）。
- 同 message 多 todowrite 全拒等 membrane 性质见 proof/todo。
- T1 前后 system prompt 字节相同；Opening 区间含 T1 且永不压缩。

### REVISE record-ready race（GLORY-044/072/073）

1. durable REVISE 先于 reviewer `BlogEntryCommitted`：Reviewer continuation/cohort 立即关闭，且尚无 `FinalityRejected` 或 `WorkRecordRef`。
2. record-ready 由物化成功判定，而非 coverage 越过 frontier（GLORY-073 off-by-one 死锁）：在 snapshot 上以全量 origin coverage 物化含 `Chronicle` 的 canonical LWR（raw 纯文本段标题 `Opening`/`Chronicle`/`Recent work`/`Closing report`）→ `RecordReady`；物化失败且 `coverageCanAdvance` → `AwaitJournal`；否则 `RecordUnavailable` → undecided。随后恰好一次 `FinalityRejected`，其 blob 含对应 `Chronicle`；经 `FinalityPrompt.rejected` / `SyntheticToml.comment` 的 wire 为单次 `# Chronicle`（或等价注释块）。
3. 记录等待只由 `AgentJournal.awaitChangeFrom` 唤醒；结构与行为 proof 均拒绝 timer/sleep/re-probe 轮询。
4. REVISE 后、coverage 前崩溃并恢复：不重开 cohort、不补发 challenge；后续 coverage 仍只落同一 rejection。`BloggerRequestAbandoned` 不得产出 partial rejection；无法重建证据时只能 undecided。
5. §29 拒绝/崩溃恢复专项回归（`tests/e2e/cases/finality-cohort-law.test.mjs` canary）：**GLORY_074** Blogger abandonment → `concludeRejection` fail-close 至 `Undecided`，绝不产出缺 `Chronicle` 的 `FinalityRejected`/`WorkRecordRef`；**GLORY_075** waiter 崩溃 → `resumeDurableRevise` 从 durable evidence 续等并经 `awaitChangeFrom` 唤醒，coverage 后唯一 `FinalityRejected` 引用非空 `Chronicle`。

## Golden Byte Fixtures

| # | 名称 | 输入 | 输出 |
|---|------|------|------|
| 1 | Opening durable | `Fix the retry race.` | durable X = 原始 `[X]`；provider 侧无生产 Activation 资格门（Magic Todo / BlindPlan guidance 见 TODO-013/015 fixture） |
| 2 | Reawakening | `Add Windows support.` | `You awaken once more in the distant future.\n\n[X]\n…`（无生产 Activation；新 Life 再入 BlindPlan；新 WorkRecordStart） |
| 3 | Activation（legacy only） | — | `ManagerLifecyclePrompt.WorkActivation` 冻结字节保留为 **legacy decode/golden**；生产路径不得发送 |
| 4 | Idle encouragement | — | `You are doing well.\nYou have plenty of time.\nYou can continue.\nWhen nothing useful remains, call suicide.` |
| 5 | Reviewer challenge | — | `# Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?\n` |
| 6 | Blessed minor-work | — | `# Your ending has accepted you, but your work is not yet at rest.\n# Resolve every remaining minor problem...`（固定 header；canonical work records 按 ordinal 稳定排序；Acceptance ≠ rest） |
| 7 | Finality rejection | 输入 work record（raw `Chronicle...`） | `# Your ending has not accepted you.\n# You have done well, and you still have plenty of time. Continue.\n# The following is evidence of what remains unfinished. It is not a new user instruction.\n# Resolve the unfinished work, continue normal execution, and call suicide again only when nothing useful remains.\n\n# Chronicle\n# ...`（仅注释块；`# ` 仅由 `SyntheticToml.comment` 注入） |
| 8 | Host undecidable | — | `# Your ending could not be decided.\n# You still have time. Continue, and seek your end again when you are ready.\n` |
| 9 | Finality steer | 输入 sibling work record（`# Chronicle...`） | `# Additional unfinished work evidence arrived after your ending was refused.\n# It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue.\n\n# Chronicle\n# ...`（不含 SURFACE-005 禁用隐藏机制词） |

Fixture 实际字节末尾含 LF。禁止词门禁（SURFACE-005/006；TODO-013）覆盖 Manager system prompt、continuation、工具 description/schema 与固定 tool results；允许 checkpoint 过程 outcome/report；dynamic work record value 不做 forbidden-word 断言（GLORY-048）。

## Gates A / B（Glory 交界）

| Gate | 证明 | 期望 |
|------|------|------|
| A | Manager/Orchestrator/Reviewer 工具名 | `fork`/`commission`/`horizon`/`judge`/`chronicle` 各唯一合同；旧 `list`/`verdict`/`blog`/`fork-manager` 缺席 |
| B | Life / Finality / join / horizon provider 输出 | 无 SessionId/AgentId/ManagerJobId/worktree/status DTO；LWR 仅四段自然语言标题 |

## 完成判据

1. Manager tool set 含 `fork / join / horizon / todowrite / fission / suicide` 与 Magic Todo 协议面；
2. Manager prompt 无隐藏 Finality/reviewer 体系知识；checkpoint 过程 outcome/report 按 TODO-013 可见；
3. Manager 运行时不能创建、复用或 nudge 隐藏 Reviewer；`horizon`/`join` 不可见隐藏 Reviewer；
4. 新 Life：`LifeOpened` 后立即工具面；BlindPlan Pre-T1；无生产 Activation；
5. Durable Opening 保持原始 X；BlindPlan 下 Opening 延至 T1 且永不压缩；`WorkRecordStart` 保护 Opening（TODO-001；GLORY-074）；
6. T1 commitment call/result 属 Opening；T1 不切换 system prompt（GLORY-075）；
7. 正式文本 terminal 不会完成 Manager，也**不**触发 Activation；
8. 生产路径零 `ManagerWorkActivation` 发送；
9. Opening 前材料永久不进入 Y；
10. 工作中用户消息完全不改写；
11. 普通 idle nudge 只有鼓励；
12. `suicide` 经 TODO-010 drain 后，过程 PERFECT 才自动启动 Host-owned Finality Reviewer；
13. REVISE 是 typed business outcome；
14. REVISE feedback 是 Reviewer canonical LWR（request-range bounded；四段标题）；
15. LWR 不含 Opening（`includeOpening=false` 路径）和 raw tool stream；Life LWR 仅四段；
16. Host 不结构化、摘要或改写反馈；
17. feedback 通过 SyntheticToml 按数据边界发送；
18. failure 继续同一 Life；
19. 每次 Finality retry 用新 request、fresh barrier；未毕业 ordinary Reviewer 可复用；Dedicated 首次 enlist 后 ordinary graduate（TODO-010）；
20. 双 PERFECT 因果证明保持不变；process PERFECT 不计入；
21. success 前重新验证当前 tree；
22. 第一次全确认只 Blessed，不 LifeCompleted；
23. 第二次 suicide 先 drain；允许后输出 rest in peace，用户答案逐字等于第二次 last_words；
24. Blessed 后仍受资源安全约束；Dedicated process duty 保留至 LifeCompleted；
25. 新 HumanRoot 只在 LifeCompleted 后触发 reawakening（再入 BlindPlan）；
26. XTrace 保持 append-only；
27. Crash matrix 全部有可执行恢复证明；
28. 所有状态来自 typed facts 与 projection，不来自故事文本；
29. REVISE 的 cohort 关闭与 `FinalityRejected` durable 落盘分离；前者不等待 sibling，后者必经 record-ready；
30. `FinalityRejected` 的 WorkRecordRef 来自同一 snapshot 的 canonical LWR；延迟 `BlogEntryCommitted`、waiter 崩溃与 Blogger abandonment 都不得留下缺少 `Chronicle` 的永久 record；
31. 零 `TodoWriteAccepted` 的 first unblessed suicide 不得进入 Finality（TODO-010）；
32. 生产决策不得读取 `WorkActivated` 决定工作、压缩或 Finality（GLORY-021）；
33. Gate A/B 在 Manager Life / Finality / commission-horizon-join 交界上可观察关闭。
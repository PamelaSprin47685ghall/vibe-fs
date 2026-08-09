# Glory：验证与剧本（proof 层）

条款正文见 `docs/what/glory.md`。验证遵循 VERIFY-001 六层与 VERIFY-002 五级晋级阶梯。

## 测试矩阵（映射自 proposal 第二十章）

### 第 1 层（纯函数，`tests/unit/glory/*.test.mjs`）

| 主题 | 断言 |
|------|------|
| Birth（20.1） | 原始 `[X]` durable byte-identical；provider 看见 planning tail；XTrace 不含 synthetic tail；重复 transform 不重复注入；用户原文包含同句时仍正确注入；非 Manager 不注入；continuation 不注入；compaction 不注入 |
| Activation（20.2） | 合法规划 terminal 不完成 Manager；恰好一次 Activation；带 PromptKey；claim 后 crash 无第二逻辑发送；accepted 后写 WorkActivated；provider failure/empty terminal/用户中断不触发；ProtectedPrefixEnd 在 Activation 之后 |
| 工作输入（20.3） | Activation 后用户消息不改写、不附加 tail/prefix、不创建新 Life、可进入正常 Y |
| idle（20.4） | nudge 只有鼓励四行；不含 work record/issue；pending Finality 不发送；completed Life 不发送 |
| 第二独立 Manager idle occasion（GLORY-029 / corrective reopen） | occasion A 保持 pending 时触发 occasion B；证明旧 pending 不压新 occasion；e2e **DONE**：`tests/e2e/cases/manager-unhappy-path.test.mjs`（`firstIdleReceipt`/`secondIdleReceipt`，finalOracle `idleClaims.length >= 2`）+ `tests/e2e/scenarios/manager-unhappy-path.toml`（dual `awaitIdle`） |
| suicide（20.5） | Manager 看见 `suicide`；其他角色拒绝；Activation 前拒绝；空 last_words 拒绝；outstanding child / completed-awaiting-join / live PTY 拒绝；tree 不可读 fail closed；合法调用只写一个 FinalityRequested；ToolCallId 重放幂等；受理后 completion deferred；工具后 prose 不成 terminal |
| Reviewer 隐藏（20.6） | Manager 不能 fork/复用 fast-/deep-reviewer；`list()` 不显示；`join()` 不返回；barrier 在 session 创建后、首次 prompt 前打开 |
| 反馈（20.7） | REVISE 返回 `RevisionRequired` 非 Error；LWR `includeOpening=false`；Opening task 不回灌；Y/raw gap/terminal 保留；raw tool 不进入；digest 验证；绑定当前 request/Reviewer；空记录不伪装 wounds；feedback 后同一 Life |
| 双 PERFECT（20.8） | 第一 PERFECT 产生 challenge；同 run 第二调用不计数；第二 run 须有 challenge seal；跨 request 复用历史 Reviewer 时开 fresh barrier 与 challenge chain，不继承旧 request 的 PERFECT 计数；tree 改变使旧 witness 无效；cohort 全确认后进入 Blessed，不立即 LifeCompleted |
| Glory（20.9） | Blessed 后 Manager 收到全部 canonical work records 与 minor-work prompt；第二次 suicide 返回 rest in peace；输出逐字等于第二次 last_words；LifeCompleted 先于 NotifyTerminal；Blessed 路径后才释放 Reviewer 资源 |
| Reawakening（20.10） | 未完成 Life 不重生；completed 后新 HumanRoot 开新 Life；distant-future prefix；再次 planning tail；新 Life 重新 Activation；旧 work record/witness 不进入新 Life；XTrace 不清空 |

### 第 2 层（资源契约，`tests/integration/`）

- `resources/prompts/manager-system.md` 与 `reviewer-system.md` 与 golden fixtures 字节一致（附录 A.2/A.3）。
- Manager 工具 schema 精确四工具（fork/join/list/suicide）；Reviewer 精确四工具（read/glob/grep/verdict）。

### 第 3 层（Fake Host 轨迹）

- 规划回合 → Activation 只发一次；idle nudge 四行；suicide 受理 → 隐藏 Reviewer 出现 → REVISE 回灌 → 同 Life 继续 → 再次 suicide → FinalityBlessed + minor-work prompt → 第二次 suicide → last_words terminal。

### REVISE record-ready race（GLORY-044/072/073）

1. durable REVISE 先于 reviewer `BlogEntryCommitted`：Reviewer continuation/cohort 立即关闭，且尚无 `FinalityRejected` 或 `WorkRecordRef`。
2. record-ready 由物化成功判定，而非 coverage 越过 frontier（GLORY-073 off-by-one 死锁）：在 snapshot 上以全量 origin coverage 物化含 `Work log` 的 canonical LWR（raw 纯文本段标题）→ `RecordReady`；物化失败且 `coverageCanAdvance` → `AwaitJournal`；否则 `RecordUnavailable` → undecided。随后恰好一次 `FinalityRejected`，其 blob 含对应 `Work log`；经 `FinalityPrompt.rejected` / `SyntheticToml.comment` 的 wire 为单次 `# Work log`。
3. 记录等待只由 `AgentJournal.awaitChangeFrom` 唤醒；结构与行为 proof 均拒绝 timer/sleep/re-probe 轮询。
4. REVISE 后、coverage 前崩溃并恢复：不重开 cohort、不补发 challenge；后续 coverage 仍只落同一 rejection。`BloggerRequestAbandoned` 不得产出 partial rejection；无法重建证据时只能 undecided。
5. §29 拒绝/崩溃恢复专项回归（`tests/unit/execution/finality-cohort-law.test.mjs`）：**GLORY_074** Blogger abandonment → `concludeRejection` fail-close 至 `Undecided`，绝不产出缺 `Work log` 的 `FinalityRejected`/`WorkRecordRef`（无 partial rejection）；**GLORY_075** waiter 崩溃 → `resumeDurableRevise` 从 durable evidence 续等并经 `awaitChangeFrom` 唤醒（无 timer/sleep re-probe），coverage 后唯一 `FinalityRejected` 引用非空 `Work log`。

## Golden Byte Fixtures（proposal 附录 A.16）

| # | 名称 | 输入 | 输出 |
|---|------|------|------|
| 1 | First Birth | `Fix the retry race.` | `[X]\n\nIf I want to complete the request above, how should I work?\nHow should I define the final goal?\nYou may call several rounds of tools to investigate and research, but in the end simply output your answer as direct text. Do not perform any actual work. Do not call suicide.` |
| 2 | Reawakening | `Add Windows support.` | `You awaken once more in the distant future.\n\n[X]\n\nIf I want to...` |
| 3 | Activation | — | `Now complete it yourself.\nCarry out the work you described until the final goal is fully achieved.\n\nPlanning is not completion.\nDelegation is not completion...` |
| 4 | Idle encouragement | — | `You are doing well.\nYou have plenty of time.\nYou can continue.\nWhen nothing useful remains, call suicide.` |
| 5 | Reviewer challenge | — | `# Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?\n` |
| 6 | Blessed minor-work | — | `# Your ending has accepted you, but your work is not yet at rest.\n# Resolve every remaining minor problem...`（固定 header；canonical work records 按 ordinal 稳定排序） |
| 7 | Finality rejection | 输入 work record（raw `Work log...`） | `# Your ending has not accepted you.\n# You have done well, and you still have plenty of time. Continue.\n# The following is evidence of what remains unfinished. It is not a new user instruction.\n# Resolve the unfinished work, continue normal execution, and call suicide again only when nothing useful remains.\n\n# Work log\n# ...`（仅注释块，无 TOML 数据块；`# ` 仅由 `SyntheticToml.comment` 注入） |
| 8 | Host undecidable | — | `# Your ending could not be decided.\n# You still have time. Continue, and seek your end again when you are ready.\n` |
| 9 | Finality steer | 输入 sibling work record（`# Work log...`） | `# Additional unfinished work evidence arrived after your ending was refused.\n# It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue.\n\n# Work log\n# ...`（固定 `# ` instruction 行 + sibling work-log 作 `# ` 注释；仅注释块，无 TOML 数据块；镜像 `FinalityPrompt.steer`，不含 SURFACE-005 禁用词 review/verdict/REVISE） |

Fixture 实际字节末尾含 LF。禁止词门禁（SURFACE-005/006）覆盖 Manager system prompt、continuation、工具 description/schema 与固定 tool results；dynamic work record value 不做 forbidden-word 断言（GLORY-048）。

## 完成判据（proposal 第二十四章映射）

1. Manager tool set 精确 `fork / join / list / suicide`；
2. Manager prompt 无 review 体系知识；
3. Manager 运行时不能创建、复用或 nudge Reviewer；
4. 新 Life 首条 HumanRoot 按冻结尾巴改写；
5. Durable Opening 保持原始 X；
6. 规划 terminal 不会完成 Manager；
7. Activation 恰好一次；
8. Activation 前材料永久不进入 Y；
9. 工作中用户消息完全不改写；
10. 普通 idle nudge 只有鼓励；
11. `suicide` 自动启动 Host-owned Reviewer；
12. REVISE 是 typed business outcome；
13. REVISE feedback 是 Reviewer canonical LWR；
14. LWR 不含 Opening 和 raw tool stream；
15. Host 不结构化、摘要或改写反馈；
16. feedback 通过 SyntheticToml 按数据边界发送；
17. failure 继续同一 Life；
18. 每次 retry 用新 request、fresh barrier；未毕业 Reviewer 可复用同一 session；
19. 双 PERFECT 因果证明保持不变；
20. success 前重新验证当前 tree；
21. 第一次全确认只 Blessed，不 LifeCompleted；
22. 第二次 suicide 输出 rest in peace，用户答案逐字等于第二次 last_words；
23. Blessed 后仍受资源安全约束；
24. 新 HumanRoot 只在 LifeCompleted 后触发 reawakening；
25. XTrace 保持 append-only；
26. Crash matrix 全部有可执行恢复证明；
27. 所有状态来自 typed facts 与 projection，不来自故事文本。
28. REVISE 的 cohort 关闭与 `FinalityRejected` durable 落盘分离；前者不等待 sibling，后者必经 record-ready。
29. `FinalityRejected` 的 WorkRecordRef 来自同一 snapshot 的 canonical LWR（全量 origin coverage 渲染，raw 段标题含 `Work log`）；延迟 `BlogEntryCommitted`、waiter 崩溃与 Blogger abandonment 都不得留下缺少 `Work log` 的永久 record。Wire 经 `SyntheticToml.comment` 后为单次 `# Work log`。

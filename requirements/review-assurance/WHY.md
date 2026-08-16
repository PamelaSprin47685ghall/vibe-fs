# WHY — review-assurance

## 不可替代的存在理由

`review-judgement` 回答了「PERFECT/REVISE **意味着什么**」。但一个 reviewer 输出了 judgement，**不等于系统已经证明这个 judgement 有资格被消费**。`review-assurance` 回答的问题是：

> 这个判断，针对的是当前被审对象吗？它消费了必要的 challenge 吗？它的证据完整到可以被下游使用吗？

如果 assurance 缺席，下列退化就会发生：

- **旧 tree 上的确认被消费**：rebase 或后续提交改变了被审代码，旧的双 PERFECT 仍被当作当前确认——系统消费的是针对已不存在对象的判断。
- **same-root 猜测假绿**：Host 重排消息时，仅凭 AuthorityRoot 或 PhysicalMessageId 相同就确认第二次 PERFECT——确认链不证明模型真的看过 challenge，只证明消息「看起来相关」。
- **外围 Map 补身份**：Guard 依赖外围可变 Map 补 witness 身份——恢复与并发 Job 会静默读到别人的确认或空确认。
- **「只有 verdict、没有 report」被当作可消费**：VerdictKnown 立即放行下一 TodoWrite——Manager 拿不到 canonical 报告，或 Host 用 terminal 摘要顶替 LWR。
- **基础设施失败伪装成业务 REVISE**：create/resume/assignment/LWR 物化失败被写成 REVISE——Manager 被派去「修复」系统故障，Journal 里留下伪造 settlement。
- **过程 PERFECT 冒充终末 witness**：process 一次判断被计入 terminal dual-PERFECT——终末 2N 代数被稀释，mission 在证据不足时结束。

`review-assurance` 存在的意义：**judgement 的消费资格必须由 bounded evidence（request-range 记录）、fresh witness（当前 tree/barrier）与因果确认（challenge 出现在 seal 里）建立**。

## RED 长什么样

满足以下任一情形，世界就是 RED：

1. 系统可以消费针对旧 tree / 错误 frontier / 未看 challenge / 缺 report 的 judgement；
2. 确认可以凭 AuthorityRoot / PhysicalMessageId 猜测成功，而不需要 seal 证明 challenge 被消费；
3. Guard 的确认依据依赖外围 Map 或存储的布尔标志，而不是自包含 witness 的派生谓词；
4. 在「仅有 verdict、报告未 record-ready」时提前 append 空壳 `TodoReviewConcluded`；
5. record-ready 判定用较晚 XTrace head 替换冻结 frontier、分两次读取 coverage 与 LWR、或用 timer/sleep/wall-clock 轮询；
6. 基础设施失败（create/resume/assignment/LWR 物化）被写成业务 PERFECT/REVISE；
7. process PERFECT 计入 terminal dual-PERFECT，或 process REVISE 被当成 `FinalityRejected` 事实。

## 为什么必须独立存在（Independent Change Test）

HANDOFF §6.4 / §7.6：

> 判断哲学可以整体重写，而 witness/finality 因果协议不变；反之亦然。

具体：你可以重写 dual-PERFECT 的 direct-CE 因果协议，judgement 的 discrimination 语义一行不改；你也可以重写 Role Law / Examiner's Ledger 的判断方向，typed physical challenge edge、attempt identity、tree invalidation、record-ready 代数一行不改。两个 failure meaning 完全不同：

- `review-judgement` RED = reviewer 可以凭表演/checklist/偏好决定 accept/reject；
- `review-assurance` RED = 系统可以消费针对旧 tree / 未看 challenge / 缺报告 的 judgement。

## 与相邻包的边界（为什么这些不归我）

| 邻近事实 | 真正的 owner | 为什么不归 assurance |
|---|---|---|
| PERFECT/REVISE 的意义、materiality、checklist 禁令 | `review-judgement` | 判断哲学；assurance 只消费判断 |
| 1:1 Rk 派生、lag-1 节拍、CurrentObligations | `obligation-ledger` | 账本规则；assurance 只管「何时可消费」 |
| 终末 cohort / rejection / blessing / rest / drain | `finality` | 不可逆结束资格；assurance 提供证据原语 |
| canonical LWR 的表示/物化/三标题 | `work-record` | 记录表示；assurance 只拥有「record-ready 才可消费」与 request 绑定 |
| Host 因果读的传输侧（physical execution binding / PromptKey acceptance） | `host-boundary` / `interaction-authority` | 传输能力；本包只消费 typed physical identity 并 fail closed |
| 等待的因果可观测性（awaitChangeFrom） | `causal-wait` | 等待机制；「record-ready 等待必须事件驱动」的 review 用法是本包 |
| tool 语法红字分类 | `capability-enforcement` | 三态失败分型的工具侧；「infra 不伪装 REVISE」的 review 侧是本包 |
| infra fatal fail-fast / 崩溃恢复 | `host-boundary` / `crash-reconciliation` | 系统故障处置；本包只要求不伪 REVISE、义务保持 outstanding |

## 历史教训（考古）

- 历史 why/review 条款：单 PERFECT 可被模型随口同意。旧实现曾用双 PERFECT + provider-input seal 证明 challenge 消费；本轮已废弃该文本/digest 推断，改由 direct CE + PromptKey→PhysicalUserMessageId 的 typed physical edge 建立因果。Witness 自包含（拒外围 Map）；tree 变化作废（审的是代码状态不是 Session 情绪）。
- 历史 why/review「VerdictKnown 与 ConsumableReview 分型」：把「只有判断、尚无 report」挤进同一个 `TodoReviewConcluded`，恢复路径无法区分「已可 settle」与「已可展示报告」。
- 历史 why/review「为何基础设施失败不是 REVISE」：伪装成 REVISE 会触发错误 semantic merge、推进虚假 ConsumableReview，让 Manager 去修系统故障。
- 历史 why/review「为何禁止 wall-clock polling」：sleep/timer 把 Journal 因果等待退化成运气；本地 waiter 崩溃后无法从 durable facts 重建同一等待。
- 历史 change（fix-revise）（GARBAGE transcript 但考古高价值）：Gap A 曾暴露 record-ready 的 fail-closed 回归——Blogger 放弃时 `recordReadiness` 必须判 `RecordUnavailable` 并 fail-close 至 `FinalityUndecided`，绝不产生缺 `# Work log` 的 `FinalityRejected`；waiter 崩溃必须以相同 ToolCallId 从 durable evidence 续等（`awaitChangeFrom`，无 timerTask/sleep re-probe）。
- 历史 change（magic-todo）：两段式事实分型 `VerdictKnown(k)` vs `TodoReviewConcluded(k) ≡ ConsumableReview(k)`；`ensureReview` 可重入；Rk obligation pending 看的是缺失 Concluded 而非缺失 VerdictKnown。

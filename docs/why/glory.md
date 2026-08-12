# Glory：设计理由与被拒方案（why 层）

条款正文见 `docs/what/glory.md`。本文记录 GLORY 设计的关键理由与裁决中被拒绝的方向，供未来变更回溯。Magic Todo 过程评审与 checkpoint 代数的正式理由见 `docs/why/todo.md` 与 `TODO-*`；此处只记录与 GLORY Life/Finality 交界的裁决。

## 核心假设：BlindPlan 交托取代 Activation 阶段机

被拒方案：保留 planning-only Birth → `ManagerWorkActivation` → Labor，并在 Planning/Working 之间切换 system prompt。否决：两套阶段机叠加复杂度；prompt 切换破坏 prefix cache 与身份连续性，且开场即泄露「你携带一个任务」（TODO-001；系统 prompt 稳定性）。

正式路径：Manager = BlindPlan Opening。Pre-T1 为 Planning Table（替他人规划）；第一次 accepted `todowrite` = T1 commitment；canonical T1 result 揭示「路是你的」。同一 Life 内 system prompt byte-identical——T1 / fallback / review / reanchor / Strength 均不改它。交托发生在 conversation + tool result，不发生在 office 身份切换。

Opening 仍永久 raw；Blogger floor 由结构性 `WorkRecordStart` 承担，不是 Activation/`WorkActivated`（TODO-001；GLORY-006/023）。历史「伪装规划 tail + Activation 命令」可继续 decode，但不得再作为生产资格门（GLORY-018/021）。

## 为什么 Manager 不能知道隐藏 review 机制

旧 prompt 把 review 显式化为 checklist 的最后一步（调查→修改→测试→review→返回），Manager 会在工作尚未收敛时机械执行最后一项。完成顺序反转：Manager 主动请求终结（`suicide`）→ Host 才开始隐藏 Finality 审查。Reviewer 存在本身、双 PERFECT、barrier、witness、2N 全部对 Manager 隐藏（GLORY-002），把终局质量门从「可勾选的步骤」变成「不可见的命运」。

**窄例外（GLORY-030 / TODO-013）**：Todo Checkpoint 过程评审的 outcome（PERFECT/REVISE）与 concrete report 必须对 Manager 可见，否则 lag-1 消费与修正无法进行；该例外不得泄漏 dedicated reviewer 身份、session、barrier、witness 或 Finality 编排。

## 为什么失败反馈是完整 canonical LWR 而不是结构化 findings

被拒方案：结构化 `FinalityFinding` schema，或任何 per-role fixed Closing report（`### Summary` / `### Files Changed` / …）。拒绝理由：Reviewer 已拥有完整工作记录；再抽结构化 issue 丢失推理关系、引入第二事实源、需要 parser、产生摘要漂移、让 Host 替 Reviewer 解释（GLORY-004/049/050；TODO-008）。Closing report = prose claim，不是 verdict，也不是固定字段义务——约束诚实，不约束骨架。完整 LWR（Y frames + raw gap + terminal，不含 Opening；request-range bounded）在证据完整性上无损，且复用现有 materializer。

## 为什么失败 nudge 必须组合鼓励与精确证据

被拒方案：完全模糊的失败 nudge（只有 "You are doing very well. You have plenty of time."）。否决：Manager 不知道错在哪，只会重复已有工作、无意义 fork、再次 suicide，形成「失败—鼓励—重试」的无限循环。普通 idle nudge 保持纯鼓励（GLORY-005/029），而 FinalityRejected / process REVISE 必须携带 Reviewer 的精确证据。

## 为什么字面工具名 `suicide`

实测效果不错：工具描述故意模糊（"End your life when your task is complete."），不解释 Host 会执行什么、终结可能被拒绝、自动 Reviewer、双 PERFECT、tree hash、barrier、witness。悲壮感让 Manager 把「调用 suicide」当作慎重且不可逆的行为，抑制提前结束。内部模块仍用 `Finality` 语义命名（GLORY-008/035），不依赖该词的文本语义。

## 为什么 REVISE 是正常业务结果

`RevisionRequired` 从「执行错误」提升为正常业务结果；只有 Reviewer 启动失败、Journal 失败、tree 不可读等才是基础设施错误（GLORY-044/056）。区分二者的意义：wounds 是 Manager 该修复的证据；基础设施失败是系统缺陷，不得伪装成「工作不完整」。

## 为什么 REVISE 立即关闭 Reviewer、却不立即写拒绝事实

REVISE 已是对本 request 的终局审查判断，继续发 challenge、等待 sibling 或让 cohort 产生下一次效果只会制造与该判断竞争的事实，故必须立即关闭。`FinalityRejected` 则不是 verdict 的别名：它永久引用 canonical LWR；若 Blogger 尚未以 `BlogEntryCommitted` 覆盖 reviewer terminal frontier，立即物化会把临时空 frame 误写成永久证据，之后任何补写都无法修复 blob（GLORY-044/072）。

被拒方向：以 sleep、timer 或当前 XTrace head 等待/重试。否决：前者把 Journal 因果等待退化为轮询；后者会在崩溃或后续活动后改变原 REVISE 的记录目标。只接受 durable frontier、同一 snapshot 的 coverage 与 materialization；本地 waiter 丢失后由 durable evidence 重建（GLORY-073）。

## 为什么 suicide 必须抽干过程评审且禁止零 checkpoint 绕过

每个 `TodoWriteAccepted` 派生新的 process-review obligation；永远可能存在「最新 Rk 尚未被下一 todowrite 消费」。禁止要求「再调用一次 todowrite flush」（会创造 R(k+1) 无限后移）。唯一 tail drain 是 `suicide`（TODO-010）。first unblessed 路径至少一次 `TodoWriteAccepted`，否则 fail closed——这证明 Manager 进入过必需 checkpoint 协议，**不是**机械要求 todos 全 completed。Blessed 后的终末 suicide 同样先抽干未消费 ConsumableReview（GLORY-062）。

## 为什么 Dedicated 普通 graduate 却保留 process duty

被拒方案：Dedicated 永不 graduate / 每轮 Finality 强制回流。否决：与既有 Finality 毕业语义冲突，且过度推导用户「dedicated 也会加入终末 2N」的要求。正式保守默认：首次 terminal Finality ordinary enlist，其后 ordinary graduate；process-review 物理 session 仍保留到 `LifeCompleted`（TODO-008/010；GLORY-003/045）。process PERFECT 不计入 terminal dual-PERFECT。

## 为什么复用现有 Reviewer session 与 ReviewRunner

被拒方案：私有 `FinalityReviewHandleId`。否决：复用现有 Reviewer session 与 ReviewRunner，只改变所有权与可见性（GLORY-042/045）。自动 Reviewer 是隐藏的 Host-owned session，不进入 Manager 的 handle 面；Finality 每次 request 用 fresh barrier，保证终局证明只描述当前 tree 和当前请求。Dedicated process reviewer 可跨 checkpoint 复用同一 physical session，但每次报告仍 request-range bounded（TODO-008/010）。

## 为什么成功输出逐字等于 last_words

被拒方案：成功后再让 Manager 写总结或追加系统成功文本。否决：成功后再唤醒 Manager 会稀释叙事、引入第二轮修改风险；`last_words` 是 Manager 深思后的最终答案，Host 任何附加文本都会破坏「死于荣耀」的完整性（GLORY-061/062）。

## 为什么 XTrace 保持 append-only 且不引入多 Opening

被拒方向：为多 Life 立即改造通用 XTrace 为多 Opening。否决：非 Manager 角色不需要多 Opening；清空 XTrace 会破坏 append-only 不变量。通用 XTrace 继续 append semantic parts，ManagerLifecycle 单独记录每个 Life 的 opening/terminal 与 cursor range（GLORY-066/067），按 range 物化。OpeningMaterial = 该 range 上 preserved 区间，禁止 `OpeningPromptRaw` 重建。

## 其他被拒方向

- 文本判断生命周期（搜索 suicide/glory/distant future 推导状态）：拒绝，状态只能来自 typed facts（GLORY-009；TODO-012）。
- 自动清洗 Reviewer 工作记录中的叙事词：拒绝，证据完整性优先于词汇纯度（GLORY-048）。
- 只发送 Reviewer terminal / 只发送纯 Y frames：拒绝，都存在信息缺口。
- Manager 手动 fork Reviewer：拒绝，会把质量门重新变成显式 checklist（GLORY-002/031）。
- 把 `verdict`/`judge` 重命名为 `suicide`：拒绝，`judge` 属于 Reviewer、`suicide` 属于 Manager，因果身份不同。
- 保留生产 `ManagerWorkActivation`/`WorkActivated` 资格门或 Activation 切换 system prompt：拒绝（TODO-001；GLORY-018）。
- 用 RecordCoverage 证明 prefix 可替换或用 PrefixCoverage 填 LWR gap：拒绝（TODO-008/009；GLORY-050）。
- 零 `TodoWriteAccepted` 的 first unblessed suicide 进 Finality：拒绝（TODO-010）。
- 压缩 Opening / 把 Opening 纳入 Blogger/Y：拒绝——章程不可缩短。

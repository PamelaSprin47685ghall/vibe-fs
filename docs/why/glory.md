# Glory：设计理由与被拒方案（why 层）

条款正文见 `docs/what/glory.md`。本文记录 GLORY 设计的关键理由与裁决中被拒绝的方向，供未来变更回溯。

## 核心假设：伪装规划能改善诚实规划

把初始任务 `[X]` 改写为「如果我想要完成以上需求，我需要如何工作？我应该如何设定最终的目标？」会让模型倾向：诚实描述完整工作量、说明必要验证、明确最终完成状态；不会因「自己马上要执行」而偷偷缩小范围；不把困难步骤省略为「后续可以做」。随后 Host 发送 Activation（"Now complete it yourself."），模型必须执行自己刚刚给出的完整方案。这是整个叙事机制的心理基础。

## 为什么 Manager 不能知道 review

旧 prompt 把 review 显式化为 checklist 的最后一步（调查→修改→测试→review→返回），Manager 会在工作尚未收敛时机械执行最后一项。新协议把完成顺序反转：Manager 主动请求终结（`suicide`）→ Host 才开始隐藏审查。Reviewer 存在本身、双 PERFECT、barrier、witness 全部对 Manager 隐藏（GLORY-002），把质量门从「可勾选的步骤」变成「不可见的命运」。

## 为什么失败反馈是完整 canonical LWR 而不是结构化 findings

被拒方案：结构化 `FinalityFinding` schema。拒绝理由：Reviewer 已经拥有完整工作记录；再抽取结构化 issue 会丢失推理关系与上下文、引入第二事实源、需要额外 parser、产生摘要漂移、让 Host 替 Reviewer 解释（GLORY-004/049/050）。完整 LWR（Y frames + raw gap + terminal，不含 Opening）在证据完整性上无损，且复用现有 materializer，不需要第二套反馈通道。

## 为什么失败 nudge 必须组合鼓励与精确证据

被拒方案：完全模糊的失败 nudge（只有 "You are doing very well. You have plenty of time."）。否决：Manager 不知道错在哪，只会重复已有工作、无意义 fork、再次 suicide，形成「失败—鼓励—重试」的无限循环。普通 idle nudge 保持纯鼓励（GLORY-005/029），而 FinalityRejected 必须携带 Reviewer 的精确证据。

## 为什么字面工具名 `suicide`

实测效果不错：工具描述故意模糊（"End your life when your task is complete."），不解释 Host 会执行什么、终结可能被拒绝、自动 Reviewer、双 PERFECT、tree hash、barrier、witness（附录 A.8.4）。悲壮感让 Manager 把「调用 suicide」当作慎重且不可逆的行为，抑制提前结束。内部模块仍用 `Finality` 语义命名（GLORY-008/035），不依赖该词的文本语义。

## 为什么 REVISE 是正常业务结果

`RevisionRequired` 从「执行错误」提升为正常业务结果；只有 Reviewer 启动失败、Journal 失败、tree 不可读等才是基础设施错误（GLORY-044/056）。区分二者的意义：wounds 是 Manager 该修复的证据；基础设施失败是系统缺陷，不得伪装成「工作不完整」。

## 为什么 REVISE 立即关闭 Reviewer、却不立即写拒绝事实

REVISE 已是对本 request 的终局审查判断，继续发 challenge、等待 sibling 或让 cohort 产生下一次效果只会制造与该判断竞争的事实，故必须立即关闭。`FinalityRejected` 则不是 verdict 的别名：它永久引用 canonical LWR；若 Blogger 尚未以 `BlogEntryCommitted` 覆盖 reviewer terminal frontier，立即物化会把临时空 frame 误写成永久证据，之后任何补写都无法修复 blob（GLORY-044/072）。

被拒方向：以 sleep、timer 或当前 XTrace head 等待/重试。否决：前者把 Journal 因果等待退化为轮询；后者会在崩溃或后续活动后改变原 REVISE 的记录目标。只接受 durable frontier、同一 snapshot 的 coverage 与 materialization；本地 waiter 丢失后由 durable evidence 重建（GLORY-073）。

## 为什么复用现有 Reviewer session 与 ReviewRunner

被拒方案：私有 `FinalityReviewHandleId`。否决：复用现有 Reviewer session 与 ReviewRunner，只改变所有权与可见性（GLORY-042/045）。自动 Reviewer 是隐藏的 Host-owned session，不进入 Manager 的 handle 面；每次 suicide 用全新 session/barrier，保证工作记录只描述当前 tree 和当前请求。

## 为什么成功输出逐字等于 last_words

被拒方案：成功后再让 Manager 写总结或追加系统成功文本。否决：成功后再唤醒 Manager 会稀释叙事、引入第二轮修改风险；`last_words` 是 Manager 深思后的最终答案，Host 任何附加文本都会破坏「死于荣耀」的完整性（GLORY-061/062）。

## 为什么 XTrace 保持 append-only 且不引入多 Opening

被拒方向：为多 Life 立即改造通用 XTrace 为多 Opening。否决：非 Manager 角色不需要多 Opening；清空 XTrace 会破坏 append-only 不变量。通用 XTrace 继续 append semantic parts，ManagerLifecycle 单独记录每个 Life 的 opening/terminal 与 cursor range（GLORY-066/067），按 range 物化。

## 其他被拒方向

- 文本判断生命周期（搜索 suicide/glory/distant future 推导状态）：拒绝，状态只能来自 typed facts（GLORY-009/023.6）。
- 自动清洗 Reviewer 工作记录中的叙事词：拒绝，证据完整性优先于词汇纯度（GLORY-048/23.7）。
- 只发送 Reviewer terminal / 只发送纯 Y frames：拒绝，都存在信息缺口（23.2/23.3）。
- Manager 手动 fork Reviewer：拒绝，会把质量门重新变成显式 checklist（23.4）。
- 把 `verdict` 重命名为 `suicide`：拒绝，`verdict` 属于 Reviewer、`suicide` 属于 Manager，因果身份不同（23.5）。

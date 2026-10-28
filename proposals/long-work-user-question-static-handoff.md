# 长程任务不停机与显式用户询问

> 2026-10-15 的静态交接记录已收口：当时的改动已提交并经后续回归（见 git 与 `requirements/work/` 测试），
> 「未运行/待验收」的过程账不再维护。本文只留现行语义与代码落点。

## 修订的语义

目标义务独立于 provider 回合。Manager/Plan 未完成目标时，自然 stop、空回复、进度汇报都不能充当完成证据；必须继续当前任务。生命周期收尾走 `review`（清空义务账且资源不再阻塞即结案），不再走已退役的 `deliver`。真正缺用户信息时，只有成功的 `ask(question)` 能产生显式等待。用户明确取消与基础设施失败仍有自己的出口，不构建不可取消的自动循环。

根 `AGENTS.md` 写明上述公理与推论。work-027 改为禁止静默停止，work-028 改为 `devops` 调查，新增 work-049 约束人类问题、占位回执、中断、恢复与幂等。office-capability、managed-session-lifecycle、索引和 Manager/Plan 双语提示同步调整。历史提案里“Plan 可以空停”的描述由本次决议取代，不能继续作为现行依据。

## 代码改动

### 1. 自然停机的业务门

`Composition/Turn/OrdinaryTurnWorkflow.fs` 在发布自然完成前检查长程工作：显式人类等待、旧权威/旧提问者回执及已证明退休/交付不再触发修复；仍有义务的自然停止走既有 idle gate 提醒。provider 错误、length 截断和用户中止仍走既有专门路径，不把所有终态都改成继续。

Manager 有单独的 idle 分派，不会自动经过上述分支，因此 `Mission/Manager/Workflow.fs` 同步处理等待/退休，并保留实际评审、工作、收尾阶段提示。此前缺工作投影时直接跳过 nudge；现在缺失不当成完成，回落到明确的任务核对提醒。发送失败或明确未发出会报告错误，已被新输入撤销的 idle 许可仍是无副作用退出。提醒复用已有精确 provider-run 去重，不新增“一次提醒后永久放行”的预算。

`CompletedTurnClassifier.isPlanEmptyStop` 及调用处的豁免删除。`InteractionRepairWorkflow.repairUnfinishedWork` 使用新的双语长程提醒，说明为什么不能停、接下来应核对什么；Manager 提示 `join` 是临时等待工具。

### 2. 两个名字、两种能力

对外 `devops(intent)` 继续原有固定 DevOps 同步调查，Plan 独占；对外 `ask(question)` 是 Manager/Plan 询问用户。新增独立 `DevOpsConsultation` permission，静态可见性、运行时门禁、投机调查分类及枚举桥同步更新。新 ask 不创建 DevOps，也没有保留 `ask(intent)` 的工具别名。

旧 Plan 内部 `executeAsk` / `AskResult` 仍作为既有调查实现及测试边界的标识，只有 ToolRegistry 对外名称改为 devops；它们不是新人类询问的执行路径。没有因为改名复制第二套调查流程。

### 3. 等待不等于结束

PromptAuthority 新增 `UserQuestionAsked` / `UserQuestionAnswered` 事实及其投影。问题绑定逻辑运行、权威根、源物理用户消息、provider run、tool call 与原问题。`UserQuestionJournal` 只通过现有 AgentJournal 追加/读取，未建立新数据库或修改 wanxiangli 的 Work 词表。原 PromptFactCodec 的自动 union 编解码承接新类型；新词表与旧二进制的互读能力尚未验收，不宣称旧版本兼容。

ToolRuntimeScope 等待真实问题通知确认，再登记等待并返回占位回执。同会话并发发送有本地入场保护；已登记的精确重放不重发通知。Host 界面通过现有 `tui.showToast` 呈现问题，原问题也保留在工具参数及 durable 事实里；这里没有伪造一条人类消息。

工具结果不能在 interrupt 时丢失：中断放在实际 `message.part.updated` 的 completed 工具部件上，必须匹配 session、provider message identity 和 callId。未完成部件、别的调用、别的 provider run、用户回复后的旧事件都不能中断新执行；不使用 sleep/setTimeout 猜测保存顺序。

**根会话的旧硬门必须一起处理。** `InjectedSessionPort.InterruptAttempt` 原先拒绝普通用户根会话。现在宿主组装传入从同一 durable 等待投影读取的窄许可，只有已登记等待时开放 attempt interrupt。普通 root `AbortSession` 仍拒绝；这条路径不取消已排队的用户输入、不递归清理 children、不关闭 logical run。不能简单把所有 root interrupt 放开。

等待期间，插件工具执行门与 Host-native tool-before 门都拒绝新工作。用户回复沿既有物理输入准入进入；只有已记录的真实 HumanRoot/HumanMessage 输入才能解除等待，旧源输入和内部 continuation 不算答案。仍保留旧提问者身份，防止迟到终态去关闭新一轮。

## 行为要点（真实 Host）

对应测试：`requirements/work/tests/`（021/024/027/028/030/031/032/049）、office-capability、dispatch-protocol-002、managed-session-lifecycle 与 interaction-authority 的关联用例。当时的轮次计数不再维护。

1. Manager 与 Plan 都能在没有后台任务、只有合格散文的自然 stop 后被催促；下一次偷停仍催，同一次终态不重复发。
2. 问题可见、工具结果实际保存后才 interrupt，LLM 不再自动出下一条工作请求；取消的是当前输出，不是用户排队输入、子任务或逻辑运行。
3. 用户在通知、问题落盘、工具完成、中断期间回复的交错，以及 deferred human admission，均不会丢答案、停错新 attempt 或永久留在等待状态。
4. 进程在通知已发但等待尚未确认、等待已落盘但工具完成尚未观察、工具完成但 interrupt 回执未知等位置中断后的恢复。当前改动没有宣称通知的跨进程 exactly-once，也没有自动重发未知外部效果。
5. 提醒发送被新输入撤销与真正发送故障分开；真正的结案、用户取消和已有失败恢复不会被当作偷停。

本文件不承载运行凭证；运行事实以 git 提交与上述测试为准。

# delegation — 为什么必须独立存在

## 不可替代的存在理由

系统里有多种「把工作交给别人」的机制：Manager `fork` 使命内开证人、Orchestrator `commission`
独立道路、`inspect`/`establish-behavior`/`repair-behavior` 同步取证/修复、NEEDHELP 求助 consultation。
它们共享同一个 WHY：**语义工作转交另一 participant 时，authority、charge、owner 与返回后果必须明确**。

没有这个包，以下任一事故都会发生：

1. **机器拓扑冒充业务委托**：caller 用 `agent_id` / `job_id` / `worktree` / `reused=true` 描述委托，
   模型被迫当 union decoder，把「创建独立工作」「续做同一路」「同步取证」混成一种模糊 act。
2. **委托暗中改变 authority/personhood**：续做某 person 却悄悄换成另一个 model / binding，或
   consultation 的 advice 被当成 replacement assignment，改变 owner 的 charge。
3. **返回后果没有边界**：一次委托返回一整份 session 历史或字段式 DTO，caller 无法判断它改变了什么认识。
4. **runtime 拓扑泄漏**：SessionId/AgentId/worktree path 出现在 provider 面，业务语义被物理事实污染。

RED 判定：caller 无法从业务语义区分「创建独立工作」「续做同一路」「同步取证」，或委托暗中改变
authority/personhood。此时世界 RED。

## 独立变化测试

把 sync delegation 从 dedicated reusable session 改成 one-shot invocation，而 returned consequence
contract（bounded WorkRecord、canonical/sibling 分型）不变——本包 WHAT 全部不动。反之，把
`fork`/`commission`/`inspect` 工具改名为其它动词，只要语义合同不变，本包 WHAT 也不动（工具名 = HOW）。

## 历史失败模式（为什么现在是这个形状）

- **fork-manager DTO**（`changes/completed/orchestrator-e2e-timeout.md` 考古、`docs/why/orchestrator.md` 备选节）：
  旧 `agent=fast-manager|job_id` + `worktree` + `reused=true` 把机器拓扑当世界语言。拒因：双轨期间旧面与新面并存，
  测试与 prompt 永远对齐泄漏面；一次断，旧符号删。
- **`return` 双 await**（`docs/why/execution.md` SyncDelegate 节；`changes/completed/universal.md` §14–16）：
  旧路径 specialist 调 `return(A)` resolve `Returned`，再等 `TurnCompleted`——「结束协议」伪装成工具能力，
  污染 self-model，并逼调用方解码双通道。GrandRewrite 删除独立 `return`，选 ordinary completion 物化
  bounded WorkRecord。
- **Student–Teacher**（`changes/completed/universal.md`、`ce-student-teacher-collapse.md`）：
  生命周期 cell + handoff + pending slot 合并把调用栈位置藏进可变字段，terminal handler 必须猜下一步。
  已 clean-break 删除；SyncDelegate 是后继，不继承该拓扑。
- **microtask 猜批次**（`docs/why/execution.md` Sync batch 备选节）：用「先 drain 一次、创建 child 后再
  drain」的时间窗口拼批次，或把同一 assistant message 的并发 sync calls 当多轮队列。拒因：批次成员应由
  Host 已完成的 assistant message 直接给出（ProviderRun + role + tool-call 顺序）。
- **NEEDHELP 当失败**（`changes/completed/increase-strength.md` §3.1/§6）：把求助当 provider failure /
  fallback / 羞辱会走错恢复分支。consultation 是正常协作，是真实 child 委托（AGENT-031 / HOST-027）。
- **用户消息唤醒 join 被误当 authority**（`changes/completed/corrective.md`）：唤醒只结束当前 wait，
  不创建 HumanRoot / LogicalRun，不 cancel child。低权限 pulse 与 authority transition 是两类事件。

## 与相邻包的边界

- authority/personhood 定义本身 → `participant-identity`；office consequence → `office-capability`。
- session 创建/复用/取消/retire/级联 → `managed-session-lifecycle`。
- bounded WorkRecord 的物化格式与三段标题 → `work-record`。
- 机器信息准入过滤（什么能穿过 horizon）→ `participant-horizon`。
- 委托「发给 provider 的字节怎么渲染」→ `provider-projection`。

本包只拥有「转交语义」本身：charge 是什么、谁拥有、允许什么后果、返回什么、怎么区分道路与续做。

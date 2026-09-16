# delegation — HOW

## 架构机制

### 委托接口分流与权能门禁

DELEG-020 约束：委托语义不依赖当前工具名字面值（`fork`、`commission`、`resume` 等），改名不动 WHAT 语义定义。

系统定义三类委托途径，由角色权能门禁严格限制：

1. **异步见证与续做委托（`fork` / `resume`）**：由 Manager 在使命内部调用。
   - `fork`：必填 calling（仅限 `engineer`），创建具有独立 Byname 的新 Engineer 子执行者；
   - `resume`：按 Byname 续做既有子执行者（包含 Engineer 既有道路以及 Manager 道路唯一绑定的固定 DevOps），复用其历史与环境，传入 calling 被类型化拒绝。
   - 两者均支持附加历史背景（attachment）与建议性工具调用估算。
2. **独立道路委托（`commission`）**：由 Orchestrator 调用，负责开启或续做独立集成道路，支持多道路并行推进。
3. **Sphinx 程序内部同步调研（`SyncDelegate`）**：由 Sphinx 程序在需要语义事实时同步调用只读 Engineer 调研。调用方等待本次结果，只读 Engineer 仅调研现有本地事实并返回程序调用点，不具备写权限、命令执行、DevOps 差遣、Fission 或递归探究权限。

### Reusable work unit 与 resume 语义

- 业务控制流只存在于 F# CE：`prepareHandoff → dispatch → await own completion → checkpointCompletedHandoff`。禁止 `Stage/Phase/ActiveWorkUnit`、显式 transition API 等第二运行时或 durable program counter。
- durable truth 只记录已经发生的事实：某个 logical route 的一次已完成 handoff 确实让 callee 看到了 parent XTrace 截止到哪个 cursor。projection 仅把这些 completion facts 积分成 `latestDeliveredThrough(route)`；它不拥有执行位置。
- 新调用从 `latestDeliveredThrough(route)` 到当前 parent XTrace head 物化 delta；route 首次调用取完整 parent LWR。logical route = fork/resume Byname 或 caller scope 下的 dedicated SyncDelegate role，绝不以 physical child `SessionId` 作为连续性身份。
- invocation-local 的 child start cursor、expected Authority Root、waiter/subscription 属于物理 correlation resource，可跨 callback 保存；它们不得 durable 化为 workflow stage。
- fork/resume 必须同步等待接收方确切完成身份校验与接收持久化（AcceptedAssignment），异步执行工作；仅有 transport receipt（Submitted）未获接收确认时不得宣布承接。真实消息身份在接收方确立时绑定 Authority Root，且仅有已确认接收的 assignment 才发布为可 join 任务，工作完成仍交 join/horizon。
- 首 prompt 的 Host acceptance 若为 unknown，`PromptAuthority` 的 durable Pending claim 是唯一恢复所有者：fork run 保持 Active、terminal observer 保持绑定、不得合成 `HandleCompleted`、不得自动重发。调用面返回明确的“可能已接受”后果，阻止调用方用第二个 child 猜测性补偿。
- 新 charge 遇到仍在运行的同 route 调用直接拒绝。Busy nudge 只服务同一 LogicalRun 的内部 continuation，彻底退出 assignment 工具路径。

### 禁止 Engineer→DevOps 差遣

- Engineer 专注于本地文件调查与源码实现/测试编写，不具备真实执行与 DevOps 调度工具。本次任务完成后直接向 Manager 返回。
- DevOps 执行后可自行完成非架构级局部修复并重新验证，向 Manager 汇报运行事实与改动；DevOps 不创建或差遣其他工程子代理。
- 需要将实现交由 DevOps 验证或根据 DevOps 报告调整工程任务时，均由 Manager 统一组织。

## GAP

- `DELEG-032`（OPEN）：Engineer 完成即返回 Manager、禁止 Engineer→DevOps 差遣与 DevOps 差遣其他子代理，待 P1/P2/P3 角色权限与工具链收拢后闭合。
- `sync-stream-seal`（CLOSED）：流式环境中多个 Inspector 并发调用时，单个工具调用无法在执行时刻预知本轮是否还有后续 sibling 工具调用到达。现由 `InspectorTool` 在流式步骤中以瞬时接受占位方式返回（解除流式死锁与 premature batch 冲突），并将待检任务登记到 `SyncDelegateBatching` 延期队列中；在宿主进入下一轮请求前，由 `PluginTransforms`（`experimental.chat.messages.transform` 管道）一次性收拢本轮积攒的所有 Inspector charges，以单一聚合 Prompt 触发一次性 Inspector 子会话调度，取得权威 `WorkRecord` 与 sibling 引用并存入持久化替换字典；在每次 Transform 执行时扫描 `messages` 就地替换历史 tool 结果，保证上下文与真实子会话执行完全满足 DELEG-008/012，全量单元测试与 56 步 Long Stroke G2 E2E 真实 Host 验证全绿。
- `GAP-027`（CLOSED）：旧 reusable handoff 以 physical child `SessionId` 持有 cursor，并在 prompt 已 dispatch 后追加可失败 bookkeeping；旧 sticky terminal 还能跨 invocation 重放，fork idle reuse 又会立即返回旧/全生命周期结果，active new charge 还会混入 `BusyAgentNudge`。现已收口为 direct F# CE：logical-route frontier 只由 completed-handoff fact 推导；same-road fork/SyncDelegate 都执行 `prepare delta → dispatch → await own causal completion → bounded callee LWR → checkpoint`；fresh-only terminal observation 与 Authority Root 共同阻断上一轮 Completed/Failed；active assignment 明确拒绝；HostForkRuntime 的 bounded WorkRecord projector 为必需 capability，不能再构造“可完成但无 invocation delta”的 runtime。真实 fork tool 与 inspector/coder reuse 回归均已覆盖，authoritative runner 3405/3405 green。

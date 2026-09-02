# delegation — WHY

## 领域价值与核心矛盾

在多角色协作系统中，工作必须能够从一个 participant 转交至另一个 participant（例如 Manager 启动 witness、Orchestrator 委托独立工作道路、同步取证与行为修复、以及求助咨询）。

转交的核心矛盾在于：**业务意图上的职责交接必须与底层的机器执行拓扑正交**。若将执行拓扑（SessionId、AgentId、worktree 路径、复用标志等）混入业务协议，模型将被迫充当物理拓扑的解码器，模糊任务所有权与权能边界。

## 核心不变量

1. **按后果委任（Entrust by Consequence）**：委任必须明确 charge（语义任务）、office（权能后果）、logical owner（任务归属）以及 bounded 返回后果。
2. **拓扑隔离**：机器身份与会话拓扑严禁穿透 horizon 污染业务协议面。
3. **独立道路与续做严格区分**：独立任务与同一道路的连续执行在语义上正交，不因工作量或阶段演进而混淆。
4. **返回是证据而非权力转移**：委托返回的 WorkRecord 仅更新调用方的认知状态，绝不自动赋予调用方额外权能或解除其既定义务。
5. **单向载荷与信封隔离**：父向子传递背景必须作为只读数据字段，子向父交付结果必须作为 entry-local 证据，严禁逆向污染。
6. **复用的是 participant，不是物理 session 或上一轮执行态**：同一 Byname / dedicated role 可以连续承接多个 work unit；每个新 charge 都拥有独立的输入窗口、执行身份与完成证据。
7. **已发生的 effect 不得被后置 bookkeeping 否认**：一旦某个 work unit 已经 durable admission 并进入物理 dispatch，后续 projection/affinity/frontier 写入失败只能进入明确的失败或 reconciliation 语义，绝不能把调用结果降格成“没有放置”。
8. **Contract 不携带执行拓扑**：业务 consumer 只编译 delegation command/result、typed payload 与 capability；fold、AgentJournal ledger、sync/fork workflow、Host callback、PTY、recovery 与 OpenCode adapter 分居单向依赖的 Runtime/Composition/Adapter locality。否则一个委托类型会把 Host、store、PTY 与恢复实现拖入所有 consumer 的 Fable 闭包。

## 破坏后果

- **拓扑冒充业务**：调用方依赖物理 `agent_id` 或 `worktree` 识别委托，导致运行时调度调整时业务逻辑全面断裂。
- **权能越界与篡夺**：委托被误解为所有权或 Persona 的隐式置换，咨询建议被当成任务重新分配。
- **认知发散与幻觉**：背景材料被当作新 assignment，导致子会话背离自身使命或重复执行父会话义务。
- **旧结果冒充新完成**：上一轮 sticky terminal 或全生命周期 WorkRecord 被下一次复用直接消费，调用方看见“新工作已完成”，实际 child 根本没有处理新需求。
- **命令/现实分叉**：Host 已经启动 child、模型已经在工作，tool 却因发送后的第二次 journal append 失败返回“无法放置”；调用方随后重试，只会撞上一个自己刚刚被告知“不存在”的 busy participant。
- **物理拓扑偷走连续性**：把 parent delta frontier 挂在 `SessionId` 上，session replacement/recovery 后同一 logical participant 会被误当成第一次 handoff，重复或丢失背景。

## 编译闭包预算裁决（2026-09-02）

focused locality 的规模预算服务于一个目的：让单 owner 开发编译显著快于全量 flat build。AGENTS.md 给出双层语义——contract ≤100、runtime ≤185 为建议预算（target），全仓 production `.fs` 的 60% 为 full-fallback 硬顶。DELEG-028 把该裁决写成规范合同。

本轮实测裁决：

- `delegation-fold`（163）、`delegation-sync-runtime`（69）、`delegation-fork-runtime`（51）、`delegation-contract`（26）在 target 内，断言保持 hard gate。
- `delegation-host-adapter`（315）与 `delegation-pty-adapter`（316）的闭包被上游共享 spine 主导（`dispatch-runtime` 227 → `sessionquiescencegate` 212 → `host-signal-adapter` 194；`interaction-authority-ledger` 156；journal spine 180）。adapter 的 charter 要求物理组装 durable spine、wait、host signal 与 managed-agent vocabulary；把 `SyncDelegate/Runtime` 的 Host 集成从 adapter 抽出只会移动文件不减少闭包（composition root 同时绑定两者）。在 spine 本身瘦身（durable-events/host-boundary 的工作）之前，185 对 adapter 不可达。
- 裁决：adapter locality 不以 185 为 hard gate，而以 60% full-fallback 为 hard ceiling；实测值进入 WHAT 作为 ratchet——闭包增长必须修订本条，收缩自动受益。这不是削弱断言：测试从"单一假数字"改为"kind 分层预算 + 增长 ratchet + 60% 硬顶"三重断言。

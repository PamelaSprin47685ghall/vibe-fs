# execution-model-routing — PROOF

当前 WHAT 已定，production 尚未迁移；本包因此继续记录一个聚合 proof gap（GAP-016）。实现时按下表先落可红 oracle，再改 production。

| 命题 | 目标 proof | 状态 |
|---|---|---|
| EMR-001 sole MJS authority + bootstrap | planned oracle `scheduler-module-config`：固定 `.mjs` 路径；缺失时 atomic create-if-absent 推荐模板并从磁盘 import；并发 EEXIST 不覆盖；已有文件绝不改写；create/import/default-export failure fail closed；拒绝 opencode/env/runtime-default fallback | GAP-016 |
| EMR-002 scheduler ABI | planned oracle `scheduler-abi`：`role + running → {model,reasoning}|null`；重复项保留；throw/Promise/invalid target fail closed | GAP-016 |
| EMR-003 running lease multiset / process sharing | planned oracle `routing-occupancy`：每 `(SessionId,EffectiveAgent)` 一个 occurrence、A/B 同 target 仍重复；`worktree-shared-capacity`：两个 PluginRuntimeScope/instance 观察同一 multiset | GAP-016 |
| EMR-004 event-driven null/wait | planned oracle `routing-event-loop`：required `null` 不发 provider/不推进 fallback；occupancy event 才重试；无 timer/busy-loop；取消 waiter；较早 null 不阻塞后续可调度 role | GAP-016 |
| EMR-005 MJS owns policy | planned oracle `routing-policy-boundary`：production 无 `ExecutionLane`/`max_sessions`/candidate/first-free 内建策略；runtime 对合法 target 只做结构校验 | GAP-016 |
| EMR-006 stable session×agent lease / AABB orthogonal | planned oracle `session-model-lease`：普通 prompt 复用；A/A 与 B/B 各自稳定；A/B 可完全相同；peer 判定不看 target | GAP-016 |
| EMR-007 release drives retry | planned oracle `routing-event-loop`：session retire 每 lease 删除一个 occurrence、重复 cleanup 幂等；system ephemeral terminal 释放；真实 occupancy change 触发 pending 一轮 | GAP-016 |
| EMR-008 opencode model non-authority | planned oracle `managed-agent-model-projection`：Host conflict 不改变 MJS lease；无 duplicate-pair-model validation | GAP-016 |
| EMR-009 user-facing model non-authority | planned oracle `managed-request-model-routing` + `host-boundary` physical canary：外部 model/reasoning 被 managed lease 覆盖，实际 provider target 与 lease 一致 | GAP-016 |
| EMR-010 title/compaction scheduler routing | planned oracle `system-model-routing` + Host canary：调用 role=`title`/`compaction`；target 进入 ephemeral `running`；`null` 事件等待；terminal 释放 | GAP-016 |

## GAP

| GAP | 缺口 | 状态 | 关闭条件 |
|---|---|---|---|
| GAP-016 | 新 auto-bootstrap MJS scheduler / event-driven occupancy 合同已有 normative 文档，但 production 仍由 `ManagedAgentConfig` 读取 Host-final `opencode.json` model inventory；尚无推荐模板 resource/create-if-absent loader，旧 duplicate-pair validation/Strength static binding 仍存在，Host managed-request/title/compaction model+reasoning override canary 未建 | OPEN | 上述目标 oracle 全部落地并可红→绿；bootstrap resource 与 HOW 推荐模板一致；Host physical canary 通过；旧 static inventory、内建 lane/capacity 路径与 duplicate-pair validation 从 production/旧 proof 删除 |

本包当前不得宣称实现完成；GAP-016 关闭前 README/CHANGELOG 不应把 `.mjs` scheduler 写成已发布行为。

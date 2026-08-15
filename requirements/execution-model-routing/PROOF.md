# execution-model-routing — PROOF

当前 WHAT 已定，production 尚未迁移；本包因此记录一个聚合 proof gap（GAP-016）。实现时按下表先落可红 oracle，再改 production。

| 命题 | 目标 proof | 状态 |
|---|---|---|
| EMR-001 sole TOML authority | planned oracle `model-config`：固定路径、缺失/parse/schema fail closed、拒绝 opencode/env fallback | GAP-016 |
| EMR-002 seven lanes | planned oracle `lane-routing`：闭集 + 22 managed names + title/compaction 精确映射；`fast-browser`/`deep-browser` 分属第 6/7 lane | GAP-016 |
| EMR-003 candidate/cap schema | planned oracle `model-config`：ordered non-empty、positive cap、跨 lane 同物理模型 cap 必须一致、variant 不分容量 | GAP-016 |
| EMR-004 ordered admission/wait | planned oracle `model-lane-runtime`：first-free、全满不 spill/不 oversubscribe、lane FIFO waiter、释放后重新按序选择、取消 waiter 不抢占 | GAP-016 |
| EMR-005 process-shared physical capacity | planned oracle `model-lane-runtime`：cross-role/cross-lane 汇总、同 session 去重；`worktree-shared-capacity`：两个 PluginRuntimeScope/instance 共用 registry | GAP-016 |
| EMR-006 stable session×agent lease / AABB orthogonal | planned oracle `session-model-lease`：A/A 稳定、B/B 稳定、A/B 可同 model；peer 判定不看 model string | GAP-016 |
| EMR-007 retire release | planned oracle `model-lane-runtime`：session release 全 lease、physical occupant once、重复 cleanup 幂等、waiter 被唤醒 | GAP-016 |
| EMR-008 opencode model non-authority | planned oracle `managed-agent-model-projection`：Host conflict 不改变 lease；无 duplicate-pair-model validation | GAP-016 |
| EMR-009 user-facing model non-authority | planned oracle `managed-request-model-routing` + `host-boundary` physical canary：外部 model 被 managed lease 覆盖，实际 provider model 与 lease 一致 | GAP-016 |
| EMR-010 title/compaction primary target | planned oracle `system-model-routing` + Host canary：两者使用 fastest[0]，不计 managed session capacity | GAP-016 |

## GAP

| GAP | 缺口 | 状态 | 关闭条件 |
|---|---|---|---|
| GAP-016 | 新 lane/TOML/capacity routing 合同已有 normative 文档，但 production 仍由 `ManagedAgentConfig` 读取 Host-final `opencode.json` model inventory；上述独立 oracle 尚未落地 | OPEN | 目标 tests 全部落地并可红→绿；Host request-model mutation/title-compaction 物理 canary 通过；旧 static inventory/duplicate-pair validation 从 production 与旧 proof 删除 |

本包当前不得宣称实现完成；GAP-016 关闭前 README/CHANGELOG 不应把新配置写成已发布行为。

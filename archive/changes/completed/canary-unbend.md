> 本文件是变更工作记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Unbend canary — 纠正 canary 迎合错误生产

## Original instruction（冻结）

> 目前为了让测试绿, 很多地方扭曲了 canary 测试来迎合错误的生产. 请你改正这种行为.
> (orch-unhappy-path 除外,这个测试还没有成功过)

范围：全部 e2e canary；排除 `orchestrator-unhappy-path`（另有并发 agent 负责）。

## 原则

红→绿纪律：生产错修生产；声明与 wire 不符但 wire 合契约则改声明；绝不削弱断言、
不做软跳过、不放宽 oracle。调试结论落盘为持久回归，不留临时探针。

## Active work

已完成（全部绿，已随外部流程提交，HEAD 附近含 `b5e8f947` 起的系列提交）：

- Harness 层：`tests/e2e/support/runtime-key.js` toolsGate（entry.tools ⊆ 请求、
  forbiddenTools ∩ 请求 = ∅，违则 fail-closed）；`scenario-schema.js` 编译+校验
  forbiddenTools；`legacy-fields.js` 解除 retired 并引导改新文本。两个 harness 元测试
  适配。integration 全绿，unit 1095/1095。
- 8/9 区域修复并跑绿：context-recovery（含生产修复 `TurnCompletionProgram.fs`
  `isRecoveryProbeRun` 防 repair 劫持 probe）、reviewer-verdict、host、fallback、
  executor/agent-dsl/inspector/companion、process-stress + enforcer-repair-persist、
  manager 清理、student-teacher（恢复 v2 deferred-completion）。
- 补跑 7 个未覆盖 canary 全绿：orchestrator-publish、host-nudge、reviewer-restart、
  manager-full-loop、manager-file-root、blogger-quiet-stop、pty-stress。

区域2（orchestrator-restart-publish 家族）进行中，生产侧已落：

- `Host.fs`：ORCH-003 restart 经 `AdoptChild` re-enlist 原 Manager session（Fork 走
  existing-child nudge 路径）；`resumeManager` 改为 gate 磁盘冲突标记消失后直接
  finalizeWorktree，不再单独等 Host pending terminal（提交 `57c08ab4`）。
- `FinalityTool.fs` `ensureMigrationLife`：已完成（archived）Life 视为已结篇章，
  ORCH-007 恢复时为 Manager 开新 migration Life（提交 `76a909b6`）。
- `orchestrator-restart-publish-conflict.toml`：第二 suicide（完整 Finality cycle）+
  新增 `orch-repair` optional turn 承接发到 orchestrator 的 missing-final-report poke。

## Remaining work

唯一未绿：`orchestrator-restart-publish-conflict`。

最新基线（HEAD `ccd0ded7`，build ok，`node tests/e2e/cases/orchestrator-restart-publish.test.mjs`）：

- `orchestrator-restart-publish` **通过**（crash-after-candidate exactly-once 恢复）。
- conflict 场景失败，签名：fatal script mismatch no-declared-turn，
  lane=fast-manager kind=chat step=1，lastUser="#\n"，候选
  manager.1/conflict-resume.1/blogger.1 均未匹配；末尾 pending blocking
  orch-continue.1@fast-orchestrator/chat/step-1。日志 `/tmp/restart-publish-baseline.log`。

下一步提示：manager lane 也收到了 "#\n" poke，但 `orch-repair` 声明只覆盖
orchestrator session；需判定该 poke 按契约应发到哪里（修生产路由）或为 manager lane
补声明（改声明），然后两场景连绿 + `node scripts/check.mjs`。

## Blockers

无客观 blocker；仅剩余 conflict 场景 wire/声明对齐。

## Completion criteria

- `orchestrator-restart-publish` 与 `orchestrator-restart-publish-conflict` 均绿，
  断言保持现有强度（Published eq 1、ManagerJobCreated eq 1、barrier-reviewer 计数不削弱）。
- `node scripts/check.mjs` 静态门禁绿；受影响 canary 复跑绿。
- 汇总报告：审计发现的扭曲点清单 + 修复矩阵。

## 遗留观察（不阻塞，报告提及）

- reviewer-verdict：REVIEW-004 nudge 覆盖缺口。
- fallback-aabb-trace：AABB_TRACE_OUT 未接线。

## Final outcome

### Outcome

`orchestrator-restart-publish-conflict` 已绿。`orchestrator-restart-publish` 与
`orchestrator-restart-publish-conflict` 均通过（crash-after-candidate exactly-once
恢复），`node scripts/check.mjs` 静态门禁绿（spec-check 346 条款、architecture 240
文件），orchestrator-publish 与 reviewer-restart 回归绿。断言保持现有强度（Published
eq 1、ManagerJobCreated eq 1、barrier-reviewer 计数未削弱）。唯一未绿场景已闭环，
无阻塞。

### Final specification

conflict 场景最终失败根因：restart 后复用 Reviewer session 时 manager directory 绑定
丢失，系统 prompt 回落至 root workspace，重新加载 `AGENTS.md` 引发同 session 前缀字节
漂移，违反 ARCH-004 前缀缓存密封不变量，Provider 返回 500（`reason=seal-undeclared`）。
生产路由判定为符合契约：`HostSessionNudge.trySendInteractionRepair` 将 missing-final-
report poke 按产生 turn 的 `SessionId` 投递，manager lane 收到 `"#\n"` 属预期。因此：
生产错修生产（重登记目录），wire 合契约则补声明。

### Implementation result

- 生产侧 `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FinalityController.fs`：复用
  Reviewer session 时通过 `scope.RegisterDirectory` 重新登记当前存活的 Manager worktree
  directory，恢复同 epoch 请求上下文一致，消除 ARCH-004 seal 漂移。受 ORCH-006
  `Directory.Exists` 防御保护，不会将不存在路径注册为稳定目录。
- 测试侧 `tests/e2e/scenarios/orchestrator-restart-publish-conflict.toml`：两个 internal
  `#\n` repair turn（orch-repair 与 manager-repair）以不同 tool surfaces 匹配接收者
  （`fork-manager/join` 对 `fork/join/list/suicide`），均不声明 `lane` 字段，遵守
  `scenario-schema.js` 对 internal turn 禁止 `lane` 的硬约束，消除 `no-declared-turn`
  与 `NOTHING_TO_JOIN`。

### Verification

- `node tests/e2e/cases/orchestrator-restart-publish.test.mjs`：
  `orchestrator-restart-publish` 与 `orchestrator-restart-publish-conflict` 均 exit 0。
- `node scripts/check.mjs`：exit 0（spec-check 346 条款、architecture 240 文件）。
- 回归：`orchestrator-publish.test.mjs` 与 `reviewer-restart.test.mjs` 均 exit 0。
- `git diff --check` 无空白错误，工作区 clean。
- 验证由 DevOps 执行；本记录仅如实登记结果。

### References

- ARCH-004 前缀缓存保护：`docs/what/architecture.md`
- ORCH-003 Job·worktree·Manager 一对一：`docs/shape/orchestrator.md`
- ORCH-007 恢复：`docs/what/orchestrator.md`
- GOV-006 单文件 Change 生命周期：`docs/what/document-governance.md`
- GOV-007 用户所有权与启动授权：`docs/what/document-governance.md`

### 审计发现与修复矩阵

| 范围／扭曲点或验证点 | 根因分类 | 修复位置或已验证状态 | Proof |
| --- | --- | --- | --- |
| harness：toolsGate | 声明/harness：请求工具集与声明工具集未严格收敛 | `runtime-key.js` 已按 `entry.tools ⊆ 请求` 与 `forbiddenTools ∩ 请求 = ∅` fail-closed | 两个 harness 元测试适配；integration 全绿，unit 1095/1095 |
| harness：forbiddenTools | 声明/harness：场景禁用工具未在编译期校验 | `scenario-schema.js` 已编译并校验 `forbiddenTools` | 两个 harness 元测试适配；integration 全绿，unit 1095/1095 |
| harness：legacy fields | 声明/harness：retired 字段仍可沿用 | `legacy-fields.js` 已解除 retired 并引导改用新文本 | 两个 harness 元测试适配；integration 全绿，unit 1095/1095 |
| context-recovery | 生产：repair 会劫持 recovery probe | `TurnCompletionProgram.fs` 的 `isRecoveryProbeRun` 已防止该劫持 | Active work 记录为已跑绿 |
| reviewer-verdict | 生产或声明对齐 | 已修复并跑绿 | Active work 记录为已跑绿 |
| host | 生产或声明对齐 | 已修复并跑绿；ORCH-003 restart 经 `AdoptChild` re-enlist 原 Manager session，`resumeManager` 在磁盘冲突标记消失后直接 `finalizeWorktree` | Active work；`57c08ab4` |
| fallback | 生产或声明对齐 | 已修复并跑绿 | Active work 记录为已跑绿 |
| executor／agent-dsl／inspector／companion | 生产或声明对齐 | 已修复并跑绿 | Active work 记录为已跑绿 |
| process-stress／enforcer-repair-persist | 生产或声明对齐 | 已修复并跑绿 | Active work 记录为已跑绿 |
| manager | 生产或声明对齐 | manager 清理已完成并跑绿；ORCH-007 恢复时 `ensureMigrationLife` 为 Manager 开新 migration Life | Active work；`76a909b6` |
| student-teacher | 生产或声明对齐 | 已恢复 v2 deferred-completion 并跑绿 | Active work 记录为已跑绿 |
| 补跑七项 | 验证覆盖 | `orchestrator-publish`、`host-nudge`、`reviewer-restart`、`manager-full-loop`、`manager-file-root`、`blogger-quiet-stop`、`pty-stress` 均已补跑 | Active work 记录为全绿；其中 `orchestrator-publish`、`reviewer-restart` 亦在 Final outcome Verification 记录 exit 0 |
| restart-publish conflict | 生产：重启后复用 Reviewer session 丢失 Manager directory，system prompt 回落 root workspace，触发 ARCH-004 `seal-undeclared` | `FinalityController.fs` 复用 Reviewer session 时以 `scope.RegisterDirectory` 重登记存活 Manager worktree directory；ORCH-006 `Directory.Exists` 防御保留 | `orchestrator-restart-publish.test.mjs` 两场景均 exit 0；`check.mjs` exit 0；`orchestrator-publish` 与 `reviewer-restart` 回归 exit 0 |
| restart-publish conflict | 声明/harness：按 `SessionId` 正确路由到 manager lane 的 `#\n` repair 未被 strict 声明覆盖；internal turn 非法 lane 声明曾触发 schema 编译失败 | `orchestrator-restart-publish-conflict.toml` 的 `orch-repair` 与 `manager-repair` 用不同 tool surfaces 匹配，均不声明 `lane` | 同一 `orchestrator-restart-publish.test.mjs` 证明消除 `no-declared-turn` 与 `NOTHING_TO_JOIN`，断言强度保持不变 |

第 43–57 行的 `no-declared-turn` 失败签名是已解决的历史基线；其后的 completion
criteria 已满足，当前无未绿项。矩阵逐项覆盖原始完成条件所要求的审计发现、根因、修复和
proof，且不改变已登记的其它事实。

两个非阻塞观察：reviewer-verdict 的 REVIEW-004 nudge 覆盖缺口；fallback-aabb-trace 的
AABB_TRACE_OUT 未接线。

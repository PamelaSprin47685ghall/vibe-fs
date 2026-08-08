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

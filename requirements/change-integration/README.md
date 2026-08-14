# change-integration

## 一句话 WHY

独立 Git 工作道路进入共享 ref 时必须在**短原子门**内发布；长时间 review/repair 若持全局锁会把并行工作错误串行化。

## WHAT 概览

- 独立 worktree/job → 共享 target 的 publish lifecycle；共享 ref mutation 是唯一短 critical section（CHGINT-001/004）。
- Clean Gate：工作区必须干净才受理编排（CHGINT-002）。
- candidate/rebase/publish claim/CAS 的原子边界（CHGINT-003/007）。
- conflict 在门外 repair/review 再重新 claim；同一 Manager、同一 worktree（CHGINT-005）。
- restart 后从 Journal 最后事实 fold 出唯一恢复动作（CHGINT-006/012）。
- target ref 冻结 + ff-only CAS；读 head 失败 fail closed（CHGINT-008）。
- same-road continuation 与独立 road 的 integration identity（CHGINT-009）。
- 长 review/repair 不占全局门（CHGINT-010）；墙内机械不进 provider horizon（CHGINT-011）。
- target 变化后旧 post-rebase witness 作废（CHGINT-013）。

## HOW 概览

实现模型：`Infrastructure/Git/{IntegrationGate,GitGateway,WorktreeResource,HookDispatcher,GitOperations,GitSubject}.fs`、
`Application/Orchestration/{ManagerJob,Program,Prompts,Runtime,Types}.fs`、
`Journal/{OrchestratorProjection,OrchestratorFactFold}.fs`。发布 = 事实驱动的 Requested → 幂等执行 →
Accepted/Published（effect-accounting 边界）。详见 HOW.md。

## PROOF 概览

- 包内（MOVE）：`tests/integration-gate.test.mjs`、`tests/git-operations.test.mjs`、
  `tests/worktree-resource.test.mjs`、`tests/job.test.mjs`（合计 74 断言）。
- REUSE（SPLIT@cutover）：`tests/unit/orchestrator/{host,runtime}.test.mjs`、
  `requirements/durable-events/tests/hook-dispatcher.test.mjs`（store ref → `durable-events`）。
- Semantic anchors（`scripts/checks/semantic-anchors.mjs`）拥有 orchestrator 组 `shared-gate`、
  `host-vs-orchestrator`。

## 阅读顺序

1. `WHY.md` — 为什么必须独立存在、RED 是什么、历史失败模式。
2. `WHAT.md` — normative 合同。
3. `HOW.md` — 实现模型（非 normative；含「历史与弃权」）。
4. `PROOF.md` — 命题落点表 + SPLIT@cutover。

## 边界（DOES NOT OWN）

Git 命令具体序列（HOW）；general durable event store（`durable-events`）；review judgement 本身
（`review-judgement`/`review-assurance`）；Orchestrator Persona/guidance（`cognitive-environment`）；
generic effect accounting law（`effect-accounting`）。

# intra-participant-parallelism — HOW

## 架构机制

### 准入检查与会话替换

1. **Origin 门禁**：执行侧首先验证调用方是否具有物理父会话（`parentID` 存在）。根会话（root session）在解析 prompt 前即被拦截并拒绝，并在工具投影中显式关闭裂变可见性。
2. **参数校验与原子准入**：校验 `prompts` 数组（N≥2 且非空），预留并发槽位后，为每条 lane 创建与原 caller 具有相同父级的 fresh sibling 会话。
3. **首载荷注入与静默交接**：各 lane 继承调用方的角色配置与语言设置，注入原 caller 的 canonical LWR 与对应 lane 输入。全量 lanes 建立成功后，向原 caller 发起 Fission 专属的静默中断，无缝移交执行流。

### 债权分配与收敛网络

- **广播与亲和分配**：裂变前的未完成子任务（subagents / PTY）注册为广播源，其完成事实向每条 lane 投递一次；裂变后新创建的子任务自动附加发起 lane 的亲和标记，仅由发起 lane 消费。
- **控制面让渡**：Fission Host adapter 将 reconciled turn 压成 Fission 自己的 settlement observation。需要 nudge、provider fallback/AABB 或 degeneration-guard 自恢复的 turn 返回 `YieldToTurnWorkflow`。Fission 不复刻这些 owner 的恢复状态机。
- **稳定 completion 才 materialize**：只有 lane 的普通 `TurnCompleted` 且其共享/亲和债权已结算时，才写 `FissionLaneMaterialized`。一次 physical attempt 的 abort/failure 不是 lane terminal。
- **Deterministic ring fold**：ring plan 只由 `laneCount` 生成 canonical order `0..N-1`；aggregate 按该 order 读取 keyed WorkRecord，终点 `N-1` 是唯一 final takeover lane。生产代码不得根据 callback 到达顺序维护 `LastMaterializedLaneIndex` 一类控制状态。
- **Takeover 跨 continuation**：`FissionTakeoverClaimed` 只证明 final takeover 已准入并记录其 durable origin，不把 takeover 生命周期锁死在首条 `PhysicalUserMessageId`。Composition/Turn 已经把每次 observation 收敛到当前 physical message；Fission 只验证当前 turn 属于 takeover lane，然后重复同一 settlement law，直到稳定 `TurnCompleted` 写 `FissionConverged`。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| INTRA-PARTICIPANT-PARALLELISM-001 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-001] lanes carry no provider-visible identity or handle and keep the same logical participant` |
| INTRA-PARTICIPANT-PARALLELISM-002 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-002] canonical lane array preserves each prompt including embedded newlines` |
| INTRA-PARTICIPANT-PARALLELISM-003 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input` |
| INTRA-PARTICIPANT-PARALLELISM-004 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller` |
| INTRA-PARTICIPANT-PARALLELISM-005 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] old caller silent-interrupts only after every lane started`；`requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set` |
| INTRA-PARTICIPANT-PARALLELISM-006 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-006] pre-fission completion broadcasts to every lane exactly once with idempotent delivery` |
| INTRA-PARTICIPANT-PARALLELISM-007 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-007] post-fission completion has exactly one affinity target: the initiating lane` |
| INTRA-PARTICIPANT-PARALLELISM-008 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-008] keyed work bundle is idempotent and rejects conflicting records for one lane` |
| INTRA-PARTICIPANT-PARALLELISM-009 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] convergence requires all lane records and all completion deliveries`；`requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] ring successor wraps and forwards past already-closed lanes to the next live present` |
| INTRA-PARTICIPANT-PARALLELISM-010 | `requirements/intra-participant-parallelism/tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors` |
| INTRA-PARTICIPANT-PARALLELISM-011 | `requirements/intra-participant-parallelism/tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release` |
| INTRA-PARTICIPANT-PARALLELISM-012 | `requirements/intra-participant-parallelism/tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary` |
| INTRA-PARTICIPANT-PARALLELISM-013 | `requirements/intra-participant-parallelism/tests/fission-tool-origin.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] real root chat message carries a request-local fission deny`；`requirements/intra-participant-parallelism/tests/fission-tool-origin.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] forced root fission rejects origin before parsing prompts` |
| INTRA-PARTICIPANT-PARALLELISM-014 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-014] control-plane successors run before lane settlement and final takeover`；`requirements/intra-participant-parallelism/tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-014] Degeneration guard remains control-plane owner before Fission settlement` |
| INTRA-PARTICIPANT-PARALLELISM-015 | `requirements/intra-participant-parallelism/tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-015] ring fold order and final takeover lane are canonical, never arrival ordered` |
| INTRA-PARTICIPANT-PARALLELISM-016 | `requirements/intra-participant-parallelism/tests/task-result-list-traversal.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-016] TASK_RESULT_LIST_traverseM_calls_mapper_once_per_input_in_order_stops_at_first_Error_and_skips_empty` |

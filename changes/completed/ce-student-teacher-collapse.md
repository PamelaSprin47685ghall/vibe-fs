# CE follow-up: Student–Teacher capability collapse + durable evidence

**Status:** Completed  
**Priority:** P0 / architectural correctness  
**Closes:** `changes/proposed/ce.md` Student–Teacher REVISE（Teacher CE + Student durable）与举一反三复审  
**Does not close:** 全仓 `test:e2e` / `check:release`（3 个 orchestrator canary 在 clean master 同红 → `changes/proposed/orchestrator-e2e-timeout.md`）  
**Depends:** prior CE / corrective / fix work already in `changes/completed/`

---

## Outcome

### 1. Teacher CE collapse (结构性，不是 regex)

`StudentTeacherRuntime` 现为：

- `TeacherCall` 持 `Returned` + `Completion` 两个 TCS；`InvokeTeacher` 是单一 CE 栈：
  `sendTeacherPrompt → await Returned → await Completion`
- 删除 `teacherCompletions` 与 `CompletionRun option` stage bit
- 删除 `teacherOwners` cache；owner 只读 durable `SessionAssociationProjection`
- `HandleTurn` Teacher 分支只比较 **normalize 后的 turn payload** 是否等于
  `TeacherReturnCompletion`，匹配则 resolve `Completion`，否则有界 nudge
- `beginTeacherCall` 用 `use`/`IDisposable` 保证作用域退出注销，防止泄漏

### 2. Student durable evidence

- Compile 完成 vs idle：读 `StudentQaStore.Exists`（QA.md 存在性），不再用
  final-completion registry presence 当 PC
- `studentFinalCompletions` 降级为 `pendingCompletionTexts`（仅 TextComplete 改写武装）
- `currentStudentRequestKind`：Accepted 优先；Claimed-but-not-Accepted 的 Compile claim
  不得误判 Learn

### 3. EXEC-027 nudge 预算

Teacher / Student compile idle nudge 均受 `AgentPairCursor.DefaultAutoRecoveryBudget`
约束；预算耗尽 fail-closed（父 `teacher` 失败 / 停止自动 compile nudge）。

### 4. Registry 终态

| registry | 裁定 |
| --- | --- |
| `runs` | 保留 |
| `teacherCalls` | 保留为投递 + 单飞 |
| `pendingCompletionTexts` | TextComplete 武装（非 HandleTurn PC） |
| `skillMutations` | 保留 |
| `teacherOwners` | 已删 |
| `teacherCompletions` | 已删 |

结构性证明：`HandleTurn` 不再联合 probe 两 registry presence 选 effect。

### 5. 举一反三

对 Blogger `decideMaterial`、PluginRuntimeScope parked∩offer、Reconciler active∩queued、
BloggerCrashRecovery、ReviewSeal park→bind 做了同判据复审：**均为物理路由/投递握手**，
不套用 CE collapse。仅修正 ReviewSeal / HostSignalBootstrap 过时 onTurn-bind 注释，
并写入 `docs/proof/dsl-structured-program.md`。

### 6. 测试与门禁

新增：

- `tests/unit/student-teacher/ce-collapse.test.mjs`（单飞 / dispose / 幂等 /
  预算耗尽 / 第二 await 取消 / payload normalize）
- `StudentQaStore.Exists` + `qa-store` 契约测试

文档：`docs/shape/execution.md` EXEC-026、`docs/proof/execution.md`、
`docs/proof/dsl-structured-program.md`；ratchet baseline
`Session/StudentTeacherRuntime.fs` 收紧为 `mutable-record-field: 0`。

治理：原 `changes/completed/ce.md`（时序所有权大提案）与 proposed 撞名，已重命名为
`changes/completed/ce-temporal-ownership.md`。

**已验证：**

- `npm run check` 全绿（lint + build + unit + integration）
- `tests/unit/student-teacher/**` 全绿（含 ce-collapse）
- `dsl-ownership` OK（246 files）
- e2e：`student-teacher`、`devops-mechanical-repair-loop`、`manager-unhappy-path` 及
  其余 24/27 canary 绿

**诚实边界（E2E 全集）：**

以下 3 个 orchestrator canary 在 **未含本 Change 的 clean master** 上单独复现同样超时
（`orch.2` / `manager.3` / `barrier-reviewer.0`），**不是本 Change 引入**：

- `orchestrator-publish`
- `orchestrator-unhappy-path`
- `orchestrator-restart-publish`

因此不能用「缩水 close」掩盖；也不能把无关环境/既有 flake 算进本 Change 的回归。
后续应单开 Change 修 orchestrator canary；本 Change 的 Student–Teacher REVISE 与
结构性 PC 消除已闭环。

---

## Non-goals

- 不新增 registry-joint-branch-v2 / try* helper 静态 detector
- 不把 Student compile 链强行 collapse 进跨 Host turn 的 detached CE（违反 DSL-004）

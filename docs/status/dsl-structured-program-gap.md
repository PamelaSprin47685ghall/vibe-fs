# DSL 结构化程序规则实现差距

对应规范：`what/dsl-structured-program.md`、`shape/dsl-structured-program.md`、`how/dsl-structured-program.md`、`proof/dsl-structured-program.md`。

## 未实现差距

1. `src/Wanxiangshu/Process/NodeProcessWait.fs` 仍用 `timedOut/cancelled/killSent/killAckExpired` 四个 `let mutable` 布尔。
2. `src/Wanxiangshu/Session/Companion.fs` 仍暴露 `ArmRecoverySlot/DisarmRecoverySlot/IsRecoveryArmed`。
3. `src/Wanxiangshu/Session/BloggerRuntimeState.fs` 仍含 `BloggerRuntimeCell` 状态乘积与 `BloggerToolRecovery`。
4. `src/Wanxiangshu/Domain/SessionRecovery.fs` 仍含 `RecoveryTrace` 解释器用于生产正确性证明。
5. `src/Wanxiangshu/Application/Reconciliation/ReconciledTurn.fs` 与 `src/Wanxiangshu/Domain/ReconcileProgram.fs` 仍各定义一份 `TurnOutcome`。
6. `src/Wanxiangshu/Kernel/Roles.fs` 与 `src/Wanxiangshu/Session/AgentRoleIdentity.fs` 仍各定义一份十态角色 DU。
7. `src/Wanxiangshu/Journal/AgentFact` 仍是 41-case 跨域总和类型。
8. `scripts/checks/dsl-ownership.mjs` 仍无法识别语义状态机、组合状态、多布尔循环、大 DU 分类、重复 case 集。

## 阻塞

无外部阻塞。各 PR 可独立按 `how/dsl-structured-program.md` 顺序实施。

## 验收标准

- `npm run lint` 保持通过。
- `npm run check:release` 在新实现完成前允许存在已登记 status 差距；完成后删除本文件。

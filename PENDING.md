# PENDING — 历史交付清单（已完成）

> **状态：COMPLETED / HISTORICAL**  
> 原 8 项功能施工单已全部落入正式规范 + 生产实现 + 自动化回归。  
> 本文件不再是待办；后续变更只读 `docs/{what,shape,how,proof}` 与 `AGENTS.md`。  
> 施工设计原稿已废止为证据索引（不再保留未勾选框）。

## 完成判定（2026 审计）

| # | 要求 | 正式条款 | 生产所有者 | 自动化证据 | 状态 |
|---|------|----------|------------|------------|------|
| 1 | join 等待可被新 user 消息打断，返回 `interrupted`（非 error）；不 `runtime.Cancel` child | EXEC-017 | `Session/HostForkRuntime.fs` · `Tools/JoinTool.fs` · `Session/CompletionMailbox.fs` | `tests/unit/execution/join-v2-mailbox.test.mjs` · `join-v2-program.test.mjs` · `join-v2-wire.test.mjs` | 已实现且已验证 |
| 2 | Manager/Orch 优先复用已有 sub-session（`agent_id` / nudge） | EXEC-002 及 prompts | `Tools/ForkTool.fs` · `resources/prompts/{manager,orchestrator}-system.md` | `tests/unit/verify/orchestrator-reuse-contract.test.mjs` | 已实现且已验证 |
| 3 | join wire 的 work_record 为安全注释，非 TOML 可执行字段 | EXEC-018 · ARCH-010 | `Codec/JoinResultRenderer.fs` | `tests/unit/execution/join-v2-wire.test.mjs` · `tests/integration/plugin/manager-tool-contract.test.mjs` | 已实现且已验证 |
| 4 | Blogger 缺 tool：先 InteractionRepair/nudge，失败后再 AABB | ENFORCER-060…068 | `Session/EnforcerHost.fs` · `BloggerRuntimeState.fs` · `HostSessionNudge.fs` | `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs` · `blogger-crash-recovery.test.mjs` | 已实现且已验证 |
| 5 | join 批量返回积压结果；`MaxJoinBatch=32`；稳定序；CAS 单次消费 | EXEC-018 / EXEC-019 | `HostForkRuntime` · `ManagerJob` · `JoinResultRenderer` | `join-v2-mailbox.test.mjs` · `join-v2-abandoned-order.test.mjs` · `join-v2-wire.test.mjs` | 已实现且已验证 |
| 6 | Enforcer 单必填 `tip`（catalog field 枚举 120）；删 score-vector；tip 历史可 replay / squash 保留 | ENFORCER-020…026 · 071 · 072 | `Domain/EnforcerCatalog.fs` · `EnforcerCodec.fs` · `BlogTool.fs` · `Journal/*` | `tests/unit/enforcer/tip-v2-contract.test.mjs` · `codec.test.mjs` · `catalog*.test.mjs` · journal tip v2 拒绝旧 shape | 已实现且已验证 |
| 7 | coder 必填 `tdd=red\|green`；Manager fork coder 同约束（prompt）；非 coder 不要求 | Domain `TddPhase` · `CoderTool` · `ForkTool` | `Domain/TddPhase.fs` · `Tools/CoderTool.fs` · `Tools/ForkTool.fs` · coder/manager prompts | `tests/unit/execution/tdd-phase.test.mjs` · `verify/fork-child-payload-tdd-contract.test.mjs` | 已实现且已验证 |
| 8 | Transform 注入结对编程 synthetic marker；全锚点稳定重放；不进 XTrace/Blogger/work record | HOST-013 | `Host/PairProgrammingThoughtTransform.fs` · `SpikePlugin` 链 | `tests/integration/plugin/manager-tool-contract.test.mjs`（HOST-013）· `tests/integration/harness/runtime-key-cases.mjs` | 已实现且已验证 |

## 规范收口（取代施工单旧描述）

- Transform：**全锚点**稳定重放 + 稳定 id（HOST-013 / `how/host.md`），不是「只处理最新锚点」。  
- Join 中断是调度结果，不是 `ForkError`。  
- Enforcer tip v2 clean break：旧 `ScoreVectorRef` journal fail-closed（PERSIST-005 / ENFORCER-072）。  
- DSL：直接 CE + ports；`dsl-ownership --threshold=0`（禁止上调）。  
- `TASK.md` = DSL 纠偏历史档案，非现行施工指令。  
- `docs/status/` 仅保留未裁决 proposal 差距（`strength-student-teacher.md`）；本清单无活跃 gap。

## 验证命令（交付时）

```bash
npm run lint
npm run build
npm test
npm run test:integration
npm run test:e2e
npm run test:package
npm run check:release
```

本文件勾选框时代已结束。若需协议细节，读 `docs/what/execution.md`、`enforcer.md`、`host.md` 与对应 proof。

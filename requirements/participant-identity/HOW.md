# participant-identity — HOW

## 架构与核心机制

`participant-identity` 在 Domain/Kernel 层独占身份解析与不可变性：

```text
合法 root input / typed owner-derived evidence
                       │
                       ▼
resolve(Role, persona provenance/version, canonical catalog)
                       │
                       ▼
prepared ParticipantIdentityEvidence {
  SelectedAgent; PeerAgent; Role; Persona; PersonaEvidence
}
                       │
                       ▼
AuthorityRootAccepted { exact root keys; ParticipantIdentityEvidence }  ← single durable append
                       │
                       └──► system-prompt / authority / capability consumers

exact execution request ───► ExecutionBinding { EffectiveAgent; provider/model; lease }
```

1. **Root identity acceptance**：identity owner 只接受合法 root input 或 exact owner-derived evidence，并纯计算/校验完整 `ParticipantIdentityEvidence`。Authority 把它作为 `AuthorityRootAccepted` 的必填 payload 单次原子追加；该追加同时是 identity installation 与 root acceptance 的唯一 durable fact。禁止独立 identity-installation write。child、attached 与 InternalLeaf 若缺少 evidence，或 evidence 的 owner/run 不精确匹配，必须 fail-closed；append 未提交时不得发布任一状态。

2. **Run-scoped fold**：identity 与 authority 投影都从同一 `AuthorityRootAccepted` 重放，以 exact `(SessionId, LogicalRunId, AuthorityRootId)` 为 key；Session cache 与 Host physical parent 不参与。重复 acceptance payload 幂等，任何 run 内不同 payload 都拒绝。

3. **Container reuse**：fresh root acceptance 必须先观察 exact `AuthorityLogicalRunClosed`，其 key 精确匹配旧 `(SessionId, LogicalRunId, AuthorityRootId)`，且 authority fold 已由该事实释放旧 active identity binding。缺少该 closure、仅有 lifecycle terminal/association removal/idle/timeout 或仍有 active run 时不得替换。同一 SessionId 的后继 run 从合法 root input 重新 resolve，不读取旧 run identity。

4. **Execution separation**：canonical SelectedAgent/PeerAgent 是 immutable identity evidence；fallback、Strength、Peer 路由与 provider lease 只生成含当前 EffectiveAgent/provider/model/lease 的新 `ExecutionBinding`。system prompt、authority profile 与 capability projection 消费同一 durable identity evidence，不反向解析或改写身份。内部 Role 使用私有 catalog 分支，不进入 public `Role`。

5. **typed evidence 与所有权切割**：
   - `ParticipantIdentityEvidence` 是私有构造的完整值；root resolve、owner-derived inheritance 与 durable rehydration 都必须校验 canonical agent/peer、Role、Persona、catalog version 与 provenance，不能逐字段补写。
   - `SessionPersona`、`SessionSurface`、Host `PersonaBinding` 与 `RoleIdentity` 均不存在；身份不能落入 `SessionId` keyed process cache，也不能由显示字符串授权。
   - `Roles.fs` 不含 `ToolPermission`、权限矩阵或 capability 判断；这些事实只在 `Foundation/OfficeCapability.fs`。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PID-001 | `requirements/participant-identity/tests/participant-identity.test.mjs::WHAT[PID-001] resolves every canonical participant identity and persona`；`requirements/participant-identity/tests/identity-boundary-gate.test.mjs::WHAT[PID-001] rejects SessionId keyed identity cache` |
| PID-002 | `requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-002] ProviderAttempt carries its ParticipantIdentity as one nested value` |
| PID-003 | `requirements/participant-identity/tests/participant-identity.test.mjs::WHAT[PID-003] rejects blank Persona and unsupported catalog version` |
| PID-004 | `requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-004] terminal dispatch preserves the exact IdentitySeed`；`requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-004] Strength replica inherits owner Persona and version with the same EffectiveAgent` |
| PID-005 | `requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-005] provider planning selects the system prompt and tool set from profile Role` |
| PID-006 | `requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-006] fallback preserves ParticipantIdentity` |
| PID-007 | `requirements/participant-identity/tests/participant-identity-consumers.test.mjs::WHAT[PID-007] Bookkeeper has private identity and no public Role` |
| PID-008 | `requirements/participant-identity/tests/identity-lineage.test.mjs::WHAT[PID-008] inherited identity records the exact durable owner witness`；`requirements/participant-identity/tests/identity-lineage.test.mjs::WHAT[PID-008] rejects stale owner identity evidence` |
| PID-009 | `requirements/participant-identity/tests/session-reuse-identity.test.mjs::WHAT[PID-009] reuses SessionId with a fresh closed-run identity`；`requirements/participant-identity/tests/session-reuse-composition.test.mjs::WHAT[PID-009] production plugin replaces identity only after exact durable Manager closure` |

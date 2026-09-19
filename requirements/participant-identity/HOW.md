# participant-identity — HOW

## 架构与核心机制

`participant-identity` 在 Domain/Kernel 层独占身份解析与不可变性：

```text
合法 root input / typed owner-derived IdentitySeed
                          │
                          ▼
resolve(Role, persona provenance/version, canonical catalog)
                          │
                          ▼
prepared ParticipantIdentityEvidence {
  SelectedAgent; Role; Persona; PersonaEvidence  (immutable per logical run)
}
                          │
                          ▼
AuthorityRootAccepted { exact root keys; ParticipantIdentityEvidence }  ← single durable append
                          │
                          ├──► system-prompt / authority / capability consumers
                          │
                          ▼ (fresh physical execution routes fixed Role via MJS scheduler)
ExecutionBinding { target: ModelTarget; fence: CapacityFence; lease }
  (capacity exact identity = session + physical + Role + Participant + target + fence)
```

1. **Root identity acceptance**：identity owner 只接受合法 root input 或 exact owner-derived evidence，并纯计算/校验完整 `ParticipantIdentityEvidence`。Authority 把它作为 `AuthorityRootAccepted` 的必填 payload 单次原子追加；该追加同时是 identity installation 与 root acceptance 的唯一 durable fact。禁止独立 identity-installation write。child、attached 与 InternalLeaf 若缺少 evidence，或 evidence 的 owner/run 不精确匹配，必须 fail-closed；append 未提交时不得发布任一状态。

2. **Run-scoped fold**：identity 与 authority 投影都从同一 `AuthorityRootAccepted` 重放，以 exact `(SessionId, LogicalRunId, AuthorityRootId)` 为 key；Session cache 与 Host physical parent 不参与。重复 acceptance payload 幂等，任何 run 内不同 payload 都拒绝。

3. **Container reuse**：fresh root acceptance 必须先观察 exact `AuthorityLogicalRunClosed`，其 key 精确匹配旧 `(SessionId, LogicalRunId, AuthorityRootId)`，且 authority fold 已由该事实释放旧 active identity binding。缺少该 closure、仅有 lifecycle terminal/association removal/idle/timeout 或仍有 active run 时不得替换。同一 SessionId 的后继 run 从合法 root input 重新 resolve，不读取旧 run identity。

4. **Execution separation**：canonical Role/Persona/SelectedAgent 是 immutable identity evidence（由 `IdentitySeed` 派生并在 logical run 内恒定不变；`PeerAgent` 与游标选择的 `EffectiveAgent` 语义均不存在）。每次 fresh physical execution 将固定 Role 经 MJS scheduler 路由至 model target，并签发包含 target/lease/fence 的 `ExecutionBinding`（binding 仅改变 target 与 lease，不得改写身份）。system prompt、authority profile 与 capability projection 消费同一 durable identity evidence，不反向解析或改写身份。内部 Role（Bookkeeper/Predictor）使用私有 catalog 分支，不进入 public `Role`。

5. **typed evidence 与所有权切割**：
   - `ParticipantIdentityEvidence` 是私有构造的完整值；root resolve、owner-derived inheritance 与 durable rehydration 都必须校验 canonical Role、Persona、SelectedAgent、catalog version 与 provenance，不能逐字段补写。
   - `SessionPersona`、`SessionSurface`、Host `PersonaBinding` 与 `RoleIdentity` 均不存在；身份不能落入 `SessionId` keyed process cache，也不能由显示字符串授权；PeerAgent 与 cursor-selected EffectiveAgent 语义已彻底删除。
   - `Roles.fs` 不含 `ToolPermission`、权限矩阵或 capability 判断；这些事实只在 `Foundation/OfficeCapability.fs`。

6. **活跃身份与历史身份解析隔离**：
   - 活跃名字解析（`resolveParticipantIdentityAtRoot` 等）只接受当前规范角色（`engineer`、`manager`、`orchestrator`、`devops`、`blogger`），拒绝废弃角色（`coder`、`inspector`、`browser`、`inquiry`、`distiller`）。
   - 历史回放与解码仅在专用解码边界识别旧身份，不进行静默权限提升（旧 `inspector` 保持只读身份，不升级为可写 `engineer`；旧 `devops` 不获得 `engineer` 的 Fission 权能）。

## GAP

- `participant-identity-010`（CLOSED）：活跃身份解析已收敛为 Engineer/DevOps/Manager/Orchestrator/Blogger，Coder/Inspector/Browser/Inquiry/Distiller 退出活跃路径，历史身份解码隔离已闭合，落点 `Identity.fs` 升权修复、`Roles` 分流及 `tests/010.test.mjs`。

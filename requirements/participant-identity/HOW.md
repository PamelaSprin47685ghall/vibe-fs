# participant-identity — 实现模型与约束（非 normative）

## 实现模型

身份轴的 canonical 事实全部落在 Domain/Kernel 层，发送路径只消费：

| 事实 | 实现 | 说明 |
|------|------|------|
| `Role` DU + `AgentTier` | `src/Wanxiangshu/Kernel/Roles.fs` | 10 个 office 身份 + Fast/Deep 两档 |
| 名称/peer/分组公式 | `src/Wanxiangshu/Domain/ManagedAgentCatalog.fs` | `nameOf`/`peerNameOf`/`managerForkableRoles`/`bookkeeperNames`；peer 是公式不是表 |
| Persona 解析 | `src/Wanxiangshu/Domain/PersonaCatalog.fs` | `persona role tier`（Role × initial tier）；`bookkeeperPersona`；`inheritFrom` |
| Persona 冻结 | `PersonaCatalog.SessionPersona` | `bindOnce`：同值幂等、异值拒绝；`clearAllForTests`（测试钩子） |
| agent 名解析 | `src/Wanxiangshu/Domain/PromptAuthority.fs` `parseAgentNameTyped` | `fast-ROLE`/`deep-ROLE` → `{Name; Role; Tier; PeerName}`；legacy/未知/畸形三分拒绝 |
| prompt identity | `PromptAuthority.systemPromptIdFor` | 只依赖 `CanonicalRole`（tier 不参与，PID-005） |
| profile 组装 | `PromptAuthority.buildAttemptExecutionProfile` + `Domain/AttemptPlanner.fs` | `EffectiveAgent` 随 fallback cursor 动；`SystemPromptId`/Persona 不动 |
| binding 解析律 | `src/Wanxiangshu/Infrastructure/OpenCode/Host/Sessions.fs`、`ChatParamsHook.fs` | `BindingIntent` = Preserve / ExplicitExecutionOverride；managed frozen / user-facing 追最近真实请求 / 不一致 fail-closed（PROMPT-006） |
| 角色标签持久化 | `src/Wanxiangshu/Session/AgentRoleIdentity.fs` | `roleName` 委托 `ManagedAgentCatalog.roleLabel`，避免 DU 拼写改名破坏 durable string |

关键不变量：任何路径都**不得**在 `buildAttemptExecutionProfile` 之外另造身份字段；tier/EffectiveAgent
只影响 `EffectiveAgent` 一个字段（FALLBACK-004），Persona / SystemPromptId / ToolCapabilitySet 不随其变。

## 边界与弃权

### 不归本包（引用其它包）

- office 的 entitled consequence、权限矩阵内容 → `office-capability`、`capability-enforcement`。
- session 的 execution class（Work/InternalLeaf）× attachment（Attached/…）→ `session-ontology`
  （DEPENDS ON）。
- fallback/Strength 的预算与算法 → `provider-attempt-recovery` / `speculative-investigation`。
- system prompt 字节稳定性、prefix epoch → `prefix-stability`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| AGENT-002「恰好 22 名、非空互异 model 串」 | HOW（runtime contract） | COVERAGE：exact catalog + machine names = implementation vocabulary；「缺一启动失败」是当前 runtime 契约。`catalog.test.mjs` 保留为 runtime-contract proof（`AGENT_002_required_names...`、`MACFG_validate_reports_missing_managed_agent...`） |
| AGENT-004 非法旧名清单（orchestrator/meditator/student/…） | GARBAGE（migration ratchet） | legacy reject = 迁移证明；`catalog.test.mjs` 的 `AGENT_004_*` 断言保留作 ratchet，新世界基线稳定后可删 |
| Persona display 名（Integrator/Director/Coordinator/…） | HOW | AGENT-028 表是当前命名；除非命名成为 public contract，否则不构成 WHAT |
| `SessionPersona` process-local `Dictionary` | HOW | Phase 16 实现；durable journal fact 是未来演进，不改变「bind-once」语义 |
| `AgentRoleIdentity.ofRole/ofManaged/toRole` 恒等函数 | HOW | 只作 Host-wire 解析入口的显式类型通道 |

## 历史（考古摘要）

- `archive/changes/completed/universal.md`：Student/Teacher 删除后目录重排、Bookkeeper pair 条件化——
  session 部分归 `session-ontology`，身份轴（Role/Persona/Binding 分离）由此沉淀。
- `archive/docs/why/agent.md`「备选与被拒」：Role=Persona=Binding 合一被拒（Bookkeeper 病理实例）；
  双层权限、内部 Agent 隐去、Persona 一次绑定各有独立失败模式记录。
- `archive/docs/what/prompt.md` PROMPT-006/014：binding 解析律与 Persona 冻结立法。

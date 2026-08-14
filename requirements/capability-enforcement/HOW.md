# capability-enforcement — 实现模型与约束（非 normative）

## 实现模型

| 层 | 实现 | 说明 |
|----|------|------|
| 域能力 token | `src/Wanxiangshu/Kernel/Roles.fs` `ToolPermission` + `Roles.permissions role` | 唯一 Role→能力集；Kernel 层 Vocabulary |
| 唯一 profile 构建 | `src/Wanxiangshu/Domain/PromptAuthority.fs` `toolCapabilitiesFor` + `buildAttemptExecutionProfile`；调用点 `Domain/AttemptPlanner.fs` `plan` | `AttemptExecutionProfile.ToolCapabilitySet` 是每次请求能力权威；architecture gate 拒绝模块外 record expression |
| js 投影 | `src/Wanxiangshu/Domain/JsCapability.fs`（`ofToolPermission` 唯一映射、`JsFragmentRegistry`）+ `Infrastructure/OpenCode/Tools/ToolRegistry.fs` `JsToolGenerator.generate` | 四层同构（ENF-008）；无手写 `js-*` spec |
| schema 层 | `src/Wanxiangshu/Tools/StaticTools.fs` `permissionObj` → `Infrastructure/OpenCode/Host/ManagedAgentConfig.fs` `applyOwnedFields` | Host-final permission；`external_directory` 唯一写点（ENF-011） |
| gate 层 | `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRegistry.fs` `rolePredicate` + `gateExecute` | 逐工具 Role 谓词 + execute 前拒绝；unresolved role → `DeniedUnestablished`（ENF-010） |
| MCP wildcard | `Kernel/StealthBrowserMcp.fs`（Network→`stealth-browser-mcp_*`）、`Kernel/SphinxMcp`（Sphinx→`sphinx_*`） | 域能力 token 留在 `Roles.permissions`，wildcard 只是 schema 键（ENF-007） |
| 静态 gate | `scripts/checks/capability-isomorphism-gate.mjs`（KEEP 本包）、`tool-referential-integrity.mjs`（Gate A）、`js-surface-gate.mjs`（KEEP repository-programming） | 分别防四层漂移 / 同名异义 / 手写 js-* 名 |

## 边界与弃权

### 不归本包（引用其它包）

- office 的 entitled consequence（offices 有什么资格）→ `office-capability`（DEPENDS ON）。
- Role/Persona/Binding 身份轴 → `participant-identity`（DEPENDS ON）。
- action 描述的五问合同（act/时机/负边界/成功后果/参数）→ `action-affordance`。
- internal participant 不进 choice surface 的可见性 → `participant-horizon`。
- 编程面 SDK 语义（sandbox/transaction/anchor/failure algebra）→ `repository-programming`。
- MCP 启动/注入机制 → `host-boundary`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| AGENT-006 精确工具名清单（表内容） | HOW（当前矩阵） | 「矩阵是 enforcement 投影」是 WHAT；每个名字本身是当前实现 vocabulary，随能力演进可重画 |
| MCP wildcard 字符串（`stealth-browser-mcp_*` / `sphinx_*`） | HOW | Host schema 键；「域能力 token 唯一」才是 WHAT（ENF-007） |
| `attempt-plan.test.mjs` 中 prefix/probe 断言 | HOW → `prefix-stability` | 该文件是 context family SPLIT；本包只引用 PROMPT-008/AGENT-010 能力断言 |
| AGENT-002 缺一失败 / AGENT-004 旧名拒绝（`agent-permission-gate` / `managed-agent-config` 中的断言） | HOW（runtime contract / migration ratchet） | COVERAGE：exact catalog = implementation vocabulary；legacy reject = 迁移证明；断言保留作 runtime-contract proof |
| `tool-referential-integrity` 的 LEGACY_FORBIDDEN_NAMES 清单 | HOW | 旧名 ratchet；「同名唯一合同」才是 WHAT（ENF-009） |

## 历史（考古摘要）

- `changes/completed/js-capability-projected-tools.md`：四层同构立法（§2.1）；「不新增第二份
  Authority」——generator 只读 `AttemptExecutionProfile.ToolCapabilitySet`（§3）；手写矩阵被拒（§1）。
- `docs/why/js-tools.md`：「If a method is present, the capability exists. If a method is absent,
  it does not.」四层同构；万能基类 + prose warning 被拒。
- `docs/why/agent.md`：「双层权限 vs 单层可信」被拒（Host 配置可漂）；external_directory 固定 allow
  元权限 vs 塞矩阵被拒；内部 Agent 从 public enum 消失。
- `docs/shape/agent.md` AGENT-007/019：双层边界与唯一写点。
- COVERAGE OVERLAP 修复：同构/同源律唯一归 capability-enforcement；repository-programming 只应用
  （因此 `js-surface-gate.mjs` 的语义 oracle 属于本包律的应用）。

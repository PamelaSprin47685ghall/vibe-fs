# capability-enforcement — WHAT

## ENF-001: 每次 provider attempt 有一个 canonical ToolCapabilitySet，由 CanonicalRole × RequestKind 唯一决定

每次 provider 请求执行的可用能力集合（ToolCapabilitySet）由 `CanonicalRole` 与 `RequestKind` 唯一确定，并在构建 `AttemptExecutionProfile` 时单点完成组装。禁止在 profile 之外另设独立的权限来源或旁路字段。

## ENF-002: provider-visible schema 与 runtime execution gate 读同一 capability truth

Host 侧展示给模型的工具 Schema 与运行时执行拦截 Gate 必须双向完备，且均直接推导自唯一的 `Roles.permissions` 权威映射。无权限工具不出现在 Schema 中，异常绕过 Schema 的调用亦在运行时 Gate 被即时阻断。

## ENF-003: capability projection 可按 office + request contract 收窄，但不得扩大 office entitlement

能力投影允许根据特定 `RequestKind`（如投机副本或窄通道）对角色固有权限进行收窄，但任何投影出的能力集合必须是 `Roles.permissions(role)` 的严格子集，严禁发生权能扩大。

## ENF-004: execution tier 不改变同 office 的 authority：permissions(fast-ROLE) = permissions(deep-ROLE)

同一 Office 的 fast 档与 deep 档工具权限集完全相等：`permissions(fast-ROLE) = permissions(deep-ROLE)`。执行档位仅代表底层机器与推理深度，不影响权限矩阵。

## ENF-005: request-specific replica/leaf 可进一步收窄：StrengthReplica 只 {Read; Glob; Grep}

用于投机调查的 `StrengthReplica` 仅保留 `{Read; Glob; Grep}` 只读能力面。其运行时工具映射在 deny 全部工具后精准放行此三项，其余修改与执行工具在副本内全部 fail-closed。

## ENF-006: internal-only participants/actions 不进无资格 participant 的工具面

内部专用工具（如 Blogger 的 `chronicle`、Bookkeeper 的 `js-bookkeeper`）仅向对应内部角色开放，普通角色不可见亦不可执行。认知与交互效用工具（如 `assume` 等）不属于领域业务权限，不进入 `Roles.permissions`，亦不得借此扩大角色的领域权能。

## ENF-007: Host-native/MCP/plugin 等不同技术来源的 actions 服从同一 semantic capability policy

无论工具来源于 Host 原生、MCP 外部集成还是插件内部注册，其权限控制均由唯一的领域能力令牌（如 `Network` 映射至 `stealth-browser-mcp_*`，`Sphinx` 映射至 `sphinx_*`）统管，严禁为不同技术来源维护独立的权限映射表。

## ENF-008: js-* 编程面四层同构：capability → base-class member → description → example → runtime gate

针对 JS 文件系统能力：若角色缺少对应 capability，则代码生成器生成的基类中不包含对应方法、工具描述中不提及该方法、示例代码中不展示该方法，且底层运行时 Gate 同样拦截对该方法的调用。所有 `js-*` 工具规范必须由代码生成器运行时生成。

## ENF-009: 工具名引用完整性：same tool name → 唯一 schema owner + 唯一 semantic contract

全系统内同一工具名必须对应唯一的参数 Schema 定义与唯一确定的生命周期、语义动作及返回契约。禁止不同角色在同一工具名下共享存在语义分歧的契约。

## ENF-010: 双层 fail-closed：Role 未定 → 工具集空/拒绝执行；Host 配置异常仍写 deny 默认

若角色或 profile 无法解析，模型可见工具集置为空集且运行时拦截一切调用。若 Host 配置校验失败，系统必须先写入全量 deny 默认策略以覆盖 Host 宽松默认，随后触发进程级致命错误终止退出。

## ENF-011: external_directory=allow 是 Host 路径边界元权限：每 managed agent 显式写入、唯一生产写点

`external_directory = "allow"` 作为路径边界的元权限，统一由托管配置装配层显式写入各 managed agent 的 Host 配置中，不计入角色业务权限矩阵，亦不得作为普通工具放行。

## ENF-012: 工具名投影唯一写入口 = CanonicalRole → permission；禁止第二套旧名表/手写矩阵

所有角色到工具名称的投影均以 `CanonicalRole → permission` 为唯一写入口。系统严禁引入历史别名兼容表或手工硬编码的工具名子集。

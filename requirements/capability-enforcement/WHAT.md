# capability-enforcement — WHAT

## ENF-001: 每次 provider attempt 有一个 canonical ToolCapabilitySet，由 CanonicalRole × RequestKind 唯一决定

每次 provider 请求执行的可用能力集合（ToolCapabilitySet）由 `CanonicalRole` 与 `RequestKind` 唯一确定，并在构建 `AttemptExecutionProfile` 时单点完成组装。禁止在 profile 之外另设独立的权限来源或旁路字段。

## ENF-002: provider-visible schema 与 runtime execution gate 读同一 capability truth

Host 侧展示给模型的工具 Schema 与运行时执行拦截 Gate 必须双向完备，且均直接推导自唯一的 `Roles.permissions` 权威映射。无权限工具不出现在 Schema 中，异常绕过 Schema 的调用亦在运行时 Gate 被即时阻断。

## ENF-003: capability projection 可按 office + request contract 收窄，但不得扩大 office entitlement

能力投影允许根据特定 `RequestKind`（如投机副本或窄通道）对角色固有权限进行收窄，但任何投影出的能力集合必须是 `Roles.permissions(role)` 的严格子集，严禁发生权能扩大。

## ENF-004: execution tier 不改变同 office 的 authority：同一 CanonicalRole 的权限集与 tier 无关

同一 Office 的工具权限集完全由 `CanonicalRole` 决定，与执行档位无关。执行档位仅代表底层机器与推理深度，不影响权限矩阵。托管 agent 使用 canonical bare 名称 (`manager`/`coder`/…/`predictor`)，不再有 `fast-`/`deep-` 前缀。

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

## ENF-013: 权威值严格分为 Evidence / Decision / Witness / Capability / Receipt / PhysicalHandle

六类值的因果职责不可互换：`Evidence` 是已观察输入，`Decision` 是纯分类结果，`Witness` 是带精确对象与版本的证明，`Capability` 是对下一动作的不可伪造许可，`Receipt` 是已受理/已应用结果，`PhysicalHandle` 是当前进程资源。名称相似但只描述工具词汇的类型（例如 `JsCapability`）必须由正向 DSL/manifest 分类为 vocabulary，不得靠名称 allowlist 猜测。

## ENF-014: owner 单点发行；manifest 以 exact file + symbol + source/proof anchor fail closed

每个敏感权威声明必须在 `authority-contracts.json` 正向登记 exact file、symbol、六类分类、owner、WHAT 与声明/发行 proof anchor。anchor 移动或消失、敏感声明未分类、非登记 owner/issuer 构造权威值均使 gate 失败；严禁 name-only allowlist、baseline 或 suppression。

## ENF-015: authority scope 必须精确绑定 current subject + version/sequence + 必要 digest

Witness、Capability 与 Receipt 的合同必须声明 subject、版本/序列及能区分内容的 digest/hash（若该边界具有内容身份）。Witness 不得直接驱动 append/write/send/execute 等效果；消费者必须先针对当前 subject、当前 version/sequence 与当前 digest 做 fresh admission。Finality 继续复用 `FINALITY-002` 与 `FINALITY-010` 的 current request/barrier/Git tree proof，不另造第二套审查权威。

## ENF-016: freshness 在消费点重验；stale witness 只能产生新的 admission，不能复活旧能力

已记录 witness/receipt 可作为历史证据，但其旧版本不授权当前效果。subject、版本、序列或 digest 变化后，旧 witness 必须被拒绝；若当前事实再次满足条件，owner 从当前观察产生 fresh admission。恢复路径不得隐藏旧 program counter 或以历史能力跳过普通准入。

## ENF-017: multiplicity 明示且一次性能力不可复制消费

每个权威合同必须声明 one-shot、exact-N、set/closure 或 replayable evidence 的 multiplicity。一次性 Capability/permit 的成功消费或 release 原子关闭该次机会；Receipt/Evidence 的可重放读取不等于重复执行效果。

## ENF-018: QuiescencePermit 由 ObserveIdle 唯一发行且消费/释放返回 typed failure

`QuiescencePermit` 绑定 opaque gate owner identity、`SessionId` 与 attempt serial；只有 `SessionQuiescenceGate.ObserveIdle` 可发行。`TryConsume` 与 `TryRelease` 均返回 `Result<unit, QuiescencePermitFailure>`，其中 `QuiescencePermitFailure = WrongOwner | NoFreshIdle | AlreadyConsumed | Superseded | Revoked`。跨 gate 为 `WrongOwner`，重复消费为 `AlreadyConsumed`，更新 attempt 后旧 permit 为 `Superseded`，revoke 后为 `Revoked`，drop 或无 eligible idle 为 `NoFreshIdle`；每个 `Error` 分支零效果。JS surface 只投影稳定 typed result view，permit 保持 opaque。

## ENF-019: process capability/permit/PhysicalHandle 非耐久；重启后由当前事实 fresh admission

进程 capability、permit 与 `PhysicalHandle` 禁止进入 Fact/Event、journal codec、JSON 或任何跨进程恢复载荷。崩溃后可恢复 durable Evidence/Receipt，再经普通 owner admission 发行当前进程的新能力；不得持久化 capability、读取 feature history 推断能力、或设置隐藏 recovery PC。Quiescence 恢复必须重新经历当前 physical attempt 的 `ObserveIdle`，不得消费崩溃前 permit。

## ENF-020: 配置不变量fatal使用mandatory injected fuse

invalid managed-agent configuration必须先收敛为capability-enforcement拥有的typed incident；解释该incident的composition必须显式注入fatal capability。validator/runtime不得直接引用fatal physical adapter，不得持有optional/default/global fallback；同一incident只允许一次report与一次kill。此边界不把普通可预期admission rejection升级为fatal。

# capability-enforcement — WHAT

## [001] 每次 provider attempt 的能力集合由 CanonicalRole × RequestKind 单点决定

一次 provider 请求实际可用的工具集合（`ToolCapabilitySet`）由角色的办公能力与本次请求类型共同确定，并在构建 `AttemptExecutionProfile` 时一次组装完成。profile 之外不存在第二处权限来源，也没有旁路字段。角色或请求类型解析不出来时得到空集合，不产出可执行 profile。

能力矩阵只认六个现行角色标签（`advisor`、`manager`、`orchestrator`、`engineer`、`devops`、`plan`），矩阵之外的标签返回空集。`Fission` 是 Engineer 独有的能力：其它角色调用时在运行入口与 `ToolRegistry` 门禁被直接拒绝，拒绝文案点名唯一有权角色（`tool/registry/denied-fission`），并行侧的行为见 intra-participant-parallelism-017。

## [002] provider-visible schema 与运行时门禁读同一份能力事实

Host 侧展示给模型的工具 Schema 与执行拦截门禁读同一份角色能力判定：schema 投影给出固定上限，门禁按当前事实逐次判定；两者同源不表示每轮动态删工具。无权限工具不出现在 Schema 中；绕过 Schema 送进来的调用在运行时门禁被即时拒绝，拒绝是可观察的返回值，不静默放行。

## [003] 能力投影可以按 office + request contract 收窄，不得扩大办公能力

窄通道请求可以比角色固有权限更少。任何投影出的能力集合必须是 `OfficeCapability.permissions role` 的子集；不存在把工具加进投影的通路，一项能力对应的工具名只能来自 `OfficeMatrix` 的唯一映射。

## [004] 执行档位不改变同 office 的权限

同一角色的工具权限完全由 `CanonicalRole` 决定。执行档位与模型目标只影响底层机器和推理深度，不进入权限矩阵；档位输入变化不改变同一角色的 prompt 身份与能力投影。托管 agent 使用 canonical bare 名（`manager`/`orchestrator`/`engineer`/`devops`/`plan`/`bookkeeper`），不注册带档位前缀的名字，也不注册矩阵之外的角色工具面。

## [005] 不可解析的请求类型与角色标签一律空权限集 fail-closed

无法解析的 `RequestKind` 或角色标签不产生任何工具：能力投影为空集，运行时门禁拒绝全部调用。不存在副本式投机调查工具面。判定不回落默认值，也不按标签名字近似猜测。

## [006] internal-only participants/actions 不进无资格 participant 的工具面

内部专用工具（如 Bookkeeper 的 `todowrite-for-bookkeeper`）只对对应内部身份开放，普通角色既看不见也执行不了；判定发生在 attachment 归属处，与角色业务权限矩阵无关。认知与交互效用工具（`assume`）以及 Host 工具（`skill`）不属于领域业务权限，不进入 `OfficeCapability.permissions`，也不得借此扩大角色的领域权能。

Advisor 是严格例外：它是评审者而非行动者，能力集为空；`OfficeCapability.admitsUtility` 对 `advisor` 的 `skill` 与 `assume` 一律返回 false，协议工具 `negotiate`、`resetScope` 同样不给它办公权能。实际 `permissionObj`、provider 请求工具表与执行 admission 三处同时拒绝全部工具。

## [007] 不同技术来源的 actions 服从同一 semantic capability policy

Host 原生、MCP 外部集成、插件注册的工具没有各自独立的权限映射表。外部集成工具不被任何办公能力令牌准入：Host 声明了 MCP server 时，provider 侧仍可见 `<server>_*` 通配键（`"*"` 保持 `deny`），而 managed session 的实际调用在 `ExternalToolAdmission` 执行前被拒绝。门禁读进程内单调只增的已声明命名空间集合，因此始终覆盖本进程写过的每一张 session 权限表；无人 advertise 的命名空间一律拒绝。任何角色都没有外部网络浏览工具；Sphinx 由外部经 MCP 接入，不在核侧另立权限表。

## [008] js-* 编程面四层一致：capability → 基类成员 → 描述 → 示例 → 运行时门禁

角色缺少某项能力时，生成器产出的基类没有对应成员、工具描述不提它、示例代码不展示它，运行时门禁同样拒绝对该成员的调用。`todowrite-for-manager` 面向 Manager 常开，只读文件能力为 Read/Glob，另持有委托原语 Fork/Join，不含任何写或执行成员；能力过滤落到真实执行 API 层，不止于文档措辞。`todowrite-for-<role>` 名字只由生成器产出，`scripts/checks/js-surface-gate.mjs` 拦截生产源码里手写的逐角色工具名。

## [009] 同一工具名 → 唯一 schema owner + 唯一语义契约

全系统内一个工具名对应唯一参数 Schema、唯一生命周期与确定的返回契约。不同角色不得在同一名字下持有语义分歧的契约；`fork` 与 `resume` 是不同生命周期契约，必须保持独立。

## [010] 双层 fail-closed：Role 未定 → 工具集空/拒绝执行；Host 配置异常仍写 deny 默认

角色或 profile 无法解析时，模型可见工具集置为空集，运行时拦截一切调用。Host 配置校验失败时，万象理拥有的字段（`"*": "deny"`、mode、prompt）仍然照旧写入以覆盖 Host 的宽松默认，随后以 `managed-agent-config-invalid` 报错终止；`bash` 在任何分支下都不被放行。

## [011] external_directory=allow 是 Host 路径边界元权限：唯一写点显式写入

`external_directory = "allow"` 由托管配置装配层（`OpenCode/Tools/StaticTools.fs` 的 `permissionObj` 组装）显式写入每个 managed agent，Advisor 写 `deny`。它是路径边界的元权限，不计入角色业务权限矩阵，也不作为普通工具放行。

## [012] 能力↔工具名的唯一映射，禁止第二套名字表

角色到工具名的投影只经 `CanonicalRole → permission → toolNames` 这一条路（`wanxiangli/capability/OfficeMatrix`）。Schema 投影、`requestToolMap`、执行准入读同一份映射。Manager 的专用别名在同一写入口内按角色解析。系统不引入旧名兼容表，也不硬编码工具名子集。

## [013] 权威值按因果职责区分，不按名字猜测

已观察输入、纯分类结果、对下一动作的一次性许可、已受理/已应用的结果、当前进程资源，这几类值不能互相顶替：一次性许可不能靠重复读取再次生效（见 [017]、[018]），进程资源不能跨进程恢复（见 [019]），已受理记录可以重读但旧版本不授权今天的动作（见 [016]）。

源码用 `/// DSL-class:` 注释声明每个类型属于哪一类，现行类别是 `Vocabulary`、`Evidence`、`Decision`、`DurableFact`、`PhysicalHandle`、`ExternalSignal`；名称相似但只描述工具词汇的类型（如 `JsCapability`）按正向标注归为 `Vocabulary`，判定依据是声明本身，不靠名称 allowlist。

【程序待改】原条款写的六分类里 `Witness` / `Capability` / `Receipt` 三个类别在程序中不存在，也没有消费它们的分类器；`scripts/checks/js-surface-manifest.mjs` 的 `validateSurfaceManifest` 只登记 module/source/laws/representation/kind，不校验类别。建议二选一：把 `DSL-class` 纳入实际校验（缺失或错型声明即失败），或者删掉 `tests/013.test.mjs`、`tests/014.test.mjs` 挂着的六分类与 manifest 锚点待办，不要让文档替不存在的分类器背书。

## [014] 每项敏感权威只有一个发行者，消费点按当前 subject/版本拒绝错配

每个敏感权威值由它的真实 owner 单点产生，消费者只在自己那一处核对：`QuiescencePermit` 只由 `SessionQuiescenceGate.ObserveIdle` 产生，跨 gate 或伪造外形返回 `WrongOwner`；`ManagedChatAcceptanceWitness`（承载 `ChatExecutionKey` 与已受理 evidence）只由受理事务产生，subject 不符返回 `AttemptKeyMismatch`、同 subject 版本不符返回 `EstablishedEvidenceConflict`，两者都是零追加、零效果。拒绝一律是带类型的返回值，不靠 name-only allowlist、baseline 或 suppression。

【程序待改】原条款要求的「manifest 以 exact file + symbol + source/proof anchor fail closed」这套装置不存在（见 [013]）。这类锚点清单建议删除；它想保证的性质已由 owner 单点构造加 typed 拒绝在代码结构里承担。

## [015] 权威载荷绑定当前 subject + version/sequence + 必要的内容摘要

许可与受理载荷必须点名它绑定的 subject（如 `SessionId` + `PhysicalUserMessageId`）、版本或序列，并在该边界具有内容身份时带上能区分内容的摘要。这类载荷本身不驱动 append/write/send/execute：动作发生前，消费者要针对当前 subject、当前版本/序列与当前摘要再判一次，判定通过才有效果。

## [016] 过期值只能在当前事实下重新准入

已记录的受理或许可可以说明过去发生过什么，旧版本不授权当前的动作。subject、版本、序列或摘要一变，旧值即被拒绝（`WrongOwner`、`Superseded`、`NoFreshIdle`、`AttemptKeyMismatch`）；如果当前事实再次满足条件，由 owner 从当前观察产生一份新的准入。恢复路径不得藏起执行位置，也不得拿历史记录跳过普通准入。

## [017] multiplicity 明示且一次性机会不可复制消费

每个许可合同声明自己是一次性、exact-N、集合/闭包，还是可重放的记录。一次性许可被成功消费或明确释放后，该次机会即关闭；同一份记录被重复读取不等于重复执行效果。

## [018] QuiescencePermit 由 ObserveIdle 唯一发行，消费/释放返回 typed failure

`QuiescencePermit` 绑定 opaque gate owner identity、`SessionId` 与 attempt serial；只有 `SessionQuiescenceGate.ObserveIdle` 可发行。`TryConsume` 与 `TryRelease` 均返回 `Result<unit, QuiescencePermitFailure>`，`QuiescencePermitFailure = WrongOwner | NoFreshIdle | AlreadyConsumed | Superseded | Revoked`：跨 gate 为 `WrongOwner`，重复消费为 `AlreadyConsumed`，新 attempt 建立后旧 permit 为 `Superseded`，revoke 之后为 `Revoked`，drop 或无 eligible idle 为 `NoFreshIdle`。每个拒绝分支零效果。JS surface 只投影稳定的 typed result view，permit 自身保持 opaque。

## [019] process capability/permit/PhysicalHandle 非耐久；重启后由当前事实重新准入

进程 capability、permit 与 `PhysicalHandle` 不进入 Fact/Event、journal codec、JSON 或任何跨进程恢复载荷；对活 permit 直接 `JSON.stringify` 即被拒。崩溃后先恢复 durable 记录，再经 owner 的正常准入得到当前进程的新许可；不持久化 capability，不读特性历史反推能力，不设隐藏恢复位置。Quiescence 恢复必须重新走完当前物理 attempt 的 `ObserveIdle`，不消费崩溃前的 permit。

## [020] 配置不变量 fatal 使用 mandatory injected fuse

此边界不把普通可预期 admission rejection 升级为 fatal。

## [023] DevOps 角色固有直接源码修改与测试编写权能，禁止 allowRepair 等逐次授权开关

DevOps 原生具备完整的本地工程文件操作能力（Read、Write、Edit、Glob、Grep、Move、Remove）与真实执行能力（Exec），经 `todowrite-for-devops` 编程面（`cat`/`ls`/`stat`/`where`/`whereAll`/`mkdir`/`write`/`mv`/`rm`/`run`/`join`）统一投影，不另设直接文件 Host 工具。验证过程中遇到非架构级缺陷时，DevOps 可以直接调查、修改源码、补回归测试并重新验证，不需要逐次取得批准。系统不引入 `allowRepair`、`managerApprovedMutation`、`canFixAfterFailure` 这类逐次审批参数，提示词里也不得写「只有 Manager 明确允许才可以改源码」这类暗门。

## [024] Fork 与 Resume 权能分离：Manager fork 仅准入 Engineer，固定 DevOps 只经 resume 续做且同一道路至多一个活跃工作单元

`fork` 与 `resume` 在模型参数、生命周期与门禁上严格分离：

- `fork`：只允许 Manager 创建新的独立 Engineer 子会话；fork DevOps 或 fork 其它角色在门禁被拒绝。
- `resume`：用于 Manager 续做既有固定 DevOps 道路；同一道路同时至多一个活跃的 DevOps 工作单元。DevOps 忙时明确拒绝新的 assignment，不把新任务伪装成 nudge，也不通过新建操作员绕过忙碌状态。

## [025] Manager 程序入口常开，仓库访问仅限 Review

`todowrite-for-manager` 对已确立身份的 Manager 固定可见，不因评审阶段、末任或清理窗口而整票拒绝；它仍受身份、取消、等待与存储等各自独立边界约束。仓库访问只经 `this.reviewOnly.cat/stat/ls/rg` 暴露，无顶层别名；每次操作在 Host 访问仓库前核验当前任期仍活跃、评审尚未接纳、无清理阻塞且未冻结，末任同样如此。保存对象或方法引用、挂起恢复与插件重开都不复用过期授权。拒绝返回稳定机器码 `REVIEW_ONLY`，不读取目标文件、不启动 rg。

`where`/`whereAll` 只处理已有文本，保持顶层；普通计算、view 声明与生命周期成员不受评审阶段限制。委托、收拢、交付各守自身权限。空账首评审或 `Promoted` 只表示具备最终交付资格，不表示任务已完成：活跃的末任 Manager 仍持有 `Fork`（派 Engineer）、`Resume`（续派固定 DevOps）、`Join`、ReviewAssessment 与 Finality 权能，不凭 `IsFinalIncumbent` 或空 findings 静态拒绝这些收尾动作。无活跃任期、评审未接纳、资源 blocker 与用户问询等待仍按各自独立门禁拒绝。其它角色保持原有直接成员与既定权限，共用同一套底层文件实现，不另设 Manager 专用文件引擎。基类、描述与示例使用同一命名空间，评审前后工具定义一致。

Manager 不借用 `todowrite-for-engineer`/`todowrite-for-devops` 绕过能力边界，跨角色调用在门禁被拒。

`Join` 与 `Horizon` 按已确立的角色与当前进程的子任务归属准入，不依赖 Work 活跃任期、Review 接纳、清理 blocker 或退任冻结。首次 Review 之前与收尾期间都要能观察、等待、收取已建立的子任务；顶层工具与程序成员不因是否经过 Work 门禁而给出不同结果。角色未定、用户问询等待、消融、子任务恢复与归属边界仍然有效；观察与收拢不创建 Work 任期，也不授予新派工权限。

## [026] Review 接纳前禁止向固定 DevOps 派工

当前迭代的 Review 被系统接纳前，Manager 不得向绑定的固定 DevOps 派发任何任务。对未接纳评审前向 DevOps 派工的拦截与拒绝，发生在任何持久化副作用之前。既有只读 Engineer 的合法 `resume` 与 `fork` 只读调查不受影响。

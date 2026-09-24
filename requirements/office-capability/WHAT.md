# office-capability — WHAT

## [001] office 由 entitled consequence 定义，不由 persona 名 / 工具名 / 权限清单定义

Office capability 由该职位有权产生的后果（Entitled Consequence）定义，严禁以 persona 名称、工具可达性或权限矩阵清单作为能力事实。调用方必须依据 Office 的承诺与后果进行认知和委托。

## [003] 同一 office 的 authority 不变

同一 Office 的权能与权限集合保持完全一致。每个 Role 恰对应一个 Persona，不存在 fast/deep 档位或组合名解析语义。当前执行事实（证书、清理阻塞、已接纳评审、退任冻结）可以按既有门禁收窄一次具体动作的准入，但不扩大 Office entitlement。

## [004] capability 是 consequence model，不是 tool whitelist 的口语转写

Office 能力是纯粹的后果模型，权限矩阵仅是其在执行层的投影。严禁从工具清单反向推导 Office 定义，亦不得将后果模型降格为具体工具列表的口语表述。

## [005] 单一语义所有权、多处投影：consequence 在所有决策面同 ID 命中，不得漂移

同一条 Entitled Consequence 会同时投影到 Manager Role Law、fork/resume 工具描述、各 Office 的自我模型以及调用方边界镜像中。各处投影文案可以因语境调整，但语义内核必须同源一致，严禁出现分叉。

角色合并必须重写完整工作链，不能只换名称或在旧说明前追加新规则。共同法、角色自述、派工与接力提示、工具说明、错误提示、案例整理、输出截断、纪律提示和模型配置都必须讲述同一套当前职责。中英文同时生效。

Engineer 的调查不包含任何真实命令执行，即使命令只读；只读调研是本次任务约束，不是另一个角色。DevOps 的普通修复允许既定需求内的工程判断，不以「只有唯一机械操作」或逐次批准为前提。Manager 必须区分 Engineer 完成、DevOps 运行和自修后的重新验证、自己作出的验收判断。

非当前合法活跃角色的可加载提示词、工具建议、别名或模型池不得留在 resources/。普通浏览器测试与一般探究用语不属于此类，不作关键词式误删。资源回归须覆盖实际分发资源及其调用接点，而非只检查角色目录。

## [006] offices 不可互换：禁止把 office 当可互换通用 agent

各 Office 具备不可替代的领域边界：Engineer 不是真实命令执行器；DevOps 不是架构/产品决策者，亦不负责差遣其他代理；Manager 不承担直接源码实现；仅允许在当前独立评审中使用评审专用只读工具直接取证；Sphinx 是程序工作流而非通用代理。

## [007] Manager 无普通工具：原生读查与写入拒绝；评审专用只读工具限未接纳评审

Manager 的核心权能是统筹、委托、评估与集成；原生 read/grep/glob 与一切写入/终端对 Manager 始终拒绝；评审接纳前仅可使用评审专用只读工具 js-manager，接纳后该工具同样拒绝；禁止 Fission；不提供评审专用只读工具的副本语义。

resume 的同道路续做语义归 delegation-003、delegation-024 所有。「DevOps 只能通过 resume 调用」不等于「resume 只能用于 DevOps」；续做不能改变已有角色、已绑定配置或控制权，也不能创建替代 DevOps。

## [011] Manager consequence = 完整管理与编排权能；non-consequence = 亲自修改工作树与使用 Fission

Manager 在任何活跃任期阶段（包括评审前与接责后）均具备从一而终的完整管理与记账权能（Fork, Resume, Join, Horizon, TodoWrite, ReviewAssessment, Finality）。Manager 拥有权能并不等同于已有执行任务，有能力无任务完全合法。Manager 严禁亲自修改工作树、使用 Fission；评审未接纳前的只读取证是唯一合法直接调查窗口，评审接纳后即关闭；完整管理权不等于任何时刻都可以向固定 DevOps 派工——Review 接纳前不得向 DevOps 派工，已有只读 Engineer 的合法 resume 不受影响。

## [012] Orchestrator consequence = commission manager；不 commission 其它 office

Orchestrator 仅负责为顶层道路委任或接续 Manager，不直接委任其它子级 Office，亦不直接介入具体的微观执行，不使用 Fission。

## [015] Predictor 是内部机制专用角色，不参与普通调度与工具门禁

Predictor 仅为 Strength 降级指定廉价 provider/model，不进入 Manager 公开 fork 候选、不参与普通 participant 调度、不拥有工具门禁规则、不暴露给用户可见接口。Predictor 只在内部强度机制中使用。

## [016] Engineer consequence = 本地事实调查与源码工作；non-consequence = 真实命令执行 / 差遣 DevOps / 外部网络浏览；独享 Fission

Engineer 专注于本地事实调查与源码工作：
- **Entitled consequence**：读取、创建、修改、移动、删除仓库文件；完成业务与架构实现、重构；编写测试源码。
- **Non-consequence**：不执行真实构建与测试命令，不调用或差遣 DevOps，不承担外部网络浏览职责。完成本次工作或到达需要 Manager 决策的边界时立即返回。
- **Fission 独享**：Engineer 是全系统**唯一具备 Fission 角色能力**的 Office。Fission 仅代表同一 Engineer 的多个 execution lane，必须收敛为一次返回。

## [017] DevOps consequence = Engineer 全部本地工程能力 + 真实执行/终端/进程管理 + 角色固有非架构级修复授权；non-consequence = 发明架构/产品含义 / 削弱验证 / fork 或差遣其他代理 / Fission

DevOps 拥有完整的运维执行与直接工程修复权能：
- **Entitled consequence**：拥有 Engineer 的全部本地工程能力（文件读、写、改、移、删）；拥有真实命令执行（Exec）、终端与进程管理（Pty）；拥有**角色固有的非架构级修复授权**——在执行中观察到非架构级缺陷或测试失败时，应自行调查、直接修改源码、补充必要回归测试并重新验证，无需 Manager 逐次授权，亦不受任何 `allowRepair` 式开关限制。
- **Non-consequence**：不发明架构、产品含义、兼容性或安全政策；不通过削弱断言或绕过门禁制造成功；到达架构与产品边界时交回 Manager；自身不 fork、不 resume 其他代理，不使用 Fission。

## [018] Sphinx consequence = 程序控制探究流程、工作项、预算、续行和收束；同步调用标准 Engineer；non-consequence = 充当独立 Role/Persona / 拥有独立 Fission 身份 / 保留 Inquiry 驾驶层

Sphinx 是完全由程序控制的探究流程（epistemic workflow）：
- **Entitled consequence**：程序驱动探究步骤推进、工作项决策、预算控制、续行与收束；通过唯一 `sphinx(question, expectTurns?)` 工具及直接 `/sphinx question` 命令调用，同步使用标准 Engineer 获取事实与完成工作项。内部 Engineer 的文件修改、工具及 Fission 能力直接来自标准 Engineer 权限，受相同的资格与 authority 检查。
- **Non-consequence**：Sphinx 不是 Role、Persona 或普通 subagent；不拥有独立 Fission 身份，不占用额外 session 层级；不额外授予 Engineer 真实执行或 DevOps 调度权；不设立中间模型驾驶层，不另造只读 Engineer profile。

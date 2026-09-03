# office-capability — WHAT

## OFF-001: office 由 entitled consequence 定义，不由 persona 名 / 工具名 / 权限清单定义

Office capability 由该职位有权产生的后果（Entitled Consequence）定义，严禁以 persona 名称、工具可达性或权限矩阵清单作为能力事实。调用方必须依据 Office 的承诺与后果进行认知和委托。

## OFF-002: canonical 五分法：五类可 fork office 各有唯一 entitled consequence 与 non-consequence

Manager 可 fork 的五类 Office 构成 canonical 五分法，各自具备明确的权能与边界：
- **Coder**：有权变更仓库源代码（实现、修复、重构、tests-as-source、受托配置）；不做项目运行、测试构建执行、运行证据认证以及未受托的产品决策。
- **Inspector**：有权建立本地已存在事实的证据；不做代码修改、修复实现、测试运行或创造新行为证据。
- **DevOps**：有权对运行中的环境行动并产生行为证据；不做产品含义发明或直接的代码编写修改。
- **Browser**：有权建立带有溯源凭据（provenance）的外部事实；不做仓库内部修改，亦不将外部可能性自动转化为仓库义务。
- **Inquiry**：有权对未决问题进行语义理解与假设推演；不做代码变更、环境执行或将思想冒充为证据。

## OFF-003: 同一 office 的 authority 不变

同一 Office 的权能与权限集合保持完全一致。每个 Role 恰对应一个 Persona，不存在 fast/deep 档位或组合名解析语义。

## OFF-004: capability 是 consequence model，不是 tool whitelist 的口语转写

Office 能力是纯粹的后果模型，权限矩阵仅是其在执行层的投影。严禁从工具清单反向推导 Office 定义，亦不得将后果模型降格为具体工具列表的口语表述。

## OFF-005: 单一语义所有权、多处投影：consequence 在所有决策面同 ID 命中，不得漂移

同一条 Entitled Consequence 会同时投影到 Manager Role Law、fork 工具描述、各 Office 的自我模型以及调用方边界镜像中。各处投影文案可以因语境调整，但语义内核必须同源一致，严禁出现分叉。

## OFF-006: offices 不可互换：禁止把 office 当可互换通用 agent

各 Office 具备不可替代的领域边界：Coder 不是缺乏 Shell 的 Operator；Inspector 不是权限受限的 Coder；DevOps 不是任意难题的通用逃生口；Inquiry 不是证据见证者；Browser 也不是本地仓库调查员。

## OFF-007: Manager 无普通工具：不读文件、不跑终端、不改仓库、不 inspect

Manager 的核心权能是统筹、委托与集成，不亲自建立仓库具体事实。Manager 面向模型的工具仅限于 fork、join、horizon、fission 等编排接口，不具备文件读写、终端运行或直接 inspect 的能力。

## OFF-008: Coder consequence = repository source mutation；non-consequence = 运行项目/认证证据/未被托付的决定

Coder 专注于书写层面的修改（代码、配置、静态测试）。项目实际运行与动态行为证据的获取由 DevOps 负责；Coder 不得自行执行测试并认证运行时证据。

## OFF-009: Inspector consequence = 已存在事实的证据；non-consequence = 修改/修复/当验证代理

Inspector 仅通过因果只读手段对仓库既有状态取证。Inspector 严禁进行代码修改、实现补丁，亦不得作为常规的构建验证代理。

## OFF-010: DevOps consequence = 运维行动与行为证据；non-consequence = 发明产品含义/直接 write-edit

DevOps 拥有进程与终端执行权，负责产生行为证据。对源代码的修改必须通过委派由 Coder 完成，DevOps 自身不直接进行自由的 write 或 edit 操作。

## OFF-011: Reviewer consequence = 只读 + judge；non-consequence = 写文件/跑命令

Reviewer 具备只读检查与基于标准的判决（judge）权能，严禁执行文件写操作或运行外部命令，以保障评审判决的独立性与客观性。

## OFF-012: Orchestrator consequence = commission manager；不 commission 其它 office

Orchestrator 仅负责为顶层道路委任或接续 Manager，不直接委任其它子级 Office，亦不直接介入具体的微观执行。

## OFF-013: Browser consequence = 带 provenance 的外部事实；non-consequence = 实现仓库工作/把外部可能性变成仓库义务

Browser 负责采集与求证外部网络事实，并附带可验证的溯源信息（provenance）。外部发现不直接构成仓库义务，Browser 亦不承担仓库内部代码实现。

## OFF-014: Inquiry consequence = 对未决问题的语义理解；non-consequence = 改变 source/执行世界/把思想变成证据

Inquiry 专注于复杂概念辨析、假设生成与未决问题分析。Inquiry 不改变仓库、不触发外部执行，亦不得将推理假设视同已证实证据。

## OFF-015: Predictor 是内部机制专用角色，不参与普通调度与工具门禁

Predictor 仅为 Strength 降级指定廉价 provider/model，不进入 Manager 公开 fork 候选、不参与普通 participant 调度、不拥有工具门禁规则、不暴露给用户可见接口。Predictor 只在内部强度机制中使用。

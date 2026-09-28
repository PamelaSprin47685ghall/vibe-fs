# participant-identity — WHAT

## [001] logical run 的唯一身份

每个 durable logical participant run 恰有一个私有强类型 `ParticipantIdentity`，原子拥有 Role、稳定 Persona 及其 provenance/version。其他包不得分拆拥有或独立改写；身份在 exact run 内不可变，不以 SessionId 生命周期为作用域。

Role 是本名词汇（Manager/Orchestrator/Engineer/DevOps/Blogger），每个 Role 在运行时恰对应一个 Persona（如 Engineer 为 Engineer、Manager 为 Lead、Orchestrator 为 Director、DevOps 为 Operator、Blogger 为 Chronicler）。Engineer 是独立身份，其权能由 office-capability 定义；活跃解析范围见 010，内部身份见 007。

## [002] ParticipantIdentity 与 ExecutionBinding 分离

IdentitySeed 确立 run 内不变的 Role、Persona、SelectedAgent 证据。ExecutionBinding 分配具体物理执行的 model target、租约和容量栅栏，精确绑定 `session + physical + Role + Participant + target + fence`。

fresh physical execution 按固定 Role 经 execution-model-routing 的调度契约取得绑定。重试、Strength 或其他执行调度不把目标与租约回写为身份，也不得轮换角色。

## [003] 身份解析与原子安装

run 建立时，身份所有者按 Role 与 Persona provenance/version 解析一次完整 `ParticipantIdentityEvidence`，包含 Role、Persona 和 SelectedAgent。它只能随同一个 `AuthorityRootAccepted` 原子安装，不能先装身份再接受 root；写入失败则二者均未安装。相同接受载荷重放幂等，run 内不同载荷拒绝。

## [004] 换执行者不换人

Retry、Strength 和援助升级只改变物理执行目标及租约。执行中暴露的 Role、Persona、SelectedAgent、provenance/version 必须逐字段等于该 run 的持久身份证据，不被 provider/model 或租约覆盖。

## [005] 身份 prompt 的来源

system prompt 的身份标识只来自 ParticipantIdentity 的 Role 与稳定 Persona，不从 provider、model 或执行绑定推导；物理执行切换不改变同一 run 的身份标识。

## [007] 内部身份

Bookkeeper、Predictor 的内部 logical run 也由同一原子身份模型约束，拥有稳定 Persona，不另设 Persona 缓存。内部 Role 不进入公开 Role 联合类型、普通调度、公开工具门禁或 Manager 的 fork 候选；Predictor 仅用于 Strength，Bookkeeper 仅用于案例维护。

## [008] 派生身份的精确证据

child、attached 和 InternalLeaf 的 root 必须携带身份所有者为 exact run 签发的 owner-derived evidence，由 IdentitySeed 原子命名 OwnerLogicalRunId、LogicalRunId、Role、SelectedAgent、Persona 及 provenance/version。owner、run、root acceptance 全部匹配后才能安装；错误归属、错误 run 或字段缺失均安全失败。

继承关系只由该证据证明，不从 Session 缓存、Host parent 或物理拓扑推断、补全或重解析。后续执行目标仍由模型调度契约独立决定。

## [009] 关闭旧 run 后才可复用容器身份

同一 SessionId 有未精确关闭的 run 时，不接受 fresh root 或不同身份。仅在 interaction-authority 为 exact `(SessionId, LogicalRunId, AuthorityRootId)` 持久化唯一 `AuthorityLogicalRunClosed`，且同一 fold 释放 active identity binding 后，才可通过新的原子 `AuthorityRootAccepted` 安装新身份。

新身份不继承旧 run 缓存。lifecycle terminal、解除关联、时间、idle/timeout 或 Host 观察均不能单独证明 closure。

## [010] 活跃解析与历史解码隔离

活跃身份解析（名字解析、新建 root 等）只接受 engineer、manager、orchestrator、devops、blogger；其余身份不进入活跃调度。历史身份只在历史事件、日志及归档边界解码，读取和恢复不能把只读历史身份升级为有源码写权的 Engineer，也不能把 DevOps 解析为有 Fission 权的 Engineer。

## [011] 持久化角色标签稳定

持久化角色标签由规范 role catalog 唯一确定，不随内部类型或代码重命名漂移。

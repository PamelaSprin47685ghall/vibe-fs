# Changelog — 版本历史

## Unreleased — Manager 循环 clean cutover

- 增量编译证明的临时工程改用显式 subsystem／compile-shard，去掉旧 owner／locality／kind 夹具参数。保留精确影响集合、合并编译、缓存及 CLI 断言；实现错误扩入反向消费者和签名遗漏反向消费者的两个反例均被拒绝，生产 planner 与工程不变。

- 生产 compiler-boundary 证明统一读取现有 subsystem inventory，移除重复 fsproj 扫描、XML 引用解析和 GitGateway 的旧 locality 标签断言。保留 GitGateway、NodeFs／工具合同、request kind／fallback facts 的源码与依赖边界；显式元数据正例、错误 subsystem 与缺失物理 provider 反例通过，planner fixtures 和生产工程不变。

- Delegation 编译边界证明复用 subsystem/compile-shard inventory，移除旧 kind 与 owner 文件名筛选依赖，验证真实 subsystem 归属。保留 DELEG-028 明文预算、增长 ratchet、物理隔离与必要 provider 断言；显式元数据正例及错误归属、Process 依赖反例通过，生产工程与合同不变。

- Host 闭包测试复用 compile-shard/subsystem inventory，移除退役 locality/kind、数量预算与 legacy owner 文件筛选；保留真实依赖与隔离断言，覆盖显式 Host 分片和 runtime-platform 摘要归属。显式声明正例及工具能力泄漏、错误归属反例通过，生产工程与行为不变。

- 按用户裁决同步 HOST-BOUNDARY-026：独立列出消息、SDK 类型、终端合同与工具适配器，摘要原语归 runtime-platform；普通业务契约按实际知识消费窄合同，不再受两个旧名称限制。保留物理能力隔离、唯一实现和失败语义；未修改源码、工程或验证阈值，残余 Host legacy 验证缺口记入 GAP-033。

- 将既有 ToolHostCodec／ToolHostSurface 独立为 Host 工具适配分片，工具消费者不再为参数解码、schema 与注册编入信号路由和终端总线；bootstrap 显式装配工具与信号两侧。保留所有源码、公开签名、取消释放、输出截断与 aggregate 顺序，不复制物理实现。

- 终端事件合同不再传递 SDK 类型、MessagePart 或摘要实现；SessionSnapshot 显式引用唯一消息合同，诊断与消息可见性移除不使用的终端引用。补齐 signal adapter 原本漏报的 failure、chat-execution 与 RuntimePath 静态依赖，清除失效 namespace 引入；保持终端重放、精确 authority、取消与公开签名。

- 将既有 OpencodeTypes sibling 源码独立到零引用的 Host SDK 类型分片；OpenCodeContract 与模型路由直接消费它，不再为 OpencodeModel 引入终端事件和 MessagePart。保持 SDK wire 类型、模型投影、公开签名与 aggregate 顺序；既有编译边界回归拒绝两条 consumer 恢复宽 Host 引用。

- 将既有 MessagePart sibling 源码独立到零引用的 host/message contract 分片；HostMessageCodec 直接消费它，不再编入无关 SDK DTO、终端事件和摘要实现。保持消息 union、decoder 行为、全部公开签名与 aggregate 顺序；编译闭包回归明确拒绝重新引入宽 Host 引用。

- 删除无调用方的 OpenCode GitTree 适配器与 GitTreePort，保留 EventStore GitTree 和 Relay snapshot；JoinAttemptRegistry 独立为窄 delegation 分片，补齐 PluginSessionScope 的真实静态依赖，不再靠聚合编译补入 registry。

- Provider wire decoder 与 Git hook 分片直接引用 runtime-platform/digest，去掉未使用的 OpenCode 消息／事件合同闭包；保留媒体 URL 摘要、Git common-dir socket key、用户 SSH 配置、全部源码与公开签名。

- Change 事实分片直接引用 runtime-platform/digest，不再为 RuntimePath 的工作区摘要引入 OpenCode 消息／事件合同；保留 Git common-dir、XDG fallback、事实投影与全部公开签名。

- Requirement Grounding 模型与 Relay workspace snapshot 直接引用 runtime-platform/digest，去掉未使用的 OpenCode 消息／事件编译输入；保留规范材料与 package 摘要字节、Git snapshot canonical 输入和全部公开签名。

- Institutional Learning 的事实／Enhancer 分片直接引用 runtime-platform/digest，不再通过摘要取得 OpenCode 消息、事件和角色闭包；保留规则版本输入、学习 disposition 与冻结重放语义。

- 将既有 UTF-8 字符串 SHA-256 原语抽到无领域引用的 runtime-platform/digest 编译分片；Casebook 与 Sphinx 删除重复 crypto 实现，Sphinx 全部调用方迁移并删除旧摘要导出。摘要输入、canonical JSON、事件身份、salt 与 Casebook null 语义不变；二进制 SHA-1／SHA-256 不合并。真实捕获测试改为独立固定摘要，删除只测试测试内 crypto 的伪 Host 证明。

- CompletionMailbox、Change VerdictMailbox 与 HostForkJoin 的等待竞争改用 typed Choice，删除手写数字标签和无类型结果字段解码；保持单次 Promise 映射、注册顺序、drain-first 与局部中断语义。补充真实 verdict mailbox 的优先级、waiter 释放和有界 FIFO 证明，删除误称等待证明的重复 renderer／源码 token 检查。

- ToolHostSurface 的 schema 解包移回 ToolHostCodec 的 internal typed 合同，删除 Fable 私有表示探针，避免把原生 schema 的 `.value` 错解为返回值；保留私有 HostSchema 构造器。删除迎合旧解包的 mock 形状测试，改以真实 SDK validator 和旧败新胜的 literal schema 反例验证。

- Prefix Wire 的 Replica 识别与 Authority 检查改用既有 StrengthRuntime／StrengthReplicaBinding 合同，删除私有字典路径、无类型字段读取和异常吞没；Fallback Workflow 改用 SessionAssociationProjection 的类型安全查询，删除 Map 扫描与手写 union tag。两处补齐真实静态依赖，不改变重试预算、前缀选择、材料等待或 Replica 权限策略。

- CanonicalIntegrator 的 Casebook／JS transaction oracle、需求接地 glob 匹配和 repository 观察 Surface 恢复静态 typed 调用及真实编译引用，删除动态模块加载、编译器 union 布局解码与缺失模块 fallback。Generator 的既有 `typedRole` 合同公开给真实 composition consumer，不复制生成流程；接地失败词汇、观察失败不阻断已提交修改、事务 Current 与重放语义保持不变。

- RequirementGroundingTransform 与 PromptResources 恢复对既有消息投影、ProviderResources 的静态 typed 调用及真实 ProjectReference，删除动态加载、手写成功 union、空资源与静默跳过校验的 fallback。规范重放证明改为比较冻结终端结果字节，验证磁盘内容变化不会改写历史，新的内容版本仅追加；删除只匹配入口符号的伪证明。

- 修复成功 retry 的后续 tool step 因新增 Blogger coverage 再次选择 prefix probe、造成未声明冷边界的问题。候选资格读取已结算的连续失败计数，零失败保持 committed prefix 并跳过候选物化；已有 frozen plan 保持不变，新失败仍可恢复 probe。

- 修复 HostSignalBootstrap 动态查找 LoopSensor 失败时静默丢失退化保护：静态构造并装配真实 sensor，reset／observe／drop 全部使用 typed 合同，复用异常类别资源映射。真实插件回归证明 managed child 重复流触发一次物理中断，root 与 foreign session 保持豁免。

- ToolRegistry 与 PluginHooks 恢复静态 typed 工具注册、Casebook 门禁／观察捕获／工具接线，删除动态模块查找、备用权限判定和静默漏注册。JS 事务持久化能力从 PluginHostInterop factory 到 registry 全程保持 `IJsTransactionPersistence`；真实 consumer 编译补齐 ExecutorTool 已有 Distillation 依赖，不以丢失持久化或隐藏引用冒充解耦。

- Host provider 校验、chat admission 绑定／释放和证明 Surface 恢复对 `SessionExecutionBinding` 的静态 typed 调用，移除动态加载、手写 union、静默 no-op 和虚假零值；SyncDelegate 证明入口直接构造并执行真实 Inspector tool，不依赖生成 JavaScript 的构造器布局。真实 consumer 并集编译同时清除 JoinSurface 的陈旧 Manager namespace 引入，保留 exact settlement、child 复用与 bounded WorkRecord 语义。

- Fork WarmStart 与 Prefix WorkRecord 恢复静态 typed 依赖，删除动态加载、union 解码和默认成功／备用渲染路径；保留 WarmStart 查询级 fail-open、无关键词零工作、同 session Opening 排除与 frame 读取失败语义。移除只匹配源码注释的 Opening 伪证明，记录真实行为证明的覆盖边界。

- Coder WarmStart 恢复静态 typed 调用，保留查询级 fail-open，移除 adapter 动态加载、union 解码与 catch-all；Blogger context 构造归入 MainContext，删除旧 Enforcer Host 和 Recovery 副本。Coverage 出生门测试改走真实 trace fold／生产函数，补足无法映射与同 turn 推进的反例，移除虚假 precheck 证明；JoinGuard 证明 adapter 独立编译并显式声明实际依赖。

- `ToolRuntimeScope` 的 Relay 查询改用既有 typed `RoadView`，移除 JavaScript Map／union 布局探针；退休围栏以 `IncumbencyId` 存储并暴露，保持 assessment、证书三项绑定、同任期冻结与新任期清除旧围栏的语义。

- 恢复 `ToolRuntimeScope` 对 `OrchestratorHost` 的静态构造和 typed dependencies，消除 `createObj + box` 抹掉回调调用约定后触发的 `computation.then is not a function`；工作区快照保持 `WorkspaceSnapshotId`，取消与卸载直接调用真实 Host，不再动态查找模块或以默认成功掩盖缺失。

- 恢复规则书校验与 Context fact fold 的静态类型依赖，移除动态模块查找、手写 union tag 和校验缺失时的默认成功；将 Nudge、Enforcer repair、provider system transform 分成可由真实 consumer 独立编译的窄分片。
- authority gate 分离源码 subsystem 身份与 WHAT package 归属，退役 legacy 身份别名；结构测试改用唯一归属、合法增长、真实 shard DAG／subsystem SCC 和平台隔离的正反例，不再锁住迁移数量快照。
- 修复已有 execution parent binding、共享 parent cache 尚空时，真实子会话被误判为根会话并禁用 Fission 的问题。请求投影与父关系发现使用一致证据；真实 chat hook 回归同时保留根请求和 `/continue` 的 origin deny。

- 证明缺口与测试错误分离：缺少 active test/HOW proof edge 继续输出 GAP，保持 OPEN/PARTIAL，不再导致 check 或 meta-verifier 失败；已有证明的悬空引用、非法归属与真实断言失败仍严格报错。共享 proof graph 不伪造证明边。

- 删除独立 FCS 扫描器、compiler-dependent extractor/report CLI、扫描计时命令、专用 fixtures 与所有 compiler-evidence 消费分支；DSL、authority、decorator、owner-contract 检查恢复纯源码路径。187 项针对性回归通过；真实 check 越过原挂点并在证明追踪缺口处正常失败。STRUCTURED-WORKFLOW-013 的完整新合同证明继续保持 GAP-031 OPEN，不以删除代码代替证明。

- 修复 fork/resume 将正常本机派发回执误报为“不确定”：派发成功且 Submitted 已持久化即返回已承接，不等待 PhysicalAccepted 或 child completion；后续真实消息仍负责绑定 Authority Root，真正发送结果未知时保留恢复权且不重发。

- 全仓自建 FCS 扫描禁令（规范修正，取代下条旧 FCS 优化 rationale）：仓库自建 FCS（FSharp.Compiler.Service）扫描在全仓任何位置一律禁止——直接调用、wrapper/reflection/fsx 封装、typed AST / symbol / application / inferred type / source-edge 提取等任何形态，whole-tree、focused/locality、fixture、report-only、CLI、CI、prebuild、cache/snapshot/delta/reuse/externally supplied evidence 等任何执行入口，均不得作为验收证据、门禁手段或临时 report lane；Fable 内部正常编译与纯源码文本静态门禁不受影响，但不得为提取证据而加做额外/instrumented 编译。F# 执行依据为声明式 ProjectReference DAG 与精确编译闭包、sibling `.fsi` 与普通 Fable 签名/私有可见性编译 canary、已注册行为证明；C(W) 只含 direct byte-backed 的 explicit interop 与 JS/generated observations（见 VERIFICATION-SYSTEM-001；STRUCTURED-WORKFLOW-011 载有同规则并回指 001；`requirements/GAP.md` GAP-031 已同步改写为 PARTIAL 缺口记录）。下条“优化 check 的 FCS 路径”保留为历史记录，其优化思路已被本禁令取代，不得再作为批准依据；现存 FCS producer/consumer/test 及引用 FCS 证据的过期 schema、测试与文档断言尚未移除，属未解决的非合规缺口。

- 优化 check 的 FCS 路径：DSL 只提取完整 declaration/application evidence，不执行无消费者的 capability 类型递归分类；逐文件 checker 调用改为一次 implementation project check，反射属性元数据按类型复用。完整 report 保留原有分类覆盖，真实 compiler fixture 精确验证两条提取路径的证据等价。

- 执行故障链收敛为单一 `ExecutionFailureResolution`：删除可组合出非法状态的 retry/fallback/message 三轴，F# CE 每回合只返回一个互斥恢复或终结动作；provider fallback 以 exact `ProviderRunIdentity` + durable authorization 去重，managed-chat 不再成为第二 retry owner。Reconciler 的 `TurnFailed` 强制等待匹配的 typed physical witness，idle/retry 抢先到达不能裸终结；delegated completion 维持 first-proven-terminal 单次赋值。Long Strike 现连续注入两个非重试 provider failure，证明两次独立 durable recovery、第三 provider 成功、无并发 terminal exhaustion。
- Host failure presentation 明确边界：恢复期 Wanxiangshu 不额外产生 final presentation，耗尽后仅一个 typed terminal；OpenCode 1.18.29 的 post-publication plugin event 无法拦截上游原始 `session.error`，不再声称虚假 UI suppression。依赖同步至当前解析版本：`@fable-org/fable-library-js` 2.6.0、`@opencode-ai/plugin` / `opencode-ai` 1.18.29、`zod` 4.5.4；lockfile 与 Host compatibility fixture 同步。
- Manager baton/successor 模型按 clean cutover 退役，无别名与兼容路径：删除 `BatonSource`、`BatonId`、`ProjectionCutId`、`BatonEnvelope`、`ActiveSource` 与存储态 `OpenObligations`，删除 `SuccessorRequested` / `SuccessorActivated` 与 `Decision.activateSuccessor`；`Decision.openIncumbency` 不再接受 source 参数，初始开启唯一经真实权威接受后的 Plugin `BeginPhysicalProviderAttempt`，Change Host 不再伪造 `PhysicalUserMessageId`。
- 新循环语义：每一轮 Manager 都在共享工作区上从权威用户消息重新开始并独立评估，评审后指派的修复由本轮承担，完成后清理资源并退出；是否开启下一轮只由系统裁决 `RetirementOutcome = Continue | Accepted of QualityCertificateId`，`Accepted` 提供证书绑定的候选接受、发布成功则退出，若 Change 准入因快照/rebase/CAS 现实变化使证书失效则以另一轮普通独立迭代继续。`ProjectionCut = { ProviderRunId; ToolCallId }` 精确绑定已退休 provider run 与结束工具调用；`RetirementSummary = { Id; IncumbencyId; ProjectionCut; SnapshotId; AuthorityRevision; Outcome }`（其中 `SnapshotId: WorkspaceSnapshotId`）以工作区快照与权威版本绑定退休观察，拒绝陈旧 `Accepted`/`Continue` 重放。
- 上下文切段：新 active 迭代只保留类型化权威消息与本轮消息，移除所有前轮消息与仅用于唤醒循环的首个非权威用户延续，并跳过 XWire/Companion 历史投影；两种 retirement 都在 transform 边界清空退休 run 的后续请求并精确归还其 provider-step admission，`Continue` 随后自动激活下一轮，`Accepted` 只终止旧 attempt。发布成功即退出；若 Change 准入因快照/rebase/CAS 现实变化使证书失效，则以另一轮普通独立迭代继续。审计保留全量历史，证书、CAS 发布与退休资源围栏保持不变；推进提醒去重归 durable `PromptAuthority` 门控，`ExitRequiredNudgeScheduled` 重复 Relay 状态已删除。
- 配套改名：`RelaySuccessorGate` → `ManagerLoopGate`（kind 前缀 `manager-loop:`），`RequestSuccessor` → `ContinueLoop : ManagerJobId -> Task<Result<IncumbencyId,string>>`（去掉 reason 与冗余 WorktreePath），`runtime/relay-successor` 与 `runtime/relay-exit-required` 由 `runtime/manager-assess`、`runtime/manager-work`、`runtime/manager-finish` 取代，无 `runtime/manager-loop` 资源，循环唤醒与 `AuditPending` 提醒均复用规范 `runtime/manager-assess`；公开文档不再教授接力棒或合成交接，`requirements/GAP.md` 移除已过时的后继准入 GAP-033。

## 0.9.0

- JS capability-projected 编辑面升级为渐进式双层协议：
  - 新增默认 `edit(path, changes)`，用 `{ find, put, all? }` 覆盖精确替换、插入、删除、全匹配与同文件批量修改；所有 change 基于同一不可变快照规划并至多暂存一个 Rewrite，既有 `rewrite(path, newText)` 继续作为完整文件计算与结构重组的无上限逃生舱。
  - 新增 `INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP` 稳定失败码；近似文本只生成有界、双语、copy-ready 诊断，绝不自动获得写权限。
  - 工具说明改为 action-first 决策阶梯与 replace / insert / delete / all 规范示例；说明、成员、示例与 runtime binding 均按实际 capability 裁剪，较弱模型不再被推荐调用不存在的方法。
  - 保持事务、ReadSet 冲突检测、CRLF、同路径单意图、跨文件全有或全无与 no-op 零写盘语义。

## 0.8.4

- Obligation & Magic Todo 强类型化与恢复去令牌化（OBL-002/004）：
  - MagicTodo checkpoint 生命周期及进度追踪实现全链路强类型化，彻底消除字符串弱类型推导。
  - 删除 `JobRecoveryAction` 控制令牌调度器；崩溃恢复流程从持久化 facts 重新进入普通 CE workflow。
  - 清理 AGENTS.md 历史义务账与旧控制流。

- Finality & Review Judgement 裁决去未决态与生命周期收口：
  - 审阅裁决（`judge`）提交流程引入强类型请求标识与去重；消除未决分支（undecided outcomes）。
  - Finality 工具支持数组提示词（array prompts），完善审阅者裁决差距（reviewer judgement gaps）。

- Degeneration Guard / Loop Detection 动态校准：
  - LoopDetector 常量解耦硬编码，转为基于构建产物动态校准分布参数。
  - LoopSensor 准确捕获并处理 reasoning 与 thinking 增量。

- Session 生命周期与 Abort / 级联中断模型精细化：
  - 会话级联中断与中止（cascading abort / InterruptAttempt）模型细化，引入 typed assistance outcomes。
  - 增强会话终止、Daemon 管理与父子会话发现（`bindManagedChild`）。
  - 内部中断后续生命周期收口。

- Host Boundary & 执行模型路由适配：
  - 修复连续 user message 之间自动插入 assistant dot message，符合 Host 对话契约。
  - 插件 Hook 增强柯里化函数与生成适配器的兼容性处理。
  - 强化物理执行绑定（execution binding）、披露类参数与会话 ID 抽取；显式 `/continue` 命令处理与抑制保持。

- Blogger / Chronicle 与借用容量管理：
  - 稳定 Blogger 飞行状态与路由默认值；重构 chronicle thought 注入。
  - 实现借用容量（borrowing capacity）与 credit source 路由管理。

- Requirement Grounding 规范接地系统：
  - 引入 APPLIES-TO 清单与规范接地上下文压缩、观测闭环。

## 0.8.3

- 依赖整备：bun-pty `^0.4.10`、gpt-tokenizer `^4.0.0`（`o200k_base` API 不变，滴定常量未漂移）、@opencode-ai/plugin `^1.18.18`、opencode-ai `1.18.18`、smol-toml `1.8.0`；删除零引用的 `eventsource`。Fable 5.13.0 / fable-library-js 2.5.1 已是最新。
- HOST-BOUNDARY-008 projection catch-up 事件驱动化：armed retry 的 bounded re-read 改由 session `message.updated` 信号唤醒（`MessageVisibilityHub`，ITimerPort deadline 仅作无信号 backstop）；消除 Fable 5.13.0 把 `Task.Delay` 编译为 fable-library-js 未导出的 `delay`、导致 dist 模块图不可加载的根因。authoritative suite 回到 0 fail。

- Managed agent 默认温度硬编码为 1.0：`chat.params` hook 在校验 observed provider 绑定的同时，对 managed agent provider request 输出投影 `temperature = 1.0`；非 managed 会话保持 untouched。

- Managed model routing 改为 `~/.config/opencode/wanxiangshu.mjs` 单一 authority：同步 `route(role, running)` 返回 `{ model, reasoning } | null`；`running` 是同一 OpenCode process 跨 root/worktree plugin instance 共享的 session×EffectiveAgent lease multiset，`null` 形成事件驱动 backpressure，不推进 provider AABB failure。
  - 文件缺失时以原子 create-if-absent 生成可编辑推荐模板；已有文件永不覆盖。模板只承载推荐七组策略，runtime 不拥有 lane / capacity / candidate 算法。
  - `opencode.json` managed agent `model` 不再参与路由，也不再要求 fast/deep 物理 model 不同；managed request 在 `chat.message` 被 lease model+variant 覆盖，`chat.params` 只验证真实 provider binding。
  - 缺 catalog 名由 `config` hook 投影到 live Host config，不再要求 `opencode.json` 手写 22 个 agent；旧名仍 fail-closed。
  - `fast-browser` / `deep-browser` 独立配置；Host `title` / `compaction` 不属于此 model-routing 合同。

- 机械检查瘦身（2026-08-15，用户要求）：删除 `kolmogorov-size` 行数 advisory（`scripts/checks/kolmogorov-size.mjs` + baseline + `kolmogorov-size-advisory.test.mjs`）与 `enforcer-cross-family-collision` A40 机械替代（gate + GD-010 条款 + 本体测试）。
  - 行数从此不做任何机械检查（VERIFICATION-SYSTEM-012 更新：非门禁且无 advisory）；检测语料可区分性归 review 判断（A40 人类 tournament）。
  - VERIFICATION-SYSTEM-012 机器载体 = `requirements/verification-system/tests/no-line-count-check.test.mjs`（结构性 absence：本包 tests 与 scripts/checks 无行数检查指纹）。
  - check.mjs wired gate 20→18；proof-ladder 下限同步下调；e2e/support 13 处 advisory 注释清理；verification-system 四文档与 guidance-delivery 三文档同步。

- 结构重排第一轮（平衡树式旋转，2026-08-14）：`Application/Reconciliation` 拆散归各语义 owner，
  `Journal` 掏空为持久化基板；此后 **namespace = dir** 为仓库规则。
  - `Composition/Turn/`（ReconciledTurn→Observation、TurnBinding→Binding、Reconciler→Scheduler、
    ReconcileSupervisor→Supervisor、TurnWorkflow→Workflow + TurnReconcile/ReconcilePass/OrdinaryTurnWorkflow）、
    `Composition/Durable/`（AgentProjection→Projection、ProjectionState、ProjectionUpdate、FoldRejection、Fold 路由）、
    `Composition/Bridges/FinalityReview/`（FinalityReviewCohort 接缝显式化）。
  - 各 bounded projection/fact-fold 归家：Context/{Trace,Prefix,Companion/Blogger}、
    Interaction/{Authority,Dispatch,Repair}、Feedback/Enforcer(+Guidance)、
    Execution/{Session,Delegation,Fission}、Mission/{Manager/Life,Obligation/Todo,Review,Review/Barrier,Review/Assurance}、
    Change/Orchestration、Participant/Provider/Attempt/Fallback、OpenCode/Contract。
  - `Persistence/Journal/` 仅剩 substrate：Envelope/Codec/Writer/Boot/AgentJournal/SharedJournal/RuntimePath/FactCodec/EventStoreJournalWriter。
  - `Kernel/Fact.fs` 外层 union 与 per-family facts 拆分（第三刀）留待下一轮（wire-compat 评估后）。
  - 移动文件 namespace 跟随目录；引用按编译器驱动补 opens；测试 dist import 与 requirements 文档路径同步更新；
    `dsl-ownership` host-boundary 白名单扩展（过渡项，第二轮后移除）。

- Requirement Package cutover 收尾：`docs/`、`changes/`、`tests/` 全部腾空。
  - 45 包 normative 树 `requirements/<package>/{WHY,WHAT,HOW,PROOF}.md` 为唯一语义权威；
    旧 Clause 与变更记录已归档（2026-08-14 cutover；git 历史可回溯）。
  - 测试全部分包：`tests/unit` 146 文件 MOVE/SPLIT/DELETE 归各包 `tests/`；
    `tests/eval` → `office-capability`；`tests/integration` suites 归 owner 包；
    e2e Long Stroke、support harness、unit/integration runner 归 `verification-system/tests/`。
  - `package.json` release ladder 与新路径对齐；meta-verifier 骨架源迁入 `requirements/INDEX.md`。
  - 迁移 ratchet 退休：`g4r-freeze`、`student-teacher-absence`、`enforcer-rulebook-gate`（retired stub）。

## 0.8.2

- Provider Surface Grand Repair：ARCH-017 Office Capability；PROMPT-020 Tool Affordance；PROMPT-021 Critical Semantic Redundancy；ARCH-016 Gate F。Role Law 教身份，Tool Law 教动作，Delegation Law 教他人能成为什么。
- HOST-013 ordinary renderer：OpenCode Host 不再写 pending FakeReq。每个 occurrence 在 ResultGap 渲染一条 completed `auto-injected` tool part，由 `toModelMessagesEffect` 展开为 provider tool-call + tool-result，消除伪中断文案。
- 持久化、Git 与 session 工作流统一采用异步 Task 调用链，减少 Node 事件循环中的同步等待；GitGateway、EventStore、AgentJournal 与 blob 路径完成贯通。
- SyncDelegate 语义批处理、WorkRecord/Lifecycle 物化、HostFork/Join/Recovery/Enforcer/Finality/Manager/Review 的 durable 顺序进一步收口。
- 发布 Fork `attach`、Horizon 最新子 Agent 工作摘要，以及 Magic Todo / dedicated reviewer 的 obligation 与 assignment 改进；包入口与 durable store schema version 不变。

## 0.8.1

- REVIEW-003 skeptical challenge 迁入 `resources/provider/review/challenge`；tool result / nudge / seal 跟 Reviewer session `ProviderLanguage`；英文 canonical 字节不变（`ChallengeTextVersion = 1`）。
- journal / 公开 wire 合同相对 0.8.0：无 domain protocol 破坏。

## 0.8.0

- Provider-visible prose ownership（PROMPT-019 / ARCH-016 Gate E）：进入 participant horizon 的 Class A 自然语言经 `ProviderResources` 装载、由 session `ProviderLanguage` 管辖。Gate E baseline `{}`。
- Gate C 现行面补齐：叶对 + `{{placeholder}}` 集合一致 + Role Law semantic-anchor 同 ID 双语命中（`scripts/checks/semantic-anchors.mjs`）。
- HOST-013 pair guideline 迁入 `resources/provider/host/pair-programming-guideline`；生产路径 `ProviderProse.render`，禁止 `match lang` 挑选正文。
- Role Law 是身份文本，不列工具名。REVIEW-003 challenge 仍为固定英文协议句。
- journal / 公开 wire 合同相对 0.7.0：无 domain protocol 破坏。

## 0.7.0

- Kolmogorov 所有权二级拆分（语义汇流点，非按行数切文件）：
  - LWR journal 物化 → `LifecycleWorkRecordProjection`；`XTraceCapture` 只保留 semantic capture。
  - Manager durable open / migrate / activate → `ManagerLifeWorkflow`；`ManagerNarrativeTransform` 只保留 wire 门控与 provider rewrite。
  - `PluginTransforms` 只保留 hook 顺序；Strength replay/traced → `StrengthReplay`。
  - `HostSignalBootstrap` 退回订阅/路由；政策 → `HostTurnObserver` / `HostCompactionObserver` / `HostSessionDeletion`。
  - `Reconciler.Scheduler`（coalesce/drain）与 `ReconcilePass`（causal reread/publish）分居；`ReconcileProgram` 不变。
- Wave 0–5 Kolmogorov 重构收口：`kolmogorov-size` ratchet；JsTools / ProjectionAlgebra / PluginRuntimeScope / Fold / EnforcerHost / SpikePlugin / Codec Projection / HostForkJoin / SyncDelegate 等按 owner 装箱（详见 `changes/completed/refactor.md`）。
- Strength 提案闭环；EnforcerContinuation 从 EnforcerHost 二次抽出。
- G6 Casebook / G9 Session ownership ratchet Product Exit（问卷八 kind + 接线 gate）。
- JS capability-projected tools：structured TOML result；grep/glob 能力面收紧。
- AGENT-026/027：Stealth Browser MCP Host 接线 + 内部 Semble MCP；共享 `McpLaunch` 词汇（Disabled / Fixture / Uvx），消除 dup-cases。
- 持久化写入延迟：EventStore 的 Git raw store 不再为每个对象 spawn 一次 `git`。新 `GitObjectDatabase` 直接读写 loose object（`sha1` + `zlib` + `objects/xx/yyyy`，tmp+rename），并对内容寻址的对象/tree 读取与 `mktree` 结果做实例级 memo。单事件 append 由 **24 次同步 git 子进程 / ~60ms** 降到 **2 次 / ~7.5ms**；由于 `execFileSync` 会阻塞 Node 事件循环，这段成本此前会让同一 Host 内所有 session 串行等待。oid、on-disk 布局与 `git cat-file` 可读性完全不变（`tests/integration/persist/object-identity.test.mjs` 对真实 git 二进制逐项比对）；`gc` 之后的 packed 对象仍回落 git CLI 读取。
- FALLBACK-013：Host abort/cleanup 残留（在途工具被标 `status=error` + `metadata.interrupted=true`）不再推进 A/A/B/B cursor、不消耗自动恢复预算。此前 owner 的一次 provider 失败会被记两次——一次来自它自己的失败路径，一次来自被同一次 abort 清理打断的 Companion cycle（且用 Blogger 的 `ProviderRunIdentity`，FALLBACK-003 去重无法折叠）——导致 provider 可见的 A/A/B/B 顺序取决于两次 append 的竞争，恢复可能落回刚失败的同一侧。Companion 侧仍注入一次 `# Protocol repair`，有界性由 ENFORCER-153 marker 保证；`ToolExecutionError`（无 `interrupted`）仍按 ENFORCER-065/068 推进 cursor。
- journal / 公开 wire 合同相对 0.6.0：无 domain protocol 破坏；控制流与 Host 边界所有权收紧。

## 0.6.0

- Causal CE / 时序所有权：可观察因果等待、Wait Graph、waitFact 续期归因；Reconciler 去业务轮询；Join interrupt / user-wake 收口；Diagnostic Bridge。
- Manager Finality / lifecycle：`FinalityTool`、terminal frontier、sibling steering / durable revision；PERFECT 后的收口与 rest-in-peace 路径。
- HOST-013：guideline pair 永久 append-only；prefix-cache 不变量；idle-derived continuation 资格门控（SessionQuiescenceGate）。
- Student–Teacher CE collapse：Teacher 侧单一 CE await 链；durable evidence；相关单元/回归收口。
- Projection Algebra / Glory：attempt-local PrefixProbe 与 plain-X 前缀投影迁入投影 DSL；idle / revise / MISSING_FINAL_REPORT 观察路径加固。
- Coder 工具面：`bash-honeypot` 禁未授权 shell；严禁 Coder 跑测试；PTY prompt 补齐换行。
- EXEC-028：同步 one-shot `inspector`/`coder` 返回统一为 entry-local LWR 注释（`includeOpening=false`）+ 末条 TurnFormalText，禁字段式 `work_record`；与 Join 共用 COMPANION-003 物化器。Opening 在 send 前从原始 assignment 捕获以便物化；`Completed` 无法物化非空 LWR 时 fail-closed 返回工具级 `error=`，不 soft-omit。
- LWR 段标题在 materialize 中为纯文本（`Opening task` / `Work log` / …）；`# ` 仅由 `SyntheticToml.comment` 在 wire 注入，消除 join/oneshot/finality 上的 `# # Work log`。
- Enforcer / Blogger-as-Enforcer rebase 文档收口：`how`/`shape`/`proof` 对齐 tip-v2 基线（PartOrdinal-first 多调用 tip、物理所有权轴、`§13` 证明清单）。`bounds.test.mjs` 永久回归锁定归并 size/count 越界 fail-closed（>32 calls / text >512 KiB / evidence >128 KiB）；未恢复 wire/runtime score 路径。
- 文档治理：变更单文件生命周期 `changes/{proposed,active,completed}`；条款 ID 唯一归属正式层；`PENDING.md` 收口为 COMPLETED/HISTORICAL；`AGENTS.md` 修正 architecture 文件数与 `gate:dsl-ownership --threshold=0`。
- Canary unbend：纠正迎合错误生产的声明扭曲；e2e 事件驱动等待取代固定 poll slice。
- journal / 公开 wire 合同相对 0.5.4 兼容方向：控制流、投影与 Host 不变量收紧；破坏性细节见上列条目与 `docs/`。

## 0.5.4

- AGENT-019：managed agent Host-final permission 固定 `external_directory = allow`，覆盖 Host 默认 ask，取消项目外路径的交互确认。
- DSL 全面主导化（ARCH-001 / FLOW）：门禁债 `157 → 0`。
  - 删业务 Program AST / Interpreter；Child/Session Recovery、Orchestrator/Reconcile/Join 直接 CE。
  - `Kernel/Flow` → `Kernel/Parallel`（仅 `mapBounded`）；`CycleDisposition`、`DrainWindow`、`BloggerRuntimeHost`。
  - `dsl-ownership` 契约：合法 mutable（Domain/Session/Application/Parallel）；Host 边界 `open` basename 白名单；`--threshold=0`。
- e2e 稳定性：`gitConflictProof` 挂 worktree 已存在之后；`ProcessHost.stop` 在 leak assert 前回收残留 listen 端口。
- AGENTS.md 收束为现行纪律（P0–P3 施工表退役）；`TASK.md` 作历史档案。
- 无 journal / wire 协议破坏；控制流与门禁契约收紧，产品对外协议与 0.5.3 兼容。

## 0.5.3

- No runtime protocol changes.
- Normalized source, resource, specification, test, and build layouts.
- Replaced the generated Enforcer catalog with packaged runtime data.
- Packaging now uses the repository root and includes resources directly.
- Removed migration evidence, generated conformance ledgers, and legacy gates.
- Renamed internal files and test directories without changing public behavior.

## 0.5.2 — 全 SSOT 收敛

- 收敛目标：Active 规范全部收敛。
- 规范：spec/14 Strength、spec/16 Student&Teacher、ENFORCER nudge/throttle/规则目录迁出到 `RFC/`，spec/15 仅保留 0.5.1 已交付的 Blogger 工具化子集。
- 版本：全仓文案从 `0.5.0-rc.1` / `0.5.1` 统一到 `0.5.2`。

## 0.5.1 — Blogger vertical-slice convergence (spec/15)

生产闭环 Blogger 请求形状 / 挂起 / Squash / 恢复载体（不做 Enforcer throttle、nudge、Strength、Student&Teacher）。

### Runtime authority
- 生产 `BloggerRuntimeCell`（Idle / InFlight / Parked / Disposed）
- `CurrentRequest` 与 `PendingOffer` 双槽；唯一 busy 定义 = InFlight
- 唯一入口 `BloggerCoordinator.onMainMaterial`；删除 `offerToBlogger` 旁路与 `inFlightTask` busy 权威

### Projection & commit
- 发送前冻结 typed context 并落盘 `BloggerRequestMaterialized`
- 首次 / resume / Squash 共用 `CompanionProjectionBuilder`；删除 raw TOML 抽取与 `BloggerNeedsReset`
- Squash 迁入 blog tool continuation，提交 `BlogSquashCommitted`（coverage 不变）
- 仅 `KnownCommitted` 后 Park；`KnownNotCommitted` / `CommitUnknown` 不 Park、不重问
- 统一 `BloggerCycleReceipt`（Entry|Squash）按 ProviderRun 幂等
- 一次 `RepairSpent` repair；资源上限；Main Entry 成功清 fallback

### Recovery & teardown
- crash-window recovery 挂 `EnsureRecoveryDone`；live CurrentRequest 不 stomp
- fail-closed `loadEffectiveFrames`；`CompanionIdentity.newWorkMessageId`
- Host 重建消息带 synthetic/source 标记；main dispose 清 linked Blogger waiter

### Evidence
- layer-4：`host-transform-capability-canary`（park/resume、第三 turn 单飞、materialize）
- layer-4：`companion-canary`（同 child 两轮 blog tool）
- 静态 `blogger-convergence` 防回退门禁
- 条款收敛：`COMPANION-005/008`、`CTX-006/007/012`、`ENFORCER-010`

## 0.5.0 — 正式版

- 正式发布：0.5.0（从 rc.1 收口；breaking changes 见 `0.5.0-rc.1` 条目）
- 生产可用：canary 森林 17 驱动（18 剧本）× 3 轮全绿，`test:release`（gate:static →
  build → unit → harness → P0×3）完整通过
- Review 双 PERFECT 见证（REVIEW-006/007）
- Orchestrator 恢复链（ORCH-005/006/007）：restart 后 exactly-once publish、rebase
  冲突恢复
- guard nudge seal 稳定性修复（ORCH-006/ARCH-004）：session worktree 目录绑定
- 来源解析顺序（PROMPT-004/009）、发送格式（PROMPT-006）、fire-and-forget（PROMPT-007）
- 工具权限双层 fail-closed（AGENT-007）
- 未验证条款清零（8 条批量段条款补第 1 层判据）

## 0.5.0-rc.1 — docs freeze / RC development

Breaking changes:
- All agents now require explicit `fast-*` or `deep-*` names
- Unprefixed agent names, `build`, `plan` aliases removed
- Agent-to-model bindings read exclusively from `opencode.json`
- All Wanxiangshu model environment variables removed
- No longer persists or overrides model IDs
- Provider fallback cycles A/A/B/B within budget（Cursor 无限定义；自动恢复上限默认 12 连续失败）
- Provider retry count no longer kills a Logical Run
- Blogger and Executor Agent are now internal fast/deep pairs
- Pre-0.5.0 runtime journals not supported

## 0.4.0 — 最终版

- Structured Agent Program (Flow CE, no Stage/Phase/Lease platform)
- Prompt Authority / Logical Run rules
- Companion + ActivePrefixEpoch / FrozenB cache protection
- Manager `fork-agent / join / list`; Orchestrator `fork-manager / join`
- Static role matrix with full system prompts
- Logical-Run Fallback A/A/B/B with durable retry writer
- Dual PERFECT Review with ProviderRunIdentity binding
- Process/Executor: 3× estimate, large gate, 200KB ripple-carry
- PTY via DevOps `fork-pty` only; onExit-only completion; structured signals
- Orchestrator: clean gate, worktree, serial publish lock, rebase, re-review, ff-only
- OpenCode adapter: idle/retry/deleted signal + single-flight reconcile
- Private distribution: `private: true`, provisional commercial LICENSE

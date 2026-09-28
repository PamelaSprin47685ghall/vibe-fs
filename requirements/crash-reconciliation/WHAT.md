# crash-reconciliation — WHAT

## [001] 进程内状态不是恢复权威

重启后清空所有 process-local 状态，包括 armed anomaly、permit、waiter 和 detector；不得将其作为恢复权威。没有 fresh evidence 不得自动产生副作用。

## [002] 恢复证据来源

恢复只使用已提交的不可变 EventStore 事件及 fold 投影，以及 Host SDK 快照、Git ref 等可信物理观察；不得从缓存、墙钟或日志散文推断状态。

## [003] 未知 effect 先核对

结局未知的外部 effect 不得当作未发生而重放。`finish=None` 快照属于私有观测 `TurnUnknown`，等待静止证据后才由业务层裁决。

## [004] 复用普通入口

恢复遵循 `Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口`，不恢复程序节点、continuation 或执行步数，不另建恢复状态机。工具与会话中断按 [017]/[018] 处理，不在启动时自动重放、补写或修复。

## [005] 证据不足时停止或等待

证据冲突、缺失或不足时，明确进入 `Waiting`、`Blocked` 或 `RecoveryIncomplete`，不得猜测继续。Waiting 只表示瞬态等待，不能发送请求、发布完成态或声称 Ready；只读观察与有副作用的处理必须分开。

## [006] Fresh permit 与物理发送

Join 必须持有效 `FamilyRecoveryPermit`，并按 [011] 核验成员；当前进程凭据不证明跨进程恢复完成。跨进程续传遵循 [017]/[018]。

Idle-derived continuation 必须在物理发送边界消费 fresh `QuiescencePermit`。静止要求当前 attempt 已被 Host 观测为 idle，且该 session 无 active tool body；提前到达的 idle 只能在最后一个 tool 结束后变为可消费证据。新物理用户输入立即、幂等撤销旧许可。

只有 Host 明确证明 acceptance 前拒绝，且同一 attempt serial 未被新材料取代时，才可将 exact permit 从 `IdleConsumed` 原子归还为 `Idle`。新 attempt、新物理用户材料或 acceptance unknown 均禁止归还。

## [007] TurnUnknown 不外发

`TurnUnknown` 只属于 reconciliation 内部观察，不得作为正式 `TurnOutcome` 发布。

## [008] Abort 属于控制面

Host abort 解码为 typed `AttemptAborted`，撤销当前 attempt 的全部 continuation 能力并唤醒 Reconciler，不得改写为 `ProviderFailure`。

## [009] Child 无 Aborted 终态

Child 终态只有 `Succeeded | Failed | Abandoned`。单独 abort 观察不构成终态；`JoinableCompletion` 必须有真实解码正文。

## [010] 恢复结果互斥且穷尽

`RecoveredActive` 表示仍在运行，`RecoveryIncomplete` 表示缺少终态证据；`Waiting` 表示瞬态等待，`Blocked` 表示硬性阻断。各分支不得混用。

## [011] 每次 Join 重新核验

每次 join 前重新验证 `FamilyRecoveryPermit`：它证明的成员不得丢失，后来新增成员可单调准入。跨进程与当前进程凭据的类型和语义必须对应实际核对范围，不得伪称全家族已恢复。

## [012] 完成态唯一提交

完成态只有一个提交 owner，先写 blob 再写事实，拒绝重复 claim；retire 墓碑保证重启后不重复投递完成态。

## [013] 恢复结果合并

优先级为 `Blocked > Waiting > Recovered`；同层级合并与输入顺序无关。

## [014] 闭包与单调准入

闭包中 session 重复即 `RecoveryCycle`，必须阻断；permit 证明的闭包成员不得丢失。

## [015] 附加会话复用与替换

附加子会话恢复时，唯一关联 ID、agent、title 均匹配才复用；关联不存在才新建，冲突或多重匹配必须阻断。替换须先证明旧物理会话消失，再显式 Close，最后 Link 新会话。恢复触发仍受 [017]/[018] 约束。

## [016] Blogger 修复不跨进程继承

Blogger 的 nudge/AABB episode、waiter 和 flight lease 只属于当前进程 live owner。进程死亡后能力消失；durable dispatch/terminal facts 只供核对、诊断，不得据此恢复修复阶段或自动续发。新进程按 [017]/[018] 重新准入。

## [017] 中断工具不自动恢复

进程死亡时的在途工具按中断处理；新进程不得自动重放、补写完成态或隐式修复，不为工具另设崩溃恢复 owner。

## [018] 加载归位与按需复用

重启后系统在加载阶段自行归位，只做持久记账，不重放命令；不设显式续传命令、独立续传材料通道或 disclosure-only provider 轮次。

上个 runtime 遗留的活跃子 run 以 `ChildRunVoided` 关闭，不产生 completion 或待收交付，join/horizon 对该 run 为空；Manager 可显式 resume。父子执行绑定与 fission lane 按 durable 投影按需解析，以 handle 的 TargetAgent 而非 Byname 确定执行者，不作启动预登记扫描。复用门禁、placement 与 await 均以 durable handle 判断存在性。

中断工具保持失败且原样留在可见历史，不推断完成、不隐藏、不伪造终态。

## [019] 每项外部 effect 的闭合证据

每个高价值外部 effect 在唯一 owner 下登记 `intent → process-local admission → physical receipt → durable outcome`，不适用阶段须说明理由。登记包括物理 identity、有限且穷尽的歧义状态、查询/补偿入口、安全重试律（仅 `proven-not-applied` 或 `never`）和普通 CE 重入入口；未知、冲突、缺失证据均拒绝继续。

Host、provider、Git、process 边界同时需要确定性歧义证明和 Adapter 或 Long Stroke 证据。层级由 verification owner 的独立 registry 按 exact `(path, title, WHAT)` 唯一分类，不接受 effect 自报、改标或未登记分类。owner、WHAT、source symbol 和 executable proof title 均须精确；重复 WHAT ID、过期锚点或未闭合 effect 使 gate 失败。

Prompt dispatch 的 admission 锚定物理发送前的 `physicalAdmission`；Blogger receipt 是 `TransportReceipt`/`PluginPromptSubmitted` 及随后接受的 `PhysicalUserMessageId`，不是预先派生的 `PromptKey`。恢复不持久化 capability、continuation 或 `ResumeAt`/`RecoveryStage`/`RecoveryStep`/`NextAction` 程序计数器。

## [020] 固定 DevOps 的单一权威

同一道路的固定 DevOps 恢复后只映射一个活跃物理会话，不得并行产生两个执行权威。崩溃前未决的 run/PTY 输入按中断处理，重启后不自动重放、补写或续发。

恢复或 resume 沿用道路初始化时持久绑定的 `ModelTarget` 与 Persona，不得换模型；新建与恢复只接纳当前合法角色，不隐式恢复旧角色状态。

加载归位一次性结算上个 runtime 遗留且本进程不再持有的 durable 工作：活跃子 run 按 [018] 作废；未关闭的 Blogger request 以 `BloggerRequestAbandoned`、reason `stale-open-at-load` 结算。本进程仍持有同 RequestId live flight 的请求不得结算。

## [021] 进程缓存不决定 durable 存在性

子会话、lane、handle 的存在、归属与执行者均以 durable 投影为真源。进程缓存未命中时按需解析并回填，不得仅因本地缺项而报未知、拒绝或忽略，也不另设加载预热通道。

只有当前进程持有的 PTY、pending run、teardown 与 live companion host 等进程资源，其归属可仅由本地记录决定。

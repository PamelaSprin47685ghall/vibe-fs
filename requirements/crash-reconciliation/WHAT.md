# crash-reconciliation — WHAT

## [001] process-local 状态不是恢复权威

进程重启后，所有 process-local 状态（许可、gate 与 detector 的进程内登记）全部清空，不作为恢复依据。没有新的现场观察，不自动产生任何副作用。

## [002] 重启从 durable facts + 可信物理观察重建世界

系统恢复只用两类输入：EventStore 中已提交的不可变事件及其 fold 投影，以及 Host SDK 快照、Git ref 等可信物理观察。不用缓存、墙钟时间或日志散文推断状态。

## [003] 未决外部 effect 先 reconcile 再决定是否可重试

结局未知的外部 effect 不直接视为未发生而盲目重放。Reconcile 观察中 `finish=None` 的快照属于私有观测 `TurnUnknown`，等静止证据齐备后由业务层决定处理策略。

## [004] 恢复复用普通 workflow 入口，不发明程序计数器

恢复遵循 `Journal facts → Fold → 纯恢复决策 → 普通 workflow 合法入口`。不恢复 Program 节点、continuation 或执行步数，不引入 `RecoveryStage` 之类的第二状态机。工具与执行中断的边界见 crash-reconciliation-017 与 crash-reconciliation-018：不设隐式崩溃恢复 owner，新进程启动时不自动重放、不补写完成态、不隐式修复；加载阶段自行归位（结算遗留子 run、重新登记子会话）；发送能力不靠回填执行绑定缓存，无缓存时仍依据权威事实正确发送与查询。

## [005] ambiguous / multiple / missing 证据 fail closed

恢复证据不足、冲突或缺失时，系统显式停留在 `Waiting`、`Blocked` 或 `RecoveryIncomplete` 分支，不猜测继续。Waiting 只代表瞬态等待（等新 turn 或观察稳定）；处于 Waiting 时消费端不执行 Ready 分支的副作用（不发送请求、不发布完成态、不虚假声明就绪），只读观察与 effectful observe 严格分离。

## [006] 没有 fresh evidence 就没有自动 effect

恢复闭合后的副作用操作须持有对应许可：执行 join 须持有 `FamilyRecoveryPermit`；发送 idle-derived continuation 须持有仍新鲜的 `QuiescencePermit`。

quiescence 是物理条件的合取：当前 provider attempt 已被 Host 观测为 idle，且该 SessionId 没有仍在执行的 tool body。Host 的 `SessionIdle` 先于 tool completion 到达时只能建立待静止证据，permit 在最后一个 active tool 结束前不可消费。新的物理用户输入到达时立即幂等撤销旧的静止许可。

permit 在物理发送边界被消费（`IdleConsumed`；permit 状态机由万象通侧 `wanxiangli/src/chat-execution/SessionQuiescenceGate.fs` 持有）。若 Host 在 acceptance 前明确拒绝、且同一 attempt serial 未被更新材料取代，允许把该 permit 原子归还为 `Idle`，使仍未满足的 gate 可重试；任何更新的 provider attempt、物理用户材料或 acceptance-unknown 都使归还失败。

当前进程的 join/admission 凭据只覆盖本进程内的准入合法性；跨进程恢复由 crash-reconciliation-017 与 crash-reconciliation-018 的加载归位承担，join 仍须持有 `FamilyRecoveryPermit` 并做 exact membership 闭包检查。

## [007] TurnUnknown 是 reconciliation 私有观测

`TurnUnknown` 只是 reconciliation 内部观测，不作为正式的 `TurnOutcome` 对外发布。

## [008] abort 是 typed 控制面，不是 ProviderFailure

Host 的 abort 信号解码为类型化的 `AttemptAborted` 控制面事件，撤销当前 attempt 的所有 continuation 能力并唤醒 Reconciler；不改写为 `ProviderFailure`。

## [009] child recovery 没有 Aborted 终态

Child 终态只有 `Succeeded | Failed | Abandoned`（`ChildFinality`），没有 Aborted 终态。单纯的 abort 观察不构成 terminal 证据；JoinableCompletion 必须有真实解码正文。abort 观察可以说明"交付已不再可能"，结论只能是作废结清（`ChildWorkVoided`），不能写成完成或失败。

## [010] 恢复结果分支穷尽，Waiting ≠ Blocked

恢复结果分支语义互斥且穷尽：`RecoveredActive`（活跃运行中）≠ `RecoveryIncomplete`（缺终态证据需等待）；`Waiting`（瞬态等待）≠ `Blocked`（硬性失败阻断）。

## [011] 线性序 permit → join，每 join 重新验证

每次执行 join 前重新验证 `FamilyRecoveryPermit`。Permit 携带恢复闭包的成员集合；已恢复成员丢失则拒绝执行，恢复后新增成员允许单调准入。跨进程与当前进程 join 的证据接口在类型和语义上与真正核对结果一致，不伪造全家族恢复完成凭证。

## [012] completion 单一 owner

HandleController 的 `recordCompletion` 是提交完成态的唯一入口：blob 先于事实写入，拒绝重复 claim，retire 墓碑保证重启后完成态不重复投递。

## [013] combine 优先级 Blocked > Waiting > Recovered，按层序无关

多个恢复结果合并时优先级严格为 Blocked 优于 Waiting 优于 Recovered；同层级内的合并与输入顺序无关。

## [014] closure 校验与 permit 单调准入

闭包中出现重复 session 判定为 `RecoveryCycle` 并 fail-closed 阻断。Permit 校验要求闭包成员单调不丢失。

## [015] Attached restore 复用/替换/fail-closed

重启后附加子会话恢复时：匹配唯一关联 ID、agent 与 title 时复用；关联不存在时新建；冲突或多重匹配时 fail-closed 阻断。替换须先确认旧物理会话已消失，显式执行 Close 后再 Link 新会话。

## [017] 工具中断不恢复；未来 session 续传必须显式

工具执行不设隐式的崩溃恢复 owner。进程死亡时正在运行的工具调用按中断处理；新进程启动时不自动重放、不补写完成态、不隐式修复。被中断的工具调用保持失败并原样留在可见历史中，不推断其完成、不隐藏、不伪造终态。

## [018] 重启后由系统自行归位，没有显式 resume 命令

不存在显式续传命令。系统在加载阶段自行归位，只做持久记账，不重放任何命令：

- 上一 runtime 遗留的活跃子 run 被作废：已有 scoped 工作以其实际准入身份追加 `ExecutionFactCases.ChildWorkVoided`；合法的无 scope 历史才使用 `ChildRunVoided`，不由裸 Root 补造工作准入。作废不是完成——不写完成正文，不把已有交付视图当完成凭据；但作废结清父方的未解除义务：以 `voided` 状态进入 join 的下一次报告批次，报告一次后从名册退役（durable），重启不重复报告。作废在报告前仍是可见的在场后果，父方不会把它误认作"什么都没发生"。
- 父子会话的执行绑定与 fission lane 归属按 durable 投影按需解析（取 handle 的 `TargetAgent`，不取逻辑 `Byname`）；进程本地表只是缓存，装载阶段不做预登记扫描。
- 复用仍由 manager 显式发起；复用判定、placement、await 一律以 durable handle 为存在性依据。

本进程完成加载归位后，在下一次真实用户指令之前 prepend 一次只读的重启状态指导，向模型说明万象术已被重启及其含义。状态指导只出现一次；不改写用户原文、不创建或延续 Interaction Authority、不改变角色、权限或主体，并冻结已交付的 provider wire 字节。除此之外没有独立的续传材料通道，也没有 disclosure-only 的 provider 轮次。

## [019] 外部 effect 逐项闭合，未知即停

每个高价值外部 effect 在唯一 owner 下走类型化生命周期 `intent → process-local admission → physical receipt → durable outcome`；不适用的阶段给出明确理由。登记项锚定物理 effect identity、有限且穷尽的歧义状态、查询或补偿入口、安全重试律（仅 `proven-not-applied` 或 `never`）、以及复用普通 CE 的重入入口。Prompt dispatch 锚定物理发送前的 process-local `physicalAdmission`；外部 receipt 是 `TransportReceipt` / `PluginPromptSubmitted` 与随后接受的 `PhysicalUserMessageId`，不是预先可派生的 `PromptKey`。恢复不持久化 capability、continuation、`ResumeAt`、`RecoveryStage`、`RecoveryStep` 或 `NextAction` 之类的程序计数器；未知、冲突、缺失证据一律 fail closed。

【程序待改】原条款要求的「verification owner 独立 registry 按 `(path, title, WHAT)` 分类、source symbol / executable proof title 精确锚点、gate 失败」装置未实现（019 测试为 `test.todo`）。程序侧应删去这类门禁/registry 残留接线，或落地一个真实的 effect 登记与核对入口，二选一。

## [020] 固定 DevOps 崩溃恢复的单一逻辑执行权威与命令去重

同一道路绑定的固定 DevOps 在崩溃恢复后只映射到唯一的当前活跃物理会话，不产生两个并行生效的可执行物理权威。崩溃前未决的物理命令（`run`、PTY 输入）按中断处理：重启后不自动重放、不补写、不隐式续发，杜绝物理副作用重复。恢复沿用道路初始化时持久化的绑定模型（ModelTarget）与 Persona，不在恢复时切换模型；新会话与恢复只接纳合法新角色集合，历史旧状态不隐式跨边界恢复。

Load Phase 一次性结算上一 runtime 遗留、本进程无法继续持有的 durable 未决工作（仍活跃的子工作 run）。该结算是加载归位的首个持久动作：作废事实须先于下一次交接的身份判定落定，否则新交接会读到仍然活跃的旧根，把已退休的子会话判成身份冲突。作废结清该工作的未解除义务（join 报告一次 `voided` 后退役）；不作废也不结清，则父方的未解除义务跨重启悬空。

## [021] 进程本地表是缓存，durable 投影是存在性真源

"这个子会话/lane/handle 是否存在、属于谁、由谁执行"的判定必须能从 durable 投影回答；进程本地注册表（子会话登记、dormant 集、执行绑定、lane 注册表）只记录"本进程当前在驱动什么"，不作为拒绝、忽略或"未知"的唯一依据。

- 未命中缓存时按需解析并回填（`DurableChildLookup` by handle id / byname、执行身份按 durable authority 投影与 exact lease 解析、fission lane 由 `Execution/Fission` 的折叠事实解析），而不是回 `Unknown agent id`、`person-unavailable` 或静默跳过。
- 装载阶段没有"预登记/预热"特例通道：同一件事只有一个按需规则。
- 只有进程资源归属（本进程持有的 PTY、pending run、teardown 集合、live companion host）只读本地表；它们描述进程，不描述世界。

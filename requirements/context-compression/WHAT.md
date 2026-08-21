# context-compression — WHAT

## CONTEXT-COMPRESSION-001: 不观察容量

严禁读取、查询、推导或缓存任何模型上下文窗口大小。禁止引入任何形式的上下文余量、Token 计数器、模型容量表或字节换算逻辑。唯一允许的字节计量为 200 KiB 输入合同与合法的文件/进程计量。

## CONTEXT-COMPRESSION-002: 不主动预测溢出

在请求发送前严禁判断是否接近容量上限，禁止根据投影长度比例、剩余预算或累计 Token 主动选择压缩点。真实的 provider attempt 失败是唯一的恢复触发信号。

## CONTEXT-COMPRESSION-003: 200 KiB 输入合同

单次 delta 渲染字节上限为 200 KiB（`BloggerDeltaLimitBytes = 200 * 1024`）。该数值为输入合同而非窗口估算，不参与动态调参或主动触发决策；超限时采用确定性的切块与截断策略。

## CONTEXT-COMPRESSION-004: 输出预算属 provider

系统不计算 squash 应占用的 Token 数量，亦不检查压缩比例。对压缩输出的唯一内容校验为 `isValidTerminal`（非空且非纯 XML）。

## CONTEXT-COMPRESSION-005: 失败不分类

控制流仅识别快照的 `Outcome`（`Completed | Failed | Aborted`），严禁根据错误字符串或异常类型细分溢出、网络或限流等原因。

## CONTEXT-COMPRESSION-006: 恢复机会一次性消费，恢复动作 = opportunity ∧ hasMaterial

真实失败被 `FallbackLedger` 接受并推进 cursor 后，只有新 Offset 为奇数时才产生一次 `RecoveryOpportunity`；它是 `armedByFailure ∧ primed` 的类型化结果，不得与 `hasMaterial` 折叠成一个布尔值。X 在该机会中必须实际执行候选选择，由 `PrefixProbeSelection` 的 `Ok probe | Error NoCandidateReason` 证明材料是否可用；Y 则以当前 typed Blogger request 与 durable frames 判定是否先执行 squash。若本次物理 attempt 没有可用材料，则发送普通主请求并消费该机会，严禁把 arming 留给未来无关请求，也严禁通过进程内 waiter 等待未来 X material 再补做 recovery。

## CONTEXT-COMPRESSION-007: 按 RequestKind 分派结局

每个 attempt 的结局按 `ProviderRequestKind` 进行确定性分派；RequestKind 必须来自 attempt 的 typed context / durable receipt / accepted continuation evidence，而非角色猜测或错误文本。同种 RequestKind 的同种结局必须产生完全一致的后继动作。`BloggerSquash` 与 `InteractionRepair` 成功不得清零连续失败计数；只有 `WorkMain | BloggerMain` 的有效成功可以清零。

## CONTEXT-COMPRESSION-008: X 不发压缩请求

主工作会话（Work Session）严禁向主模型发送请求压缩历史的指令。压缩操作仅在 Y 的 squash 或 X 的 prefix 替换投影中发生。仅 `WorkMain` 请求有资格携带 prefix probe。

## CONTEXT-COMPRESSION-009: 候选未提交不是事实

恢复槽中尝试替换 X 前缀时，候选前缀仅作为 attempt-local 的执行配置，不修改已提交的 `ActivePrefixEpoch`。Probe 失败则直接丢弃候选，不产生任何持久化事实，亦无需回滚。

## CONTEXT-COMPRESSION-010: 候选选择严格新于已提交 epoch

候选前缀必须在 coverage 证明上严格新于当前已提交的 epoch：cutoff 游标不得回退，与已提交前缀不可区分的候选必须直接拒绝，无候选时不构造空 probe 而发送普通请求。

## CONTEXT-COMPRESSION-011: 提交语义分型

X probe 的**物理 provider attempt** 一旦产生可用成功（包括 `finish=tool-calls`：Host turn 尚未结束、但该 provider attempt 已成功），必须先原子提交新 epoch 并继承 SealRoot，再允许下一次 provider request 组装；失败或不可用回应则无 rebase 事实。`PrefixRebaseCommitted` 的 durable append 成功是消费 attempt-local probe plan 的前置条件，append 失败必须 fail-closed，严禁“plan 已消费但 epoch 未提交”。一旦 epoch 已提交，后续每一个普通 `WorkMain` 都必须继续投影该 committed prefix；无 `RecoveryOpportunity` 只表示本次不得构造新 probe，绝不表示退回 raw X 历史。Y squash 成功时提交 squashed observation 并使 FrameEpoch 递增，失败时不修改现有 frames 与 coverage。`m=1` 时仍允许 `k=1` 的真实 rewrite；新的 squash terminal blob 是新的历史表示，其 `TextDigest` 可以且通常应与被替换 Entry 不同，不得把 digest 相等误作单帧 squash 的 PERSIST-010 条件。

## CONTEXT-COMPRESSION-012: Blogger delta TOML 合同

Blogger delta 以 data-only TOML 形式冻结于 blob，指令头部仅在投影时注入。严格遵守 200 KiB 渲染上限，超限时确定性切块，包含决策相关的可见推理，与 LWR gap 保持分立投影。

## CONTEXT-COMPRESSION-013: 诊断不是控制输入

可观测诊断日志严禁作为控制流输入，不得使用任何诊断字段驱动 Fallback、probe 或 squash 的分支决策。

## CONTEXT-COMPRESSION-014: squash 只处理本 X 的 frames

Squash 仅压缩当前 X 会话自身的 frames，严禁混入父级 LWR 或跨会话上下文。

## CONTEXT-COMPRESSION-015: busy/失败不推进 coverage

Blogger 处于 busy 状态、请求失败、结果为空或为纯 XML 时，严禁推进 RecordCoverage。仅在 `BlogObservationCommitted` 时原子推进 frame 可见性与 RecordCoverage。

## CONTEXT-COMPRESSION-016: Y prefix 只物化 PrefixCoverage 完整 turn

Y prefix 物化仅允许使用具有 PrefixCoverage 完整 turn 证明的 Y 产物，严禁使用 RawGap。CoverableTurnCutoffExclusive 仅在完整 Host turn 边界推进。

## CONTEXT-COMPRESSION-017: 只有真实 Opening 构成不可压缩 floor

真实 Opening 消息永久保持 raw 状态：不交给 Y 改写，不随 rebase 消失，在 compaction 与 recovery 中完整保留。same-session FrozenRecordPrefix 必须采用 `includeOpening=false`；其 canonical WorkRecord 仍保存 Opening 事实，但 provider write-back 必须通过 XTrace stable Host identity 明确保留 raw Opening，而不是把 Opening 复制进 Y memory 后删除原消息。Manager 是否已到 T1 不得改变压缩 floor；pre-T1 与 post-T1 普通工作历史一视同仁，Blogger 的 effectiveStart 始终取 `max(RecordCoverage, Life.WorkRecordStart)`，其中 `Life.WorkRecordStart` 仅表示真实 Opening 之后的首个 XTrace 位置，不再扩张到动态 XTrace head 或 BlindPlan T1 commitment 边界。同 session 的前缀替换使用自身历史替换旧前缀，严禁包装为 delegation 字段。

## CONTEXT-COMPRESSION-018: Blogger catch-up 连续追平；禁止 frozen drain frontier；quiet 只等待事件

一次唤醒可驱动多个 ≤200 KiB 的 Blogger cycle 直至追平当前 Current。每个已提交 cycle 后必须基于最新的 Blog coverage 与 XTrace Current 重新计算下一块，严禁冻结截断线。当前无可消费材料代表暂时 caught-up，在当前执行存活期内必须挂起等待 `MaterialAvailable typedContext | Cancelled` 事件；等待没有 lifetime、deadline 或 timeout。进程死亡不跨进程自动恢复旧 continuation。

## CONTEXT-COMPRESSION-019: X→Y 后旧辅助注入不跨 horizon 保留

任何代表 X→Y 冷边界的重锚操作，必须将旧 provider horizon 中的辅助注入（如 pair guideline、tip、grounding read）可见性彻底归零。持久化事件保留作为审计事实，但新 horizon 的初始消息不再回放旧辅助材料，仅在后续各自正常触发时逐步重新生成。

## CONTEXT-COMPRESSION-020: `todowrite` 所在回合永远保留 X 原文

任何包含 `todowrite` tool call 的 Host 消息，以及与该 call id 对应的 tool result 消息，都不得被 Y 前缀替换删除。Prefix cutoff 可以越过这些回合并压缩其余历史，但写回 provider context 时必须从被 drop 的 X 前缀中提取这些消息并原样保留；该规则不依赖 Manager 的 T1/T2 阶段，也适用于 AABB/recovery 后形成的 Y replacement。

## CONTEXT-COMPRESSION-021: Y recovery 由失败会话当场拥有，禁止未来 X material 代触发

`BloggerMain` 的已确认失败在 Fallback advance 后若进入 primed Offset 且当前 X 存在可 squash frames，则该 Blogger 的下一次 continuation 必须先物化 `BloggerRequestContext.Squash` 并发送 `BloggerSquash`；不得先重发失败的 BloggerMain，也不得注册 process-local recovery waiter 等待未来主 X transform。`BloggerSquash` 成功提交后，下一次 provider step 必须从 durable Blog + XTrace 重新派生 BloggerMain；Squash 失败则结束该槽、再次 advance 后才允许进入下一槽。失败 request 的 durable open materialization 必须在任何 retry 前关闭，新物理 retry 必须重新绑定自己的 PromptKey。

## CONTEXT-COMPRESSION-022: BloggerMainContext 是唯一重建公式，canonical XTrace 是唯一输入宇宙

正常 catch-up、squash 成功后的 Main、失败后的 Main retry 与 crash/AABB refresh 必须共用同一个 `BloggerMainContext` 推导：同一 Opening floor、同一 XTrace generation、同一 ingest cursor、同一 200 KiB chunk 与同一 coverage digest 规则。所有路径先通过 `XTraceMaterialization.currentProjection` 得到 canonical X，再进入 `BloggerMainContext`；严禁 normal transform 从 request-local provider presentation 计算 digest、而 recovery 从 XTrace 重建，也严禁在 Coordinator、Enforcer 或 recovery workflow 中复制第二套 next-main 算法。

## CONTEXT-COMPRESSION-023: recovery/park 全事件驱动且时间无关

Context compression 与 provider recovery 的 correctness path 严禁读取 wall clock、构造 `TimeSpan`、调用 timer/deadline/Delay 或以超时作为状态转换。Blogger park 的完成值必须携带 typed event，而非 `bool` 再从第二个槽位取 material。WorkMain recovery 若需要等待正在生产的 Y，只能以 durable `BloggerRequestMaterialized` open request 证明 producer 存在，并订阅 `AgentJournal` 的已提交 change；每个 change 后重新读取 projection，直到出现严格更新的 coverage 或该 durable open request 被 commit/abandon 关闭。`HasFlight`、PendingOffer、进程内 waiter 与时间窗口均不得作为 recovery correctness 证明。

## CONTEXT-COMPRESSION-024: Blogger materialization admission 串行；flight 不得跨 RequestId 覆盖

同一 BloggerSession 的 `BloggerRequestMaterialized` / PromptKey bind / `BloggerRequestAbandoned` 命令必须通过跨 plugin instance 的 process-local materialization admission 串行化；每次取得 admission 后必须重新读取 canonical journal projection 再决定 abandon/materialize，禁止 snapshot-check 与 append 之间存在并发竞态。该 admission 仅保护命令临界区，不是 durable producer proof。live flight claim 必须原子：空槽可建立、同 RequestId 可刷新、不同 RequestId 必须返回 typed conflict 且保留原 owner，严禁覆盖写。normal start、provider retry 与 crash recovery 必须共享这一条 claim/admission 语义。

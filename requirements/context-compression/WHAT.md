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

## CONTEXT-COMPRESSION-006: 恢复槽 = armed ∧ primed ∧ hasMaterial

仅当同时满足 `armedByFailure`（由真实失败激活）、`primed`（处于奇数 Offset）与 `hasMaterial`（存在可用材料）三项合取条件时，才允许执行恢复动作（X 执行 prefix probe，Y 执行 frame squash）。无材料时正常发送主请求。

## CONTEXT-COMPRESSION-007: 按 RequestKind 分派结局

每个 attempt 的结局按 `ProviderRequestKind` 进行确定性分派。同种 RequestKind 的同种结局必须产生完全一致的后继动作，严禁根据错误文本产生分支。恢复槽内的失败继续累加连续失败计数。

## CONTEXT-COMPRESSION-008: X 不发压缩请求

主工作会话（Work Session）严禁向主模型发送请求压缩历史的指令。压缩操作仅在 Y 的 squash 或 X 的 prefix 替换投影中发生。仅 `WorkMain` 请求有资格携带 prefix probe。

## CONTEXT-COMPRESSION-009: 候选未提交不是事实

恢复槽中尝试替换 X 前缀时，候选前缀仅作为 attempt-local 的执行配置，不修改已提交的 `ActivePrefixEpoch`。Probe 失败则直接丢弃候选，不产生任何持久化事实，亦无需回滚。

## CONTEXT-COMPRESSION-010: 候选选择严格新于已提交 epoch

候选前缀必须在 coverage 证明上严格新于当前已提交的 epoch：cutoff 游标不得回退，与已提交前缀不可区分的候选必须直接拒绝，无候选时不构造空 probe 而发送普通请求。

## CONTEXT-COMPRESSION-011: 提交语义分型

X probe 成功时原子提交新 epoch 并继承 SealRoot，失败则无事实；Y squash 成功时提交 squashed observation 并使 FrameEpoch 递增，失败时不修改现有 frames 与 coverage。

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

## CONTEXT-COMPRESSION-017: Opening floor（WorkRecordStart）

Opening 永久保持 raw 状态：不交给 Y 改写，不随 rebase 消失，在 compaction 与 recovery 中完整保留。Blogger 的 effectiveStart 始终取 `max(RecordCoverage, Life.WorkRecordStart)`。同 session 的前缀替换使用自身历史替换旧前缀，严禁包装为 delegation 字段。

## CONTEXT-COMPRESSION-018: Blogger catch-up 连续追平；禁止 frozen drain frontier；quiet 在同一存活执行内必须 park 等未来 material

一次唤醒可驱动多个 ≤200 KiB 的 Blogger cycle 直至追平当前 Current。每个已提交 cycle 后必须基于最新的 Blog coverage 与 XTrace Current 重新计算下一块，严禁冻结截断线。当前无可消费材料代表暂时 caught-up，在当前执行存活期内必须挂起等待未来材料到达，不得直接终止连续追平。进程死亡不跨进程自动恢复旧 continuation。

## CONTEXT-COMPRESSION-019: X→Y 后旧辅助注入不跨 horizon 保留

任何代表 X→Y 冷边界的重锚操作，必须将旧 provider horizon 中的辅助注入（如 pair guideline、tip、grounding read）可见性彻底归零。持久化事件保留作为审计事实，但新 horizon 的初始消息不再回放旧辅助材料，仅在后续各自正常触发时逐步重新生成。

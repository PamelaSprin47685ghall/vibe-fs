# Companion — 理由

每个 Work Session 配叶子 Y，是为了把「可压缩的工作日志」从主会话原始历史中分离，而不把 Companion 做成角色特权。

LWR 自包含跨 Session hand-off；父 LWR 不作 child Seed，防止多代 fork 指数嵌套。

RecordCoverage 与 PrefixCoverage 分型，避免「Y 还没覆盖完就声称可替换 X 前缀」。同 epoch 前缀字节稳定，是 KV-cache 与 ReviewSeal 的共同前提；epoch 切换必须由已提交事实驱动，不能由 token 估算驱动。

Universal WorkRecord 三段 `Opening? / Chronicle / Recent work` 是跨边界唯一通信语言。Opening = 交托关闭前的语义区间（preserved XTrace），不是可重拼 blob。正式陈述 = Recent work 中最后一条助手文本（散文 claim，不是固定 schema；无独立 Closing report 段）。

## 备选与被拒

**Companion 形态：每 WS 配叶子 Y vs 角色特权。** 拒特权：与 Role/Tier/工具面无关（COMPANION-001/002）；把「可压缩工作日志」从主会话原始历史分离，而非给某角色加权限。

**LWR 衔接：自包含跨 Session vs 父 LWR 当 child Seed。** 拒 Seed：多代 fork 指数嵌套（COMPANION-003）。父 LWR 只是 child 输入 context，不复制 Opening/Seed。

**coverage：Record/Prefix 分型 vs 混用。** 拒混用：Y 未覆盖完就声称可替换 X 前缀（COMPANION-003）。RecordCoverage 管 LWR gap，PrefixCoverage 管 prefix 证明，不可互换。

**epoch 切换：已提交事实驱动 vs token 估算。** 拒估算：按容量切 epoch 破坏 seal/前缀稳定。仅 probe 提升与 compaction 重锚两源（COMPANION-009）。

**low-trust 注入：明确标记 context block vs 伪装指令。** 拒伪装：低信任片段（frozen prefix、enforcer tip、historic_frame）必须显式标记，防被当 system/human 指令（COMPANION-010）。

**Opening 材料：`OpeningMaterial` = exact XTrace 区间 `[work start, OpeningBoundary)` vs `OpeningPromptRaw` 拼 `AssignmentText`/`AuthoritativeRequirements`。** 拒拼接重建：重编号 requirements、重写 assignment 会丢掉交托区间内的调查/委派回报/澄清/commitment call+result，并制造第二事实源。Opening 是 preserved，不是 reconstructed。BlindPlan 下 T1 `todowrite` call + canonical accepted result 属 constitutive Opening，不得当 incidental tool 滤掉。

**Opening 压缩：永不压缩 vs 纳入 Blogger/Y/prefix-replace。** 拒压缩：旅程可缩短，章程不可缩短。Opening always raw；Blogger/Y 只从 `WorkRecordStart`（Opening exclusive end）起算。

**WorkRecord 标题：`Opening / Chronicle / Recent work` vs `Opening task / Work log / Uncompressed tail / Final output` 与已删 `Closing report`。** 拒旧标题：把压缩边界说成「未压缩尾巴」、把 claim 说成「Final output」，诱导实现者再造固定报告 DTO。拒第四段 Closing：答案已是 Recent work 最后一条助手文本；独立 Closing 是第二通道。Chronicle/Recent = 表示边界（Y coverage），不是「对读者是否新近」。

**陈述：散文 vs 固定字段 schema。** 拒 universal schema（result/files/tests/risks/blockers）：约束诚实，不约束骨架。角色可自然提及事实；不得把提及义务写成格式。machine-semantic 结构只留在协议真需处（如 `exit_code`、`verdict`）。

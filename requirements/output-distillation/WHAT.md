# output-distillation — WHAT

## DISTILL-001: 大输出有损但诚实地压成固定成本 bounded observation

当执行输出超过 participant horizon 的承载上限时，系统必须将其压缩为 bounded observation。蒸馏输入最多取 spool 最近 200 KiB；更早内容被丢弃时必须显式声明截断边界。输出规模不得提高模型并发度。

## DISTILL-002: 保留会改变后续 judgment 的事实

在 bounded tail 内，压缩必须优先保留具体且具备区分度的关键印记：错误类型、带行号的文件路径、失败的断言信息、panic 或异常、互斥的矛盾行、以及携带上下文的原始错误尾部。对已截断的更早内容不得声称已观察或已验证。

## DISTILL-003: tail 谦逊——沉默的 tail ≠ 整体成功

最近 200 KiB tail 中未包含失败文本不等于全局执行成功。只要 spool 超过输入上限，结果必须明确承认更早字节已截断，严禁将局部无异常升级为整体验收结论。

## DISTILL-004: 禁止按输出大小自动 fan-out / reduce

一次 spool 蒸馏恰好最多创建一个 Distiller。严禁按 chunk 数自动创建多个 map Distiller，严禁再创建 merge/reduce Distiller，严禁让输出字节数决定模型子会话数量。

## DISTILL-005: 蒸馏结果对未见过原始输出的 reader 仍可用

蒸馏产出的摘要必须保持自包含与可定位性，使从未接触过原始大文本的读者能够仅凭摘要中的路径、行号、错误线索与截断声明理解当前可见现场，严禁假装包含未观察到的上下文。

## DISTILL-006: 唯一 Distiller 失败 = 明确失败 + bounded raw tail

唯一 Distiller 发生超时或不可恢复失败时，系统严禁伪造完整成功，必须返回失败说明并附带同一 bounded raw tail 作为最近未压缩证据；失败代理的工作记录不得作为成功摘要呈现。对该 owned Distiller 的物理取消至多一次。

## DISTILL-007: 蒸馏输入是 spool；流式丢弃旧字节，只保留最后一个固定窗口

蒸馏机制消费流式落盘的 spool 文件，但读取过程中只保留最后一个 `Spool.ChunkSizeBytes` 窗口；旧窗口立即丢弃，不累积 chunks，不建立 map task 数组，不执行 online reduce。非空 spool 最多启动一个 Distiller，并且只等待该 exact agent。

## DISTILL-008: 单 Distiller 定向 await；permit 门分型

唯一 Distiller 的等待必须通过恢复准入门并绑定 exact agent id。遇到 FamilyWaiting 时只等待 journal readiness 事件后重新取得 fresh permit；遇到不可恢复故障时直接失败，严禁无凭证伪造就绪状态或改等别的 agent。

## DISTILL-009: Distiller 是私有叶子 runtime，不进公开 fork/horizon，也不配 Blogger

蒸馏子会话属于宿主私有运行时，具备隐藏句柄所有权，不得向外部模型暴露为公开的 fork 或 horizon 目标。Distiller 是叶子 runtime：Companion attachment 对 Distiller 必须拒绝，严禁为 Distiller 创建 Blogger 子会话。

## DISTILL-010: Distiller 不执行、不改变世界、不裁决

蒸馏角色仅承担“在长度压力下进行事实提炼”的单一职责，不具备外部命令执行、世界状态修改或代码验收裁决的权能，与命令执行角色严格解耦。

## DISTILL-011: Large Gate 与输出预算合同一致；禁无界缓冲

大输出进程的并发执行必须受到单持有者门禁（Large Gate）的严格约束，输出预算估算必须与门禁获取逻辑一致。内存缓冲必须设有上限并在超限前提前流式落盘，严禁无界缓冲。

## DISTILL-012: 自定义 tool 文本结果确定性留尾截断

插件回传给宿主的自定义工具文本结果在触发宿主默认头部截断前，必须在指定行数与字节限制下完成确定性留尾截断，保留显式截断标记与最新的尾部完整行，保证最终结果满足边界且不再被二次随机截断。

## DISTILL-013: 蒸馏不返回 chunk 统计仪表盘

蒸馏输出严禁包含分块索引、切片层级、分块计数或成功百分比等反映底层机械切分过程的仪表盘数据。向外部呈现的必须是实质性的业务事实与未决印记。

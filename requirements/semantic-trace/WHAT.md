# semantic-trace — WHAT

## SEMANTIC-TRACE-001: XTrace 是 X 的唯一 append-only 原始语义历史

工作会话的生命周期语义轨迹由仅追加（append-only）的 XTrace 承载，它是会话唯一的原始语义事实源。在会话生命周期内：初始任务 Opening 捕获一次且永不覆盖；语义部件（Parts）按游标严格单调追加；每个已完成的 ProviderRun 产生至多一个绑有确定历史前沿的 TerminalOutputCaptured。同一 ProviderRun 的相同重放具有幂等性，冲突的输出必须拒绝。

## SEMANTIC-TRACE-002: typed capture 边界

XTrace 仅捕获具有语义约束力的实体：宿主可见 prompt、助手正文、推理过程、工具调用及工具返回结果，以及必要的省略标记。严禁记录 UI 增量、用量统计、成本、时间戳、目录路径或传输状态标识。以物理部件身份结合消息与运行标识作为幂等凭据，防止由于数组索引漂移导致重复记录。

## SEMANTIC-TRACE-003: cursor 严格单调、独立于 Host 坐标

XTraceCursor 在会话生命周期内严格单调递增，独立于宿主内部的转录数组索引与语义轮次编号。相同或回退的游标追加请求必须直接拒绝。宿主上下文压缩（compaction）与重锚发生后，已记录的游标与覆盖范围作为持久事实完整保留，绝不随之重置。

## SEMANTIC-TRACE-004: provenance 按 provider run 分段

XTrace 的溯源信息按独立的 provider run 进行分段标记，而非使用单一模型名称。在发生上下文重锚后生成新的代际标记（generation），确保重编号后的宿主轮次不会与既有代际的历史标识发生碰撞。

## SEMANTIC-TRACE-005: semantic parts 与 transport/wire identity 分离

XTrace 实体在内部持有溯源归属，但在执行语义渲染输出时，严禁向外部包含内部 call_id 或传输跟踪标识。语义渲染必须保证确定性，相同语义内容在不同重放环境下产生完全一致的渲染文本。

## SEMANTIC-TRACE-006: 稳定 frontier / range / cutoff

XTrace 提供确定性的半开区间定位能力，支持按起始点与排他终点精确切片。工作记录覆盖游标作为持久化事实记录消费进度，允许精准落在半轮位置。在审查前沿建立时，必须基于完整的宿主快照与持久化 XTrace 精确收敛，消除未完成传输切片的不确定性。

## SEMANTIC-TRACE-007: XTrace 是 Y delta / LWR gap / terminal 的单一 source

下游的增量压缩输入（delta）、生命周期工作记录缺口（LWR gap）与终端输出捕获均以 XTrace 为单一事实源。针对不同消费场景采用确定的同源投影规则，严禁针对同一历史片段维护多套相互冲突的解析逻辑。

## SEMANTIC-TRACE-008: 未发生材料永不写成历史

捕获流程严禁将尚未实际生效的投机执行或临时状态提前写入历史：未被正式确认的投机候选绝不进入 XTrace，失败的探测尝试不记录事实变更，可追加的事实类型严格限于已确认的 Opening、Part 与 Terminal 事实。

## SEMANTIC-TRACE-009: Host compaction 不得删除 XTrace

宿主层面的上下文重锚与压缩属于传输窗口的视图管理，严禁删除、清空或覆盖既有的 XTrace 记录。重锚事实仅更新前缀纪元并重置前缀覆盖范围，已持久化的部件、Opening 与历史记录覆盖游标必须全量存活。

## SEMANTIC-TRACE-010: Opening 在 trace 内 preserved

初始任务材料（OpeningMaterial）严格对应会话起始至初始边界之间的 XTrace 区间。该区间内的根本性承诺与初始交付物作为宪章性材料完整保留，不作为普通偶发工具滤除，同一文本重放幂等，异构文本尝试覆写直接拒绝。

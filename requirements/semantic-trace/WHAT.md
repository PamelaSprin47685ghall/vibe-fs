# semantic-trace — WHAT

## [001] XTrace 是 X 的唯一 append-only 原始语义历史

XTrace 是工作会话唯一的原始语义事实源，只能追加。Opening 捕获一次且不覆盖；Parts 按游标严格单调追加；每个已完成的 ProviderRun 至多捕获一个绑定确定前沿的 Terminal。相同重放幂等，冲突输出拒绝。

## [002] typed capture 边界

捕获范围限于任务与续接 prompt、助手正文、推理、工具调用与结果及必要省略标记，不含 UI、用量、成本、时间、目录或传输状态元数据。幂等凭据使用物理部件、消息与运行身份，不依赖数组下标。

`ProviderRetryAttempt` 必须按 Host 的 typed origin 识别，不按正文猜测。其物理身份可保留坐标，正文不得进入 XTrace、Y、WorkRecord 或 prefix digest。

transform 与完整 snapshot 使用同一 typed origin 裁决；缺少物理 part ID 仍是缺失，不能用 callID 冒充。真实 callID 可以在独立字段内参与材料去重。材料种类与 raw 身份分开编码，不能因 raw ID 含 facet 标签而碰撞。

user 来源遵守 interaction-authority-008：同 Session 的确切已确认物理消息优先，其次确切未决 PromptKey，最后才用 Host 诊断来源标签。不能凭正文、其他 Session、已被接受的旧 key 或当前运行猜来源；Host 标签也不得覆盖持久证据。新捕获的 message、地址类型、地址与 facet 采用无歧义的结构化编码，完整保留 opaque 身份；旧未转义且有歧义的历史不补造缺失身份。

同一确切物理 part 只接受同材料重放，或共享角色、ProviderRun 与 ToolCallId 的 tool-call/tool-result 两个 facet；不允许其他 kind 改写。同批冲突须在首条写入前拒绝。旧历史缺失物理字段且旧路径有歧义时保留未知，不补造物理身份。

Stable historical insertion 须有 durable journal、非 legacy positional trace，且普通 Host 消息身份全部存在、非空、互异。资格查询与捕获复用 trace 所有者的同一纯裁决，消费者不得重写判断。

## [003] cursor 严格单调、独立于 Host 坐标

游标独立于 Host 下标与轮次，在会话内严格递增；重复或回退的追加拒绝。同一会话的并发捕获须在分配游标前串行化，不能先分配冲突游标再拒绝一方。该顺序不授予业务进度或恢复资格；compaction 与重锚不得重置已有游标和历史覆盖事实。

## [004] provenance 按 provider run 分段

溯源按 ProviderRun 分段，不能只记模型名。重锚后启用新 generation，重编号的 Host 轮次不得与旧历史身份碰撞。

## [005] semantic parts 与 transport/wire identity 分离

溯源身份保留在内部，不进入语义渲染。同一语义内容在不同重放环境下须渲染为相同文本，不含内部 call_id 或传输跟踪标识。

投影状态、游标与区间由 trace 所有者封装。跨所有者只提供复制的语义部件、Opening/Terminal 证据及精确查询，不公开内部状态、追加引用或游标字段。

独立原件 owner 保存已确认材料的真实字节与语义种类。合法缺失的 ProviderRun 或 Host 消息完成状态须保留缺失，不得按 part 游标合成 run/message 并补写 Completed。Terminal 用明确的 ProviderRun 与固定前沿绑定；它不是库的最后助手散文视图。冷 replay 必须校验同一 durable envelope 的 payload 与摘要，缺失或冲突不推进原件前缀。消费者从该 owner 取材，不重新读正文后另建临时 Trace。

## [006] 稳定 frontier / range / cutoff

历史按半开区间精确切片；工作记录的持久覆盖游标可落在半轮。审查前沿须由完整 Host 快照与 durable XTrace 收敛确定，不能使用未完成传输切片。

按 stable Host identity 定位时，请求集合须全部且恰好出现，按历史顺序构成单一连续区间；缺失、夹入额外身份或游标缺口均返回无证据。

游标创建、序列化、比较、覆盖与区间包含关系由 trace 所有者计算，消费者不得重写公式。

canonical 消费坐标由 Trace 按 durable 游标顺序生成。一个 Host 工具的 call/result、晚到结果与其他已捕获部件都占独立语义位置；不得按旧 Host 下标把晚到材料插入绝对前缀。Blogger 的 NextCursor 只能覆盖本次已提供的连续前缀，换算与物化共用同一坐标规则。边界若截断单个 Host 消息的多个 canonical 段，整条物理前缀替换无证据；不能只过滤该消息后继续替换。

## [007] XTrace 是 Y delta / prefix proof / LWR gap / terminal / 案例来源的单一 source

Y delta、prefix proof、LWR gap、Case sourceTrace 与 Terminal 均从 XTrace 派生。canonical projection、其有界切片与 WorkRecord 渲染由 trace 所有者统一物化，不得另设冲突解析。

请求级呈现与辅助注入不得成为持久 prefix coverage 的证据，也不得与自身旧呈现的 hash 对账。捕获返回 typed receipt/error，终结返回 typed completion evidence；装配层只安排顺序，不读内部状态猜结果。

## [008] 未发生材料永不写成历史

XTrace 只追加已确认的 Opening、Part 与 Terminal。未确认的投机候选、临时状态和失败探测所尝试的变更不得写成已发生的历史。

## [009] Host compaction 不得删除 XTrace

Host compaction 与重锚不得删除、清空或覆盖 XTrace。重锚只更新 prefix epoch 并重置当前 prefix coverage；已有 Parts、Opening 与工作记录覆盖游标完整保留。

## [010] Opening 在 trace 内 preserved

OpeningMaterial 精确对应会话起始至初始边界的 XTrace 区间，完整保留根本承诺与初始交付物，不作为普通工具材料滤除。相同材料重放幂等，冲突改写拒绝。

## [011] Fission keyed convergence 与多 Present 轨迹归并

Engineer Fission 的各 lane 产生 keyed 溯源部件，按确定性规则汇聚，保留归属与因果关系，不按偶然到达顺序拼接。整项工作形成统一终结前沿，供 WorkRecord 与 Casebook 一次性消费。

## [012] 独立 Invocation 范围与 Resume 边界

同一物理 Session 的每次 invocation 均有明确、隔离的 `[InvocationStartCursor..InvocationEndCursor)`。WorkRecord 与 Casebook 按该区间取材，不得跨 resume 边界混合独立工作。

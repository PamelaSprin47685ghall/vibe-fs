# guidance-delivery — WHAT

## [001] Frontier 与 Coverage 独立

TipDeliveryFrontier 按 TipOccurrence 记录已向该 Main 首次交付的事实，持久、单调，重锚不重置。TipSemanticCoverage 按 TipName 记录完整处置手册在当前 provider horizon 的可恢复性，重锚时清空。两者不得合为单一标志。

## [002] Full 与首次交付

occurrence 未交付或规则全文已不在当前 Coverage 时，呈现 Full：规则提示标题、TipName 和按 owner 语言选择的完整处置正文。首次交付须原子记录 TipGuidanceDelivered 并推进 Frontier；语义恢复遵守 [005]。

## [003] IdentityOnly

occurrence 已交付且规则全文仍在当前 Coverage 时，仅呈现稳定的 `tip: <name>`，不重复正文、不推进 Frontier，不把简写当成全文可永久恢复的事实。

## [004] Durable Facts 决定交付

交付判定只依赖持久事实的投影，按 Main session 隔离；重启、恢复与重试保持确定。进程内集合、临时文件或未持久账本不得成为判定依据。

## [005] 重锚后的语义恢复

ContextReanchored 仅清空 Coverage、保留 Frontier。再次遇到已交付规则时恢复 Full，不新增 occurrence、不推进 Frontier，不留下只有身份而无正文的引用。

## [006] Owner 解析与缺失

Main 或其 Blogger ID 通过会话关联解析到 owner Main，使用其最近已提交 tip。无关联、无 tip 或无对应规则时返回 None，不编造 guidance。

## [007] 历史别名等价

latestTipNudge 与 latestTipGuidance 对相同输入返回相同字节，不增加评分或控制流程。

## [008] Audience 隔离

检测正文只供 Blogger system prompt，处置正文只供 Main guidance；两端共用 TipName，不混用正文。Blogger 的历史 tip 是低信任观察，不得成为 Main Authority。

## [009] 交付不创建 Authority

guidance 随结对指引，以 NUL+BOM 分隔后附在真实终端工具结果尾部。不伪造工具调用或用户消息，不创建 Interaction Authority Root，不改变权限或主体。

## [011] 已投递字节冻结

每个自动注入 occurrence 持久记录序号、CallId、放置点与实际 wire payload 原文字节。历史重放使用这些字节，不随规则版本改写；拒绝序号错乱、重复 CallId 和同一放置点的重复记录。

## [012] 新 occurrence 的动态材料

新 occurrence 合成 latest tip guidance、session elapsed、remaining expected tool calls，以及待交付的 concern 订阅公告或邮箱消息。各动态材料只从所属 O(1) 投影读取一次；最终 MarkerText 原子持久化后冻结，重放不重新渲染或消费动态材料。

# behavior-diagnosis — WHAT

## [001] live Rulebook 与唯一身份

TipName = RuleId = FieldName。内置规则（目录 basename 为 TipName）与获准的 durable InstitutionalRuleBorn 合成唯一 live Rulebook，共用身份空间、Blogger system prompt 和 Main 处置索引。身份冲突 fail closed，不设平行目录、身份清单或规则真源。

## [002] 装载 fail-fast，准入先校验

内置规则装载缺失、空白或非法时，启动立即进程级 fail-fast，不跳过、降级或使用备用规则。institutional candidate 在追加前须通过同一结构、身份与双语校验；预检绑定精确 RulebookRevision，原子提交前若 revision 变化，则整笔学习事务 KnownNotCommitted、零提交，由上层重评。

## [003] live union 校验合同

schemaVersion 为 1，至少一条规则；Name、RuleId、FieldName 各自唯一且相等，EnforcerText/MainText 及双语正文 trim 后非空。LexicalOrder 从合流集合确定性派生为连续 1..N，不使用事件自带的竞争序号。

## [004] 完整、确定的检测语料

Blogger effective system prompt 由基础 prompt、具名的完整 live Rulebook 及当前语言的 EnforcerText 全文按 LexicalOrder 合成。同一输入产生相同字节；结果仅为内存派生产物，不写回文件或成为第三真源。

## [005] 双语叶子完整，无 fallback

内置与 institutional 规则遵守同一语言合同。每个 institutional BIRTH 同时提供 English、zh-CN 的完整 EnforcerText/MainText，身份一致且正文非空；缺任一语言即拒绝。投影按 provider 语言选择，不跨语言 fallback。

## [006] chronicle 参数与 NoLiveCycle

chronicle 必须有 trim 后非空的 entry 和可映射为 TipName 的 string tip；缺失、空白或非 string tip 返回稳定错误。无存活 Blogger cycle 时返回封闭的 NoLiveCycle 协议结果并终止过时 session，仅最外层工具适配器将其编码为 Host 异常。

## [007] tip 的确定性最近映射

trim 后精确命中 TipName 则直接选择，否则按 Levenshtein 距离取最近规则，并列取最小 LexicalOrder。任何非空 string 必须得到一条规则，不设 UnknownTip 或逃逸分支；RuleId、FieldName、TipName 保持相等。

## [008] 离散 tip，不使用数值评分

诊断只选单条 tip，不暴露或恢复 Scores、数值严重度、评分解析及旧 ScoreWhen/Nudge/Family/CatalogOrdinal 桥接字段。额外数值输入安全忽略，不参与判定。

## [009] 每个 provider run 恰好一次 chronicle

只有原始 assistant step 恰好一次 chronicle 才可形成有效 cycle。零次或多次不得合并、提交 BlogObservationCommitted 或推进 coverage；terminal 前等待 Host 工具收敛，terminal 后进入有界协议修复。

## [010] provider/tool 身份门禁

单次调用基数合格后仍须有可验证、非空的 ProviderRunIdentity（messageId），缺失触发 enforcer-cycle-failed 致命失败。ToolCallId 缺失或非法则进入有界协议修复；身份证据不足不得提交。

## [011] 内容硬界由单一纯判定拥有

单 cycle 规范化日志不超过 512 KiB UTF-8，证据不超过 128 KiB UTF-8；超限 fail closed 并报告 enforcer-cycle-failed。阈值、计量与拒绝共用唯一纯判定，解码、提交与测试接缝不得复制。硬界不作为业务启发式评分。

## [012] 唯一原子 cycle 事实

正常 cycle 仅提交 BlogObservationCommitted，一并发布 frame、RecordCoverage、单一 TipRuleId/FieldName、provider/tool 身份和大文本引用。监督状态由该事实纯派生，不另设 EnforcementCycleCommitted；日志与 coverage 同生共死。

## [013] coverage 出生与提交校验

Next ≤ Prev 或 NextCursor 不可映射时，不生成 context chunk、不启动零推进窗口。提交前核对 staged cursor/cutoff/epoch 与当前投影；失配返回 KnownNotCommitted 并可恢复弃置，不先写事件再交 fold 拒绝。

## [014] RecentTip 有界、幂等

每个已提交 cycle 恰好派生一个含 RuleId、FieldName、CycleId 的 RecentTip，最多保留 8 项，严格 oldest → newest。同一 ProviderRun 重复提交幂等拒绝，不产生重复收据。

## [015] tip 与 frame 的配对观察

Observation 是 tip 与 frame 的配对视图，按顺序前向 zip，剩余侧 unpaired 追加；tip 锚定视图省略无 tip 的 frame，不编造 tip，不以独立平行数组作为权威历史。

## [016] squash 只改变历史表示

最老 K 个 frame 折为一个 Squash frame 时，同步裁去最老 min(K, tips) 条 RecentTips。Squash 不新增 tip、不触发新 Main 交付，不创造 occurrence。

## [017] 有界、幂等的协议修复

无效 terminal（零/多调用、缺 tip、空 entry）只由唯一 Blogger 协调者认领与发送修复：完全静止的 idle 才可首发 Nudge，transform 不发送；每个 RequestId 至多一次，纯文本 terminal 由 SessionIdle 唤醒。

Nudge 后新的 ProviderRun 再次无效，记录 confirmed failure 并首发 AABB；保持同一 RequestId 与目标 terminal。相同 run 的重复观察只返回 AlreadyAdmitted 并等待，不当成发送失败或提前推进 AABB，不按错误字符串区分。重判只消费 ToolName=chronicle 的 completed 执行证据，不猜测或兼容别名。

同 episode 重复观察不重复认领/发送，非 quiescent 不花预算；Nudge、AABB 各至多一次，耗尽后一次 abandon、exact release。无 journal 直接 abandon、零发送。Shutdown 与准入原子关闸，拒绝新 episode，取消并 drain 已认领者。

## [018] RulebookRevision 按 life 冻结

Blogger life 创建时绑定同一 RulebookRevision 的 system prompt、tip 枚举、解码映射和 Main 索引，存活期间四者冻结。新规则不打断 in-flight cycle，只在下一 cycle 创建 fresh life 时生效。

## [019] typed incident 经必需注入执行 fatal

先取得 typed protocol-exhausted 或 commit-unknown 证据，完成该路径的 durable settlement，再构造 incident。runtime 只使用构造时必需注入的 fatal capability，不直接引用物理实现，不设 optional/default/global fallback，也不由 Host/Journal 重复 fatal。同一 incident 只报告、终止一次。

## [020] 语义可区分性由人类 Review

规则的正交性、可区分性与无冲突性由作者及同行 Review 保证。运行时不设词法重叠或相似度门禁，不因文本相似阻止合法交付。

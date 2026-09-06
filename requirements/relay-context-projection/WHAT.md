# relay-context-projection — WHAT

## PROJ-001: audit history 完整，provider history 按 retirement cut 切段

物理 transcript 与 durable audit 不删除前任消息。下一迭代的 provider projection 必须排除 projection cut 之前属于退休迭代的 raw messages、tool calls、results、nudge 与迟到旧 run parts。

## PROJ-002: cut 覆盖 suicide request 与 tool result，并丢弃内部 wake

accepted retirement 的 cut frontier 必须覆盖前任最后 assistant part、suicide tool call、其 tool result 及同 causal batch 的内部 ack；不能用最近 N 条、时间戳或文本搜索 suicide 推断。retirement tool 之后、下一个真实用户需求之前的消息分两类丢弃：一是 retired run 自己的 racing tail（closing、phantom join），二是仅用于唤醒循环的内部 loop wake continuation（与 `runtime/manager-assess` 同文的首个非 authority user continuation，projection 按位置与身份剥离，不按文本/路径识别）。尚无后继真实用户输入时整个尾巴都丢。真实用户需求与 typed authority 消息永不过 cut。cut 只认位置、role 与 typed message identity，不认文本、不猜 run、不等物理 arrival。

## PROJ-003: provider 消息上下文只含 typed authority 内容与当前迭代消息

下一迭代的 provider 消息上下文只包含 typed authority 消息内容与当前迭代消息：前任 chain-of-thought、原始长日志、secret 与循环唤醒虚构文本一律不得进入，wake 消息本身从 provider context 中移除。工作区是共享执行状态，Manager 直接检查它，projection 不转述它；当前 phase 只由可用 capabilities 表达。AuthorityRevision、SnapshotId 与 phase ID 永不序列化进 provider 消息。


## PROJ-004: 每一迭代使用同一 review-first 起点

所有迭代使用同一 review-first 结构：先独立评审当前完成情况和质量。首迭代只陈述当前权威需求，不伪造前任 commit、测试或结论；后续迭代同样只陈述当前权威需求，不复述前任私有过程。工作区是共享执行状态，Manager 自行检查工作区，projection 不负责叙述工作区内容。

## PROJ-005: 物理 SessionId 与用户线程保持连续

迭代切换不得为了缩上下文创建新的用户聊天线程或删除历史。SessionId 可以跨迭代复用；IncumbencyId 和 provider context 不得复用。

## PROJ-006: projection 是确定性过滤，不生成合成列表

projection 只是对 durable facts 的确定性过滤：相同 facts 产生相同 provider 可见集合，不生成任何合成消息与合成列表。不得包含 hidden reasoning、完整 transcript、raw diff/log、token 或 credential。

## PROJ-007: crash recovery 不得回退 cut

已 committed retirement/cut 在 Host crash 后仍是 provider projection 的下界。任何退休后开启的新 active 迭代一律以上一次 LatestRetirement cut 为下界，包括 Accepted 退休经显式证书失效后重开的迭代。两种 outcome 都在 transform 边界中断已退休 attempt 的后续 provider 请求；只有 Continue 自动激活下一迭代。迟到旧 ProviderRunIdentity parts 只能进入 audit 或 stale diagnostics，不能进入下一迭代 context。

## PROJ-008: authority message chain 跨 cut 保留，普通前任消息与内部 wake 不借机穿透

初始 root authority message 与之后每个 durable accepted `AuthorityRevision` 对应的物理 authority message 都是 Road 的权威输入，projection 必须按 typed message identity 保留它们，即使它们位于 predecessor cut 之前。除此之外的 predecessor user/assistant/tool/nudge 原始消息仍必须被 cut；内部 loop wake continuation 即使形如 user message，也因其非 authority 身份而被移除；不得为了保留追加要求而放宽成“保留所有 user message”或文本匹配例外。

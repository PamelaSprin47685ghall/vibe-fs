# relay-context-projection — WHAT

## PROJ-001: audit history 完整，provider history 按 retirement cut 切段

物理 transcript 与 durable audit 不删除前任消息。successor provider projection 必须排除 projection cut 之前属于退休任期的 raw messages、tool calls、results、nudge 与迟到旧 run parts。

## PROJ-002: cut 覆盖 suicide request 与 tool result

accepted retirement 的 cut frontier 必须覆盖前任最后 assistant part、suicide tool call、其 tool result 及同 causal batch 的内部 ack；不能用最近 N 条、时间戳或文本搜索 suicide 推断。retirement tool 之后、下一个 user turn 之前的非 authority 消息是 retired run 自己的 racing tail（closing、phantom join），一律丢弃；尚无后继 user turn 时整个尾巴都丢。user turn 本身永不过 cut。cut 只认位置与 role，不认文本、不猜 run、不等物理 arrival。

## PROJ-003: successor 上下文只注入当前权威事实

successor provider context 必须包含最新 root authority、AuthorityRevision、current WorkspaceSnapshotId、bounded BatonEnvelope、requirements/evidence refs 与当前 phase capability；不得包含前任 chain-of-thought、原始长日志或 secret。

## PROJ-004: 第一任使用 ExistingWorld typed source

第一任与 successor 使用同一 review-first prompt。第一任 BatonSource=ExistingWorld，只陈述“此前已有其他同事负责”的稳定叙述，不伪造前任 commit、测试或结论。

## PROJ-005: 物理 SessionId 与用户线程保持连续

任期切换不得为了缩上下文创建新的用户聊天线程或删除历史。SessionId 可以跨任期复用；IncumbencyId 和 provider context 不得复用。

## PROJ-006: BatonEnvelope deterministic、bounded、secret-safe

Baton 只能由 durable facts 机器生成，相同 facts 产生 byte-for-byte 相同 canonical payload/digest。列表按 Contract 上限确定性截断，保留 digest/evidence ref；不得包含 hidden reasoning、完整 transcript、raw diff/log、token 或 credential。

## PROJ-007: crash recovery 不得回退 cut

已 committed retirement/cut 在 Host crash 后仍是 provider projection 的下界。迟到旧 ProviderRunIdentity parts 只能进入 audit 或 stale diagnostics，不能进入 successor context。

## PROJ-008: authority message chain 跨 cut 保留，普通前任消息不借机穿透

初始 root authority message 与之后每个 durable accepted `AuthorityRevision` 对应的物理 authority message 都是 Road 的权威输入，successor projection 必须按 typed message identity 保留它们，即使它们位于 predecessor cut 之前。除此之外的 predecessor user/assistant/tool/nudge 原始消息仍必须被 cut；不得为了保留追加要求而放宽成“保留所有 user message”或文本匹配例外。

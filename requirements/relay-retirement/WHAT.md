# relay-retirement — WHAT

## RETIRE-001: suicide 是唯一正常模型出口

正常 assistant stop 不产生 retirement。模型正常离场只有 accepted `suicide`；authority revocation、session deletion、fatal fuse 与 provider capacity exhaustion 属于独立 exceptional terminal，不伪造 suicide。

## RETIRE-002: 质量、进度、测试、义务与 Git 状态不阻塞 retirement

未 assessment、低分、open obligations、失败测试、dirty worktree、untracked、index conflict、未 commit 或半成品均不得成为 suicide admission blocker。

## RETIRE-003: 递归 live resources 是唯一业务 blocker

suicide 只检查当前 IncumbencyId 直接或递归拥有的 live child、background job、PTY/process、active tool execution、side-effect/execution lease、同步 descendant provider work 与未观察到 terminal 的 cancel/join。

## RETIRE-004: retirement 必须 freeze-before-check

admission 先冻结，再读取 exact recursive ownership projection。冻结前已 accepted 的资源必须阻塞；冻结后的创建因 stale fence 被拒绝。freeze fence 绑定精确 `IncumbencyId`，不得只绑定可复用的物理 `SessionId`；前任退休后 successor 即使复用同一 SessionId，也不得继承前任 fence。若有 blocker，只恢复当前任 cleanup capability，不恢复新工作 admission。

## RETIRE-005: normal stop nudge 按 causal frontier 去重且无固定次数上限

每个新的正常 assistant terminal frontier 在仍 active 且没有 accepted suicide 时最多产生一个 outstanding nudge；同一 frontier replay 幂等。新的 terminal 可继续产生下一 nudge，不使用 timer、sleep 或固定 retry 次数。

## RETIRE-006: provider failure 与 authority terminal 不进入 nudge 代数

provider/network failure 先由 ExecutionFailurePolicy 结算；只有恢复出新 provider admission 且 incumbency 仍 active 才继续协议。authority revoked、session deleted、fatal fuse 与 capacity exhaustion 停止 nudge。

## RETIRE-007: retirement、baton 与 cut 是不可分割的可恢复提交

成功 retirement 必须在同一 durable transaction 中记录离场 snapshot、IncumbencyRetired、BatonPrepared、ProjectionCutRecorded 以及 SuccessorRequested 或 QualityCandidateAccepted。崩溃恢复不得看到永久的“已退休但无 baton/cut”状态。

## RETIRE-008: retirement 不做 session 级物理 abort

suicide 提交 durable retirement 后不得调用 session 级 `InterruptAttempt`/`AbortSession`。这类 abort 只能命名 session，不能命名 retired run；Host 在不可观测的延迟后才真正执行，而 successor 复用同一物理 SessionId，迟到的 kill 会落进后任 run 的 tool body 中间并杀死它。已退休 run 的后续 provider 请求在 transform 钩子按 exact 身份拒绝：每个 provider 请求都经过 transform，退休 run 的延续请求在那里被拦下，run 自然结束；后继 prompt 与后继 run 持有正式 gate 身份，不受影响。已退休输出的 containment 只靠 durable cut（`StaleProviderRunIds` 吸收迟到 parts）与 Retired phase tool denial。


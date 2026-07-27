---
import:
  - README.md
---

# 工程执行约束

本文件只描述工程约束、当前证据、未闭合边界和执行顺序。它不是产品语义副本，也不是聊天审计归档。

## 权威来源

1. `next/Doc/SSOT.md`：产品语义唯一真理源。实现、测试、工具面、持久化和宿主边界以它为准。
2. `AGENTS.md`：工程执行纪律、当前证据和纠偏顺序。
3. `README.md`：面向用户的已实现事实与入口说明，不得宣称未验证能力。
4. `next/Doc/host-docs/`：OpenCode 宿主事实调查，不是产品设计。
5. 旧 `src/`、旧 `tests/`、Mux、OMP、Mimocode：冻结黑盒 Oracle；不得把旧实现重新导入 `next/`。

语义冲突按上述顺序裁决。不得把本文件、README 或测试夹具写成第二份 SSOT。

## 本轮纠偏证据

当前工作树在纠偏前为 `e499e41d`，已建立本地冻结标签 `correction-freeze-e499e41d`，冻结对象不得被后续工作覆盖。

选定的历史锚点为 `834cb579`：它保留已验证的 Fork/Join、Companion B 累积和 Process 基础，同时尚未引入本轮测试驱动的生产污染。融合不是 checkout、整文件替换、ours/theirs 或盲目 cherry-pick；按文件、符号、行为逐行取舍。

第一阶段纯回滚提交：`ae51da07 fix: roll back test-driven production pollution`，已推送至 `origin/worktree-0-branch`。

已从生产代码删除：

- `RunnerCore` 的 `WANXIANG_RUN_ID`、`wanxiang-ledger`、`/proc` 台账、全局 `activeChildrenJs`、`killAllActive`。
- 插件 `dispose`/`unload` 对所有 Runner/PTY 的无归属全局清理。
- `Sessions` 传输层的 60 次重试、固定 50ms 轮询、Fallback 事实伪造和伪造 terminal。
- `GitTree` 对未跟踪文件的 fail-open 忽略；读取失败现在阻止树哈希完成。
- `PtySurface` 及 `ToolSurface` 中把普通 shell 冒充 PTY 的分支、signal 参数和 PTY 列表伪装。
- `npm test`/`test:e2e:p0` 的自动全机 Reaper 前置步骤。

保留：

- TestKit 自己持有的进程句柄、进程组、spawn ledger 和诊断能力；它们不得进入 `next/Process` 或 `next/OpenCode`。
- P0 的并行执行。并行是隔离契约，不因测试困难退回串行。
- 2s scenario-local Watchdog。它是死锁探针，不是业务成功条件；心跳必须是当前场景因果图的真实进展，不是任意 SSE、health check 或无关 Blogger 噪音。统一值来自 `testkit/opencode/watchdog-constants.js`。
- `next/Process/Pty.fs` 的窄 Port/行为测试。真实 PTY 未接入模型工具面前，不得创建第二个伪 PTY 表面。

纠偏提交已验证：

- `npm run build`：Fable 编译通过，69 个生产源文件。
- `npm run test:manager-tools`：1/1 通过。
- 纠偏提交前后均无未提交文件；推送目标为当前工作分支。

上述是本轮直接证据。未在本轮重新执行的旧 canary、全量测试和 release 结论不得重新写成“已通过”。

## HEAD 代码与本轮直接证据

当前 HEAD 为 `fba7ad82`。在 `bf632dea`（busy nudge、Process 分层摘要、真实 bun-pty、Companion canonical baseline、Fallback durable identity、Orchestrator Git authority/global SSE/reconcile hook）之上，本轮又接入：Executor 单终摘要免 reduce、PTY 真实 spawn/signal 链路、同进程 Journal 按 runtime 目录共享、Orchestrator ManagerJob durable 恢复、mock 确定性 tool-call id；符号存在不等于行为闭合，以下验收状态只按本轮命令结果提升。

本轮直接通过：

- `npm run build`。
- `npm run test:compile && npm run test:next`：164/164 通过（含 `RecoverManagerJob` durable 恢复契约）。
- `npm run test:manager-tools`：1/1 通过；`node testkit/opencode/tests/gate-testkit.mjs`：23/23 通过。
- 全部 14 个 canary 单场景各自通过。
- 本轮标准 `npm run test:e2e:p0` 通过：14/14 连续两次（默认入口、默认并行池）。并行红门的根因不是测试隔离，而是两类已修复缺陷：四个 canary 的 lane 模型落后于真实产品行为（ReviewGuard 对未确认 Manager terminal 的 nudge、Companion Y 自压缩 condense 与 restart re-anchor 请求形状、JoinPublished 的 2+1+1+1 PERFECT 轮次、Manager 拥有 blogger sidecar child 后 children 位置选取），以及 PTY 后端的 Fable Emit/柯里化缺陷（runLoader 从未被调用、bun-pty spawn 被柯里化）与 bun-pty 依赖缺失。启动风暴由 runner stagger 承担：每个 Bun SEA 宿主启动实测约 8 CPU-秒，14 个同时启动会拖垮 2s 因果 Watchdog；stagger 固定 2000ms 只错开启动脉冲，全部 14 个场景期仍全并行，Watchdog 与 expectation 均未放宽。

同进程 active child 的 busy nudge 已走 `SendChildPromptFireAndForget`，不新建 Run/listener/completion；`HostForkRunLifecycle.complete` 对本地/global 双事件流做一次性 claim。跨插件重启的 active-run 恢复仍未证明。

Orchestrator 的 `fork(agent=manager)`/`join` 已路由 `ForkManagerJob`/`JoinPublished`，真实 Git publish 单场景覆盖 worktree → candidate → 双 PERFECT → rebase → ff-only → cleanup；authority 与懒加载 reconcile hook 已接线，但不构成完整 restart resume。

- `StrictMockProvider` 的 expectation lane 已包含 scenario、session alias、role、turn、request-kind；真实 OpenCode 请求以 `x-session-affinity` 绑定 session，child 首请求还校验 `x-parent-session-id`。已绑定 session 优先于未绑定 sibling lane；错 parent、错顺序、extra、missing、ambiguous 全部返回 500。
- 每个实际产生的 title、零宽 continuation、Blogger、Executor、Reviewer 请求都是显式 expectation；`allowOutOfOrder`、`allowBloggerRequests`、`allowTitleGeneration`、`allowSyntheticContinuations` 已删除。没有自动吞请求路径。
- `afterExpectation()` 在已消费 expectation 的同一因果边界注册后继 lane；它避免多 child/session 交错时的注册竞态，不引入 retry 或生产状态。
- Watchdog 固定 2s。只接受 blocking expectation 消费、显式 session/restart/barrier；background Blogger 默认只留诊断。未知 `session.created` 只进入 `sessionCreatedDiagnostics`，不重置 Watchdog。replacement busy-skip 仅在测试显式等待该事实时作为 blocking barrier。
- `testkit/opencode/tests/gate-testkit.mjs` 的 gate 覆盖 lane 交错/FIFO、extra/missing、真实 session-parent 绑定、原子后继、title 分类、background-only watchdog、未知 `session.created` 噪音与 2s 常量集中化。`npm run test:e2e:p0` 默认即全套并行隔离门（含 publish canary）；不得再用 2 路线程池冒充全套并存。
- Journal 运行时路径已在 Git common directory 的 `wanxiangshu-next/runtimes`；测试依赖不再创建 workspace `node_modules`，Reviewer canary 断言工作树没有 `.wanxiangshu-next`。
- Manager→Coder→Join 现以 child-created、Coder write completed、Coder terminal、Manager join completed、Manager terminal 五个真实 Host barrier 收口；不再以全局 idle 推断因果。
- `HostEventRouter` 以 `messageID`/`messageId` 聚合 `message.part.updated`，再在 terminal 判定空助手轮：有 text/tool part 的完成轮不会误发零宽续命；真正空轮仍会续命。Manager terminal 的当前 Git tree 未获双 PERFECT 时，用 listener-before-send 发 guard 并 append `GuardPromptAccepted`；Reviewer 无 verdict terminal nudge 同一会话；abort terminal 不发 guard/continuation。`session.status=retry` 的每个 attempt 一次性 append `FallbackFailureRecorded`，500→即时 retry→重启累计由 fallback canary 覆盖。
- Companion 成功回合以单条 `CompanionAdvanced(SessionId, Projection, Content)` 原子 append；`Content` 是累积后的完整 B，不再分两条事实留下 baseline/B 撕裂窗口。`prefixLength` 现在比较 canonical message content，不把同 ID 的 tool/text 更新误判为已覆盖；`jsonDelta` 输出确定性的 add/remove/replace 操作，覆盖嵌套删除和数组缩短；Blogger model 从 `WANXIANGSHU_BLOGGER_MODEL` 明确解析，缺失/非法配置 fail-closed，TestKit 显式注入 `test/test-model`。
- ReviewGuard 的 Manager/Reviewer nudge 共用 durable `GuardPromptAccepted`，GuardKey 绑定 target、trigger、reason；重启可去重，发送失败不写事实。Journal/GitTreePort 缺失或读取异常不再返回允许完成的 `None`，而是阻止 Manager finish。
- 完成状态只允许由当前直接测试证据提升；历史“全量通过”不得复读。Watchdog 冻结为 2s scenario-local 静默探针。

## 本轮直接证据（2026-07-27 工作树状态）

- `npm run build`：通过。
- `npm run test:compile && npm run test:next`：215/217。失败 2 项：`Next_source_files_do_not_exceed_300_lines`（`next/Session/HostForkRuntime.fs` 314 行、`next/Orchestrator.PublishChain.fs` 303 行、`next/OpenCode/HostEventRouter.fs` 323 行）；`HostForkRuntimeSessionDeadTests > Dead_decision_survives_journal_rebuild`。本轮只记录，未处理。
- 本轮未重跑任何 canary 与 P0；下述 canary 状态为此前证据。
- 工作树含未提交的 best-effort 修复与调试清理：`HostEventRetry.record` 的 `isHostShutdown` 守卫、`HostEventRouter.fs` 的 `hostShutdownSessions` 集合、`AgentJournal` 按 session 有界 `RecentFailureIds`；`observeIdle`/`recordFallbackFailure`/`HostEventRetry` 三处调试 printfn 已移除。
- 工作树含未评审的 runtime-aware child linkage 改动（`LinkedRuntimeIds`/`childRuntimes`/`onChildCreatedDir`，跨 `HostForkRuntime.fs`、`AgentFacts*.fs`、`OrchestratorHost.fs`、`ToolSurface.fs`）；来源是被终止子代理的未完成工作，已最小修复至可编译，语义未验证。疑点见 `困惑.md` 谜团三。

## 当前未闭合边界

0. **Restart 红门（最高优先）**：`host-restart-canary.mjs` 与 `orchestrator-restart-publish-canary.mjs` 红。重启风暴期间出现幽灵 `FallbackFailureRecorded`（`empty-xml`），累计 4 条使 Reviewer/Coder session 误判 Dead，expectation 永不消费。三条证据互相矛盾（仅 `observeIdle` 可能写该签名、失败 session 无 `session.idle` 事件、被记录消息实际完好非空）；调试 printfn 写入 Host stdout 不进 canary 日志，故“踪迹未出现”不证“路径未走”。完整证据与假设见 `困惑.md` 谜团一。
1. ~~TestKit 并行门~~：已闭合（见上文直接证据；stagger 只错开启动脉冲，不降低并发）。
2. Fork/Join：busy-overlap 单场景已通过；`pendingRuns` 仍是进程内 active 来源，restart 只恢复 child linkage，不恢复在途 Run/completion。Orchestrator ManagerJob 已从 durable `OrchestratorManagerJobCreated` 恢复：有 candidate checkpoint 时直接补 completion 走 candidate/rebase，否则以原始 durable prompt 重跑；engine 懒加载时触发，不持久化 Task/handle/phase，不做开机扫描。新增未评审的 runtime-aware linkage（`LinkedRuntimeIds`）改变了 restart 复用语义，且疑似破坏 `Dead_decision_survives_journal_rebuild`；复用/拒绝边界需按 SSOT restart resume 语义重新裁决。
3. Process：200KB 流式 map、fan-in=8 分层 reduce 和 spool `finally` 清理已有生产代码；当前 Executor canary 只覆盖单 chunk、零 reduce。`ExecutorSummarize.awaitAgent` 消费非目标 completion 后只放本地 stash，不能回填公共 mailbox，目标路由与并发 completion 所有权未闭合。
4. PTY：真实 bun-pty 与 `fork(agent=pty)`/signal/list/join 表面已接线；单场景现在断言真实 handle、输出 read、TERM/KILL signal outcome 与 list 收口；`PtyBackend` 的 live `Read` 消费 buffer 并完成读取。模块级 `live`/`pending` 仍没有 per-runtime owner 清理，跨进程隔离与更宽 write/read/join/list/abort 覆盖未闭合。**不得宣称 PTY 产品能力完成**。
5. Companion：完整 messages 的 canonical baseline 已接线；当前 Y 阈值分支只写入 `[companion-rebase] condensed...` 占位内容，且同步 `TryRebase` 不持久化。真实、durable 的自压缩和对应 budget/restart gate 未闭合。
6. Fallback：retry/空轮 identity 已进入 durable fact，A/A/B/B 跨重启单场景通过；`IsDead` 目前只解析为 `Model=None`，发送路径仍继续调用 Host prompt，SessionDead fail-closed 尚未实现。
7. Orchestrator：真实 publish 单场景通过；reconcile 只在 engine 懒加载时、且 target HEAD 匹配 durable candidate 时补 `Published`，不恢复中间阶段。ff/Published crash window、target-ref 级共享串行化、cleanup 错误处理与真实 restart E2E 仍未闭合。

## 解冻后的严格顺序

### 阶段一：TestKit 因果模型

先修测试基座；若真实 Host event payload 暴露错误归因，修正 adapter 的事实提取，不得用 permissive expectation 掩盖。

- 每个 scenario 独占 workspace、HOME、XDG、OpenCode 数据、Provider、端口、Journal、spool、PTY namespace、diagnostics 和 expectation store。
- 只读 build、fixture、源码可以共享；运行中不得改共享 build。
- expectation key 至少包含 scenario、session、role、turn、request-kind；实现必须能拒绝错 lane、错顺序、额外请求和缺失请求。
- Blogger 是显式 lane，不是背景噪音；Manager Blogger 与 Coder Blogger 分离；禁止 Blogger-of-Blogger。
- 继续使用并行 P0，最多 3 次；标准门禁必须让全部 P0 canary 进入运行态并隔离。不可用串行或 2 路线程池掩盖隔离错误。
- Watchdog 保持 2s，但只接受当前场景的因果进展。诊断必须列出最后进展、阻塞 lane 和剩余 expectation。
- Reaper 只能作为人工诊断命令，不能挂在 npm lifecycle；TestKit 清理自己创建的进程树，不扫描全机、不猜归属。

完成标准：`gate-testkit` 已证明 lane 间交错、lane 内保序、extra/missing/unmatched request、真实 parent/session 绑定和原子后继；并行 P0 是夹具迁移的补充，不替代生产语义证据。

### 阶段二：最小真实纵切

只恢复一个 Manager→Coder→Join 场景，最多固定 3 次，使用确定性 barrier 覆盖：Coder 先完成、Manager 先 join、Blogger 插入关键路径。每一步等待语义事件；失败消息必须指向具体 lane 和动作。

本阶段不得同时修 Review、Fallback、Process、PTY、Orchestrator。生产修改必须先回答：

1. 对应 SSOT 哪条不变量或 Behavior ID？
2. 哪个失败 expectation 证明外部行为错误？
3. 错误在 Harness 还是 Production？
4. 是否引入 retry、timeout、queue、cap、global registry 或测试标记？若是，必须停止并重新设计。
5. 脱离测试夹具后该修改是否仍然是正确产品语义？

### 阶段三：Companion

显式 Manager/Coder Blogger lane，验证角色白名单、B 累积、busy skip、无递归 sidecar、JSON baseline、replacement、重启恢复。B 只能承载认知上下文，不能成为调度事实源。

### 阶段四：其他边界

固定顺序：Companion → Reviewer → Fallback → Process → PTY → Orchestrator。前序边界有部分证据；未闭合项见上文。Orchestrator 必须 Git 权威 + 重启 reconcile + 真实 publish E2E。

## 不可违反的生产纪律

- Fable 是唯一目标平台；`next/` 不得出现 `#if`、`#else`、`#endif` 或非 Fable 分支。
- 产品异步内核使用 Promise/Task 既有边界；不得新增 Flow AST、解释器、Workflow Engine、Stage/Phase 注册表或 Journal 驱动的程序计数器。
- 事件日志只保存跨重启领域事实；Task、Channel、listener、Process/PTY handle、semaphore、owner、lease、phase、generation、调用栈不得持久化。
- Transport Port 一次发送；不得在 Port 层管理宿主 prompt 队列、重试、fallback 计数或伪造 terminal。A/B Fallback 只能在唯一模型调用边界归因。
- 进程资源由创建作用域拥有；parent cancellation 只清理自己的后代；插件卸载不得全局 kill；禁止 SIGKILL OpenCode，只允许按约定处理 `opencode serve` 子树。
- Git tree 必须 fail closed。任何未跟踪文件读取/哈希失败都不能得到 PERFECT。
- OpenCode hook 若要求原地修改字段，必须原地修改；禁止以新数组/新引用替换宿主随后读取的对象。
- 角色工具面静态装配；Manager、Orchestrator、Coder、Inspector、Reviewer 等权限以 SSOT 为准，不能靠 prompt 劝阻模型。
- 单函数超过约 50–60 行必须重构；源文件超过 200 行应警惕，超过 300 行必须拆分。禁止删空行、压缩语句逃避门禁。
- 调试结论必须落成正式 deterministic gate/contract/test；临时脚本、一次性探针、注释掉的断言和只跑不提交的探针不算验收。
- 不改 `../oh-my-pi`、`../opencode` 上游；Mux 只允许最小 binding 修复，首选本仓库实现。

## 测试与稳定性纪律

标准入口：

```text
npm run build
npm run test:compile
npm run test:next
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
npm run test:e2e:p0
```

当前纠偏后先跑最小目标测试；全量测试只能在对应阶段完成后运行。稳定性上限固定 3 次，且只允许 runner 外层控制重复；canary 自身只能执行一次。`npm run test:e2e:p0` 即全套并行隔离门。禁止 fixed sleep；使用 SSE、Provider、HTTP 事件和明确 barrier，但 Watchdog 只记录因果进展。

- 每个 E2E 场景必须具备：独立环境、显式 expectation、场景级 diagnostics、2s causal-progress watchdog、拥有者清理、无 PID/端口/session/worktree 泄漏检查。测试失败首先检查 expectation 因果模型、Mock 假设、等待事件和资源归属，不得先改生产代码迎合夹具。

## Git 与融合纪律

- 开发解冻后的每个逻辑行为单独提交并推送；提交信息说明行为，不把文档、TestKit、多个角色和新功能混成一坨。
- 历史代码只能作行为证据。融合必须逐文件、逐符号、逐段落手工取舍；禁止 `git checkout` 历史版本、`git checkout --ours`、`git checkout --theirs`、整文件覆盖和无审查 cherry-pick。
- 提交前检查工作树、diff、架构门禁和目标测试；未直接验证不得提高完成状态。
- `correction-freeze-e499e41d` 是纠偏前证据，不得删除或移动；若需新的历史锚点，先记录 SHA 和取舍理由。

## 行为索引

产品 Behavior ID 只在 `next/Doc/SSOT.md` 维护。本文件只引用当前纠偏顺序：

- `AG-FORK-*`：listener-before-send、fast completion、busy nudge、join any、completion once、parent cancel。
- `BLOG-*`：canonical JSON、JSON delta、busy skip、B-only output、replacement、restart。
- `PROC-*`：pump、唯一 3× deadline、Large gate、SIGKILL tree、完整 spool、200KB map/reduce、no orphan。
- `REV-*`：REVISE immediate、same-tree double PERFECT、tree change invalidation、missing-verdict nudge。
- `FB-*`：A/A/B/B、per-session cumulative failure、fourth dead、success keeps count。
- `ORCH-*`：dirty reject、same-manager conflict、post-rebase double PERFECT、ff-only、cleanup。

## 当前解冻动作

0. 未完成事项（2026-07-27 记录，未处理）：
   - 解开 Restart 红门幽灵 fallback（`困惑.md` 谜团一）：下一步建议将诊断改为插件写文件（非 printfn），定位 `observeIdle` 在关闭竞态下的 parts 缺失假设。
   - 处理 2 项单元测试失败：300 行门禁三个文件需拆分；`Dead_decision_survives_journal_rebuild` 需定位与 runtime-aware linkage 的交互。
   - 评审/验证 runtime-aware child linkage（谜团三），必要时按 SSOT 重裁或回退。
   - 原 release 四提交计划（Companion/Fallback、PTY、TestKit、Orchestrator）未执行；稳定性三重复扫（`CANARY_REPEAT=3`）未跑；未推送。

1. 保持 Watchdog 2s、14-way 标准 P0 和显式 expectation 不变，闭合当前全并行红门；先证明因果停滞归属，不以单场景绿色替代隔离契约。
2. 保留已通过的 busy-overlap 场景，补 restart active-run/completion 语义；不得恢复危险的双 completion 路径。
3. 按既定顺序先完成 Companion：用真实且 durable 的 Y 自压缩替换占位 rebase，并补 budget/restart deterministic gate；随后回归 Reviewer。
4. Fallback 在第四次失败后必须阻止新 Host prompt；保留 durable attempt identity 与跨重启 A/A/B/B 证据。
5. Process 修复非目标 completion 的 mailbox 所有权，并以多 chunk、跨 reduce level、summarizer 失败清理场景验收。
6. PTY 补真实输出 read/terminal 交付、owner cleanup 与角色权限边界，再扩展 canary 覆盖 write/read/join/list/abort。
7. Orchestrator 补 target-ref 级串行化、ff/Published crash-safe reconcile、阶段恢复和 cleanup 失败处理，以真实 restart publish E2E 收口。
8. `MIGRATION.md` 继续记录行为接管和删除门槛；不把旧实现重新引入。完成状态禁止写进本文件当宣言。

任何“为了让测试不挂”“先加几十次重试”“测试环境才设置变量”“以后换真正 PTY”“读不到就忽略”“方便清理所以全局 kill”的修改均拒绝。测试必须证明 SSOT；生产不得追着测试夹具跑。

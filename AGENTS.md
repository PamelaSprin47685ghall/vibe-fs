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

## 本轮 TestKit 因果证据

本轮只推进四项，不向 Fallback、Process、PTY 或 Orchestrator 提前扩张：

1. busy existing agent 的 nudge 不再经过带 `runWork` 的新 `Fork`；同一 active source 只允许一个 completion。
2. SSOT 将 idle existing 与 busy existing 明确分开：前者建 Run，后者只 nudge。
3. `session.created` 不再是全局 Watchdog 心跳；只有显式等待后的因果 barrier 才能推进 2s Watchdog。
4. 重复控制只保留 runner 外层；每个 canary 子进程强制 `CANARY_REPEAT=1`。除默认开发门外，另有 `MAX_PARALLEL_CANARIES=13` 的全 P0 隔离门。

- `StrictMockProvider` 的 expectation lane 已包含 scenario、session alias、role、turn、request-kind；真实 OpenCode 请求以 `x-session-affinity` 绑定 session，child 首请求还校验 `x-parent-session-id`。已绑定 session 优先于未绑定 sibling lane；错 parent、错顺序、extra、missing、ambiguous 全部返回 500。
- 每个实际产生的 title、零宽 continuation、Blogger、Executor、Reviewer 请求都是显式 expectation；`allowOutOfOrder`、`allowBloggerRequests`、`allowTitleGeneration`、`allowSyntheticContinuations` 已删除。没有自动吞请求路径。
- `afterExpectation()` 在已消费 expectation 的同一因果边界注册后继 lane；它避免多 child/session 交错时的注册竞态，不引入 retry 或生产状态。
- Watchdog 固定 2s。只接受 blocking expectation 消费、显式 session/restart/barrier；background Blogger 默认只留诊断。未知 `session.created` 只进入 `sessionCreatedDiagnostics`，不重置 Watchdog。replacement busy-skip 仅在测试显式等待该事实时作为 blocking barrier。
- `testkit/opencode/tests/gate-testkit.mjs` 的 23 项 gate 已覆盖 lane 交错/FIFO、extra/missing、真实 session-parent 绑定、原子后继、title 分类、background-only watchdog、未知 `session.created` 噪音与 2s 常量集中化。`npm run test:e2e:p0` 是默认有界并发门；`npm run test:e2e:p0:parallel` 才证明 13 个 canary 同时存在时的环境隔离。
- Journal 运行时路径已在 Git common directory 的 `wanxiangshu-next/runtimes`；测试依赖不再创建 workspace `node_modules`，Reviewer canary 断言工作树没有 `.wanxiangshu-next`。
- Manager→Coder→Join 现以 child-created、Coder write completed、Coder terminal、Manager join completed、Manager terminal 五个真实 Host barrier 收口；不再以全局 idle 推断因果。
- `HostEventRouter` 以 `messageID`/`messageId` 聚合 `message.part.updated`，再在 terminal 判定空助手轮：有 text/tool part 的完成轮不会误发零宽续命；真正空轮仍会续命。Manager terminal 的当前 Git tree 未获双 PERFECT 时，用 listener-before-send 发 guard 并 append `GuardPromptAccepted`；Reviewer 无 verdict terminal nudge 同一会话；abort terminal 不发 guard/continuation。`session.status=retry` 的每个 attempt 一次性 append `FallbackFailureRecorded`，500→即时 retry→重启累计由 fallback canary 覆盖。
- Companion 成功回合以单条 `CompanionAdvanced(SessionId, Projection, Content)` 原子 append；`Content` 是累积后的完整 B，不再分两条事实留下 baseline/B 撕裂窗口。`prefixLength` 现在比较 canonical message content，不把同 ID 的 tool/text 更新误判为已覆盖；`jsonDelta` 输出确定性的 add/remove/replace 操作，覆盖嵌套删除和数组缩短；Blogger model 从 `WANXIANGSHU_BLOGGER_MODEL` 明确解析，缺失/非法配置 fail-closed，TestKit 显式注入 `test/test-model`。
- ReviewGuard 的 Manager/Reviewer nudge 共用 durable `GuardPromptAccepted`，GuardKey 绑定 target、trigger、reason；重启可去重，发送失败不写事实。Journal/GitTreePort 缺失或读取异常不再返回允许完成的 `None`，而是阻止 Manager finish。
- 本轮已直接验证：`npm run test:next`（156/156 Fable）、`node testkit/opencode/tests/gate-testkit.mjs`（23/23）、Companion projection/replacement canary、Reviewer verdict canary、Fallback A/A/B/B + restart canary、Process bounded spool/cancellation tests、PTY DSL unified surface tests、默认 P0、13-way P0、3× P0 与全量 `npm test` 均通过。

## 当前未闭合边界

1. 四项因果纠偏与 Companion/Review 本轮边界已有直接测试证据；仍不得把 canary 名称外推为完整产品闭合。
2. Fallback 已闭合：HostForkRuntime 注入 durable ModelResolver；每个 retry attempt 只记一条事实；同一 child 跨重启实测 A/A/B/B。
3. Process 已闭合有界 spool/map-reduce、唯一 deadline、取消杀进程树与 pipe EOF；真实 PTY `fork` 表面、Orchestrator worktree/rebase/ff-only 发布仍未闭合。
4. 不得宣称 release-ready；下一阶段只能按 PTY → Orchestrator 顺序推进。

## 解冻后的严格顺序

### 阶段一：TestKit 因果模型

先修测试基座；若真实 Host event payload 暴露错误归因，修正 adapter 的事实提取，不得用 permissive expectation 掩盖。

- 每个 scenario 独占 workspace、HOME、XDG、OpenCode 数据、Provider、端口、Journal、spool、PTY namespace、diagnostics 和 expectation store。
- 只读 build、fixture、源码可以共享；运行中不得改共享 build。
- expectation key 至少包含 scenario、session、role、turn、request-kind；实现必须能拒绝错 lane、错顺序、额外请求和缺失请求。
- Blogger 是显式 lane，不是背景噪音；Manager Blogger 与 Coder Blogger 分离；禁止 Blogger-of-Blogger。
- 继续使用并行 P0，最多 3 次；默认开发门可有界并发，但必须另跑 `npm run test:e2e:p0:parallel` 验证全套件同时存在。不可用串行掩盖隔离错误。
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

固定顺序：Companion → Reviewer → Fallback → Process → PTY → Orchestrator。Companion/Reviewer 本轮已有可复现直接证据；下一阶段只进入 Fallback，不能跨阶段修改 Process/PTY/Orchestrator。

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

当前纠偏后先跑最小目标测试；全量测试只能在对应阶段完成后运行。稳定性上限固定 3 次，且只允许 runner 外层控制重复；canary 自身只能执行一次。`npm run test:e2e:p0` 使用默认开发并发，`npm run test:e2e:p0:parallel` 使用 13-way 全并发隔离门。禁止 fixed sleep；使用 SSE、Provider、HTTP 事件和明确 barrier，但 Watchdog 只记录因果进展。

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

1. busy existing nudge、Watchdog、runner 重复层和 13-way 隔离门已完成并推送。
2. Companion canonical prefix、JSON remove delta、cheap Blogger model 与 Reviewer durable guard 已完成本轮定向验证。
3. Fallback attempt identity/A-A-B-B 与重启恢复已完成；Process 有界 spool/取消链已完成，下一步进入 PTY，Orchestrator 继续冻结。
4. `MIGRATION.md` 继续记录行为接管和删除门槛；不把旧实现重新引入。

任何“为了让测试不挂”“先加几十次重试”“测试环境才设置变量”“以后换真正 PTY”“读不到就忽略”“方便清理所以全局 kill”的修改均拒绝。测试必须证明 SSOT；生产不得追着测试夹具跑。

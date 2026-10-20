# CUT-19：多 writer 存储与收敛（子范围①结构 k-way 归并、②writer 保留规则）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-19) / [范围对账](../COVERAGE.md)。

## 1. 状态与范围

- **owner**：eng-cut19c-goose（2026-10-20 接手 eng-cut19b-goose 中断后的工作树 `cut/CUT-19b`：库提交 3585c4f/66fd900 已在 wanxiangli main，主仓提交与本卡记录由 19c rebase、复验后落地；此前 eng-cut19b-goose 接手；原 owner eng-cut19-goose 已退出：其工作树 `cut/CUT-19` 干净、与 master 同步、无运行进程，交接提交 1c90ecf69）。原认领 993e93683。reviewer：未配非作者 reviewer（本轮全自治，见 §8）。
- **本轮闭合的职责**：多 writer 事实流的**结构归并**（durable-convergence-001/002/003 与 011 的 retained 父边界）。唯一实现从主仓 `EventKWayMerge.fs` 迁到 `wanxiangli/convergence`（`KWayMerge`）。
- **本卡整体未完成**：段文件原子追加与滚动（`ProcessEventLog`）、retention 截止与整 writer 过期、跨进程锁、物化缓存、`IntegratorEngine` 业务折叠与 cut-tail、`validateLocalHistory` 与 `wanxiangli/events.validate` 的同义校验收敛，都仍在主仓，归本卡后续子范围。故看板列为 `DOING`，不是 `DONE`。

## 2. A. 范围

| 项 | 内容 |
|---|---|
| 一句职责 | 把若干条完整 writer 逻辑流归并为一条确定的因果序列：writer 内顺序不变，parent 先于 child，就绪 head 之间按 EventId 文本序；同 id 同字节去重，异字节为身份碰撞。 |
| 当前调用者 | `IntegratorEngine.replay`（`ReloadLocal` 与 cut-tail 一次性全量重放的真实链：`ProcessEventLog.readStreams → validateLocalHistory → EventKWayMerge.mergeRetained → integrateOne`）；`EventMergeSurface.merge/mergeWithDiagnostics`（JS 证明面，durable-convergence-001/002/006 与 durable-events-014 使用）。 |
| 状态归属 | 纯计算，单次调用内的游标/堆/等待表是草稿，不跨调用保留；无持久状态。 |
| 候选删除 | `EventKWayMerge.fs` 中全部排序算法（游标、就绪堆、父等待/重复等待、前沿错误判定，约 600 行）。 |

## 3. B. 契约（`wanxiangli/convergence`，0.14.0）

- 输入：`[writerId, EventInput[]][]`。每个事件只读 `id`、`parents`（字符串）；其余字段只在同 id 重复时交给 `wanxiangli/events` 的 `checkIdentity`。
- 成功：`{ ok: true, events, readyComparisons }`，`events` 是**调用方传入的对象本身**（按引用）。主仓 facade 用身份 Map 还原为原 `EventEnvelope`，不重新解码。
- 失败（零副作用）：`IdentityCollision`（来自 events）、`MissingParent`（严格模式缺父）、`NonCanonical`（成环/逆序前沿；同名 writer 流）、`MalformedEnvelope`（输入形状错误）。
- `merge`：集合内外缺父均为损坏。`mergeRetained`：整个保留集合中不存在的 parent 视为窗口外边界；集合内依赖照常排序与成环检测。
- `readyComparisons`：就绪堆比较次数，仅成本观测。
- **当前行为差异裁决**：旧实现以 `Map.ofList` 折叠同名 writer 流（后者整条覆盖前者，结果仍 Ok）。新实现拒绝为 `NonCanonical "duplicate writer stream: <id>"`。生产链 `ProcessEventLog.writerFileGroups` 已按 WriterId `groupBy`，从不产生同名流，故生产行为不变；只有直接构造流的调用方（JS 证明面）会看到差异。这是把静默丢事实改为 fail-closed，符合 durable-convergence-001/003，不是范围缩减。

## 4. C. 反例（真实失败，非自抛）

`requirements/durable-convergence/tests/001.test.mjs` 新增「a WriterId naming two streams fails closed instead of dropping one branch」：

- 在旧 master 代码（同一工作树还原三份 F# 文件后重建）上：**失败**，`merge` 返回 `ok: true`，事件只有 `[root, c…]`——分支 `b…` 被丢弃。日志：`/tmp/cut19-main-tests-base.log`（243 pass / 1 fail）。
- 在候选上：通过。

库侧 mutation 自检：去掉同名拒绝 → `convergence/tests/003` 红；就绪序反转 → 新增的「concurrently ready heads are ordered by EventId text」红。

## 5. D/E. 独立实现与实际消费

- 库提交：`1e398a7 feat(convergence): …`，发布 `674bdfb chore(release): release 0.14.0 (convergence, CUT-19)`（wanxiangli main）。
- 文件：`src/convergence/{KWayMerge.fs, Wanxiangli.Convergence.fsproj, index.d.ts, AGENTS.md, tests/{001,002,003,011}.test.mjs}`，`scripts/tests/convergence-{package.test.mjs,consumer.mjs,consumer.ts}`。独立 fsproj，零 ProjectReference；运行时只 import `wanxiangli/events`（esbuild external）。
- 库测试：`convergence` 15/15；含 events、exports、repository、build、convergence-package 的定向集 54/54（rebase 到 6bf65d3 之后、发布前，Node 22.21.1）。`convergence-package` 实际 `npm pack` → 干净目录安装 → `tsc --strict` 类型消费 → Node 与 Bun 各跑一次真实分叉归并。
- 全量 `npm test`（rebase 前，Node 22.21.1）：475 项中 471 pass；4 项未通过均与本卡无关：`eventstore-001` 双进程争抢在机器满载下失败一次、单独重跑 5/5 通过；`ownership-package`/`package`/`trace-package` 三个安装包测试在 pass 之后进程不退出，被我手动终止（`trace-package` 单独重跑同样 ok 后挂起至超时）。如实记为未完成运行，不计为通过。
- 主仓消费方式：子模块 gitlink（发行物不再是 vendor tarball，见 CUT-33）。落地提交 b7d45b063 把 gitlink 从 master 当时的 `6bf65d3`（0.13.0）推进到 `674bdfb`，`package-lock.json` 中 wanxiangli 版本 `0.13.0 → 0.14.0`（中途 master 从 0.9.0 前进，故 rebase 后的实际起点是 0.13.0），主仓在此 gitlink 下完整构建通过。

## 6. F. 接回真实链

- `src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs`：628 行算法 → 约 90 行 facade（`[<Import("KWayMerge", "wanxiangli/convergence")>]`），`.fsi` 不变，两个调用者零改动。
- `CanonicalEventCodec.fs(.fsi)`：把已有的 envelope→JS 转换与 CodecError→StorageInvalid 映射公开为 `toLibraryEvent` / `invalidOfLibraryError`，让两个库 facade 共用，不再复制转换。
- 真实链 `IntegratorEngine.replay → EventKWayMerge.mergeRetained` 现在走库实现；durable-convergence 004/005/007（真实 EventStore 写入、重开后的 Current 与 structural heads）都经过这条链。

## 7. G. 旧 owner 退出

| 旧符号 | 调用者去向 | 处置 |
|---|---|---|
| `EventKWayMerge` 内部游标/堆/等待/前沿判定 | 无（私有） | 删除 |
| `EventKWayMerge.merge/mergeRetained/mergeWithDiagnostics` | IntegratorEngine、EventMergeSurface | 保留为 facade，只做类型投影 |
| `MergeObservation` | EventMergeSurface | 保留（类型面） |

持久数据：纯计算，不读不写任何文件，不改 NDJSON 字节或段格式；真实 `.git/wanxiangshu` / `.Herdchestra` 未触碰，测试只用夹具临时目录。没有第二条写入链。

## 8. H. 验证与比较（同一工作树、同一机器、同一 lib gitlink 3e10bc3）

| 项 | base（master 三文件还原） | candidate |
|---|---|---|
| `node scripts/build.mjs` | ok | ok（rebase 到 e2d97b922 + gitlink 674bdfb 后再次 ok，725 模块链接） |
| durable-convergence + durable-events（run.mjs，34 文件） | 243 pass / **1 fail**（新反例）/ 14 skip / 10 todo | **244 pass / 0 fail** / 14 skip / 10 todo |
| bench：32 writer × 1000 事件，`mergeWithDiagnostics` 经 JS 面 | readyComparisons 301943；中位 220 ms，最小 161 ms | readyComparisons **301943**（一致）；中位 233 ms，最小 208 ms |
| `node scripts/check.mjs` | FAIL（既有，154 条列出项） | FAIL（既有，154 条；shard-edge 低级发现 256 → 255，少的是旧 EventKWayMerge 的 `Finished` 类型名边） |
| fantomas --check（本卡三文件） | — | 格式化后通过 |

- 接手复验（eng-cut19c-goose）：rebase 到 master b40dd6efa（期间新增 CUT-25 文档与 CUT-08 tool-name map 接线，后者已把 gitlink 推到 b6b05e2 / 0.20.0，含本卡 66fd900 的 0.19.0 内容）；gitlink 与 lock 取 master 的 0.20.0，重建库 dist 后同工作树 `node scripts/build.mjs` ok，durable-events + durable-convergence 35 文件 250 pass / 0 fail / 14 skip / 10 todo；日志 `/workspace/vfs-cut/wt/logs-cut19c/rebased-run2.log`。
- 14 skip 是未开 integration tier 的编译闭包测试（durable-events 022/023 等）；10 todo 是既有登记。run.mjs 因此以「pending proof」退出 1，两侧相同，不是通过。
- integration tier（`WXS_TIER_INTEGRATION=1` 跑 022/023/014）**未完成**：运行被会话中断，没有结果，不计入。
- 成本：每次归并多一次 envelope→JS 对象转换，bench 中位慢约 6%，最小值慢约 30%；机器同时有其他 worker 在跑构建，噪声大。比较次数相同说明算法等价。只在本地变化时才会触发归并（durable-convergence-010），量级可接受，但这是实测回退，如实登记。
- 回退：纯计算，没有新数据；`git revert` 主仓提交即可回到旧算法（库 0.14.0 可保留）。
- 未做：全量 `scripts/verify.mjs` 与 integration run.mjs（同机重型套件，留给集成者）。

## 9. 子范围②：整条 writer 的 UTC 日保留规则

- **职责**：`Retain(now, W)` 的日期判定（durable-convergence-011）：最近活动的 UTC 日 ≥ 今天的前一天即保留，否则整条过期。
- **库**：`wanxiangli/retention` / `WriterRetention`（`utcDay`、`isRetained`、`retain`、`dayMilliseconds`），提交 `03f3c8b`，发布 `c9a943d`（0.16.0）。纯计算，不读时钟、不读文件、不删除；非有限时间抛错。库测试 `retention/tests/011` 5/5（日边界、日历日而非滚动 24h、活动推进、固定种子 300 组并集分配与按引用返回、非有限拒绝）；`convergence-package` 的干净安装消费同时覆盖 retention（Node + Bun，tsc strict）。发布前定向集 59/59。
- **主仓**：`ProcessEventLog.{utcDayOf, writerRetentionMilliseconds, isWriterActiveAt}` 不再自己算日期，改为调用库；`.fsi` 不变。真实链：`ReloadLocal → readStreams → readStreamsAt → isWriterActiveAt`，以及 `RetentionSurface.retainedWriterIdsAt`。本仓生产代码中保留规则只用于读取过滤，没有删除路径；不改任何文件字节。活动时刻的取得（Journal 尾部 `ObservedAt`、`ProjectionCutTail` 回溯、文件时间回退）涉及文件 I/O，仍留在 `ProcessEventLog`。
- **反例**：新增 `requirements/durable-convergence/tests/011.test.mjs`。在 base 上「non-finite cut-off is rejected instead of retaining nothing」**失败**：旧规则下 NaN 截止让所有比较为假，读取链返回空保留集，整个本地历史从 Current 静默消失。候选上抛错。生产 `readStreams` 的 now 来自 `currentTimeMs()`，正常是有限数，所以这里是 fail-closed 加固，不改变正常行为。
- **比较**（同一工作树，base = b7d45b063 的 dist，`--skip-staleness-check` 因源码已改）：durable-convergence + durable-events 35 文件 base 245 pass / 1 fail（新反例）/ 14 skip / 10 todo；候选 **246 / 0** / 14 / 10。`check.mjs` 两侧都是既有 FAIL，154 条列出项、门禁计数相同。
- **未做**：integration tier；全量 verify。

## 10. 子范围③：已落盘行的闭包校验收敛到 `wanxiangli/events`

- **职责**：本地已落盘行（追加批与保留窗口重放）必须满足的闭包——先对整个集合查词汇，再逐行严格载荷闭包：每个 PayloadRef（不论是否内容地址形态）都内联且 sha256 吻合，每个内联载荷都被某个 ref 指名。不查父闭包（保留窗口合法地指向已过期的父，归 convergence），不做身份合并。
- **旧状**：两份手写实现。追加侧 `Store.validatePayloadClosure`（缺失/失配/无主都报 `MissingPayload`）；重放侧 `IntegratorEngine.validateLocalHistory`（词汇 + 缺失/失配，**不查无主载荷**）。库里 `validate` 又是第三种（只约束内容地址 ref、还查父闭包，不适用于保留窗口）。
- **库**：`CanonicalEventCodec.checkClosure(events, isKnownEventType)`，新错误码 `PayloadDigestMismatch`、`UnreferencedPayload`（只由 checkClosure 产生），提交 `3585c4f`，随 `66fd900`（0.19.0）发布。库测试 `events/tests/012` 新增五例（自包含与无 ref 行通过；缺失/失配/无主分别命名；非内容地址 ref 不豁免；词汇先于载荷；不查父）。变异：去掉无主检查 → 012 红。
- **主仓**：`CanonicalEventCodec.checkStoredClosure` 薄投影为 `StoredLineFault`；`Store.validatePayloadClosure` 与 `IntegratorEngine.validateLocalHistory` 都改为调用它，各自只保留错误形状（追加侧仍一律 `MissingPayload`；重放侧保留 027 锁定的消息，新增 `unreferenced durable inline payload during replay: <ref>`）。`ProcessEventLog.payloadDigest` 只剩 `WritePayload` 计算地址一个用途。
- **反例（base 红）**：`requirements/durable-events/tests/012.test.mjs`「reload refuses inline bytes that no PayloadRef names instead of serving them by that address」。重放把每个内联载荷按键折进内容寻址的 PayloadIndex；旧重放不查无主载荷，于是一行在别人的内容地址下携带伪造字节就能装载，`readPayload(该地址)` 交出伪造字节。base 上 `create` 成功（Missing expected exception）；候选上 fail-closed，且不改写该行；去掉伪造字节后同目录正常装载、该地址读不到内容。追加侧早已拒绝这种行，所以合法写入的历史不受影响——这是把重放与追加对齐，不是范围缩减。

## 11. 子范围④：writer 活动时刻的尾行判定交给 `wanxiangli/retention`

- **职责**（durable-convergence-011）：哪一条尾行替 writer 说出活动时刻——逻辑流最末的 Journal 信封的有限 `ObservedAt`；尾部连续的 `ProjectionCutTail` 是前一条事实的整合元数据，往前走（可跨段）；其它任何尾行或行已读尽都是“没有持久活动”，退回最新文件时间。
- **库**：`WriterRetention.classifyTail(line)` 与 `lastActivity(linesNewestFirst: Iterable<string>)`（只拉取到第一个结论，不读盘），提交 `3585c4f` / 0.19.0。库测试 `retention/tests/011-tail` 6 例（含“只拉 3 行就停”的惰性证明、坏 JSON/非对象/缺字段/非字符串或不可解析时刻一律 silent、非可迭代与非字符串行拒绝）。变异：让 cutTail 也停 → 2 例红。`convergence-package` 干净安装消费同时覆盖 `lastActivity` 与 `checkClosure`（Node + Bun 1.4.2，tsc strict）。
- **主仓**：`ProcessEventLog.writerActivityOf` 只剩物理读取：按“最新段在前”惰性地逐行倒读（每次拉取 O(该行字节)），在旧扫描停下的地方停止供行（空文件、不完整尾行、空行或非 UTF-8 行、读失败），文件描述符在一次查找结束后统一关闭；判定交给库。删除 `TailActivity`/`TailSearch`/`FileTailOutcome`/`classify*`/`findDurableActivity`/`scanFileTail` 等约 130 行与 `Date.parse`/`isFinite` 两个 Emit。
- **行为差异**：旧实现对非字符串 `ObservedAt` 会 `Date.parse` 强转（如数字 `1760918400000` 被当作公元 1760918400000 年附近的字符串解析）；库只接受字符串，其余为 silent。写入方一律以 `ToString("O")` 写字符串，生产不产生此差异。
- **正控（base 与候选都通过）**：`requirements/durable-convergence/tests/011.test.mjs` 新增三例走真实读取链 `retainedWriterIdsAt`：Journal ObservedAt 胜过文件时间；cut-tail 链跨段回溯到上一段的 Journal；其它尾行让最新文件时间说话。没有找到旧实现上会红的反例，不伪造红。

## 12. 验证（同一工作树、同一机器，Node 22.21.1，dotnet 10.0.401）

| 项 | base（master 1c90ecf69，gitlink c9a943d / 0.16.0） | candidate（rebase 到 0c9ae2c3f 之后，gitlink 66fd900 / 0.19.0） |
|---|---|---|
| `node scripts/build.mjs` | ok | ok（725 模块链接） |
| durable-events + durable-convergence（run.mjs，35 文件；base 用 `--skip-staleness-check`，因新增测试是被跟踪文档） | 249 pass / **1 fail**（新反例）/ 14 skip / 10 todo | **250 pass / 0 fail** / 14 skip / 10 todo |
| 其余 94 个导入 EventStore/Journal 的测试文件（run.mjs，候选） | 未在 base 重跑 | 660 pass / 20 fail：work 009/011/019/020/024/026 九项与 CUT-07 的 base 日志 `logs-cut07/b4-work.log` 一致（master 既有）；delegation-029、host-boundary-026、repository-programming-041 是源码词法/编译清单断言；knowledge-reuse-010、managed-session-lifecycle-022 失败于 `view declaration is invalid: thenMap key 'question'`。都不经过本卡改动的函数，但这 5 个文件**没有在 base 上重跑**，如实记为“推断无关、未对照” |
| fantomas --check（EventStore 目录） | — | 通过 |
| 库定向集（events、retention、convergence、exports、build、repository、convergence-package） | — | 87/87（rebase 到 71958aa 后、发布前） |

- 14 skip 是未开 integration tier 的编译闭包测试；10 todo 是既有登记；run.mjs 因此以 pending proof 退出 1，两侧相同，不是通过。
- 日志：`/workspace/vfs-cut/wt/logs-cut19b/`（base-run、cand-run、cand-wide、rebased-run）。
- 回退：纯校验与纯判定，不写新数据、不改任何文件字节；`git revert` 主仓提交即回到旧实现（库 0.19.0 可保留）。被新规则拒绝装载的只有含无主内联载荷的行，而追加侧从来不写这种行。
- 未做：全量 `scripts/verify.mjs` 与 integration run.mjs（同机重型套件，留给集成者）。

## 13. 段存储（`ProcessEventLog` 段聚合、原子追加、滚动、共存收敛）的持久数据计划（README §10）

动手之前先写清；本轮**未改动**段存储的任何代码。

- **真实旧数据**：每个仓库的 `<common-dir>/wanxiangshu/events/` 下，legacy `<writer>.ndjson` 与段系列 `<writer>-NNNN.ndjson` 可能同时存在（durable-events-005 的中断整理/版本过渡中间态）；还有进程内解码前缀缓存。真实主仓 `.git/wanxiangshu` 是在用数据，**任何实验都不触碰**；只用夹具临时目录。
- **六类状态**：未开始（无文件）；已预留未承接（段文件已建、无完整行）；已绑定未确认（追加中、半行）；活动（当前段正在追加）；已终结（封存段）；损坏/未知（坏尾、非规范行、共存形态不成立）。核对者是读取链 `writerFileGroups → coexistingFormsOf → decode`，停止准入的是 `withStoreLock` 跨进程锁。
- **冻结点**：字节格式（canonical NDJSON + LF）、文件名规则（`-` + 4 位数字 + `.ndjson`）、段上限默认 64 MiB、共存判定（字节前缀 / 行续接）全部不变。抽取只允许把**纯命名与分组律**（文件名 → writer 分组、序号、legacy 与段系列的共存裁决输入）搬入库，物理读写、锁、fsync、rename 留在主仓。
- **验证方式**：只做影子纯计算——同一份目录列表与字节由新旧两份分组律各算一次，逐项相等（含边界名：writer 名本身以 `-dddd` 结尾、超 9999 段、legacy 与段同名前缀）；禁止开第二条真实写入链或双执行追加。
- **备份与回退**：不迁移、不转换、不覆盖任何原件，因此没有回退截止点；代码 revert 即回旧律。若将来需要新格式，另立子卡写冻结、备份、完整性检查、幂等转换与回退截止。
- **下一步候选**：上述纯命名/分组律（`isSegmentFileName`、`parseSegmentTail`、`writerFileGroups` 的分组与排序、`coexistingFormsOf` 的裁决部分）。

## 14. 本卡剩余（仍归本卡，看板保持 DOING）

1. ~~§13 的段命名与分组律~~：已完成，见 §15。
2. `IntegratorEngine` 业务折叠的故障隔离与 cut-tail 重放（`integrateOne`/`applyCut`/全量重放预算）。
3. Journal 侧多 writer 接线与 Git/Hook 同步、旧 Store/Journal 退出（CARDS 的“退出”条件：数据格式、恢复、同步、关闭各有 owner，最后一个旧消费者退出后才删旧实现）。
4. 跨进程锁与原子追加是真实宿主 I/O，预计明确保留在主仓（需在关闭本卡时写入保留理由）。

本卡的“退出”条件远未满足，故不标 DONE；原 owner 已退出不改变这一点。

## 15. 子范围⑤：段命名与分组律交给 `wanxiangli/segments`（按 §13 数据计划）

- **职责**（durable-events-005/014）：`<WriterId>-dddd.ndjson` 段名（恰好四位 ASCII 数字，序号 0..9999）、旧式 `<WriterId>.ndjson`、新 writerId 的三条约束、某 writer 的段系列、目录名单 → writer 分组（序号从 0000 起连续才并组，否则按完整 stem 降为旧式）、一组文件的形态（只有段 / 只有旧式 / 共存 / 多旧式）。只算名字；目录列举、字节读写、共存的字节裁决（字节前缀 / 行续接）、锁、fsync 都留在主仓。
- **库**：新模块 `wanxiangli/segments` / `WriterSegments`（`5295e3d`，随 `7a2ccf6` 发布为 0.23.0）。库测试 `segments/tests/005`、`014` 共 7 例，含按冻结读取律独立写成的参考实现在固定种子 400 组随机名单上的逐项对照及名单逆序不变。变异：把 ASCII 判定换回 `Char.IsDigit`、去掉序号上界 → 7 例中 6 例红。`convergence-package` 干净安装消费覆盖 `groupWriterFiles`/`streamLayout`（Node + Bun，tsc strict）。库发布前定向集 95/95。
- **主仓**：`ProcessEventLog` 删去 `isSegmentFileName`/`endsWithSegmentSuffix`/`stemOfFileName`/`parseSegmentTail`/`parseSegmentStem`/`contiguousSegmentGroups`/`legacyGroupsOf`/`writerFileNames` 与手写段名拼接，`segmentFileOf`/`legacyFileOf`/`ordinalOfSegmentFileName`/`safeWriterId`/`seriesFilesOf`/`writerFileGroups`/`logicalStreamFiles` 改为调用库（错误消息形状不变）。旧式行续接改为先拼出全部目标名、再写任何字节。
- **反例（base 红，候选绿）**，`requirements/durable-events/tests/005.test.mjs` 末段：
  1. 「a file whose -dddd tail uses non-ASCII digits is a legacy writer, not a crash of the whole store」：旧律用 `Char.IsDigit`（Unicode Nd）认段名再用 `Int32.Parse` 取序号，事件目录里一个 `imported-٠٠٠٠.ndjson` 让 `create` 抛 `The input string ٠٠٠٠ was not in a correct format.`，**所有 writer** 的历史都装不上。候选把它读成旧式 writer `imported-٠٠٠٠`，两条事件都可读。
  2. 「continuing legacy lines past segment 9999 refuses before any byte moves instead of splitting the writer」：旧的行续接把序号 10000 拼成 `w-10000.ndjson`（不是段名，读回是另一个 writer），再删掉旧式文件。base 探针：整理返回 `{ok:true}`，目录多出 `w-10000.ndjson`、`w-10001.ndjson`，`retainedWriterIdsAt` 从 `['w']` 变成 `['w','w-10000','w-10001']`——一个 writer 的流被拆成三个。候选在写任何字节前以 `segment ordinal exhausted the four-digit space` 拒绝，目录与旧式文件逐字节不变，仍是一个 writer。
  - 日志：`/workspace/vfs-cut/wt/logs-cut19c/seg-base-red.log`（base dist 为本卡上一刀落地后的构建副本）。
- **影子双算**（§13 计划，`evidence/CUT-19/shadow-segment-groups.mjs`）：同一目录名单（空夹具文件，临时目录，不碰真实 `.git/wanxiangshu`）分别交 base 与候选的 `writerPhysicalMetadata`，比较 writerId 与文件名。固定种子 2000 轮：纯 ASCII 名单 1715 轮全部逐项相等；含非 ASCII 数字名的 285 轮中 19 轮相等（怪名不是 `.ndjson`）、266 轮 base 抛错而候选正常分组；**不相等 0，候选抛错 0**。
- **行为差异（如实记录）**：①非 ASCII 数字尾的名字从“整库装载失败”变为旧式 writer；`w-٠٠٠٠` 这类 writerId 现在允许创建（其段名 `w-٠٠٠٠-0000.ndjson` 与旧式名都不会与别的 writer 同形）。②序号越界从“拆出新 writer”变为拒绝；`writeSegmentSeries` 与追加滚动本来就先算名、旧实现在追加滚动处已有同样的上界检查。③形态判定按“是否是本 writer 自己的段”而非“是否形似段名”区分旧式文件：只在 writerId 本身以 `-dddd` 结尾时才可能不同（这种 id 不能新建，只来自降级 stem），此时多出一次共存字节校验而不是直接并读。④名为 `.ndjson` 的文件（空 writer）在整理时拒绝命名，旧实现会把它改名成另一个 writer `-0000`。⑤库抛出的是 Fable 异常对象（不是 JS `Error` 实例，与 retention 一致），按消息识别。
- **验证**（同一工作树，rebase 到 master 04efd06ca，gitlink 7a2ccf6 / 0.23.0，重建库 dist 与主仓 build ok，fantomas --check 通过；落地前再 rebase 到 e4ff18704，gitlink 取 master 的 d2a9f3b / 0.23.1（含 0.23.0），重建后 35 文件同为 252 / 0，日志 `seg-cand-run2.log`）：durable-events + durable-convergence 35 文件 **252 pass / 0 fail** / 14 skip / 10 todo（上一刀 250 + 本刀 2 个反例）；另 21 个触及事件目录 / RetentionSurface 的文件 294 pass / 3 fail：managed-session-lifecycle-018（unknown-stop 得到 `CompletedAwaitingJoin`）与 -022 两例（`Promise resolution is still pending`），在 base dist 上以同样消息失败（`logs-cut19c/msl-base.log`），与本刀无关。日志 `logs-cut19c/seg-cand-run.log`、`seg-wide.log`、`shadow.json`。
- **回退**：只改名字的计算，不迁移、不改写任何已有文件；`git revert` 主仓提交即回旧律（库 0.23.0 可保留）。
- **未做**：全量 `scripts/verify.mjs` 与 integration tier。

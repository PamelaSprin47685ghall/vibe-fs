# CUT-19：多 writer 存储与收敛（子范围①结构 k-way 归并、②writer 保留规则）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-19) / [范围对账](../COVERAGE.md)。

## 1. 状态与范围

- **owner**：eng-cut19-goose（2026-10-20 认领，`board(CUT-19): claim` 993e93683）。reviewer：未配非作者 reviewer（本轮全自治，见 §8）。
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

## 10. 下一子范围（仍归本卡）

1. `IntegratorEngine.validateLocalHistory` 的词汇/载荷闭包检查与 `wanxiangli/events.validate` 收敛（CUT-18 遗留，注意 027 锁定的错误消息形状）。
2. writer 活动时刻的尾部判定（Journal `ObservedAt` 优先、`ProjectionCutTail` 跨段回溯）中的纯分类部分；文件读取留在主仓。
3. 段聚合与原子追加（`ProcessEventLog`）：需持久数据计划，按 README §10 另行准备。

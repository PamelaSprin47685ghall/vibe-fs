# STATUS/blocker-HOST-006 — Host compaction 无法全局关闭

状态：SSOT 例外协议已触发（第 1 次）。
触发日期：2026-07-30。
绑定 Host 版本：`1.18.9`（`packages/opencode/package.json:3`）。
绑定本仓库 commit：`cd1f8f09`。
完整源码证据：`STATUS/evidence/host-context-recovery.md` 第 6–10 项。

## 协议步骤

| 步 | 要求 | 状态 |
|----|------|------|
| 1 | 停止相关代码迁移 | 满足。包 X7 未开始写任何代码，X0 是它的前置 |
| 2 | 写本文件 | 本文件 |
| 3 | 证明是 Host 能力或逻辑矛盾，非实现困难，引用源码行号 | 见下方两节 |
| 4 | 修改 SSOT | HOST-006 重写；新增事实 `ContextReanchored` 进 PERSIST-010 |
| 5 | 在 `shock-anneal.md` 追加 supersedes 记录 | 已追加 |
| 6 | 重新冻结 | 本次提交 |

## 冻结版 HOST-006 的原文

```text
万象术管理的所有 X 和 Y Session 均禁止 Host compaction。自动、overflow、
manual compaction 必须关闭或拒绝，autocontinue 必须为 false。Host compaction
结果不得成为 PrefixEpoch、BlogFrame、FrozenB 或任何领域事实。Host 无法满足
该合同时启动失败。
```

## 第 3 步之一：Host 能力判据

四类 compaction 中三类可关闭，一类不可。

| 类别 | 关闭手段 | 源码 |
|------|---------|------|
| automatic | `compaction.auto = false` | `overflow.ts:28` 首行短路 |
| overflow（预测式） | 同上 | `processor.ts:477-479` 走同一 `isOverflow` |
| overflow（provider 报错后） | 同上 | `processor.ts:607-614` 提前返回，转为终局错误 |
| autocontinue | `experimental.compaction.autocontinue → enabled: false` | `compaction.ts:451-472` |
| manual | 不存在 | 见下 |

manual compaction 的完整路径，全程无 hook、无配置查询：

```text
POST /session/:sessionID/summarize
  groups/session.ts:303-315                 路由
  handlers/session.ts:273-293               直接调 compactSvc.create，不查 compaction.auto
  prompt.ts:1149-1159                       循环执行 compaction 任务，不查配置
  compaction.ts:513-536                     create 不接 hook、不查配置
```

TUI 侧同一入口：`packages/tui/src/config/keybind.ts:99`（`session_compact`，`<leader>c`）→ `packages/tui/src/routes/session/index.tsx:121,556` → `handlers/tui.ts:15`。

compaction 执行期唯一的 hook 无法否决：

```ts
// packages/opencode/src/session/compaction.ts:342-348
const compacting = yield* plugin.trigger(
  "experimental.session.compacting",
  { sessionID: input.sessionID },
  { context: [], prompt: undefined },
)
```

输出类型 `{ context: string[]; prompt?: string }`（`packages/plugin/src/index.ts:298-308`）无 `enabled` / `cancel` 字段。即便有，`plugin.trigger` 的返回值在调用点被丢弃（`packages/opencode/src/plugin/index.ts:284-293`），否决语义还需要改写调用点——那是 ARCH-003 禁止的修改 Host 本体。

已确认不存在的替代路径：
- 无 per-session compaction 配置。`Config.get()` 返回 per-instance 状态（`packages/opencode/src/config/config.ts:606-608`），compaction 相关键只在 `packages/core/src/v1/config/config.ts:149-156` 一处。
- `experimental.session.compacting` 的输入虽带 `sessionID`，但其效果面只有 prompt 与 context。
- 无「拒绝任务」类 hook。`prompt.ts:1149` 的任务分派不经过任何插件边界。

## 第 3 步之二：逻辑矛盾判据

冻结版把两句话连读：

```text
manual compaction 必须关闭或拒绝
+
Host 无法满足该合同时启动失败
```

由于 manual compaction 在所有受支持的 Host 版本上都无法关闭或拒绝，这两句要求插件在所有版本上无条件启动失败。

一条永远无法满足的条款不是 fail-closed 保护，是死产品。 这是条款自身的逻辑矛盾，不是实现难度问题——不存在任何实现能同时满足这两句。

## 修订方向：预防层 + 收容层

原条款只有一层：把所有 compaction 挡在门外。挡不住的那一类使整条条款失效。

修订后是两层，各自解决不同问题。

### 预防层：关掉能关的，关不掉就拒绝启动

`automatic`、`overflow`、`autocontinue`、`prune` 四项必须关闭，无法证明已关闭则 `HostContractUnsupported`。

这一层与冻结版一致，只增加了 `prune`（见后文）。

它对 Host 版本敏感：配置键名、hook 名、`isOverflow` 的短路位置都可能在上游变动。这是本层的固有脆弱性，不可消除。

### 收容层：出现的 compaction 一律重锚

无论 compaction 因何出现，插件执行同一次重锚（re-anchor）。

这一层对 Host 版本不敏感：它只依赖「compaction pseudo-run 在 transcript 里可识别」，而这是一个远比配置键稳定的观察面——`agent = "compaction"` / `mode = "compaction"` / `summary = true` 三者任一为真，已在 `next/OpenCode/SessionSnapshotPort.fs:127-134` 折叠为单一谓词 `IsCompaction`，`next/OpenCode/ReviewSeal.fs:56` 已在消费它。

由于 ARCH-003 禁止修改 Host、也无法钉住 Host 版本，耐用的那一层才是主要防线。 预防层是安全带，收容层是气囊。

### 两个来源，一条路径

收容层处理的两种情况在实现上不作区分：

```text
用户手动 /compact          官方支持的用法
Host 意外触发 compaction    预防层被绕过（未知版本、第二实现、配置漂移、上游 bug）
```

插件不区分来源，也不应区分。 三条理由：

其一，观察面相同。reconcile 出的 snapshot 里只有一条 compaction pseudo-run，没有「谁触发的」这个字段。

其二，试图区分需要读意图。插件手里没有意图，只有一条已经写进 transcript 的消息。造一个「这是我预期的 / 这不是我预期的」标记，等于在 Host 事实之外建第二事实来源。

其三，与 CTX-005 同构。那一条禁止按错误文本分类失败根因，理由是所有失败走同一恢复协议，分类只会长出永不执行的分支。compaction 来源同理：两种来源的正确处置完全一样，分类没有产出。

收容层永远武装，不因预防层通过而解除。 预防层的启动探测是一次性的、对当前 Host 版本的，收容层是每次 reconcile 都在的。

## 重锚的定义

「妥善处理」的具体含义是一次重锚，不是无动作，也不是崩溃：

```text
观察到 compaction pseudo-run
→ 退役 ActivePrefixEpoch（Snapshot → None，EpochId += 1）
→ Companion coverage 归零（IngestCursor、CoverableTurnCutoff → 0）
→ BlogFrame 全部保留
→ 后续正常轮次重新累积 coverage，probe 能力自然恢复
```

四条选择各有理由。

Epoch 退役而非保留。 `PrefixSnapshot.CutoffExclusive` 是旧编号下的索引，compaction 之后该编号不再指向任何真实前缀。退役后 X 的投影回到「system + 全部 raw history」，而 raw history 现在就是压缩后的短历史——这恰好是用户按 `/compact` 想要的。

Coverage 归零而非重新指向。 不把 cutoff 设成「摘要之后」并声称 B 覆盖了摘要的跨度：Companion 可能因 busy skip 落后于 Host，B 实际覆盖的范围小于摘要覆盖的范围。声称覆盖会用一个更窄的 B 替换一个更宽的摘要，是信息损失。归零后摘要作为普通 raw 消息留在历史里，Companion 从新编号 0 重新消化——它会为摘要本身写一条 frame，轻微冗余，无不正确。

Frames 全部保留。 B 是关于真实发生过的工作的记录，那些工作确实发生了。被 compaction 作废的只是「哪些 X turn 被覆盖」这个索引映射，不是工作日志的内容。删除 frames 会丢弃与本次事件无关的历史。

Epoch 递增而非原地清空。 这是一次真实的冷边界：provider 可见前缀确实变了，seal barrier 确实断了。COMPANION-009 的逐字节稳定以 epoch 为界，因此必须换 epoch 才能诚实地表达「前缀合法地变了」。

### 为何不是「靠 digest 失配自然降级」

一个更省事的方案是什么都不做：COMPANION-011 要求投影前重算 `CoveredPrefixDigest`，compaction 后必然失配，于是不构造 probe。

这个方案被否决。 digest 失配是永久的——`CoveredPrefixDigest` 记录的是旧编号下 `messages[0..cutoff)` 的哈希，compaction 之后该编号不再指向任何真实前缀，重算永远不等。于是那个 Session 在余下生命周期里再也不会有 probe，而用户看不到任何提示。

「用户按了一次键就永久静默关闭核心机制」比拒绝启动更糟：后者至少是响亮的。

### 重锚是一个持久事实

重锚改变 `PrefixEpoch` 与 Companion coverage 两处投影，且必须原子——只退役 epoch 不归零 coverage 会留下一个声称覆盖了不存在编号的 B。

因此新增一个事实，进 PERSIST-010 的 fold 规则：

```fsharp
type ContextReanchored =
    { SessionId: SessionId
      PreviousEpochId: int64
      NextEpochId: int64
      /// 证据：被观察到的 compaction pseudo-run 的物理消息 id。
      /// 这是「哪条消息证明它发生了」，不是「它为什么发生」——
      /// 前者是物理世界的事实，后者是 CTX-005 禁止的分类。
      ObservedCompactionMessageId: ProviderRunIdentity }
```

fold 校验：`PreviousEpochId` 必须等于当前 `EpochId`；`NextEpochId = PreviousEpochId + 1`；提交后 `Snapshot → None` 且 coverage 三元组归零。同一 compaction 消息 id 只接受一次重锚（幂等）。

单一写入口：观察 compaction 的那一个 reconcile 路径。

### 重锚不违反 CTX-001 / CTX-002

那两条禁止的是插件测量或预测上下文容量后主动切换 Epoch。

重锚的触发者是一个已经发生的外部物理事件——transcript 里多了一条 compaction 消息。插件读的是「发生了什么」，不是「还剩多少空间」。

## best effort 的确切边界

以下四项明确不保证：

不保证 B 覆盖了 Host 丢弃的内容。 Companion busy skip 期间未消化的 turn，在 compaction 之后同时消失于 transcript 与 B。信息真实丢失，本方案不尝试恢复，也无法恢复。

不保证前缀缓存连续。 epoch 切换是一次冷边界，一次重新 prefill。

不保证 Host 摘要的质量。 摘要文本永远不作为 B 读取，也不进入 FrozenB（这一条未修改）。

不保证紧随其后就能 probe。 重锚使未来的 probe 重新可能，但第一个 armed 槽可能找不到新 coverage，此时按 CTX-011 走正常主请求。

## autocontinue 在 manual 路径上不触发

`handlers/session.ts:282-290` 传 `auto: ctx.payload.auto ?? false`，而 `compaction.ts:422` 的 replay 分支与 `451` 的 hooked 分支都要求 `input.auto`。因此 manual `/compact` 不注入合成 continue 轮。

若某个 caller 显式传 `auto: true`，注入的 continue 消息没有 PromptKey，PROMPT-009 的解析把它判为 `HostInternal`（`next/Domain/PromptAuthorityRun.fs:189`）——既非 Authority Root 也非 Continuation，不改执行档案、不建 Logical Run。该语义已实现，无需新增。

## 顺带发现的一项必须新增的启动断言

`compaction.prune` 绕过 transform 直接删除持久消息行：

```ts
// packages/opencode/src/session/compaction.ts:248-250
const msgs = yield* session.messages({ sessionID })
```

默认 false（`packages/core/src/v1/config/config.ts:154-156`），但若被开启，它与 COMPANION-009 的「原始 Host transcript 永远不物理删除」直接冲突，且收容层也救不回来——被删的行不是索引失效，是不存在。

这一项可读、可断言，因此进入预防层的必须关闭清单。

## 未解决的次生风险：第二个 compaction 实现

`packages/core/src/session/runner/llm.ts:215` 调用 `compaction.compactIfNeeded`，该实现从外发请求估算（`packages/core/src/session/compaction.ts:225-236`），配置来自 config 文档（`compaction.ts:114-126`），完全没有插件 hook。

它接入了 `packages/core/src/location-services.ts:78`，但在 `packages/opencode/src/server` 中未找到驱动它的 HTTP 路由。无法从源码判定它在 1.18.9 是否可达。

处置：预防层的启动门禁不得只依赖静态源码结论，必须包含一次运行时探测。

探测判据不能是「compaction pseudo-run 数恒为 0」——那会把用户合法的 `/compact` 判成 Host 违约。判据是：

```text
首个 managed session 的第一轮请求完成后，该 session 的 compaction pseudo-run 数为 0
```

第一轮请求必然远低于任何阈值，automatic compaction 不可能合法触发。此时出现 pseudo-run 只能说明存在一个不受 `compaction.auto` 控制的第二实现。

有了收容层，为何还要拒绝启动？ 因为一个无法预防的自动 compaction 会把机制磨成无用：每隔几轮就 epoch 退役 + coverage 归零，probe 永远攒不够 coverage。每一次重锚都正确，整体却在空转，且从外部看起来一切正常。这正是「静默降级」——比响亮失败更坏的那一类。

残留误判：用户在插件启动后、首轮完成前手动 compact 一个空会话。后果是一次带明确原因的启动拒绝，用户不重复该动作即可。该误判可接受，因为反向漏判的代价是两套压缩系统静默并行。

## 修订不改变的部分

- Host compaction 结果不得成为 PrefixEpoch、BlogFrame、FrozenB 或任何领域事实——保留并扩充到 Authority Root 与 Continuation。`ContextReanchored` 记录的是插件自己的退役动作，不是把摘要当成事实
- automatic / overflow / autocontinue 必须关闭，无法证明已关闭则启动失败——保留
- CTX-001…014 全部不受影响。CTX-002 的「真实失败是唯一恢复触发信号」反而由 `auto = false` 下的 `processor.ts:607-614` 免费提供：溢出产生 `finish = "error"` 与 `status = idle`，reconcile 从完整 snapshot 读到 `Failed`
- CTX-005 的「失败不分类」在此处尤其重要：插件不得读 `ContextOverflowError` 这个名字来推断根因

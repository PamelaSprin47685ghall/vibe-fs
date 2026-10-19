# fragment-event-as-data — Lesson

## 评价

### 定义

Transport fragment 被误当 domain data，是因为 client 把**delivery choreography**当成 authoritative business fact。

Notification、patch、partial stream chunk、callback、delta、progress event、websocket frame、provider-specific update 往往只回答一个窄问题：

> 某些东西变了，或者一部分 observation 到了。

它们并不自动回答：

> 现在完整 authoritative state 到底是什么？

### 支配原则

这个区别很重要，因为很多 transport contract 本来就允许 coalesce、duplicate、reorder、omit intermediate update、reconnect 后从更晚位置继续、改变 patch granularity，而仍然完全符合协议。如果 client 把这些 fragment 当 durable ordered fact 去 fold domain state，就在偷偷把 transport guarantee 强化成 provider 从未承诺的东西。

### 何时触发

以下情形触发：

- websocket/SSE/provider delta 被直接 fold 成 canonical domain state，没有 authoritative refresh path；
- reconnect 从 “now” 继续，client 就当 missing intermediate fragment 从未重要；
- duplicate/reordered notification 能造出 source 从未存在过的 state；
- progress/update stream 被 persist 成 business history，但 provider 明确把它称为 ephemeral；
- callback arrival order 被当 transition order，却没有 sequence/commit identity；
- patch 说 “field X changed”，client 却把它应用到旧 base version 并当 current truth；
- test 假设每次 source mutation 必有一条 notification，但 contract 允许 coalescing。

### 不应触发

不要误杀真正 event source。如果 stream 本身就是 durable ordered domain facts，拥有 stable identity、replay、retention、authoritative semantics，那么 event sourcing 完全合法——这些 event 本来就是 data，不是“fragment 冒充 data”。

如果 fragment 只是 wake-up signal 也没问题：notification 到 → 读取 authoritative snapshot/version → domain behavior 基于完整 state。

### 与相邻规则区分

邻近规则：

- `snapshot-as-truth`：derived projection 高于 source；
- `log-as-recovery-protocol`：diagnostic output 被当 durable truth；
- `race-first-wins-semantics`：arrival order 决定 business outcome。

### 判定程序

最决定性的分类问题：

> 这条 channel 是 **fact log**，还是 **notification channel**？

如果 provider 可以 drop/coalesce/reorder fragment 而仍然算正确，这条 channel 就不能安全定义 domain history。它能告诉你**什么时候该看**，不能告诉你**该相信什么**。

### 例子

健康 client 常把 fragment 当 invalidation hint：

```text
notification arrives
        ↓
identify affected object/version
        ↓
read authoritative state
        ↓
replace/derive local view
```

如果 full refresh 太贵而必须 incremental，也应强化 protocol，而不是给 client 堆 heuristic：base version、event identity、必要 order、gap detection、replay/resume、duplicate semantics、snapshot/resync escape hatch，都应由 provider contract 正式提供。

## 建议

### 现在该做什么

把 notification 与 truth 拆开。

如果 transport 没承诺 durable / complete / ordered facts，就只用 fragment 决定**什么时候、该刷新什么**，然后从真正 source of record 读取 authoritative object/state/version。

### 为什么重要

这样 coalescing、duplication、intermediate omission 最多影响 latency/efficiency，不再直接破坏 correctness。

### 修复策略

一个健康模式：

```text
fragment / notification
        ↓
mark object stale or changed
        ↓
fetch/read authoritative version
        ↓
replace local canonical state
        ↓
derive UI/workflow consequence
```

如果 full snapshot 太贵而必须 incremental，不要在 client 堆 heuristic，要强化 protocol。一个安全 incremental protocol 往往需要：

- stable event/delta identity；
- patch 对应的 source/base version；
- domain 所需的 monotonic sequence / causal relation；
- explicit gap detection；
- 从 known cursor replay/resume；
- duplicate/idempotency semantics；
- gap 无法修时可 snapshot/resync；
- provider 明确说明每个 semantic transition 是否一定出现为 event。

### 决策分支

没有这些 guarantee，client 自己做 buffering/reordering，通常是在本地制造一个 provider 根本没提供的更强协议。

### 常见假修复

常见假修复：

- debounce harder，希望 coalesced update “差不多就是 latest”；
- 维护巨大 reorder window，却没有 source sequence 证明 gap；
- 把收到的 patch persist 成新的 system of record；
- reconnect 后直接从 newest message 继续，不证明 missed history；
- duplicate fragment 按一次次 delta apply，虽然 operation 非 idempotent；
- transport timestamp 被当 authoritative order；
- patch 直接叠到当前 local base，不验证 base version；
- provider 从未承诺 fragment replay，却为“missing fragment”加 retry。

### 验证

验证要主动攻击 delivery contract。对**非 authoritative notification**执行 drop、duplicate、coalesce、reorder。Refresh/resync 后 local domain state 必须收敛到 authoritative source。

如果是真 event protocol，则反过来测：missing event 必须可检测，replay/recovery 必须恢复 source 承诺的 exact history semantics，不能退回 “大概 current” 的模糊状态。

还要测 reconnect 与 stale-base patch。针对 version N 的 delta，不能盲目应用到 N+2，除非 delta contract 能证明这样仍然合法。

### 完成条件

完成条件很简单：

> Transport behavior 可以改变 client **什么时候**知道该刷新；除非 transport 本身就是明确 authoritative fact log，否则它不能改变 client 最终**相信哪些事实**。

边界一旦清楚，UI 仍可激进使用 fragment 获得 responsiveness；business truth 继续锚定 source contract，而不是 packet choreography。

> Ephemeral delta 先当 hint，除非它真正赚到了“history”这个更强称号。

### Nudge

> 不要把 transport 的形状抬成 truth 的形状。Notification 可以告诉你去哪里看，只有 authoritative contract 才能告诉你发生了什么。

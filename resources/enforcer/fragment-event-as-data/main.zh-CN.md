# fragment-event-as-data — Main

把 notification 与 truth 拆开。

如果 transport 没承诺 durable / complete / ordered facts，就只用 fragment 决定**什么时候、该刷新什么**，然后从真正 source of record 读取 authoritative object/state/version。

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

这样 coalescing、duplication、intermediate omission 最多影响 latency/efficiency，不再直接破坏 correctness。

如果 full snapshot 太贵而必须 incremental，不要在 client 堆 heuristic，要强化 protocol。一个安全 incremental protocol 往往需要：

- stable event/delta identity；
- patch 对应的 source/base version；
- domain 所需的 monotonic sequence / causal relation；
- explicit gap detection；
- 从 known cursor replay/resume；
- duplicate/idempotency semantics；
- gap 无法修时可 snapshot/resync；
- provider 明确说明每个 semantic transition 是否一定出现为 event。

没有这些 guarantee，client 自己做 buffering/reordering，通常是在本地制造一个 provider 根本没提供的更强协议。

常见假修复：

- debounce harder，希望 coalesced update “差不多就是 latest”；
- 维护巨大 reorder window，却没有 source sequence 证明 gap；
- 把收到的 patch persist 成新的 system of record；
- reconnect 后直接从 newest message 继续，不证明 missed history；
- duplicate fragment 按一次次 delta apply，虽然 operation 非 idempotent；
- transport timestamp 被当 authoritative order；
- patch 直接叠到当前 local base，不验证 base version；
- provider 从未承诺 fragment replay，却为“missing fragment”加 retry。

验证要主动攻击 delivery contract。对**非 authoritative notification**执行 drop、duplicate、coalesce、reorder。Refresh/resync 后 local domain state 必须收敛到 authoritative source。

如果是真 event protocol，则反过来测：missing event 必须可检测，replay/recovery 必须恢复 source 承诺的 exact history semantics，不能退回 “大概 current” 的模糊状态。

还要测 reconnect 与 stale-base patch。针对 version N 的 delta，不能盲目应用到 N+2，除非 delta contract 能证明这样仍然合法。

完成条件很简单：

> Transport behavior 可以改变 client **什么时候**知道该刷新；除非 transport 本身就是明确 authoritative fact log，否则它不能改变 client 最终**相信哪些事实**。

边界一旦清楚，UI 仍可激进使用 fragment 获得 responsiveness；business truth 继续锚定 source contract，而不是 packet choreography。

> Ephemeral delta 先当 hint，除非它真正赚到了“history”这个更强称号。
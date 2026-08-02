# Active Blockers

## HOST-006 次生风险：Host 第二个 compaction 实现的运行时探测（开放）

状态：开放。预防层已入 SSOT/07 HOST-006（CONFORMANT，`HostCompactionGate.fs` 14 项
第 1 层测试），但以下次生风险未闭合：

`packages/core/src/session/runner/llm.ts:215` 调用 `compaction.compactIfNeeded`，该实现从
外发请求估算（`packages/core/src/session/compaction.ts:225-236`），配置来自 config 文档
（`compaction.ts:114-126`），完全没有插件 hook。它接入 `packages/core/src/location-services.ts:78`
但在 `packages/opencode/src/server` 中未找到驱动它的 HTTP 路由，无法从源码判定它在
Host 1.18.9 是否可达。

处置（已定，未执行）：预防层的启动门禁不得只依赖静态源码结论，必须包含一次运行时
探测。判据：首个 managed session 的第一轮请求完成后，该 session 的 compaction
pseudo-run 数为 0。第一轮必然远低于任何阈值，此时出现 pseudo-run 只能说明存在一个不受
`compaction.auto` 控制的第二实现。残留误判可接受：用户在插件启动后、首轮完成前手动
compact 一个空会话，会得到一次带明确原因的启动拒绝。

为什么必须闭合：一个无法预防的自动 compaction 会把机制磨成无用——每隔几轮就 epoch
退役 + coverage 归零，probe 永远攒不够 coverage。每一次重锚都正确，整体却在空转，且从
外部看起来一切正常——静默降级，比响亮失败更坏。

历史裁决全文见 `docs/archive/shock-anneal-2026/FINAL-REPORT.md` §7（Host compaction 裁决）
与 `docs/archive/shock-anneal-2026/evidence/host-context-recovery.md`。

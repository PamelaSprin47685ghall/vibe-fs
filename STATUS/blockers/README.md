# Active Blockers

## HOST-006 次生风险：Host 第二个 compaction 实现的运行时探测（已闭合）

状态：已闭合。预防层已入 SSOT/07 HOST-006（CONFORMANT，`HostCompactionGate.fs` 14 项
第 1 层测试），运行时探测已实现并接线，见下。

### 闭合证据

运行时探测由三处组成，全部存在并接线：

| 构件 | 位置 | 职责 |
|------|------|------|
| 判据 | `HostCompactionGate.judgeStartup`（`Infrastructure/OpenCode/Host/HostCompactionGate.fs:181`） | 首个 managed session 第一轮完成后，该 session 的 compaction pseudo-run 数必须为 0；否则 `CompactionGateVerdict.CompactedDespiteSettings` → `HostContractUnsupported` |
| 调用点 | `HostSignalBootstrap.onSnapshot`（`HostSignalBootstrap.fs:150-157`） | 每次 reconcile pass 的最后 snapshot 上判定；`CompactionProbePending` 为假后跳过 |
| 单例 latch | `PluginRuntimeScope.TryClaimStartupProbe`（`PluginRuntimeScope.fs:128-140`） | 每插件实例恰好判定一次；并发 pass 不会重复判定 |
| setting gap | `SpikePlugin.fs:165-167` config hook → `PluginRuntimeScope.RecordCompactionSettingGap` | 预防层四项（auto/overflow/prune/autocontinue）关闭结果在启动时记录，`SettingUnavailable` 优先于观察判据（根因优先） |

残留误判（用户在插件启动后、首轮完成前手动 compact 空会话 → 一次带原因的启动拒绝）
在 `HostCompactionPolicy.fs:128-132` 注释中显式记录，与裁决一致。

### 新观察（2026-06，读 `../opencode` 1.18.10 源码确认）

blocker 的物理前提成立：Host 确实存在不受 `compaction.auto` 控制的第二实现，且经
生产路径可达——

- V1 主路径（TUI/CLI，`SessionPrompt.runLoop`）的 overflow 检查全部有 `auto === false`
  短路：`packages/opencode/src/session/overflow.ts:28` 与 `processor.ts:608`。
- V2 runner（`packages/core/src/session/runner/llm.ts:370` → `compactAfterOverflow`，
  `packages/core/src/session/compaction.ts:172-224`）缺少 `!config.auto` 短路，且经
  `packages/opencode/src/server/routes/instance/httpapi/server.ts:299`（或
  `packages/server/src/routes.ts:52`）→ `SessionV2.prompt` → `execution.wake` → coordinator
  → `runner.run` 可达。V2 的 provider overflow 恢复会在 `auto=false` 时仍触发 compaction。

即：若插件只依赖静态配置结论，V2 路径会在用户无感知时写入 compaction pseudo-run，
把重锚机制磨成空转。运行时探测的存在因此是必要的，不是过度防御——探测在首轮判定
pseudo-run 数，恰好能抓到这条不受配置控制的路径。

处置（已定，未执行）：无。探测已落地，`SSOT/07.md` 条款与实现一致。此观察项可留给
Host 上游（V2 runner 的 `compactAfterOverflow` 未遵守 `compaction.auto=false`），
不属于本仓库可修范围（ARCH-003）。

历史裁决全文见 `docs/archive/shock-anneal-2026/FINAL-REPORT.md` §7（Host compaction 裁决）
与 `docs/archive/shock-anneal-2026/evidence/host-context-recovery.md`。

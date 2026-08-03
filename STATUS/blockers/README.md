# Active Blockers

## Blogger 垂直切片剩余缺口（2026-08，C0–C4 已合入）

状态：活跃。主链 C0–C4 + frame loader/anti-regression 已合入（`1d0568e4` 及前序）。
下列项阻止将 COMPANION-005/008、CTX-006/007/012、ENFORCER-010 升为 `CONFORMANT`。

| 级别 | 缺口 | 评估 | 处置 |
|------|------|------|------|
| HIGH | 五窗口 crash recovery | durable materialization + receipt 已有；缺 Host snapshot reconcile 启动路径（窗口 A–E） | C5 剩余：启动时 fold open requests + Host |
| MEDIUM | layer-4 竞态/Squash/invalid canary | 第 1 层红测/门禁已绿；三轮 canary 未建 | C7 |
| LOW | physical/synthetic provenance 在 Host obj 上保留 | builder 有 `IsPhysical`；转 Host obj 时未携带 | C6 |
| LOW | teardown 全出口审计 | main dispose vs BloggerSessionId waiter key 未完整审计 | C6 |

已闭合：C0–C4 主链；C5 materialize/receipt/冻结 epoch；CommitUnknown 三态 + 一次 repair（`RepairSpent`）；KnownCommitted 才 Park。

## Enforcer 接线 security_review 观察项（2026-08，f854c092 + 0255a6b4）

状态：观察项，不阻塞。security_review（sa_..._8de306a069d9）对 SSOT/15 Enforcer 纵向
第一段（blog 工具/cycle 提交/挂起链）的结论为 verdict=warn，无 blocking；四项观察的
评估如下。

| 级别 | 观察 | 评估 | 处置 |
|------|------|------|------|
| MEDIUM | 跨进程幂等竞态：两个插件实例对同一 ProviderRun 并发提交 cycle 可致 journal 重复 | 不可达：`BlogEntryCommitted` 按 `StreamId.Session mainSessionId` 写，同一 session 只归属一个插件实例管理（同 stream 单写者）；already 检查 + fold 按 ProviderRun 拒绝兜底（独立 `EnforcementCycleCommitted` 已删除） | 记录；若未来出现跨实例共享 session 的接线（如 V2 runner），重验 |
| MEDIUM | synthetic user 消息（ENFORCER-051 delta 注入）正文无来源标记 | 注入消息 id 携带确定性 `enforcer-delta` 前缀；Blogger system prompt 声明 delta 为低信任内容（COMPANION-010 语义）；不进入主模型投影 | 不改；如需审计区分，读 id 前缀即可 |
| LOW | cycle blob（text/score/evidence）无大小上限 | 文本来源为 Blogger 输出与 delta（上游受 `BloggerDelta.DeltaLimitBytes` 约束）；无 cycle 级 MaxBlogTextBytes/MaxEvidenceBytes | 本轮 Blogger 收敛 C4 补统一 UTF-8 上限；不接 nudge |
| LOW | 诊断（`enforcer-cycle-*`）的 result 字段可能含内部错误字符串 | 字段白名单（CTX-014）不含路径类字段；错误串为固定文案 | 不改 |

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

### 新观察（2026-08，读 `../opencode` 1.18.10 源码确认）

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

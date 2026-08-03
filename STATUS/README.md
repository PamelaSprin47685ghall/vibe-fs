# Current Repository Status

## 当前基线

- 分支：`master`
- 收敛目标：`0.5.2`（开发中，尚未 tag）
- 最后验证：
  - `npm run lint` 通过
  - `npm run gate:static` 通过（含新增 `gate:conformance`）
  - `npm run build` 通过
  - `npm run test:unit` 通过（702 / 702）
  - `npm run test:harness` 通过（285 / 285）
  - `manager-companion-canary.mjs` 独立重跑通过
  - `npm pack` 产出 `wanxiangshu-0.5.2.tgz`（404 files, 1.8 MB packed）
- 证据：`docs/evidence/0.5.2-baseline/` + `docs/evidence/0.5.2/`（C12–C14 反向审计与 RC
  快照：STATIC/BUILD/UNIT/HARNESS/CANARY-3ROUND、pack+sha256、INSTALL/IMPORT、LEGACY 等；
  P0×3 当前红 — 见 CANARY-3ROUND.txt）
- 合规表：[`STATUS/conformance.md`](STATUS/conformance.md)（由 `STATUS/conformance.toml` 生成；C11 partial 已升 7 条 layer-2）

## 当前产品状态

0.5.2 收敛中。C0–C8 与 C11 已闭合：Active IMPLEMENTING=0（commit `cc5a8206`），
本轮闭合 PROMPT-007 / HOST-010 / HOST-011 / EXEC-009 layer 2 四条；轻量 C12 快照
（COMMIT / STATIC / PACKAGE-CONTENTS）已落 `docs/evidence/0.5.2/`：

- C0：建立 0.5.2 baseline，跑通 `test:release` 并记录证据。
- C1：把 Strength / StudentTeacher / Enforcer nudge throttle 迁出 Active SSOT 到 `RFC/`，
  `SSOT/00.md` 拆分 Active 与 RFC 索引，`SSOT/15.md` 明确为 Blogger 工具化 Active 子集。
- C2：建立 `STATUS/conformance.toml` 与 `scripts/conformance-gen.mjs` / `conformance-gate.mjs`，
  生成 180 条 Active 条款机器账本。
- C3：版本与状态真值对齐到 `0.5.2`（package.json、package-lock.json、packaging template、
  README、CHANGELOG、STATUS/blockers/README.md）。
- C4：删除 legacy Companion/Blogger 旁路入口（`Companion.Submit`、`SubmitProjection`、
  `StartMainFromContext`、`startMainFromContext`、`blog` 函数、`CompanionOutcome` DU）。
- C5：建立唯一 `ManagedAgentCatalog`（commit `204a41ca`）：消费
  `PromptAuthority`/`ManagedAgent`/`ManagedAgentConfig` 三处重复角色/peer/legacy name，
  统一版本无关拒绝文案，新增 `scripts/role-matrix-gate.mjs` 并注册入 `gate:static`，
  补 AGENT-001/002/003/004 目录测试与 facade 出口。
- C6：durable join（`EXEC-009`，commit `cb3457db` + `99fcb06f` 重命名
  `HandleCompletionBlob` → `HandleCompletionCodec`）：`HandleCompleted` 携带 durable
  completion blob ref/digest；`HostForkRuntime.Join` 改投影优先消费
  （`HandleProjection.joinable` + `HandleController.consume` CAS 退休），mailbox 降级
  为通知；restart 从 blob 恢复完成；`ForkTypes` legacy 宽松语义删除。
- C7：durable effects（`PERSIST-009` CONFORMANT layer 2，`verified_commit` `db6693f5`）：
  删除零调用点 `DurableEffectRequested`/`Accepted` + `EffectProjection`；typed
  `WorktreeCreateRequested`/`WorktreeCreated`（writer=`Orchestrator.forkManagerCore`）；
  Git publish 既有 `PublishClaimed`/`Published`；session create 物理限制记入 SSOT/11
  （`HandleLinked`/`CompanionBloggerLinked` 作 Accepted 证据）。证据
  `docs/evidence/0.5.2/PERSIST-009-EFFECTS.txt`。
- C8：ARCH-001 最小诚实范围——`JobProgress` 已是纯业务事实 DU（每 case 带物理证据，
  `recoveryAction` 从事实推导，非程序计数器）；无重写 Orchestrator。仅向
  `architecture-gate` `FORBIDDEN_TOKENS` 补 `CurrentStage`/`StepIndex`（standalone
  `Phase` 不加：word-boundary 会误伤禁令自引用注释）。

0.5.1 已发布（tag `v0.5.1`）：闭合 SSOT/15 Blogger 请求形状 / 挂起 / Squash / crash recovery 纵向链：
唯一 coordinator、typed materialize、blog-tool Squash、KnownCommitted 才 Park、
crash-window 不 stomp live CurrentRequest。canary 证据见 `host-transform-capability`
与 `companion-canary`；发布证据目录 `docs/evidence/0.5.1/`。

LifecycleWorkRecord 迁移已完成（方案 `STATUS/lifecycle-work-record.md`）：父→子与子→父统一为
LWR（Y frames + X gap + Terminal；Opening 由 `includeOpening` 控制）；A/B 双轨、FinalText、
Seed、TerminalSessionA、FrozenB 全部废止；Y normal delta data-only、Blogger delta 稀疏 schema、
TOML data body 单 LF、join 最小 wire（status/agent/work_record）。

本轮补充合同：
- tool call/result 可进 XTrace 作 Y 压缩源；LWR gap/terminal 禁止 raw tool（`forWorkRecord`）
- 父→子 `includeOpening=true`；子→父 join `includeOpening=false`（布置者已知任务）
- 自定义 tool result 经 `ToolResultBound` 抢先留尾截断（34B marker + ≤1998 行 / ≤51166B），
  使 Host 默认 2000 行 / 50 KiB head 截断 no-op（ARCH-010-TOOL-BOUND）

## 当前开发阶段

0.5.2 收敛：按 `STATUS/0.5.2-convergence.md` 执行 C0–C15。

SSOT/14 Strength、SSOT/16 Student&Teacher 与 ENFORCER nudge/throttle 已迁出 Active
SSOT 至 `RFC/`，不属于 0.5.2 产品合同。Active 子集为 SSOT/01–13 + SSOT/15 Blogger 工具化 +
SSOT/17 LOOP（退化循环检测与强杀）。

### 本轮已闭合：PrefixCoverage 推进与 prefix-probe

`8bfea409` 之后 fallback canary 的 `prefix-probe` 从未触发。根因不是
`semanticCursorFor`，而是 `commitCycle` 只推进了 RecordCoverage 一半：

1. `commitCycle` 未消费 staged PrefixCoverage（`Session/EnforcerHost.fs`）：
   `NextCoverableTurnCutoffExclusive` / `NextCoveredPrefixDigest` 写成当前值
   自指，PrefixCoverage 永远停在 0 → `hasCoverage=false` → probe 永不选中。
   修复：staged `BloggerMainRequestContext` 成为唯一 coverage 源（fail closed）。
2. `mainContextFromChunk` 不计算 CoveredPrefixDigest：恢复旧路径——cutoff
   前进时对 projection 前缀做 `renderSemantic` 哈希；cutoff 不动时保留旧 digest。
3. `lastCoveredSequence` 对齐 `semanticCursorFor` 的 `>` 语义：chunk 的
   `NextCursor` 是「首个未覆盖」位置，映射为「末个已覆盖」XTrace sequence。
4. canary 剧本：Enforcer 接线后 Blogger 只接受 `blog` 工具；fallback /
   fallback-aabb-trace 仍回 plain text → 无 `BlogEntryCommitted` → 无 coverage。
   已改为 `tool-call blog`，并声明 `frame-commit` 冷边界。

验证：`npm run build` + 616 unit + fallback + fallback-aabb-trace +
host-transform-capability 全绿。

## 活跃阻塞

见 `STATUS/blockers/README.md`。当前无活跃 blocker。

## 已知未闭合项

0.5.2 尚未发布。Active IMPLEMENTING=0（commit `cc5a8206`）；C11 余项已闭合
（layer 2 单测 + EXEC-009 canary 断言已写入）：

- 已 CONFORMANT（本轮）：`PROMPT-007`（`AwaitMode.Detached` + fire-and-forget 测试）、
  `HOST-010`（`bindableRun` 正/负用例）、`HOST-011`（`ToolHostCodec.decodeContext`）、
  `EXEC-009`（layer 2；`host-restart.toml` 已加 `HandleCompleted`/`HandleRetired` 断言；
  layer-4 绿跑仍归 DevOps）
- 已 CONFORMANT（C11 partial，layer 2）：`AGENT-007`、`PROMPT-004/006/009`、
  `HOST-005/009`、`COMPANION-013`（`verified_commit=db6693f5`）
- `PERSIST-009`：已 CONFORMANT（layer 2，`db6693f5`）；worktree 路径无独立 fault-injection
  canary（依赖 fold 单测 + publish canary）
- X 恢复链：生产接线已闭合，但 X-A–X-D canary 剧本未建，第 4 层证据未产出。
- `EnforcerCodec` / `EnforcerCycle` / `EnforcerHost` 仍携带 `ScoreVectorRef`、`MergedScores`、
  nudge/throttle 路径；0.5.2 Active 子集仅保留 text/evidence。
- `manager-companion-canary.mjs` 在并发套件中存在 flaky：两次判据事件间隔超过
  `WATCHDOG_TIMEOUT_MS` 导致 watchdog 误触发。已写入 `AGENTS.md`，待修复。

## 源码地图

生产源码唯一根：`src/Wanxiangshu.Next/`（`Wanxiangshu.Next.fsproj` 编译全部 203 个 `.fs`）。

```text
src/Wanxiangshu.Next/
├── Kernel/                       领域内核：身份、角色、Flow、事实、结果
├── Domain/                       纯领域：PromptAuthority/Review/Recovery/Projection/Strength/Enforcer/StudentTeacher
├── Journal/                      持久化：Envelope/Writer/Fold/各 Projection
├── Session/                      会话运行时：Companion/Fork/Fallback/Review 控制器
├── Process/                      进程与 PTY：Runner/Deadline/LargeGate/Pty*
├── Agent/                        代理程序
├── Application/
│   ├── Orchestration/            Orchestrator 应用流程
│   ├── Reconciliation/           turn 恢复/协调/重放（XWire、TurnReconcile 等）
│   └── Prompting/                prompt 派发/ingress（Dispatcher、Ingress、Authority）
├── Infrastructure/
│   ├── OpenCode/Host/            Host 适配：插件生命周期、session 管理、Orchestrator Host
│   ├── OpenCode/Codec/           Host 事件/消息/tool/prompt 编解码 + wire 类型
│   ├── OpenCode/Plugin/          插件入口（Plugin/SpikePlugin）
│   ├── OpenCode/Signals/         信号类型与订阅
│   ├── OpenCode/Tools/           工具定义与工具运行时
│   └── Git/                      Git 设施（Orchestrator 的 git/worktree/lockfile 适配）
├── Host/                         HostDigest
├── Tools/                        文件/静态工具与 prompt 资产
├── prompts/                      Agent system prompts
└── Wanxiangshu.Next.fsproj
```

布局纪律由 `scripts/repository-layout-gate.mjs`（gate:static 第一段）机器验证：
根目录白名单、生产源码唯一根、顶层 module 与文件名一致、重复源码探测。分发产物契约
不变：Fable 输出 `build/next/`，npm 包 main 指向 `next/Infrastructure/OpenCode/Plugin/Plugin.js`
（模板 `packaging/npm-package.template.json`）。

## 下一步

0.5.2 剩余收敛项（见 `STATUS/0.5.2-convergence.md`）：

- Active IMPLEMENTING = 5（LOOP-001/002/007/008/010）；Active CONFORMANT = 187/192
- LOOP：SSOT/17 已注册进 conformance 账本（`17.md` 入 ACTIVE_SSOT，LOOP 前缀已入
  ssot-lint 与 gate 正则）；生产三件套 `Domain/LoopDetector.fs`、`Host/LoopSensor.fs`、
  `TurnCompletionProgram.fs` 已实现，层 1/2 测试 `tests-mjs/Domain/loop-detector.test.mjs`、
  `loop-sensor.test.mjs` 覆盖 LOOP-003/004/005/006/009/011（CONFORMANT layer 2）；
  LOOP-001/002/007/008/010 暂无判据，标 implementing；canary 未建，不发明
- C9：layer-2 已闭合；identity canary（transform id = tool messageID）仅保留为 Host
  升级门禁可选加强项
- C10：Context recovery X-A–X-D 第四层 canary 剧本未建，仍待产出
- C11：已闭合（IMPLEMENTING=0）
- C12：已补全 — NODE-MATRIX（single-node v25.9.0，诚实声明 expand later）、
  真实 `npm pack` + `TARBALL.sha256`、隔离 INSTALL/IMPORT 绿
  （证据 `docs/evidence/0.5.2/`）
- C13：反向审计证据已落盘（LEGACY-SCAN / REACHABILITY / SINGLE-WRITER /
  ACTIVE-NONCONFORMANT-SCAN / VERSION-CHECK）
- C14：gate:static / build / unit(702) / harness(285) 绿；
  `test:e2e:p0:three` 失败 iteration 1：`fallback-canary.mjs`
  `cold boundary never fired: continue.0 (prefix-probe)`。
  绿前 不 tag（C15 阻塞）
- C15：tag `v0.5.2` — 阻塞于 C14 e2e

IMPLEMENTING 条款：LOOP-001 / LOOP-002 / LOOP-007 / LOOP-008 / LOOP-010（生产已实现，
判据未产出；见 SSOT/17 与 `STATUS/conformance.toml`）。

历史项：

1. （已闭合）共享 Host capability canary——`host-transform-capability` 已建并全绿：证明
   STRENGTH-078 C-01…C-10（每请求一次 transform、挂起/恢复、跨 session 并行、取消、
   tool-loop continuation、blog 工具立即返回 "OK"）与 ENFORCER-180 第 0 步 1–6。支撑构件：
   `Session/ParkedTransform.fs`（挂起原语，ENFORCER-160/162）、`Session/EnforcerHost.fs`
   （cycle 原子提交 + offer/恢复/synthetic delta 注入，ENFORCER-044/047/050/051）、
   `Infrastructure/OpenCode/Tools/BlogTool.fs`（blog 工具，ENFORCER-010/020/040/041）、
   `Journal/EnforcementProjection.fs`（`BlogEntryCommitted` 原子推进；独立
   `EnforcementCycleCommitted` 已删除）。接线经普通 review + security_review 双审查
   （无 blocking）；security_review 观察项记于 `STATUS/blockers/README.md`。
   Blogger 垂直切片已 CONFORMANT（0.5.1）
2. 逐纵向接线（推荐顺序不变）：Strength shadow（Replica session/ruleset/候选帧，解锁
   STRENGTH-078 C-11…C-21）→ Enforcer nudge overlay（ENFORCER-080…115，第 0 步 7–9
   补完）→ Student&Teacher（teacher/return 工具、QA 落盘，LEARN-082…088）
3. 包 K8f：X-A–X-D 剧本（X 恢复链生产接线已闭合；剧本未建，第 4 层证据未产出）
4. `EXEC-009` durable join（生产已闭合；CONFORMANT layer 2；`host-restart` 已挂断言，
   layer-4 绿跑待 DevOps）
5. `CompanionDelta.jsonDelta` 替换为包 X3 的 TOML delta（当前仍在 Submit 路径）

## 事实入口

- 正式规范：`SSOT/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`

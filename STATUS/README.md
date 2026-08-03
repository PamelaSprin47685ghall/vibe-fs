# Current Repository Status

## 当前基线

- 分支：`master`
- 最后验证 commit：`8d817db8`（PrefixCoverage 从 staged context 推进；fallback prefix-probe 闭合）
- 本轮验证：build + 616 unit + fallback / fallback-aabb-trace / host-transform-capability 全绿

## 当前产品状态

0.5.0 已发布（正式版，从 rc.1 收口）。canary 森林 17 驱动（18 剧本）× 3 轮全绿，
`test:release`（gate:static → build → unit → harness → P0×3）完整通过。生产代码与测试
整体迁移到 `SSOT/` 条款；测试体系为直接消费 `build/next` 发布产物的 `tests-mjs`。
conformance 表 `UNVERIFIED` 已清零（绑定 commit `24bda4f5`）。

LifecycleWorkRecord 迁移已完成（方案 `STATUS/lifecycle-work-record.md`）：父→子与子→父统一为
LWR（Opening + Y frames + X gap + Terminal）；A/B 双轨、FinalText、Seed、TerminalSessionA、
FrozenB 全部废止；Y normal delta data-only、Blogger delta 稀疏 schema、TOML data body 单 LF、
join 最小 wire（status/agent/work_record）。

## 当前开发阶段

SSOT/14-16（Strength / Enforcer / Student&Teacher）纯领域内核已合入并测试；共享
Host capability canary（`host-transform-capability`，STRENGTH-078 C-01…C-10 /
ENFORCER-180 第 0 步 1–6）已建并全绿，Enforcer 的 blog 工具与挂起链已接线
（PARTIAL）。下一步是逐纵向接线（推荐顺序：Strength shadow → Enforcer nudge
overlay → Student/Teacher）。

### 本轮已闭合：PrefixCoverage 推进与 prefix-probe

`8bfea409` 之后 fallback canary 的 `prefix-probe` 从未触发。根因不是
`semanticCursorFor`，而是 `commitCycle` 只推进了 RecordCoverage 一半：

1. **`commitCycle` 未消费 staged PrefixCoverage**（`Session/EnforcerHost.fs`）：
   `NextCoverableTurnCutoffExclusive` / `NextCoveredPrefixDigest` 写成当前值
   自指，PrefixCoverage 永远停在 0 → `hasCoverage=false` → probe 永不选中。
   修复：staged `BloggerMainRequestContext` 成为唯一 coverage 源（fail closed）。
2. **`mainContextFromChunk` 不计算 CoveredPrefixDigest**：恢复旧路径——cutoff
   前进时对 projection 前缀做 `renderSemantic` 哈希；cutoff 不动时保留旧 digest。
3. **`lastCoveredSequence` 对齐 `semanticCursorFor` 的 `>` 语义**：chunk 的
   `NextCursor` 是「首个未覆盖」位置，映射为「末个已覆盖」XTrace sequence。
4. **canary 剧本**：Enforcer 接线后 Blogger 只接受 `blog` 工具；fallback /
   fallback-aabb-trace 仍回 plain text → 无 `BlogEntryCommitted` → 无 coverage。
   已改为 `tool-call blog`，并声明 `frame-commit` 冷边界。

验证：`npm run build` + 616 unit + fallback + fallback-aabb-trace +
host-transform-capability 全绿。

## 活跃阻塞

见 `STATUS/blockers/README.md`。HOST-006 次生风险（运行时探测）已闭合：探测已实现并
接线（`HostSignalBootstrap.onSnapshot` → `HostCompactionGate.judgeStartup` →
`PluginRuntimeScope.TryClaimStartupProbe`）。无未闭合 blocker。

## 源码地图

生产源码唯一根：`src/Wanxiangshu.Next/`（`Wanxiangshu.Next.fsproj` 编译全部 190 个 `.fs`）。

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

1. （已闭合）共享 Host capability canary——`host-transform-capability` 已建并全绿：证明
   STRENGTH-078 C-01…C-10（每请求一次 transform、挂起/恢复、跨 session 并行、取消、
   tool-loop continuation、blog 工具立即返回 "OK"）与 ENFORCER-180 第 0 步 1–6。支撑构件：
   `Session/ParkedTransform.fs`（挂起原语，ENFORCER-160/162）、`Session/EnforcerHost.fs`
   （cycle 原子提交 + offer/恢复/synthetic delta 注入，ENFORCER-044/047/050/051）、
   `Infrastructure/OpenCode/Tools/BlogTool.fs`（blog 工具，ENFORCER-010/020/040/041）、
   `Journal/EnforcementProjection.fs`（`EnforcementCycleCommitted` 独立事实，
   ENFORCER-150 第二种形态；`BlogEntryCommitted` 扩展与 clean break 未做）。接线经普通
   review + security_review 双审查（无 blocking）；security_review 四项观察（跨进程幂等
   竞态——不可达论证、合成消息来源标记、blob 大小上限、诊断内部路径）记于
   `STATUS/blockers/README.md`
2. 逐纵向接线（推荐顺序不变）：Strength shadow（Replica session/ruleset/候选帧，解锁
   STRENGTH-078 C-11…C-21）→ Enforcer nudge overlay（ENFORCER-080…115，第 0 步 7–9
   补完）→ Student&Teacher（teacher/return 工具、QA 落盘，LEARN-082…088）
3. 包 K8f：X-A–X-D 剧本（X 恢复链生产接线已闭合；剧本未建，第 4 层证据未产出）
4. `HandleProjection.joinable` 零生产调用点：`join` 仍走运行期 mailbox，
   `CompletedAwaitingJoin` 的 durable 消费链未闭合（EXEC-009）
5. `CompanionDelta.jsonDelta` 替换为包 X3 的 TOML delta（当前仍在 Submit 路径）

## 事实入口

- 正式规范：`SSOT/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`

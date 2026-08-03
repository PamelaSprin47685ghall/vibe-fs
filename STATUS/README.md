# Current Repository Status

## 当前基线

- 分支：`master`
- 收敛目标：`0.5.2`（开发中，尚未 tag）
- 最后验证：`test:release` 全绿（gate:static → build → unit → harness → P0×3，canary 18×3）
- 证据：`docs/evidence/0.5.2-baseline/` 与生成中的 `docs/evidence/0.5.2/`
- 合规表：[`STATUS/conformance.md`](STATUS/conformance.md)（由 `STATUS/conformance.toml` 生成）

## 当前产品状态

0.5.2 收敛中。目标：Active SSOT 全 CONFORMANT，未来设计迁出到 `RFC/`，状态唯一源为 `STATUS/conformance.toml`。

0.5.1 已发布（tag `v0.5.1`）：闭合 SSOT/15 Blogger 请求形状 / 挂起 / Squash / crash recovery 纵向链：
唯一 coordinator、typed materialize、blog-tool Squash、KnownCommitted 才 Park、
crash-window 不 stomp live CurrentRequest。canary 证据见 `host-transform-capability`
与 `companion-canary`；发布证据目录 `docs/evidence/0.5.1/`。

LifecycleWorkRecord 迁移已完成（方案 `STATUS/lifecycle-work-record.md`）：父→子与子→父统一为
LWR（Opening + Y frames + X gap + Terminal）；A/B 双轨、FinalText、Seed、TerminalSessionA、
FrozenB 全部废止；Y normal delta data-only、Blogger delta 稀疏 schema、TOML data body 单 LF、
join 最小 wire（status/agent/work_record）。

## 当前开发阶段

0.5.2 收敛：按 `STATUS/0.5.2-convergence.md` 执行 C0–C15。

SSOT/14 Strength、SSOT/16 Student&Teacher 与 ENFORCER nudge/throttle 已迁出 Active
SSOT 至 `RFC/`，不属于 0.5.2 产品合同。Active 子集为 SSOT/01–13 + SSOT/15 Blogger 工具化。

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

见 `STATUS/blockers/README.md`。HOST-006 次生风险（运行时探测）已闭合：探测已实现并
接线（`HostSignalBootstrap.onSnapshot` → `HostCompactionGate.judgeStartup` →
`PluginRuntimeScope.TryClaimStartupProbe`）。无未闭合 blocker。

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

1. C3：版本与状态真值（进行中）
2. C4：删除旧 Companion/Blogger 旁路入口
3. C5：建立唯一 ManagedAgentCatalog
4. C6：durable join（EXEC-009）
5. C7：durable effects（PERSIST-009）
6. C8：ARCH-001 与 Orchestrator stage-like 语义清理
7. C9：Host/Review/Cold boundary 证据
8. C10：Context recovery X-A–X-D 第四层证据
9. C11：测试与 clause coverage 补齐
10. C12：运行环境与发布包
11. C13：全仓反向审计
12. C14：RC 验证
13. C15：0.5.2 发布

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
4. `HandleProjection.joinable` 零生产调用点：`join` 仍走运行期 mailbox，
   `CompletedAwaitingJoin` 的 durable 消费链未闭合（EXEC-009）
5. `CompanionDelta.jsonDelta` 替换为包 X3 的 TOML delta（当前仍在 Submit 路径）

## 事实入口

- 正式规范：`SSOT/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`

# Current Repository Status

## 0.5.3 正规化进度

- 已完成：enforcer JSON（resources/enforcer/catalog.json）、prompts → resources/prompts、单 package manifest、gitignore/根白名单、宝典 → docs/decisions/kolmogorov.md、Directory.Build.props 单一化、scripts 公共命令面收敛、RFC → docs/rfcs、spec 骨架（README + coverage.toml）
- 未做：SSOT 全量改名、STATUS 删除、tests 树合并、Wanxiangshu 改名、dist 输出、README 终稿

## 当前基线

- 分支：`master`
- 版本：`0.5.2` 已发布（tag `v0.5.2`）
- 最终验证 commit：`ddd504ca`
- evidence commit：`de8db893`
- tag tip：随发布锚点对齐更新（`git rev-parse v0.5.2^{commit}` 与 HEAD 一致；仅含
  evidence 提交，生产+testkit 与验证 tip `ddd504ca` 一致）
- 最后验证：
  - `npm run gate:static` 通过（10 子门禁，含 `gate:conformance`）
  - `npm run build` 通过（Fable 编译 212 源单元 → `dist`）
  - `npm run test:unit` 通过（737 / 737）
  - `npm run test:harness` 通过（285 / 285）
  - `npm run test:e2e:p0:three` 通过（19 canary × 3 轮 = 57/57）
  - `npm pack` 产出 `wanxiangshu-0.5.2.tgz`（298 files, 1.6 MB packed）
  - 隔离 install/import 通过
- 证据：`docs/evidence/0.5.2/`（ENV / COMMIT / STATIC / BUILD / UNIT / HARNESS /
  CANARY-3ROUND / PACKAGE-CONTENTS / INSTALL / IMPORT / TARBALL.sha256 等）
- 合规表：[`STATUS/conformance.md`](STATUS/conformance.md)
  （由 `STATUS/conformance.toml` 生成；Active 192/192 CONFORMANT，0 IMPLEMENTING）

## 当前产品状态

0.5.2 已发布。Active SSOT 全部 conformant。当前无活跃 blocker。未来功能见 `docs/rfcs/`，
不属于当前产品合同。

0.5.2 收敛（C0–C15）已闭合。关键交付：

- C0：建立 0.5.2 baseline，跑通 `test:release` 并记录证据。
- C1：把 Strength / StudentTeacher / Enforcer nudge throttle 迁出 Active SSOT 到 `RFC/`，
  `spec/00.md` 拆分 Active 与 RFC 索引，`spec/15.md` 明确为 Blogger 工具化 Active 子集。
- C2：建立 `STATUS/conformance.toml` 与 `scripts/conformance-gen.mjs` / `conformance-gate.mjs`，
  生成 Active 条款机器账本。
- C3：版本与状态真值对齐到 `0.5.2`（package.json、packaging template、README、CHANGELOG）。
- C4：删除 legacy Companion/Blogger 旁路入口（`Companion.Submit`、`SubmitProjection`、
  `StartMainFromContext`、`startMainFromContext`、`blog` 函数、`AppendSquash` / `squash-legacy`）。
- C5：建立唯一 `ManagedAgentCatalog`：消费
  `PromptAuthority`/`ManagedAgent`/`ManagedAgentConfig` 三处重复角色/peer/legacy name，
  统一版本无关拒绝文案，新增 `scripts/role-matrix-gate.mjs`。
- C6：durable join（`EXEC-009`）：`HandleCompleted` 携带 durable completion blob；
  `HostForkRuntime.Join` 投影优先消费（`HandleProjection.joinable` + CAS 退休），
  mailbox 降级为通知。
- C7：durable effects（`PERSIST-009`）：typed worktree / publish / prompt / blogger
  effect 映射；删除零调用点 `DurableEffect` 旁路。
- C8：ARCH-001 最小诚实范围——`JobProgress` 为纯业务事实 DU；`architecture-gate`
  禁止 `CurrentStage`/`StepIndex` 等程序计数器令牌。
- C9–C13：layer-2 证据、反向审计、Node/pack 矩阵与 evidence 账本。
- C14：P0×3 全绿（54/54）；Blogger park/resume、epoch 声明、PERSIST-010 CAS、
  ARCH-010 harness 字段名等 RC 阻塞已闭合。
- C15：tag `v0.5.2` 创建；evidence 与 tag/commit 对齐；工作树 clean。

0.5.1 已发布（tag `v0.5.1`）：闭合 spec/15 Blogger 请求形状 / 挂起 / Squash / crash recovery 纵向链。
发布证据目录 `docs/evidence/0.5.1/`。

LifecycleWorkRecord 迁移已完成（方案 `STATUS/lifecycle-work-record.md`）：父→子与子→父统一为
LWR（Y frames + X gap + Terminal；Opening 由 `includeOpening` 控制）；A/B 双轨、FinalText、
Seed、TerminalSessionA、FrozenB 全部废止。

本轮补充合同：
- tool call/result 可进 XTrace 作 Y 压缩源；LWR gap/terminal 禁止 raw tool（`forWorkRecord`）
- 父→子 `includeOpening=true`；子→父 join `includeOpening=false`（布置者已知任务）
- 自定义 tool result 经 `ToolResultBound` 抢先留尾截断（34B marker + ≤1998 行 / ≤51166B），
  使 Host 默认 2000 行 / 50 KiB head 截断 no-op（ARCH-010-TOOL-BOUND）

## 当前开发阶段

0.5.2 已发布。Active 子集为 spec/01–13 + spec/15 Blogger 工具化 + spec/17 LOOP
（退化循环检测与强杀）。spec/14 Strength、spec/16 Student&Teacher 与 ENFORCER
nudge/throttle 在 `docs/rfcs/`，不属于当前产品合同。

### PrefixCoverage 与 prefix-probe（已闭合）

`commitCycle` 以 staged `BloggerMainRequestContext` 为唯一 coverage 源；cutoff 前进时
对 projection 前缀做 `renderSemantic` 哈希；fallback 剧本使用 `tool-call blog` 并声明
`frame-commit` / `prefix-probe` 冷边界。RC 验证已纳入 C14 全绿。

## 活跃阻塞

见 `STATUS/blockers/README.md`。当前无活跃 blocker。

## 已知说明（非发布阻塞）

以下不阻挡 0.5.2 已发布状态，仅作后续可选加强：

- X 恢复链：生产接线已闭合；X-A–X-D layer-4 canary 已交付（`x-recovery-canary.mjs` 四场景）
- `PERSIST-009` worktree 路径无独立 fault-injection canary（依赖 fold 单测 + publish canary）
- HOST-010：可观测代理 = Reviewer 链 seal/verdict 等式（`reviewer-verdict-canary`）+ X 链 `SolvingProviderRun`（`x-recovery-canary`）；HOST-011：ToolCallIds + BlogEntryCommitted.ProviderRun（`host-transform-capability-canary`）
- 未来功能（Strength shadow / Enforcer nudge / Student&Teacher）见 `docs/rfcs/`

## 源码地图

生产源码唯一根：`src/Wanxiangshu/`（`Wanxiangshu.fsproj` 编译全部）。三口径：

- 208 生产 `.fs` 源文件（`find … -name '*.fs'`）
- 209 gate source files（208 `.fs` + 1 `.fsproj`；`gate:layout` / `gate:architecture` 的 `SOURCE_EXTENSIONS`）
- 212 Fable 编译单元（project + references；含工程外引用，≠ 生产 `.fs` 数）

```text
src/Wanxiangshu/
├── Kernel/                       领域内核：身份、角色、Flow、事实、结果
├── Domain/                       纯领域：PromptAuthority/Review/Recovery/Projection/Enforcer
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
└── Wanxiangshu.fsproj
```

布局纪律由 `scripts/repository-layout-gate.mjs`（gate:static 第一段）机器验证：
根目录白名单、生产源码唯一根、顶层 module 与文件名一致、重复源码探测。分发产物契约：
Fable 输出 `dist/`；根 `package.json` 为唯一 manifest（`main` →
`dist/Infrastructure/OpenCode/Plugin/Plugin.js`，`files` → `dist` + `resources`）；
从仓库根执行 `npm pack`，无第二 package root / postbuild staging。

## 下一步

0.5.2 已发布。Active SSOT 全部 conformant。当前无活跃 blocker。未来功能见 `docs/rfcs/`，
不属于当前产品合同。

## 事实入口

- 正式规范：`spec/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`

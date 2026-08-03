# Current Repository Status

## 当前基线

- 分支：`master`
- 版本：`0.5.2` 已发布（tag `v0.5.2`）
- 最终验证 commit：`17358c38`
- evidence commit：`7443007a`
- tag commit：`a6668d10`
- 最后验证：
  - `npm run gate:static` 通过（10 子门禁，含 `gate:conformance`）
  - `npm run build` 通过（Fable 编译 212 源单元 → `build/next`）
  - `npm run test:unit` 通过（737 / 737）
  - `npm run test:harness` 通过（285 / 285）
  - `npm run test:e2e:p0:three` 通过（18 canary × 3 轮 = 54/54）
  - `npm pack` 产出 `wanxiangshu-0.5.2.tgz`（298 files, 1.6 MB packed）
  - 隔离 install/import 通过
- 证据：`docs/evidence/0.5.2/`（ENV / COMMIT / STATIC / BUILD / UNIT / HARNESS /
  CANARY-3ROUND / PACKAGE-CONTENTS / INSTALL / IMPORT / TARBALL.sha256 等）
- 合规表：[`STATUS/conformance.md`](STATUS/conformance.md)
  （由 `STATUS/conformance.toml` 生成；Active 192/192 CONFORMANT，0 IMPLEMENTING）

## 当前产品状态

0.5.2 已发布。Active SSOT 全部 conformant。当前无活跃 blocker。未来功能见 `RFC/`，
不属于当前产品合同。

0.5.2 收敛（C0–C15）已闭合。关键交付：

- C0：建立 0.5.2 baseline，跑通 `test:release` 并记录证据。
- C1：把 Strength / StudentTeacher / Enforcer nudge throttle 迁出 Active SSOT 到 `RFC/`，
  `SSOT/00.md` 拆分 Active 与 RFC 索引，`SSOT/15.md` 明确为 Blogger 工具化 Active 子集。
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

0.5.1 已发布（tag `v0.5.1`）：闭合 SSOT/15 Blogger 请求形状 / 挂起 / Squash / crash recovery 纵向链。
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

0.5.2 已发布。Active 子集为 SSOT/01–13 + SSOT/15 Blogger 工具化 + SSOT/17 LOOP
（退化循环检测与强杀）。SSOT/14 Strength、SSOT/16 Student&Teacher 与 ENFORCER
nudge/throttle 在 `RFC/`，不属于当前产品合同。

### PrefixCoverage 与 prefix-probe（已闭合）

`commitCycle` 以 staged `BloggerMainRequestContext` 为唯一 coverage 源；cutoff 前进时
对 projection 前缀做 `renderSemantic` 哈希；fallback 剧本使用 `tool-call blog` 并声明
`frame-commit` / `prefix-probe` 冷边界。RC 验证已纳入 C14 全绿。

## 活跃阻塞

见 `STATUS/blockers/README.md`。当前无活跃 blocker。

## 已知说明（非发布阻塞）

以下不阻挡 0.5.2 已发布状态，仅作后续可选加强：

- X 恢复链：生产接线已闭合；X-A–X-D 独立 canary 剧本可作为后续加强（layer 1–3 与
  相关 canary 证据已在 0.5.2 账本内）
- `PERSIST-009` worktree 路径无独立 fault-injection canary（依赖 fold 单测 + publish canary）
- identity canary（transform id = tool messageID）可作为 Host 升级门禁可选加强项
- 未来功能（Strength shadow / Enforcer nudge / Student&Teacher）见 `RFC/`

## 源码地图

生产源码唯一根：`src/Wanxiangshu.Next/`（`Wanxiangshu.Next.fsproj` 编译全部；
**209 个生产 `.fs` 源文件**，`gate:layout` / `gate:architecture` 口径；Fable 解析
project+references 报告 212 编译单元，含工程外引用，不等于生产 `.fs` 文件数）。

```text
src/Wanxiangshu.Next/
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
└── Wanxiangshu.Next.fsproj
```

布局纪律由 `scripts/repository-layout-gate.mjs`（gate:static 第一段）机器验证：
根目录白名单、生产源码唯一根、顶层 module 与文件名一致、重复源码探测。分发产物契约
不变：Fable 输出 `build/next/`，npm 包 main 指向 `next/Infrastructure/OpenCode/Plugin/Plugin.js`
（模板 `packaging/npm-package.template.json`）。

## 下一步

0.5.2 已发布。Active SSOT 全部 conformant。当前无活跃 blocker。未来功能见 `RFC/`，
不属于当前产品合同。

## 事实入口

- 正式规范：`SSOT/`
- 当前合规：`STATUS/conformance.md`
- 历史归档：`docs/archive/shock-anneal-2026/`（FINAL-REPORT.md + 原始证据）
- 发布证据：`docs/evidence/`
- 版本历史：`CHANGELOG.md`

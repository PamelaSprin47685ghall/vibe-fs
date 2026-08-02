# 休克—退火迁移最终报告

## 0. 档案声明

| 项 | 值 |
|----|----|
| 归档日期 | 2026-08-02 |
| 归档 commit | `38cc1882bf3f9a1bf823008398d31579507e99b2` |
| 退火分支 | `refactor/ssot-shock-anneal` |
| 归档负责人 | 万象术工程（Wanxiangshu engineering） |
| 归档范围 | 休克—退火迁移全过程：阶段记录、工作包、设计裁决、缺陷调查、机器证据索引 |
| 性质声明 | **本报告是历史档案，不是规范。** 所有当前有效的规则以 `SSOT/` 为准；本报告不新增、不修改、不解释任何条款。规范性的表述一律引用 `SSOT/` 条款 ID，不在此重复定义 |

本档案完整归并了 `STATUS/shock-anneal.md` 及以下散件。原始机器证据原样保留在
`docs/archive/shock-anneal-2026/evidence/`，本报告只做索引与总结，不复制全文。

## 1. 执行摘要

万象术（Wanxiangshu）在 2026-07 至 2026-08 完成了从「以代码注释与过渡态维护为特征的旧实现」
到「以 `SSOT/` 条款为唯一产品规范的休克—退火迁移」。迁移全程以 `SSOT/01.md` 的三条架构 DNA
（ARCH-001 结构化程序替代状态机、ARCH-002 事件是信号不是数据、ARCH-003 不修改 OpenCode 本体）
为不可违反约束，并新增第四条同级硬禁止——上下文恢复必须由失败驱动（CTX-001 / CTX-002，
`SSOT/12`）。

迁移终点（退火三，2026-08-02）：canary 森林 16/16 全绿、P0×3 三轮全绿、`test:release`
（gate:static → build → unit → harness → P0×3）完整通过。生产代码全部迁移到 `SSOT/` 条款，
测试体系从 F# `tests-next` 整体迁移为直接消费 `build/next` 发布产物的 `tests-mjs`。

## 2. 背景与迁移动机

迁移前系统状态（基线证据 `STATUS/evidence/pre-shock/`，旧世界最后一次完整机器反馈）：

- 生产代码存在大量「过渡态维护」：旧类型（裸 `string` 承载 LogicalRunId / ProviderRunId /
  ToolCallId / GitTreeHash / ManagerId）与补字段、adapter、让旧测试继续通过的手法并存；
- 测试体系是 F# 测试 → Fable 编译 → 自制 Assert shim → 自制导出发现 runner → node，
  与生产走 `--precompiledLib` 链接，测的是另一次编译结果；
- 剧本森林（canary 测试）依赖 mock 嗅探请求形状、动态加载、substring 匹配，无法证明
  与生产语义一致；
- `STATUS/conformance.md` 存在「规范已合入但代码零实现」与绑定 commit 过期的问题。

迁移动机：把「靠注释维持的条款」变成「编译期事实」，让源码成为唯一不过期的规则说明。

### 0.4.0 发布历程（RC 序列）

| 版本 | 说明 |
|------|------|
| 0.4.0-rc.2 | 开发里程碑，Prompts/Authority 类型，后撤回 green claim |
| 0.4.0-rc.3 | 首个真实 RC，276 tests, 29 gate-testkit, 17 canaries ×3 |
| 0.4.0-rc.4 | 修复 Prompt Authority 问题，后被 rc.5 取代 |
| 0.4.0-rc.5 | 冻结版本，全角色 system prompt，session-wide A。277 tests |
| 0.4.0-rc.6 | ReviewConfirmation 关联修复。281 tests |
| 0.4.0-rc.7 | debounce + resolveForSession AABB。18 canaries ×3 |
| 0.4.0 最终 | rc.7 仅版本变化。281 tests, 29 gate-testkit, 18 canaries ×3 |

发布策略：默认私有交付（`private: true`, `license: SEE LICENSE IN LICENSE`），生成 tarball
但不公开发布到 npm；需要正式许可证和商业授权审查后才可改为公开。

### 0.4.x → 0.5.0 迁移（无自动迁移器）

破坏性变更：所有 Agent 需要显式 `fast-*` 或 `deep-*` 名称；`build`/`plan` alias 移除；
模型绑定只读 `opencode.json`；模型环境变量全部移除；不持久化/覆盖 model ID；Fallback
预算内循环 AABBAABB（Cursor 无限定义；自动恢复上限默认 12 连续失败）；不因 retry 次数
杀死 Logical Run；Blogger/Executor 为内部 fast/deep pair；Pre-0.5.0 journal 不支持。

迁移步骤：1 停止 OpenCode → 2 归档/删除旧 runtime journal → 3 删除全部模型环境变量 →
4 在 `opencode.json` 配置 20 个 Managed Agent → 5 将所有调用中的旧名称改为 `fast-*` 或
`deep-*` → 6 修改 Manager/Orchestrator 调用（必须传 Agent）→ 7 重新启动 →
8 检查 Config Gate + smoke test。

## 3. 迁移前基线

| 项 | 值 |
|----|----|
| 封炉基线 | `274a30aa` |
| 旧测试入口 | `tests-next/Wanxiangshu.Next.Tests.fsproj`（292 个测试，1 个失败） |
| 旧测试链路 | F# → Fable → Assert shim → 导出发现 runner → node |
| 旧生产入口 | `build/next/OpenCode/Plugin.js` |
| 验证层 | 第 0 层静态检查器（ssot-lint / shock-audit / architecture-gate）+ 第 1–2 层 F# 测试 |
| Host 版本 | OpenCode 1.18.9 |

## 4. 迁移目标与不变量

1. **单一写入口**（VERIFY-005）：每个领域恰好一个 writer；出现第二个 writer 是熔断条件。
2. **验证阶梯**（VERIFY-001/002）：六层验证不允许跨级；休克期只允许第 0 层反馈。
3. **测试语言边界**（VERIFY-008）：生产 `.fs`，第 1–3 层测试全部 `.mjs`，直接 import
   `build/next` 发布产物；语言边界物理性地阻止测试触碰实现内部。
4. **三条架构 DNA**（ARCH-001/002/003）+ **第四条**：上下文恢复必须由失败驱动
   （CTX-001 / CTX-002）。
5. **休克期纪律**：不运行编译与测试；未迁移调用点用 `failwith "SHOCK-UNMIGRATED[条款ID]"`
   显式标记，禁止裸 `TODO`；第一次编译前所有 `SHOCK-UNMIGRATED` 必须清零。

## 5. 时间线与提交边界

| 阶段 | 名称 | 完成条件 | 完成 commit |
|------|------|---------|------------|
| 0 | 封炉：冻结 SSOT、基线、迁移地图、验证层工装 | 静态检查 + 最后一次完整编译测试 | `274a30aa` |
| 1 | 休克一：领域内核与持久事实（包 0） | 关闭 | 包 0a–0e |
| 2 | 休克二：生产代码全部调用链（包 A–H） | 关闭 | — |
| 3 | 清场：删除旧语义与临时标记 | 静态检查 | `SHOCK-UNMIGRATED` = 0，八个单一写入口 ok(1) |
| 3.5 | SSOT/12 并入规范（`CTX-` 前缀 + 六个受影响文件） | ssot-lint | `c95429b3` |
| 4 | 退火一：恢复生产编译 | dotnet build → npm run build | Build succeeded；Fable 157 产物新鲜 |
| 5 | 失败驱动上下文恢复（包 X，X0–X9） | 编译 + 第 0–3 层 | 探针链接线 `c6ac0eb1…5ff3c53a`；BlogSquash 接线 `d5c49125` |
| 6 | 休克三 + 退火二：按条款写 `tests-mjs`，删除 `tests-next`（包 T） | test:mjs | 386 测试三时区全绿，证据 `evidence/post-anneal2/` |
| 6.5 | 剧本森林重建（包 K） | 载入期校验 + 森林自检 | ORCH-006 durable worktree 修复 `9fcaad24` |
| 6.6 | 因果推进门禁重建（包 W） | gate-testkit + test:mjs | W1–W7 |
| 6.7 | 运行时合成文本 TOML 记法（包 N，SSOT/13 → ARCH-010） | gate:static + canary | N0–N5b |
| 7 | 退火三：恢复 Host / E2E / Release | P0×3 + test:release | 修复链 `2a2660be`、`71763142` |

### 关键顺序决策

- **包 X 先于包 T**：包 T 要为 COMPANION-001…013 与 PROMPT-008 写测试，而这些条款的实现
  正是包 X 的产出。先写测试只能对着旧语义写，包 X 落地时同一批测试要整体重写——两次编写
  之间没有任何信息增益。
- **包 K 在包 T 之后、退火三之前**：剧本的 lane 划分与 step 序列反映迁移后的生产行为，
  先重写会锁定旧语义；而它必须早于任何 canary 运行。
- **包 X 依赖退火一而非退火二**：它新增大量类型与 fold 校验，需要编译反馈，但不需要既有
  mjs 套件先全绿。`domain.mjs` facade 必须先能加载（所有 mjs 测试的唯一入口）。

## 6. 阶段执行记录

### 6.1 封炉（阶段 0）

冻结 SSOT、建立迁移基线（`STATUS/evidence/pre-shock/`）、迁移地图与验证层工装。
封炉工装自证见 `STATUS/evidence/post-freeze/`（静态检查器能跑、能测出预期残留）。

封炉期已完成项：基线保存（prod/tests build 绿，test:next 290/3，gate-testkit 29/0）；
SSOT 矛盾修复（FALLBACK-005 循环无界 vs 预算有界、新增 FALLBACK-010、VERIFY-006 重写、
PROMPT-005 四事实 + Abandon reason、PROMPT-011 PromptKey 定义 + 恢复边界、ORCH-006 补
ManagerAgent/WorktreeIdentity/TargetBranchFrozen、ORCH-007 三分支固定顺序、VERIFY-003 改用
Semantic projection、VERIFY-007 单向有损关系、新增 HOST-010/HOST-011）；Host 能力证明
（REVIEW-010 seal→run 绑定可实现，证据 `evidence/host-transform-run-binding.md`）；悬空
引用清理（README 改指 `SSOT/00.md`）；行数门禁废除（删除
`Next_source_files_do_not_exceed_300_lines`，VERIFY-005 改为只阻断语义）；测试语言边界
确立（新增 VERIFY-008）；Fable 边界实证（JS 对象字面量可作 F# record 参数，同时实证
`DateTimeOffset` 裸 `new Date()` 会让 `isExpired` 反向错误且静默）；静态检查工具
（`scripts/ssot-lint.mjs`、`scripts/shock-audit.mjs`、`scripts/repo-scan.mjs`）。

封炉期工装：tag `ssot-freeze-0.5.0` 指向 `0e2e4239`；`scripts/architecture-gate.mjs` 12 个
门禁全部迁出测试套件，新增 `fsproj-drift`（首次运行发现 6 个死文件并删除，其中 5 个是
`c3c35756` 起就未运行的 Prompt Authority 测试，15 个断言登记入包 T）；`tests-mjs/domain.mjs`
38 导出封死三个静默陷阱；`tests-mjs/domain.meta.test.mjs` 20 测试全绿；
`tests-mjs/runner.mjs` 陈旧产物 fail closed + 每测试 1000ms 硬超时 + 300s 套件上限。

### 6.2 休克一：领域内核与持久事实（包 0）

- **包 0**：Identity 与基础类型。26 个 typed identity + 2 个复合身份
  （`FallbackAttemptIdentity` `ReviewAttemptIdentity`）。覆盖 PROMPT-001/002/005/008、
  FALLBACK-002/004/005、REVIEW-003/004/006/008、EXEC-009、ORCH-006/007/008、
  HOST-010/011、ARCH-006。命名统一：`ProviderAttemptIdentity` = `ProviderRunIdentity`
  （依据 `evidence/host-transform-run-binding.md`：一条 Host assistant message = 一次
  provider request = 一次 attempt）。
- **包 0b**：Journal 事实集。重构事实定义，使业务事实只从 SDK API 读完整 snapshot
  （ARCH-002）。
- **包 0c**：Journal 投影与 Fold。投影查询不扫描完整历史，必须 O(1) 积分状态（PERSIST-008）。
- **包 0d**：`AttemptExecutionProfile` 与两种 Provider Projection（`ProviderWireProjection`
  含 ID 字节相等、`ProviderSemanticProjection` 去 ID 语义相等）。
- **包 0e**：单一 Host 摘要适配器与读侧修正。

### 6.3 休克二：生产调用链（包 A–H）

| 包 | 内容 | 关键点 |
|----|------|--------|
| A | PromptDispatcher | PROMPT-005 四阶段协议：唯一 writer（`PromptDispatcher` 的三个 send 成员 + `sendFirstPrompt` 是插件文本到达 provider 的唯一通路） |
| B | AttemptExecutionProfile | PROMPT-008：角色由 `AttemptExecutionProfile` 唯一决定；旧「buildAttemptExecutionProfile 零调用点」在包 X8 才拿到第一个真实调用点 |
| C | FallbackController | FALLBACK-003 唯一 writer：`FallbackCursorAdvanced` / `FallbackExhausted` 只由 `FallbackController` 产生 |
| D | Review | REVIEW-006：Review confirmed 只能从 witness 派生，不能赋值 |
| E | Companion | COMPANION 系列；`CompanionDelta.jsonDelta` 仍在 Submit 路径（包 X3 的 TOML delta 与三级 chunker 已实现但未接线） |
| F | Execution / Handle | EXEC-009：Handle 持久化 + tombstone |
| G | Orchestrator | ORCH-003/006/007：durable worktree 只在终态显式释放 |
| H | Plugin Composition Root | HOST hook 面（`experimental.chat.messages.transform`、`experimental.session.compacting`、`experimental.compaction.autocontinue`）经 `PluginHostInterop.fs` 的 `curriedHook` / `pairedHook` emit 助手 |

### 6.4 清场（阶段 3）

删除旧语义与临时标记。`SHOCK-UNMIGRATED` 清零、八个单一写入口全部 ok(1)。
后续 X4 仍有一处 PERSIST-009 阻断。

### 6.5 退火一：恢复生产编译（阶段 4）

恢复 `dotnet build next/` 与 `npm run build`（Fable 157 产物新鲜）。

### 6.6 上下文恢复（阶段 5，包 X）

失败驱动上下文恢复。核心形态（已被 CTX-001/002 判死的旧形态，均已在包 X9 删除）：

| 旧形态 | 违反 | 替代 |
|--------|------|------|
| `estimateTokens` / `estimateTokensUtf8` | CTX-001 | 无。不估算 |
| `shouldSwitchEpoch`（估算值 vs contextLimit） | CTX-001 + CTX-002 | 探针被 Host 接受后提交（CTX-012） |
| `bloggerSelfRebaseDue`（0.8 预算阈值） | CTX-001 + CTX-002 | 恢复槽内 squash（CTX-006） |
| `CompanionBudgetStore` / `BudgetFacts` | CTX-001 | 无。不存容量 |
| `CompanionHost.TransformRaw` 里的 epoch 注入 | CTX-002 | `AttemptPlanner.plan`（失败后） |
| `CompanionProgram.shouldReplacePrefix` | CTX-001 | `PrefixProbeSelection` |

接线状态（包 X10 前）：X-wire 探针链已接线（`SpikePlugin.transform → XWire.applyTransform`，
`HostSignalBootstrap.onTurn → ArmRecovery + reconcileAttempt`）；BlogSquash 生产链已接线
（`AppendSquash` 唯一构造点 + armed 槽触发）。剩余缺口：X 恢复链零生产调用点
（`XPrefixProjection`、`AttemptPlanner`、`PrefixProbeSelection` 及其三个事实皆无 writer），
第 1 层测试已存在，接线属包 X10；包 K8f 的 X-A–X-D 剧本因此阻断。

### 6.7 退火二：测试语言迁移（阶段 6，包 T）

从 F# 测试迁移为 mjs：

```text
迁移前：F# 测试 → Fable 编译 → 自制 Assert shim → 自制导出发现 runner → node
迁移后：mjs → node:test
```

连带删除：`tests-next/Wanxiangshu.Next.Tests.fsproj`、`tests-next/Assert.fs`（手写 xunit
替身）、`npm run test:compile`、`build/tests-next`、runner 的 Fable 导出发现逻辑。
386 测试三时区全绿（证据 `evidence/post-anneal2/`）。

测试布局：`tests-mjs/runner.mjs`（父层，陈旧产物 fail closed + 判据静默窗口监督）、
`tests-mjs/domain.mjs`（唯一允许知道 Fable 输出形状的文件）、`tests-mjs/<Domain>/*.test.mjs`
（按条款命名）。

### 6.8 剧本森林重建（阶段 6.5，包 K）

设计定稿 `STATUS/design-script-forest.md`。关键裁决：

- 一个 scenario 恰好一个 TOML 文件，Host 启动前一次性静态加载；禁止运行期换剧本；
- 运行时键四分量皆为请求的纯函数，最长前缀唯一命中；禁止 specificity 打分、子串长度、
  路径下标消歧；
- 书写形式是对话（TOML），前缀索引是编译产物；作者不写前缀数组；
- 死边检查在载入期计算可达性，不动点而非单遍——fork 链是真实的；
- 故障与内容正交，允许计数（物理投递次数真实可数）；重试必须重选同一条内容边；
- 冷边界必须由 scenario 显式声明，禁止 mock 嗅探；
- 四条 wire 实测纠正：session 身份在 `x-session-affinity` header 不在 body；别名到 session
  是一对多；`kind` 必须扫描全部前置消息；故障与冷边界必须按 `entryId` 索引不能按文本。

### 6.9 Synthetic TOML（阶段 6.7，包 N）

`SSOT/13` → `ARCH-010`（`SSOT/01.md`）：运行时 LLM 可见合成内容的 TOML Instruction/Data
记法。17 条禁止实现（`STATUS/design-synthetic-toml.md`）。`gate:surface`
（`scripts/surface-inventory.mjs`）由 sink 侧派生清单：`PromptDispatcher` 的三个 send 成员
加 `sendFirstPrompt` 是插件文本到达 provider 的唯一通路，故 sink 是可枚举的闭集。
一处实质修订：多行 delimiter 由 `"""` 改为 `'''`。

### 6.10 Orchestrator 恢复（阶段 7 前）

`orchestrator-recovery-puzzle.md` 调查结案（2026-08-02，退火三完成）：worktree 删除调查、
guard 轮循环镜像树与 rebase 后当前树失配、家族 session seal worktree 释放后 system prompt
丢 AGENTS.md 块。结案后三个红灯修复链（见 6.11）。

### 6.11 退火三与最终收口（阶段 7）

P0 16/16、P0×3 三轮全绿、`test:release` 完整通过。三个红灯修复链：

1. `reviewer-restart` 并发红：插件构造期 `PromptRecovery.reconcile` 经 SDK 重入未就绪 Host
   → 改为 post-init single-flight `RecoveryGate`（`2a2660be`）；
2. `orchestrator-publish` seal-undeclared：guard 轮 continuation 的 `turn.Directory` = worktree，
   worktree 释放后 instruction 丢失 → `liveDirectory` 回退 root + `TurnInProgress` 在 job
   离开 `ManagerStarted` 后直接完成 manager（`71763142`）；
3. teardown 端口泄漏 flake：`terminateChild` 在 `terminateTree` 报 survivors 后补一次
   SIGKILL（非调大超时，`71763142`）。

## 7. 关键设计裁决

### SSOT 例外协议（已触发 2 次）

| # | 条款 | 日期 | commit | blocker | 变更 |
|---|------|------|--------|---------|------|
| 1 | HOST-006 | 2026-07-30 | `cd1f8f09` | `STATUS/blocker-HOST-006.md` | 单层「全部禁止」改为预防层 + 收容层；manual `/compact` 成为官方支持用法，效果 best effort；新增 `compaction prune` 到必须关闭清单；启动门禁从静态配置读取升级为运行时探测；新增持久事实 `ContextReanchored`（PERSIST-010） |
| 2 | ARCH-009（新增） | 2026-07-30 | 本次 | 无（不是矛盾，是规范缺失） | 新增条款：业务层并发只允许有界 map；`maxConcurrency` 必须为正且拒绝非正值；结果按输入位置排列；取消在获取许可处观察且 token 传达到 action；拒绝不取消 siblings，许可必须在失败时归还 |

例外 1 的判据是逻辑矛盾，不是实现困难。例外 2 是反向缺口：不是条款不可实现，是条款
不存在——包 T-5e 写 `Parallel.mapBounded` 的第 1 层测试时发现这个跨领域共享原语的行为契约
真实存在而 `SSOT/` 没有任何条款管它。ARCH-009 的文字先于测试改名写定，而不是把已有测试的
行为抄成条款。「拒绝不取消 siblings」写进条款的判据：这条行为决定调用方的正确写法，规范
必须表达它，否则它只被一个测试锁住，而测试不是规范。`Promise.all` 用于实现有界原语本身
不受 ARCH-009 约束——条款禁止的是业务层直接无界扇出。

### 常规裁决
| 测试改 `.mjs`，生产保持 `.fs` | VERIFY-008 |
| 行数门禁废除，Gate 只阻断语义 | VERIFY-005 |
| Architecture Gate 是第 0 层静态检查器 | VERIFY-001、VERIFY-005 |
| REVIEW-010 seal→run 绑定可实现，不需 SSOT 例外 | HOST-010、`evidence/host-transform-run-binding.md` |
| 剧本森林为静态 TOML，禁运行期加载 | VERIFY-003、`design-script-forest.md` |
| Fallback：Offset 循环无界，自动恢复预算有界 | FALLBACK-005 |
| Host `Attempt` 与 `ConsecutiveFailureCount` 是不同的量 | FALLBACK-010 |
| durable worktree 只在终态显式释放；`NeedsReview` 不释放 | ORCH-003、ORCH-006、ORCH-007 |
| fork 首 prompt 一律 ARCH-010 信封，continuation 一律原样 | PROMPT-008、N3（`783caf3b`） |
| 已确认 barrier 的多余 PERFECT 不重开挑战 | REVIEW-003（`783caf3b`） |
| `ProviderAttemptIdentity` = `ProviderRunIdentity` 单一类型 | HOST-010、`evidence/host-transform-run-binding.md` |
| `CompanionDelta.jsonDelta` 仍在 Submit 路径（TOML delta 未接线） | COMPANION（已知未闭合项，非缺陷） |
| `CompanionHost.TransformRaw` 只做累积原样返回是 CTX-002 正确形态 | CTX-002（恢复决策只能在 `AttemptPlanner.plan` 里做） |

### 上下文恢复的设计演化（来自 `design-context-recovery.md`）

- **AXIOM-CTX-001**：不观察上下文容量。禁止读 provider 的 context/input/output limit、
  做 token 估算、拿估算值与阈值比较。
- **AXIOM-CTX-002**：不主动预测溢出。所有恢复动作的前置条件是一次真实失败的 attempt。
- **AXIOM-X-001**：X 不发压缩请求；X 替换首先是 probe（AXIOM-X-002）；probe 成功是经验
  判据（AXIOM-X-003）。
- **AXIOM-Y-001**：Y 使用永久 squash；所有工作 Session 都有 Y（AXIOM-COMPANION-001）；
  Y 是叶子（AXIOM-COMPANION-002）。
- **AXIOM-HOST-001**：全局关闭 Host compaction（HOST-006 预防层）。
- 方案有效性评估：真正解决的问题（2.1）、删除模型特判（2.2）、X 压缩零额外往返（2.3）、
  probe 避免错误永久化（2.4）、统一 Companion 降低分支数量（2.5）、已接受的代价（2.6）。

### Host compaction（blocker-HOST-006 裁决）

SSOT 例外协议第 1 次触发（2026-07-30）。绑定 Host 1.18.9、仓库 commit `cd1f8f09`。
完整源码证据 `evidence/host-context-recovery.md` 第 6–10 项。四类 compaction 中三类可关闭、
一类不可：manual compaction（`POST /session/:sessionID/summarize`）全程无 hook、无配置查询
（`groups/session.ts:303-315` → `handlers/session.ts:273-293` → `prompt.ts:1149-1159` →
`compaction.ts:513-536`）；执行期唯一 hook `experimental.session.compacting` 无法否决（输出
类型无 `enabled`/`cancel` 字段，返回值被丢弃）。已确认不存在的替代路径：无 per-session
compaction 配置、无「拒绝任务」类 hook。

两层解法（已入 SSOT/07 HOST-006 + PERSIST-010）：
- **预防层**：关掉 auto/overflow/autocontinue/prune；写不进配置则启动失败；
- **收容层**：任何观察到的 compaction 转成一次 `ContextReanchored` 重锚（永远 armed，
  幂等，不分类来源）。

**未解决的次生风险**（归档时仍开放）：`packages/core/src/session/runner/llm.ts:215` 调用
`compaction.compactIfNeeded`，该实现从外发请求估算（`packages/core/src/session/compaction.ts:
225-236`），配置来自 config 文档，完全没有插件 hook；接入 `location-services.ts:78` 但在
server 中未找到驱动它的 HTTP 路由，无法从源码判定它在 1.18.9 是否可达。处置：预防层的
启动门禁不得只依赖静态源码结论，必须包含一次运行时探测——首个 managed session 第一轮请求
完成后，该 session 的 compaction pseudo-run 数为 0（第一轮必然远低于任何阈值，此时出现
pseudo-run 只能说明存在不受 `compaction.auto` 控制的第二实现）。

## 8. 重大缺陷与根因

| 缺陷 | 根因 | 修复 |
|------|------|------|
| Fable `Task.CompletedTask` 编译成对 `get_CompletedTask` 的引用而 Fable 不导出该 getter，`build/next/OpenCode/Plugin.js` import 即抛错，插件根本加载不了 | Fable 语义在 `dotnet build` 下完全不可见 | 用 `next/Kernel/AsyncSupport.fs` 的 `completedTask()` |
| `[<Emit>]` 模板与 Fable 实际生成元数不匹配，三个 Host hook 在每次调用时抛异常 | 多参函数在 Fable 输出里可能是柯里化链也可能是单个多元箭头 | `PluginHostInterop.fs` 的 `curriedHook` / `pairedHook` |
| `parentSession` 双重死代码：唯一数据源是 provider 从不接收的 `__testkitHeaders`，比较经 `sessionBindings` 解析一个从未绑定的别名 | 有读取无数据 + 有写入无读取叠加 | 与 `__testkitHeaders` 同批退役 |
| seal 屏障在 `ScenarioRuntime` 路径上完全不通电：session 身份在 `x-session-affinity` header 不在 body，按 body 取 id 恒得 `undefined` | wire 形状误判 | K 包按 header 取身份 |
| 别名到 session 一对一建表，第二个子会话被 `try/catch` 静默吞掉 | 一对多关系误判 | 映射改为别名 → session 集合 |
| gate-testkit 行为用例「全部绿但 watchdog 从未续期」：spawn 时装一次、之后从不续期与正确接线得出同一结论 | 区分性输入不合法（用例在静默窗口内跑完） | 区分性输入必须是合法地比窗口更慢的工作（5 × 800ms vs 3000ms 窗口） |
| `buildAttemptExecutionProfile` 零调用点长期假装合规（PROMPT-008 标 CONTRADICTS 存活到包 X8） | 门禁只查「没有旁路者」不查「存在调用者」 | `architecture-gate.mjs` 双向检查 |
| conformance.md 漂移：包 C/T-3/T-5 完成后未回写状态表，Fallback 段七行、Review 段五行、VERIFY 段四行全部描述迁移前状态 | 「代码前进、状态表留在原地」的偏移没有任何机器会发现——`ssot-lint` 只检查条款 ID 与实现状态词的分离，不检查状态词是否真实 | 包 T-3/T-5 逐段更正并绑定到第 1 层测试或 `shock-audit` 实测；状态往乐观方向偏移更危险——标着 `CONTRADICTS` 的合规项只是噪音，标着 `CONFORMANT` 的违规项会让人跳过检查 |
| 门禁「零用例」与「全部通过」逐字相同 | 预先注册留空数组的用例文件 | W7 完备性门禁按禁止降级清单逐项要求命名用例 |

## 9. 被否决的方案和历史误判

- **包 X 排在包 T 之后**（原计划）：否决。先写测试只能对着旧语义写，包 X 落地时同一批
  测试要整体重写——两次编写之间没有任何信息增益。
- **上下文容量估算（`estimateTokens` / `shouldSwitchEpoch` / `bloggerSelfRebaseDue` /
  `CompanionBudgetStore`）**：全部否决并删除（包 X9）。违反 CTX-001/CTX-002。
- **双模型 ProviderAttemptIdentity / ProviderRunIdentity**：否决。两个类型会让「同一
  attempt 的两个身份是否相等」成为可提问但无意义的问题。
- **transform hook 里做恢复决策**：否决。transform 看不到 attempt 结局，恢复决策只能在
  `AttemptPlanner.plan` 里做（CTX-002 推论）。
- **`estimateTokens` 的替代**：无。不估算。
- **用「破坏内容幂等」表达失败（对 error 删除 seal 缓存）**：禁止。故障与内容正交。
- **为门禁而生的额外证据发射**：禁止。匹配子进程已有的计时行本身，不新增证据。

## 10. 架构变化前后对照

| 维度 | 迁移前 | 迁移后 |
|------|--------|--------|
| 身份 | 裸 `string` 承载 LogicalRunId / ProviderRunId / ToolCallId / GitTreeHash / ManagerId；单一 `MessageId` 同时表示物理消息与 Authority Root | 26 个 typed identity + 2 个复合身份；`TransportReceipt` 独立类型 |
| 状态机 | Stage/Phase/Lease/Owner/Generation 作为程序计数器 | 结构化程序替代状态机（ARCH-001）；控制流只用 `let!/do!/use!/match/尾递归` |
| 事件 | 碎片事件进入业务层 | 事件是信号不是数据（ARCH-002）；只有 `session.status=idle/retry`、`session.deleted` 能进入业务层 |
| 测试 | F# → Fable → Assert shim → node | mjs → node:test，直接消费 `build/next` 发布产物 |
| 剧本 | 动态加载 + 嗅探 + substring 匹配 | 静态 TOML + 前缀索引 + 载入期校验 |
| 上下文管理 | 估算容量、预算阈值 | 失败驱动恢复（CTX-001/002）；探针被接受后提交 |
| 写入口 | 多个 writer | 每个领域恰好一个 writer（VERIFY-005） |

## 11. 测试与验证体系变化

- **验证阶梯**（VERIFY-001）六层：0 静态检查 / 1 纯函数测试 / 2 资源契约测试 /
  3 Fake Host 轨迹 / 4 单 canary / 5 发布门禁（3 轮 × test:release）。VERIFY-002 不允许跨级。
- **测试语言**：mjs + node:test。铁律：禁止断言 DU tag 序数、Fable 命名约定；禁止只断言
  真值；禁止为测试可见性新增生产 export；新增契约面必须先开 facade 出口。
- **facade 封死的三个静默陷阱**（`domain.mjs` + `domain.meta.test.mjs`）：`new Date(iso)`
  无 `offset` 属性（Fable `compareDates` 反向错误）；JS 数组 `tail` 是 `undefined`
  （`List.fold` 返回种子，投影全空而断言全过）；union tag 是位置序数（中间插入新 case 后
  按序数构造静默造出另一个事实）。
- **时间界四条实测语义**（VERIFY-004）：`node:test` timeout 是判据线不是中止线；续期只能
  由判据事件驱动；watchdog 计时器必须 `unref`；「全部判据绿但子进程不肯退出」是失败。
- **门禁清单**：`gate:static`（ssot-lint + architecture-gate + docs + toml + budget +
  surface）、`gate:shock`（休克期专用）、`test:mjs`、`test:harness`、`test:e2e:p0`、
  `test:release`。`gate:budget` 禁止 ≥1000 的计时字面量（量级即语义线）；`gate:surface`
  由 sink 侧派生合成文本清单，双向检查。
- **投影分工**（VERIFY-007）：Seal 与前缀缓存用 `ProviderWireProjection`（含 ID，字节相等），
  剧本匹配与 Blogger delta 用 `ProviderSemanticProjection`（去 ID，语义相等）；两者是不同
  类型，不得隐式互转。

## 12. 旧语义和旧符号清理结果

旧符号灭绝表在 `STATUS/shock-anneal.md`（原文已归并本报告，明细不再复制）。要点：

- 生产与测试侧旧符号按作用域计数，全部灭绝（明细原在 `shock-anneal.md` 第 2277–2398 行）；
- 上下文恢复侧（包 X，X2 步测量；X9 步复测）；
- 剧本森林侧（包 K）；
- 单一写入口实测：八个单一写入口全部 ok(1)。

## 13. 最终机器验证

| 证据 | 位置 |
|------|------|
| 旧世界基线（最后完整反馈，含 292 测试 1 失败） | `evidence/pre-shock/` |
| 封炉工装自证（静态检查器能跑、能测出预期残留） | `evidence/post-freeze/` |
| 退火二完成（第 0–2 层反馈恢复，386 测试三时区全绿） | `evidence/post-anneal2/` |
| Host transform 与 run 绑定 | `evidence/host-transform-run-binding.md` |
| Host 上下文恢复（HOST-006 源码证据第 6–10 项） | `evidence/host-context-recovery.md` |
| K11 变异红绿 | `evidence/k11-mutation-red-green.md` |
| manager worktree durable ownership | `evidence/manager-worktree-durable-ownership.md` |
| orchestrator restart recovery 修复链 | `evidence/orchestrator-restart-recovery-fixes.md` |

退火三最终：P0 16/16、P0×3 三轮全绿、`test:release`（gate:static → build → unit → harness
→ P0×3）完整通过。

## 14. SSOT 变更索引

| 变更 | 位置 | 说明 |
|------|------|------|
| SSOT/12 并入（`CTX-` 前缀） | `SSOT/12.md` | 失败驱动上下文恢复；六个受影响文件 |
| HOST-006 重写 | `SSOT/07.md` | 预防层 + 收容层；`ContextReanchored`；例外协议 #1（`cd1f8f09`） |
| ARCH-009 新增 | `SSOT/01.md` | 有界并发与共享原语契约；例外协议 #2（规范缺失，非矛盾） |
| PERSIST-010 新增 | `SSOT/11.md` | 上下文恢复事实的 fold 规则 |
| SSOT/13 → ARCH-010 | `SSOT/01.md` | 运行时合成文本 TOML 记法（17 条禁止实现） |
| SSOT/14 | `SSOT/14.md` | Strength 纯领域内核（predictor/controller/value/policy/types，27 测试） |
| SSOT/15 | `SSOT/15.md` | Enforcer 纯领域内核（rule catalog/codec/throttle/nudge/cycle，39 测试） |
| SSOT/16 | `SSOT/16.md` | Student & Teacher 纯领域内核（request kinds/tool faces/tier/QA，14 测试） |
| 命名统一 | `SSOT/03.md` `SSOT/04.md` | `ProviderAttemptIdentity` → `ProviderRunIdentity` 4 处 |

SSOT/14-16 合入 commit `9b4e931d`。三个方案的纯领域内核已实现并测试（Strength 28 /
Enforcer 39 / StudentTeacher 15 项第 1 层测试；`93c421b7`、`dd1c0553`、`e52cf2be`）。
生产接线被各方案自设的 Host canary 门禁阻断（STRENGTH-078 / ENFORCER-180 /
LEARN-082…088），属后续阶段。

## 15. 已知限制与非目标

- **X 恢复链零生产调用点**（`XPrefixProjection`、`AttemptPlanner`、`PrefixProbeSelection`
  及其三个事实皆无 writer）：第 1 层测试已存在，接线属包 X10；包 K8f 的 X-A–X-D 剧本
  因此阻断——没有调用点的剧本只能证明 mock 自己。
- **`CompanionDelta.jsonDelta` 仍在 `Companion.Submit` 路径上**（`Companion.fs:104`、
  `CompanionProgram.fs:27`）：包 X3 的 TOML delta 与三级 chunker 已实现但未接线。
- **Host compaction 第二实现的运行时探测**：未完成（见 §7 HOST-006 次生风险）。
- **SSOT/14-16 生产接线**：被 Host canary 门禁阻断（STRENGTH-078 / ENFORCER-180 /
  LEARN-082…088）。推荐顺序：SatelliteRuntime → Projection DSL → Strength shadow →
  Enforcer → Student/Teacher。
- **非目标**：不修改 OpenCode 本体（ARCH-003）；不估算上下文容量（CTX-001）；不在失败前
  压缩（CTX-002）。

## 16. 退火后的当前基线

归档时（commit `38cc1882`）：

- 分支 `refactor/ssot-shock-anneal`；
- 当前产品状态：生产可用；canary 森林 16/16 全绿，`test:release` 完整通过；
- 当前开发阶段：SSOT/14-16 纯领域内核已合入，生产接线被 Host canary 门禁阻断——先建共享
  Host capability canary 证明 transform 挂起/取消/身份绑定，再逐纵向接线；
- 当前状态入口：`STATUS/README.md`；合规表 `STATUS/conformance.md`；
- 活跃阻塞：见 `STATUS/blockers/README.md`（归档时仅一项：HOST-006 次生风险——第二
  compaction 实现的运行时探测）。

## 17. 证据索引

全部原始机器证据已移入 `docs/archive/shock-anneal-2026/evidence/`，原样保留，未修改、
未复制全文。本报告只索引：

| 证据 | 内容 | 绑定 |
|------|------|------|
| `pre-shock/` | 旧世界最后一次完整反馈（build-production/build-tests/unit-baseline 44KB/environment） | commit 见 `pre-shock/COMMIT.txt` |
| `post-freeze/` | 封炉工装自证（ssot-lint/architecture-gate/shock-audit/test-next-after-gate-removal） | 基线 `274a30aa` |
| `post-anneal2/` | 退火二完成（test-mjs 91KB 完整输出/test-inventory/build-fable/module-load） | `2b30301c`，TZ=CST |
| `host-context-recovery.md` | HOST-006 源码证据（第 6–10 项）与上下文恢复设计 | HOST-006 |
| `host-transform-run-binding.md` | 一条 Host assistant message = 一次 provider request = 一次 attempt | HOST-010、REVIEW-004 |
| `k11-mutation-red-green.md` | K11 变异测试红绿记录 | 包 K |
| `manager-worktree-durable-ownership.md` | ORCH-006 durable worktree ownership 修复 | `9fcaad24` |
| `orchestrator-restart-recovery-fixes.md` | orchestrator-restart-publish 单 canary 3/3 修复链 | 退火三 |

## 18. 原文归并映射

| 原文件 | 最终报告位置 | 处理 |
|--------|-------------|------|
| `STATUS/00-current.md` | §16、`STATUS/README.md` | 归并：当前基线/阶段/已确立关键决定入 §16；「下一步」与「阅读顺序/反馈纪律」为当前状态职责，由重建的 `STATUS/README.md` 承接，未复制入报告 |
| `STATUS/shock-anneal.md` | §2–16 | 全量归并（阶段/工作包/熔断/SSOT 例外协议/封炉期记录）；旧符号灭绝明细（原 2277–2398 行）未保留全文——§12 只记要点，明细随原文删除 |
| `STATUS/design-context-recovery.md` | §7、§8、§9 | 推理与裁决归并 |
| `STATUS/design-script-forest.md` | §7、§11 | 归并（剧本森林设计裁决 + 验证体系变化） |
| `STATUS/design-synthetic-toml.md` | §7、§14 | 归并（ARCH-010 裁决 + SSOT 变更索引） |
| `STATUS/orchestrator-recovery-puzzle.md` | §6.10、§8 | 归并（结案 + 缺陷） |
| `STATUS/blocker-CTX-006.md` | §8 | 记录已解除过程（2026-07-31 解除；BlogSquash 生产链接线 d5c49125） |
| `STATUS/blocker-HOST-006.md` | §7、§15 | 记录裁决及剩余边界（次生风险提炼为当前 blocker，见 `STATUS/blockers/README.md`） |
| `STATUS/history-and-migration.md` | §2、§10 | 归并（0.4.0 发布历程 + 0.4→0.5 迁移） |
| `STATUS/plan-sidecars.md` | — | 整篇归档删除（用户裁决：Enforcer 已被 SSOT/15 取代；Prefetcher 无近期实施意图） |
| `STATUS/changelog.md` | — | 迁出 `STATUS/`，版本历史移至根目录 `CHANGELOG.md` |
| `STATUS/evidence/**` | §17 | 保留原始证据，不复制全文；原样移入 `docs/archive/shock-anneal-2026/evidence/` |

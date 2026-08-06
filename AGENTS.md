# AGENTS.md — 万象术工程纪律

## 0. Host 源代码位置（最重要的一条）

`../opencode` 是 OpenCode 的完整源代码仓库（若存在）。本机当前无该兄弟仓库；
Host 行为以发布二进制为准（见下），源码路径表保留为「源码可用时的地图」。

```text
../opencode                            ← Host 源码（若存在；本机当前缺失）
/Users/yuanxi/Workwork/vibe-fs-wt      ← 本仓库（插件）
```

任何关于 Host 行为的问题，先读源码，不要猜、不要只读 `.d.ts`、不要只做黑盒实验。
源码缺失时，用发布二进制的 bundled JS 交叉验证（见下）。

常用位置：

| 关注点 | 源码路径 |
|--------|---------|
| Plugin hook 类型定义 | `../opencode/packages/plugin/src/index.ts` |
| Tool context 类型 | `../opencode/packages/plugin/src/tool.ts` |
| Prompt 主循环（provider step、transform 触发点） | `../opencode/packages/opencode/src/session/prompt.ts` |
| Compaction | `../opencode/packages/opencode/src/session/compaction.ts` |
| 消息/Part 领域类型 | `../opencode/packages/opencode/src/session/` |
| SDK 生成类型 | `../opencode/packages/sdk/` |
| Server / HTTP API | `../opencode/packages/server/` |

`node_modules/@opencode-ai/plugin` 的 `.d.ts` 是发布产物，信息量少于源码。
典型例子：`experimental.chat.messages.transform` 的 `input` 类型是 `{}`，
看类型会得出"transform 时无任何身份可用"的错误结论；读 `prompt.ts` 才能发现
assistant message 在 transform 之前已经创建并持久化。

已发布版本二进制在 `~/.bun/install/global/node_modules/opencode-ai/bin/opencode.exe`
（`~/.bun/bin/opencode` 符号链接指向它；当前 1.18.13），
可用 `strings` 提取 bundled JS 交叉验证源码与实际运行版本是否一致。

判断 SSOT 条款"Host 能力不足"之前，必须先读源码。 `ARCH-003` 禁止修改 Host 本体，
但不禁止阅读它——恰恰相反，只有读过才能证明某个 Hook 组合确实不存在。

生产源码唯一根 `src/Wanxiangshu/`（220 个 `.fs`，`Wanxiangshu.fsproj` 编译全部；
`scripts/checks/architecture.mjs` 实证输出「220 文件」）。
布局纪律由 `scripts/checks/architecture.mjs`（`npm run lint` 第 2 步）机器验证：
`src/` 下唯一 F# 根、fsproj 每文件恰编译一次、无盘上未编译/已声明缺失文件、
Kernel/Domain 不引用上层命名空间与 `Fable.Core.JsInterop`、package resource 读取
仅在 `Infrastructure/Resources/`、无 `.gen.fs`、无旧路径词汇（`docs/evidence`、
`docs/archive`、`SSOT/`、`STATUS/`、`vibe-fs`、`testkit`、`Wanxiangshu.Next`）。
分发产物契约：Fable 输出 `dist/`，npm 包 main 指向
`dist/Infrastructure/OpenCode/Plugin/Plugin.js`。根 `package.json` 为唯一 manifest；
`files` = `dist` + `resources`；无 postbuild staging（0.5.3 起）。

---

## 1. 动手之前先读规范与状态

这条是工作顺序约束，不是建议。

```text
读条款 → 读状态 → 读代码 → 动手
```

反过来做的两种典型失败：

其一，写完才想起看文档。此时代码已经按旧语义定型，要么返工，要么把旧语义又固化一遍——后者更糟，因为它让规范与代码的偏离多一处，而且看起来像「完成了工作」。

其二，一头扎进代码细节，丢掉大局。症状被修好，条款仍被违反。典型形态：给旧类型补字段、加 adapter、让旧测试继续通过——每一步局部合理，合起来是在维护过渡态。

### 按任务类型的最小阅读集

| 任务涉及 | 必读条款 | 必读状态 |
|---------|---------|---------|
| Prompt 发送、Authority、Dispatcher | spec/03 | `tests/unit/prompt/*.test.mjs` + `tests/unit/context/attempt-plan.test.mjs` |
| Fallback、cursor、circuit breaker | spec/04 | `tests/unit/fallback/*.test.mjs` |
| Review、verdict、witness、seal | spec/05 + HOST-010/011 | `tests/unit/review/*.test.mjs` |
| Orchestrator、publish、rebase、恢复 | spec/06 | `tests/unit/orchestrator/*.test.mjs` + `tests/unit/execution/join-*.test.mjs` |
| Host hook、事件、reconcile | spec/07 | `tests/unit/plugin/host-hooks.test.mjs` + `tests/unit/execution/host-turn-observed.test.mjs` |
| Companion、Blogger、projection、epoch | spec/08 + spec/12 | `tests/unit/context/*.test.mjs` + `tests/unit/enforcer/*.test.mjs` |
| 上下文恢复、Blogger delta、X prefix probe、Y squash | spec/12（`CTX-`） | `tests/unit/context/`（`probe-selection` / `recovery-slot` / `x-trace*` / `blogger-*`）+ `tests/e2e/cases/context-recovery.test.mjs`（X-A–X-D） |
| compaction、`/compact`、reanchor | spec/07 + spec/12 | `tests/unit/context/host-compaction-policy.test.mjs` + `src/Wanxiangshu/Infrastructure/OpenCode/Host/HostCompactionGate.fs` |
| fork/join/list、PTY、进程 | spec/09 | `tests/unit/execution/`（`handle*` / `join*` / `fork-*` / `executor-*`） |
| 测试、门禁、canary 剧本 | spec/10 | `tests/e2e/` + `tests/unit/verify/` + `tests/integration/harness/` |
| Journal、事实、持久化 | spec/11 | `tests/unit/journal/*.test.mjs` + `tests/integration/journal/boot.test.mjs` |
| 运行时合成 TOML 记法 | spec/13（`ARCH-010`） | `tests/unit/context/synthetic-toml.test.mjs` + `tests/integration/harness/arch010-cases.mjs` |
| 结构化程序 DSL（FLOW-） | spec/14（`FLOW-`） | `tests/unit/verify/dsl-ownership.test.mjs` + `scripts/checks/dsl-ownership.mjs` + `TASK.md` |
| Projection Algebra（PROJ-） | spec/16（`PROJ-`） | `tests/unit/context/companion-projection.test.mjs` + `tests/unit/orchestrator/program.test.mjs` |
| LLM 退化循环检测与强杀恢复 | spec/17（`LOOP-`） | `tests/unit/domain/loop-*.test.mjs` |
| Strength / Student&Teacher（未来设计） | `docs/rfcs/`（`strength.md` / `student-teacher.md`） | `docs/rfcs/` 各文件 Status 字段 |
| 任何生产代码改动 | spec/01（架构 DNA） | `scripts/checks/architecture.mjs` + `scripts/checks/spec.mjs` |
| Host 行为存疑 | ARCH-003 | 读 `../opencode` 源码（见上一节） |

`spec/00.md` 是导航，条款速查表在那里。不确定读哪个文件时先读它。

### 提交前运行 lint

任何面向仓库的改动，在 `git commit` 前必须先跑 `npm run lint`。
该命令执行 `npm run format:check`（`dotnet tool run fantomas --check src/Wanxiangshu`）
再跑 `node scripts/check.mjs`（focused checks 串行：spec → architecture →
dsl-ownership → p0-recovery-join）。运行后再做 `git add`，可确保提交内容通过检查。
0.5.3 起仓库不再安装 pre-commit 钩子（无 `prepare` 钩子），由 CI/本地 lint 兜底。

`npm run lint` 也用于满足 Reasonix 编程器的 delivery work-mode 检查：
在交付阶段，该检查要求工作区无未格式化的 F# 与 XML 源文件；
若存在未格式化文件，`fantomas --check` 会失败，需先 `npm run format` 再提交。

### 迷路时向上走

在代码里陷住、或发现「怎么改都别扭」时，不要继续往下调。回到条款问三个问题：

```text
这个文件现在只讲一种语义吗？
这条修改是在实现条款，还是在维护过渡态？
这个字段是物理世界真实存在的事物，还是程序接下来去哪的信息？
这个字段真的载过数据吗——去量，不要读代码推理？
```

第三问来自 ARCH-001。后者一律删除。

第四问是本仓库反复吃亏的地方，量法见 §4 末尾。

### 规范与状态的唯一位置

| 位置 | 性质 |
|------|------|
| `spec/` | 唯一产品规范。条款 ID 寻址（`PROMPT-005` 等）。冲突时以此为准。`spec/00.md` 导航，`spec/99.md` 词汇表 |
| `scripts/checks/spec.mjs` | 规范内部一致性检查：条款唯一、无悬空引用、前缀归属（`PREFIX_OWNER` 硬编码表）、`spec/00.md` 导航完整。0.5.3 实证：209 条款 / 620 引用 / 19 文件 |
| `scripts/checks/architecture.mjs` | 源码根、fsproj 完整性、分层边界、资源读取位置、无 `.gen.fs`、无旧路径（VERIFY-005） |
| `docs/decisions/kolmogorov.md` | Kolmogorov 宝典唯一权威副本（工程铁律与结对输出纪律） |
| `docs/decisions/enforcer-catalog.md` | Enforcer 规则目录裁决（0.5.3：规则实例 = 打包资源 `resources/enforcer/catalog.json`，非生成源码） |
| `docs/rfcs/` | 未来设计 RFC（strength / student-teacher）；非产品合同 |
| `docs/architecture.md` / `development.md` / `releasing.md` | 架构速览 / 开发 / 发布流程。0.5.3 起取代旧 `docs/archive/` 与 `docs/evidence/`（两者已删除） |
| `resources/` | 运行时静态资源：prompts/ + enforcer/catalog.json（随 npm pack 发布） |

代码里的注释不是规范。测试断言不是规范。README 不是规范。

0.5.3 起不再有 `spec/conformance.toml` / `spec/conformance.md` / `coverage.toml`：
conformance 账本与 STATUS ledger 已退役删除，条款状态由 `scripts/checks/spec.mjs`
（文本一致性）与 `scripts/checks/architecture.mjs`（源码面）直接断言，不再有
「实现状态词」中间层。`spec/` 只描述应该如何；实现是否满足由 focused checks
与测试树给出证据，不落账本。

### 发现条款本身有问题

不要顺手改条款让它符合代码。走 SSOT 例外协议：写 blocker 记录（`docs/archive/` 或 issue）、用
`../opencode` 源码行号证明是 Host 能力或逻辑矛盾而非实现困难、再改 SSOT、记 supersedes、
重新冻结。

一边改代码一边悄悄降低条款是本项目最严重的违规。

---

## 2. 迁移已收口，当前开发阶段

休克—退火迁移已收口。0.5.3 已发布：仓库规范化（单一 manifest、`resources/` 布局、
`spec/` 条款文档、`dist/` 构建输出、统一 `tests/` 树、Wanxiangshu 项目改名去掉
`.Next`、数据驱动 enforcer catalog），无运行时行为变化。0.5.3 同时退役了
conformance 账本、STATUS ledger 与旧 gate 森林（见下）。

### 当前开发阶段

0.5.3 主线是「结构化程序 DSL 纠偏」（见 `TASK.md` 与 `spec/14`，FLOW-001…FLOW-008）：
DSL 是**直接执行的 F# computation expression** + 领域命名的强类型操作 + 少量组合子，
不是待解释的业务 AST。早期 spec/14 曾把 DSL 误写成「封闭指令 AST + 唯一 Interpreter +
Trace Interpreter」并要求业务程序只能构造 Program、副作用只由 Interpreter 执行；该方向
与 spec/01 的 ARCH-001（直接用 computation expression 写流程）相悖，已 SUPERSEDED
（见 `TASK.md` 头部声明与 spec/14 的历史纠偏）。通用 `Kernel/Program.fs` /
`Kernel/TraceInterpreter.fs` 已删除；下一步删除业务侧 `Command/Reply/Step` 内部协议，
禁止再造第二套运行时——`AgentProgram`、`CompanionProgram` 那类「functions, not a Flow AST」、
直接以 `task`/`let!`/异常映射执行的写法正是参考实现。旧设计里值得保留的价值（纯决策、有界递归、命名组合子、
可检查轨迹、规则 DSL、从 Journal 恢复）由 direct CE / fake ports 承载而非 AST。`spec/16`
（PROJ-，Projection Algebra）承接 `spec/08` 的 `COMPANION-007` 与 `spec/10` 的 `VERIFY-007`，
作为投影的正式规范。

### DSL 纠偏进度（TASK PR0–PR6）

以 git tip `bcf66e3a` 与生产源码实测为准。完成 = 旧路径已删 + 新路径可跑 + 测试/门禁锁住。

| PR | 内容 | 状态 | 证据 |
|----|------|------|------|
| PR 0 | SUPERSEDED 声明 + `spec/14` 改写为直接 CE | **done** | `TASK.md` 头、`spec/14` FLOW-001…008；commit `03223a1b` |
| PR 1 | 门禁改向：允许 `task{}`、禁止 Command/Reply/Step 与业务 Interpreter | **done** | `scripts/checks/dsl-ownership.mjs` + `direct-ce-contract.test.mjs`；commit `bcf66e3a` |
| PR 2 | 删除 `Kernel/Program.fs` + `Kernel/TraceInterpreter.fs` 及 facade/测试 | **done** | 已删源文件/fsproj；facade 早在 `bcf66e3a` 移除；ratchet baseline 重生（去掉内核两项，并按现行扫描补回 Join/SessionRecovery/ChildRecovery 既有债务） |
| PR 3 | Orchestrator 垂直切片：Ops + 直接 CE，删 AST/Interpreter | **todo** | 仍是 `OrchestratorCommand/Reply/Step` + `OrchestratorInterpreter`（`37ddce30` 走了错误 M2 方向） |
| PR 4 | Reconcile 垂直切片：保留纯决策，删 Command/Reply/Program AST | **todo** | `ReconcileEvidence/Decision` 可保留；AST + `ProtocolMismatch` + `TraceInterpreter` 仍在（`6fe3a9b3`） |
| PR 5 | Join / SessionRecovery / ChildRecovery 小型 AST 清理 | **todo** | `*Interpreter.fs` 与 `JoinProgram` 仍在 |
| PR 6 | 文档/命名收尾：去掉 Program-is-data / unique interpreter 表述 | **todo** | AGENTS 后半仍残留旧 Wave/M* 叙述，待与代码同步清理 |

历史误入（已 SUPERSEDED，勿继续）：`M1` 通用 Program Kernel（`07f659d4`）、`M2` Orchestrator via Interpreter（`37ddce30`）、`M3` Reconcile Program AST（`6fe3a9b3`）。它们与现行 `TASK.md`/`spec/14` 相悖；纠偏是删除第二运行时，不是完善 Interpreter。

### 已退役的 0.5.2 机制（勿重新引入）

| 旧机制 | 0.5.3 替代 |
|--------|-----------|
| `spec/conformance.toml` / `conformance.md` / `coverage.toml` 账本 | `scripts/checks/spec.mjs`（条款唯一/悬空引用/前缀归属/导航）+ `scripts/checks/architecture.mjs`（源码面） |
| `gate:static` 森林（layout / ssot / conformance / architecture / docs / toml / budget / surface / role-matrix / shock） | `npm run lint` → `node scripts/check.mjs`：`checks/spec.mjs` + `checks/architecture.mjs` + `checks/dsl-ownership.mjs` + `checks/p0-recovery-join.mjs`；其余静态性质移入 `tests/integration/harness/`（`arch010-cases` / `budget-cases` / `schema-cases` / `single-source-cases` 等） |
| `tests/unit/runner.mjs`（父层）+ `run-inner.mjs` | `tests/unit/run.mjs`（陈旧产物 fail closed + 判据静默监督）+ `tests/e2e/support/supervise-node-test.mjs`（共享 VERIFY-004 判据） |
| `tests/e2e/scripts/*.toml`（23 个）+ `run-canary-staggered.mjs` | `tests/e2e/scenarios/*.toml`（24 个）+ `tests/e2e/cases/*.test.mjs`（20 个）+ `tests/e2e/run.mjs`（事件驱动错峰全并行，`--repeat 3`） |
| `docs/archive/`、`docs/evidence/`、blockers-closed | 已删除。Host 例外见 `spec/07` HOST-006（compaction 预防/收容）；发布流程见 `docs/releasing.md` |
| `docs/rfcs/enforcer-nudge.md` | nudge/throttle 语义并入 `spec/15`（ENFORCER-）；规则实例数据化于 `resources/enforcer/catalog.json`（`docs/decisions/enforcer-catalog.md`） |

Active 规范 = spec/01–17（`spec/15` 为 0.5.1 已交付的 Blogger 工具化子集）。
`docs/rfcs/strength.md` / `docs/rfcs/student-teacher.md` 是已批准但未交付的未来
设计，不属于当前产品合同。

### 已知说明（非发布阻塞）

- X 恢复链生产接线已闭合（`XWire.applyTransform` / `reconcileAttempt` 经 `SpikePlugin.fs`
  与 `HostSignalBootstrap.fs` 进入生产路径，`AttemptPlanner.plan` 两个调用点：
  `XWire.fs` / `CompanionTransform.fs`）；X-A–X-D layer-4 canary 已交付
  （`tests/e2e/cases/context-recovery.test.mjs` + `tests/e2e/scenarios/x-*.toml` 四个剧本）
- `PERSIST-009` worktree 路径无独立 fault-injection canary（依赖 fold 单测 + publish canary）
- Host compaction 预防/收容（HOST-006 / PERSIST-010）：Host 的 `compactIfNeeded`
  估算路径（`../opencode/packages/core/src/session/compaction.ts`）无插件
  hook 可达，因此预防层不能只写配置——必须关闭 `automatic`/`overflow`/
  `autocontinue`/`prune` 并在首轮启动做运行时探测；收容层由
  `HostCompactionGate.fs` 把任何观察到的 compaction pseudo-run 转成一条
  `ContextReanchored`

---

## 3. 三条不可违反的架构 DNA

完整规范 `spec/01.md`。

1. 结构化程序替代状态机（ARCH-001）。控制流只用 `let!/do!/use!/match/尾递归`。
   禁止 `Stage`、`Phase`、`Lease`、`Owner`、`Generation` 作为程序计数器。
   判断标准：这个字段是物理世界真实存在的事物，还是"程序接下来去哪"？后者删除。
2. 事件是信号，不是数据（ARCH-002）。碎片事件在最早边界丢弃。
   只有 `session.status=idle/retry`、`session.deleted` 能进入业务层。
   业务事实只从 SDK API 读完整 snapshot。
3. 不修改 OpenCode 本体（ARCH-003）。只用现有 Hook 和 SDK API。
   读源码是允许且必须的；改源码、要求上游加 Hook、依赖未公开 API 都不允许。

### 第四条：上下文恢复必须由失败驱动（CTX-001 / CTX-002）

与上面三条同级的硬禁止，来自 spec/12。

禁止观察或估算上下文容量（CTX-001）：不读 provider 的 context/input/output limit，
不做 token 估算，不拿估算值与任何阈值比较。禁止在失败发生前压缩（CTX-002）：
所有恢复动作的前置条件是一次真实失败的 attempt。

被这两条判死的具体形态（均已在包 X9 删除，勿重新引入）：

| 旧形态 | 违反 | 替代 |
|--------|------|------|
| `estimateTokens` / `estimateTokensUtf8` | CTX-001 | 无。不估算 |
| `shouldSwitchEpoch`（估算值 vs contextLimit） | CTX-001 + CTX-002 | 探针被 Host 接受后提交（CTX-012） |
| `bloggerSelfRebaseDue`（0.8 预算阈值） | CTX-001 + CTX-002 | 恢复槽内 squash（CTX-006） |
| `CompanionBudgetStore` / `BudgetFacts` | CTX-001 | 无。不存容量 |
| `CompanionHost.TransformRaw` 里的 epoch 注入 | CTX-002 | `AttemptPlanner.plan`（失败后） |
| `CompanionProgram.shouldReplacePrefix` | CTX-001 | `PrefixProbeSelection` |

推论：`transform` hook 里做不了恢复决策，因为它看不到 attempt 结局。
没有已提交的探针时，X 看到的就是原始历史——这是 spec/12 的正确行为，不是降级。

手工 `/compact` 无法阻断（Host 无配置开关也无可否决 Hook，属官方支持用法）。
Host 的 `compactIfNeeded` 估算路径同样无插件 hook 可达，因此配置关闭本身
不能单独构成证明。解法是两层（HOST-006）：预防层关掉
`automatic`/`overflow`/`autocontinue`/`prune` 并在首轮启动做运行时探测
（首个 managed session 完成第一轮请求后 compaction pseudo-run 必须为零，
否则 `HostContractUnsupported` 启动失败），收容层把任何观察到的
compaction 转成 `ContextReanchored` 重锚（`HostCompactionGate.fs`，
PERSIST-010）。

---

## 4. 单一写入口

每个领域恰好一个 writer（`VERIFY-005` 硬阻断项）：

| 事实 | 唯一写入口 |
|------|-----------|
| `FallbackCursorAdvanced` / `FallbackExhausted` | `FallbackController`（FALLBACK-003） |
| 任何 user-shaped prompt | `PromptDispatcher`（PROMPT-005） |
| PTY completion | backend `onExit`（EXEC-015） |
| Review confirmed | 只能从 witness 派生，不能赋值（REVIEW-006） |

出现第二个 writer 是熔断条件，立即停止新增迁移。

`scripts/architecture-gate.mjs` 已随 0.5.3 删除，其 `single-constructor` 双向检查
（既查「没有旁路者」，也查「存在调用者」）的历史教训仍在：只有前者时，一个零调用点
的唯一入口能长期假装合规——`buildAttemptExecutionProfile` 就这样在 `PROMPT-008`
标着 `CONTRADICTS` 的情况下存活到包 X8 才拿到第一个真实调用点（`AttemptPlanner.plan`）。
当前由 `tests/unit/context/attempt-plan.test.mjs` 把 profile 钉为请求的唯一源头，
单一写入口规则列于 `spec/10`（VERIFY-005）。

### 判死代码要量，不要读

删字段之前先证明它载过数据。读代码只能证明「有人写了它」，量运行时才能证明
「它到达过判断」。三种已实证的死法，各自要不同的量法：

| 死法 | 症状 | 量法 |
|------|------|------|
| 零调用点 | 唯一入口无人调用 | 旧 `architecture-gate` 双向检查；现由 `tests/unit/context/attempt-plan.test.mjs` 把 `buildAttemptExecutionProfile` 钉为唯一源头 |
| 有写入无读取 | 字段被赋值，读侧分支从不进入 | 在读点插桩计数，跑全部剧本 |
| 有读取无数据 | 读到的永远是 `undefined`，比较短路 | 在比较点打印两侧实际值 |

第三种最隐蔽，因为代码读起来完全合理。`parentSession` 是标本：16 个剧本声明它、
`matchesExpectation` 认真比较它，但唯一数据源是 provider 从不接收的
`__testkitHeaders`，而比较又经 `sessionBindings` 解析一个从未绑定的别名——
两条链各自都断。插桩五分钟得到的结论，读代码读不出来。

推论：发现一处死代码后，先量清它死了几重，再决定替代物。只修好其中一重会造出
更精巧的死代码。`parentSession` 的第一版修法是给可达性加不动点边，那条边在实测中
遍历的是空图。

---

## 5. 验证阶梯

`VERIFY-001` 六层，`VERIFY-002` 五级晋级阶梯不允许跨级：

```text
0. 静态检查（规范一致性、旧符号灭绝、架构门禁）— 不需要产物，任何阶段可跑
1. 纯函数测试（Fallback fold、authority fold、review witness）
2. 资源契约测试（Flow Using、Completion Channel、Process pumps）
3. Fake Host 轨迹（blogger busy skip、nudge、fallback、guard）
4. 单 canary（real OpenCode Host + mock provider）
5. 发布门禁（恰好 3 轮 × 完整 check:release）
```

命令（0.5.3）：

```bash
npm run lint                  # 第 0 层：format:check + node scripts/check.mjs（spec → architecture → dsl-ownership → p0-recovery-join）
npm run format               # 修正 F#/XML 格式（dotnet tool run fantomas）

npm run build                # 生产 Fable → dist（scripts/build.mjs：rm dist → fable precompile → 清 .gitignore/.fs → 校验入口与 catalog）
npm test                     # 第 1–3 层：node tests/unit/run.mjs（陈旧产物 fail closed + 判据静默监督）
npm run test:unit            # test 别名
npm run test:integration     # node tests/integration/run.mjs：resources → journal/boot → plugin → package → harness（顺序串行）
npm run test:e2e             # 单轮 canary（tests/e2e/run.mjs，事件驱动错峰全并行；--repeat 3 为三轮）
npm run test:package         # 独立跑 package 套件（pack/install/import 检查）
npm run check                # lint → build → unit → integration
npm run check:release        # check + test:e2e --repeat 3 + test:package + npm pack --dry-run
npm run gate:dsl-ownership   # 单独跑 DSL 门禁（--threshold=322）
```

`test` 拒绝在 `dist` 陈旧时运行（fail closed）。先 `npm run build`。

时间界（VERIFY-004）：所有 wall-clock 兜底集中定义在
`tests/e2e/support/time-budget.js`，逐条带理由；`tests/integration/harness/budget-cases.mjs`
断言整张预算表与实现逐字一致（预算表变更即红灯）。

### 时间界的四条实测语义（VERIFY-004）

- `node:test` 的 `timeout` 是判据线，不是中止线。超时测试继续跑，判据迟到到达。
  故静默窗口必须严格大于单测超时（`UNIT_VERDICT_SILENCE_MS > PER_TEST_TIMEOUT_MS`），
  且严格小于兜底（`< SUITE_BACKSTOP_MS`）。倒置即恢复 VERIFY-004 首条禁止项
- 续期只能由测试判据事件驱动（`test:pass` / `test:fail` / `test:complete`）。
  `test:stdout` / `test:stderr` / `diagnostic` 属背景流量，接成续期源等价于
  「让原始 SSE 或 provider 流量续期 watchdog」——一个不停打印的挂死测试将永不被判死
- watchdog 计时器必须 `unref`。否则干净结束也要等满整个窗口（实测 2000ms 窗口 → 2004ms）
- 「全部判据绿但子进程不肯退出」是失败，不是通过。旧父层 `await stream.on('end')`
  在泄漏 interval 的套件上正常收到 `end` 并 exit 0。判据全绿与进程能够离开是两个断言，
  开发者说的绿只指后者

命名随语义走：总超时改名 `SUITE_BACKSTOP_MS`，因为它在正确接线后只剩兜底职责；
叫 `SUITE_TIMEOUT_MS` 会让下一个人把它当主判据。

启动阶段（`spawn` → ready）同样不许只有兜底覆盖。`tests/e2e/support/readiness.js`
把它拆成 9 级因果阶梯（`READINESS_STAGES`），每级独立预算，到达即重新计时；总启动
时长因此无界，被界住的是静默。阶梯只前进不回退——重试的健康检查若能重置，重试循环会
永久续期启动预算。就绪门禁：未在有限窗口内输出就绪标记 → canary 失败；早退门禁：
输出就绪标记前退出 → canary 失败。匹配子进程已有的计时行本身，不新增为门禁而生的
证据：必须为门禁额外发射的证据，门禁无法信任。

先跑当前改动的最小目标测试；该阶段契约证明后才扩大范围。

禁止的捷径：加 sleep、延长 timeout 掩盖竞态、放宽断言、删除 flaky 测试、
repeat-until-pass 宣称成功、在测试中手工写 projection 终态。

### 门禁必须红过一次才算存在

写完门禁先把它守的性质破坏掉，确认它真的红。没红过的门禁与注释等价。

实证：W4 的行为用例写完后，把 `classifyVerdict` 改成恒返回 `null`（心跳完全断线），
五条用例里四条仍然全绿——它们各自都在一个静默窗口内跑完，于是「spawn 时装一次、之后
从不续期」的 watchdog 与正确接线得出同一结论。对的结果，错的原因，零覆盖机制。
区分性输入必须是合法地比窗口更慢的工作（5 × 800ms vs 3000ms 窗口）。

同源陷阱：预先注册、留空数组的门禁用例文件。在门禁输出里「零用例」与「全部通过」逐字
相同。空文件只能由完备性门禁判红——W7 按 VERIFY-004 的禁止降级清单逐项要求命名用例，
而不是靠人记得回来填。

## 6. 测试语言边界（VERIFY-008）

生产 `.fs`。第 1–3 层测试全部 `.mjs`，直接 import `dist` 发布产物。

理由不是省编译时间，而是语言边界物理性地阻止测试触碰实现内部。能从 mjs 干净进入的
恰好是 SSOT 认定为事实的契约面；碰不到的恰好是实现自由部分。

布局（0.5.3，目录已小写化）：

```text
tests/unit/run.mjs                        入口。陈旧产物 fail closed + 判据静默窗口监督
tests/unit/support/run-inner.mjs          node:test 实际执行（files/timeout/concurrency）
tests/unit/support/verdict-feed.mjs       判据分类：哪些事件允许续期 watchdog
tests/unit/support/fixtures/*.fixture.mjs 门禁驱动的故意病态套件，对真实套件不可见
tests/unit/support/domain.mjs             唯一允许知道 Fable 输出形状的文件（facade）
tests/unit/domain.meta.test.mjs           facade 自身的契约（锁住三个静默陷阱，含三时区断言）
tests/unit/guide-contract.test.mjs        VERIFY-005/008：DSL 程序入口导出契约（可调用 + 元数）
tests/unit/<domain>/*.test.mjs            按条款命名的第 1–3 层测试（context/prompt/review/
                                         fallback/execution/journal/orchestrator/kernel/plugin/
                                         verify/enforcer/strength/student-teacher/agent/domain/…）
```

铁律：

- 禁止断言 DU tag 序数、Fable 命名约定（`Module_` 前缀、`$reflection`、`FSharpMap` 内部）
- Fable 约定只能出现在 `tests/unit/support/domain.mjs` 这一个 facade 里，等价于生产侧的
  Adapter/Codec 边界门禁（`spec/10` VERIFY-008）
- 禁止只断言真值。mjs 无编译期重命名保护，字段改名会静默读到 `undefined`；
  断言必须比对完整结构或完整序列化文本
- 禁止为测试可见性新增生产 export。缺契约面就补契约，不补 export
- 新增契约面必须先在 `domain.mjs` 开出口再写测试。facade 现已覆盖
  `fallbackProjection`、`blogProjection`、`prefixEpochProjection`、`sessionAssociation`、
  `bloggerToml`、`bloggerDelta`、`companionPrompt`、`companionIdentity`、
  `companionProjection`、`hostCompaction`、`probeSelection`、`attemptPlanner`、`xPrefix`、
  `recoverySlot`、`providerInputSeal`、`reviewProjection`、`providerProjection`、
  `handleProjection`、`orchestratorProjection`、`loopDetector`、`loopSensor`、
  `xTrace`、`xTraceCapture`、`strength`、`studentTeacher`、`enforcer`、
  `joinProgram`、`orchestratorProgram`、`forkChildPayload`、`sessionRecovery`、
  `executorSummarize` 等命名空间

三个已实证的静默陷阱，全部由 facade 封死，`domain.meta.test.mjs` 锁住：

| 陷阱 | 后果 | facade 出口 |
|------|------|------------|
| `new Date(iso)` 无 `offset` 属性 | Fable `compareDates` 走 DateTime 分支加本地时区偏移，`isExpired` 反向错误 | `utcOffset()` / `clockAt()` |
| JS 数组的 `tail` 是 `undefined` | `FSharpList__get_IsEmpty` 判其为空，`List.fold` 返回种子，投影全空而断言全过 | `toList()`，`fold.apply` 自动转换 |
| union tag 是位置序数 | 中间插入新 case 后按序数构造会静默造出另一个事实 | `fact(caseName, payload)`，未知名字抛错 |

三者共同点：不抛异常、不报类型错误，只是答案错。一个写错的测试宣布错误的实现正确，
比没有测试更危险。

测试名直接引用条款：`FALLBACK_003_duplicate_signal_advances_once`。
粒度原则：入口粗，覆盖细。一个测试只验证一条因果链。

### dotnet build 绿不代表 JS 能加载

Fable 的两条语义在 `dotnet build` 下完全不可见，两者都已实证击穿过生产入口：

`Task.CompletedTask` 编译成对 `get_CompletedTask` 的引用，而 Fable 不导出该 getter，
于是 `dist/Infrastructure/OpenCode/Plugin/Plugin.js` 在 import 时就抛错——整个插件
根本加载不了，而 F# 侧毫无警告。用 `src/Wanxiangshu/Kernel/AsyncSupport.fs` 的
`completedTask()` 代替。

`[<Emit>]` 模板必须匹配 Fable 实际生成的元数。多参函数在 Fable 输出里可能是柯里化链
也可能是单个多元箭头，模板押错一边就在每次 Host 调用时抛异常。三个 Host hook
（`experimental.chat.messages.transform`、`experimental.session.compacting`、
`experimental.compaction.autocontinue`）曾同时踩中，现由 `PluginHostInterop.fs`
（`Infrastructure/OpenCode/Host/`）的 `curriedHook` / `pairedHook` 两个 emit 助手分开表达。

推论：改动任何 `[<Emit>]` 或 `Plugin.fs` 导出面之后，必须真的 `import` 一次发布产物。
`tests/unit/plugin/host-hooks.test.mjs` 以 fixture 完备性门禁锁住 hook 面，
新增 hook 未登记会失败。

## 7. Canary 剧本与 fixture

森林设计已定稿并合入 `spec/10`（VERIFY-003）。0.5.3 布局：剧本位于
`tests/e2e/scenarios/*.toml`（24 个），case 位于 `tests/e2e/cases/*.test.mjs`（20 个），
canary 清单由 `tests/e2e/support/manifest.mjs` 从文件系统派生（`CANARY_SUFFIX` =
`.test.mjs`；空/缺失目录即报错，绝不静默空跑）。runner 为 `tests/e2e/run.mjs`。

已落地的构件（`tests/e2e/`）：

| 文件 | 职责 |
|------|------|
| `support/runtime-key.js` | `(lane, turn, step, kind)` 纯函数 + 最长前缀唯一查找 |
| `support/scenario-runtime.js` | 单剧本运行时（前缀索引、seal 屏障、`clearSeals`） |
| `support/delivery-plan.js` | 故障与内容正交，物理投递计数（`attempts` 一基） |
| `support/cold-boundary.js` | 只认显式声明的冷边界 |
| `support/scenario-schema.js` | TOML 编译器，8 个根键 + 26 个 flow 动词白名单 + 载入期校验（含死边可达性不动点） |
| `support/legacy-fields.js` | 20 个退役字段，出现即拒绝载入 |
| `support/scenario-runner.js` / `scenario-turn.js` / `scenario-http.js` / `scenario-parallel.js` / `scenario-paths.js` | 单剧本运行、turn 会话、HTTP 通道、并行变体、路径隔离 |
| `support/strict-mock-*.js`（provider/server/sse/responses/matches/decorate/state/signals） | provider 严格 mock 拆分：无 scenario 匹配一律记未匹配 |
| `support/provider-wire.js` | testkit 侧仅解码 OpenAI wire，再调生产 projection（VERIFY-007 边界） |
| `support/event-probe.js` / `event-shape.js` / `journal-observer.js` | 判据事件等待/查询与 journal 事实观察 |
| `support/stability-checker.js` | VERIFY-004 三轮 + leak check（`tests/e2e/run.mjs` 调用） |
| `support/scenario-driver.mjs` / `lane.mjs` | canary 驱动与 lane 记账 |
| `support/manifest.mjs` | canary 清单由文件系统派生，计数漂移在结构上不可能发生 |
| `support/readiness.js` | 启动 9 级就绪阶梯（`READINESS_STAGES`），单调前进 |
| `support/watchdog.js` | `advance({blocking})` 判据续期，`unref`，触发时 dump 最后进展 |
| `support/time-budget.js` | 全部 wall-clock 兜底的单一来源，逐条带理由（VERIFY-004） |
| `support/supervise-node-test.mjs` | 判据静默监督共享实现（unit/integration/package 共用） |
| `support/spawn-ledger.js` / `process-lifecycle.js` / `process-host*.js` / `reaper.mjs` | 进程树、spawn 记账与清理 |

内容层（VERIFY-003，未变）：

- 剧本是 mock 的压缩表示法。压掉重复的对话前缀，不压掉语义
- 一个 scenario 恰好一个 TOML 文件，Host 启动前一次性静态加载。禁止运行期换剧本
- 运行时键四个分量皆为请求的纯函数。`step` = 该 user 消息之后的 assistant 消息条数，
  客观存在于请求里，不需要 mock 记账；`kind` 区分 chat 与 title
- 最长前缀唯一命中；命中 0 条 fail closed；同长度冲突在载入期拒绝
- 禁止用 specificity 打分、子串长度、路径下标消歧
- 书写形式是对话（TOML），前缀索引是编译产物。作者不写前缀数组
- 生产侧包裹过的 prompt 用有序片段声明 `user = ["包裹前缀", "assignment"]`：片段按序出现
  即命中，允许片段之间存在声明未覆盖的可变文本。这是 REVIEW-002 一类「生产合成外壳 +
  作者只知内容」的唯一正确表达，不要改成整段字面量
- `internal = true` 的 turn 禁止带 lane：其 prompt 由生产内部合成，不属任何声明车道

死边检查与 `internal`（载入期不动点可达性）：

- 可达边是已达 turn 的 `respond.args.prompt`；`internal = true` opt out（不在可达性
  论域内）。`internal = false` 被拒绝。当前内部合成 lane：Blogger
  （`CompanionHostBlogger.fs` 的 `sendBloggerPrompt`）、Executor map 子会话
  （`ExecutorSummarize.fs` 的 `runExecutorPrompt`）
- title turn 不需要特例：title 请求携带被标题的对话，普通前缀规则即可覆盖

故障层：

- provider 失败、SSE 中断、超时属于传输层，与内容正交。允许计数，因为物理投递次数
  真实可数。`attempts` 一基，因为它数的是投递而非数组下标
- 禁止用「破坏内容幂等」表达失败（例如对 error 删除 seal 缓存）
- 重试必须重选同一条内容边——这是「内容是请求的纯函数」为真而非口号的判据

冷边界：

- 前缀缓存的合法例外只有 epoch 切换（COMPANION-009）与 fallback 换边（FALLBACK-004）
- 必须由 scenario 显式声明位置，禁止 mock 嗅探请求形状推断
- 未声明处前缀断裂 fail closed

不得重新推导领域概念：

- mock 只能观察 wire 上真实存在的东西，且不得二次推断身份
- 禁止从 tools 形状猜 CanonicalRole、从 prompt 正文猜 Agent/tier、嗅探自定义 header
- 禁止在生产 prompt 里埋测试专用标记
- 角色由 `AttemptExecutionProfile` 唯一决定（PROMPT-008）
- harness 记账是单向的：`__testkitHeaders` 已退役，剧本只能匹配 provider 真正收到的东西。
  `parentSession` 同批退役（实测双重死代码，见 §4 末尾）
- fixture 缓存键必须用语义投影，不能用 wire 投影。用 wire 会把同一语义对话的不同
  ID 当成不同 fixture，缓存永不命中而看起来仍然工作

wire 上真实存在什么——四条实测纠正，每条都曾让整类断言静默失效：

- session 身份在 `x-session-affinity` header，不在 body。按 body 取 id 恒得
  `undefined`，ARCH-004 的 seal 屏障因此在 `ScenarioRuntime` 路径上完全不通电
- 别名到 session 是一对多。`lanesOf` 原按一对一建表，K9 实测第二个子会话被
  `try/catch` 静默吞掉；映射必须是别名 → session 集合
- `kind` 必须扫描全部前置消息，不能只看 `[0]`。title agent 的 system prompt 正在 `[0]`
- 故障与冷边界必须按 `entryId` 索引，不能按文本。按文本索引会让每一条真实故障声明
  失效——文本一经生产侧改写即失配，而失配的表现是「没有故障」，恰好是绿灯

被删的伪门禁：`containsTool`（其检查的工具词汇已灭绝，恒真）、`selfRebaseBlog`（零调用点）。
判据存在性本身要被门禁守住，否则一个恒真检查会长期冒充覆盖。
0.5.3 起旧 `scripts/run-canary-staggered.mjs` / `tests/e2e/tests/x-recovery-canary.mjs`
已删除：X 恢复 canary 现为 `tests/e2e/cases/context-recovery.test.mjs`（X-A–X-D 四剧本）。

投影分工（VERIFY-007）：

- Seal 与前缀缓存用 `ProviderWireProjection`（含 ID，字节相等，本地时间线）
- 剧本匹配与 Blogger delta 用 `ProviderSemanticProjection`（去 ID，语义相等，跨会话）
- 两者是不同类型，不得隐式互转

隔离（VERIFY-004）：

- 每个 scenario 独占 workspace、HOME/XDG、Provider、端口、Journal、spool、进程组、
  diagnostics
- 每个 scenario dispose 后检查 PID / port / session / worktree / temp / lock /
  runtime journal 全空

### Canary 判据静默与事件间隔

`manager-companion.test.mjs` 在 `tests/e2e/run.mjs` 并发套件中偶发
`manager tool schema must include manager tools` 失败，独立重跑却通过。根因不是
schema 本身，而是两次判据事件之间静默时间超过 `WATCHDOG_TIMEOUT_MS`：事件探针在这段时间内
没有收到续期事件，watchdog 先于真实完成触发。这是一个 flaky 窗口，而非被测语义失败。

处理原则：

- 不要因此放宽断言、删除 canary、或把 manager tools 重新加回来。
- 要检查该路径是否确实在合理时间范围内产生了应有的判据事件；若没有，说明该工作缺少
  中间里程碑事件，应让被测代码在长时间步骤中显式发射进度信号，或让 canary 在该步骤上
  使用更合适的等待判据（如 journal 事实而不是 `awaitTerminal`）。
- 永远不把 `repeat-until-pass` 当成稳定证据。

---

## 8. Journal

- 位于 Git common directory 下私有 `wanxiangshu-next/runtimes` 路径
  （`src/Wanxiangshu/Journal/RuntimePath.fs`），不在受测 workspace 创建
  `node_modules` 或 `.wanxiangshu-next`
- Append 只有 Committed 或 CommitUnknown，没有部分写入（PERSIST-002）
- Projection 查询不扫描完整历史，必须 O(1) 积分状态（PERSIST-008）
- Pre-0.5.0 journal 不猜测迁移，启动发现旧 schema 直接失败（PERSIST-005）
- 外部副作用走类型化 Requested → 幂等执行 → Accepted（PERSIST-009；worktree=`WorktreeCreateRequested`/`WorktreeCreated`，publish=`PublishClaimed`/`Published`）
- 序列化时间戳必须归一化到 UTC offset（PERSIST-001）。否则同一事实在不同时区产出不同
  字节，快照指纹与跨机重放全部失效。`tests/unit/domain.meta.test.mjs` 在 UTC /
  Asia/Shanghai / America/Los_Angeles 三个时区下断言 `isExpired` 与 `remainingMs`
  不随环境漂移
- Projection 的 create 类操作必须幂等（`createJob` 曾无条件覆盖，恢复重放会抹掉进度）
- 上下文恢复事实的 fold 规则见 PERSIST-010：`OpeningPromptCaptured` 幂等不可覆盖、
  `XTracePartAppended` 严格顺序 append-only、`ContextReanchored`（HOST-006 收容层）等

---

## 9. Git

- 不推 main/master
- 保持自动 git commit 提交
- 优先 stage 具体文件而非 `git add .`
- 破坏性操作（force push、`reset --hard`、`clean -f`、`branch -D`）需显式许可
- 保留 hooks，不用 `--no-verify`

# Kolmogorov 宝典

本宝典唯一权威副本 `docs/decisions/kolmogorov.md`。改动必须两边同步。

- 从最重要的开始。构建软件设计有两种方法：一种是使其足够简单，以至于明显没有缺陷；另一种是使其足够复杂，以至于没有明显的缺陷：请思考你想要哪种。取法于上，仅得其中；取法于中，不免为下。记住：君子不立危墙之下。当你写下勉强工作的代码时，透支的是未来的可控性，你在完全清醒的状态下，看着自己的逻辑链条一环扣一环地走向疯狂。毁灭你，或者拯救你，取决于你是否愿意写出明显正确的代码。
- 软件设计把不可消除复杂度压成不可再短的充分描述。好代码每行承载真实概念，名字指向领域事实，分支对应业务边界，类型拦截非法世界。文件数百行函数数十行通常是样板框架礼仪错误抽象挤占空间而非业务变深。工程第一洁癖是拯救读者注意力，让人和机器只付本质复杂度之账。小问题免框架税，大问题不手工搬砖，合适工具让问题露本相，不在配置生命周期隐式约定调试黑箱里绕路。
- 压缩不是合并，复用不是提前抽象。两段像只说明此刻长得像，不说明同一份知识。唯一表示是同一事实多处重复并开始不一致。独立生命周期概念逐字相同也该分居。边界先于抽象成熟，规则网络协议持久化权限语境视图各有领土。同个用户在认证后台订单会话是四种概念，正确解法是在上下文设海关，只传真需信息，模块包画国界，显式转换通关，架构测试守国界不被赶工磨穿，靠口头纪律的分层迟早被无意导入击穿。
- 类型系统是最便宜边防。字符数字布尔最会偷渡错误，账户号订单号用户标识若同属基本类型则编译器分不清。概念独立命名在运行时零成本，维护时直击知识边界。状态不靠可空字段和布尔开关拼凑，那会凭空造出不存在的非法组合。有限状态用有限构造表达，合法状态携带此刻有意义数据，矛盾状态在源码层生不出来。处理状态必穷尽分支，不让万能分支吞掉未来。新增状态编译器红线标红比线上日志收尸可靠。业务可预见失败不伪装成异常，不混null，不变解析字符串，找不到未授权库存不足余额不够都是返回类型具体分支，调用方被迫面对，前端直接匹配，不对文案做脆弱正则。异常只留给程序无法继续的事故。
- 非全封闭的错误处理会导致倒霉的嵌套解析。在多语言或前后端交界处，未能在边界处第一时间将其收敛为强类型，就会迫使下游编写大量胶水代码来进行运行时类型推导。
- 类型立起边界，行为回归数据。仅有字段没有规则等于敞开保险柜贴纸条。不可变数据自带约束，外界不能绕过方法偷改内部事实。变化时旧值算出新值，不在原物涂改。复杂对象构建链式设置加运行时检查只是延迟爆炸，构建阶段状态可编码进类型，必填步骤由编译器审查。派生新对象不用克隆可变原型再改字段，直接用不可变复制表达差异。纯函数内临时累加器如草稿纸允许局部可变，只要不改入参不碰外部同入同出。高频大状态更新若成瓶颈再引结构共享持久化数据结构只重建变化路径，瓶颈出现前别让优化成新偶然复杂度。为时间无关测试让路，依赖注入是好武器。
- 二十三式设计模式在代数数据类型+高阶函数+不可变数据三面棱镜下坍成三条原理。选实现的模式本质是语言缺密封类型和穷举匹配时用类层级模拟编译期分支：全局唯一实例由模块作用域承载，条件创建由密封类型加匹配表达，正交维度稳的建数据变的变函数参数，树形由和类型递归，状态切换成不可变状态机，新增扩展由模式匹配保证，编译器替你记遗漏。换行为的模式本质是语言缺一等函数时用继承接口模拟参数化：创建策略退成创建函数注入，算法骨架变化点交高阶函数，增强是函数组合，策略退成函数变量和声明式规则，处理链交组合子，操作请求退成可序列化纯数据由纯函数解释，语法解释退成小函数组合，遍历交生成器，函数可赋值传递组合后继承结构失去理由。共享缓存通知的模式本质是语言缺不可变数据和响应式原语时手工模拟信息流：接口不兼容有类型纯转换就是适配器，复杂子系统入口优先收敛公开API，内部混乱加门面只是遮羞，共享计算用纯函数缓存，观察变化交响应式流，网状通信退成发布订阅，历史快照退成事件重放，并发访问和延迟加载交Actor位置透明。GoF翻到末页只剩数据函数类型组合。
- 系统可理解性来自把判断写成规则原文，不是写成脑内单步调试的控制流。校验逻辑由签名统一小函数组成，每条独立命名，组合子串联。规则有依赖就短路：先确认轮到谁再检查手里有没有牌；规则独立就一次收全错，调用方获完整失败集合。业务表达式由是否有效有权限越界这类查询函数拼成，读起来像制度文本，不像一团if临时变量跳转路径。这样写是让源码成唯一不过期规则说明，业务方能指着一行讨论，测试能覆盖组合，编译器能保证分支完整。
- 纯函数是内核：不读时钟不掷骰子不查库不发网不写盘不改入参不造返回值外可见效果，同入同出。测试不用启服务器，重放不担心今明不同，审计不靠环境运气。真实世界网络文件时钟队列住在外壳，外壳收输入转命令，内核用当前状态和命令算结果，外壳把事件持久化广播投递。核心状态机压成一个签名：给定状态和命令返回下一状态加事件列表或强类型错误。旧状态不被修改，副作用不从函数体偷跑，事件成广播审计恢复投影共同事实来源。
- 验证不靠手工回放与临时脚本：禁止临时测试、一次性探针、只跑不提交的调试片段充当验收。调试过程永久化→排查与复现结论写成仓库内正式自动化回归（单元/集成/契约，随项目惯例命名与目录），纳入团队标准测试入口，可重放、可失败、可 CI。调试过程未落盘=未发生；注释掉的 print、随手 shell 试探、本地改完即删的断言=技术债预付款。
- 命令和事件必须分，意图可拒事实不可驳。用户说我要这样做，系统检查权限顺序资源规则，任何不过返回失败。事件说事已发生，重放历史只能忠实应用，不能因今天规则升级否定昨天写入事实。当前状态不是唯一真理，只是事件流积分，从历史折叠出的当下。银行信流水推余额，系统信不可篡改事件推局面报表时间线审计视图。原地赋值和UPDATE覆盖旧字段本质都在销毁从A变到B的事实，丢掉A存在过的证据。事件溯源是对信息完整性最基本尊重。修正历史追加补偿事件不改旧行，历史可涂改溯源就退化成覆盖写的伪装。
- 并发根本矛盾在共享可变状态，Actor将其翻转：每个处理单元拥己态，外界只发消息，内部一次处理一条不需要锁。事件循环用少量线程服务大量连接，每次上环快进快出，只做解析纯计算分发。数据库查询文件读写外部调用等阻塞操作交工作线程池，否则一个等待拖住同循环所有连接。实时共享态让写路径在墙内串行，读路径在墙外并发。写者独占态，更新后把只读数据推入管道，订阅者只消费不修改。给客户端推状态时安全边界在服务器最后一公里完成，每个接收方得己视图，私有数据完整，他人私密只留摘要计数或状态标记，别信客户端不展示，抓包工具不看界面。
- 事件落盘顺序决定记忆伦理。收到命令不能先改内存再写盘，内存会看见无证据未来。正确顺序是先追加持久化介质，确认成功后再替换内存权威状态。写盘失败等同命令未发生，写盘成功即使崩溃重启重放也回同一局面。物理载体顺应事件流，NDJSON一行一个自包含事件，追加只碰末尾，恢复逐行读取折叠。普通JSON数组追加要改已有结构，风险和语义都错。恢复时首行损坏应在损坏处截断，不跳过后续行。事件前后相扣，缺了中间后续事实就建在错基上，宁可少恢复一步，不恢复矛盾态。历史变长格式演化机器故障需要少而硬的约束，快照只是书签非真理，要记录事件总数、完整状态前缀、事件校验指纹。恢复重算指纹，对不上就弃快照从头重放，不靠文件大小字节数修改时间猜测对齐。事件结构变更每条携版本号，旧版逐级升级转最新语义，升级函数纯且幂等，不读时钟不碰网不依赖环境，否则同一历史不同时间重放出不同世界。大量独立日志，每个房间恢复独立隔离，一个文件坏只牺牲自己。启动拿文件排他锁防两个实例同时读写撕裂历史。这条链上铁律说同一件事：别信刚写入已安全，除非证明安全。先写盘后改内存因内存会骗，前缀完整性因后行完整不代表站对基础，版本号校验因大小时间撒谎，快照指纹因快照可能对不上。整条持久化纪律本质是信任负向清单。
- 这些分散规则围绕同一闭环转：用类型消灭不可能态，用纯函数固定可重现判断，用事件记录不可抵赖事实，用边界隔离语境，用组合子压缩控制流，用模块函数生成器响应式流声明式规则接管旧类层级样板，用架构测试守分层，用合适重量工具降低偶然复杂度。宏观系统切成纯内核加薄外壳，中观上下文API消息事件视图各守其位，微观变量名返回类型分支穷尽日志行版本号校验指纹替同一原则服务。不靠纪律审查文档，穷举检查让编译器站岗，代数数据类型让编译器拒非法态，架构测试让编译器守边界，密封接口让编译器记新增分支。写代码时编译器是对手，设计类型时编译器是士兵。最好代码不是模式最多，而是读者能沿每个概念边界一路追踪：从用户意图到业务判断，从事件落盘到状态重放，从私有数据到安全视图，从单行规则到整体架构，处处无暗道无多余解释，都像问题本身找到不可再短不可混淆不可逃避的表达。这一切指向同一件事：把人的注意力留给只有人能做的事。

## 思考和输出
- 你处于结对编程模式，请你解释你的每一步操作背后的原理并接受质疑。
- 偶然复杂度+修饰礼仪=∅。∀ 词必承载核心概念，零冗余。
- 斩断语气词+垫字。消除控制流跳转→直击核心事实。短句+短词，极致信息密度。
- 强类型术语+代码符号+精确错误字符串+标准缩写=绝对精准。不给脆弱文案留伪装。
- 严禁状态宣告。源码=唯一时效规则，回答=纯干货。
- 拒绝臃肿。行文=极短函数，快进快出→直接定位知识边界。
- 必要时引入 Unicode 或数学符号(如 +, =, →, ∀, ∃, ↓)进阶压缩空间。
- 风格=宝典+铁律，当代极简中文+正确全角标点，拒绝`等宽`加粗等小格式。

## 铁律输出示例
> Fable 编译 JS 环境，如何选择异步原语？全库开除 Async+Task。规避运行时装箱开销+状态机断层。
  JS.Promise<'T>=唯一异步货币。async{}→promise{}，原 Async 静态方法→Promise 模块。
  调用 Node.js 异步 API 或对外暴露接口，如何处理类型转换？
  拒绝任何装箱拆箱与强转。原生 JS Promise 完美融入 promise { }→直接 let! 解析。外发 Hook 签名直写 JS.Promise<unit/obj>→消除边界摩擦。
  Fable 禁用 MailboxProcessor 后，如何实现 Actor 模型防并发泥潭？
  JS 单线程串行化本质=Promise 链。造 SerialQueue 局部可变变量 tail 锁住队尾。内部捕获异常防止断链。异步变更强行排队→无锁保护内部状态。
  异步操作中如何处理并发、超时与异常？
  并发→Promise.all，超时→Promise.race 组合子。可见失败禁止裸抛异常。promise 内部就地 try...with 捕获→转为强类型 Result 分支→逼迫调用方匹配，不给异常留改道机会。

## 关于工具调用
- 只要需要→并行调用多个工具：并行读取+并行编辑+同文件+异文件=绝对安全。
- 强烈鼓励对同文件+异文件提交大量并行编辑。
- 并行工具执行顺序≠线性(系统不保证顺序)→∃依赖时禁止高并发调用。
- 拒绝频繁全量重写文件→精准修改=核心。
- 鼓励多意图并发→拆分独立元素+对每个意图提供完备背景知识(上下文互隔离)。
- 诉求拆细→细粒度并发。拒绝大块意图→规避长时延迟。

## 极简架构与编码铁律
- 极度推崇 DRY+KISS+极简架构。厌恶+拒绝复杂错误处理+日志记录+配置管理。
- 除非绝对必要→零注释，零意图解释(隐晦处除外)。
- 强制：中文思考+回复+编写计划；英文编写程序。
- 绝不偏离最佳实践，严禁 Dirty Hack，三思而后行。
- 厌恶无谓赋值→灵活处理+内联。边界=不引起阅读焦虑。
- 严禁通过一行多事+滥用分号来伪造行数减少。
- 强制使用高阶语法→消除代码琐碎。
- ∀变量名=极致清晰。绝不用数学味/晦涩命名+引发焦虑的缩写。
- 除非明确要求→颠覆式创新+破坏式创新。重构时丢弃旧兼容性负担，严禁滥用 facade 逃避架构整理。
- 零保留旧代码。不以 Public+契约+影响面大为由逃避重构。通知下游→不合理处皆可改。
- 任何时候，尽量精准实现，优雅实现，拒绝兜底实现或者看似“双保险”其实是弄不清楚原理不得不乱来的实现方法。

## 具体工作
- 宁慢且稳，严禁使用自动化程序批量增删改查程序代码。
- 脚本=急速幻觉+反复返工；手工编辑=脚踏实地+步步为营。慢=快。


# 关于文件行数

本仓库曾经有文件不超过 300 行限制，现在作废。

> ⚠ **SUPERSEDED（2026-08 裁决）**：以下整段是上一版「Program 为数据 + 唯一 Interpreter」方案
> 的工作笔记，其方向已被新裁决撤回。新方向见本文件 §2「当前开发阶段」、`spec/14`
> （FLOW-001…008）与 `TASK.md`：DSL 是**直接执行的 computation expression**，不是待解释的
> 业务 AST。下文「Program 必须是数据，副作用由 Interpreter 执行」「建立三个 Interpreter」
> 「该结果由哪个 Program 决定」等表述一律以新方向为准——纯决策仍保留在 Domain，但不存在
> Program AST / Interpreter / Trace 层。可保留的合理动作只有：纯决策 `Evidence -> Decision`、
> 有界递归、命名组合子、含预算的退避、fake ports 记录调用轨迹、从 Journal facts 重入普通
> workflow。本段的逐文件清单与「先止血/后大修」取舍不再作为实施指引。

你说得对，我上一版把**失败后的污染机制**抬得太高了。

未取消的输家协程和叶子 `process.exit` 最多会让一次失败变得更难看、更难诊断；它们不能解释为什么正常业务路径会出现丢完成、重复消费、错误恢复、错误 continuation 或偶发顺序差异。只要场景全绿，它们通常不会进入主因果链。

当前更有力的根因证据是：

* `spec/14` 明确要求 Program 是封闭 AST、业务程序是数据、每个 DSL 有唯一 Interpreter；
* 但当前 `AgentProgram.fs` 甚至明确写着“functions, not a Flow AST”，内部仍直接执行 `task`；
* `Application/Orchestration/Program.fs` 直接持有 Git、Manager、Journal 等端口并执行大量 `task`；
* `ReconcileSupervisor.fs` 用 `Dirty + Running + cont + terminalFound + releaseOnExit` 控制后续路径；
* `EnforcerHost.fs` 用 `commitUnknown + injectRepair + abandonThenCatchUp` 等 mutable 标志决定提交和恢复。

这不是“DSL 完成度不够”，而是**规范所说的 DSL 尚未真正成为控制流所有者**。

TASK 对目标形态的判断是正确的：Program 必须是数据，副作用由 Interpreter 执行，恢复只能由 Journal facts 和 Fold 重新构造 Program。

不过我不能仅凭仓库静态内容断言**每一个** canary flaky 都来自这个根因，因为没有上传具体失败日志。但它确实是目前最应该优先消除、也最可能系统性产生 race 的机制。

# 一、重新裁决：不是“先止血”或“先大修”

正确方法是：

```text
禁止继续制造旧控制流
        ↓
建立最小 DSL 支点
        ↓
按一个纵切面完成：
纯决策 → Program → Interpreter → 切生产入口 → 删除旧路径 → 收编 canary
        ↓
再迁下一个纵切面
```

也就是：

> **止血不是在旧架构上修补，而是每次只修一条完整因果链，并让这条链永久退出旧架构。**

不能采用以下两种极端。

### 错误极端 A：先把 canary 全修绿

这通常会产生：

* 增加 sleep；
* 增加 debounce；
* 增加 `alreadyHandled`；
* 增加 `ignoreNextEvent`；
* 增加重试；
* 增加 RuntimeState；
* 增加“为防 race”的 bool。

结果是 canary 暂时绿了，但 DSL 迁移更困难。

### 错误极端 B：关闭所有功能，半年大爆改

这会产生：

* 长期大分支；
* 新旧理解漂移；
* 一次合并几百个文件；
* 无法判断哪项改动破坏语义；
* 最后只能靠 E2E 猜正确性。

正确答案是**纵向 clean break**：每次只迁一个业务闭环，但该闭环必须一次迁透。

---

# 二、先划清三类“止血”

今后每个紧急修改先归类。

## A 类：允许立即做的结构性止血

这些修改不会固化旧架构，而且本来就是目标设计的一部分：

* 从 Journal/Fold 读取权威事实；
* 以稳定 identity 做幂等；
* durable CAS；
* 每 Session 单解释器；
* signal 只负责唤醒，不承载业务数据；
* 无法证明时 fail closed；
* 用 DU 替代行为型 bool；
* 把纯决策从 Host 中抽出；
* 确定性排序；
* 将多个副作用收进一个领域指令的 Interpreter。

这些可以叫止血，也可以叫 DSL 迁移的第一部分。

## B 类：只允许作为临时隔离的止血

例如：

* 将某个高风险 canary 单独运行；
* 暂时降低某个场景的并发度；
* 将已知红测试放入明确的 known-red 清单；
* 暂停该子系统的新功能；
* 给旧入口加调用计数，确认是否还有流量。

每项必须写明：

```text
owner
对应缺陷
删除它的 DSL cutover
不得晚于哪个合并列车删除
```

这类措施不能算修复，也不能用于发布放行。

## C 类：禁止的伪止血

* 调大超时；
* rerun-to-green；
* 吞异常；
* 无身份重试；
* 增加 `Dirty2`、`AlreadyProcessed`、`RepairSent`；
* 新旧实现都执行，取先成功的一个；
* 在 Host 中增加更多分支来纠正另一个 Host 分支；
* 用 facade 包旧 Flow 并称为 Program。

---

# 三、第一步：立即建立“不再恶化”的门禁

这一步不是完成 DSL，而是封住新增债务。

当前 `dsl-ownership.mjs` 支持一个总数 threshold，只能冻结总量。它不能阻止“删一个轻微违规、增加一个严重违规”。

改为**逐文件、逐类别 ratchet**：

```json
{
  "src/.../ReconcileSupervisor.fs": {
    "raw-task": 3,
    "mutable": 9,
    "program-counter": 5
  },
  "src/.../EnforcerHost.fs": {
    "raw-task": 2,
    "mutable": 8,
    "program-counter": 6
  }
}
```

规则如下：

1. 新文件违规必须为零。
2. 已有文件每一类违规不得增加。
3. 删除过的违规不能重新加入。
4. 修改某个 Program 文件时，该文件的违规必须至少减少一项，或者保持零。
5. baseline 只能下降，不能在 PR 中调高。
6. `Flow.lift/create` 在新增代码中零容忍。
7. 每条门禁必须有负例 fixture，证明真的会红。

这一门禁可以立即合并，因为它不需要先完成 DSL，也不会影响现有运行时。

## 同时冻结以下区域

在对应 DSL 迁移前，禁止新增业务行为：

```text
ReconcileSupervisor
EnforcerHost
BloggerRuntimeState
Application/Orchestration/Program
AgentProgram
CompanionProgram
```

只允许：

* 抽纯函数；
* 增加领域类型；
* 写 Program；
* 写 Interpreter；
* 删除旧逻辑；
* 修复已经证明的正确性缺陷。

---

# 四、建立两个并行轨道

## 轨道一：DSL 基础和纵切面迁移

负责真正修复根因。

## 轨道二：canary 因果归属

不大修 harness，不追求让所有测试表面变绿，而是回答：

```text
这个失败属于哪个业务控制流？
违反了哪条领域不变量？
对应哪个 DSL cutover？
```

为每个 flaky 建一张卡：

```text
Canary:
首次错误：
最后一个确定事实：
期待但缺失的事实：
是否重复执行 effect：
是否观察到错误顺序：
当前旧控制流所有者：
目标 Program：
目标 Interpreter：
迁移 PR：
```

例如不要记录：

```text
manager-full-loop 偶发超时
```

而要记录：

```text
Manager completion 已写入物理 transcript，
但 Reconcile 未产生对应 TurnCompleted，
或被另一次 pass 的 provisional 状态覆盖。
Owner: ReconcileProgram cutover。
```

没有失败日志支持时标为“未归因”，不能凭感觉修改代码。

---

# 五、最合理的迁移顺序

## Wave 0：合同、门禁和所有权

先合并：

1. 确认 `spec/14` 是 active contract。
2. 给每个 DSL 指定唯一 owner。
3. 建逐文件 ratchet。
4. 建 known-red 清单，但 release 仍被 known-red 阻止。
5. 建共享文件所有权。

共享文件包括：

```text
Fact.fs
FactCodec.fs
Fold.fs
Wanxiangshu.fsproj
spec 索引
package.json
```

这些文件一次只能由 Integration Owner 合并。

---

## Wave 1：最小 Program 内核

只做最小机制，不做万能 Operation。

需要：

```fsharp
type Program<'instruction, 'result> =
    | Pure of 'result
    | Suspend of 'instruction * (obj -> Program<'instruction, 'result>)
```

实际实现可以使用 GADT 模拟、Free monad 变体或每个领域独立 DU；重点不是具体技巧，而是满足：

```text
Program 不执行
Program 不持有 Runtime
Program 不持有 Host port
Program 不追加 Journal
Program 可以被 Trace Interpreter 检查
```

同时建立三个 Interpreter：

```text
Production Interpreter
Model Interpreter
Trace Interpreter
```

先证明四件事：

1. 指令顺序可观察；
2. 同输入生成同一 trace；
3. Interpreter 错误映射稳定；
4. 取消后不再产生新的 owned effect。

这一步不要改大量生产入口。

---

## Wave 2：Orchestrator 作为 DSL 校准样板

TASK 选择 Orchestrator 作为试点是合理的，因为顺序明确、Git 和 Journal 边界清晰。

当前 Orchestrator 的所谓 `Program.fs` 实际直接执行：

```text
Git.GetTargetHead
Git.Rebase
Git.FfMerge
Manager.AwaitManager
AppendFact
Worktree.Release
```

所以第一目标不是把这些函数换个名字，而是形成：

```fsharp
type OrchestratorInstruction<'next> =
    | AwaitManager of ManagerJobId * (ManagerCompletion -> 'next)
    | ReadTargetHead of TargetRef * (Result<CommitHash, GitError> -> 'next)
    | Rebase of WorktreePath * TargetRef * (RebaseOutcome -> 'next)
    | AppendFact of StreamId * AgentFact * (AppendOutcome -> 'next)
    | AcquirePublishGate of GatePath * (GateHandle -> 'next)
    | PublishFastForward of PublishClaim * (PublishOutcome -> 'next)
    | ReleaseWorktree of WorktreeIdentity * (ReleaseOutcome -> 'next)
```

业务 Program：

```fsharp
orchestrator {
    do! awaitManager job
    do! reviewCurrentTree job
    do! registerCandidate job
    do! rebaseAgainstFrozenTarget job
    do! reviewCurrentTree job
    return! publishFastForward job
}
```

纯决策：

```text
recoveryAction
publish retry decision
review acceptance
target moved decision
```

生产 Interpreter 才持有 Git、Journal、Manager。

### 这一纵切面的合并标准

同一合并列车中完成：

```text
新 Program 上线
唯一生产 Interpreter 上线
原 OrchestratorProgram.run 断开
旧 task helper 删除
Trace 测试加入
E2E 保持原行为
```

不允许先上新 Program、仍由它调用旧 `Program.run`。

完成这个样板后，团队才有共同的“正确 DSL”参照物。

---

# 六、Wave 3：优先根治 Reconcile race

这是最应该与 canary 止血穿插的区域。

当前 Reconcile 同时存在四种“状态来源”：

```text
Host signal
Dirty / Running 内存状态
SDK snapshot
Journal/Fold durable facts
```

这四者并不拥有相同语义，却共同决定后续程序路径。`Dirty` 和 `Running` 又在复制队列和调用栈事实，因此事件顺序一变化，就可能走不同分支。

## 目标结构

```text
Host signal
    ↓
CoalescingQueue<SessionId>
    ↓
每 Session 恰好一个 Reconcile Interpreter
    ↓
读取最新权威 Fold + snapshot
    ↓
构造 ReconcileProgram
    ↓
解释到完成
```

Signal 只表达：

```text
“这个 Session 可能有新事实，请重新观察。”
```

Signal 不表达：

```text
“某个具体 turn 已经完成。”
“下一步应该 continuation。”
“上一次 pass 应该再跑一次。”
```

## ReconcileProgram 指令

```text
readActiveBinding
readAuthoritativeSnapshot
classifyTurn
awaitCausalProgress
commitCompletion
sendInteractionRepair
observeCompaction
abortPhysicalRun
```

## 必须先抽出的纯类型

```fsharp
type ReconcileEvidence =
    | NoActiveBinding
    | SnapshotUnavailable of SnapshotError
    | NoMatchingTurn
    | ProvisionalTurn of ReconciledTurn
    | TerminalTurn of ReconciledTurn
    | ContradictoryEvidence of ReconcileContradiction

type ReconcileDecision =
    | ObserveOnly
    | RetryWithin of RetryBudget
    | PublishProvisional of ReconciledTurn
    | CommitTerminal of ReconciledTurn
    | SendRepair of RepairRequest
    | FailClosed of ReconcileContradiction
```

## 删除什么

```text
Dirty
Running
releaseOnExit
cont
terminalFound
continuationCandidate mutable
turnFound mutable
```

重试预算可以作为局部有界值，但不能成为跨调用生命周期状态。

## Canary 怎样随迁移收编

原 canary 若断言：

```text
在 N 秒内看见某事件
```

迁移后优先断言：

```text
Trace 中出现 ReadSnapshot
随后出现 CommitTerminal
Journal 中只有一个 terminal fact
同一个 completion 不会第二次发布
```

E2E 仍然保留，但它不再是唯一 oracle。

---

# 七、Wave 4：Join v2 与 Agent/Fork DSL 一次完成

PENDING 的 1、3、5 必须作为同一个纵切面：

* user 消息中断 join；
* 批量返回积压结果；
* work record 改为前置注释。

它们共享同一个等待和消费语义，不能拆成三个临时实现。

## 领域类型先行

```fsharp
type JoinWaitOutcome<'item> =
    | ResultsAvailable of NonEmptyBatch<'item>
    | InterruptedByUserMessage

type JoinInstruction<'next> =
    | ReadJoinableCompletions of BatchLimit * (Completion list -> 'next)
    | AwaitCompletionSignal of (JoinWakeReason -> 'next)
    | ConsumeCompletion of CompletionIdentity * (ConsumeOutcome -> 'next)
```

关键不变量：

```text
durable projection 是事实来源
mailbox 只是 wake signal
每个 completion 最多消费一次
interrupt 与 completion 同时发生时重新读事实
已有 completion 优先
```

Program 表达竞争语义：

```fsharp
join {
    let! available = readJoinables MaxJoinBatch

    match available with
    | NonEmpty batch ->
        return! consume batch
    | Empty ->
        let! wake = awaitWake ()

        let! afterWake = readJoinables MaxJoinBatch

        match afterWake, wake with
        | NonEmpty batch, _ ->
            return! consume batch
        | Empty, UserInterrupted ->
            return InterruptedByUserMessage
        | Empty, CompletionMayBeAvailable ->
            return! repeatWithin budget
}
```

这里 race 的裁决在 Program 中清晰可读，而不是散落在 mailbox、Host、tool abort 和 runtime cancel 之间。

### 同一 cutover 删除

* 旧单项 Join 成功 wire；
* tool abort → runtime cancel 的路径；
* 旧 mailbox 数据源语义；
* 无稳定顺序的 backlog drain；
* 旧 `work_record` TOML 字段。

PENDING 已经给出了完整验收矩阵，应直接作为该纵切面的完成定义。

---

# 八、Wave 5：Blogger 与 Enforcer 必须一起迁

PENDING 4 和 6 与 TASK 的 Blogger/Enforcer clean break 是同一件事，不应先在旧 Host 上实现新功能。

正确顺序：

```text
RecoveryEvidence
→ CycleEvidence
→ CycleResolution
→ BloggerProgram
→ Blogger Interpreter
→ 单一 tip 事实
→ RecentTips Fold
→ nudge 后 AABB
→ 删除旧状态
```

## 纯决策先行

```fsharp
type CycleResolution =
    | CommitMain of MainCommit
    | CommitSquashThenContinue of SquashCommit
    | SendSingleInteractionRepair of RepairRequest
    | PerformAabbRecovery of RecoveryContext
    | AbandonStaleCycle of AbandonReason
    | StopPhysicalRun of StopReason
    | FailClosed of CycleProtocolError
```

```fsharp
resolveCycle :
    CycleEvidence
    -> Result<CycleResolution, CycleProtocolError>
```

`EnforcerHost` 只能解释结果，不能再自己维护：

```text
committed
commitUnknown
injectRepair
repairCtx
abandonThenCatchUp
```

## nudge → AABB 必须由 Program 结构表达

```fsharp
blogger {
    let! first = runCycle request

    match first with
    | Valid cycle ->
        return! commit cycle

    | PureTextTerminal evidence ->
        do! sendSingleRepair evidence

        let! second = awaitRepairOutcome evidence

        match second with
        | Valid cycle ->
            return! commit cycle
        | PureTextTerminal again ->
            return! performAabb again
        | ProviderFailure failure ->
            return! recoverProviderFailure failure
}
```

这样 `RepairSpent` 不需要存在。程序结构本身就证明只能 repair 一次。

## Enforcer tip v2

同一纵切面完成：

* `tip` 必填；
* enum 来自 catalog；
* 每次只有一个 RuleId；
* 删除 score vector；
* Fold 持有最近 tip；
* squash 不丢历史；
* Blogger projection 能看到最近 tip；
* 删除旧 120 字段路径。

PENDING 对这一项要求 clean break，不能保留“新 tip + 旧 scores”双轨。

---

# 九、低耦合 PENDING 如何并行

以下两项可以在前述大迁移期间由独立队伍完成：

## sub-session 复用提示词

只修改：

* Manager prompt；
* Orchestrator prompt；
* ForkTool 描述；
* reuse contract 测试。

不得修改 Agent/Fork 控制流核心。

## Coder `tdd = red | green`

只修改：

* 领域枚举；
* CoderTool schema；
* prompt；
* tool codec；
* contract 测试。

不得趁机修改 Join、Reconcile 或 Journal 事实。

它们可以较早合并，因为与核心控制流冲突小。

---

# 十、Projection 和伪 assistant 消息最后完成

PENDING 8 不能直接在 transform 尾部 append。

必须先完成 Projection owner：

```text
ProjectionSnapshot
→ ProjectionIntent list
→ conflict detection
→ semantic projection
→ wire projection
→ input seal
```

然后把结对编程消息建模为：

```fsharp
InsertPairProgrammingThought of
    anchor: MessageIdentity *
    text: PairProgrammingThought
```

由 Projection Interpreter：

* 找到最新 user/tool-result 锚点；
* 保证同锚点幂等；
* 计算稳定 synthetic identity；
* 放入最终 seal；
* 排除出 XTrace、Blogger delta 和 work record。

PENDING 明确指出它会改变 provider bytes、prefix cache 和 review seal，所以放在 Projection 迁移完成后是正确顺序。

---

# 十一、程序员很多时的实际分工

不要按“每人认领几个文件”分工，要按知识边界分工。

```text
团队 A：Program kernel + Trace 工具
团队 B：Orchestrator DSL
团队 C：Reconcile Domain/Program
团队 D：Reconcile Interpreter + Host adapter
团队 E：Join/Agent Domain/Program
团队 F：Join Interpreter + wire
团队 G：Blogger/Enforcer Domain
团队 H：Blogger/Enforcer Interpreter
团队 I：Projection DSL
团队 J：CI 门禁、负例和模型测试
团队 K：PENDING prompt/TDD 低耦合项
团队 Z：Integration Owner
```

严格文件所有权：

```text
一个文件在一个合并列车中只有一个 owner
共享事实只能由 Integration Owner 修改
Program 团队不能修改 Infrastructure
Interpreter 团队不能修改业务决策
测试团队不得通过放宽断言换绿
```

大量程序员应并行完成：

```text
领域类型
纯决策
Program AST
Trace Interpreter
模型测试
生产 Interpreter
E2E adapter
负例门禁
```

而不是十个人同时修改 `EnforcerHost.fs`。

---

# 十二、每个纵切面的保姆级施工模板

以后每个 DSL 都严格执行以下十步。

## 1. 写不变量

例如：

```text
一个 completion 最多消费一次
同一个 terminal turn 最多发布一次
repair 最多发送一次
没有 durable evidence 不得 commit
```

## 2. 写非法世界

例如：

```text
completion 已消费但再次可见
terminal fact 存在但状态仍 Running
repair pending 同时 AABB
两个互斥 commit 决策同时为 true
```

## 3. 建 Evidence DU

只描述已知事实。

## 4. 建 Decision DU

只描述允许采取的动作。

## 5. 写纯决策函数

```text
Evidence → Result<Decision, Error>
```

## 6. 建 Program AST

将多个 Decision 组合成业务过程。

## 7. 建 Trace Interpreter

先证明顺序，不碰真实 Host。

## 8. 建模型/排列测试

枚举关键顺序：

```text
signal 在 snapshot 前
signal 在 snapshot 后
completion 与 interrupt 同时
effect 成功但 fact 未写
fact 已写但返回丢失
restart 后重新构造 Program
```

不要依赖 sleep。

## 9. 建生产 Interpreter 并切入口

生产只能有一个 owner。

## 10. 同一合并列车删除旧路径

删除：

```text
旧 Flow 调用
旧 coordinator
旧 RuntimeState
兼容 adapter
迁移 flag
旧测试 fixture
```

---

# 十三、canary 在迁移期间如何管理

不需要先重构整个测试运行器。

建立三层状态：

## Contract gate：始终阻塞

* 编译；
* spec；
* Program ownership；
* 纯决策；
* Trace；
* 属性测试；
* 模型并发测试。

## Slice canary：对应模块 cutover 后阻塞

例如 Reconcile cutover 后：

```text
host-nudge
host-restart
reviewer-restart
manager-full-loop
```

中归属于 Reconcile 的测试必须稳定通过。

## Legacy canary：迁移前允许 known-red，但禁止发布

known-red 必须精确到测试名，不能整个目录 skip：

```json
{
  "test": "manager-full-loop",
  "owner": "ReconcileProgram",
  "observedInvariant": "terminal fact occasionally missing",
  "removalCutover": "reconcile-dsl"
}
```

规则：

* unrelated PR 可以继续工作；
* 对应 DSL PR 必须删除该 known-red；
* release 时 known-red 数量必须为零；
* 不允许通过 rerun 让它变绿；
* 不允许把新失败加入清单而没有 owner 和 cutover。

这就解决了“止血和大修矛盾”：主干不必被一个已归因 race 完全瘫痪，但团队也不能谎称它已经修复。

---

# 十四、两项争议工作的最终位置

## 未取消的输家协程

降为 **P3 测试卫生**。

仅在以下情况下提前处理：

* 它污染后续场景；
* 它掩盖真正的第一错误；
* 它占用端口或进程导致无法复现。

否则等业务 race 根治后再清理。

## 叶子 `process.exit`

同样降为 **P3 诊断完整性**。

只要它没有让第一错误丢失，就不应占据当前关键路径。

最终仍值得清理，但不是 DSL 大修的前置条件，更不是 canary 根因。

---

# 十五、最终合并顺序

```text
M0  active contract + ownership + per-file ratchet
M1  最小 Program/Trace 内核
M2  Orchestrator 完整 clean break

M3  Reconcile Evidence/Decision/Program
M4  Reconcile Interpreter + 生产切换 + 删除 Dirty/Running
M5  收编 Reconcile 所属 canary

M6  Join v2 / Agent Program
M7  Join Interpreter + batch wire + interrupt + 删除旧 Join
M8  收编 Join 所属 canary

M9  sub-session reuse
M10 coder required TDD

M11 Blogger/Enforcer Evidence/Decision/Program
M12 Blogger/Enforcer Interpreter + tip fact migration
M13 删除 BloggerRuntimeState 和旧 score-vector
M14 收编 Blogger canary

M15 Projection Program
M16 pair-programming thought intent
M17 Agent/Companion 其余路径迁移

M18 删除 Flow、DomainFlow、旧 coordinator 和迁移豁免
M19 全量 crash/restart/concurrency/release 验收
M20 测试卫生：输家取消、叶子退出、诊断整理
```

其中 M3 和 M6 可以由不同团队提前准备纯领域类型和测试，但**生产切换按顺序进入主干**，避免同时切换两个高风险控制流所有者。

# 最核心的一条施工纪律

每次遇到 canary 红，不要先问：

```text
怎样让它不红？
```

先问：

```text
这个结果应该由哪个 Program 决定？
决定所依据的 Evidence 是什么？
哪个 Interpreter 执行了什么 effect？
哪个 durable fact 证明 effect 已完成？
当前为什么存在第二个控制流所有者？
```

回答不了这五个问题，就说明修复位置还没有找对。

最终目标不是“全绿且代码看起来用了 CE”，而是：

> 给定同一组 durable facts 和 authoritative snapshot，只能构造出一个合法 Program；该 Program 的 trace 明确、effect 身份稳定，任何事件到达顺序都不能创造第二种业务真相。

# 裁决

这次不是普通的“实现偏重”，而是一次**规范漂移驱动的架构误导**。

`spec/01` 的第一性原理很明确：F#/.NET 运行时已经提供 continuation、局部变量、调用栈和取消机制，业务层应当直接用 computation expression 写流程，不要再造一套 Stage、Phase 或执行机。 

但后来的 `spec/14` 和 `TASK.md` 把它改写成了：

> 封闭指令 AST + Reply + Interpreter + Trace Interpreter

甚至明确提出“computation expression ≠ DSL”。这才是本轮复杂度膨胀的根源。更关键的是，`spec/14` 自己声明冲突时以 `spec/01` 为准，因此现在可以直接依据 SSOT 纠偏，不需要继续证明哪一种解释更“高级”。  

最终裁决应当写成一句话：

> **本项目中的 DSL，是直接执行的 F# computation expression，加上领域命名的强类型操作和少量组合子；它不是待解释的业务 AST。**

---

# 一、先统一正确心智模型

## 1. DSL 应当是什么

推荐定义：

```text
DSL
  = F# computation expression
  + let! / do! / return! / match
  + 领域命名的强类型函数
  + 少量有明确语义的组合子
```

例如：

```fsharp
orchestrator {
    let! managerResult = awaitManager ops job ct
    let! candidate = reviewCandidate ops job managerResult ct
    let! published = rebaseAndPublish ops job candidate ct
    return published
}
```

这里：

* F# 调用栈就是流程栈；
* `let!` 就是顺序控制；
* `match` 就是业务分支；
* `return!` 就是尾调用；
* `CancellationToken` 就是取消协议；
* `Task<Result<_,_>>` 就是异步和错误通道；
* 普通递归就是循环；
* 类型系统直接约束每一个操作的输入和返回值。

不需要额外的 `Command`、`Reply`、`Step`、`Suspend` 或 Interpreter。

## 2. DSL 不应当是什么

以下形态全部属于“第二套运行时”：

```fsharp
type Command =
    | ReadHead ...
    | Rebase ...
    | Publish ...

type Reply =
    | UnitOk
    | Head of CommitHash
    | RebaseOk
    | RebaseConflict ...
    | Failed of string

type Program =
    | Return of Result<...>
    | Step of Command * (Reply -> Program)
```

当前 `OrchestratorProgram` 正是这种结构：一个大 `Command`，一个大 `Reply`，一个 continuation-bearing `Step`，再用 builder 拼装数据。它实际上用业务代码重新实现了一遍动态调用协议。 

这会必然产生：

* 每条正常调用被拆成 Command 定义、Reply 定义、构造器、Interpreter 分支、测试 facade 五份；
* 一个操作只能返回自己那一种结果，但大 Reply DU 允许所有回复；
* 因此每一步都要处理十几个理论上不可能出现的 Reply；
* 为了测试“执行顺序”，又造 Trace Interpreter；
* 为了复用，又造通用 Program Kernel；
* 最后 CE 不再简化代码，只是 AST 构造器的表面语法。

当前通用 Trace Interpreter 甚至通过给 continuation 传入 `null` 来遍历程序，这已经是抽象失真的强信号。 

---

# 二、目标架构

只保留四层，不再存在 `Program AST → Interpreter` 这一中间层。

```text
Domain
    事实、证据、值对象、业务结果 DU、纯决策函数

Application Workflow
    直接执行的 computation expression
    let! / match / return! 表达业务流程

Ports / Capabilities
    业务所需的强类型操作接口

Infrastructure / Runtime
    Host、Git、Journal、锁、队列、时钟、网络、进程
```

## Domain 层保留什么

例如 Reconcile 中这些应该保留：

```fsharp
type ReconcileEvidence =
    | SnapshotError of string
    | NoTurn
    | Provisional of ObservedTurn
    | Unknown of ObservedTurn option
    | Terminal of ObservedTurn
    | BudgetExhausted of hasCandidate: bool
    | SessionCleared

type ReconcileDecision =
    | RereadWithBackoff of clearCandidate: bool
    | Publish
    | StopPass

val decideStep : ReconcileEvidence -> ReconcileDecision
```

它们描述的是真实业务概念：观察到了什么、应采取什么决策。

## Domain 层删除什么

这些应删除：

```fsharp
type ReconcileCommand
type ReconcileReply
type ReconcileProgram
ProtocolMismatch
materializePass
TraceInterpreter
```

因为它们描述的不是领域，而是：

> “程序执行到这里以后，下一个函数调用是什么。”

这正是 `ARCH-001` 要交给语言运行时的东西。

---

# 三、推荐的最小 CE 实现

优先使用内置 `task {}`。只有在 `Result` 短路样板明显过多时，才保留一个极小的 `TaskResultBuilder`。

```fsharp
type TaskResult<'value, 'error> =
    Task<Result<'value, 'error>>

type TaskResultBuilder() =

    member _.Return(value: 'value) : TaskResult<'value, 'error> =
        Task.FromResult(Ok value)

    member _.ReturnFrom
        (operation: TaskResult<'value, 'error>)
        : TaskResult<'value, 'error> =
        operation

    member _.Bind
        (
            operation: TaskResult<'value, 'error>,
            next: 'value -> TaskResult<'next, 'error>
        )
        : TaskResult<'next, 'error> =
        task {
            match! operation with
            | Ok value -> return! next value
            | Error error -> return Error error
        }

    member _.Zero() : TaskResult<unit, 'error> =
        Task.FromResult(Ok())

    member _.Delay
        (factory: unit -> TaskResult<'value, 'error>)
        : TaskResult<'value, 'error> =
        task { return! factory () }

let taskResult = TaskResultBuilder()
```

它必须满足以下限制：

* 直接执行，不构造 AST；
* 不含 `Command`、`Reply`、`Step`、`Suspend`；
* 不含 `obj`、`unbox`、反射；
* 不实现通用 Interpreter；
* 不持久化 continuation；
* 最好不提供通用 `While`、`For` 和复杂异常 DSL；
* 总体保持几十行，不能逐步长成另一个框架。

`agent`、`companion`、`orchestrator` 可以只是这个 builder 的语义别名，甚至可以全部直接使用 `taskResult`。

---

# 四、Orchestrator 应如何改

## 1. 用强类型 capability 替代 Command/Reply

```fsharp
type OrchestratorOps =
    {
        AwaitManager:
            ManagerJobId ->
            CancellationToken ->
            Task<Result<ManagerResult, OrchestratorError>>

        ReadTargetHead:
            TargetRef ->
            CancellationToken ->
            Task<Result<CommitHash, OrchestratorError>>

        RebaseOnto:
            WorktreePath ->
            TargetRef ->
            CancellationToken ->
            Task<Result<RebaseResult, OrchestratorError>>

        Review:
            ReviewRequest ->
            CancellationToken ->
            Task<Result<ReviewResult, OrchestratorError>>

        Publish:
            PublishRequest ->
            CancellationToken ->
            Task<Result<PublishResult, OrchestratorError>>

        ReleaseWorktree:
            WorktreePath ->
            CancellationToken ->
            Task<Result<unit, OrchestratorError>>
    }
```

每个操作返回自己的结果：

```fsharp
type RebaseResult =
    | Rebased of CommitHash
    | Conflicted of files: string list * worktreeHead: CommitHash

type PublishResult =
    | Landed of CommitHash
    | TargetMoved
```

不要再让 `ReadTargetHead` 理论上收到 `ReviewOk` 或 `PublishFailed`。

## 2. 直接写流程

```fsharp
let rec rebaseReviewPublish
    (ops: OrchestratorOps)
    (job: ManagerJob)
    (round: int)
    (ct: CancellationToken)
    : Task<Result<CommitHash, OrchestratorError>> =
    taskResult {
        let! targetHead = ops.ReadTargetHead job.TargetRef ct
        let! rebaseResult = ops.RebaseOnto job.Worktree.Path job.TargetRef ct

        match rebaseResult with
        | Conflicted(files, worktreeHead) ->
            let! resumed =
                resumeConflict ops job files worktreeHead ct

            return! rebaseReviewPublish ops resumed (round + 1) ct

        | Rebased candidate ->
            let! review =
                ops.Review
                    { JobId = job.Id
                      SessionId = job.ManagerSessionId
                      Candidate = candidate
                      Round = round }
                    ct

            match review with
            | RevisionRequired feedback ->
                let! resumed = resumeAfterReview ops job feedback ct
                return! rebaseReviewPublish ops resumed (round + 1) ct

            | ConfirmedPerfect ->
                let! publish =
                    ops.Publish
                        { JobId = job.Id
                          Candidate = candidate
                          ExpectedTargetHead = targetHead }
                        ct

                match publish with
                | Landed commit ->
                    do! ops.ReleaseWorktree job.Worktree.Path ct
                    return commit

                | TargetMoved ->
                    return! rebaseReviewPublish ops job (round + 1) ct
    }
```

读这段代码时，审阅者看到的就是业务流程，而不是一棵要在脑内执行的 AST。

---

# 五、Reconcile 应如何改

Reconcile 需要把两种东西拆开：

1. **运行时调度机制**：队列、single-flight、generation、清理、并发锁；
2. **一次 reconcile pass 的业务流程**。

第一类是物理运行时状态，可以保留。第二类直接用 CE。

```fsharp
type ReconcileOps =
    {
        ReadActiveBinding:
            SessionId ->
            CancellationToken ->
            Task<Result<ActiveRunBinding option, ReconcileError>>

        ReadSnapshot:
            SessionId ->
            CancellationToken ->
            Task<Result<SessionMessage list, ReconcileError>>

        Delay:
            TimeSpan ->
            CancellationToken ->
            Task<Result<unit, ReconcileError>>

        PublishTurn:
            ReconciledTurn ->
            CancellationToken ->
            Task<Result<unit, ReconcileError>>

        ObserveSnapshot:
            SessionId ->
            SessionMessage list ->
            CancellationToken ->
            Task<Result<unit, ReconcileError>>
    }
```

```fsharp
let rec reconcileActive
    (ops: ReconcileOps)
    (policy: ReconcilePolicy)
    (state: ReconcilePassState)
    (ct: CancellationToken)
    : Task<Result<unit, ReconcileError>> =
    taskResult {
        if state.BudgetRemaining <= TimeSpan.Zero then
            return! publishCandidateIfNeeded ops state ct
        else
            let! messages = ops.ReadSnapshot state.SessionId ct
            let evidence = classifySnapshot state.Binding messages

            match ReconcileProgram.decideStep evidence with
            | ReconcileDecision.StopPass ->
                do! ops.ObserveSnapshot state.SessionId messages ct

            | ReconcileDecision.Publish ->
                do! publishEvidenceIfNeeded ops state evidence ct
                do! ops.ObserveSnapshot state.SessionId messages ct

            | ReconcileDecision.RereadWithBackoff clearCandidate ->
                let delay = policy.NextDelay state.BackoffIndex state.BudgetRemaining
                do! ops.Delay delay ct

                let next =
                    state
                    |> ReconcilePassState.afterObservation evidence clearCandidate
                    |> ReconcilePassState.consumeBudget delay

                return! reconcileActive ops policy next ct
    }
```

现有 `ReconcileProgram` 中大量 `ProtocolMismatch` 分支，正是大 Reply DU 丢失操作级静态类型后产生的补偿代码。 

改造后：

* 不再存在 Reply 协议；
* 因而没有协议错配；
* 每个 port 的返回类型在编译期确定；
* 调度器只负责“何时跑”，不负责“业务下一步做什么”。

---

# 六、逐 PR 纠偏顺序

## PR 0：紧急停止继续跑偏

目标：先阻止新增 AST，不动生产行为。

修改：

1. 给 `TASK.md` 加醒目的 `SUPERSEDED` 声明。
2. 修订 `spec/14`，明确它不得改变 `ARCH-001`。
3. 新增一条架构决议：

```text
本项目 DSL 为直接执行的 computation expression。
禁止把普通业务调用序列编码成 Command/Reply/Step AST。
```

4. 暂停新增以下类型：

```text
*Command
*Reply
*Program = Return | Step
Pure | Suspend
ProtocolMismatch
*Interpreter 用于解释内部业务调用
```

这一 PR 必须最先落地。否则后续删代码时，现有规范测试会持续要求工程师重新补回 Program Kernel 和 Interpreter。

---

## PR 1：纠正测试与门禁

当前门禁扫描整个 Agent、Application、Domain、Kernel、Session，并把原始 `task {}` 视为违规；同时只对名称以 `Interpreter.fs` 结尾的 Application 文件豁免。这会制度性地诱导工程师把正常代码搬进 Interpreter。  

应立即调整。

### 删除或改写

* `tests/unit/verify/program-kernel-contract.test.mjs`
* `tests/unit/verify/program-kernel.test.mjs`
* 要求 `programKernel` 导出的 facade 代码；
* 要求 `Pure/Suspend`、Trace Interpreter 存在的静态测试；
* 要求 Orchestrator 只能通过 Interpreter 运行的 shape test。

这些测试目前明确锁定了通用 Program Kernel 和 Trace Interpreter，所以它们已从防回归测试变成了错误架构的护城河。  

### 新门禁应检查

禁止：

```text
业务层 CurrentStage / NextAction / Running 等程序计数器
Command + Reply + Step/Suspend 内部执行协议
持久化 continuation 或 Program 节点
Domain 引用 Host/Infrastructure
obj/unbox 驱动的通用业务程序内核
仅用于重放普通调用序列的 Interpreter
```

允许：

```text
Application 中直接 task/taskResult
有界递归
物理并发状态的 mutable
锁、队列、取消源、completion cell
外部协议的 codec/parser/interpreter
纯领域决策 DU
```

尤其要删除 `raw-task` 违规项。`task {}` 正是正确方案的一部分，不是逃生口。

---

## PR 2：删除错误的通用基础设施

建议直接删除：

```text
src/Wanxiangshu/Kernel/Program.fs
src/Wanxiangshu/Kernel/TraceInterpreter.fs
```

同步删除：

* `.fsproj` 编译项；
* `domain.mjs` 的 `programKernel` facade；
* 对应 contract 和 behavior tests；
* 所有只为这个内核存在的导出。

然后处理：

```text
Kernel/Flow.fs
Kernel/DomainFlow.fs
```

推荐选择：

### 首选

直接使用：

```fsharp
task { ... }
```

### 次选

保留一个极小的：

```fsharp
TaskResultBuilder
```

需要保留的并行能力，例如 `parallelMapBounded`，单独放进：

```text
Kernel/Parallel.fs
```

不要为了保住一个有用的并行函数而保留整个 Flow 框架。

---

## PR 3：先改 Orchestrator

Orchestrator 是最合适的首个垂直切片：

* AST 和 Interpreter 边界明确；
* 行为有现成测试；
* 业务流程相对集中；
* 能快速展示复杂度下降。

迁移步骤：

1. 为现有行为补 characterization tests；
2. 定义 `OrchestratorOps`；
3. 把 `executeCommand` 中每个分支变成一个强类型 capability；
4. 把 `OrchestratorPrograms` 的嵌套 `Step` 改成直接 CE；
5. 测试 fresh run；
6. 测试 conflict resume；
7. 测试 target moved 重试；
8. 测试 review revision；
9. 测试 publish landed；
10. 同一 PR 删除旧 AST 和 Interpreter。

不要长期双跑。可以在开发分支暂时并存，但合并时必须 clean break。

---

## PR 4：再改 Reconcile

迁移时按下面边界切：

### 保留

```text
ReconcileEvidence
ReconcileDecision
decideStep
pickDelay
publishDecision
PublishMaps
snapshot classification
```

### 删除

```text
ReconcileCommand
ReconcileReply
ReconcileProgram
materializePass
protocolMismatch
TraceInterpreter
stepName / replyName 测试辅助面
```

### 重构

```text
ReconcileInterpreter.fs
    ↓
Reconciler.fs
```

其中：

* 队列、generation、single-flight、clear session 保留；
* `Interpret(program)` 删除；
* 改成直接调用 `runPass ops ...`；
* fake port 记录调用事件，用于测试执行顺序。

测试执行轨迹不必来自 AST。Fake port 可以简单记录：

```fsharp
ResizeArray<ReconcileEvent>
```

得到：

```text
ReadBinding
ReadSnapshot
Delay
ReadSnapshot
Publish
Observe
```

这仍然可检查，但不要求生产代码先变成一棵树。

---

## PR 5：清理其余“小型解释器”

逐个审查：

```text
Domain/JoinProgram.fs
Domain/SessionRecovery.fs
Domain/ChildRecovery.fs
Application/Reconciliation/JoinInterpreter.fs
Application/Reconciliation/SessionRecoveryInterpreter.fs
Application/Reconciliation/ChildRecoveryInterpreter.fs
```

审查标准只有一个：

> 这个 DU 描述的是领域事实/决策，还是只是在描述接下来调用哪个函数？

若是前者，保留。

若是后者，改成直接 CE。

例如当前 `JoinProgram` 一方面宣称是 Program 数据，另一方面又把 `Task<unit>` 作为 `interrupt` 放进节点，本身已经说明“纯数据 AST”边界并不成立。 

---

## PR 6：清理文档和命名

完成后全仓删除或更名：

```text
Program AST
Program is data
unique production interpreter
Trace Interpreter
Command/Reply protocol
materialize program
executeCommand
ProtocolMismatch
```

但是不要机械删除所有 `Interpreter`：

### 可以保留

* 外部 JSON/TOML/Host 协议解释；
* codec；
* parser；
* 真正的语言或数据格式解释器。

### 应删除

* 仅仅把内部 `ReadX` Command 转回 `port.ReadX()` 的解释器；
* 仅仅把普通函数调用序列编码后再回放的解释器。

---

# 七、建议直接改写 `spec/14`

可按以下条款重写。

## FLOW-001：流程直接由语言表达

业务流程必须直接使用 F# computation expression、`let!`、`do!`、`match`、`return!`、纯函数和有界递归表达。

## FLOW-002：DSL 是可执行语法，不是中间表示

领域 DSL 是 computation expression 与领域命名操作构成的源码表面。

禁止要求业务流程先构造内部 AST 再执行。

## FLOW-003：领域操作必须强类型、具名

业务流程只能通过具名 capability 调用副作用：

```text
awaitManager
readTargetHead
rebaseOnto
publish
readSnapshot
observeSnapshot
```

不公开泛化的 `execute Command`。

## FLOW-004：纯决策与效果流程分离

领域层负责：

```text
Evidence -> Decision
Facts -> Projection
Input -> Result
```

Application 层负责根据 Decision 直接执行效果。

## FLOW-005：恢复从事实重新进入普通流程

恢复过程：

```text
Journal facts
→ Fold
→ 纯恢复决策
→ 调用普通 workflow 的合法入口
```

不得恢复 Program 节点或 continuation。

## FLOW-006：禁止第二运行时

禁止：

```text
通用 Program<Pure,Suspend>
业务 Command/Reply 总线
Step continuation AST
内部业务 Interpreter
ProtocolMismatch 补偿分支
持久化执行位置
```

## FLOW-007：循环和并发必须有界

允许普通递归和并发组合子，但必须具有明确：

* 预算；
* 退出条件；
* 取消传播；
* 资源释放；
* 错误类型。

不要求通过 AST 表达。

## FLOW-008：通过可观察效果测试流程

流程测试使用 fake capabilities 记录调用、参数、facts 和结果。

不以 Program 树形或 Trace Interpreter 作为生产合同。

---

# 八、文件级处理清单

| 文件                                                     | 建议                                     |
| ------------------------------------------------------ | -------------------------------------- |
| `Kernel/Program.fs`                                    | 删除                                     |
| `Kernel/TraceInterpreter.fs`                           | 删除                                     |
| `Kernel/Flow.fs`                                       | 缩成极小 TaskResult builder，或删除            |
| `Kernel/DomainFlow.fs`                                 | 删除或仅保留 builder 别名                      |
| `Domain/OrchestratorProgram.fs`                        | 改成纯领域类型；流程迁到 Application               |
| `Application/Orchestration/OrchestratorInterpreter.fs` | 改为 `OrchestratorWorkflow.fs`，删除解释循环    |
| `Domain/ReconcileProgram.fs`                           | 拆成 `ReconcilePolicy.fs`，仅留纯决策          |
| `Application/Reconciliation/ReconcileInterpreter.fs`   | 改成调度器 + 直接 workflow                    |
| `Domain/JoinProgram.fs`                                | 改为直接 `joinAny` / `joinAvailable` CE 操作 |
| `Agent/AgentProgram.fs`                                | 作为正确方向的参考                              |
| `Session/CompanionProgram.fs`                          | 作为正确方向的参考                              |
| `dsl-ownership.mjs`                                    | 删除 raw-task 禁令和 Interpreter 路径豁免       |
| `dsl-ownership-ratchet-baseline.json`                  | 按新规则重新生成                               |
| Program kernel/trace tests                             | 删除或改成 workflow behavior tests          |
| `TASK.md`                                              | 标记失效，避免继续指导实施                          |

仓库里的 `AgentProgram` 和 `CompanionProgram` 已经明确使用“functions, not a Flow AST”，并直接以 `task`、`let!`、异常映射来执行，这两处正好可以作为新规范的参考实现。  

---

# 九、每个 PR 的验收标准

一个子系统只有满足以下条件才算纠偏完成：

1. 业务主流程可以从上到下直接阅读。
2. 不存在该子系统的 `Command + Reply + Step/Suspend`。
3. 不存在解释该内部 AST 的生产 Interpreter。
4. 不存在由大 Reply DU 导致的“不可能回复”分支。
5. Domain 中只剩事实、证据、决策和值对象。
6. 异步、取消和错误直接使用语言运行时。
7. 恢复从 Journal facts 和 projection 重新进入正常 workflow。
8. 测试通过 fake ports 验证调用顺序和外部结果。
9. 行为级 unit、integration、e2e 全部保持。
10. 删除旧实现，不留下长期双路径。

建议再加三个量化门槛：

```text
目标子系统 ProtocolMismatch 数量 = 0
目标子系统内部 Program Interpreter 数量 = 0
目标子系统 AST trace-only 测试数量 = 0
```

---

# 十、Code Review 的三问法

今后任何人提出新的 DSL 抽象，Reviewer 只问三件事：

### 问题一

> 这段流程能不能直接用 `let! / match / return!` 写？

能，就不允许造 AST。

### 问题二

> 这个 DU 表示真实领域状态，还是“程序下一步去哪”？

后者删除。

### 问题三

> 这个 Interpreter 在解释外部协议，还是在把内部 Command 重新变回函数调用？

后者删除。

---

# 最终落地顺序

```text
先改 spec
→ 再改门禁
→ 删除通用 Program Kernel
→ Orchestrator 垂直切片
→ Reconcile 垂直切片
→ Join / Recovery 小型 AST 清理
→ 文档和 facade 收尾
```

不要从“优化 Interpreter”开始，也不要先设计一个更强的泛型 Program。正确方向不是做出一个更好的解释器，而是让这类解释器根本不再需要。

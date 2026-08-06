# AGENTS.md — 万象术工程纪律与 AI 代理指南

> 本文档为万象术（Wanxiangshu）开发人员与 AI 代理（Agents）的**最高行为与工程纪律指南**。  
> 规范体系整体遵循 `docs/README.md` 定义的单向链条：`what → shape → how → proof → why`。

---

## 1. 规范体系与工作流协议

### 1.1 规范体系单向链条

所有的设计与实现必须遵循规范文档的单向流动与证明链条：

```text
what（可观察行为与条款） → shape（所有权与边界） → how（目标实现与算法） → code/resources
                                                             ↓
                                                           proof（验证与剧本）
                                                             ↑
                                                           why（设计理由与 Kolmogorov 宝典）
```

- **流动面**：`docs/proposal/`（未裁决候选，禁止直接实现）、`docs/status/`（实现相对规范的活跃差距）。
- **治理合同**：`what/document-governance.md`；执行程序：`how/document-governance.md`；理由：`why/document-governance.md`。

---

### 1.2 动手之前先读规范与状态

> Kolmogorov 标准工作流程

```text
proposal -> write why -> write what -> write shape -> write how -> check how against why -> write proof
         -> move proposal to status -> write code -> check proof -> remove proposal from status
```

**两种典型失败：**
1. **写完才想起看文档**：代码已经按旧语义定型，要么返工，要么把旧语义固化，导致规范与代码偏离。
2. **一头扎进代码细节，丢掉大局**：症状被修好，条款仍被违反（例如给旧类型补字段、加 adapter、让旧测试继续通过——局部合理，合起来是在维护过渡态）。

#### 按任务类型的最小阅读集

| 任务涉及 | 必读条款 | 必读状态 |
|---------|---------|---------|
| Prompt 发送、Authority、Dispatcher | `docs/what/prompt.md` | `tests/unit/prompt/*.test.mjs` + `tests/unit/context/attempt-plan.test.mjs` |
| Fallback、cursor、circuit breaker | `docs/what/fallback.md` | `tests/unit/fallback/*.test.mjs` |
| Review、verdict、witness、seal | `docs/what/review.md` + HOST-010/011 | `tests/unit/review/*.test.mjs` |
| Orchestrator、publish、rebase、恢复 | `docs/what/orchestrator.md` | `tests/unit/orchestrator/*.test.mjs` + `tests/unit/execution/join-*.test.mjs` |
| Host hook、事件、reconcile | `docs/what/host.md` | `tests/unit/plugin/host-hooks.test.mjs` + `tests/unit/execution/host-turn-observed.test.mjs` |
| Companion、Blogger、projection、epoch | `docs/what/companion.md` + `docs/what/context.md` | `tests/unit/context/*.test.mjs` + `tests/unit/enforcer/*.test.mjs` |
| 上下文恢复、Blogger delta、X prefix probe、Y squash | `docs/what/context.md`（`CTX-`） | `tests/unit/context/`（`probe-selection` / `recovery-slot` / `x-trace*` / `blogger-*`）+ `tests/e2e/cases/context-recovery.test.mjs`（X-A–X-D） |
| compaction、`/compact`、reanchor | `docs/what/host.md` + `docs/what/context.md` | `tests/unit/context/host-compaction-policy.test.mjs` + `src/Wanxiangshu/Infrastructure/OpenCode/Host/HostCompactionGate.fs` |
| fork/join/list、PTY、进程 | `docs/what/execution.md` | `tests/unit/execution/`（`handle*` / `join*` / `fork-*` / `executor-*`） |
| 测试、门禁、canary 剧本 | `docs/proof/verify.md` | `tests/e2e/` + `tests/unit/verify/` + `tests/integration/harness/` |
| Journal、事实、持久化 | `docs/what/persist.md` | `tests/unit/journal/*.test.mjs` + `tests/integration/journal/boot.test.mjs` |
| 运行时合成 TOML 记法 | `docs/how/synthetic-toml.md`（`ARCH-010`） | `tests/unit/context/synthetic-toml.test.mjs` + `tests/integration/harness/arch010-cases.mjs` |
| 结构化程序 DSL（FLOW-） | `docs/what/flow.md`（`FLOW-`） | `tests/unit/verify/dsl-ownership.test.mjs` + `scripts/checks/dsl-ownership.mjs` |
| Projection Algebra（PROJ-） | `docs/what/projection.md`（`PROJ-`） | `tests/unit/context/companion-projection.test.mjs` + `tests/unit/orchestrator/program.test.mjs` |
| LLM 退化循环检测与强杀恢复 | `docs/what/loop.md`（`LOOP-`） | `tests/unit/domain/loop-*.test.mjs` |
| Strength / Student&Teacher（未裁决） | `docs/proposal/strength.md` / `student-teacher.md` | `docs/status/strength-student-teacher.md` |
| 任何生产代码改动 | `docs/what/architecture.md` + `docs/shape/architecture.md` | `scripts/checks/architecture.mjs` + `scripts/checks/spec.mjs` |
| Host 行为存疑 | ARCH-003 | 读 `../opencode` 源码（见 §2.1） |

`docs/README.md` 是主导航。不确定读哪个文件时先读它。

---

### 1.3 规范与状态的权威位置

| 位置 | 性质 |
|------|------|
| `docs/what` · `shape` · `how` · `proof` · `why` | 分域产品规范。条款 ID 寻址（`PROMPT-005` 等）。冲突时以正式层为准。导航 `docs/README.md`，词汇表 `docs/what/glossary.md` |
| `docs/proposal/` | 未裁决候选；禁止直接实现 |
| `docs/status/` | 实现相对规范的活跃差距；对齐后删除 |
| `scripts/checks/spec.mjs` | 规范内部一致性：条款唯一、无悬空引用、前缀归属、导航覆盖 |
| `scripts/checks/architecture.mjs` | 源码根、fsproj 完整性、分层边界、资源读取位置、无 `.gen.fs`、无旧路径（VERIFY-005） |
| `docs/why/kolmogorov.md` | Kolmogorov 宝典唯一权威副本（工程铁律与结对输出纪律） |
| `docs/why/enforcer.md` + `resources/enforcer/catalog.json` | Enforcer 理由与规则实例（实例在实现面） |
| `resources/` | 运行时静态资源：prompts/ + enforcer/catalog.json（随 npm pack 发布） |

代码里的注释不是规范。测试断言不是规范。根 README 不是规范正文。  
旧 `spec/` 目录与 conformance 账本已 clean break 废止。条款状态由 `scripts/checks/spec.mjs` 与 `scripts/checks/architecture.mjs` 及测试树直接断言；实现差距只在 `docs/status/`。

---

### 1.4 迷路时的向上追问与 SSOT 修改协议

#### 迷路时向上走
在代码里陷住、或发现「怎么改都别扭」时，不要继续往下调。回到条款问四个问题：
1. 这个文件现在只讲一种语义吗？
2. 这条修改是在实现条款，还是在维护过渡态？
3. 这个字段是物理世界真实存在的事物，还是程序接下来去哪的信息？（来自 ARCH-001，后者一律删除）
4. 这个字段真的载过数据吗——去量，不要读代码推理？（量法见 §3.5）

#### 发现条款本身有问题
不要顺手改条款让它符合代码。走 **SSOT 例外协议**：
1. 写 blocker 记录（`docs/archive/` 或 issue）。
2. 用 `../opencode` 源码行号证明是 Host 能力或逻辑矛盾而非实现困难。
3. 修改 SSOT，记录 supersedes，重新冻结。

一边改代码一边悄悄降低条款是本项目最严重的违规。

---

### 1.5 提交前必跑门禁 (`npm run lint`)

任何面向仓库的改动，在 `git commit` 前必须先跑 `npm run lint`：
- 该命令执行 `npm run format:check`（`dotnet tool run fantomas --check src/Wanxiangshu`）
- 再跑 `node scripts/check.mjs`（focused checks 串行：spec → architecture → dsl-ownership → p0-recovery-join）。
- 运行后再做 `git add`，可确保提交内容通过检查。

`npm run lint` 也用于满足 Reasonix 编程器的 delivery work-mode 检查：在交付阶段，该检查要求工作区无未格式化的 F# 与 XML 源文件；若存在未格式化文件，`fantomas --check` 会失败，需先 `npm run format` 再提交。

---

## 2. 物理拓扑与宿主边界 (A3 地缘上下文)

### 2.1 Host 源代码位置（最重要的一条）

`../opencode` 是 OpenCode 的完整源代码仓库（若存在）。本机当前无该兄弟仓库；Host 行为以发布二进制为准，源码路径表保留为「源码可用时的地图」。

```text
../opencode                            ← Host 源码（若存在；本机当前缺失）
/Users/yuanxi/Workwork/vibe-fs-wt      ← 本仓库（插件）
```

任何关于 Host 行为的问题，先读源码，不要猜、不要只读 `.d.ts`、不要只做黑盒实验。源码缺失时，用发布二进制的 bundled JS 交叉验证。

#### Host 常用源码位置

| 关注点 | 源码路径 |
|--------|---------|
| Plugin hook 类型定义 | `../opencode/packages/plugin/src/index.ts` |
| Tool context 类型 | `../opencode/packages/plugin/src/tool.ts` |
| Prompt 主循环（provider step、transform 触发点） | `../opencode/packages/opencode/src/session/prompt.ts` |
| Compaction | `../opencode/packages/opencode/src/session/compaction.ts` |
| 消息/Part 领域类型 | `../opencode/packages/opencode/src/session/` |
| SDK 生成类型 | `../opencode/packages/sdk/` |
| Server / HTTP API | `../opencode/packages/server/` |

`node_modules/@opencode-ai/plugin` 的 `.d.ts` 是发布产物，信息量少于源码。典型例子：`experimental.chat.messages.transform` 的 `input` 类型是 `{}`，看类型会得出"transform 时无任何身份可用"的错误结论；读 `prompt.ts` 才能发现 assistant message 在 transform 之前已经创建并持久化。

已发布版本二进制在 `~/.bun/install/global/node_modules/opencode-ai/bin/opencode.exe`（`~/.bun/bin/opencode` 符号链接指向它；当前 1.18.13），可用 `strings` 提取 bundled JS 交叉验证源码与实际运行版本是否一致。

判断 SSOT 条款"Host 能力不足"之前，必须先读源码。`ARCH-003` 禁止修改 Host 本体，但不禁止阅读它——恰恰相反，只有读过才能证明某个 Hook 组合确实不存在。

---

### 2.2 生产源码布局纪律 (`src/Wanxiangshu/`)

生产源码统一位于 `src/Wanxiangshu/`（由 `Wanxiangshu.fsproj` 编译全部）：
- `Kernel/`：与业务无关的基础代数与并发控制（`AsyncSupport.fs`、`Parallel.fs` 等）。
- `Domain/`：领域事实、证据、决策与值对象（纯逻辑，不引用上层与 `Fable.Core.JsInterop`）。
- `Session/`：会话级别 Program AST 与结构化程序（`AgentProgram.fs`、`CompanionProgram.fs`）。
- `Application/`：工作流、恢复逻辑与协调器（`Reconciliation/`、`Orchestrator/`）。
- `Infrastructure/`：与 OpenCode Host/SDK/Journal/Resources 适配（`OpenCode/`、`Journal/`、`Resources/`）。

#### 布局机器验证（`scripts/checks/architecture.mjs`）
- `src/` 下唯一 F# 根、fsproj 每文件恰编译一次、无盘上未编译/已声明缺失文件；
- Kernel/Domain 不引用上层命名空间与 `Fable.Core.JsInterop`；
- package resource 读取仅在 `Infrastructure/Resources/`；
- 无 `.gen.fs`、无旧路径词汇（`docs/evidence`、`docs/archive`、`SSOT/`、`STATUS/`、`vibe-fs`、`testkit`、`Wanxiangshu.Next`）。

---

### 2.3 分发产物契约与入口

- **Fable 编译产物**：输出至 `dist/`。
- **npm 包主入口**：指向 `dist/Infrastructure/OpenCode/Plugin/Plugin.js`。
- **Manifest 契约**：根 `package.json` 为唯一 manifest；`files` = `dist` + `resources`；无 postbuild staging（0.5.3 起）。

---

## 3. 核心架构 DNA 与控制流纪律 (A1 / B1 / B4)

### 3.1 三条不可违反的架构 DNA

完整规范：`docs/what/architecture.md` 与 `docs/shape/architecture.md`。

1. **结构化程序替代状态机（ARCH-001）**：控制流只用 `let!/do!/use!/match/尾递归`。绝对禁止定义程序计数器，禁止使用 `Stage`、`Phase`、`Lease`、`Owner`、`Generation` 作为程序计数器。判断标准：*这个字段是物理世界真实存在的事物，还是"程序接下来去哪"？后者删除。*
2. **事件是信号，不是数据（ARCH-002）**：流式碎片事件在最早边界丢弃。只有 `session.status=idle/retry`、`session.deleted` 能进入业务层。业务事实只从 SDK API 读完整 snapshot。
3. **不修改 OpenCode 本体（ARCH-003）**：只用现有 Hook 和 SDK API。读源码是允许且必须的；改源码、要求上游加 Hook、依赖未公开 API 都不允许。

---

### 3.2 第四条硬性禁令：上下文恢复必须由失败驱动（CTX-001 / CTX-002）

与前三条同级的硬禁止，来自 `docs/what/context.md`（CTX-）：
- **禁止观察或估算上下文容量（CTX-001）**：不读 provider 的 context/input/output limit，不做 token 估算，不拿估算值与任何阈值比较。
- **禁止在失败发生前压缩（CTX-002）**：所有恢复动作的前置条件是一次真实失败的 attempt。

#### 被判死的具体形态（严禁重新引入）

| 旧形态 | 违反条款 | 替代解法 |
|--------|---------|---------|
| `estimateTokens` / `estimateTokensUtf8` | CTX-001 | 无。不估算 |
| `shouldSwitchEpoch`（估算值 vs contextLimit） | CTX-001 + CTX-002 | 探针被 Host 接受后提交（CTX-012） |
| `bloggerSelfRebaseDue`（0.8 预算阈值） | CTX-001 + CTX-002 | 恢复槽内 squash（CTX-006） |
| `CompanionBudgetStore` / `BudgetFacts` | CTX-001 | 无。不存容量 |
| `CompanionHost.TransformRaw` 里的 epoch 注入 | CTX-002 | `AttemptPlanner.plan`（失败后） |
| `CompanionProgram.shouldReplacePrefix` | CTX-001 | `PrefixProbeSelection` |

**推论**：`transform` hook 里做不了恢复决策，因为它看不到 attempt 结局。没有已提交的探针时，X 看到的就是原始历史——这是 CTX 的正确行为，不是降级。

#### HOST-006 Compaction 双层收容机制
手工 `/compact` 无法阻断（Host 无配置开关也无可否决 Hook，属官方支持用法）。Host 的 `compactIfNeeded` 估算路径同样无插件 hook 可达，因此配置关闭本身不能单独构成证明。
1. **预防层**：关掉 `automatic`/`overflow`/`autocontinue`/`prune` 并在首轮启动做运行时探测（首个 managed session 完成第一轮请求后 compaction pseudo-run 必须为零，否则 `HostContractUnsupported` 启动失败）。
2. **收容层**：把任何观察到的 compaction 转成 `ContextReanchored` 重锚（`HostCompactionGate.fs`，PERSIST-010）。

---

### 3.3 现行控制流纪律 (FLOW-)

```text
业务流程 = 直接执行的 F# task / let! / match + 强类型 ports
Domain   = 事实 / 证据 / 决策 / 值对象
恢复     = Journal facts + projection → 重入普通 workflow
禁止     = 业务 Program AST、业务 Interpreter、Command/Reply/Step 第二运行时、
           程序计数器 bool、facade 包旧路径、threshold 上调
```

规范：`docs/what/architecture.md` / `shape/architecture.md`（ARCH-）+ `docs/what/flow.md`（FLOW-001…008）。  
投影：`docs/what/projection.md`（PROJ-）。  
参考实现：`Session/AgentProgram.fs`、`Session/CompanionProgram.fs`、`Application/Reconciliation/ChildRecoveryWorkflow.fs`、`Application/Reconciliation/SessionRecoveryWorkflow.fs`。

#### 现行门禁契约（`scripts/checks/dsl-ownership.mjs`）

```text
--threshold=0   （scripts/check.mjs；只允许下调，禁止上调）
```

| 门 | 规则 |
|----|------|
| second-runtime-protocol / business-interpreter | 业务路径禁止 |
| program-counter / behaviour-bool | 禁止（领域 evidence 名 allowlist） |
| mutable | **合法**：Domain / Session / Application / `Kernel/Parallel.fs`；**fail-closed**：Agent / 其余 Kernel |
| infrastructure-leak | **合法 open**：仅 `HOST_BOUNDARY_OPEN_BASENAMES`（10 个 HostFork*/CompanionHost*）；其余 Session/Application fail-closed |

新增 Host 边界文件要 `open OpenCode/Process` 时：先登记 basename，再写 `open`。长期可把端口/扩展方法上移以缩小白名单。

---

### 3.4 Blogger 分层分工与施工 Review 三问

#### 分层分工（勿再双写）

| 层 | 职责 |
|----|------|
| `BloggerRuntime` | 纯 cell 转移 |
| `BloggerCoordinator` | 主会话 material 唯一入口（`onMainMaterial`） |
| `EnforcerHost` | continuation / catch-up / repair |
| `BloggerRuntimeHost` | 共用 seal / blocks / reactivate 侧效 |

#### 施工 Review 三问
1. 能否直接 `let!/match/return!`？能 → 禁造 AST。
2. DU 是领域状态还是「程序下一步」？后者删。
3. Interpreter 解外部协议还是把内部 Command 变回函数调用？后者删。

**禁止项**：`agent{}` 空壳包 `task{}`、长期双跑、只改文档不改门禁、skip/known-red 伪装完成。

---

### 3.5 单一写入口与判死代码量化法则 (VERIFY-005)

每个领域恰好一个 writer（`VERIFY-005` 硬阻断项）：

| 事实 | 唯一写入口 |
|------|-----------|
| `FallbackCursorAdvanced` / `FallbackExhausted` | `FallbackController`（FALLBACK-003） |
| 任何 user-shaped prompt | `PromptDispatcher`（PROMPT-005） |
| PTY completion | backend `onExit`（EXEC-015） |
| Review confirmed | 只能从 witness 派生，不能赋值（REVIEW-006） |

出现第二个 writer 是熔断条件，立即停止新增迁移。

`scripts/architecture-gate.mjs` 已随 0.5.3 删除，其 `single-constructor` 双向检查（既查「没有旁路者」，也查「存在调用者」）的历史教训仍在：当前由 `tests/unit/context/attempt-plan.test.mjs` 把 `buildAttemptExecutionProfile` 钉为请求的唯一源头（PROMPT-008）。

#### 判死代码要量，不要读
删字段之前先证明它载过数据。读代码只能证明「有人写了它」，量运行时才能证明「它到达过判断」。三种已实证的死法，各自要不同的量法：

| 死法 | 症状 | 量法 |
|------|------|------|
| 零调用点 | 唯一入口无人调用 | `tests/unit/context/attempt-plan.test.mjs` 钉住唯一源头 |
| 有写入无读取 | 字段被赋值，读侧分支从不进入 | 在读点插桩计数，跑全部剧本 |
| 有读取无数据 | 读到的永远是 `undefined`，比较短路 | 在比较点打印两侧实际值 |

第三种最隐蔽，因为代码读起来完全合理。`parentSession` 是标本：16 个剧本声明它、`matchesExpectation` 认真比较它，但唯一数据源是 provider 从不接收的 `__testkitHeaders`，而比较又经 `sessionBindings` 解析一个从未绑定的别名——两条链各自都断。插桩五分钟得到的结论，读代码读不出来。

---

## 4. 验证阶梯与测试契约 (D1 / D2 Proof 层)

### 4.1 六层验证与五级晋级阶梯 (VERIFY-001 / VERIFY-002)

`VERIFY-001` 六层，`VERIFY-002` 五级晋级阶梯不允许跨级：

```text
0. 静态检查（规范一致性、旧符号灭绝、架构门禁）— 不需要产物，任何阶段可跑
1. 纯函数测试（Fallback fold、authority fold、review witness）
2. 资源契约测试（Flow Using、Completion Channel、Process pumps）
3. Fake Host 轨迹（blogger busy skip、nudge、fallback、guard）
4. 单 canary（real OpenCode Host + mock provider）
5. 发布门禁（恰好 3 轮 × 完整 check:release）
```

---

### 4.2 命令入口与脚本规范

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
npm run gate:dsl-ownership   # 单独跑 DSL 门禁（scripts/check.mjs 固定 --threshold=0）
```

`test` 拒绝在 `dist` 陈旧时运行（fail closed）。先 `npm run build`。

---

### 4.3 VERIFY-004 时间界与就绪阶梯 (Readiness)

所有 wall-clock 兜底集中定义在 `tests/e2e/support/time-budget.js`，逐条带理由；`tests/integration/harness/budget-cases.mjs` 断言整张预算表与实现逐字一致（预算表变更即红灯）。

#### 时间界的四条实测语义
1. `node:test` 的 `timeout` 是判据线，不是中止线。超时测试继续跑，判据迟到到达。故静默窗口必须严格大于单测超时（`UNIT_VERDICT_SILENCE_MS > PER_TEST_TIMEOUT_MS`），且严格小于兜底（`< SUITE_BACKSTOP_MS`）。
2. 续期只能由测试判据事件驱动（`test:pass` / `test:fail` / `test:complete`）。`test:stdout` / `test:stderr` / `diagnostic` 属背景流量，接成续期源等价于「让原始 SSE 或 provider 流量续期 watchdog」。
3. watchdog 计时器必须 `unref`。否则干净结束也要等满整个窗口（实测 2000ms 窗口 → 2004ms）。
4. 「全部判据绿但子进程不肯退出」是失败，不是通过。判据全绿与进程能够离开是两个断言。

#### 9 级就绪阶梯 (`READINESS_STAGES`)
启动阶段（`spawn` → ready）拆成 9 级因果阶梯（`tests/e2e/support/readiness.js`），每级独立预算，到达即重新计时；总启动时长无界，被界住的是静默。阶梯只前进不回退。就绪门禁：未在有限窗口内输出就绪标记 → canary 失败；早退门禁：输出就绪标记前退出 → canary 失败。

---

### 4.4 门禁有效性法则与红灯断言

#### 门禁必须红过一次才算存在
写完门禁先把它守的性质破坏掉，确认它真的红。没红过的门禁与注释等价。

**实证**：W4 的行为用例写完后，把 `classifyVerdict` 改成恒返回 `null`（心跳完全断线），五条用例里四条仍然全绿——它们各自都在一个静默窗口内跑完，导致 watchdog 得出错误结论。区分性输入必须是合法地比窗口更慢的工作（5 × 800ms vs 3000ms 窗口）。

**同源陷阱**：预先注册、留空数组的门禁用例文件。在门禁输出里「零用例」与「全部通过」逐字相同。空文件只能由完备性门禁判红。

---

### 4.5 VERIFY-008 测试语言边界与物理隔离

生产为 `.fs`；第 1–3 层测试全部为 `.mjs`，直接 import `dist` 发布产物。语言边界物理性地阻止测试触碰实现内部，能从 mjs 干净进入的恰好是 SSOT 认定为事实的契约面。

#### 测试目录结构
```text
tests/unit/run.mjs                        入口。陈旧产物 fail closed + 判据静默窗口监督
tests/unit/support/run-inner.mjs          node:test 实际执行（files/timeout/concurrency）
tests/unit/support/verdict-feed.mjs       判据分类：哪些事件允许续期 watchdog
tests/unit/support/fixtures/*.fixture.mjs 门禁驱动的故意病态套件，对真实套件不可见
tests/unit/support/domain.mjs             唯一允许知道 Fable 输出形状的文件（facade）
tests/unit/domain.meta.test.mjs           facade 自身的契约（锁住三个静默陷阱，含三时区断言）
tests/unit/guide-contract.test.mjs        VERIFY-005/008：DSL 程序入口导出契约（可调用 + 元数）
tests/unit/<domain>/*.test.mjs            按条款命名的第 1–3 层测试
```

#### 测试铁律
- 禁止断言 DU tag 序数、Fable 命名约定（`Module_` 前缀、`$reflection`、`FSharpMap` 内部）。
- Fable 约定只能出现在 `tests/unit/support/domain.mjs` 这一 facade 中（VERIFY-008）。
- 禁止只断言真值。mjs 无编译期重命名保护，字段改名会静默读到 `undefined`；断言必须比对完整结构或完整序列化文本。
- 禁止为测试可见性新增生产 export。缺契约面就补契约，不补 export。
- 新增契约面必须先在 `domain.mjs` 开出口再写测试。

---

### 4.6 三大静默陷阱与 Fable/Emit 运行时加载盲区

#### 三大静默陷阱（由 `domain.mjs` 封死，`domain.meta.test.mjs` 锁住）

| 陷阱 | 后果 | facade 出口 |
|------|------|------------|
| `new Date(iso)` 无 `offset` 属性 | Fable `compareDates` 走 DateTime 分支加本地时区偏移，`isExpired` 反向错误 | `utcOffset()` / `clockAt()` |
| JS 数组的 `tail` 是 `undefined` | `FSharpList__get_IsEmpty` 判其为空，`List.fold` 返回种子，投影全空而断言全过 | `toList()`，`fold.apply` 自动转换 |
| union tag 是位置序数 | 中间插入新 case 后按序数构造会静默造出另一个事实 | `fact(caseName, payload)`，未知名字抛错 |

#### dotnet build 绿不代表 JS 能加载
Fable 的两条语义在 `dotnet build` 下完全不可见，两者都已实证击穿过生产入口：
1. `Task.CompletedTask` 编译成对 `get_CompletedTask` 的引用，而 Fable 不导出该 getter，导致 JS 抛错。用 `src/Wanxiangshu/Kernel/AsyncSupport.fs` 的 `completedTask()` 代替。
2. `[<Emit>]` 模板必须匹配 Fable 实际生成的元数。模板押错一边就在每次 Host 调用时抛异常。在 `PluginHostInterop.fs` 用 `curriedHook` / `pairedHook` 分开表达。

**推论**：改动任何 `[<Emit>]` 或 `Plugin.fs` 导出面之后，必须真的 `import` 一次发布产物。

---

### 4.7 VERIFY-003 / VERIFY-007 Canary 剧本与 Fixture 契约

剧本位于 `tests/e2e/scenarios/*.toml`（24 个），cases 位于 `tests/e2e/cases/*.test.mjs`（20 个），canary 清单由 `tests/e2e/support/manifest.mjs` 动态派生。

#### 核心构件与规则
- **剧本匹配与索引**：键 `(lane, turn, step, kind)` 为请求纯函数。最长前缀唯一命中；命中 0 条 fail closed。
- **故障层**：与内容正交。物理投递计数 `attempts` 为 1-based。
- **冷边界**：必须由 scenario 显式声明，禁止 mock 嗅探推断。未声明处前缀断裂 fail closed。
- **投影分工 (VERIFY-007)**：
  - Seal 与前缀缓存用 `ProviderWireProjection`（含 ID，字节相等）。
  - 剧本匹配与 Blogger delta 用 `ProviderSemanticProjection`（去 ID，语义相等）。
- **隔离 (VERIFY-004)**：每个 scenario 独占 workspace、HOME/XDG、Provider、端口、Journal、spool、进程组。dispose 后检查全空。

#### Canary 判据静默与事件间隔处理原则
- 判据事件静默超时触发 watchdog 时，不要放宽断言、删除 canary，也不要把 `repeat-until-pass` 当作成功证据。
- 应检查被测代码在长时间步骤中是否显式发射进度信号，或调整 canary 等待判据（如改用 Journal 事实而非 `awaitTerminal`）。

---

### 4.8 PERSIST- Journal 与持久化约束

- **私有路径**：位于 Git common directory 下私有 `wanxiangshu-next/runtimes` 路径（`RuntimePath.fs`）。
- **PERSIST-002 原子 Append**：只有 Committed 或 CommitUnknown，没有部分写入。
- **PERSIST-008 O(1) 积分状态**：Projection 查询不扫描完整历史，必须 O(1) 积分状态。
- **PERSIST-005 Schema 迁移**：Pre-0.5.0 journal 不猜测迁移，启动发现旧 schema 直接失败。
- **PERSIST-009 外部副作用**：走类型化 `Requested` → 幂等执行 → `Accepted`。
- **PERSIST-001 UTC 归一化**：序列化时间戳必须归一化到 UTC offset。
- **PERSIST-010 上下文恢复 fold**：`OpeningPromptCaptured` 幂等不可覆盖、`XTracePartAppended` 严格顺序 append-only、`ContextReanchored` 重锚。

---

## 5. Kolmogorov 软件设计宝典 (Why 层)

> **唯一权威副本**：`docs/why/kolmogorov.md`。改动必须两边同步。

1. **绝对简洁优先**：软件设计有两种方法：一种是使其足够简单，以至于明显没有缺陷；另一种是使其足够复杂，以至于没有明显的缺陷。取法于上，仅得其中。拒绝勉强工作的代码，写出明显正确的代码。
2. **压缩不可消除复杂度**：好代码每行承载真实概念，名字指向领域事实，分支对应业务边界，类型拦截非法世界。让人和机器只付本质复杂度之账。
3. **压缩不是合并，复用不是提前抽象**：两段像只说明此刻长得像，不说明同一份知识。唯一表示是同一事实多处重复并开始不一致。边界先于抽象成熟，在上下文设海关，只传真需信息，模块包画国界。
4. **类型系统是最便宜的边防**：概念独立命名在运行时零成本。有限状态用有限构造表达，合法状态携带此刻有意义数据，矛盾状态在源码层生不出来。处理状态必穷尽分支。业务可预见失败不伪装成异常，异常只留给程序无法继续的事故。
5. **封闭错误处理**：非全封闭的错误处理会导致倒霉的嵌套解析。在边界处第一时间将其收敛为强类型，不给下游留运行时类型推导的胶水代码。
6. **类型立边界，行为回归数据**：不可变数据自带约束，变化时旧值算出新值，不在原物涂改。构建阶段状态可编码进类型，必填步骤由编译器审查。纯函数内允许局部可变，不改入参不碰外部同入同出。
7. **设计模式向代数数据类型与高阶函数坍缩**：二十三式设计模式在代数数据类型 + 高阶函数 + 不可变数据三面棱镜下坍成三条原理。GoF 翻到末页只剩数据、函数与类型组合。
8. **规则可读性**：系统可理解性来自把判断写成规则原文，不是写成脑内单步调试的控制流。校验逻辑由签名统一小函数组成，读起来像制度文本，让源码成为唯一不过期的规则说明。
9. **纯函数内核与薄外壳**：纯函数是内核：不读时钟、不掷骰子、不查库、不发网、不写盘、不改入参。真实世界网络文件时钟队列住在外壳，外壳收输入转命令，内核用当前状态和命令算结果。
10. **验证不靠临时调试片段**：禁止临时测试、一次性探针、只跑不提交的调试片段充当验收。调试过程永久化 → 排查与复现结论写成仓库内正式自动化回归（单元/集成/契约），纳入团队标准测试入口。调试过程未落盘 = 未发生。
11. **命令与事件分离**：命令可拒绝，事实不可驳。用户说我要这样做（命令），系统检查规则；事件说事已发生，重放历史只能忠实应用。系统信不可篡改事件推局面，原地赋值和 UPDATE 覆盖都是在销毁事实。
12. **并发无共享可变状态**：Actor 将并发矛盾翻转：每个处理单元拥己态，外界只发消息，内部一次处理一条不需要锁。写路径墙内串行，读路径墙外并发。给客户端推状态时安全边界在服务器最后一公里完成。
13. **记忆伦理与先写盘后改内存**：收到命令不能先改内存再写盘，先追加持久化介质，确认成功后再替换内存权威状态。写盘失败等同命令未发生。恢复时首行损坏在损坏处截断，不恢复矛盾态。不靠文件大小字节数修改时间猜测对齐，依靠事件总数、完整状态前缀与校验指纹。
14. **工程闭环**：用类型消灭不可能态，用纯函数固定可重现判断，用事件记录不可抵赖事实，用边界隔离语境，用组合子压缩控制流。不靠纪律审查文档，穷举检查让编译器站岗。

---

## 6. AI 代理结对与编码纪律 (Execution)

### 6.1 结对编程模式与思考/输出协议

1. **原理解释**：你处于结对编程模式，必须解释你每一步操作背后的原理并接受质疑。
2. **零冗余表达**：偶然复杂度 + 修饰礼仪 = ∅。∀ 词必承载核心概念，零冗余。
3. **极致信息密度**：斩断语气词 + 垫字。消除控制流跳转 → 直击核心事实。短句 + 短词。
4. **绝对精准**：强类型术语 + 代码符号 + 精确错误字符串 + 标准缩写 = 绝对精准。不给脆弱文案留伪装。
5. **严禁状态宣告**：源码 = 唯一时效规则，回答 = 纯干货。绝不宣告“我已经完成/我正在做”。
6. **快进快出**：行文 = 极短函数，直接定位知识边界。
7. **排版风格**：当代极简中文 + 正确全角标点，拒绝无意义加粗与修饰。

#### 铁律输出示例
> **Q: Fable 编译 JS 环境，如何选择异步原语？**  
> 全库开除 Async+Task。规避运行时装箱开销 + 状态机断层。JS.Promise<'T> = 唯一异步货币。`async{}` → `promise{}`，原 Async 静态方法 → Promise 模块。  
> **Q: 调用 Node.js 异步 API 如何处理类型转换？**  
> 拒绝装箱拆箱强转。原生 JS Promise 完美融入 `promise{}` → 直接 `let!` 解析。外发 Hook 签名直写 `JS.Promise<unit/obj>` → 消除边界摩擦。  
> **Q: Fable 禁用 MailboxProcessor 后，如何实现 Actor 模型防并发泥潭？**  
> JS 单线程串行化本质 = Promise 链。造 `SerialQueue` 局部可变变量 `tail` 锁住队尾。内部捕获异常防止断链。异步变更强行排队 → 无锁保护内部状态。

---

### 6.2 工具调用与并行编辑纪律

1. **并行工具调用**：只要需要 → 并行调用多个工具（并行读取 + 并行编辑）。对同文件 + 异文件提交大量并行编辑绝对安全。但存在严格逻辑依赖时禁止并行。
2. **精准局部编辑**：拒绝频繁全量重写文件 → 精准修改 = 核心。
3. **意图细粒度拆分**：多意图并发 → 拆分独立元素 + 对每个意图提供完备背景知识。细粒度并发，拒绝大块长耗时意图。
4. **慢 = 快**：宁慢且稳，严禁使用自动化脚本批量增删改查源码。脚本 = 急速幻觉 + 反复返工；手工编辑 = 脚踏实地 + 步步为营。

---

### 6.3 极简架构与编码铁律

1. **极简架构**：极度推崇 DRY + KISS。厌恶 + 拒绝复杂错误处理、冗余日志记录与过度配置管理。
2. **零无谓注释**：除非绝对必要，零注释，零意图解释。
3. **语言界限**：强制中文思考 + 回复 + 编写计划；英文编写程序代码。
4. **拒绝 Dirty Hack**：绝不偏离最佳实践，三思而后行。
5. **内联与表达式**：厌恶无谓临时变量赋值 → 灵活处理 + 内联。严禁通过一行多事 + 滥用分号伪造行数减少。
6. **消除琐碎代码**：强制使用高阶语法与组合子。∀ 变量名 = 极致清晰。
7. **勇于重构与清理**：颠覆式创新 + 破坏式创新。重构时丢弃旧兼容性负担，严禁滥用 facade 逃避架构整理。零保留旧代码，不合理处皆可改。
8. **拒绝对赌与双保险**：任何时候，尽量精准实现、优雅实现，拒绝兜底实现或者看似“双保险”实则原理不清的糊涂代码。

---

### 6.4 Git 操作与提交保护

1. **分支保护**：禁止直接 `git push` 到 `main` 或 `master` 分支。
2. **规范提交**：保持自动 git commit 提交，优先 stage 具体文件而非 `git add .`。
3. **提交前检查**：`git commit` 前必须成功运行 `npm run lint`。
4. **破坏性操作限制**：破坏性操作（`git push --force`、`reset --hard`、`clean -f`、`branch -D`）需显式获得许可。
5. **保留 Git Hooks**：保留 hooks，禁止使用 `--no-verify`。

---

### 6.5 关于文件行数限制

本仓库曾经的文件不超过 300 行限制**已全面作废**。文件长度由本质复杂度决定，不设人工硬性上限。

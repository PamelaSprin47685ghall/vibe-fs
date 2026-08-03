# AGENTS.md — 万象术工程纪律

## 0. Host 源代码位置（最重要的一条）

`../opencode` 是 OpenCode 的完整源代码仓库。

```text
/home/kunweiz/Desktop/vibe/opencode        ← Host 源码（当前 1.18.10）
/home/kunweiz/Desktop/vibe/wanxiangshu     ← 本仓库（插件）
```

任何关于 Host 行为的问题，先读源码，不要猜、不要只读 `.d.ts`、不要只做黑盒实验。

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

已发布版本二进制在 `~/.cache/.bun/install/global/node_modules/opencode-ai/bin/opencode.exe`，
可用 `strings` 提取 bundled JS 交叉验证源码与实际运行版本是否一致。

判断 SSOT 条款"Host 能力不足"之前，必须先读源码。 `ARCH-003` 禁止修改 Host 本体，
但不禁止阅读它——恰恰相反，只有读过才能证明某个 Hook 组合确实不存在。

生产源码唯一根 `src/Wanxiangshu.Next/`（190 个 `.fs`，`Wanxiangshu.Next.fsproj` 编译全部）。
布局纪律由 `scripts/repository-layout-gate.mjs`（`gate:layout`）机器验证：根目录白名单、
生产源码唯一根、顶层 module 与文件名一致、重复源码探测。分发产物契约：Fable 输出
`build/next/`，npm 包 main 指向 `build/next/Infrastructure/OpenCode/Plugin/Plugin.js`。
根 `package.json` 为唯一 manifest；`files` = `build/next` + `resources`；无 postbuild staging。

---

## 1. 动手之前先读规范与状态

这条是工作顺序约束，不是建议。

```text
读条款 → 读状态 → 读代码 → 动手
```

反过来做的两种典型失败：

其一，写完才想起看文档。此时代码已经按旧语义定型，要么返工，要么把旧语义又固化一遍——后者更糟，因为它让 `conformance.md` 与代码的偏离多一处，而且看起来像「完成了工作」。

其二，一头扎进代码细节，丢掉大局。症状被修好，条款仍被违反。典型形态：给旧类型补字段、加 adapter、让旧测试继续通过——每一步局部合理，合起来是在维护过渡态。

### 按任务类型的最小阅读集

| 任务涉及 | 必读条款 | 必读状态 |
|---------|---------|---------|
| Prompt 发送、Authority、Dispatcher | SSOT/03 | conformance Prompt 段 |
| Fallback、cursor、circuit breaker | SSOT/04 | conformance Fallback 段 |
| Review、verdict、witness、seal | SSOT/05 + HOST-010/011 | conformance Review 段 + `docs/archive/shock-anneal-2026/evidence/host-transform-run-binding.md` |
| Orchestrator、publish、rebase、恢复 | SSOT/06 | conformance Orchestrator 段 |
| Host hook、事件、reconcile | SSOT/07 | conformance Host 段 + `docs/archive/shock-anneal-2026/evidence/host-transform-run-binding.md` |
| Companion、Blogger、projection、epoch | SSOT/08 + SSOT/12 | conformance Companion 段 |
| 上下文恢复、Blogger delta、X prefix probe、Y squash | SSOT/12（`CTX-`） | conformance CTX 段 |
| compaction、`/compact`、reanchor | SSOT/07 + SSOT/12 | `STATUS/blockers/README.md` + `docs/archive/shock-anneal-2026/evidence/host-context-recovery.md` |
| fork/join/list、PTY、进程 | SSOT/09 | conformance Execution 段 |
| 测试、门禁、canary 剧本 | SSOT/10 | conformance Verify 段 |
| Journal、事实、持久化 | SSOT/11 | — |
| 运行时合成 TOML 记法 | SSOT/13（`ARCH-010`） | conformance ARCH-010 行 + `tests-mjs/Context/synthetic-toml.test.mjs` |
| Strength / Enforcer nudge / Student&Teacher（未来设计） | `docs/rfcs/`（`strength.md` / `enforcer-nudge.md` / `student-teacher.md`） | `STATUS/README.md`「已知说明」段 |
| 任何生产代码改动 | SSOT/01（架构 DNA） | `STATUS/conformance.md`（由 `STATUS/conformance.toml` 生成） |
| Host 行为存疑 | ARCH-003 | 读 `../opencode` 源码（见上一节） |

`SSOT/00.md` 是导航，条款速查表在那里。不确定读哪个文件时先读它。

### 提交前运行 lint

任何面向仓库的改动，在 `git commit` 前必须先跑 `npm run lint`。
该命令执行 `scripts/pre-commit-formatter.mjs --all`，检查并修正全部
`.fs`、`.fsi`、`.xml`、`.fsproj` 文件格式。
运行后再做 `git add`，可确保提交内容通过 `gate:static` 与本地可选的
pre-commit 钩子（`node scripts/pre-commit-formatter.mjs`）一致检查。
安装依赖不修改 `.git/hooks`（无 `prepare` 钩子）。

`npm run lint` 也用于满足 Reasonix 编程器的 delivery work-mode 检查：
在交付阶段，该检查要求工作区无未格式化的 F# 与 XML 源文件；
`--all` 模式会重写所有相关文件，因此必须先跑 lint 再提交。

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
| `SSOT/` | 唯一产品规范。条款 ID 寻址（`PROMPT-005` 等）。冲突时以此为准。`SSOT/00.md` 导航，`SSOT/99.md` 词汇表 |
| `STATUS/conformance.md` | 条款 vs 代码合规表，由 `scripts/conformance-gate.mjs` 从 `STATUS/conformance.toml` 生成（勿手改）。当前 Active 192/192 CONFORMANT |
| `STATUS/README.md` | 当前基线：分支、最后验证 commit、产品状态、源码地图、下一步 |
| `STATUS/blockers/README.md` | 活跃 blocker 账本。HOST-006 已闭合（运行时探测已接线）；V2 runner `compactAfterOverflow` 观察项留给上游（ARCH-003） |
| `docs/archive/shock-anneal-2026/` | 休克—退火迁移最终报告（`FINAL-REPORT.md`）+ 原始机器证据（`evidence/`） |
| `docs/evidence/` | 发布验证证据（按版本目录） |
| `docs/decisions/kolmogorov.md` | Kolmogorov 宝典唯一权威副本（工程铁律与结对输出纪律） |
| `docs/rfcs/` | 未来设计 RFC（strength / student-teacher / enforcer-nudge）；非产品合同 |
| `resources/` | 运行时静态资源：prompts/ + enforcer/catalog.json（随 npm pack 发布） |
| `spec/` | 0.5.3 过渡：coverage.toml 骨架；条款正文仍在 SSOT/ |

代码里的注释不是规范。测试断言不是规范。README 不是规范。

`SSOT/` 只描述应该如何，不描述当前如何。实现状态词
（`CONFORMANT` / `PARTIAL` / `CONTRADICTS` / `UNVERIFIED` / `NOT_IMPLEMENTED` / `PURE_CORE_ONLY`）
只出现在 `STATUS/`。
`node scripts/ssot-lint.mjs` 强制这一分离，并检查条款 ID 唯一性与悬空引用。
新增条款前缀必须同时注册进 `scripts/ssot-lint.mjs` 的前缀表，否则该前缀的全部引用被判悬空。

### 发现条款本身有问题

不要顺手改条款让它符合代码。走 SSOT 例外协议：写 blocker（`STATUS/blockers/`）、用
`../opencode` 源码行号证明是 Host 能力或逻辑矛盾而非实现困难、再改 SSOT、记 supersedes、
重新冻结。

一边改代码一边悄悄降低条款是本项目最严重的违规。

---

## 2. 迁移已收口，当前开发阶段

休克—退火迁移已收口（最终报告 `docs/archive/shock-anneal-2026/FINAL-REPORT.md`）。
0.5.2 已发布（tag `v0.5.2`）：gate:static → build → unit（737）→ harness（285）→
P0×3（19 canary × 3 轮 = 57/57）完整通过；Active SSOT 192/192 CONFORMANT。
`STATUS/conformance.toml` 是逐条款机器账本，`STATUS/conformance.md` 由
`scripts/conformance-gate.mjs` 生成。

### 当前开发阶段

0.5.2 全 SSOT 收敛已发布：Active 192/192 CONFORMANT，0 IMPLEMENTING / 0 PARTIAL /
0 PURE_CORE_ONLY / 0 NOT_IMPLEMENTED。Active 子集 = SSOT/01–13 + SSOT/15 Blogger
工具化 + SSOT/17 LOOP。SSOT/14 Strength 与 SSOT/16 Student&Teacher 已迁
`docs/rfcs/strength.md` / `docs/rfcs/student-teacher.md`，ENFORCER nudge/throttle 在
`docs/rfcs/enforcer-nudge.md`——均为已批准但未交付的未来设计，不属于当前产品合同。

### 已知说明（非发布阻塞）

- X 恢复链生产接线已闭合（`XWire.applyTransform` / `reconcileAttempt` 经 `SpikePlugin.fs`
  与 `HostSignalBootstrap.fs` 进入生产路径，`AttemptPlanner.plan` 两个调用点）；
  X-A–X-D layer-4 canary 已交付（`testkit/opencode/tests/x-recovery-canary.mjs` + 四个 TOML 剧本）
- `PERSIST-009` worktree 路径无独立 fault-injection canary（依赖 fold 单测 + publish canary）
- V2 runner `compactAfterOverflow` 不遵守 `compaction.auto=false`：Host 上游观察项
  （ARCH-003，不可在本仓修；归档见 `docs/archive/shock-anneal-2026/FINAL-REPORT.md` §7）

---

## 3. 三条不可违反的架构 DNA

完整规范 `SSOT/01.md`。

1. 结构化程序替代状态机（ARCH-001）。控制流只用 `let!/do!/use!/match/尾递归`。
   禁止 `Stage`、`Phase`、`Lease`、`Owner`、`Generation` 作为程序计数器。
   判断标准：这个字段是物理世界真实存在的事物，还是"程序接下来去哪"？后者删除。
2. 事件是信号，不是数据（ARCH-002）。碎片事件在最早边界丢弃。
   只有 `session.status=idle/retry`、`session.deleted` 能进入业务层。
   业务事实只从 SDK API 读完整 snapshot。
3. 不修改 OpenCode 本体（ARCH-003）。只用现有 Hook 和 SDK API。
   读源码是允许且必须的；改源码、要求上游加 Hook、依赖未公开 API 都不允许。

### 第四条：上下文恢复必须由失败驱动（CTX-001 / CTX-002）

与上面三条同级的硬禁止，来自 SSOT/12。

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
没有已提交的探针时，X 看到的就是原始历史——这是 SSOT/12 的正确行为，不是降级。

手工 `/compact` 无法阻断（SSOT 例外 1，见 `STATUS/blockers/README.md`）。
解法是两层：预防层关掉 `auto`/`prune`/`autocontinue` 并在首轮启动探测，
收容层把任何观察到的 compaction 转成 `ContextReanchored` 重锚。

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

`scripts/architecture-gate.mjs` 的 `single-constructor` 检查双向：既查「没有旁路者」，
也查「存在调用者」。只有前者时，一个零调用点的唯一入口能长期假装合规——
`buildAttemptExecutionProfile` 就这样在 `PROMPT-008` 标着 `CONTRADICTS` 的情况下
存活到包 X8 才拿到第一个真实调用点（`AttemptPlanner.plan`）。

### 判死代码要量，不要读

删字段之前先证明它载过数据。读代码只能证明「有人写了它」，量运行时才能证明
「它到达过判断」。三种已实证的死法，各自要不同的量法：

| 死法 | 症状 | 量法 |
|------|------|------|
| 零调用点 | 唯一入口无人调用 | `architecture-gate` 双向检查 |
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

`VERIFY-001` 六层，`VERIFY-002` 不允许跨级：

```text
0. 静态检查（不需要产物，任何阶段可跑）
1. 纯函数测试
2. 资源契约测试
3. Fake Host 轨迹
4. 单 canary（CANARY_REPEAT=1）
5. 发布门禁（恰好 3 轮 × 完整 check:release）
```

命令：

```bash
npm run gate:static            # 第 0 层：layout + ssot + conformance + architecture + docs + toml + budget + surface + role-matrix
npm run gate:shock             # 第 0 层：静态残留审计 + 单一写入口（shock-audit.mjs）

npm run build                                # 生产 Fable → build/next（dotnet fable precompile src/Wanxiangshu.Next/Wanxiangshu.Next.fsproj）

npm run test                   # 第 1–3 层（mjs，无编译步骤）
npm run test:harness           # gate-testkit：mock 森林与隔离自检
npm run test:e2e              # 单轮 canary（run-canary-staggered.mjs，事件驱动错峰全并行）
npm run test:e2e:three        # canary 三轮（CANARY_REPEAT=3）
npm run check:release          # gate:static → build → unit → harness → e2e×3 → npm pack --dry-run
```

`test:unit` 只是 `test` 的别名（`tests-next/` 已删除，残余 F# 套件归零）。
`gate:toml`（`scripts/toml-format.mjs`）：剧本 TOML 必须与 formatter 输出逐字节一致。
`gate:budget`（`scripts/budget-gate.mjs`）：`testkit/**`、
`scripts/**`、`tests-mjs/runner.mjs` 里不得出现 ≥1000 的计时字面量，值必须来自
`testkit/opencode/time-budget.js`。判据是量级即语义线——轮询切片必须比它所受的界更快，
故合法切片按构造 < 1000ms；≥1000ms 者本身即预算。门禁无豁免通道，字符串也不得把预算
重述成带单位的时长。
`gate:surface`（`scripts/surface-inventory.mjs`）：ARCH-010
纳入范围的运行时合成文本必须逐一登记并分类。清单由 sink 侧派生而非手写生产者清单——
PROMPT-005 使 `PromptDispatcher` 的三个 send 成员加 `sendFirstPrompt` 成为插件文本到达
provider 的唯一通路，故 sink 是可枚举的闭集。双向检查：新增 send 站点无条目判红，条目
指向已消失的站点也判红；sink 名改动导致扫描为空同样 fail closed。system prompt 与
human raw 的排除是结构性的而非声明式的——send 站点文件不得在代码里引用 prompt asset，
send 行不得携带 `HumanRoot`。两项均已红过。

`gate:layout`（`scripts/repository-layout-gate.mjs`）：根目录白名单、生产源码唯一根
（`src/Wanxiangshu.Next/`）、顶层 module 与文件名一致、重复源码探测。
`gate:docs`（`scripts/strip-doc-bold.mjs`）：prose 去加粗 + 全角标点空格规范化，
fenced code block 不触碰。

`test`（`test:unit`）拒绝在 `build/next` 陈旧时运行（fail closed）。先 `npm run build`。

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

启动阶段（`spawn` → ready）同样不许只有兜底覆盖。`testkit/opencode/readiness.js` 把它
拆成 6 级因果阶梯，每级独立预算，到达即重新计时；总启动时长因此无界，被界住的是静默。
阶梯只前进不回退——重试的健康检查若能重置，重试循环会永久续期启动预算。匹配子进程
已有的计时行本身，不新增为门禁而生的证据：必须为门禁额外发射的证据，门禁无法信任。

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

生产 `.fs`。第 1–3 层测试全部 `.mjs`，直接 import `build/next` 发布产物。

理由不是省编译时间，而是语言边界物理性地阻止测试触碰实现内部。能从 mjs 干净进入的
恰好是 SSOT 认定为事实的契约面；碰不到的恰好是实现自由部分。

布局：

```text
tests-mjs/runner.mjs              父层。陈旧产物 fail closed + 判据静默窗口监督
tests-mjs/run-inner.mjs           子层。node:test 实际执行（files/timeout/concurrency）
tests-mjs/verdict-feed.mjs        判据分类：哪些事件允许续期 watchdog
tests-mjs/fixtures/*.fixture.mjs  门禁驱动的故意病态套件，对真实套件不可见
tests-mjs/domain.mjs              唯一允许知道 Fable 输出形状的文件
tests-mjs/domain.meta.test.mjs    facade 自身的契约（锁住三个静默陷阱）
tests-mjs/guide-contract.test.mjs ARCH-009 契约：断言不存在无界的 `Parallel.map*` 兄弟
tests-mjs/<Domain>/*.test.mjs     按条款命名的第 1–3 层测试（Context/Prompt/Review/Fallback/
                                 Execution/Journal/Orchestrator/Kernel/Plugin/Verify/Strength/Enforcer/StudentTeacher）
```

铁律：

- 禁止断言 DU tag 序数、Fable 命名约定（`Module_` 前缀、`$reflection`、`FSharpMap` 内部）
- Fable 约定只能出现在 `tests-mjs/domain.mjs` 这一个 facade 里，等价于生产侧的
  Adapter/Codec 边界门禁
- 禁止只断言真值。mjs 无编译期重命名保护，字段改名会静默读到 `undefined`；
  断言必须比对完整结构或完整序列化文本
- 禁止为测试可见性新增生产 export。缺契约面就补契约，不补 export
- 新增契约面必须先在 `domain.mjs` 开出口再写测试。facade 现已覆盖
  `fallbackProjection`、`blogProjection`、`prefixEpochProjection`、`sessionAssociation`、
  `bloggerToml`、`bloggerDelta`、`companionPrompt`、`companionIdentity`、
  `companionProjection`、`hostCompaction`、`probeSelection`、`attemptPlanner`、`xPrefix`、
  `recoverySlot`、`providerInputSeal`、`reviewProjection`、`providerProjection`、
  `handleProjection`、`orchestratorProjection` 等命名空间

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
于是 `build/next/Infrastructure/OpenCode/Plugin/Plugin.js` 在 import 时就抛错——整个插件
根本加载不了，而 F# 侧毫无警告。用 `src/Wanxiangshu.Next/Kernel/AsyncSupport.fs` 的
`completedTask()` 代替。

`[<Emit>]` 模板必须匹配 Fable 实际生成的元数。多参函数在 Fable 输出里可能是柯里化链
也可能是单个多元箭头，模板押错一边就在每次 Host 调用时抛异常。三个 Host hook
（`experimental.chat.messages.transform`、`experimental.session.compacting`、
`experimental.compaction.autocontinue`）曾同时踩中，现由 `PluginHostInterop.fs`
（`Infrastructure/OpenCode/Host/`）的 `curriedHook` / `pairedHook` 两个 emit 助手分开表达。

推论：改动任何 `[<Emit>]` 或 `Plugin.fs` 导出面之后，必须真的 `import` 一次发布产物。
`tests-mjs/Plugin/host-hooks.test.mjs` 以 fixture 完备性门禁锁住 hook 面，
新增 hook 未登记会失败。

## 7. Canary 剧本与 fixture

森林设计已定稿并合入 `SSOT/10`（VERIFY-003）与 conformance Verify 段，历史记档在
`docs/archive/shock-anneal-2026/FINAL-REPORT.md`。剧本位于 `testkit/opencode/scripts/*.toml`
（18 个），canary 清单由 `canary-manifest.js` 从文件系统派生。

已落地的构件（`testkit/opencode/`）：

| 文件 | 职责 |
|------|------|
| `runtime-key.js` | `(lane, turn, step, kind)` 纯函数 + 最长前缀唯一查找 |
| `scenario-runtime.js` | 单剧本运行时（前缀索引、seal 屏障、`clearSeals`），取代已删的 `script-loader.js` |
| `delivery-plan.js` | 故障与内容正交，物理投递计数 |
| `cold-boundary.js` | 只认显式声明的冷边界 |
| `scenario-schema.js` | TOML 编译器，8 个根键 + 24 个 flow 动词白名单 + 载入期校验 |
| `scenario-runner.js` / `scenario-turn.js` / `scenario-http.js` / `scenario-parallel.js` / `scenario-paths.js` | 单剧本运行、turn 会话、HTTP 通道、并行变体、路径隔离 |
| `strict-mock-provider.js` | provider 严格 mock：无 scenario 匹配一律记未匹配（K9 已删旧匹配路径） |
| `provider-wire.js` | testkit 侧仅解码 OpenAI wire，再调生产 projection（VERIFY-007 边界） |
| `event-probe*.js` / `journal-observer.js` | 判据事件等待/查询与 journal 事实观察 |
| `stability-checker.js` | VERIFY-004 三轮 + leak check（`run-canary-staggered.mjs` 调用） |
| `canary-driver.mjs` / `lane.mjs` | canary 驱动与 lane 记账 |
| `scripts/run-canary-staggered.mjs` | 事件驱动错峰全并行 runner：admitted canary 的首个 host-ready「bark」触发下一个启动，无固定 sleep |
| `legacy-fields.js` | 20 个退役字段，出现即拒绝载入 |
| `canary-manifest.js` | canary 清单由文件系统派生，计数漂移在结构上不可能发生 |
| `readiness.js` | 启动 6 级就绪阶梯，单调前进 |
| `watchdog.js` | `advance({blocking})` 判据续期，`unref`，触发时 dump 最后进展 |
| `time-budget.js` | 全部 wall-clock 兜底的单一来源，逐条带理由（VERIFY-004） |
| `scripts/toml-format.mjs` | 行式 formatter，`gate:toml` 强制逐字节一致 |
| `scripts/budget-gate.mjs` | `gate:budget`，禁止 ≥1000 的计时字面量散落，无豁免通道 |
| `scripts/surface-inventory.mjs` | `gate:surface`，ARCH-010 合成文本 surface 清单由 sink 侧派生，双向检查 |

内容层（VERIFY-003）：

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

死边检查与 `internal`：

- 载入期计算可达性，不动点而非单遍——fork 链是真实的（Manager → Coder → 其子会话），
  可达边是已达 turn 的 `respond.args.prompt`
- prompt 由生产内部合成的 lane 用 `internal = true` opt out。它们不在可达性的论域内，
  不是「需要更聪明的可达性」。当前两例：Blogger（`CompanionHostBlogger.fs` 的
  `sendBloggerPrompt`）、Executor map 子会话（`ExecutorSummarize.fs` 的 `runExecutorPrompt`）
- `internal = false` 被拒绝。它读起来像「已检查且可达」，是该字段含义的反面
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

`manager-companion-canary.mjs` 在 `run-canary-staggered.mjs` 并发套件中偶发
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

- 位于 Git common directory 下私有 `wanxiangshu-next/runtimes` 路径，
  不在受测 workspace 创建 `node_modules` 或 `.wanxiangshu-next`
- Append 只有 Committed 或 CommitUnknown，没有部分写入（PERSIST-002）
- Projection 查询不扫描完整历史，必须 O(1) 积分状态（PERSIST-008）
- Pre-0.5.0 journal 不猜测迁移，启动发现旧 schema 直接失败（PERSIST-005）
- 外部副作用走类型化 Requested → 幂等执行 → Accepted（PERSIST-009；worktree=`WorktreeCreateRequested`/`WorktreeCreated`，publish=`PublishClaimed`/`Published`）
- 序列化时间戳必须归一化到 UTC offset（PERSIST-001）。否则同一事实在不同时区产出不同
  字节，快照指纹与跨机重放全部失效。`test` 跑三个时区正是为了逼出这一类
- Projection 的 create 类操作必须幂等（`createJob` 曾无条件覆盖，恢复重放会抹掉进度）

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

# CUT-39：advisor 专席去向 —— 裁决与抽取

返回 [看板](../BOARD.md)。来源：[CUT-38 §7.6 F38-01](CUT-38.md)。

- **裁决人**：eng-cut39-goose，2026-10-21，按仓主“完全自主决定”的授权。
- **裁决**：advisor 是现行产品功能（`todowrite-for-manager` 的 `this.advisor(intent, reportMapping)` 成员，WHAT 001–006），**不删除**。纯规则抽取到 `wanxiangli/advisor`（0.63.0）；角色/权限表、view 生成与渲染、专席 child 与派发留主仓（RETAINED）。审查中发现终态围栏的一个生产缺陷，登记为 F39-01；后由 `b9772a9f5` 修复（首次咨询），follow-up 余项 F39-02 由 `a8f431b` 修复（rebase 到最新 master 后落为 `e11fc96c5`）。

## A. 范围（亲眼核对，master `e6d490d7a`）

| 部件 | 职责 | 调用者 | 状态归属 | 裁决 |
|---|---|---|---|---|
| `Advisor/View.fs` `AdvisorView.plan` 的增量部分 | 先生成完整 view，再按已交付身份过滤输出；顺序、去重锚点、身份列表、`hasNewMaterial` | `ToolRegistry.advisorSeatAnswer`、`Advisor/Surface.plan`（测试面） | 无（纯） | **抽取** → `AdvisorLaw.plan` |
| `AnchorView.materialKey` | 材料身份 = 锚点 + 长度 + 原文 | Advisor 增量、`DeliveredValues.select`（handoff） | 无（纯） | **抽取** → `AdvisorLaw.materialKey`；`AnchorView.materialKey` 改为委托，两条消费链共用一个实现 |
| `Advisor/Cursor.fs` | 按调用 session 记录已成功交付的身份，单调，`clear`/`clearAll` | `ToolRegistry`（成功后 `record`）、Surface | 进程内 Dictionary | **抽取** → `AdvisorLaw.ledger()`（实例持有状态）；主仓只持有一个进程内实例 |
| `Advisor/Seat.fs` 的散文读回（`latestAssistantProse`） | 最后一条非空白 assistant 文本（角色大小写不敏感） | `AdvisorSeat.readProse` 的快照回退 | 无（纯） | **抽取** → `AdvisorLaw.answer`；宿主只把 Host message parts 拼成文本 |
| `Advisor/Seat.fs` 其余（child 建立、单飞、失败复位、两条 prompt 派发、终态订阅、围栏、120 s 兜底） | 物理句柄编排 | `ToolRegistry` | 进程内 seat 表与 fence 表 | **RETAINED**：宿主 I/O 与 Host run 身份，组合者职责（README §3）。围栏缺陷见 F39-01 |
| `AnchorView.generate`/`material`、`AnchorLedger`、reportMapping 解析 | 视图投影 | 请求、handoff、advisor | 锚点账本 | **RETAINED**（repository-programming / context-compression owner；覆盖律已在 `wanxiangli/compression`） |
| TOML 渲染（`SyntheticToml`）与 `lifecycle/advisor/view`、`role/advisor` 资源 | LLM 呈现 | View | — | **RETAINED**（格式 owner 与语言资源，门禁 `llm-facing-format-gate` 指定 View.fs 为格式 owner） |
| `Role.Advisor`、`OfficeCapability`、`ToolPermission.Advisor`、`JsCapability.Advisor`、`StaticTools`、成员注册 | 产品权限表 | 生成面、注册 | — | **RETAINED**：产品规则，不是可抽算法 |

候选删除清单：`Cursor.fs` 的 Dictionary 与手写单调并集、`View.fs` 的手写过滤与身份计算、`Seat.fs` 的 `assistantProse`、`AnchorView.materialKey` 的手写编码。都已删除（见 G）。

## B. 契约（`wanxiangli/advisor`，库 `src/advisor/AGENTS.md`）

- `materialKey(anchor, text)`：`锚点:UTF-16长度:原文`；锚点非负整数、文本为字符串，否则抛 `malformed`。
- `plan(delivered, material)`：输入是调用方已生成的完整 view（`{anchor,text}[]`）；输出 `delta`（view 顺序，不排序、不合并）、`deliveredAnchors`（整张 view 的去重锚点，首见序）、`deliveredMaterial`（仅 delta 的身份）、`hasNewMaterial`。无副作用；不推进账本。
- `ledger()`：新的空账本，实例互相隔离；`record(caller, keys)` 单调并集，空列表不建条目；`delivered(caller)` 按序号排序返回；`clear`/`clearAll` 是唯一的遗忘。进程内，与专席同寿，不持久化。
- `answer(messages)`：最后一条角色为 assistant（大小写不敏感）且文本非空白者，否则 `null`。
- 与现行行为的差异：无（差分见 H）。

## C. 反例（真实失败）

- **按编号去重会吞掉新摘要**：把身份改成只含锚点（变异 M02/W1），advisor-003 的“同锚点新摘要”“锚点 0 新开场”两条，以及 handoff 侧 repository-programming-059 的 4 条立刻变红——证明身份律被两条生产链共用，不能各写一份。
- **无长度前缀会碰撞**：`1` + `2:x` 与 `12` + `x` 在朴素 `锚点:原文` 编码下相同（库 SPEC 1 显式断言，变异 M01 被抓）。
- **终态围栏不可能命中（F39-01）**：`b9772a9f5` 已修复首次咨询，follow-up 余项 F39-02 已由 `a8f431b` 修复（rebase 到最新 master 后落为 `e11fc96c5`），见下节。

## D–F. 实现、npm 消费、接回真实链

- 库：`5bcace5`（模块 `src/advisor`，SPEC 1–4，`tests/001.test.mjs` 5 例）+ 发布 `7ed7df2`（**0.63.0**）。`build.test` 的独立编译清单加入 `advisor`，`exports`/`files`/`test` 同步。
- 主仓：gitlink → `7ed7df2`，lock 中 `wanxiangli` 版本 0.63.0。`Cursor`、`AdvisorView.plan`、`AdvisorSeat.latestAssistantProse`、`AnchorView.materialKey` 都经 `import { AdvisorLaw } from "wanxiangli/advisor"` 调用库。公开签名（`.fsi`）不变，Surface 与 `ToolRegistry` 不改。
- 接线证明（`evidence/CUT-39/wiring-mutants.out`）：在主仓实际加载的 `wanxiangli/dist/advisor/AdvisorLaw.js` 上做两次变异，主仓需求测试随之变红：W1 身份只含锚点 → advisor-003 2 条 + repository-programming-059 4 条失败；W2 plan 忽略已交付集合 → advisor-003 3 条失败。恢复后 22/22 通过。

## G. 旧 owner 退出

| 旧符号 | 去向 |
|---|---|
| `Cursor` 的 `cursors` Dictionary、`cursorOf`、手写 `record` 并集 | 删除；`Cursor` 只持有一个库账本实例并委托（签名不变） |
| `AdvisorView.plan` 的手写 delta 过滤与身份计算 | 删除；改为库 `plan`，主仓只做 view 生成与 TOML 渲染 |
| `AdvisorSeat.assistantProse` | 删除；`latestAssistantProse` 拼文本后交库 `answer` |
| `AnchorView.materialKey` 的手写编码 | 改为委托库；`DeliveredValues` 不改，仍经 `AnchorView.materialKey` |

没有持久数据：账本与专席都是进程内状态，重启即遗忘（WHAT 施工分期 2 已写明）。没有两个可写账本。

## H. 审查与集成

- **库测试**：`src/advisor/tests/001.test.mjs` 5/5。变异 14 个（`evidence/CUT-39/mutants.txt`：身份去长度、身份只含锚点、deliveredMaterial 取整张 view、锚点不去重、忽略已交付、delta 按锚点排序、hasNewMaterial 恒真、record 覆盖而非并集、clear 清空全部、answer 取第一条、角色大小写敏感、接受空白回答、接受任意角色、锚点接受负数）**全部被抓**。第一轮 M06（排序）存活，补强断言后被抓。
- **差分**（`evidence/CUT-39/differential.out`，`diff-harness.mjs`）：同种子 3000 组、7010 步的 plan→（70% 成功才 record）→delivered 序列，分别跑 base dist（`e6d490d7a`）与候选 dist 的 `Advisor/Surface`。结果 JSON **sha256 全等**（`7a63e536…`）。覆盖：有效 6834 步，follow-up 3837 步，部分增量 2667 步，无新材料 167 步，含锚点 0 168 步，未记录咨询 1511 步，非法声明 176 步（拒绝信息也逐字相同）。`AnchorView.materialKey`（base）对比库 `materialKey`：5000 组（含 emoji、CJK、空串）0 处不同。读回律：已删 F# 代码的参照转写对比库 `answer`，20000 组 0 处不同。
- **定向套件**（`evidence/CUT-39/suites.out`，64 个文件：advisor、capability-enforcement、context-compression、distribution、participant-identity、provider-projection、repository-programming、verification-system、work 等凡引用 advisor/Anchor/Delivered 的用例）：base 与候选都是 402 tests / 387 pass / 1 fail / 5 skip / 9 todo，唯一失败 `delegation-031`（两边相同，既有）。只在编译时持有 heavy.lock，测试不持锁、带超时。
- **库全量**（node 22）：853 + 15 个用例。失败 `build-001 sandbox`（0.61/0.62 上也失败，CUT-38 §7.7）。`pty-003` 在机器高负载并发时超时；本卡没有改 pty，单独复跑 3 次都是 12/12。三个 npm-pack 包测试在并发时会卡住（logs-buildinc NOTE），所以逐个跑：7/7、3/3、5/5。
- `node scripts/checks/architecture.mjs`：OK。
- **未做**：全量 `scripts/verify.mjs`、integration tier、format:check。
- **回退**：`git revert` 主仓接线提交，即回到手写版本（库 0.63.0 可以保留，没有别的消费者）。不涉及事件、持久数据或资源文本。

## F39-01（FIXED，首次咨询；follow-up 余项转 F39-02）：advisor 终态围栏永不命中

`Seat.fs` 的 `settleTerminal` 只接受 `result.ProviderRun = fence.ProviderRun` 的 Completed，而 `fence.ProviderRun` 有两种取值：第一次咨询是空串，之后是**上一次**咨询意图 prompt 的 `PromptKey`。`PromptKey` 是 `PromptAuthority.derivePromptKey` 生成的 sha256 摘要，`Completed.ProviderRun` 却是宿主 assistant 消息 id（HOST-010/011）。两者不在同一个身份域，所以真实的 Completed 永远命中不了：

- Aborted/Failed 立即拒绝；
- Completed 被当作“旧 run”忽略，咨询要等满 120 s 兜底，最后返回 `advisor did not answer`（成员信封 `advisor-unavailable`）。

另外，注释写的是“接受 fence 之后第一个 Completed”，代码却是“只接受等于 fence 的那个”，比较方向也是反的。这个缺陷由仓主 `60812f575`（看板外）引入。

本卡不修，原因有两条：
1. 要判定哪个 Completed 回答本次 intent，需要把 PromptKey 对应到物理 user message，再对应到 provider run，这是宿主派发语义；而 view prompt 与 intent prompt 在宿主上是一个 run 还是两个 run，本机无法观察。
2. advisor 包本身登记了证明缺口：001/004 没有 `Seat.consult` 用例（`requirements/advisor/tests/README.md`）。在没有宿主观察口的情况下改围栏，只能靠猜。

建议的方向（交 owner 裁决）：fence 记录**已结算的那个 Completed 的 ProviderRun**（assistant id）；新咨询接受第一个满足两个条件的 Completed：ProviderRun 与上次结算的不同，且其 authority root 或物理 user message 与本次 intent 被接受的那条一致。同时补上 001/004 的宿主替身用例。

**由谁接**：advisor 包 owner（delegation 泳道，CUT-13 后继）。

### F39-01 修复记录（eng-f39-goose / eng-f39b-goose，2026-10-21）

- **提交**：`b9772a9f5` `fix(advisor): bind the consult fence to the intent prompt's identity (F39-01)`。
- **做法**：
  - 围栏改为绑定 intent prompt 的身份。Completed 按 HOST-010/011 归属：assistant 消息（id = provider run）→ `parentID` 的 user 消息 → dispatcher 盖在其上的 `PromptKey`（快照 metadata 优先，退而用 journal 的物理落地）。只在归属到本次 intent key 时结算。
  - `PriorRun` 记录上一次咨询结算的那个 provider run（assistant id），它的重放或重复投递一律拒绝。订阅时的 sticky 重放发生在 `armed` 之前，只作观察。
  - **产品假设（如实登记）**：宿主不给归属证据时（无快照或找不到 assistant 消息），取 intent 被接受之后第一个新的 Completed；intent 被接受之前看到的 Completed 只在有归属证据时才算。
  - 新 seat 上 view 作为 awaited AgentOwnerRoot 发出，等宿主接受后再发 intent，否则 intent 会被认作第二个 root（`ActiveRunIdentityConflict`）。view 发送有 120 s 上界。
  - `OrdinaryTurnWorkflow`：advisor 专席没有工具、以散文交付，completed turn 就是普通终态，不再收 plan-continue nudge，也不等 `this.deliver` 见证。
  - `plugin-fixture` 的路由桩接受 `advisor` 角色。
- **证据**（`wt/logs-f39/`）：
  - 先红：`requirements/advisor/tests/004.test.mjs` 在 base `c768df811`（只带新测试和 fixture）上红（`base.fail`）。
  - 后绿：候选构建（`land1.build.log`，rebase 到 `d25e69894`、`--clean`）后，137 个文件的 advisor / repository-programming / delegation 等定向套件（`files.txt`）与 base 名单对比：`advisor-004` 首次咨询转绿，`delegation-031` 也已绿（上游修复）；唯一多出的名字是 delegation-027 两条 “prepared busy guidance … original active work”（EMR-010），它们在 master `da790ef5d` 上不带本修复同样失败（`logs-red-integ/integ2.log`），而在 `c768df811` + 本修复上通过（`oldfix.tap`），**不是本修复引入**。
  - 004 当时（F39-01 落地时，`t004.tap`）：3 tests / 2 pass / 0 fail / 1 todo。拒绝旧 run 的断言单独成一条强制用例：“the earlier consult's sticky Completed never answers a follow-up consult (stale-run rejection)”。**F39-02 落地后**，那条 `todo` 标记已去掉，004 现在是 3 条真实用例（见下节）；**本次没有跑过测试**，此处不给出任何通过/失败计数。
  - `check.mjs`：先 fantomas 后 check，候选与 master 的发现逐条一致（`check.master.out` / `check.cand2.out`，均 132 条，Seat.fs 未新增 pyramid）。`architecture.mjs` 在落地循环里通过。
- **回退**：`git revert b9772a9f5`。不涉及事件格式、持久数据或资源文本。

### F39-02（FIXED）：follow-up 咨询的 view prompt 没有 model-routing lease

同一 child 上第二次咨询时，上一次的 logical run 仍是 `ActiveLogicalRun`，所以 `SendManagedAssignment` 把 view 作为 `ManagedDelegationAssignment` continuation 追加到已经结束的 run 上。这个 provider step 没有已提交的 model-routing lease，transform 边界报 EMR-010（`SessionExecutionBinding.enterBoundProviderStep`；`tryContinuationAdmission` 只给 BusyAgentNudge/HumanMessage）。在 `c768df811` + 本修复上同样复现，所以与 master 的 delegation-027 EMR-010 回归无关。

- 用例：004 第三条 “a follow-up consult ignores the earlier consult and its own view run, and answers on its intent run”，原为运行中的 `todo`（`F39-02`），现已去掉 `todo`。

### F39-02 修复记录（eng-f39b-goose，2026-10-21）

- **提交**：`a8f431bd35877c32458e26c43288cd1776786a5b` `fix(advisor): settle each consult prompt as its own closed run (F39-02)`（rebase 前在本地 master 上的提交；rebase 到最新 master 后在 master 上落为 `e11fc96c5bcbf85e635666d4097b38e4a13d368a`；rebase 前的父提交 `9003c2032`；改动文件：`src/Wanxiangshu/Execution/Delegation/Advisor/Seat.fs` +161/-28、`requirements/advisor/tests/004.test.mjs` +32/-25）。
- **做法**：
  - **每次咨询的每个 prompt 都是自己的 authority run**。view prompt 自己 root 起一个 run（或续上自己的 run）；**它自己的终态**结束 view 阶段；随后 child 的 logical run 被**持久地**关闭——向 child 的 stream 追加既有的 `ExecutionFact.ChildRunVoided`；**只有在这之后**才发送 intent，于是 intent 自己 root 起一个新 run，拥有自己已提交的 model-routing lease。
  - **顺序是串行的**：view → 关闭 → intent。之所以不能在咨询结算后只补一次关闭，是因为 ModelRouting 每个 session 只保留一个活跃的物理执行，intent 的准入会取代 view 的 lease；此后再去驱动 view 那条消息，按设计就是 EMR-010（打在 view 的消息上）。
  - **每一种结局都会留下关闭的 run**：Completed、aborted/failed 终态、超时、view 被拒、发送失败、意外异常，都经同一个 settle 点（`runConsultAndVoid`）把 child 的 logical run 关掉，不留无法收尾的 run。
  - **不采用卡片原先偏好的持久 `AuthorityLogicalRunClosed`**：`Run.fs` 的 `expectedClosureKind` 把 `agentOwnerLife` 硬编码为 `null`，`ClosureLaw.expectedKind` 对 `AgentOwnerRoot, None` 返回 `None`，因此今天任何 AgentOwnerRoot 都无法通过 `admitClosure`；唯一的持久关闭写入方 `Persistence/Journal/Surface.fs` 的 `appendAuthorityLogicalRunClosed` 也只写 HumanRoot。要这样关闭一个 AgentOwnerRoot 属于登记 agent-owner life 的更宽产品变更，**登记为产品变更，不在本卡实现**。
  - **EMR-010 不变**：`tryContinuationAdmission` 仍只给 `BusyAgentNudge`/`HumanMessage`。修复靠给 follow-up 的 provider step 一条真正已提交的 model-routing lease，而不是让 continuation 蒙混过关。
  - **F39-01 围栏不变**：`PriorRun`/`settledRuns`/`isStaleRun` 的 stale-run 拒绝照旧；F39-01 的 HOST-010/011 归属与“先开新 root 再发 intent”的顺序不变。
- **用例**：advisor-004 现在有 3 条真实用例，全部顺序执行：F39-01 的 “a consult returns the prose of the advisor run that answers its own intent”；“the earlier consult's sticky Completed never answers a follow-up consult (stale-run rejection)”；以及原先标 `todo` 的第三条，已去掉 `todo`。
- **证据（诚实口径：仅静态检查；本机没有跑构建，也没有跑测试）**：
  - `dotnet tool run fantomas --check src/Wanxiangshu/Execution/Delegation/Advisor/Seat.fs` → exit 0。
  - `node scripts/check.mjs` → exit 1，**5 issues**，全部是 `fsharp-control-pyramid`，都在 `src/Wanxiangshu/OpenCode/Host/SessionSnapshotPort.fs`（444/473/629/648/715），**0 条在 `Seat.fs`**。
  - 与 base 的对比（base = 本次 rebase 的落点 `origin/master` `cbe198618`，独立 worktree 复测）：`node scripts/check.mjs` → exit 1，**同样 5 issues**，逐条 file:line **完全一致**（pyramid 发现集合 diff 为空）。**没有新增 issue。**
  - `node scripts/checks/architecture.mjs` → exit 0（architecture: OK）。
  - **本机没有 `dotnet build`，也没有跑任何测试**：编译槽位不可用（`build-slot node scripts/build.mjs` → exit 75，排队超过 3 分钟），且本 clone 的 `dist/` 是 **F39-02 之前**的陈旧产物（`dist/…/Advisor/Seat.js` 里没有 `closeAfterView`/`ConsultPhase`）。因此**不得**声称任何一次测试通过；上面的结果只是静态检查。
    - 附带事实（如实记录）：在**陈旧 dist** 上直跑 `node --test requirements/advisor/tests/004.test.mjs` → 3 tests / 0 pass / 3 fail，全部报 `EMR-010: managed provider step … has no committed model-routing lease`。这**不能**作为本修复的验收结论：那份 dist 根本不含本修复的代码。该用例文件里的 `todo` 标记已在 `a8f431bd` 中去掉，源码层面已无可跳过的占位。
- **回退**：`git revert e11fc96c5bcbf85e635666d4097b38e4a13d368a`（rebase 前写作 `git revert a8f431bd35877c32458e26c43288cd1776786a5b`）。若回退，004 第三条需恢复 `todo` 标记。
- **不并入 F39-02**：master 上的 delegation-027 “prepared busy guidance … original active work” ×2 仍是**另一条未二分的既存回归**，由 integ-027 跟踪，不属于本卡，也不是本卡的验收项；`requirements/delegation/tests/027.test.mjs` 未被本卡触碰。

## 状态

CUT-39 `DONE`：纯规则只有 `wanxiangli/advisor` 一个 owner，其余各项已登记 RETAINED，并有后继项 F39-01（已由 `b9772a9f5` 修复）与 F39-02（已由 `a8f431bd35877c32458e26c43288cd1776786a5b` 修复，rebase 到最新 master 后落为 `e11fc96c5bcbf85e635666d4097b38e4a13d368a`）。这不是“advisor 已抽干”：专席编排与围栏仍在主仓。master 上 delegation-027 prepared-busy-guidance 的 EMR-010 回归仍未二分，另案跟踪，不属于本卡。

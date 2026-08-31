# Upstream 剩余语义合并计划

> 稳定引用：`docs/UPSTREAM-REMAINING-MERGE-PLAN.md`
>
> 产品语义只由 `requirements/<package>/` 定义。本文件规定迁移边界、PR 拆分、验证节奏与执行 prompt；实际提交、PR、验证结果写入 [upstream-refactor-semantic-merge-2026-08-31.md](./upstream-refactor-semantic-merge-2026-08-31.md)。

## 1. 基线与范围

- 计划基线：`upstream/master@1db90f5e8`。
- 最近执行基线：`upstream/master@fcd5ab11b`（2026-08-31 fetch；检查器拆为 text/FCS lanes，并引入 Wireit 缓存）。
- 已合并成果：PR #19，merge commit `1db90f5e8`。
- 旧成果保留分支：`codex/pre-upstream-refactor-20260831@4bb19673e`。
- 当前集成分支与 upstream tree 无差异。
- 旧分支的提交不得整段 replay 或粗暴 cherry-pick。每项先由当前 WHAT 判定，再迁入当前唯一 owner。
- 386-file production coverage backlog 是独立 ReleaseClosure 工作，不进入本计划。

## 2. 剩余模块

| 模块 | 旧证据 | 当前缺口 | 目标 PR |
|---|---|---|---|
| M0 CI/Linux baseline | PR #19 check | Linux unit 出现集中超时与 `bun-pty` ESM import 差异 | 因果修复 CI portability；禁止整体放大 timeout |
| M1 JS transaction | `d87cdd59e` | read-only snapshot 未参加统一 preflight；create race 与 rollback 可覆盖第三方写入 | immutable read snapshot、commit conflict、CAS-safe rollback |
| M2 Change publish | `ff223e798`—`b5e95e1f0` | 未证明 publish gate scope 与 stale witness 被丢弃 | observable gate capability、fresh witness、ff-only CAS proof |
| M3 Enforcer bounds | `40136ef0a` | Decode、Host、Surface 重复维护 limits/formula | Model 成为唯一 decision owner |
| M4 causal-wait | `2a9d7c803`—`e3bdfef7a` | Surface 用布尔声称能力隔离；gate 可漏扫 | opaque observer/reader capability；全树 fail-closed gate |
| M5 ambient-time | `e13738ce4`—`92b1b1896` | 选定目录扫描；missing root 可静默通过；排除过宽 | 全 production tree、exact-file exception、missing-root failure |
| M6 Host boundary | `c11511e98`、`0b87de296` | snapshot locality 与 signal closed set 只被弱分类覆盖 | exact/missing/ambiguous location；完整 typed signal shape |
| M7A Handle/fold/join mirror | `91503a3f1` 及当前 support consumers | 测试重建 handle projection、fact fold、journal/join 决策 | 调用注册 production Surface；删除对应 mirror exports |
| M7B Fork/family/terminal mirror | 同上 | 测试重建 fork、family cascade、terminal policy | 迁到当前 delegation/session owner |
| M7C Satellite/Distiller mirror | 同上 | 测试重建 satellite 与 distiller lifecycle | 迁到当前 recovery/ownership owner |
| M7D SyncDelegate mirror | 同上 | 测试重建 SyncDelegate lifecycle | 迁到当前 SyncDelegate owner |
| M7E PTY/process mirror | 同上 | 测试重建 PTY/process lifecycle | 迁到当前 process-execution owner；consumer 归零后删除 support 文件 |
| M8 requirement trace AST | `a5cdf4c02` 等 | tokenizer 不能可靠证明 test binding identity | 保留现有 graph/HOW/level 规则，只升级 binding analysis |
| M9 Surface Manifest AST | `96625a5e5`、`85469391e` | shadow、dead helper、其他 law use 可制造假因果 | exact import provenance 与 primary test callback terminal use |
| M10 fast-check pilot，可选 | `743cab7be`、`3224331a7`、`b3039fe85`、`2760853e4` 等 | 现有 deterministic suite 对少数大状态空间缺少 shrinking | 仅选择 1—2 个 production-bound property；证明新增价值后引入直接依赖 |

## 3. 明确不迁移

- PR #19 已迁移的 durable handle identity、dispatch ingress、Host chronology、review witness、blank evidence、Concern、Casebook。
- Strength DryRun 旧 harness。当前 AGENTS.md 已记录 Strength production、proof、gate 闭环；旧 harness 依赖旧 Runtime。
- `989b270fd` 删除 HOW exact proof anchors。
- `954e14fd7`、`6b5a047be` 退役当前仍受 gate 约束的 migration ledger。
- 旧 ProofGraph、RequirementSync、completion gate 删除链。
- 旧 fast-check/property 试点整批回放。M10 只允许保留能消灭新错误世界的性质。

## 4. 执行批次、顺序与 PR 边界

一个批次对应一次完整工作 prompt。批次减少重复调查与全量验证；模块仍保留独立 RED/GREEN/closure commit。只有共享同一 proof boundary 或同一待删除 support 文件的模块才进入同一 PR。

### 第 1 次：CI 可信基线

- 模块：M0。
- PR：一个独立 PR。
- 原因：后续 PR 必须先有可信 Linux verdict。不得让语义迁移与 CI portability 混在一起。
- 批次出口：upstream CI 全绿；合并后在最新 master 运行一次 `npm run format-build-test`。

执行事实（2026-09-01）：本地实现成功。最新 upstream 基线上重放后的节点为 RED `c3a39623f`、GREEN `2dbf3c179`、consumer closure `904c0be8a`、记录 `fa0003296`；定向 21/21、Fable build 734 sources / 161 surfaces、text gate 全绿。原基线完整阶梯为 3925/3925、integration/e2e/package 全绿。已随 M3 push 到 [upstream PR #20](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/20)。旧 GitHub verdict 来自 base 刷新前的 merge ref；本记录节点触发 `upstream/master@fcd5ab11b` 上的新 CI。merge SHA 尚未产生，因此第 1 次尚未进入 upstream，批次外部出口未闭合。

### 第 2 次：最小 production owner 收敛

- 模块：M3。
- PR：一个独立 PR。
- 原因：改动小、单一 owner、无前置依赖；先验证新的提交、说明和 review 节奏。
- 批次出口：Decode、Host、Surface 不再拥有 bounds 常量或公式。

执行事实（2026-09-01）：本地批次出口已闭合。RED `4b87c3c0a` 证明原行为测试全绿时三个 consumer 仍可复制 owner；GREEN `eb0e703d6` 将阈值、计数结果、typed rejection 与拒绝公式收敛到 `EnforcerCycle.validateContentBounds`；FCS closure `787a877d6` 声明 injected `byteCount` 的 exact-two trace。behavior-diagnosis 149/149、Fable build、text lane（772 WHAT / 3920 tests）与完整 owner-dep lane 全绿。PR preflight 刷新 Wireit 依赖后，正式阶梯暴露 upstream `fcd5ab11b` 遗留的两条 direct-command oracle（3924/3926）；`45b20b63e` 同时锁定顶层 npm ladder与 Wireit exact commands，focused verification/distribution 17/17。修正后正式入口又由 freshness gate 暴露 Wireit build 未 fingerprint repository-derived envelope；`51fff4bd8` 将整个 workspace 纳入 build cache key，仅排除自身输出，focused proof 11/11 且正式 build 734 sources / 161 surfaces。最终完整阶梯 3927/3927、integration/harness/package/e2e/pack 全绿；已创建 [upstream PR #20](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/20)。当前节点只刷新 GitHub 对最新 base 的 verdict，不改变实现；merge SHA 待产生。

最新 base 的 run `33424153110` 已通过 3928/3928 unit，随后暴露 FCS production/reuse/fixture 三段共用单一 verdict 的 Linux 静默窗口缺陷。修正保持 180s 单段预算与 185s watchdog 不变，只让三个完成的真实因果阶段分别产生 verdict；本地联网正式用例 5/5，最长 production scan 109.6s。production、scanner、schema、断言与 fail-closed 路径均未修改。

run `33426540260` 已验证 owner lane 修复：5/5，Linux production scan 146.8s；随后旧 workflow scanner 聚合文件在 185056ms 触发同类静默拒绝。`764c6a2cb` 将三个彼此独立的 scanner 精确拆成顺序 entry；预算、watchdog、scanner、production 与断言均不变。该 CI portability 修复从第 3 次下沉到 #20，保证最底层 PR 可独立合并。

累计 #21 run `33426894259` 进一步否证 nested verdict 方案：process-isolated file 在 185100ms 仍报告 0 blocking progress，说明 Node 20 只在 file wrapper 退出时交付 leaf 结果。最终方案把 owner fixture、production evidence+reuse、explicit-project isolation 拆成三个物理顺序文件；没有增加预算、重复 production scan或改变断言。

#20/#21 runs `33429721220` / `33429748255` 又发现 `owner-dependencies-reuse.test.mjs` 实际仍启动第二次完整 production scan，Linux 上超过 185s。最终闭环改为阶梯内 single production scan：owner-dep 原子产出 schema v3 normalized evidence；SHA-256 fingerprint 绑定 normalizer、scanner、project、dependency lock 与完整 compile set 内容；integration 读取 exact run-id 并验证 fingerprint 后消费。缺失/stale/schema/run-id/fingerprint/compile-set 错误全部 fail closed。快速合同 56/56、真实 owner-dep 全绿、三项 integration 5.38s 全绿；timeout 不变。

#21 run `33432246221` 继续暴露 artifact 只在 reuse proof 内设置，后续 semantic-decorator 子进程仍重复 production scan 并在 185077ms 失败。最终修复把 exact evidence path/run-id 提升到 integration orchestrator 环境；全部默认 production consumers 都复用并验证同一 artifact，explicit fixture project 保持独立扫描。focused composition/plugin/decorator/fixture 3/3，总计 7.72s；正式 integration orchestrator 全绿，harness 273/273。不拆测试掩盖耗时，不增加 timeout。

### 第 3 次：production-bound proof 加固

- 模块列表：
  1. M5 ambient-time。
  2. M4 causal-wait。
  3. M6 Host boundary。
- PR：默认一个 PR，内部按 M5 → M4 → M6 保留三组独立提交。任一模块若需要产品语义修改或显著扩大 diff，立即拆成独立 PR。
- 原因：三项都不改变业务规则；共同目标是把弱分类、漏扫与假 capability proof 改成 fail-closed production-bound proof。M5 先固定全树 scanner 纪律，M4 建 typed capability，M6 复用该 proof 方式。
- 批次出口：集中运行一次 build、check、三个 focused requirement suites 与完整阶梯。

### 第 4 次：JS transaction correctness

- 模块：M1。
- PR：一个独立 PR。
- 原因：涉及 snapshot、commit race 与 rollback，错误会破坏第三方数据；不与其他行为改动混合。
- 批次出口：read-only conflict、create race、CAS-safe rollback 三类 counterworld 全部绑定 production transaction。

执行事实（2026-09-01）：本地 M1 行为闭合后，累计 Linux production-evidence run 发现本 PR 新增的 `writeCreate` 会在失败分类中再次调用注入的 `resolvePath`。`847850587` 未用 trace contract 掩盖，而是将 commit/rollback 逻辑路径一次解析成 private typed mutation，使 preflight、逐项重验、write、failure classification、CAS rollback 共用 exact resolved path。Fable build、13/13 transaction proofs、真实 owner production scan+reuse（107.5s）与 format 全绿；修复需传播到第 5 次累计头后重新取得 #22/#23 verdict。

#22 run `33432275199` 又发现 upstream 原有 requirement-grounding proof 依赖 `ToolWorkflow.fs` 的源码字符串顺序，等价的 resolved-path 重构使其误红。`28d3d5d39` 下沉累计第5次已验证的 production counterexample：真实 grounding observer 先记录完整 effect set 再主动失败，repository transaction 仍提交并返回 `Succeeded`。Fable build 与 focused 21/21 全绿；没有恢复旧排版或削弱行为断言。

### 第 5 次：Change publish correctness

- 模块：M2。
- PR：一个独立 PR。
- 原因：与 M1 同属 stale state/CAS 问题，但 owner、transaction boundary、恢复语义不同。先吸收 M1 的调查结论，再独立 review。
- 批次出口：只有 fresh witness 可进入 publish gate；review/repair 在 gate 外；ff merge 使用 gate 内 fresh expected head。
- 波次出口：M1、M2 均合并后，在最新 upstream 统一运行一次完整阶梯。

### 第 6 次：mirror 清理基线

- 模块：M7A。
- PR：一个独立 PR。
- 原因：Handle/fold/join 是剩余 mirror 的基础；先建立 Surface 迁移范式并测量 consumer/export 基线。
- 批次出口：对应 mirror exports 删除，测试直接调用当前 owner。

执行事实（2026-09-01）：本地 M7A 已闭合。RED `17d7b517c` 将 `recordAbandon` 首胜 proof 从 fake journal/controller 改为要求 production resource surface；GREEN `57cdf8feb` 增加 opaque `Handle/JournalSurface`，内部调用 canonical EventStore、AgentJournal 与 HandleController；consumer closure `fe2536b20` 将 abandonment、join guard、creation order、hidden recovery、codec 与 fold proofs 迁到注册 production surfaces，删除 291 行 projection/fold/codec/journal/controller/join mirror。managed-session-lifecycle 165/165、build、check 全绿；已创建 [upstream PR #24](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/24)。当前账号无 upstream merge/rerun 权限，须由 owner 按 #20→#24 合并。

### 第 7 次：child lifecycle mirrors

- 模块列表：
  1. M7B Fork/family/terminal。
  2. M7C Satellite/Distiller。
- PR：默认一个 PR，按模块保留独立提交。
- 原因：两项共享 child ownership、terminal propagation 与同一个 support 文件；合并处理可避免反复改变 mirror 数据模型。
- 批次出口：相关 consumers 全部迁移，相关 exports 删除，剩余计数写入历史记录。

执行事实（2026-09-01）：本地 M7B/M7C 已闭合。TerminalPolicy 与 Satellite 均先保留缺失 production surface 的 RED，再通过注册 F# surface 调用当前 owner；删除 34 条 fork/family 常量断言、Satellite JavaScript 状态机、Distiller 常量结果与无 production 路径的 semantic-cut literal。managed-session-lifecycle 125/125、build（737 sources / 164 surfaces）、check（772 WHAT / 3900 tests）全绿。只剩 SyncDelegate/PTY 两个 mirror export。发现 upstream 既声明 Retired 绝对不可逆，又以 production test 要求 exact retired binding 为新 work unit 重开；本批不擅自裁决，详见 [batch 7 记录](./upstream-remaining-merge-batch-7-2026-09-01.md)。已创建累计 [upstream PR #25](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/25)，merge SHA 待 owner 产生。

### 第 8 次：execution adapter mirrors 与最终删除

- 模块列表：
  1. M7D SyncDelegate。
  2. M7E PTY/process。
- PR：默认一个 PR，按模块保留独立提交。
- 原因：两项是最后的 execution adapter consumers；共同完成 support 文件删除。M7D 必须先于 M7E。
- 批次出口：imports/exports 均为零，删除 `requirements/managed-session-lifecycle/tests/support/managed-surface.mjs`；在最新 upstream 运行完整阶梯。

执行事实（2026-09-01）：M7D/M7E 已闭合。SyncDelegate 五条 lifecycle proofs 现执行真实 journal-backed runtime；Host PTY 十一条 proofs 现执行真实 `HostForkRuntimePty`。最后两个 mirror exports 及整个 `managed-surface.mjs` 已删除。深层 owner lane 又拒绝了初稿的两个未分类 mutable resources 与 domain 路径中的 Host adapter；已补 exact resource classification，并把 `SatelliteSurface` 物理迁到 `OpenCode/Host`，未加例外。最终完整阶梯：Fantomas 700 unchanged、build 738 sources / 165 surfaces、unit 3904/3904、全部 integration/package、Long Stroke 与 pack（2019 files）全绿。已创建累计 [upstream PR #26](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/26)，merge SHA 待 owner 产生；详见 [batch 8 记录](./upstream-remaining-merge-batch-8-2026-09-01.md)。

M7A → M7B → M7C → M7D → M7E 必须串行。任何迁移不得在测试中复制 decision、fold、formula 或状态机；只能调用注册 production Surface。

### 第 9 次：共享 AST binding analyzer

- 模块列表：
  1. M8 requirement trace AST。
  2. M9 Surface Manifest AST。
- PR：默认一个 PR；shared analyzer、M8 consumer、M9 consumer 各自独立提交。
- 原因：M9 依赖 M8 的 binding analyzer；分开开发会产生短期重复 analyzer 或未消费基础设施。
- 批次出口：保留现有 graph、HOW anchor、proof level、symlink/inactive、consumer authority 与逐 law 规则；shadow、dead alias、错误 callback 与其他 law decoy 全部稳定变红；运行完整阶梯。

执行事实（2026-09-01）：M8/M9 实现闭合。`37ece7962` 先固定 unbound/shadowed/indirect `node:test` 与 shadow/dead-helper/decoy-law surface 假绿；`f69f4d480` 引入唯一共享 Acorn syntax core；`ff0cda20f` 将 requirement trace 切到 binding-aware AST 并把 15 处动态注册迁成静态命题；`024684299` 将 Surface Manifest 切到 lexical provenance + primary callback terminal use，并迁移 14 个脱钩 proof。迁移同时纠正两个既有登记错误：`ReconcileSurface` 的行为 law 为 `STRUCTURED-WORKFLOW-004`；`ReviewTodoSurface` 的 production owner 为 `review-judgement`，其实际跨 owner law 为 `EFFECT-ACCOUNTING-011`。聚焦验证：requirement trace 19/19、surface charter 19/19、受影响行为 121/121、772 WHAT / 3901 tests、165 surfaces、build/check 全绿。无缓存完整阶梯亦全绿：Fantomas 700 unchanged、owner lane 27,218 FCS uses / 333 edges / 185 contracts、Fable 738 sources / 165 surfaces、273/273 integration harness、Long Stroke 57 步 / 5.8s、pack 2019 files。累计 PR 记录见 [batch 9 记录](./upstream-remaining-merge-batch-9-2026-09-01.md)。

### 第 10 次：可选 property pilot

- 模块：M10。
- PR：一个独立可选 PR。
- 前置：M1—M9 已合并；负责人已确认依赖、property、CI 预算与 seed/path 保存形式。
- 范围：只选 1—2 个 production-bound property。候选优先考虑 prefix mutation、completion/fallback interleaving；不得用 fast-check 重建 production oracle。

## 4.1 中断与拆分规则

- 一个组合批次内，前一模块已闭合、后一模块显著膨胀时，立即提交前一模块 PR；不让简单成果等待难项。
- 两个模块只是文件相邻但 owner、WHAT 或失败世界不同，不合并 PR。
- 组合 PR 中每个模块必须有独立 proof 与 commit；一项失败不得由另一项测试代偿。
- 每次开始前 fetch upstream；每次 PR 前记录实际 base SHA。禁止长期 stacked PR。

## 5. 单个模块的 Git 与验证闭环

行为模块保留以下历史节点：

1. `spec/test(...): expose ...`：更新必要的 WHY/WHAT，加入调用 production Surface 的失败 proof。
2. `fix/refactor(...): ...`：修改唯一 production owner，使 RED 变绿。
3. `docs/verification(...): close ...`：更新 HOW、实际验证、upstream 修改理由与剩余边界。

proof/gate 模块采用 RED fixture → analyzer/gate GREEN → 文档闭合。不得用 suppression、allowlist、baseline 增长、删测试或弱化断言换绿。

验证节奏：

- 小编辑不跑全量；完成一组相关改动后跑 focused file/package suite。
- 每个模块完成时只跑对应 focused suite；到达 PR 边界后集中运行一次 `node scripts/build.mjs` 与 `node scripts/check.mjs`。
- 每个 PR 以 GitHub CI 提供独立完整验证。
- 只在第 1、3、5、8、9 次标记的批次出口，于最新 upstream 单次运行 `npm run format-build-test`。
- 最终 PR 前再次 fetch upstream、细粒度语义合并、完整阶梯、diff/status 审查。

## 6. 执行记录

本文件不维护第二套完成状态。模块是否完成只由以下事实决定：

- RED/GREEN/closure commit；
- upstream PR URL 与 merge SHA；
- GitHub CI；
- focused/build/check/full-ladder 实际结果；
- 被删除的 mirror consumer/export；
- 对 upstream 原文件的修改、原因与反例。

这些事实追加到 [upstream-refactor-semantic-merge-2026-08-31.md](./upstream-refactor-semantic-merge-2026-08-31.md)。未出现 merge SHA 的模块仍未进入 upstream。

## 7. 每一步 prompt 需要的额外输入

最小 prompt 只需提供：

1. 批次编号及模块列表，例如“第 7 次：M7B、M7C”。
2. 本次授权边界：只实现并提交；或同时 push、创建 upstream PR。
3. 若从中断点恢复：当前 branch、最后一个可信 commit、已完成但未提交的事实。
4. 只有存在产品歧义时，提供 owner/负责人裁决。无歧义时 Agent 必须从 AGENTS.md、WHAT、HOW、源码和 Git 历史自行调查。

不应复制到 prompt 的内容：owner 路径、预期实现细节、测试文件列表、旧 patch。复制这些内容会过早锁死旧结构，并让 Agent跳过当前语义调查。

外部动作需要显式授权：

- fetch/read upstream：按当前环境权限执行；网络授权失败时申请许可。
- push、创建或修改 PR：prompt 必须明确授权。
- 删除远端分支、强推、覆盖历史：本计划不授权。
- M10 引入 npm 依赖：必须单独确认。

## 8. 推荐执行 prompt

> 执行 `docs/UPSTREAM-REMAINING-MERGE-PLAN.md` 的第 N 次，模块列表为 `[Mx, My]`。完整阅读仓库根 AGENTS.md、本计划、对应 `requirements/<package>/` 的 WHY/WHAT/HOW，以及历史合并记录。先 fetch 并以最新 `upstream/master` 为基线，重新确认每个缺口仍存在；不得粗暴 cherry-pick 旧提交。严格按本计划的模块顺序与 PR 边界执行。每个模块独立完成 why → what → RED production-bound proof → 唯一 owner 实现 → HOW/GAP closure，并保留可 review 的 RED/GREEN/closure Git 节点。每个模块完成后运行对应 focused suite；到达组合 PR 边界后集中运行一次 build 与 check；只在本计划指定的批次出口运行完整阶梯。记录对 upstream 原文件的每项修改、原因、反例和验证结果。范围限于列出的模块；发现相邻问题只记录，不扩张。若后一模块显著膨胀，按 4.1 先提交已闭合模块。本次授权为：[只准备本地提交／同时 push 并创建 upstream PR]。

恢复中断任务时，在末尾补充：

> 从 branch `[branch]`、commit `[sha]` 继续。先验证工作区与历史记录，不重复已经由 commit 证明完成的步骤。未提交改动的来源与预期为：`[facts]`。

## 9. M10 专用补充输入

M10 除通用 prompt 外还需负责人确认：

- 是否接受新增直接 npm dependency；
- 选择哪 1—2 个 property；
- CI 时间预算；
- 失败 seed/path 的保存形式。

未收到这四项裁决，M10 保持可选，不阻塞 M0—M9。

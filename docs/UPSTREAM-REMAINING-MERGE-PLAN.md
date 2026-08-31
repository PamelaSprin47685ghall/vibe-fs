# Upstream 剩余语义合并计划

> 稳定引用：`docs/UPSTREAM-REMAINING-MERGE-PLAN.md`
>
> 产品语义只由 `requirements/<package>/` 定义。本文件规定迁移边界、PR 拆分、验证节奏与执行 prompt；实际提交、PR、验证结果写入 [upstream-refactor-semantic-merge-2026-08-31.md](./upstream-refactor-semantic-merge-2026-08-31.md)。

## 1. 基线与范围

- 计划基线：`upstream/master@1db90f5e8`。
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

## 4. PR 波次与依赖

### Wave 0

1. M0。
2. 合并后重新 fetch upstream。
3. 在合并后的最新 master 运行一次 `npm run format-build-test`。

### Wave A：production correctness

1. M3：小范围单一 owner，先验证 PR 节奏。
2. M1：JS transaction。
3. M2：Change publish。
4. 三项全部合并后，在最新 upstream 统一运行一次完整阶梯。

M1、M2、M3 没有语义依赖。每个分支从创建时的最新 upstream 开始；不建立长期 stacked PR。

### Wave B：proof/gate hardening

1. M4。
2. M5。
3. M6。
4. 三项全部合并后统一完整验证。

### Wave C：测试镜像清零

按 M7A → M7B → M7C → M7D → M7E 串行。它们共同修改 `requirements/managed-session-lifecycle/tests/support/managed-surface.mjs`，禁止并行编辑。

每项必须：

1. 定位当前 production owner 与注册 Surface。
2. 先增加 production-bound RED；不得在测试中复制 decision、fold、formula 或状态机。
3. 只增加最窄观察能力。
4. 迁移该组 consumer。
5. 删除该组 mirror exports。
6. 证明剩余 imports/exports 数量下降；M7E 后必须归零并删除 support 文件。

### Wave D：验证器升级

1. M8 提供共享 JS syntax/binding analyzer。
2. M9 复用 M8，只增强 Surface binding causality。
3. 保留当前 HOW exact anchor、proof level、symlink/inactive、consumer authority 与逐 law 规则。
4. 两项合并后统一完整验证。

### Wave E：可选 property pilot

M10 仅在 M1—M9 稳定后评估。候选性质优先考虑 prefix mutation、completion/fallback interleaving；不得用 fast-check 重建 production oracle。

## 5. 单个模块的 Git 与验证闭环

行为模块保留以下历史节点：

1. `spec/test(...): expose ...`：更新必要的 WHY/WHAT，加入调用 production Surface 的失败 proof。
2. `fix/refactor(...): ...`：修改唯一 production owner，使 RED 变绿。
3. `docs/verification(...): close ...`：更新 HOW、实际验证、upstream 修改理由与剩余边界。

proof/gate 模块采用 RED fixture → analyzer/gate GREEN → 文档闭合。不得用 suppression、allowlist、baseline 增长、删测试或弱化断言换绿。

验证节奏：

- 小编辑不跑全量；完成一组相关改动后跑 focused file/package suite。
- 一个模块准备 PR 前，集中运行 focused suite、`node scripts/build.mjs`、`node scripts/check.mjs`。
- 每个 PR 以 GitHub CI 提供独立完整验证。
- 每个 Wave 合并后，在最新 upstream 单次运行 `npm run format-build-test`。
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

1. 模块编号，例如 M1。
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

> 执行 `docs/UPSTREAM-REMAINING-MERGE-PLAN.md` 的 Mx。完整阅读仓库根 AGENTS.md、本计划、对应 `requirements/<package>/` 的 WHY/WHAT/HOW，以及历史合并记录。先 fetch 并以最新 `upstream/master` 为基线，重新确认缺口仍存在；不得粗暴 cherry-pick 旧提交。按 why → what → RED production-bound proof →唯一 owner 实现→HOW/GAP closure 执行。保留可 review 的 RED/GREEN/closure Git 节点。小步骤只跑 focused tests；模块完成后运行 build、check 与对应 requirement suite。记录对 upstream 原文件的每项修改、原因、反例和验证结果。范围限于 Mx；发现相邻问题只记录，不扩张。本次授权为：[只准备本地提交／同时 push 并创建 upstream PR]。

恢复中断任务时，在末尾补充：

> 从 branch `[branch]`、commit `[sha]` 继续。先验证工作区与历史记录，不重复已经由 commit 证明完成的步骤。未提交改动的来源与预期为：`[facts]`。

## 9. M10 专用补充输入

M10 除通用 prompt 外还需负责人确认：

- 是否接受新增直接 npm dependency；
- 选择哪 1—2 个 property；
- CI 时间预算；
- 失败 seed/path 的保存形式。

未收到这四项裁决，M10 保持可选，不阻塞 M0—M9。

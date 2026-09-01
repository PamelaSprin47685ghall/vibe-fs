# Upstream 重构后的语义合并记录

> 日期：2026-08-31
> 集成分支：`codex/upstream-refactor-integration`
> 上游基线：`upstream/master@4e5789e1c`（PR 前仍会最后 fetch 确认）
> 本地旧版本：`codex/pre-upstream-refactor-20260831@4bb19673e`
> 性质：合并与 review 记录；产品语义仍只由 `requirements/<package>/` 定义
> 后续执行入口：[UPSTREAM-REMAINING-MERGE-PLAN.md](./UPSTREAM-REMAINING-MERGE-PLAN.md)

## 1. 合并原则

本次不合并两棵目录树，只迁移仍成立的语义：

1. 以 `696e67cad` 的 owner、类型、Surface、HOW 精确 proof 锚点和 gate 为结构真相。
2. 先用当前 WHAT 判断旧改动是否仍成立，再定位重构后的唯一 production owner。
3. 缺陷修复保留 proof-only RED 与 owner GREEN 两个节点；重构或文档修复保持单一可验证节点。
4. 禁止回放会恢复旧架构、平行 owner、测试镜像、宽泛 allowlist 或第二套完成状态的提交。
5. 暂时无法安全迁移的成果保留在旧分支并记录原因，不用兼容 facade 把两套结构粘在一起。

## 2. 已完成的上游基线对齐

老板的重构改变了 composition trace、recovery owner、authority/chat 类型和 proof 路径。直接合并前，本分支先修复了重构后自身的机械漂移：

| 节点 | 内容 | 结果 |
|---|---|---|
| `bf8759b66` | 补齐重构 DSL 的 semantic ownership | architecture gate 识别全部 production owner |
| `7443679b5` | 注册 chat witness/receipt 新类型 | authority 类型进入正式 owner graph |
| `40b550fd6` | 清除重构引入的 F# control pyramid | 零基线恢复 |
| `88f8869a8` | 按新角色结构重建 ToolRegistry trace | exact trace，无 stale/new/drift |
| `9a44b3bab` | 按新 recovery 结构重建 PluginTransforms trace | exact trace，无 stale/new/drift |
| `c67ac047f` | 按新 chat 结构重建 HostSignalBootstrap trace | exact trace，无 stale/new/drift |
| `b7f1b0d3c` | recovery gate 跟随 extracted fallback owner | gate 证明当前调用链，不再证明旧直连路径 |
| `d68b8bba4` | 删除旧 fallback proof facade，能力归入 CursorSurface | 单一 proof surface |
| `e69291a02`—`911ca2c34` | 重连 recovery、delegation、authority、failure、Host、chat 的精确 HOW 锚点 | requirement trace 闭合 |

基线实测：

- `node scripts/build.mjs`：734 个 F# source、161 个 registered surface，成功。
- `node scripts/check.mjs`：全部 gate 通过；696 个 production file 全部有唯一 primary owner；F# control-pyramid、dead private binding、JS semantic-boundary debt 均为 0。
- requirement trace：772 WHAT、3919 executable test declarations，closure complete。
- managed chat requirement suite：92 pass / 0 fail。
- Host admission canary：OpenCode 1.18.18 真实边界通过。

## 3. 旧改动语义映射

### 3.1 已被重构吸收或替代

| 旧改动 | 重构后的结论 | 处理 |
|---|---|---|
| completion race、prefix mutation、routing capacity、Host transform detector 的早期 property/mutation 试点 | 当前仓库已有对应 production decision、composition/architecture gate 与 deterministic property suite；路径和 vocabulary 已重写 | 不 cherry-pick；最终验证只补发现的缺口 |
| dispatch durable acceptance 的旧 Journal/Ingress surface | managed chat atomic acceptance、authority receipt 与 recovery owner 已重构 | 不恢复旧 surface；只迁移仍缺失的 physical ingress codec 语义 |
| recovery fallback `HandleSurface` | recovery owner 已提取为 cursor/fallback flow | 已删除旧 facade，proof 能力归入当前 `CursorSurface` |
| 旧 owner 注释、closure carrier、GAP CLOSED 状态 | semantic owner 与 closure 已由当前 gate/ledger 定义 | 不恢复旧 carrier |

### 3.2 与当前框架冲突，明确不迁移

| 旧提交/主题 | 原作用 | 当前冲突 | 结论 |
|---|---|---|---|
| `989b270fd` 删除 HOW proof 映射 | 让测试标题成为唯一 trace edge | 当前 REQUIREMENT-SYSTEM 要求 HOW 使用精确 `(path,title)` executable proof 锚点 | 已过时，不迁移 |
| `954e14fd7`、`6b5a047be` 退役 migration ledger | 消除可伪造完成状态 | 老板重构已把 36 个迁移事实重新纳入受 gate 约束的 ledger | 已过时，不迁移 |
| 旧 completion/ProofGraph/RequirementSync gate 删除链 | 删除当时的第二套状态机 | 当前 gate 已重写且基线全绿 | 不回放旧删除提交；只审查当前 gate 本身 |

### 3.3 当前 WHAT 已要求、重构后仍缺失的行为

| 优先级 | 旧节点 | 当前 WHAT/owner | 已确认缺口 | 迁移方式 |
|---:|---|---|---|---|
| P0 | `f755497b8` + `b2cebe0c3` | MANAGED-SESSION-006/015；`LinkageProjection` | `reactivateExisting` 可把同一 handle 重绑到另一 child/role/owner | typed identity conflict；只允许 exact durable binding 承接后续 work unit；`Abandoned` 不可重开 |
| P0 | `90080cecb`—`bc7666685` | DISPATCH-PROTOCOL-004；`PromptIngressCodec` | 接受四个 carrier、空白值并按优先级掩盖冲突；真实 Host 只给 `input.messageID` 与 `output.message.id` | exact opaque 两 carrier；缺失/空白/冲突 fail closed |
| P0 | `d0ecbac8a`—`d66b3db44` | Host/run-binding；`ProviderRunBinding` | 以 lexical message id 代替 Host `time.created` 判断 latest；空白 physical identity 可通过 | 解析 finite `time.created`；按时间、再按 id tie-break；空白拒绝 |
| P0 | `262d9bf8c` + `a8fd17ced` | review assurance；`Judgement/Witness` | confirmed cohort 未验证 nested reviewer/tree 与双 attempt 独立性 | 共用一个结构资格判定函数 |
| P0 | `67234094a`—`6d063dba8` | review assurance；Identity/JudgeTool/Witness/replay | 空白 physical judgement evidence 可进入 command、witness 或 durable replay | 唯一 nonblank predicate，所有 ingress 共用 |
| P1 | `7a8b7c77b` | CONCERN-ROUTING-001；`ConcernProjection` | command 与 durable `MailboxSubscribed` replay 均可接受空白 id/concern | 共用 address validator，两个 ingress fail closed |
| P1 | `30f9d7452` | KNOWLEDGE-REUSE-009；`FetchTool.Execute` | 直接构造工具时可绕过 marker，触发索引/事件副作用 | Execute 入口先检查 marker；生产绑定反例 |
| P1 | `d87cdd59e` | structured workflow；JS transaction owner | read-only snapshot 未进入统一 preflight；create race 与自动 rollback 可能覆盖第三方写入 | immutable read snapshot + commit conflict + CAS rollback；按新 store 结构重写 |
| P1 | `40136ef0a` | behavior diagnosis；Enforcer cycle | bounds 常量与公式在 Decode、Host、Surface 重复 | Model 成为唯一 decision owner，Surface 仅转换表示 |

### 3.4 production 行为大致正确，但 proof/gate 在重构中退化

| 旧节点 | 当前缺口 | 处理建议 |
|---|---|---|
| `2a9d7c803`—`e3bdfef7a` | causal-wait Surface 用硬编码布尔声称 observer 无 snapshot、reader 有 snapshot，未让类型能力产生失败 | 恢复 opaque observer/reader handle 与 typed 调用反例 |
| `ff223e798`—`b5e95e1f0` | Change publish 实现会 reread/rebase/review，但测试只验证分类，未证明 gate scope 与旧 CAS witness 被丢弃 | 在当前 Runtime port 上注入最窄 gate acquire capability，恢复两类 observable proof |
| `e13738ce4`—`92b1b1896` | ambient-time gate 仅扫选定目录，缺失 root 可静默通过，目录级排除过宽 | 扫描完整 production tree；exact-file exception；missing root fail closed |
| `c11511e98` + `0b87de296` | Host snapshot locality 与 signal closed-set 的测试只看弱分类/部分路径 | 绑定当前 Host Surface 的 exact/missing/ambiguous 与完整 typed shape |
| `91503a3f1` | `tests/support/managed-surface.mjs` 仍有 Fork/Satellite/PTY/Terminal/SyncDelegate 镜像 | 分别迁到当前注册 Surface；全部 consumer 清零后删除镜像 |

### 3.5 框架级改动的执行状态

| 主题 | 当前事实 | 证据 / 后续条件 |
|---|---|---|
| Acorn-based requirement trace parser | M8 已完成；保留 HOW exact anchor、proof level、symlink/inactive，binding 改由 shared AST core 判断 | 见 batch 9 与 PR #27 |
| AST Surface Manifest binding | M9 已完成；保留 consumer authority 与逐 law 规则，增加 exact import provenance 与 callback terminal use | 见 batch 9 与 PR #27 |
| `fast-check@4.9.0` property 试点 | M10 已完成；没有回放旧套件，只覆盖 completion first-wins 与 provider wire history mutation | exact direct dependency；固定 seed/budget；shrunk path 实际 replay；见 batch 10 |
| Strength DryRun 大型 harness | Strength identity、audit、recovery 已被重构，旧 harness 强依赖旧 Runtime 结构 | 先为当前 source-regex proof 写精确 counterworld；若确实 false-green，再按新 runtime 重建最小 harness |

## 4. 迁移顺序

1. 先修 identity 与 lifecycle boundary：handle、dispatch、Host latest binding、review evidence。
2. 再修边界副作用：Concern、Casebook、JS transaction。
3. 收敛单一 decision owner：Enforcer bounds。
4. 加固已退化 proof：causal wait、ambient time、Host signals；Change 与 managed mirror 视重构耦合度决定本 PR 完成或暂存。
5. 行为与 proof 稳定后引入 Acorn shared analyzer；再以独立 M10 引入两个 production-bound fast-check property，没有同时修改产品逻辑。

每个节点更新本文件中的状态与实际验证。无法安全完成的节点保持在旧分支，不以兼容层伪装已合并。

## 5. 待负责人决定

当前没有阻塞 P0/P1 行为迁移的产品决策。原框架依赖取舍已经闭合：Acorn 随 M8/M9 shared AST binding 引入；fast-check 拆为后续 M10 property pilot，二者均未与产品逻辑修改混合。

剩余产品取舍只有 Change observable gate 是否允许作为后续独立 PR；managed-session 镜像迁移已经闭合。

在出现实测耦合前不提前扩大范围；本分支会保留足够证据供负责人选择。

## 6. 执行日志

- M10 property pilot：`6e4126b4c`→`8391e8e6a`→`9ad1d1df6`→`d23929a90`→`bd2b67f4a` 精确固定 `fast-check@4.9.0`，将 completion race 与 provider wire mutation 两个大输入空间绑定到注册 production Surface。删除 251 行 test-local handle/deadline 状态机；generator 只生产输入，WHAT relation/typed rejection 充当 oracle。两个定向 mutant 均被杀死并用 shrunk seed/path 实际重放。property 9/9、managed-session 125/125、trace/surface 38/38、772 WHAT / 3904 tests、165 surfaces、build/text/owner gates 与完整阶梯全绿；production F# 未修改。完整记录见 `docs/upstream-remaining-merge-batch-10-2026-09-01.md`。

- M7D/M7E mirror closure：`b680f729f`→`ad5d58437` 与 `8b32a54fe`→`11485ff83`→`5c3eaa9e0` 将 SyncDelegate 与 Host PTY consumers 迁到 production owners，并删除最后的 `managed-surface.mjs`。真实 runtime proofs 分别 5/5 与 11/11；managed-session 124/124。深层 owner gate 在初稿中发现两个未分类 mutable resources，以及放在 domain 路径中的 `SatelliteSurface` Host infrastructure；`3bf732e56` 以 exact resource classification 与 physical `OpenCode/Host` ownership 修正，没有增加 exception。最终完整阶梯：Fantomas 700 unchanged、Fable 738 sources / 165 surfaces、unit 3904/3904、全部 integration/package/Long Stroke/pack 全绿；text gate 772 WHAT / 3899 tests，owner evidence 27,218 uses / 333 edges / 185 contracts。
- M7B/M7C mirror closure：`43884d61f`→`a9a431728`→`7e7fe1d00` 与 `fba887a3d`→`2f0449153`→`a145572b3` 将 terminal、family、Satellite、Distiller assertions 迁到 production owners。删除 34 条常量 fork/family tests、Satellite JS 状态机、Distiller 常量与 literal semantic-cut 假 proof。Fable 737 sources / 164 surfaces、managed-session 125/125、text gate 772 WHAT / 3900 tests 全绿。`MANAGED-SESSION-006` 的 Retired 不可逆条款与 exact retired binding 重开 production 行为存在既有冲突，留待 owner 裁决，未借迁移改语义。

- Durable handle：新增 production-bound rebinding 反例；`LinkageProjection` 对 child/target/byname/role/ownership 任一漂移返回 `HandleIdentityConflict`。合入最新 DELEG-024/027 后，exact binding 保留 upstream 的后续 work-unit 重开语义，同一物理 child 无需 join 即可再次 dispatch；`Abandoned` 仍不可重开。
- Dispatch ingress identity：proof 从 provider-projection 移回 dispatch owner；只接受 `input.messageID` / `output.message.id` 的唯一 exact nonblank 值，冲突与非契约 carrier fail closed，opaque identity 不 trim。
- Host latest-run binding：`SessionMessage` 投影 finite `time.created`；latest assistant 按 creation sequence 与 equal-time ID tie-break 判定，缺失/非法 chronology 返回 typed `InsufficientSequence`。Review Surface 复用同一 snapshot decoder，不再维护平行 raw-message adapter。
- Review cohort witness：Finality admission 复用 `ReviewWitness.isQualifiedConfirmationFor`，同时约束 cohort/nested reviewer、barrier、outer/nested tree 与独立 ProviderRun/ToolCall，结构伪造不再升级为 blessing authority。
- Review physical evidence：`PhysicalUserMessageId.isNonBlank` 成为唯一 predicate，JudgeTool、Direct CE、witness 与 replay qualification 均在写 durable verdict 前 fail closed；空白输入保持 zero-effect。
- Concern address validity：subscription command 与 `MailboxSubscribed` replay 复用 `validateAddress`；空白 `id/concern` fail closed，不创建 mailbox/announcement。
- Casebook execution marker：`FetchTool.Execute` 复用 `CasebookFeature.isEnabled`，使直接构造工具也无法绕过 marker；disabled workspace 在 index/replay/event 之前拒绝。
- Intermediate upstream sync：先合入 `upstream/master@caa0b7b4f` 的 capability admission、verification budget、Repair 收口与静态契约；冲突文件采用 upstream 新 owner 结构，仅迁回仍有效的 Host chronology 规范与本分支行为 proof。随后又完成 `4e5789e1c` 同步，见下文 latest sync。
- Ownership closure：删除无人使用的 `HostBoundarySurface.bindableRun` 平行 adapter，Host/review proof 统一走 `ProviderRunBindingSurface`；durable journal surface 不再读取 review-owned `ObservedAttempts`，blank REVISE 以 `NoReview`（合法 REVISE 必为 `RevisionWitness`）证明零 durable verdict。
- Authority manifest merge：删除自动合并产生的 8 组重复 contract；保留 upstream 唯一声明，并将 `ChatAdmissionBindingReceipt` / `HostModelProjectionReceipt` 的 anchor 与 scope 对齐 exact admission identity、binding kind 与 OpenCode model。
- Final verification repairs：Fallback owner resolution 恢复为单次 `ownerState` 读取；Requirement Grounding 对存在路径使用 canonical realpath 防 symlink escape，对不存在路径使用同一 lexical workspace 解析，消除 macOS `/var`→`/private/var` 假越界。
- Portable distribution proof：不再用 case-insensitive filesystem 上会把 `dist/Resources` 误当 `dist/resources` 的 `existsSync`，改为检查 `dist` 的 exact directory entry；仍严格禁止 lowercase 资源副本。
- Host public-contract canary：普通 sandbox 因 `listen EPERM 127.0.0.1` 失败；授权本机 loopback 后在 OpenCode 1.18.18 真实边界 2/2 通过，确认为环境权限而非产品回归。
- Latest upstream sync：合入 `upstream/master@4e5789e1c`。`IngressCodec` 接受 upstream 扩展的 agent observation，但继续保留本分支 exact physical-message identity；fork transport receipt 与 same-Byname lifecycle 属正交语义，均保留。无文本冲突。
- Fork canonical wording：latest upstream 新测试断言 `has accepted`，但 production 与双语 canonical resource 均使用 `carries this charge now` / `现已接下这项托付`；只修正测试 oracle，未改 production 文案。
- Process-aware Git lock contract：latest upstream 让 `IntegrationGate` 调用 durable-convergence owner 的 `ProcessEventLog.processAwareFs`，却未发布跨 owner contract；新增只允许 `change-integration` 消费该单一 adapter 的 exact contract，不授予 `readStreams` 或 durable state 权限。
- Latest upstream control flow：latest upstream 在 `IngressCodec`、`ChatParamsHook`、`PairProgrammingThoughtTransform`、Requirement Grounding Transform 引入 6 个 control-pyramid 命中。以 named Evidence→Decision 与 tuple match 展平，保持原分支顺序和返回值；未增加 suppression/baseline。
- Prefix proof anchors：latest upstream 重命名 empty-history 与 lone-user 两条 executable test，却未同步 HOW；精确锚点改指现存标题，未以 prose 代替 proof。
- Pair marker integration fixture：latest upstream 已禁止 empty/lone-user transcript 以 synthetic tool call 开头，但 capability integration 仍用 lone-user 输入断言必注入。fixture 改为 user→assistant→current user，并让真实 `chat.message` 为 current physical id 建立 execution lease；全部 wire 断言保持不变。
- M0 Linux CI 基线：PR #19 的 Node 20 job 将 `node:test run({ timeout: 2500 })` 的文件 wrapper 误当叶子测试，11 个健康多测试文件在 2500ms 整齐被杀；同一个 suite `AbortSignal` 还向全部 worker 复制 listener。叶子 timeout 改为由需要 timeout-and-forget 的测试自行声明；不变的 300000ms suite backstop 由外部 supervisor 唯一持有。
- M0 PTY proof：Linux Node 20 下 `bun-pty` 的 Bun-only TypeScript entry 拒绝时序晚于单次 `setImmediate`，原测试把 scheduler tick 当成 completion。测试改为等待 production mailbox 发布的 typed completion；不改 PTY production 或错误类型。
- M0 proof 自审：RED commit 中两个 fixture 新增 budget import 多退了一层目录，首次 GREEN harness 以 module-load error 暴露。修正后连续 verdict 反例运行约 9s，超过 7s 静默窗仍因 22 个真实 leaf verdict 正常通过。全量 unit 又发现 degradation SSOT 的 exact-list consumer 停在 13 项；已将文本、ID 绑定、数量与行号连续性一起闭合到 14 项。

## 7. 相对 upstream 的修改、原因与证明

本节专供 upstream 审核。“失败来源”区分 upstream 既有缺口、本地旧语义与新 upstream 的合并冲突、冲突解决回归，以及执行环境限制。

| 修改面 | upstream 原状 / 失败来源 | 修改理由 | 独立证明 | Git 节点 |
|---|---|---|---|---|
| Durable handle binding | upstream `reactivateExisting` 会把已有 handle 的 child、target、Byname、role 与 ownership 全部覆盖。本地旧修复又一度过度禁止 `CompletedAwaitingJoin → Active`，与新 upstream DELEG-024/027 冲突，导致 same-Byname proof 失败。 | 保留 upstream “同一 logical person、同一物理 child 立即承接后续 work unit”；仅禁止 durable identity 漂移。`Abandoned` 仍封闭。 | `handle.test.mjs::EXEC_009_one_durable_handle_cannot_be_rebound_to_another_child`；`fork-tool.test.mjs::FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join`；focused 51/51；最终全量 3925/3925。 | RED `a51ac65fa`；初次 GREEN `6bedfd4c6`；upstream 语义校正 `e73e6b3c5` |
| Dispatch physical identity | upstream ingress 可接受非 Host 契约 carrier、空白值，并用优先级掩盖 carrier 冲突。 | 真实 Host 边界只允许 `input.messageID` / `output.message.id`；唯一 exact nonblank 值才能成为 opaque physical identity。 | `requirements/dispatch-protocol/tests/ingress-identity.test.mjs` 覆盖 exact、missing、blank、conflict 与 decoy carrier；dispatch focused 197/197。 | RED `06b4c7c99`；GREEN `00947647d` |
| Host latest provider run | upstream 以 lexical message id 代替 Host `time.created` 判断 latest，空白 physical identity 亦可绑定。 | latest 必须由 Host chronology 决定；equal-time 才用 id tie-break；时序不足返回 typed `InsufficientSequence`。 | `host010-run-id-equivalence.test.mjs`、`seal-bind.test.mjs`、Host identity 16/16；真实 Host canary 通过。 | RED `f44cd284e`；GREEN `0529f0349` |
| Review cohort / physical evidence | upstream confirmation 未共用 nested reviewer/tree 与独立 attempt 结构资格；blank physical judgement id 可进入 command、witness 或 replay。 | 用一个 `ReviewWitness.isQualifiedConfirmationFor` 决定结构资格；所有 ingress 共用 `PhysicalUserMessageId.isNonBlank`，空白证据 zero-effect。 | `blessing-admission.test.mjs`、`witness.test.mjs`、`host-reverify.test.mjs`；review/finality 66/66；全量中 blank-id 反例通过。 | RED `5900e5547`/`c921ceb0b`；GREEN `659dd7556`/`37ee2400b` |
| Concern address | upstream command 与 durable `MailboxSubscribed` replay 都可接受 blank id/concern。 | command 与 replay 共用一个 address validator，防止不可寻址 mailbox 进入 durable truth。 | `concern-routing.test.mjs` 的 command/replay 双反例；全量 verification 通过。 | RED `85ee3c3aa`；GREEN `b021d5b73` |
| Casebook fetch marker | upstream 只在组装路径判断 marker，直接构造 `FetchTool` 可在 disabled workspace 触发 index/replay/event 副作用。 | 最终 effect owner `FetchTool.Execute` 再次消费正式 feature decision，使旁路无法绕过。 | `fetch-tool.test.mjs` 精确直构反例；disabled 时 index/replay/event 计数保持 0。 | RED `ddcd25255`；GREEN `ce1b6b59c` |
| Fallback owner-state causality | 最新 upstream 代码分别传递 owner 并二次查找 state；合并冲突时接受该片段，丢失本地的 owner/state 同源约束。重施后旧 gate 又因只识别旧源码形状而红。 | 从同一 projection 一次解出 `(owner,current)` 并传到 admission；gate 改为证明该 pair 与 typed authorization 同时到达唯一 ledger append，不用旧排版当 oracle。 | `p0-recovery-join` 697 files / 64 rules 通过；原 malformed direct-owner fixture 仍稳定变红；`recovery-reentry.test.mjs` 5/5；最终全量 3925/3925。 | production `850f59f40`；gate `f56fdec06` |
| Requirement Grounding path | upstream 先 canonicalize workspace 为 `/private/var/...`，但不存在的 target 仍保留 `/var/...`；macOS 上同一 workspace 被误判为越界。 | 存在 target 用 canonical realpath，仍 fail closed 防 symlink escape；不存在 target 用同一 lexical workspace 解析，不混用两种 root spelling。 | 新 `scope-resolution.test.mjs::resolves nonexistent paths through a symlinked workspace without allowing symlink escape`；grounding 17/17；全量中同一 proof 通过。 | `73ae95487` |
| Distribution single-copy proof | upstream 用 `existsSync(dist/resources)` 证明无 lowercase 副本；macOS case-insensitive FS 会把合法 Fable namespace `dist/Resources` 当成命中，这是 upstream proof 误判，不是 production 回归。 | 读取 `dist` 目录项并 exact 比较 lowercase `resources`；仍严格拒绝真实资源副本，不放宽断言。 | focused distribution 4/4；package contents/import/install/resources 全绿；`npm pack --dry-run` 实际检查 2015 files。 | `95fabb42f` |
| Host admission canary | 失败仅为 sandbox `listen EPERM 127.0.0.1`；没有 upstream 或 production 修改。 | 保留原 canary，用允许 loopback 的正式验证环境重跑，不用 mock 或 skip 掩盖。 | OpenCode 1.18.18 public contract 2/2；全量 runner 中 HOST-BOUNDARY-023 通过。 | 无代码提交 |
| Fork accepted wording | latest upstream 新 fork 测试期待 `has accepted`，但同一提交未修改 canonical resource；production 正确输出 `carries this charge now`。这是 upstream 新测试 oracle 与其资源 SSOT 不一致，不是合并回归。 | 断言接受 canonical 英文与中文资源用语；不改 production，不增加兼容输出。 | `requirements/delegation/tests/fork-tool.test.mjs` 10/10；双语 resource parity 与全量 3925/3925。 | merge `3082348ef` |
| Process-aware lock ownership | latest upstream `e37c1ac4a` 新增 `IntegrationGate → ProcessEventLog.processAwareFs` 跨 owner 调用，却只登记 source owner，遗漏 consumer contract；真实 FCS owner gate 因而拒绝。 | 发布最小 exact symbol contract：owner=`durable-convergence`、consumer=`change-integration`、symbol 仅 `processAwareFs`。不开放 event stream/state 读取。 | owner scan 27,093 uses / 333 edges / 185 contracts；真实 FCS integration 2/2；Git worktree/fast-forward integration 5/5。 | `f0c5b796c` |
| Latest upstream F# control flow | latest upstream `01f80026b` / `4e5789e1c` 在 4 个文件引入 6 个 nested-decision 命中；这是 upstream 原提交违反仓库零基线，不是本地语义迁移导致。 | `IngressCodec` 用 null-safe child + ordered candidates；`ChatParamsHook` tuple-match session/physical id 并平铺 agent candidates；pair/grounding 抽出有业务名 decision。分支语义、顺序、错误值不变。 | control-pyramid debt 0；gate fixtures 10/10；受影响行为测试 46/46；Fable build 与全量 3925/3925。 | style `1b8e6974c`；semantic flatten `d79ff38c6` |
| Prefix HOW anchors | latest upstream 重命名两条 prefix tests 后，HOW 仍引用已不存在的旧标题；requirement trace fail closed。这是文档迁移遗漏。 | 用现存 exact test title 替换两个 dangling anchors，不增加 prose proof、不改变 WHAT。 | requirement trace 772 WHAT / 3919 declarations closure complete；focused prefix 13/13。 | `dc1e7b12b` |
| Capability pair-marker fixture | latest upstream 已正式规定 empty/lone-user history 不注入 pair marker，capability integration 却仍用 lone-user 输入要求注入；改成三消息后又因 current user 未获 lease 被 PROMPT-006 正确拒绝。两次红都来自 stale upstream fixture，而非 production。 | 输入改为 user→assistant→current user，并通过真实 `chat.message` 绑定 current user 的 exact physical id。保留 Host-owned `skill`、synthetic、input/status/output、canonical content 的全部断言。 | focused file 9/9；正式 capability integration 聚合 13/13；全量 3925/3925。 | `f14476da0` |
| Fable/NuGet 网络 | restricted sandbox 下 NuGet vulnerability index 无法访问，出现 NU1900；源码、lockfile 与构建配置均未变。 | 不关闭安全检查、不改 dependency；在允许 NuGet 安全索引的正式验证环境执行相同官方命令。 | 同一 `npm run format-build-test` 完整退出 0；Fable 734 sources / 161 surfaces。 | 无代码提交 |
| M0 file-wrapper timeout | PR #19 Linux Node 20 把 `run({ timeout: PER_TEST_TIMEOUT_MS })` 施加于 process-isolated file wrapper；同文件模块加载与多个健康 leaf 共享 2500ms 总预算。shared `AbortSignal.timeout` 还产生 `MaxListenersExceededWarning`。 | 从 inner `run()` 移除 file-wrapper timeout 与 shared signal。叶子测试显式持有原 2500ms 预算；external supervisor 持有原 300000ms 物理 backstop。数值零放宽。Node 20 官方 `node:test` 文档确认 process isolation 以 test file 为 child process 单位。 | 正式 harness 273/273；关键 fixture 22 个 leaf 串行约 9s，跨过 7s verdict-silence 窗口仍通过；hung/chatter、overrun、leaked-handle 反例仍在原预算内失败。[Node.js v20 test runner](https://nodejs.org/download/release/v20.19.0/docs/api/test.html) | RED `c0161f5cd`；GREEN `30ca1ce2b` |
| M0 PTY completion proof | upstream PTY 测试只等一次 `setImmediate`。Linux Node 20 下 Bun-only `bun-pty` loader 的异步拒绝尚未进入 mailbox，前 4 项失败，后 4 项被连带取消。 | 等待注册 production Surface 的 `takeCompletions` 发布 exact `failed` completion，不再用 scheduler tick 代替领域事件。production F# 零修改。 | PTY focused 48/48；最终 unit 中 PTY backend/port 全绿；Long Stroke 57 步通过。 | proof `c0161f5cd` |
| FCS integration verdict granularity | PR #20 run `33424153110` 已通过 3928/3928 unit；随后 `owner-dependencies-fcs.test.mjs` 在 185100ms 被 verdict-silence watchdog 拒绝。原测试把 production scan、reuse assertions 与第二个 fixture scan 合为一个 verdict，Linux 三段总时长可越过 180s 单段预算。 | 保留 `FCS_PROJECT_CHECK_TIMEOUT_MS=180000` 与 185000ms silence window；只把三个完整因果阶段变为独立 nested verdict。stdout/stderr 不续期，任一真实扫描自身挂起仍按原预算失败。production、scanner、schema、断言与 fail-closed 路径均未修改。 | 正式 FCS test 5/5：fixture 10.9s、production evidence 109.6s、reuse 0.7s、isolation fixture 4.9s，总计 126.1s；最长真实阶段低于原 180s 上限。首次 sandbox run 因 NuGet 网络拒绝失败，不计实现 verdict；联网复跑全绿。 | 本次提交 |
| FCS scanner file granularity | PR #20 run `33426540260` 证明前项修复有效：owner lane 5/5，Linux production scan 146.8s，整链 156.3s。下一步旧 `workflow-constitution-scanners.test.mjs` 又在 185056ms 被同一 watchdog 拒绝；它把 composition-root、plugin/decorator 与 semantic-decorator fixture 三个独立 scanner 藏在一个 file verdict 后。 | 将三个独立 scanner 拆成三个顺序 integration entry；composition-root、plugin/decorator 与 semantic FCS 各自完成才产生 verdict。保留每项原断言、原 180s 单段预算、185s watchdog 与 300s suite backstop；未修改任何 scanner 或 production。 | `764c6a2cb` 精确下沉第 3 次已审查的拆分；text gate 与正式 GitHub rerun 作为出口。 | `764c6a2cb` |
| FCS owner physical-file closure | 累计 PR #21 run `33426894259` 证明 nested test 不是 supervisor verdict：尽管源文件已有三个 nested leaves，Node 20 process-isolated file 在 185100ms 仍报告 0 blocking progress。PR #20 run `33426540260` 的 5/5 只证明当次整文件 165.5s 小于窗口，不能证明 nested renewal。 | 将 fixture symbol-resolution、production evidence+reuse、explicit-project isolation 拆为三个物理 test files，并由 integration SSOT 顺序调度。每个文件只有一个有意义的扫描边界；不增加 timeout、不重复 production scan、不改 scanner/schema/assertion。 | 正式 GitHub rerun；本地 text entry-coverage 与三个 focused files。 | 本次提交 |
| FCS single-production-scan closure | PR #20/#21 runs `33429721220` / `33429748255` 证明 physical-file split 后，`owner-dependencies-reuse.test.mjs` 仍在 integration 内重新执行完整 production scan；Linux 第二次扫描超过 185s silence window。完整阶梯的 owner-dep 阶段此前已完成同一 scan 并原子写出 normalized evidence，重复扫描既不独立也不增加错误世界。 | normalized artifact 升级 schema v3，增加 SHA-256 `inputFingerprint`，绑定 normalizer、FCS scanner、project、npm dependency lock 与完整 production compile set 内容。integration 读取 owner-dep 生成的 exact run-id artifact；缺失、schema/run-id/fingerprint 错误、compile set 漂移全部 fail closed。删除第二次 production scan，不增加 timeout。 | 快速合同 56/56；真实 owner-dep 全绿；fixture/isolation/reuse 3/3，reuse 1.55s、总计 5.38s；wrong fingerprint counterexample 被拒绝。 | 本次提交 |
| FCS integration artifact propagation | PR #21 run `33432246221` 证明只修改 reuse proof 不足：后续 `semantic-decorator` integration 子进程没有继承 owner-dep artifact 身份，仍会隐式启动另一轮 production FCS scan，并在 185077ms 被拒绝。 | integration orchestrator 在任何 step 前 fail-closed 读取同一 schema-v3 artifact，将 exact path/run-id 传给全部子进程。默认 production scanner 必须复用并验证 fingerprint；显式 fixture project 按 scanner 既有规则忽略 production reuse，仍独立真实编译。 | focused composition-root 0.63s、plugin+decorator 2.22s、semantic fixture 7.66s；正式 integration orchestrator 全绿，harness 273/273。预算、watchdog、scanner 与断言不变。 | 本次提交 |
| M0 degradation-list closure | 本次 WHAT 新增第 14 条 forbidden degradation，parser registry 与 harness coverage 已更新，但 upstream 的 exact SSOT consumer 仍固定 13 项，最初全量因此 3 项失败。 | 完整迁移 exact text order、ID↔text binding、cardinality 与 source-line continuity；不删断言、不改 parser 使其宽松。 | focused 7/7；最终 authoritative unit 3925/3925。 | `5afc522b8` |

合并后的 owner/authority manifest 收口也修改了 upstream 自动合并结果：`58081dd2a` 删除平行 Host/review consumer，`9bb863337` 删除 8 组重复 authority contract 并保留 upstream 唯一声明。latest upstream 的 process-aware lock contract 加入后，对应证明为 owner dependency 27,093 uses / 333 edges / 185 contracts、authority tests 30/30、authority production scan 全绿。

## 8. PR 前完整验证

2026-08-31 合入 `upstream/master@4e5789e1c` 并逐项修复完整门禁暴露的 upstream 迁移遗漏后，从干净节点 `f14476da0` 单次执行 `npm run format-build-test`，未拆分、未跳过长时间 scanner：

- Fantomas：696 unchanged，0 error。
- `scripts/check.mjs`：全部通过；696 architecture/semantic-owner files；27,093 owner uses / 333 edges / 185 contracts；0 control-pyramid；0 deadcode；0 JS boundary debt；772 WHAT / 3919 test declarations closure。
- Fable build：734 source files，161 registered surfaces，成功。
- authoritative verification：3925 pass / 0 fail；含 OpenCode 1.18.18 真实 Host admission canary。
- integration：全部通过；包含 FCS owner dependency、workflow constitution scanner、durable convergence 与 273-case harness。
- package integration：contents/import/install/resources 全部通过。
- e2e Long Stroke：57 steps / 7.5s，journal 587/700，SSE 2528/3450；正式入口通过，没有 skip、重复放行或替代 mock 验收。
- `npm pack --dry-run`：2015 files，package 2.2 MB，unpacked 10.5 MB，成功。

## 9. M0 CI/Linux 可信基线（2026-08-31）

### 9.1 范围与基线

- 分支：`codex/ci-linux-baseline`。
- 基线：`upstream/master@1db90f5e8`（PR #19 merge commit）。开始与验证前 fetch 均未发现更新的 upstream commit。
- 修改范围：verification requirement/runner/proof 与 PTY proof。production F#、公开 API、业务预算与 package 内容均未改。
- 原始证据：PR #19 GitHub Actions run `33376844892`，job `99440146905`。static gates 与 build 已绿；unit 为 3844 pass / 19 fail-or-cancel，失败集中在 2500ms file wrapper 与 PTY loader 时序。

### 9.2 因果修复

1. `c0161f5cd test(ci): expose file-wrapper timeout and PTY tick races`
   - WHY/WHAT 明确 leaf test、process-isolated file wrapper、verdict-silence supervisor 三层预算边界。
   - 新增 `VERIFY_004_D_LEAF_TIMEOUT_APPLIED_TO_FILE_WRAPPER`，并绑定可执行 coverage case。
   - 时间反例要求每个 leaf 持有局部 timeout，但多 leaf 文件总时长超过静默窗；只有真实 verdict feed 才能使其通过。
   - PTY 测试改为等待 production completion mailbox，删除单 scheduler tick 伪 oracle。
2. `30ca1ce2b fix(ci): separate leaf timeout from suite supervision`
   - inner `node:test run()` 只负责 files + concurrency；不再把 leaf timeout 或一个 shared AbortSignal 传给所有 file worker。
   - external supervisor 以一个 unref physical backstop 持有原 `SUITE_BACKSTOP_MS=300000`；verdict-silence 仍是首要判据。
   - RED fixture 的 budget import 路径在第一次 GREEN 验证中被证明错误；同提交修正后重跑正式反例。这一点保留在记录中，不把 proof 自身错误伪装成 runner RED。
3. `5afc522b8 test(ci): close degradation-list consumer`
   - 全量验证找到未迁移的 exact-list consumer；13→14 的文本、ID、数量、行号一次闭合。

### 9.3 验证阶梯

- RED 阶段：verification harness 271/273，其中 static binding 稳定拒绝旧 `timeout: PER_TEST_TIMEOUT_MS`；behavior fixture 后续发现 import path 错误，因此不把该次 behavioral failure 作为独立根因证据。
- 修正 proof 后：verification harness 273/273；PTY focused 48/48；degradation-list focused 7/7。
- `node scripts/build.mjs`：734 F# sources，161 registered surfaces，成功。restricted sandbox 首次仅因 NuGet NU1900 无法访问 vulnerability index 失败；不关闭检查，同命令在允许访问的环境通过。
- `node scripts/check.mjs`：全绿。27,093 owner uses / 333 edges / 185 contracts；36 个 migration node 均 DONE；0 control-pyramid；0 deadcode；0 JS boundary debt；772 WHAT / 3919 declarations closure。
- 最终无跳过 `npm run format-build-test`：
  - Fantomas：696 unchanged，0 error。
  - authoritative unit：3925/3925。
  - integration：全绿；real FCS lanes 分别 146.6s 与 180.2s；harness 273/273。
  - package integration：contents/import/install/resources 全绿。
  - Long Stroke：57 steps / 7.4s，journal 583/700，SSE 2491/3450。
  - `npm pack --dry-run`：2015 files，2.2 MB packed，10.5 MB unpacked。

### 9.4 未完成的外部事实

- M0 已随 M3 push 到 [upstream PR #20](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/20)。
- M0 只有在该 PR 的 GitHub Linux Node 20 CI 实际全绿且合并后才算进入 upstream。本地完整阶梯已闭合实现证据，不代替尚未发生的 CI 与 merge SHA。

## 10. M3 Enforcer bounds 单一 owner（2026-08-31）

### 10.1 最新 upstream 对齐

- 执行分支：`codex/remaining-merge-batch-2`；基线：`upstream/master@fcd5ab11b`。
- upstream 新增 `4e7570abf` / `fcd5ab11b`，只修改 AGENTS、package/Wireit、`.gitignore` 与 `scripts/check.mjs` lane 编排；没有修改 Enforcer production。由于 PR #19 是 squash merge，本地旧分支与 upstream 不具线性祖先关系；本分支从 `fcd5ab11b` 新建，只按原节点重放计划文档与 M0 的 4 个独立提交，避免再次合并已被 squash 吸收的施工历史。
- 旧 M3 证据 `40136ef0a` 仅用于定位历史意图；未 cherry-pick。当前 WHY/WHAT/HOW、production owner 与 gate 重新独立审查后实施。

### 10.2 RED → GREEN → FCS closure

1. `4b87c3c0a test(enforcer): expose duplicated content-bound owners`
   - WHY/WHAT 先定义：Cycle model 唯一拥有阈值、UTF-8 字节计数结果与拒绝分支。
   - 新增全 Enforcer production tree gate，不只检查三个已知文件。它拒绝 consumer 常量定义、raw threshold、直接 byte-count comparison、Decode/Surface 脱离 shared decision。
   - synthetic decoy 同时覆盖 Host 常量副本、Decode 公式副本、新增目录中的 renamed raw threshold。
   - RED 证据：原注册 Surface 行为 4/4 仍绿；gate 同时报告 Model 缺 owner、Decode/Surface 脱离 owner、Decode/Host/Surface 共 7 处副本。证明行为边界值测试不能独自阻止未来漂移。
2. `eb0e703d6 refactor(enforcer): centralize content-bound decisions`
   - `Cycle/Model.fs` 新增 `ContentBounds`、`ContentBoundsRejection`、两个 canonical threshold、`validateContentBounds` 与统一错误映射。
   - `Cycle/Decode.fs` 删除常量与判定公式，真实提交路径调用 shared decision。
   - `Surface.fs` 只暴露 owner 常量、注入 `LlmFacing.byteCount`、转换 typed result；测试不再验证另一套公式。
   - `Host.fs` 删除未消费的重复常量。
   - gate 初版曾把 F# `->` 误认作比较运算符；GREEN 中将模式收紧为带两侧空白的 ` > `，synthetic direct-comparison decoy 保持可失败。
3. `787a877d6 fix(enforcer): declare byte-count trace contract`
   - 完整 FCS lane 发现 injected `byteCount` 在同一 decision 中固定调用两次，触发 `semantic-decorator-invariant`。这不是误报：高阶 operation 的 multiplicity 需要 owner law。
   - 在函数声明处绑定 owner=`behavior-diagnosis`、WHAT=`BD-011`、text→evidence 顺序、executable proof、同步失败语义、无 cancel/deadline、invocation bound=2。没有把计数移回 consumer，没有修改或放宽 analyzer。

### 10.3 对 upstream 原文件的修改理由与证明

| 修改面 | upstream 原状 | 修改理由 | 证明 |
|---|---|---|---|
| Enforcer Cycle model | 只拥有 `CanonicalCycle` 与 nonempty 判定；不拥有 BD-011 bounds。 | 安全阈值与拒绝公式必须只有一个可执行真源。 | typed result + exact boundary/multibyte behavior；whole-tree owner gate。 |
| Decode | 自有两个常量及两个 `byteCount > limit` 分支。 | 真实 commit path 必须消费 model decision，不能与测试 Surface 漂移。 | architecture gate 强制 exact shared call；behavior-diagnosis 149/149。 |
| Host | 保留两个未消费的 bounds 常量。 | 死副本仍是未来错误 owner；删除尸体。 | whole-tree gate 禁止非 owner constant/raw threshold。 |
| Surface | 自有两个常量、两次计数与两个拒绝分支。 | Surface 只做 JS representation conversion；错误 vocabulary 与 Decode 同源。 | 注册 Surface 的 exact/boundary/UTF-8 反例；Surface Manifest 161/161。 |
| verification entry | 无 Enforcer bounds ownership gate。 | 行为测试不能证明未来 consumer 不复制公式。 | synthetic good world + 3 类 decoy；真实旧树 RED、新树 GREEN。 |

### 10.4 实际验证与剩余外部事实

- `dotnet tool run fantomas src/Wanxiangshu`：696 files，2 formatted，0 error；随后目标文件 unchanged。
- `node scripts/build.mjs`：734 F# sources、161 registered surfaces，成功。
- `node --test requirements/behavior-diagnosis/tests/*.test.mjs`：149/149。
- `node scripts/check.mjs`：text lane 全绿；696 production files；772 WHAT / 3920 tests closure；新 gate 扫描 28 个 Enforcer production files，只存在一个 decision owner。
- `node scripts/check.mjs --lane=owner-dep`：27,091 FCS cross-owner uses、333 owner edges、185 contracts；authority、composition、DSL ownership、semantic decorator 全绿。
- 最新 upstream 已把 npm scripts 切到 Wireit；本机旧 `node_modules` 最初没有 Wireit，首次 `npm run format` 因 `wireit: command not found` 未启动。PR preflight 使用 `npm ci` 按原 lockfile 重建 153 个依赖，audit 为 0 vulnerability；package/lockfile 无修改。
- 正式 `npm run format-build-test` 首次运行在 unit tier 报 3924/3926：`proof-ladder.test.mjs` 与 distribution release proof 仍要求 upstream 改造前的 direct Fantomas/check/build 字符串，而 `fcd5ab11b` 已改为 `npm run ...` → Wireit。`45b20b63e` 没有放宽为“出现任一 build 别名即可”；它分别锁定顶层 format→text→FCS→build→unit→integration→package→e2e→pack 顺序，以及四个 Wireit step 的 exact underlying commands。focused verification/distribution 17/17。
- 修正 oracle 后，正式入口由 build freshness 再次 fail closed：`fcd5ab11b` 的 `wireit.build.files` 只含 `scripts/build.mjs`、F# source 与 `package.json`，但 build 同时从 Git 跟踪的全部源码/文档生成 `LoopDetectorEnvelope.js`；本节文档修改没有使 Wireit cache 失效，旧 dist 被错误复用。`51fff4bd8` 将整个 workspace 纳入 build fingerprint，仅排除 `dist/` 与 `.fable-build/` 自身输出；新增 VERIFICATION-SYSTEM-008 proof 锁定该闭包。focused proof 11/11，`npm run build` 真实重编译 734 sources 并验证 161 surfaces。
- 最终 PR preflight 从 `2026ee242` 运行单次正式 `npm run format-build-test`：Fantomas 696 unchanged；text/FCS gates 全绿；Fable 734 sources / 161 surfaces；unit 3927/3927；两次真实 FCS integration、harness 273/273、package、唯一 Long Stroke 与 `npm pack --dry-run`（2015 files，2.2 MB）全部通过。
- M0 与 M3 已合并为同一 [upstream PR #20](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/20)。GitHub CI 与 merge SHA 尚未产生；M3 仍未进入 upstream。

## 11. M8/M9 共享 AST binding analyzer（2026-09-01）

- 施工起点为 `upstream/master@fcd5ab11b`；PR 前已语义合入 `upstream/master@d76a4a8b5`。M0–M7E 全部保留，没有回退到旧分支。
- requirement trace 从自制 lexical tokenizer 切到共享 Acorn AST，只接受真实 `node:test` binding 的直接 registration；15 处动态/间接声明迁成静态测试，原 graph、HOW anchor、proof level、symlink/inactive 与 portfolio 规则保留。
- Surface Manifest 从 import/use regex 切到 exact lexical provenance + primary WHAT callback use；shadow、dead/nonterminal alias、静态不可达、helper/nested callback、wrong-law decoy 均不能取得 proof authority。
- 新门禁暴露 14 个既有 helper-hidden proofs；全部迁为命题 callback 直接调用 production Surface。另纠正 `ReconcileSurface` 与 `ReviewTodoSurface` 两个错误 manifest owner/law 登记；production F# 与公开 API 未改。
- RED/GREEN 节点为 `37ece7962`、`f69f4d480`、`ff0cda20f`、`024684299`。聚焦验证为 requirement trace 19/19、surface charter 19/19、受影响行为 121/121、772 WHAT / 3902 tests、165 surfaces。
- `5c7ad47cf` 接受 upstream 71-node ReleaseClosure 与旧 migration ledger 删除。首次完整阶梯的两个旧 oracle 正确拒绝 upstream direct command 绕过 owner-dep；`c6db87a44` 恢复 Wireit 四步入口，18/18 proof 变绿，没有削弱断言。
- 第二次完整阶梯的 owner lane 进一步发现 16 条批次 7/8 Surface 未进入 upstream 新 published-contract 图。`e1ec11045` 发布最窄 PTY/session-port symbols，并删除 Satellite fake port 的伪造 Fatal 返回。Fable 与 22/22 行为 proof 通过；owner lane 为 27,226 strict uses / 624 edges / 778 contracts，全绿。
- 最终 `WIREIT_CACHE=none npm run format-build-test` 完整通过：Fantomas、text/FCS gates、Fable 738 sources / 165 surfaces、authoritative unit、全部 integration/package、273-case harness、Long Stroke 与 pack 均未跳过。完整事实见 `docs/upstream-remaining-merge-batch-9-2026-09-01.md`。

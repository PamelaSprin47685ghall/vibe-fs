# Upstream 重构后的语义合并记录

> 日期：2026-08-31  
> 集成分支：`codex/upstream-refactor-integration`  
> 上游基线：`upstream/master@caa0b7b4f`（PR 前仍会最后 fetch 确认）
> 本地旧版本：`codex/pre-upstream-refactor-20260831@4bb19673e`  
> 性质：合并与 review 记录；产品语义仍只由 `requirements/<package>/` 定义

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
- requirement trace：772 WHAT、3916 executable test declarations，closure complete。
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

### 3.5 先暂存在本地的框架级改动

| 主题 | 暂存原因 | 恢复条件 |
|---|---|---|
| Acorn-based requirement trace parser | 当前 lexical parser 同时承担 HOW exact anchor、proof level、symlink/inactive 检查；整段回放会丢掉老板新增规则 | 先写针对当前 parser 的 local-shadow/dead-binding RED，再最小引入 AST binding identity，不能替换新规则 |
| AST Surface Manifest binding | 当前 manifest 已有 consumer authority 与逐 law 规则；旧 scanner 不能直接覆盖 | 以 shared analyzer 方式只增强 import/use binding，保留现有授权模型 |
| `fast-check@4.9.0` 全套 property 试点 | upstream 已删除依赖且当前自有 deterministic generator 全绿；PR 前立即恢复依赖会扩大供应链与 review 面 | 行为缺陷迁完后选 1—2 个状态空间确实需要生成的 owner 做独立提交，并提供 shrinking/replay 价值证据 |
| Strength DryRun 大型 harness | Strength identity、audit、recovery 已被重构，旧 harness 强依赖旧 Runtime 结构 | 先为当前 source-regex proof 写精确 counterworld；若确实 false-green，再按新 runtime 重建最小 harness |

## 4. 迁移顺序

1. 先修 identity 与 lifecycle boundary：handle、dispatch、Host latest binding、review evidence。
2. 再修边界副作用：Concern、Casebook、JS transaction。
3. 收敛单一 decision owner：Enforcer bounds。
4. 加固已退化 proof：causal wait、ambient time、Host signals；Change 与 managed mirror 视重构耦合度决定本 PR 完成或暂存。
5. 行为与 proof 稳定后再决定是否引入 fast-check/Acorn，避免同时改变产品逻辑、测试 oracle 与验证器。

每个节点更新本文件中的状态与实际验证。无法安全完成的节点保持在旧分支，不以兼容层伪装已合并。

## 5. 待负责人决定

当前没有阻塞 P0/P1 行为迁移的产品决策。PR 边界接近时可能需要两项取舍：

1. 是否把 Acorn/fast-check 两个 npm 依赖放入本次重构 PR，还是拆成后续 proof-hardening PR。
2. Change observable gate 与剩余 managed-session 镜像迁移若显著扩大 diff，是否允许作为后续独立 PR。

在出现实测耦合前不提前扩大范围；本分支会保留足够证据供负责人选择。

## 6. 执行日志

- Durable handle：新增 production-bound rebinding 反例；`LinkageProjection` 对 child/target/byname/role/ownership 任一漂移返回 `HandleIdentityConflict`。合入最新 DELEG-024/027 后，exact binding 保留 upstream 的后续 work-unit 重开语义，同一物理 child 无需 join 即可再次 dispatch；`Abandoned` 仍不可重开。
- Dispatch ingress identity：proof 从 provider-projection 移回 dispatch owner；只接受 `input.messageID` / `output.message.id` 的唯一 exact nonblank 值，冲突与非契约 carrier fail closed，opaque identity 不 trim。
- Host latest-run binding：`SessionMessage` 投影 finite `time.created`；latest assistant 按 creation sequence 与 equal-time ID tie-break 判定，缺失/非法 chronology 返回 typed `InsufficientSequence`。Review Surface 复用同一 snapshot decoder，不再维护平行 raw-message adapter。
- Review cohort witness：Finality admission 复用 `ReviewWitness.isQualifiedConfirmationFor`，同时约束 cohort/nested reviewer、barrier、outer/nested tree 与独立 ProviderRun/ToolCall，结构伪造不再升级为 blessing authority。
- Review physical evidence：`PhysicalUserMessageId.isNonBlank` 成为唯一 predicate，JudgeTool、Direct CE、witness 与 replay qualification 均在写 durable verdict 前 fail closed；空白输入保持 zero-effect。
- Concern address validity：subscription command 与 `MailboxSubscribed` replay 复用 `validateAddress`；空白 `id/concern` fail closed，不创建 mailbox/announcement。
- Casebook execution marker：`FetchTool.Execute` 复用 `CasebookFeature.isEnabled`，使直接构造工具也无法绕过 marker；disabled workspace 在 index/replay/event 之前拒绝。
- Final upstream sync：合入 `upstream/master@caa0b7b4f` 的 capability admission、verification budget、Repair 收口与最新静态契约；冲突文件采用 upstream 新 owner 结构，仅迁回仍有效的 Host chronology 规范与本分支行为 proof。
- Ownership closure：删除无人使用的 `HostBoundarySurface.bindableRun` 平行 adapter，Host/review proof 统一走 `ProviderRunBindingSurface`；durable journal surface 不再读取 review-owned `ObservedAttempts`，blank REVISE 以 `NoReview`（合法 REVISE 必为 `RevisionWitness`）证明零 durable verdict。
- Authority manifest merge：删除自动合并产生的 8 组重复 contract；保留 upstream 唯一声明，并将 `ChatAdmissionBindingReceipt` / `HostModelProjectionReceipt` 的 anchor 与 scope 对齐 exact admission identity、binding kind 与 OpenCode model。
- Final verification repairs：Fallback owner resolution 恢复为单次 `ownerState` 读取；Requirement Grounding 对存在路径使用 canonical realpath 防 symlink escape，对不存在路径使用同一 lexical workspace 解析，消除 macOS `/var`→`/private/var` 假越界。
- Portable distribution proof：不再用 case-insensitive filesystem 上会把 `dist/Resources` 误当 `dist/resources` 的 `existsSync`，改为检查 `dist` 的 exact directory entry；仍严格禁止 lowercase 资源副本。
- Host public-contract canary：普通 sandbox 因 `listen EPERM 127.0.0.1` 失败；授权本机 loopback 后在 OpenCode 1.18.18 真实边界 2/2 通过，确认为环境权限而非产品回归。

## 7. 相对 upstream 的修改、原因与证明

本节专供 upstream 审核。“失败来源”区分 upstream 既有缺口、本地旧语义与新 upstream 的合并冲突、冲突解决回归，以及执行环境限制。

| 修改面 | upstream 原状 / 失败来源 | 修改理由 | 独立证明 | Git 节点 |
|---|---|---|---|---|
| Durable handle binding | upstream `reactivateExisting` 会把已有 handle 的 child、target、Byname、role 与 ownership 全部覆盖。本地旧修复又一度过度禁止 `CompletedAwaitingJoin → Active`，与新 upstream DELEG-024/027 冲突，导致 same-Byname proof 失败。 | 保留 upstream “同一 logical person、同一物理 child 立即承接后续 work unit”；仅禁止 durable identity 漂移。`Abandoned` 仍封闭。 | `handle.test.mjs::EXEC_009_one_durable_handle_cannot_be_rebound_to_another_child`；`fork-tool.test.mjs::FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join`；focused 51/51；全量 3922/3922。 | RED `a51ac65fa`；初次 GREEN `6bedfd4c6`；upstream 语义校正 `e73e6b3c5` |
| Dispatch physical identity | upstream ingress 可接受非 Host 契约 carrier、空白值，并用优先级掩盖 carrier 冲突。 | 真实 Host 边界只允许 `input.messageID` / `output.message.id`；唯一 exact nonblank 值才能成为 opaque physical identity。 | `requirements/dispatch-protocol/tests/ingress-identity.test.mjs` 覆盖 exact、missing、blank、conflict 与 decoy carrier；dispatch focused 197/197。 | RED `06b4c7c99`；GREEN `00947647d` |
| Host latest provider run | upstream 以 lexical message id 代替 Host `time.created` 判断 latest，空白 physical identity 亦可绑定。 | latest 必须由 Host chronology 决定；equal-time 才用 id tie-break；时序不足返回 typed `InsufficientSequence`。 | `host010-run-id-equivalence.test.mjs`、`seal-bind.test.mjs`、Host identity 16/16；真实 Host canary 通过。 | RED `f44cd284e`；GREEN `0529f0349` |
| Review cohort / physical evidence | upstream confirmation 未共用 nested reviewer/tree 与独立 attempt 结构资格；blank physical judgement id 可进入 command、witness 或 replay。 | 用一个 `ReviewWitness.isQualifiedConfirmationFor` 决定结构资格；所有 ingress 共用 `PhysicalUserMessageId.isNonBlank`，空白证据 zero-effect。 | `blessing-admission.test.mjs`、`witness.test.mjs`、`host-reverify.test.mjs`；review/finality 66/66；全量中 blank-id 反例通过。 | RED `5900e5547`/`c921ceb0b`；GREEN `659dd7556`/`37ee2400b` |
| Concern address | upstream command 与 durable `MailboxSubscribed` replay 都可接受 blank id/concern。 | command 与 replay 共用一个 address validator，防止不可寻址 mailbox 进入 durable truth。 | `concern-routing.test.mjs` 的 command/replay 双反例；全量 verification 通过。 | RED `85ee3c3aa`；GREEN `b021d5b73` |
| Casebook fetch marker | upstream 只在组装路径判断 marker，直接构造 `FetchTool` 可在 disabled workspace 触发 index/replay/event 副作用。 | 最终 effect owner `FetchTool.Execute` 再次消费正式 feature decision，使旁路无法绕过。 | `fetch-tool.test.mjs` 精确直构反例；disabled 时 index/replay/event 计数保持 0。 | RED `ddcd25255`；GREEN `ce1b6b59c` |
| Fallback owner-state causality | 最新 upstream 代码分别传递 owner 并二次查找 state；合并冲突时接受该片段，丢失本地的 owner/state 同源约束。重施后旧 gate 又因只识别旧源码形状而红。 | 从同一 projection 一次解出 `(owner,current)` 并传到 admission；gate 改为证明该 pair 与 typed authorization 同时到达唯一 ledger append，不用旧排版当 oracle。 | `p0-recovery-join` 697 files / 64 rules 通过；原 malformed direct-owner fixture 仍稳定变红；`recovery-reentry.test.mjs` 5/5；全量 3922/3922。 | production `850f59f40`；gate `f56fdec06` |
| Requirement Grounding path | upstream 先 canonicalize workspace 为 `/private/var/...`，但不存在的 target 仍保留 `/var/...`；macOS 上同一 workspace 被误判为越界。 | 存在 target 用 canonical realpath，仍 fail closed 防 symlink escape；不存在 target 用同一 lexical workspace 解析，不混用两种 root spelling。 | 新 `scope-resolution.test.mjs::resolves nonexistent paths through a symlinked workspace without allowing symlink escape`；grounding 17/17；全量中同一 proof 通过。 | `73ae95487` |
| Distribution single-copy proof | upstream 用 `existsSync(dist/resources)` 证明无 lowercase 副本；macOS case-insensitive FS 会把合法 Fable namespace `dist/Resources` 当成命中，这是 upstream proof 误判，不是 production 回归。 | 读取 `dist` 目录项并 exact 比较 lowercase `resources`；仍严格拒绝真实资源副本，不放宽断言。 | focused distribution 4/4；package contents/import/install/resources 全绿；`npm pack --dry-run` 实际检查 2015 files。 | `95fabb42f` |
| Host admission canary | 失败仅为 sandbox `listen EPERM 127.0.0.1`；没有 upstream 或 production 修改。 | 保留原 canary，用允许 loopback 的正式验证环境重跑，不用 mock 或 skip 掩盖。 | OpenCode 1.18.18 public contract 2/2；全量 runner 中 HOST-BOUNDARY-023 通过。 | 无代码提交 |

合并后的 owner/authority manifest 收口也修改了 upstream 自动合并结果：`58081dd2a` 删除平行 Host/review consumer，`9bb863337` 删除 8 组重复 authority contract 并保留 upstream 唯一声明。对应证明为 owner dependency 27,065 uses / 332 edges / 184 contracts、authority tests 30/30、authority production scan 全绿。

## 8. PR 前完整验证

2026-08-31 修复前次完整门禁暴露的 gate/proof 问题后，从零单次执行 `npm run format-build-test`，未拆分、未跳过长时间 scanner：

- Fantomas：696 unchanged，0 error。
- `scripts/check.mjs`：全部通过；696 architecture/semantic-owner files；27,065 owner uses；0 control-pyramid；0 deadcode；0 JS boundary debt；772 WHAT / 3916 tests closure。
- Fable build：734 source files，161 registered surfaces，成功。
- authoritative verification：3922 pass / 0 fail；含 OpenCode 1.18.18 真实 Host admission canary。
- integration：全部通过；包含 FCS owner dependency、workflow constitution scanner、durable convergence 与 273-case harness。
- package integration：contents/import/install/resources 全部通过。
- e2e Long Stroke：57 steps，journal 601/700，SSE 2495/3450，通过。
- `npm pack --dry-run`：2015 files，package 2.2 MB，unpacked 10.5 MB，成功。

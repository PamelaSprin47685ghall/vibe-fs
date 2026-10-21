# CUT-38：全仓残余职责与终局交账

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-38) / [范围对账](../COVERAGE.md) / [GAP 台账](../../../requirements/GAP.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（2026-10-21，eng-cut35b-goose；终局交账见 §7）
- **所属泳道 / 波次**：收尾 / W6 终局交账
- **切换前置**：`CUT-00` 至 `CUT-35`、`CUT-37`（注：`CUT-36` 为 `OPTIONAL`，不阻塞本卡）
- **关联决策护栏（`D-05`、`D-06`）**：
  - **D-05**：对 `CUT-27`（Casebook）、`CUT-29`（Strength）、`CUT-30`（Rulebook/Enforcer）、`CUT-31`（Sphinx），按实际产品裁决如实归入“已抽取并清旧 / 明确保留（`RETAINED`） / 经批准删除（`REMOVED`）”，严禁为了追求“全部抽干”的虚名而擅自删除仍生效的产品保护；
  - **D-06**：合并采用用户授权的“相对当前 `master` 净改进准入”，同时原样报告固定验收器结果（含剩余历史 TODO / skip 计数），不修改验收门禁来伪造全绿。

## 2. 目标

- **用户/维护者当前损失**：在多卡分波次推进过程中，容易残留“新库已接上、但旧分片/旧 Surface/旧桥接文件仍在 `compile-order.txt` 中编译”或“代码引用已删、但历史数据解码义务未交待”的半拉子状态。
- **这次完成后可少维护什么**：
  1. 全仓 53 个正式 `requirements/*/WHAT.md` 需求包与 `src/Wanxiangshu/` 全部 21 个顶层目录逐一完成终局对账，只允许四种明确去向：**已抽取并清旧（`DONE`）**、**明确保留（`RETAINED`）**、**经批准删除（`REMOVED`）**、**尚未闭合（转具体后继卡）**；
  2. 清理所有真正零消费者且无历史数据读取义务的旧编译分片（`.fsproj`）、`compile-order.txt` 条目、过渡适配桥与过期文档，确保没有“无人认领的残余职责”。
- **明确不做**：
  - 不另起炉灶再写一份“第二期大重构计划”；
  - 不通过删改 `requirements/*/WHAT.md` 条款或删除测试文件来让 `GAP.md` 债务凭空消失；
  - 不把明确保留的宿主适配层或产品模块批量改名为“兼容层”来假装全部抽入 `wanxiangli`。

## 3. 全仓 53 个正式需求包终局对账表（基线索引）

终局交账时，必须逐行核对以下 53 个 `requirements/<pkg>/WHAT.md` 的主责卡处置状态与测试套件回执：

| # | 需求包（`requirements/*/WHAT.md`） | 主责卡 | 协同/相邻卡 | 终局交账核对项 |
|---|---|---|---|---|
| 1 | `action-affordance` | CUT-08 | 05、25 | 工具描述五要素、`assume` 笃定、原生 `todowrite` 形状保持 |
| 2 | `attention-regulation` | CUT-22 | 09、16 | `defer`/`assume` 不建第二份 todo 或扩权 |
| 3 | `behavior-diagnosis` | CUT-30 | 05、16 | 按 D-05 裁决核对 Rulebook / `chronicle` 四句判词与有界修复 |
| 4 | `capability-enforcement` | CUT-08 | 07、10、25 | Schema 与执行门禁同源、6 类权威值分型、Manager 评审门收口 |
| 5 | `causal-wait` | CUT-21 | 12、13、32、37 | 等待观测零业务权威、读写能力隔离、未满足因果前沿 |
| 6 | `change-integration` | CUT-23 | 12、13、35 | 工作树隔离、Clean/Integration 短门、冲突归属 |
| 7 | `cognitive-environment` | CUT-05 | 17、30 | 五层认知材料组合顺序、Pair Hint 语言锚定与 `defer` 鼓励 |
| 8 | `cognitive-workspace` | CUT-16 | 22、38 | 画板已退役防回退、旧 `Cognition` 只读 no-op 兼容 |
| 9 | `concern-routing` | CUT-22 | 09、17 | 会话名邮箱自动订阅、单次公告、Pair Hint 边界投递 |
| 10 | `context-compression` | CUT-16 | 03、06、15、30 | 200 KiB 合同、真实用户原文与 `assume` 永久 raw、`todowrite` K=3 窗口 |
| 11 | `crash-reconciliation` | CUT-35 | 01、04、11—14、19、34 | 本地缓存归零、加载归位不重放命令、外部 effect 逐项闭合 |
| 12 | `degeneration-guard` | CUT-30 | 14、37 | `o200k_base` 双侧退化包络手动派生、单次中断与自有接续 |
| 13 | `delegation` | CUT-13 | 06、09—12、23、28 | `fork`/`resume`/`commission` 分离、因果身份认领、编译分层预算 |
| 14 | `dispatch-protocol` | CUT-11 | 09、10、12、34 | `PromptDispatcher` 唯一写入口、四态 claim、确定性 `PromptKey` |
| 15 | `distribution` | CUT-33 | 05、25、32、34、38 | 单一交付物、包根资源定位、`wanxiangli` 精确 tarball 捆绑闭合 |
| 16 | `durable-convergence` | CUT-19 | 18、23 | 活跃 writer 集合并集、`KWayMerge`、`DomainConflict`、UTC 日期 GC |
| 17 | `durable-events` | CUT-18 | 19、35 | Canonical JSON、64 MiB 段序列、唯一 Integrator、退役生命周期词表拒绝 |
| 18 | `effect-accounting` | CUT-11 | 01、04、12、14、23 | 意图与确认分型、先记账后行动、未知结果不盲目重发 |
| 19 | `epistemic-reasoning` | CUT-31 | 09、11 | 按 D-05 裁决核对认识状态与运行时分界 |
| 20 | `execution-failure-policy` | CUT-14 | 10—12、19 | 封闭失败分类、单一完整决策、先 exact 结算再 fatal、致命入口索引 |
| 21 | `execution-model-routing` | CUT-10 | 07、08、11、14 | `routingProtocol = 2`、真实 running multiset、exact opaque capacity fence |
| 22 | `feature-ablation` | CUT-37 | 08、30、35 | 消融 DAG 清单完整性、不跳过必要正确性 |
| 23 | `guidance-delivery` | CUT-30 | 05、15、17 | Frontier/Coverage 与原位后缀投递 |
| 24 | `host-boundary` | CUT-34 | 11、12、17、25、35 | 信号闭集、Typed Hook Membrane、编译分界、参数保险箱、只读租约校验 |
| 25 | `host-provider-failure-ownership` | CUT-14 | 34 | `chatMaxRetries = 0`、单次终态呈现、OpenCode 1.18.29 基线 |
| 26 | `interaction-authority` | CUT-09 | 07、11、12、22、35 | 四类来源、`AuthorityRootAccepted` 原子安装、五类 exact closure |
| 27 | `intra-participant-parallelism` | CUT-28 | 03、10、12、13 | Engineer subsession-only Fission、all-or-none 准入、环行 keyed 收敛 |
| 28 | `js-semantic-surface` | CUT-25 | 08、32、33 | JS 原生表示、Surface Manifest 登记、发射模块图闭合 |
| 29 | `knowledge-reuse` | CUT-27 | 03、06、19、24 | 按 D-05 裁决核对 Casebook 双基线、实质访问与 Bookkeeper 边界 |
| 30 | `managed-chat-execution` | CUT-12 | 04、10、11、14 | exact `(SessionId, PhysicalUserMessageId)`、固定事务序、纯恢复公式 |
| 31 | `managed-session-lifecycle` | CUT-12 | 04、13、34 | Attached 单一 owner、Handle 四态、DevOps 恢复与 PTY 排空、级联容量归还 |
| 32 | `obligation-ledger` | CUT-16 | 22 | 原生 `todowrite` 不改写、不建第二份 desired/applied、completed 才写 checkpoint |
| 33 | `office-capability` | CUT-08 | 07、25 | 按后果定义 Office、角色间不可互换、双语职责一致 |
| 34 | `participant-horizon` | CUT-17 | 05、06、13、22 | Decision Filter、不泄露机器拓扑与通用状态 DTO |
| 35 | `participant-identity` | CUT-07 | 08—14 | `ParticipantIdentity` 唯一 owner、换执行者不换人、prior-run closure 后复用 |
| 36 | `prefix-stability` | CUT-15 | 03、16、17 | 同 epoch 精确字节前缀、四类已提交冷边界、guidance 原字节回放 |
| 37 | `process-execution` | CUT-02 | 04、20、21、25 | 非 PTY `run` 与持续终端四动词分离、硬期限、UTF-8 留尾预算、`LargeGate` |
| 38 | `provider-attempt-recovery` | CUT-14 | 07、10、11、34 | Logical Run 失败预算、单次推进、首次失败保目标、等待确切宿主终态 |
| 39 | `provider-language` | CUT-05 | 08、17、30 | 无会话语言绑定、全局阶梯解析、成对资源、零静默回退 |
| 40 | `provider-projection` | CUT-17 | 03、05、06、08、15、16 | 纯代数管线、Semantic 与 Wire 隔离、单一 TOML 渲染器 |
| 41 | `repository-investigation` | CUT-29 | 24、26、27 | 按 D-05 裁决核对只读调查与低信任热启动线索 |
| 42 | `repository-programming` | CUT-24 | 08、25 | 不可变快照与有序锚点、`edit`/`rewrite`/`write`、All-or-Nothing 事务 |
| 43 | `requirement-grounding` | CUT-26 | 17、24 | `APPLIES-TO` 匹配、按内容版本去重、NUL+BOM 结果后缀补充 |
| 44 | `requirement-system` | CUT-26 | 00、32、38 | 条款编号与测试 `WHAT[PREFIX-NNN]` 锚点映射、`GAP.md` 唯一台账 |
| 45 | `semantic-trace` | CUT-03 | 06、15—17、27—30 | `XTrace` 唯一原始语义源、单调游标、`ProviderRetryAttempt` 排除 |
| 46 | `session-ontology` | CUT-04 | 07、12、13、34 | `ExecutionClass × Ownership` 正交分类、物理 family root 压平 |
| 47 | `speculative-investigation` | CUT-29 | 10、13、19、28 | 按 D-05 裁决核对 12 个参与工具估计协议、Predictor 常驻副本与双射恢复 |
| 48 | `sphinx-v2` | CUT-31 | 09—14、19、34 | 按 D-05 裁决核对独立 MCP 服务、目标/证据/槽位边界 |
| 49 | `structured-workflow` | CUT-35 | 00、32 | 原生控制流、subsystem 与 compile shard DAG、增量编译与依赖倒置 |
| 50 | `time-capability` | CUT-21 | 02、10、12、14 | 显式时钟端口注入、虚拟时钟推演、时间不充当权限 |
| 51 | `verification-system` | CUT-32 | 00、33、37、38 | 四级验证阶梯、因果静默监测、输入快照防篡改、嵌套输出通道隔离 |
| 52 | `work` | CUT-01 | 04、06、09、11—17、23、35 | S1—S3 与 S4 统一生命周期、`work.sqlite`、两步式交付与崩溃恢复 |
| 53 | `work-record` | CUT-06 | 03、13、16、27 | Opening + Chronicle + Recent work 三段物化、父→子与子→父方向视图 |

## 4. 逐文件与编译分片清理账（已知待收尾项）

在各波次因“并行期间不改 `compile-order.txt` 与共享 `.fsproj`”而暂留的无消费者残余，统一在本卡（或所属集成批次）清理：
1. `src/Wanxiangshu/Mission/Work/Retirement/Surface.fs(.fsi)` 及 `src/Wanxiangshu/Wanxiangshu.Owner.relay-retirement.mission-relay-retirement.fsproj`：
   - 现状：全仓零生产调用者、零测试引用、未在 `test-surface-scan.mjs` 注册（在 `CUT-01` / `CUT-04` 盘点中已确认为历史遗留死分片）；
   - 动作：移除 `mission-deliver-tool.fsproj` 中对它的 `ProjectReference`，从 `src/Wanxiangshu/compile-order.txt` 删除对应两行，删除该对 `.fs`/`.fsi` 与 `.fsproj`。
2. 各切刀（`CUT-02` 至 `CUT-35`）完成后退出的旧实现文件、旧 `*Surface.fs` 与对应 `.fsproj`：
   - 每删除一个文件，必须在交账表中记录：原 owner、承接卡、新公开入口或明确保留位置、剩余调用者（必须为 0）、历史数据读取义务处置及验证回执。

## 5. 静态门禁与终局交账硬条件（20 道检查 + 构建打包）

终局交账不仅看 grep 零命中，必须通过 `scripts/check.mjs` 注册的全部 20 道静态门禁及完整构建/打包验证：
1. `scripts/checks/architecture.mjs`（单一源码根、每个 `.fs` 唯一归属一个 owner shard、纯核心零越界引用）
2. `scripts/checks/aggregate-retired.mjs`（旧聚合 `Wanxiangshu.fsproj` 永不复活、`compile-order.txt` 完整覆盖全部 shard 源码）
3. `scripts/checks/participant-identity-boundary.mjs`（禁止 SessionId 键控的身份缓存）
4. `scripts/checks/provider-projection-boundary.mjs`（投影核心零外部业务模块泄漏）
5. `scripts/checks/subsystems.mjs`（subsystem 与 compile shard DAG 无环、无非法跨子系统边）
6. `scripts/checks/fatal-inventory-gate.mjs`（所有生产 fatal 入口与 `fatal-inventory.json` 精确对应）
7. `scripts/checks/fsharp-control-pyramid.mjs`（控制流金字塔零回退）
8. `scripts/checks/retry-owner.mjs`（`ExecutionFailurePolicy` 唯一重试授权 owner）
9. `scripts/checks/enforcer-bounds-owner.mjs`（Enforcer 内容硬界唯一纯决策 owner）
10. `scripts/checks/hook-policy.mjs`（全部 live Hook 与 `HookPolicy` 闭集元数据一一对应）
11. `scripts/checks/causal-wait-boundary.mjs`（等待词汇不进入 Fact/Journal，快照读取限在 `Execution/Session/Wait`）
12. `scripts/checks/unified-store-gate.mjs`（无私有持久化存储越界、无双写桥、唯一 `CanonicalIntegrator`）
13. `scripts/checks/tool-referential-integrity.mjs`（同名工具唯一 schema 与契约 owner、无废止工具名复活）
14. `scripts/checks/provider-leak-gate.mjs`（面向模型的渲染不泄漏机器 ID 与通用状态 DTO）
15. `scripts/checks/llm-facing-format-gate.mjs`（`LlmFacing` 唯一拥有合成 TOML 渲染）
16. `scripts/checks/language-parity-gate.mjs`（双语资源成对、无硬编码英文散文）
17. `scripts/checks/js-boundary-gate.mjs`（测试只经正式注册的 Semantic Surface 访问产品，不直连 `fable_modules`）
18. `scripts/checks/shard-edge-audit.mjs`（分片依赖闭包词法审计）
19. `scripts/checks/ablation-manifest.mjs`（消融节点、工具映射与需求包索引一致）
20. `scripts/checks/clean-break-symbols.mjs`（退役符号零残留）

## 6. 终局验证命令序列（交由 `devops` 执行）

```sh
# 1. 独立库完整构建、测试与打包检查
npm --prefix /path/to/wanxiangli test

# 2. 主仓格式检查、20 道静态门禁与 Fable 生产构建（含 js-surface-manifest 与 js-module-linkage）
npm run format:check
node scripts/check.mjs
node scripts/build.mjs --clean

# 3. 完整单元、集成与独立打包消费者验证（不带文件过滤与 TODO 覆盖开关）
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node scripts/verify.mjs
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node requirements/verification-system/tests/integration/run.mjs
node scripts/verify-package.mjs
```

## 7. 终局交账（2026-10-21，eng-cut35b-goose）

本节按卡 §2 的四种去向逐项交账：**已抽取并清旧（DONE）**、**明确保留（RETAINED）**、**经批准删除（REMOVED）**、**尚未闭合（转具体后继项）**。依 W6，下面列出的保留项和后继项都**不**计入“抽干”。CUT-36 是 OPTIONAL，不算遗漏。全部卡处置完毕，不等于软件再无缺陷。

### 7.1 看板终态

- 44 行：DONE 41（含 09-a/09-b/19-a/19-b/19-c 子卡和本卡），REMOVED 1（CUT-22），OPTIONAL 1（CUT-36）。建板时的卡全部处置完毕。本卡另在看板新增两张后继卡 CUT-39、CUT-40（BACKLOG，对应 F38-01、F38-02）。
- 各卡 DONE 的具体范围、证据和回退点在各自的 BOARD 行和 `cards/CUT-NN.md`。本节只汇总保留项、删除项和后继项，不重写各卡结论。

### 7.2 本卡的清理刀（真正零消费者的部分）

| 提交 | 内容 | 证据 |
|---|---|---|
| `7f0dd759d` | 删除 129 个零消费者的导出值和成员，涉及 75 个模块。删空的两个模块连同 shard 和 `compile-order.txt` 条目一起删除：`Change/Prompts`（`buildConflictResumePrompt` 无调用者）和 `Sphinx/V2/Runtime/Agenda`（`planDispatch` 可行性打包器，自 `Runtime/Decision` 拆分排序后无调用者） | 判据：在 `src/`、`requirements/`、`scripts/` 中，除了自身定义再无引用（`wt/logs-cut35b/deadvals.sh`）。刻意保留：`[<Literal>]` 资源键和 wire 标签（资源文件可能仍引用这些字符串）、`TaskResultBuilder.TryWith`（`try/with` 在 builder 里隐式调用，第一次构建因误删它而红，已恢复）、`Plugin.defaultExport`（JS 包入口）、Pty 互操作成员、所有 `*Surface` 模块（JS 测试边界） |
| `4ec0145fa` | 180 个只有本模块使用的值从 `.fsi` 撤下导出。只改签名，实现未动 | 同一扫描的 own-uses ≥ 2 部分；跳过成员（14）、`inline`、同文件重名（9） |
| `1954613ef` | 删除 `Git/Subject.fs` 中两段因删导出而变空的分节注释 | 发布序列中 format:check 在这个文件上报红，是本卡引入的；修后与 base 文件集合相同（§7.7） |

验证见 §7.7。两刀都只删除或收窄，没有改持久格式，可单独 revert。

### 7.3 需求包对账（现行 49 包 + 已删 5 包）

建板时的 53 包里，有 5 包已由仓主在看板之外整体删除（REMOVED，不记为抽取）：`behavior-diagnosis`、`guidance-delivery`、`speculative-investigation`、`work-record`（`584c30c61`，此前 `6eef89d1e` 退役了 Blogger/Chronicle/WorkRecord/Replica），以及 `requirement-grounding`（`da42898c4`）。另有 1 包在建板后新增，不在 COVERAGE 中：`advisor`（`131a79106`，2026-10-19），见后继项 F38-01。

| 需求包 | 主责卡 | 终局去向 | GAP 未闭合（单值状态行） |
|---|---|---|---|
| action-affordance | CUT-08 | DONE（capability 库）；工具描述资源 RETAINED 于主仓资源 | 0 |
| advisor | — | **未覆盖** → F38-01 | 0 |
| attention-regulation | CUT-22 | REMOVED（退休边界，无活算法） | 0 |
| capability-enforcement | CUT-08 | DONE | 0 |
| causal-wait | CUT-21 | DONE（wait/time 库） | 0 |
| change-integration | CUT-23 | DONE（git-porcelain/integration 0.61.0）；Git 物理进程 RETAINED | 0 |
| cognitive-environment | CUT-05 | DONE（resources 库）；业务文本 RETAINED 于各语义 owner | 0 |
| cognitive-workspace | CUT-16 | DONE（只读 no-op 墓碑保留，见 CUT-35 `144e7d020`） | 0 |
| concern-routing | CUT-22 | REMOVED | 0 |
| context-compression | CUT-16 | DONE（compression 库）；编号台账、o200k 计量、宿主消息重建 RETAINED | 0 |
| crash-reconciliation | CUT-35 | DONE（组合者收窄）；逐域恢复次序 RETAINED 为 C | 0 |
| degeneration-guard | CUT-30 | DONE（degeneration 库）；语料/包络派生、LoopSensor RETAINED | 0 |
| delegation | CUT-13 | DONE（handle-lifecycle/delegate-batch）；答复扇出 RETAINED | 0 |
| dispatch-protocol | CUT-11 | DONE（dispatch 库） | 0 |
| distribution | CUT-33 | DONE | 1 PARTIAL |
| durable-convergence | CUT-19 | DONE（convergence/retention/segments/quarantine）；宿主 I/O 与 EventStore Store/Surface RETAINED | 0 |
| durable-events | CUT-18 | DONE（events 库） | 0 |
| effect-accounting | CUT-11 | DONE | 0 |
| epistemic-reasoning | CUT-31 | DONE（ordinal/bayes）；Sphinx Core/Runtime 等 RETAINED | 0 |
| execution-failure-policy | CUT-14 | DONE（failure/failure-budget）；GAP-143/144 保留不关 | 1 PARTIAL |
| execution-model-routing | CUT-10 | DONE（routing/capacity/admission-queue/borrowing） | 0 |
| feature-ablation | CUT-37 | DONE（ablation 库）；清单读取、门禁投影、Host 诊断 RETAINED | 0 |
| host-boundary | CUT-34 | DONE（ratchet 0）；SDK 传输与 Hook 膜按 D-07 RETAINED 于适配层 | 1 PARTIAL |
| host-provider-failure-ownership | CUT-14 | DONE | 0 |
| interaction-authority | CUT-09 | DONE（authority/permit 库） | 0 |
| intra-participant-parallelism | CUT-28 | DONE（fission-group）；Admission/Runtime RETAINED | 1 PARTIAL |
| js-semantic-surface | CUT-25 | DONE（programming/sandbox） | 0 |
| knowledge-reuse | CUT-27 | DONE（casebook ×3）；Bookkeeper/Settlement/Host RETAINED | 1 PARTIAL |
| managed-chat-execution | CUT-12 | DONE（chat-execution/attachment-lease） | 2 OPEN |
| managed-session-lifecycle | CUT-12 | DONE | 0 |
| obligation-ledger | CUT-16 | DONE | 0 |
| office-capability | CUT-08 | DONE | 0 |
| participant-horizon | CUT-17 | DONE（projection 库） | 0 |
| participant-identity | CUT-07 | DONE（identity 库） | 0 |
| prefix-stability | CUT-15 | DONE（prefix 库） | 1 OPEN |
| process-execution | CUT-02 | DONE（process/pty/spool 库） | 0 |
| provider-attempt-recovery | CUT-14 | DONE | 1 OPEN |
| provider-language | CUT-05 | DONE | 0 |
| provider-projection | CUT-17 | DONE；宿主 codec/Trace/journal RETAINED | 0 |
| repository-investigation | CUT-29 | DONE（warm-start）；只读调查契约与 Semble MCP 传输 RETAINED | 0 |
| repository-programming | CUT-24 | DONE（fs/programming 库） | 0 |
| requirement-system | CUT-26 | DONE | 2 OPEN |
| semantic-trace | CUT-03 | DONE（trace 库） | 0 |
| session-ontology | CUT-04 | DONE（association/ownership） | 0 |
| sphinx-v2 | CUT-31 | DONE（数值层）；Core/Runtime/Persistence/Composition/Wire/Hosts 与插件 RETAINED | 0 |
| structured-workflow | CUT-35 | DONE（约束全程遵守，无第二运行时） | 0 |
| time-capability | CUT-21 | DONE（time 库） | 0 |
| verification-system | CUT-32 | RETAINED（主仓 supervisor 即 owner） | 1 PARTIAL |
| work | CUT-01 | DONE（lifecycle/assembly/eventstore） | 0 |
| behavior-diagnosis / guidance-delivery / speculative-investigation / work-record / requirement-grounding | 30 / 30 / 29 / 06 / 26 | REMOVED（仓主看板外删除） | speculative-investigation 1 PARTIAL 仍挂在 GAP → F38-06 |

GAP 台账（`requirements/GAP.md`）共 203 行。状态是单值的行：CLOSED 46、OPEN 6、PARTIAL 7，见上表末列。另外 144 行的状态栏是历史叙述而不是单值。本卡没有修改 GAP：不通过删行或改状态让债务消失。归一与已删包挂账的处置见 F38-06。

### 7.4 源码顶层目录对账（建板 21 个，现存 18 个）

| 目录 | 去向 |
|---|---|
| Ablation | DONE（规则 → ablation 库）；清单读取与门禁投影 RETAINED |
| Change | DONE（git-porcelain/integration）；Job/Program/Runtime 是 C，RETAINED。本卡删除了空模块 `Change/Prompts` |
| Composition | CUT-35 收窄后只剩真实 C 与持久桥；`AgentProjectionSet` 字段都有活读者，RETAINED |
| Context | Trace/Prefix/Companion 的规则已入库；宿主捕获与 Companion 宿主 RETAINED |
| Execution | Session/Delegation/Failure/Fission/Routing 的规则已入库；运行时、宿主端口和 C 部分 RETAINED |
| Foundation | 身份、时间、诊断等共通件 RETAINED（不做万能 Foundation 库） |
| Git | 物理 Git 能力 RETAINED（规则在 git-porcelain） |
| Host | 宿主契约与适配 RETAINED（CUT-34） |
| Interaction | Authority/Dispatch 规则已入库；Ingress/Send/Repair 接线 RETAINED |
| Mission | 工作内核已入库；Manager/Planning/Deliver 工具接线 RETAINED |
| OpenCode | 适配层 RETAINED（D-07） |
| Participant | Persona/Provider 语言、投影与尝试接线 RETAINED；规则已入库 |
| Persistence | EventStore/Journal 宿主 I/O RETAINED（CUT-19 §16） |
| Process | 物理进程/PTY 已改为库路由，监管者 RETAINED |
| Repository | Programming/Knowledge/Investigation 规则已入库；工具与宿主接线 RETAINED |
| Resources | 包内定位与资源读取 RETAINED（规则在 resources 库） |
| Sphinx | 数值层已入库；其余 RETAINED（CUT-31/35） |
| Verification | RETAINED（CUT-32） |
| Enforcer / Strength | REMOVED（仓主 `6eef89d1e`） |
| Requirement | REMOVED（仓主 `da42898c4`） |

wanxiangli 0.62.0 共 52 个模块、51 个 exports。主仓消费了除 `host-omp` 之外的全部模块；`host-omp` 是草稿，只属于 CUT-36。COVERAGE §3 的 0.5.2 草稿表已过时，本卡在 COVERAGE 中补了现状注记。

### 7.5 明确保留（RETAINED）登记：继续由主仓维护的显式参与者

- **组合者（C）**：`SpikePlugin`/`PluginBoot`/`PluginHostWiring`/`PluginSessionWiring`/`PluginRecoveryWiring`/`PluginRuntimeScope`、`Composition/Turn`、`Composition/Durable` 持久桥（CUT-35）。
- **宿主适配（A）**：`OpenCode/*`、`Host/*`、域内 `*/OpenCode/` 子目录（CUT-34，D-07）。
- **宿主 I/O**：EventStore 跨进程锁、原子追加/fsync/rename、解码前缀缓存、目录登记，EventStore `Store`/`Surface` 与 `StoreRef.canonical`（CUT-19/19-c）。
- **产品参与者**：Casebook Bookkeeper/Settlement/Host（CUT-27）；Fission Admission/Runtime（CUT-28）；Sphinx v2 Core/Runtime/Persistence/Composition/Wire/Hosts 与 Questionnaire/Probes/Inquiry/Provider（CUT-31）；只读调查契约与 Semble MCP 传输（CUT-29）；委派答复扇出（CUT-13）；Companion 编号台账、o200k 计量、宿主消息重建（CUT-16）；语料/包络派生、LoopSensor（CUT-30）；消融清单读取与 Host 诊断（CUT-37）。
- **验证系统**：主仓 supervisor（CUT-32）。

### 7.6 尚未闭合：具体后继项（不计入抽干）

| ID | 内容 | 由谁接 | 为什么本卡不做 |
|---|---|---|---|
| F38-01 | 新包 `advisor`（WHAT 001…，仓主 `131a79106` 于建板后新增，实现在 `Execution/Delegation/Advisor/`）不在 COVERAGE，未做抽取评估 | CUT-39（已登记 BACKLOG） | 需要按 README §6 A–H 从头做范围与产品裁决，不能在交账卡里顺手抽 |
| F38-02 | 8 个内核文件（`Mission/Manager/Workflow`、`Interaction/Repair/InteractionRepair`、`Interaction/Dispatch/{Send,Ingress,Dispatcher}`、`Participant/Provider/Attempt/Fallback/Workflow`、`Composition/Turn/OrdinaryTurnWorkflow`、`Composition/Durable/AgentJournalPortAdapter`），另有 8 个 Surface，仍 `open` 域内适配子命名空间 `*.OpenCode`。门禁 ⑧ 只统计顶层 `Wanxiangshu.OpenCode` | CUT-40（已登记 BACKLOG） | 需要先定棘轮口径并逐文件判定 A/B/C，属于 CUT-34 的边界工作 |
| F38-03 | `CompanionLeaseRuntime`/`AttachmentLeaseRegistry` 在生产中已无持有者（CUT-35 删了只写不读的插槽），只有测试面 `SatelliteSurface` 还在构造。生产的 companion 生命周期走 `PluginSessionScope.Companions` — **已裁决并处置（`938b966ff`）**：核查后两者仍是活的（`AttachmentLeaseRegistry` 在 `AttachedRuntime.fs:36` 被构造，`CompanionLeaseRuntime` 支撑 `AttachmentSurface.fs:235` 的共享 owner 伴生类型断言与 `managed-session-lifecycle-001`），因此**保留**二者；同批删掉 `CompanionHost` 上无人传入、函数体内也不读取的死参数 `?satelliteRuntime` | 已处置（`938b966ff`） | 裁决结论：运行时与注册表是活的，不能删；只删了确证无用的参数 |
| F38-04 | `node scripts/check.mjs` 在 master 上红：`fsharp-control-pyramid` 117–123 条（随仓主提交浮动，主要来自看板外的 `6f39cd156`、`5e76cb4a4`、`2d35b2ef0` 系列 delivery 重构）。本卡的候选没有新增条目 | 仓主当前的 delivery 重构批次（CUT-32 门禁 owner 复核） | 不是看板引入，也不改门禁来伪造全绿（D-06） |
| F38-05 | shard-edge-audit 的 2 条 high：`ChildRecoveryWorkflow` 使用 `AgentJournal`/`TaskDelivery`，但所在 shard 没有引用它们（PROTOCOL 地雷说明）。加 ProjectReference 会破坏 delegation-028 | CUT-13 后继 | 需要先移动代码或改依赖方向，不能用加引用了事 |
| F38-06 | GAP 台账：13 条单值 OPEN/PARTIAL、144 条叙述状态行未归一，另有挂在已删包上的条目（speculative-investigation、work-record、requirement-grounding） — **部分处置（`1d8b66d1d`）**：19 行状态归一（归档 18 / 关闭 53 / 开放 11 / 部分 112 / 退役 9，共 203 行保留），其余叙述状态行未动 | requirement-system（CUT-26 后继）—— 剩余行仍未处置 | 不通过删改 GAP 关闭债务（卡 §2 明确不做） |
| F38-07 | `scripts/checks/owner-impact-corpus.json` 是历史快照，仍列着已删文件（ToolSchemaJson、Recovery/Coordinator、Change/Prompts 等） — **已处置（`6109ed096`）**：按当前树重新测量语料并记录基线时间 | 已处置（`6109ed096`） | 它是报告语料，不是门禁；重新生成需要先定基线口径 |
| F38-08 | 余下可收窄的导出：14 个成员、9 个同文件重名值，以及 Surface 模块里只给测试用的导出。收益很小 | 按需（不另开卡） | 继续收窄的收益已经很低，本卡止于此 |
| F38-09 | 发布门禁在 base 上就是红的（§7.7）：wanxiangli `build-001` sandbox、64 个文件 format:check、pyramid 122 条、`run.mjs` 在 `repository-programming-032/036` 停住（580/765）、集成 `durable-events-022` 超过 300 s 兜底，以及各自的既有失败用例 | 发布 owner（CUT-32 后继） | 都不是本卡引入的；本卡只报告结果，不修改门禁 |
| CUT-36 | 第二宿主真实 canary | OPTIONAL，解除条件见 cards/CUT-36.md §6 | 需要用户指定宿主，并花真实模型额度 |

### 7.7 验证（README §9：同输入、同环境的 base 与候选）

**定向与全量套件（落地前）**：base `9679f699e`、候选 `4ec0145fa`，同一台机器、同一 765 个测试文件清单（`wt/logs-cut38/all-files.txt`）逐文件跑。base 失败 25 个用例名，候选 15 个，候选是 base 的子集，没有新增。少掉的 10 个是 `managed-session-lifecycle-013/015`，由仓主 `74e135d6c` 改写，与本卡无关。`node scripts/checks/architecture.mjs` 通过。

**发布序列（README §6 H，按原门禁如实报告，不改门禁）**：在 `4ec0145fa` 上跑 `wt/logs-cut38/release.sh`，然后在 base `9679f699e` 与 master `1954613ef` 上用同样的命令、同样的超时再跑一遍（`wt/logs-cut38c/rel2.sh`；只在编译时持有 heavy.lock，测试时不持有）。

| 门禁 | 候选（`4ec0145fa` / `1954613ef`） | base `9679f699e` | 结论 |
|---|---|---|---|
| wanxiangli `npm test` | exit 1：862 个用例中 1 个失败，`SPEC[build-001] sandbox` | 本卡没有移动 gitlink（base 与候选都是 `3be81f2`，0.62.0）；同一用例在 0.61.0 base 上也失败（`wt/logs-buildinc/n22-t-base-build-sandbox.log`） | 既有 |
| `npm run format:check` | `4ec0145fa` 有 65 个文件；`1954613ef` 有 64 个 | 64 个，文件集合与 `1954613ef` 完全相同 | 多出的 `Git/Subject.fs` 是本卡引入的（删除导出后留下两个空的分节注释），已由 `1954613ef` **修复**。其余 64 个是既有问题；其中本卡改过的 9 个文件，fantomas 修正量在 base 与候选上逐文件相同 |
| `node scripts/check.mjs` | exit 1：`fsharp-control-pyramid` 有 122 条 | 122 条，按“文件 + 规则 + 链”比对完全相同（行号不计） | 既有（F38-04）。shard-edge-audit 只报告，从 258 条降到 257 条 |
| `node scripts/build.mjs --clean` | exit 0 | exit 0 | 通过 |
| `requirements/verification-system/tests/run.mjs` | exit 1：运行器在 765 个文件中跑完 580 个后，卡在 `repository-programming-032/036` 的 stream drain 上，有 178 个文件没有启动；已跑部分失败 28 个用例名（`4ec0145fa` 跑两次与 `1954613ef` 结果一样） | exit 1：在同一位置停住（580/765）；失败 42 个用例名 | 既有。候选失败名单 ⊂ base；少掉的 14 个是 msl-013/015（`74e135d6c`） |
| `requirements/verification-system/tests/integration/run.mjs` | exit 1：`suites` 在 `durable-events-022`（真实 Fable 编译）处超过 300 s 物理兜底，37 个文件中完成 36 个；失败 8 个用例名；package 与 harness（288/288）通过 | exit 1：在同一位置、同样超过兜底；同样 8 个用例名，名单完全一致 | 既有 |
| `node scripts/verify-package.mjs` | exit 0 | — | 通过 |

日志：`wt/logs-cut38/rel/`（候选）和 `wt/logs-cut38c/`（base 与 master 复跑、format 与 check 比对）。发布门禁里红的几项都不是本卡引入的，所以不能说“全绿”。运行器停住和集成兜底超时列入 F38-09，交发布 owner。

### 7.8 结论

所有 CUT 卡都已处置。没有无人认领的残余职责：保留项列在 §7.5，未闭合项都有编号后继项（§7.6）。**本仓并未“全部抽干”**：主仓仍保留组合者、宿主适配、宿主 I/O 和多个产品参与者，这是逐卡裁决的结果，不是遗漏。

# CUT-38：全仓残余职责与终局交账

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-38) / [范围对账](../COVERAGE.md) / [GAP 台账](../../../requirements/GAP.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`BACKLOG`（已完成全仓 53 个需求包、21 个生产源码顶层目录与 20 个静态门禁的对账基线盘点）
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

# Meditator DSL — 威胁模型与已知限制（SSOT 附录）

> 本文是 `AGENTS.md` 的安全边界附录：部署者必须阅读。代码内的安全声明（如 `Ledger.fs` 的威胁模型注释）以此文为准。

## 1. 信任边界

| 组件 | 信任等级 | 说明 |
|---|---|---|
| 应用代码（本仓库 `dsl/` 程序集内） | **受信任** | 编译期 `internal` 权柄（`EvaluatorAuthority`/`VerifierWitness.issue`）只对程序集内代码与 `InternalsVisibleTo` 的测试程序集开放；外部代码无法签发 witness、无法读取 witness、无法构造推理链 |
| Journal（NDJSON 文件） | **受信任的完整性边界** | DSL 防程序错误与配置漂移（sequence 连续、digest 校验、round-trip canonical、verifierId 白名单、计数溢出拒绝），**不防持久化介质恶意篡改**——SHA-256 是无密钥摘要，能改 journal 者可重算 `EventId`/`PayloadDigest` 使整行"合法" |
| Transcript store（`IAcceptedTranscriptStore`） | **受信任** | 与 journal 的一致性由 `AcceptedTranscripts` 账本（fold 同 key 异 digest 拒绝）兜底；store 丢失且重问异 transcript → 恢复被阻断（fail closed） |
| LLM Oracle | **不可信** | 只能经任务专用端口提出候选（`Proposal`）；生成与接受分离，Oracle 产物不产生 warrant |
| 注入的 prover/compiler | **不可信** | Kernel 全量机械重验：contract digest、义务空、停证 digest/unknowns/grade/discharged/coverage、报告 intent/scope/引用/polarity/文本绑定/章节；完成事件由内核构造（无 encoder 注入） |

## 2. 已知限制（按优先级）

1. **恶意 journal 篡改防护缺失**：对抗"能修改 journal 文件的攻击者"需要 witness/事件签名或 MAC、外部锚定的 hash chain。当前模型下此类攻击者可以伪造任何"合法"历史——**部署时必须保护 journal 文件系统权限（PERSIST-006：0700/0600）**。
2. **程序集内能力隔离（138 版）**：`internal` 只阻止外部程序集伪造，不阻止程序集内模块越权（Oracle 模块意外签发 observation witness 等）——当前威胁模型将整个 DSL 程序集定义为受信任，越权防护依赖代码审查；真正的能力隔离需拆独立 verifier 程序集或模块 `private` 权柄（演进方向）。
3. **停证级 grade 无显式 bottom**：报告层已引入 `ReportGrade = NoEvidence | Graded`（P0-7，空证据报告为 NoEvidence）；停证级 `EpistemicGrade` 的空证据基准（`Direct/Confirmed/Clusters 1/OpenWorld/NotYetReplayed`）表示"无证据默认基准"，不表示高可靠性——停证级 bottom 属 grade 深化阶段，基准值已被测试锚定。
4. **强名称签名未启用**：`InternalsVisibleTo("TestHarness")` 无公钥限定（仅 DEBUG 构建存在；Release 生产程序集无 IVT）。启用强签名需 `.snk` 工具链（linux 环境无 `sn`），CI 环境补签名后 IVT 应带 `PublicKey=`。
5. **`deterministic-check/v1` 白名单语义**：该权柄在 verifierId 白名单中与 `schema/v1` 同列——`Validated`/`Constructed` 出口的复核 witness 以 `Schema` kind 持久化；其他 kind 会被拒绝（fail closed 是安全的）。
6. **P0 禁用 deduction（137 版）**：Claim 是自然语言字符串、无命题 AST，任何步骤检查都无法机器验证真实推理——`RuleEngine` 规则表为空，`deduce` 不可用（诚实禁用而非伪验证）；待命题 AST 落地后按版本化规则注册。A3（grade 不升级）以 `derivationStrength` 函数级测试锚定。
7. **P0-1（137 版）已收口**：`EventCodec.decode`/`decodePayload` 改 internal——"手工拼 canonical 行经 fromFields 获得 Warrant"的 codec 铸造路径关闭（解码仅限程序集内受信任重放路径与测试）。
8. **WarrantId 派生（137 版）**：WarrantId 由 canonical 内容派生（`warrantIdOfData`），fold 与 decodePayload 双层重验——任意指定 ID 的 warrant 进不了账本/日志；create 因编译顺序（Warrant 模块在 codec 之前）不校验，由账本/恢复路径兜底。138 版进一步：身份不含 ProducedAt/IntroducedBy（提交后 ID 仍匹配）、集合字段排序去重。
9. **安全公共验证路径（139 版，方案 A，定位：特权 attestation port）**：封闭 witness 后保留 `PublicVerification.observe`（提交协议回执与 claim → `Accepted<Claim>`）与 `warrantFromObservation` / `warrantFromObservationOpposing`（Accepted → Observation warrant；强度固定 Moderate；producedAt 由调用方从环境时钟注入；polarity 由调用方声明的观察结论决定——Supports/Opposes 两个构造，同一 receipt 语义）——外部（Release、无 IVT）可完成双侧 ClaimTest。**宿主边界（138 版 security-review）**：`protocolDigest` 由调用方自报、无调用者身份绑定——宿主必须只在受信任上下文暴露（或绑定 `IAcceptedTranscriptStore` 中真实存在的 transcript digest）；"安全公共验证服务"的准确名称是**特权 attestation port**：调用者声明协议已执行与观察结论，库负责以受控 witness 签名（139 版评审 #2/#3）。**独立证据防护（139 版评审 #4）**：依赖簇按 witness 分组——同一 Accepted 复制成多个 source 仍落同一簇（同一次观察不能伪装为多个独立证据簇）。receipt 类型化（ObservationReceipt 绑定 claim/scope/协议/来源/时间）为演进方向。
6. **接受的外部攻击者测试项目**（"编译失败即通过"的负向编译测试）未建立：internal 性目前由反射测试锚定（`Tests.Properties.fs` 的 issue/fromFields/create/data/witnesses 非 public 断言）。
7. **UnacceptableClaims 采用子串近似检查**：报告文本包含禁止主张的子串即拒绝（无注入面；精确语义匹配属自然语言理解范畴，不在确定性内核承诺内）。

## 3. 模型演进方向（不构成当前承诺）

- witness/事件签名（HMAC 或 Ed25519）+ 外部锚定根摘要
- 类型化 receipt（`ObservationReceipt` 等）绑定 claim/scope/protocol（139 版评审 #2：`IObservationVerifier.Verify → ObservationReceipt` + `Warrant.fromObservation : ObservationReceipt -> Warrant`——当前 attestation port 的 protocolDigest 自报由宿主信任上下文兜底）
- 报告自由事实注入区结构化（139 版评审 #14）：`ReportStatement = LedgerBacked | NormativeRecommendation | ExplicitUnverifiedNote`——当前 Qualification/EvidenceLimitations/Recommendation 只查 UnacceptableClaims，compiler 为受信任装配组件
- 独立证据防伪强化（139 版 security-review）：依赖簇已按 witness 分组（同一 Accepted 复制多 source 同簇），但 witness digest 对 Observation 是调用方自报原文——**重复 observe（换 protocolDigest）可放大 Independence**；方向：witness digest 绑定真实回执内容哈希或限制每 claim 观察数。同一 receipt 双侧 polarity（Supports/Opposes 自报）同理——方向：witness digest 纳入极性或 receipt 类型化。
- **ClaimMorphology 由装配申报（140 版 security-review）**：`TargetRefuted` 的形态-规则匹配（`refutationRuleApplies`）在 create 强制，但 claim 无形态字段——装配可将统计命题申报为 `Universal` + `LogicalCounterexample` 走通出口（attestation 语义：宿主信任装配；与 deduction 的"无 AST 诚实禁用"先例有张力）。方向：形态锚定到 claim（ClaimFramed 时声明，fold 校验）或按先例禁用；当前记录为已知限制。
- 停证级 grade bottom（报告级 NoEvidence 已实施，见 §2.3）
- 强命名 IVT + 攻击者测试项目

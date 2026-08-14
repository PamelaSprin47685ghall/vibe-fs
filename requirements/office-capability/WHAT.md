# office-capability — 可观察合同

本文件是 `office-capability` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。
证据指针 → `PROOF.md`。边界 → `HOW.md`「边界与弃权」。

## OFF-001：office 由 entitled consequence 定义，不由 persona 名 / 工具名 / 权限清单定义

office capability = 该职位有权产生的后果，不是 persona 名、不是工具名、不是权限清单的口语转写
（ARCH-017）。调用方按「后果」认识 office：`Know another office by its promises, not by its keys`。

含义/动机：`docs/why/architecture.md`「名单 vs 有权产生的后果」：权限矩阵已精确，但调用方看不到
被调用方 Role Law 时会把 Inspector 当「另一个能处理 repository 的 agent」；工具可达 ≠ 有权做。

边界：工具名 → 工具可见性清单归 `capability-enforcement`（矩阵是投影）；「后果」本身是本包唯一
权威模型。

证据：`tests/office-capability-integrity.test.mjs` `OFF_001_office_capability_is_consequence_not_tool_whitelist`。

## OFF-002：canonical 五分法：五类可 fork office 各有唯一 entitled consequence 与 non-consequence

Manager 当前可 fork 的五类 Office（Coder / Inspector / DevOps / Browser / Inquiry）构成唯一 canonical
五分法（AGENT-009 / ARCH-017），每类有**有权产生**与**不做**两条清单：

| Office | 有权产生 | 不做 |
|--------|----------|------|
| Coder / Engineer | repository source mutation（实现、修复、重构、tests-as-source、受托含义的 docs/source/config） | 运行项目；执行测试/构建；铸造或认证 runtime evidence；未被托付的产品/架构决定 |
| Inspector / Scout·Investigator | 关于本地已存在事实的证据 | 修改 source；实现修复；跑测试/构建/应用；创造新的行为证据 |
| DevOps / Technician·Operator | 运维行动与行为证据 | 发明产品含义；在若干实质不同的合法行为之间作产品选择 |
| Browser / Navigator·Researcher | 带 provenance 的外部事实 | 实现仓库工作；把外部可能性变成仓库义务 |
| Inquiry / Analyst·Inquirer | 对未决问题的语义理解 | 改变 source；执行世界；把思想变成证据 |

含义/动机：五分法是「按后果托付」的完整枚举；缺失或重叠 = 调用方无法确定该把哪种后果交给谁。

边界：五分法是当前证据、可重构（boundary card DOES NOT OWN）；权限矩阵投影归
`capability-enforcement`（AGENT-006）。

证据：`tests/office-capability-integrity.test.mjs` `OFF_002_managed_catalog_forkable_offices_are_exactly_the_five_canonical_offices`
+ `OFF_002_each_office_role_law_carries_its_entitled_consequence`。

## OFF-003：同一 office 跨 Persona/ExecutionBinding 时 authority 不变

同一 Office 的两个 calling 名（fast/deep）只差 persona 与推理深度，不差 authority（ARCH-017；
`resources/provider/tool/fork/description` 双语文档明文）。fast/deep 权限一致（AGENT-010 交叉）。

含义/动机：caller 选后果不选权限档；换深度不是换权限（`docs/why/agent.md`「fast/deep 随换模型
演化成两套产品」是反面案例）。

边界：tier/ExecutionBinding 的机器精度 → `participant-identity`；权限相等的结构性证明 →
`capability-enforcement`（ENF-004）。

证据：`tests/office-capability-integrity.test.mjs` `OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority`。

## OFF-004：capability 是 consequence model，不是 tool whitelist 的口语转写

权限矩阵（AGENT-006）是 enforcement 投影，不拥有认知面；office 的认知面是后果模型（ARCH-017）。
不得用工具清单反推 office 定义，也不得把 consequence 降格成「能调用哪些工具」。

含义/动机：把 Inspector 定义成「能 read/glob/grep/query-shell 的角色」会诱导「小 Coder + 少权限」的
错误心智；定义必须是「建立已存在事实的证据」。

边界：矩阵与 gate 的**同构执行** → `capability-enforcement`；本包只拥有「认知面 = 后果」这一分界。

证据：`tests/office-capability-integrity.test.mjs` `OFF_001_...`（manager law 按承诺不按键认识 office）。

## OFF-005：单一语义所有权、多处投影：consequence 在所有决策面同 ID 命中，不得漂移

同一条 entitled consequence 投影到 Manager Role Law（世界观）、`fork` description（调用瞬间的可行动
选择）、各 Office 自己的 Role Law（自我模型）、caller-facing tool（如 `inspect`，调用方必须看见的
边界镜）（ARCH-017 投影表）。投影文案可以不同；entitled consequence 不得不同；禁止手工维护五份
互不相干清单（PROMPT-021 单一语义所有权）。

含义/动机：真实事故——Coder 按 inspect tooltip 把修复交给 Inspector（`docs/why/architecture.md`
「关键区别：单点陈述 vs 每个会改变行动的决策面」）。

边界：Gate F 机制（`scanOfficeCapabilityIntegrity`）由 `verification-system` 提供共享 checker，
语义 oracle（OFFICE_CAPABILITY_ANCHORS 5 id）是本包拥有；action 描述的五问认知合同 →
`action-affordance`。

证据：`tests/office-capability-integrity.test.mjs`
`OFF_005_each_office_consequence_hits_manager_law_and_fork_description_in_both_locales` +
`tests/unit/verify/language-parity-gate.test.mjs` `gate_f_*`（fixture 可红性）。

## OFF-006：offices 不可互换：禁止把 office 当可互换通用 agent

```text
A Coder is not an Operator who happens not to have a shell.
An Inspector is not a Coder with fewer permissions.
DevOps is not a convenient escape hatch for any difficult repository task.
Inquiry is not a witness merely because it can reason about evidence.
Browser is not a local repository investigator merely because it can open a file-like representation.
```

（ARCH-017 negatives；Manager Role Law 双语文档必须携带；`fork` description 不得把委托写成
「commission another witness」。）

含义/动机：互换心智是「机器拓扑冒充资格」的具体形态；`tests/eval/provider-office-boundary` 四个
oracle 即为可红的行为判据。

边界：interchangeability 的 enforcement（权限层）→ `capability-enforcement`；本包拥有「office 边界
不可互换」这一认知合同。

证据：`tests/office-capability-integrity.test.mjs` `OFF_006_offices_are_not_interchangeable_general_purpose_agents`
+ `tests/eval/provider-office-boundary/office-boundary-eval.test.mjs`（四个 oracle 全绿）。

## OFF-007：Manager 无普通工具：不读文件、不跑终端、不改仓库、不 inspect

Manager 的 entitled consequence 是协调（think / entrust / integrate），不是亲手建立 repository
事实（AGENT-011）；工具面只有 fork / join / horizon / todowrite / fission / suicide + auto-injected
（后者是 HOST-013 no-op，非业务能力）。

含义/动机：Manager 若可亲手取证/改库，分层（推理→证据）会塌成「便宜证据自己看」；`no-personal-
repository-witness` 锚点（`do not establish repository facts with your own hands`）是该 non-consequence
的投影。

边界：工具面矩阵的 schema 投影 → `capability-enforcement`；Manager 的认知/世界观其余内容 →
`cognitive-environment`。

证据：`scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager.no-personal-repository-witness`
（REUSE：由 `language-parity-gate.test.mjs` `gate_c_semantic_anchor_parity_*` 验证双语文档命中）。

## OFF-008：Coder consequence = repository source mutation；non-consequence = 运行项目/认证证据/未被托付的决定

Coder 有权改变书写出来的世界（实现、修复、重构、tests-as-source、受托含义的 docs/source/config）；
不运行项目、不执行测试/构建、不铸造或认证 runtime evidence、不作未被托付的产品/架构决定。
`mv`/`rm` 只进 Coder（AGENT-016）；`bash-honeypot` 仅 Coder 且不执行 shell、只是越权拒绝文本
（AGENT-023，非放行 bash）。

含义/动机：Coder 与 DevOps 的 existing-evidence/new-behavior 边界是独立可重画的
（boundary card INDEPENDENT CHANGE）。

边界：`mv`/`rm` 的 POSIX 语义 → `repository-programming`；bash 对 managed role 的 deny 执行 →
`capability-enforcement`。

证据：`tests/eval/provider-office-boundary` `coder-inspect-ownership`（oracle：Coder 把修复交给
Inspector 被拒）+ `tests/office-capability-integrity.test.mjs`（coder law 双语文档携带 consequence）。

## OFF-009：Inspector consequence = 已存在事实的证据；non-consequence = 修改/修复/当验证代理

Inspector 有权建立 repository 中已经存在的事实的证据；因果只读，不修改 source、不实现修复、
不跑测试/构建/应用、不创造新的行为证据；不得泄露 query-shell/取证权、不得当常规验证代理
（AGENT-012 consequence 侧）。

含义/动机：Inspector 是见证者，不是第二双写代码的手；`consequence-not-verdict`（stop before the
evidence becomes a verdict）是「证据 ≠ 判决」的边界。

边界：evidence acquisition 的方法论（causal-readonly、evidence funnel）→ `repository-investigation`；
`inspect` 工具的调用瞬间合同 → `action-affordance`。

证据：`tests/eval/provider-office-boundary` `inspector-refuses-repair`（oracle：Inspector 拒绝修复）+
`tests/integration/plugin/manager-tool-contract.test.mjs` `EXEC_002_inspect_tool_description_forbids_mutation_and_execution`。

## OFF-010：DevOps consequence = 运维行动与行为证据；non-consequence = 发明产品含义/直接 write-edit

DevOps 有权对运行中的世界行动并产生行为证据（builds/tests/processes/terminals/migrations/
benchmarks/runtime diagnosis）；文件修改只能经同步 `establish-behavior`/`repair-behavior` 委派，
不能直接 `write`/`edit`（AGENT-013）；不发明产品含义、不在实质不同的合法行为之间作产品选择。

含义/动机：DevOps 不是任何困难 repository 任务的方便逃生口；`mechanical-meaning`（含义已经被
决定）与 `coder-report-not-evidence`（Coder 报告不是执行证据）是它的 non-consequence 锚点。

边界：terminal/PTY 的物理 act 语义 → `process-execution`；`run`/终端四动词的矩阵投影 →
`capability-enforcement`。

证据：`tests/eval/provider-office-boundary` `devops-does-not-choose-among-valid-behaviors`（oracle：
DevOps 不得作产品选择）。

## OFF-011：Reviewer consequence = 只读 + judge；non-consequence = 写文件/跑命令

Reviewer 有权审阅（只读工具 + `judge`），不能写文件、不能跑命令（AGENT-014）。

含义/动机：评审者若可改库/跑命令，judgement 的独立性被工具面破坏。

边界：PERFECT/REVISE 的判断标准 → `review-judgement`；judge 工具合同 → `action-affordance`。

证据：`requirements/capability-enforcement/tests/agent-permission-gate.test.mjs`
`ROLE_ALLOW.Reviewer = [read, glob, grep, judge, auto-injected]`（矩阵投影，交叉 REUSE）。

## OFF-012：Orchestrator consequence = commission manager；不 commission 其它 office

Orchestrator 只 commission fast/deep-manager（新路）或按 Byname 续做既有路（AGENT-015 consequence
侧）；不产生 job/worktree 等机器字段的可见性（后者归 `participant-horizon`）。

含义/动机：Orchestrator 是「路的集成者」，不是通用执行面；委托面只有 manager。

边界：commission 的委托语义（新路/续做）→ `delegation`；机器字段隐藏 → `participant-horizon`。

证据：`requirements/capability-enforcement/tests/agent-permission-gate.test.mjs`
`ROLE_ALLOW.Orchestrator = [commission, join, horizon, auto-injected]`（矩阵投影，交叉 REUSE）。

## OFF-013：Browser consequence = 带 provenance 的外部事实；non-consequence = 实现仓库工作/把外部可能性变成仓库义务

Browser 有权从外部世界建立带 provenance 的事实（ARCH-017 Browser 行）；不实现仓库工作；外部可能性
不自动变成 repository/product obligation。

含义/动机：`docs/why/agent.md` Browser 网络面选型（stealth-browser MCP vs 插件 network 工具）：
工具必须真实可执行且仅 Browser 可寻址；本地文件/仓库不是 web evidence。

边界：provenance 合同的建立细节（source-closest、disagreement、visual）→ `external-investigation`；
`stealth-browser-mcp_*` 的 schema 投影 → `capability-enforcement`。

证据：`tests/office-capability-integrity.test.mjs`（browser consequence 双语文档命中）+ 交叉：
`requirements-design/20-capability-external.md`（external-investigation 引用本包 Browser consequence）。

## OFF-014：Inquiry consequence = 对未决问题的语义理解；non-consequence = 改变 source/执行世界/把思想变成证据

Inquiry 有权分辨尚未明确的问题（hypotheses、语义区分、竞争性解释）；不改变 repository、不执行
世界、不把思想变成证据（ARCH-017 Inquiry 行）。

含义/动机：Inquiry = reasoning，Inspector = evidence acquisition（AGENT-025 分层）；Sphinx 是认识
状态求解器，不是第二套扫库工具。

边界：认识状态求解（epistemic state、proposal≠evidence）→ `epistemic-reasoning`；Inquiry 工具面
（inspect + sphinx_*）投影 → `capability-enforcement`。

证据：`tests/office-capability-integrity.test.mjs`（inquiry consequence 双语文档命中）+ 交叉：
`tests/unit/agent/inquiry-permissions.test.mjs`（Inquiry 工具面 = inspect + Sphinx，REUSE）。

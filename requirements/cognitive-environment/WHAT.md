# WHAT —— 唯一 normative 合同

命题前缀 `COGNITIVE-ENVIRONMENT-`。每条都是**当前世界必须同时成立**的事实。
证据指针 → [`PROOF.md`](PROOF.md)。

## 认知层组织（Prompt Composition Protocol）

### COGNITIVE-ENVIRONMENT-001：五层组合，每份材料恰属一个主权威

万象术没有单一 system prompt。每个 provider-facing 自然语言材料恰属一个主权威（PROMPT-015）：

```text
World    什么在这里普遍为真        → Common Law + shared mythology
Role     你是谁、什么属于你        → Role Law（fast/deep 共享）
Library  继承的技术知识            → Office Library
Runtime  这次 invocation 此刻为真  → 生命周期/事件注入
Mission  这个 assignment 必须成为什么 → 当前 charge
```

### COGNITIVE-ENVIRONMENT-002：层可告知，不得冒充；冲突按语义所有权裁决

- 层可以 inform（告知相关事实），不得 impersonate（冒充其它层）；
- 冲突按语义所有权边界裁决，**不**设「更靠近 system 者胜」全序（PROMPT-015）。

### COGNITIVE-ENVIRONMENT-003：canonical 组合顺序

```text
SYSTEM: Common Law → Role Law → Office Library
TOOLS:  当前生成的工具面（不属于 Role 章节）
RUNTIME/CONVERSATION: 生命周期与事件注入
USER/ASSIGNMENT: 当前 mission
```

（概念顺序 ≠ wire；`Infrastructure/Resources/PromptResources.fs` 的 `systemForRole` 是现行实现。）

### COGNITIVE-ENVIRONMENT-004：Tools 不是 Role Prompt 章节

System Prompt 含责任、认识论、authority boundary、craft、易犯认知错误、与相邻 Office 的关系；
它**不得**枚举当前 runtime 的全部工具。capability 变化不改人格；拥有 tool ≠ 获得 authority
（PROMPT-015）。

### COGNITIVE-ENVIRONMENT-005：Role Law 是长期 self-model 层

- Role Law 回答「我是什么样的参与者」；
- 同一 office 的 fast/deep 共享**同一** Role Law、同一 prompt、同一工具面，只模型绑定不同
  （AGENT-001、COMPANION-004、ENFORCER-030）；
- Role self-model 不枚举全部瞬时 capability state；不出现 `fast-`/`deep-` 机器身份自称
  （AGENT-029）。

## knowledge ≠ authority

### COGNITIVE-ENVIRONMENT-006：Office Library 是继承的技术书籍，不是 Common Law

```text
Law tells you what must remain true.
Role tells you what is yours to decide.
Books teach you how predecessors learned to do it well.
The assignment tells you what must become true now.
```

Office Library 保存职位历代 craft；**不是** Common Law，不定义角色 authority（PROMPT-016）。

### COGNITIVE-ENVIRONMENT-007：知识可跨 authority 边界流动，authority 不随知识流动

`Information may cross authority boundaries. Authority does not travel with it.`
继承知识（书）可以教识别缺陷、验证技术，但**不**授予修复权/执行权。craft 可跨 authority boundary
流动，authority 不随知识流动（PROMPT-016；boundary card）。

### COGNITIVE-ENVIRONMENT-008：Library 三轴：Class × Delivery × Audience

- Class：Rulebook / Handbook / Ledger / Atlas / Field Notes；
- Delivery：Inherited Volume / Triggered Folio / Request-Bound Volume；
- Audience 绑 semantic role 或 request contract，**不**绑 model strength；fast/deep 不造第二套
  思想传统（PROMPT-016）。

### COGNITIVE-ENVIRONMENT-009：Library 禁令

禁止：书扩大 Role 权；universal bible 灌每个 persona；同 role 的 fast/deep 异书；把隐藏编排写入
Reviewer 书；复制已有 canonical 成第二真源。若他处已有 SSOT，Library 组合引用（PROMPT-016）。

## 瞬时与长期的边界

### COGNITIVE-ENVIRONMENT-010：生命周期文本只 orient，不 educate

六种生命周期文本（Activation / Reawakening / Continuation / Handoff / Fission / Departure）只 orient，
不 educate，不叠第二套 envelope；generic Activation ≠ Manager BlindPlan phase，不得触发 system prompt
替换（PROMPT-015 / TODO-015）。

### COGNITIVE-ENVIRONMENT-011：瞬时 runtime/mission 不重写长期 self-model

`The system prompt names the office. The conversation tells you which road is yours.`
T1 revelation 只走 conversation tool result；不得经 Prompt 路径伪造 Activation / system prompt 替换
（PROMPT-014 引用 / TODO-015）。同一 life 内 system prompt 身份字节稳定（byte-stability 本体归
`prefix-stability`；本包拥有「身份由 office 决定，不由阶段决定」的认知面）。

### COGNITIVE-ENVIRONMENT-012：Reviewer prompt 不灌输流程机制

Reviewer 提示词 = Role Law + Examiner's Ledger 组合；双 PERFECT 流程完全由 Host 执行，**不**进入
Reviewer prompt（REVIEW-012）。ProcessReview 隐藏编排（dedicated session / barrier / witness / 2N）
不进任何 provider 认知层（PROMPT-016 禁令 + 008 的 hidden surface 交叉）。

## craft 资产

### COGNITIVE-ENVIRONMENT-013：Pair Hint 是 canonical craft payload

Pair Programming Hint（HOST-013 occurrence 的正文）是一个 canonical semantic payload，至少同时要求：

- 简体中文思考纪律（或所在语言世界的对应纪律）；
- 把 `[NEEDHELP]` 视为正常、可早用的协作请求——不是 failure、资源匮乏、羞辱或失败声明；
  provider-visible guidance 不暴露 fast/deep 内部身份（AGENT-031 / increase-strength §3）；
- 当前 tool surface 暴露 `todowrite` 时，把它视为工作事实账本而非可选进度播报：只要现实变化使当前
  account 不再准确（义务完成/解除、新义务出现、义务发生实质变化、确认不再需要），必须在继续下一段
  实质工作或调用其它工作工具前先提交完整最新 account；不得等阶段结束、用户追问或最后批量补记，
  自然语言宣称“已完成”或打算稍后更新不构成账本更新。account 未变化时不得为了形式重复写。
- 每次工具 turn 前寻找完整 parallel wave：已知、确有用、彼此独立的调用默认同一 assistant turn
  一起发出，最小化 provider↔tool RTT；仅真实数据依赖/共享可变 owner/协议顺序/破坏性干扰/明确有限
  容量可序列化；不猜未知参数、不制造无用调用、不写死全局并发数字（pair-parallel-tools §3-§14）。

该语义**不**按 provider 复制；wire 形状由 renderer 决定（`prefix-stability` / `provider-projection`）。

### COGNITIVE-ENVIRONMENT-014：delegated tool estimate 是校准提示，不是服从预算

当当前 participant 持有 delegator 提供的 `expected_tool_calls` measurement 时，新的 Pair Hint occurrence
可追加一段动态 calibration：明确“根据委任者估算，目前还剩约 X 次工具调用”；同时明确 X 不是执行上限。
若当前方案预计超支，应主动收缩/重排范围、缩短验证路径、提高真实可并行工作，或仅在自身已有相应
capability 时考虑委派/分裂。提示不得宣称 participant 拥有其实际没有的 delegation/fission capability。

没有 delegator estimate 的 user-facing/root participant 不出现该段；`X=0` 仍只表示估算已耗尽，不要求
停止工作或停止调用工具。X 的计量与 replace/retain 语义归 `delegation`（DELEG-022）；本包只拥有 provider
该如何理解这个 measurement 的 craft。

## 反向覆盖

本包吸收的 OWNED clause（COVERAGE.md 归属）：PROMPT-015、PROMPT-016、AGENT-031（NEEDHELP 正常协作
craft）、HOST-013（Pair Hint 正文 craft 部分）、COMPANION-004（Blogger Role Law 组合）、
ENFORCER-030（Blogger 统一 system）、REVIEW-012（Reviewer 提示词组合）。跨边界：NEEDHELP 的
consultation（delegation）、authority continuity（interaction-authority）、wire 注入
（provider-projection/prefix-stability）、same-run authority（interaction-authority）**不**归本包
（HANDOFF §10.2 WATCH 如实标注：craft 归本包，其余归各自 owner）。

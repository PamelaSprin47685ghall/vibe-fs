# WHAT — review-judgement

> 本文是 `review-judgement` 包的**唯一 normative 合同**。每条命题都是当前世界必须同时成立的事实。
> 证据指针（→ HOW.md 行号）指向本包 HOW.md 的落点表。

## 术语（本包内定义）

- **judgement**：Reviewer 对「当前工作是否足以被接受」的裁决，wire 形态为 `verdict = PERFECT | REVISE`。
- **material defect**：其后果（对照真实托付追下去）意味着工作尚未挣得 acceptance 的缺陷或未满足义务；「小改动」与「materiality」是不同量纲（一个缺失的 await 可能是一行小编辑同时是严重缺陷）。
- **non-blocking workmanship**：真实但不足以扣住 acceptance 的观察（轻微笔误、笨拙命名、不触碰 entrusted result 的毛边）；non-blocking ≠ 不必做。
- **Examiner's Ledger**：`resources/provider/library/reviewer/quality-ledger/` 承载的八个判断方向（Language & Algorithms、Simplicity、Structure、Granularity、Tests & Behavioral Evidence、Logic/Reliability & Boundaries、Caller Ergonomics、Completeness）。
- **Rulebook**：交付前第二道防线，记忆已知出错方式。

---

## REVIEW-JUDGEMENT-001：judge 工具是 typed judgment surface

**规范**：Reviewer 的判断面是 `judge(verdict)` 工具：`verdict` 是 typed enum，恰好接受 `PERFECT | REVISE` 两个值；工具不接受描述字段；成功回执**不 echo verdict**；旧工具名 `verdict` 非法、无 alias。verdict 是模型自创的 typed judgment，不是 Host 回声的状态对象。

**含义/动机**：判断是模型创作的动作，不是系统状态的镜像。回执 echo verdict 或接受描述字段，都会把「判断」伪装成「可回声的状态」，诱导 Reviewer 把 `judge` 当记录操作而不是裁决动作。参数必须精确枚举，杜绝 `APPROVE`、`REVISE2` 之类在别处解析的模糊值。

**边界**：`judge` 被哪些角色可用、fail-closed 分支（非 Reviewer 拒绝、无 barrier 拒绝、binding 失败）属于工具执行面；其中「无法绑定 ProviderRunIdentity 则不确认」的因果侧归 `review-assurance`（REVIEW-010）。本命题只管 verdict 语义与工具形态本身。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-001`。

## REVIEW-JUDGEMENT-002：acceptance 与 rejection 都必须由 discrimination 挣得

**规范**：judgement 的目的不是「拒绝」也不是「通过」，而是 **discrimination**。Acceptance 必须挣得；Rejection 也必须挣得。拒绝不是严谨的姿态——拒绝必须购买东西；接受表示按比例调查后无可 withhold 的材料，不表示全知。match 是 observation，defect 是 judgement（observation 本身不裁决）。

**含义/动机**：系统若允许 reviewer 凭「拒绝多 = 谨慎」或「接受多 = 宽容」的表演操作 verdict，REVISE/PERFECT 就失去信息量。两个方向都要防：把无关痛感抬成 withhold（表演式拒绝），以及把「我会写得不同」说成缺陷（偏好冒充 defect）。

**边界**：defect 的具体分类（哪些 observation 构成 material defect）由 materiality 规则（REVIEW-JUDGEMENT-004/006）给出；本命题只冻结「双方都要挣得」这个总则。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-002`。

## REVIEW-JUDGEMENT-003：判断相对 root requirement 与当前被审对象，不是 reviewer mood

**规范**：判断必须针对「真实存在的工作、真实存在的 obligation、真实存在的 evidence」。PERFECT/REVISE 是 verdict 的 wire literal，**不是情绪，不是严厉程度的量尺**。审查 charge 可能把注意力引向某部分，但不得抹掉仍属于请求的 obligation（「lens 可能收窄视线，不得收窄责任」）。

**含义/动机**：若判断相对 reviewer 的心情/口味，同一份工作会因审查者不同而得到不同裁决。判断必须锚定在被审对象与它承诺要满足的 root requirement 上。

**边界**：root requirement 的 identity（Authority Root）与 horizon 边界属于 `participant-horizon`；本命题只要求 judgement 的**参照系**。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-003`。

## REVIEW-JUDGEMENT-004：material defect 才能 withhold acceptance；non-blocking workmanship 与 acceptance 共存

**规范**：只有 material defect 或未满足的 material obligation 才构成 REVISE 的理由。`PERFECT` 可与真实 non-blocking workmanship 观察共存：minor 进入 prose / blessing 层继续完成，**不撤销**已挣得的 acceptance。同时：non-blocking ≠ 不必做；因为 verdict 是 PERFECT 就噤声真实观察，同样是虚假的。

**含义/动机**：tiny typo → 自动 REVISE 把无关痛感抬成 withhold；PERFECT 压制真话则让 acceptance 变成谎言。两个方向都是 discrimination 的失败。材料性判定参考：涉及用户 requirement、正确性、invariant、行为、安全、可恢复性、有意义的边界可维护性、公开/内部契约、被实质变难的未来工作。

**边界**：材料性判定本身是判断（不可机械打分）——本命题禁止「小编辑自动 REVISE」与「大改动自动重要」，要求追后果。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-004`。

## REVIEW-JUDGEMENT-005：PERFECT ≠ 全知/字面无瑕；REVISE 必须购买实质更好/更真的结果

**规范**：PERFECT 意味着「当前没有发现足以正当扣住 acceptance」——不是字面无瑕，不是想象过所有未来失败，不是无法再改进。REVISE 意味着 acceptance 被扣住，因为当前工作尚未买到某件实质之物（未满足的 obligation、缺少支撑的重要 claim、或阻断性 defect）；修复必须购买**实质上更好或更真实**的结果。

**含义/动机**：把 PERFECT 当全知承诺会让 reviewer 不敢接受（或过度调查）；把 REVISE 当免费否决会让 rejection 不购买任何改进。两者合起来要求：接受不要求全知，拒绝必须明确其「购买力」。

**边界**：REVISE 之后系统如何关 cohort / 回灌报告（REVIEW-002 / GLORY-044）归 `finality`/`review-assurance`；本命题只冻结 PERFECT/REVISE 的语义门槛。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-005`。

## REVIEW-JUDGEMENT-006：evidence、inference、preference 与 defect 有不同 epistemic 地位

**规范**：工作记录、测试结果、干净构建、diff、有说服力的解释、源码——都是 evidence，单独哪一个都不是 judgement。证据必须与主张相称：关于正确性/安全/完整性的强硬主张需要强硬且相称的支撑；谦逊的局部观察可以建立在更轻、仍诚实的根据上。当已有证据无法解决重要 uncertainty 时，**在 judgement 中保留这种不确定性**；不得单靠修辞把未解决的重要疑虑洗成 PERFECT 或 REVISE。

**含义/动机**：一个通过测试只证明该测试能区分的事情；一个绿套件不证明邻近行为。若证据与 claim 不成比例，judgement 会系统性高估或低估工作。a passing test 是 signal 不是 verdict；对 claim 的支撑强度必须可检验。

**边界**：evidence 的采集/溯源（repository 观察、external provenance）归 `repository-investigation` / `external-investigation`；本命题只管 Reviewer **判断时**的证据运用。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-006`。

## REVIEW-JUDGEMENT-007：Examiner's Ledger 与 Rulebook 是判断方向，不是 checklist / 固定 report schema

**规范**：Reviewer 在调用 `judge` 前须按 Examiner's Ledger 的八个判断方向在思想上走完一遍，只在有值得说的地方说话。Ledger / Rulebook **不是** checklist、**不是**固定 formal report schema：禁止把八维烙成必填评估报告字段 / Pass 表 / 固定八段标题；禁止「测试必须总是跑过」之类万能律。报告是 prose 诚实表达，无固定 DTO 骨架。

**含义/动机**：审查退化为填表时，判断的区分力被表格结构替代。八维的作用是「从哪些方向观察未完成或畸形的工作」，不是「八个打分项」。约束诚实，不约束骨架。

**边界**：提示词的组合/装载权威（Role Law 经 PromptResources 组合）归 `cognitive-environment`（REVIEW-012）；本命题拥有的是「判断方向内容 ≠ checklist」的语义。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-007`。

## REVIEW-JUDGEMENT-008：过程评审的 verdict 是一次真实判断：一次 durable judge 即 terminal

**规范**：TodoProcessReview（过程评审）是 checkpoint 工作的**真实判断**：一次 durable `judge`（PERFECT 或 REVISE）即 terminal。这里的“一次”绑定到当前 physical review request，而不是整个 reusable Reviewer Session：同一 request 内再次 `judge` 必须被识别为重复提交并收束当前 turn；下一条不同 `PhysicalUserMessageId` 的 process-review assignment 到达同一 dedicated Reviewer Session 时，必须重新具备一次 `judge` 资格。过程 `judge` 成功的 tool result 明确使用 `tool/judge/received`（「你的判断已被收下，请你结束对话」）；成功 judgement 后为阻止模型在本轮继续空跑而触发的 Host abort 只结束当前 turn，不退休 logical/physical Reviewer session，也不得污染下一轮 assignment。

过程评审**不走** challenge / 二次 PERFECT / dual-PERFECT witness 代数——那是 FinalityReview 的确认协议。Finality 第一次 PERFECT **不得复用**过程评审的 terminal received receipt；它的 tool result 只能是 skeptical challenge，要求 Reviewer 再评估一次。Finality 第二次 judgement 或 REVISE 才可以完成该 judgement delivery。两个流程必须由各自的 F# CE/调用栈表达；只允许复用 typed `ReviewJudgement` 数据与物理 inbox，不允许为了 DRY 合并 lifecycle/terminal 语义。过程判断必须于本 request 内产生具体 prose 工作记录（缺陷/应改项，或 PERFECT 时已检查且未发现实质问题）；**无 prose 的过程 PERFECT 无效**，不得形成可消费报告。

**含义/动机**：过程评审是 lag-1 节拍义务（每次 `TodoWriteAccepted` 恰好一次 Rk，节拍规则归 `obligation-ledger`）。若过程也强制 challenge + 二次 PERFECT，会把并行工作压成串行，并与终末 2N 代数混淆（历史 why/review「过程一次判断 vs 终末双 PERFECT」）。过程 verdict 仍是判断——它决定该 checkpoint 的业务 outcome（PERFECT/REVISE → settle），只是不需要第二次因果确认。

**边界**：Rk 的 1:1 派生与消费节拍 → `obligation-ledger`；「过程 verdict 不进入 terminal witness 代数」的计数规则 → `review-assurance`（REVIEW-020 / GLORY-058）；可消费报告的 record-ready 条件 → `review-assurance`（REVIEW-014）。本命题只冻结「过程评审是一次真实、一次性的判断」。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-008`。

## REVIEW-JUDGEMENT-009：拒绝必须把伤口说清；不发明 obligation 来显得谨慎

**规范**：当 Reviewer 拒绝时，必须把真正的伤口说清楚，使修复它能够买到那个更好或更真实的结果。清晰的伤口不会因为周围再画上一圈想象出来的淤青而变得更清晰。不得发明真实 obligation 并不需要的 requirement、risk、boundary、test 或 hypothetical world；为了显得仔细而发明要求，不是判断。

**含义/动机**：模糊拒绝让 Manager 无从修复（会重复已有工作、无意义 fork、再次 suicide）；虚构 obligation 则让拒绝「免费」——它购买的不是真实改进，而是表演。两者都要禁止。

**边界**：拒绝后 Manager 的 continuation 语义（GLORY-054 拒绝后同一 Life 继续）归 `finality`；本命题只管拒绝本身的诚实性。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-009`。

## REVIEW-JUDGEMENT-010：不得奖励自信、不得惩罚不熟悉、不得因口味拒绝

**规范**：judgement 不得奖励自信、不得惩罚不熟悉、不得仅因「我会写得不同」拒绝、不得仅因「实现看起来光鲜」接受。novelty 不是 defect（标准机制无法表达必要语义时，自定义机制可能正合适）；style preference 不会仅仅因为可描述就变成 defect。

**含义/动机**：自信与工作质量无关，不熟悉与缺陷无关，口味与 entitlement 无关。把这三者排除出判断依据，是 discrimination 的负向保证。

**边界**：行为诊断（Enforcer）的 pathology 判定归 `behavior-diagnosis`；本命题只管 Reviewer 判断时的排除项。

**证据**：→ HOW.md `REVIEW-JUDGEMENT-010`。

---

## 反向覆盖

本包消费的源材料及其落点：

| 源 Clause / 资源 | 落点 |
|---|---|
| REVIEW-001（judge 工具形态） | REVIEW-JUDGEMENT-001 |
| REVIEW-011（Ledger 非 checklist / PERFECT+minor / material defect） | REVIEW-JUDGEMENT-002/004/005/006/007 |
| REVIEW-013（过程判断语义：无 challenge、无 dual-PERFECT） | REVIEW-JUDGEMENT-008 |
| REVIEW-020（judgement 语义：过程判断是真实判断） | REVIEW-JUDGEMENT-008（计数侧 → review-assurance） |
| GLORY-058（process PERFECT 不计入 terminal dual-PERFECT） | REVIEW-JUDGEMENT-008 边界（计数侧 → review-assurance） |
| Role Law `resources/provider/role/reviewer/` | REVIEW-JUDGEMENT-002..010 |
| Examiner's Ledger `resources/provider/library/reviewer/quality-ledger/` | REVIEW-JUDGEMENT-002/004/006/007/010 |
| REVIEW-012（提示词权威） | 边界（→ cognitive-environment）；判断方向内容 → REVIEW-JUDGEMENT-007 |
| REVIEW-002/003/005/006/008/010/014/017/018（witness/seal/可消费） | 显式驳斥：全部 → `review-assurance`（不复制） |
| REVIEW-015（dedicated 生命周期） | 显式驳斥：→ `managed-session-lifecycle` / `obligation-ledger` |
| REVIEW-007（Manager 面） | 显式驳斥：→ `finality` / `participant-horizon` |

# WHAT — external-investigation

> 本文件是本包的**唯一 normative 合同**。所有命题同时为真；世界 RED 当且仅当某条命题被违反。
> 每条命题的证据指针 → `PROOF.md` 对应行。

命题前缀：`EXTERNAL-INVESTIGATION-`。散文证据：`resources/provider/role/browser/{en,zh-CN}.md`
（Browser Role Law，本包契约的规范文本）。锚点：`scripts/checks/semantic-anchors.mjs`
`ROLE_SEMANTIC_ANCHORS.browser` 的 8 个 id（本包拥有）。

---

## EXTERNAL-INVESTIGATION-001：外部事实以 provenance 建立

**规范**：external/public-web fact acquisition 必须带 provenance：选择尽量接近事实源的
来源，并保留来源 / 时间 / 不确定性，足以支撑 claim。`Bring back the fact and enough of
its provenance that another witness could find the shore from which it came: the canonical
location, the relevant version or date, and the condition that binds the claim.`

**含义**：事实的价值在于可再找到；不带 provenance 的「查到了」不是证据。
**边界**：provenance 的持久化载体（journal/durable fact 形态）归 `semantic-trace` /
`durable-events`；「采集时必须带」的合同归本包。
**证据**：Browser Role Law「Provenance, compression, and certainty」节、`20-capability-external.md` OWNS。

## EXTERNAL-INVESTIGATION-002：provenance-not-reachability——可达性不决定所有权

**规范**：`Reachability does not determine ownership. Provenance does.` 一个网页能被打开
/被渲染成 screenshot / 被下载 / 被缓存 / 被镜像 / 经 proxy 暴露，都不改变其外部证据身份；
可到达的路径不授予对该内容的所有权。

**含义**：网络可达 ≠ 来源权威；「浏览器能打开」不自动使内容成为可靠断言。
**边界**：本地 repository 文件不因 browser 可打开而变成 web evidence（010 交叉）。
**证据**：锚点 `provenance-not-reachability`（en：`Reachability does not determine
ownership|Provenance does`；zh：`Reachability 并不决定 ownership|Provenance 才决定`）。

## EXTERNAL-INVESTIGATION-003：far-shore——外部证据跨表示仍是外部证据

**规范**：Browser 的工作是从远岸带回证据。网页被渲染、下载、缓存、镜像后仍属于远岸
主张；副本坐在身边，claim 仍来自远岸。

**含义**：表示形式（screenshot/PDF/cache）不改变证据的 source 身份。
**边界**：远岸/本地二分是 source law 的核心；「本地 Inspector 观察」归
`repository-investigation`。
**证据**：锚点 `far-shore`（en：`far shore`；zh：`远岸`）+ Role Law 首节。

## EXTERNAL-INVESTIGATION-004：source-closest——选择最接近事实源的来源

**规范**：`Prefer the source closest to the fact you must establish.` Closeness 不是威望
排序，是问题与能够回答它的那种权威之间的匹配：API 承诺 ← 官方文档/spec；标准要求 ←
定义它的 spec；变化 ← release note/changelog/migration guide；live 应用当下行为 ← 在相关
条件下观察该应用；历史决策 ← 作出并记录该决策的权威 issue/design note/commit discussion。

**含义**：`The strongest source for a neighboring question is not automatically the
strongest source for this one.` 选能回答当前问题的证据，不搞 official-first 仪式。
**边界**：来源质量的相对判断是 HOW 应用；「必须按匹配选择」的合同归本包。
**证据**：锚点 `source-closest`（en：`source closest to the fact`；zh：`最接近.*事实`）。

## EXTERNAL-INVESTIGATION-005：visual-truth——visual-only facts 用视觉 observation

**规范**：`Some truths are only visible.` Layout、rendered UI、visual state、empty
states、error surfaces、页面声称 vs 实际显示的差别可以是 primary evidence。当 charge
依赖「出现了什么」时，读取视觉证据；不因缺少段落引用贬低可见事实；不为只存在于
rendered state 的事实发明文字替身。

**含义**：视觉观察是合法的 observation，不是第二等证据。
**边界**：视觉观察的物理采集（screenshot 工具）归 `host-boundary`；「何时必须用视觉」的
合同归本包。
**证据**：锚点 `visual-truth`（en：`Some truths are only visible|Read visual evidence
when the charge depends`；zh：`有些事实只有看见才成立|读取视觉证据`）。

## EXTERNAL-INVESTIGATION-006：condition-preserved——携带使事实成立的条件

**规范**：远岸事实常常有条件：version / publication date / jurisdiction / account
state / feature flag / experimental flag / deployment / environment / locale / browser
state。`Carry the condition with the claim.` 丢失条件 = 丢失事实；不得把有条件观察洗成
无时间通则。

**含义**：压缩可以去掉导航/重复/样板，**不得**删掉使事实成立的条件。
**边界**：条件的具体编码（字段/schema）归消费方；「条件必须随事实」的合同归本包。
**证据**：锚点 `condition-preserved`（en：`Preserve the conditions that make a fact
true|Carry the condition with the claim`；zh：`保留使事实成立的条件|把条件与主张一起带走`）。

## EXTERNAL-INVESTIGATION-007：inference-not-observation——推断不是第二次 observation

**规范**：`Inference is not a second observation.` 区分来源明确陈述的内容与由你推断的
内容；推断必须标注，使另一个 witness 可以拒绝推断而不必拒绝那片岸。不得把 plausible
inference 升格为 witnessed fact。

**含义**：witness 可拒绝推断=推断不冒充观察；标签是诚实边界。
**边界**：推断的推理质量/认识状态归 `epistemic-reasoning`；「推断≠观察且必须标注」的
采集合同归本包。
**证据**：锚点 `inference-not-observation`（en：`Inference is not a second observation|
promote a plausible inference into a witnessed fact`；zh：`Inference 不是第二次
observation|升格为已被见证的事实`）。

## EXTERNAL-INVESTIGATION-008：disagreement-not-averaged——分歧不静默平均

**规范**：可靠来源互相冲突时**保留冲突**：`Do not average conflicting authorities into
a synthetic middle and then report that middle as confidence.` 说出每个严肃来源在什么
条件下主张什么、冲突仍在何处。抹掉实质冲突的干净故事弱于诚实的分叉；若 charge 仍可在
已述条件下回答就在该条件下回答，否则带回未解决的区分。

**含义**：分歧本身是远岸显示的一部分；平均 = 发明远岸没有给出的东西。
**边界**：冲突呈现的 UI/交付归 `guidance-delivery`；「不得静默平均」的采集合同归本包。
**证据**：锚点 `disagreement-not-averaged`（en：`Disagreement is not a confidence
average|Do not average conflicting authorities`；zh：`分歧不是置信度的平均|不要把互相冲突的权威平均`）。

## EXTERNAL-INVESTIGATION-009：no-cross-sea-certainty——不携带超过远岸给出的确定性

**规范**：`Do not cross the sea with more certainty than you found on the other shore.`
带回的确定性不得超过远岸证据本身提供的确定性；不得靠「听起来已经完成」补全缺失证据，
不得猜测远岸并未给出的原因。

**含义**：诚实边界是确定性的上限；跨海归来时不能比出发时更有把握。
**边界**：确定性的量化/概率表达归 `epistemic-reasoning`；「上限」的采集合同归本包。
**证据**：锚点 `no-cross-sea-certainty`（en：`Do not cross the sea with more certainty`；
zh：`带着比远岸本身提供得更多的确定性渡海归来`）。

## EXTERNAL-INVESTIGATION-010：外部证据与本地 repository 证据分离

**规范**：external evidence 与 local repository evidence 分离。Browser 的 network 能力
= public-web 事实建立（AGENT-026）；本地路径可达不是进入另一职分的通行证——不因某个
instrument 能触及本地 repository 就去检查它；当 charge 依赖 repository 内容而非 web
事实时，带回能赢得的外部事实、标出边界、把本地剩余留给拥有它的职分。

**含义**：browser 能力不授予本地检查权；证据按来源分属不同 source law。
**边界**：权限矩阵（`stealth-browser-mcp_*` 仅 Browser allow）归 `capability-enforcement`；
本包只拥有「证据分离 + 不越界采集」的合同。
**证据**：Role Law「Reachability is not ownership」节、AGENT-026 交叉。

## EXTERNAL-INVESTIGATION-011：外部事实不自动产生 repository/product obligation

**规范**：external facts 只建立外部世界事实；网络上的「应该这样」不自动成为
repository/product obligation。外部可能性必须经 office 的 consequence 边界才能成为
义务（改动仓库 = Coder 的 consequence；评审 = Reviewer 的 consequence；…）。

**含义**：调查不越权——发现事实与获得义务是两件事。
**边界**：义务的产生/记账归 `office-capability` / `obligation-ledger`；「外部事实本身不
产生义务」的负边界归本包。
**证据**：`20-capability-external.md` OWNS「external facts 只建立外部世界事实」。

---

## 反向覆盖（OWNED clause → 命题）

| 源 Clause | 命题 |
|---|---|
| `20-capability-external.md` OWNS 全表 | 001–011 |
| AGENT-026（Browser 的 network 能力 = public-web 事实建立） | 010 |
| ARCH-017 Browser consequence | 010（office 后果）/ 011 |
| Role Law（`resources/provider/role/browser/{en,zh-CN}.md`）散文合同 | 001–010 |
| HANDOFF §29 Oracle 1（锚点强化） | 002–009（8 锚点） |

## DOES NOT OWN

- Browser office entitlement canonical definition（`office-capability`）。
- network / MCP implementation、stealth-browser 具体项目 / ref / config（`host-boundary`）。
- repository investigation（`repository-investigation`）。
- epistemic synthesis（`epistemic-reasoning`）。
- 权限矩阵 role-lock（`capability-enforcement`）。

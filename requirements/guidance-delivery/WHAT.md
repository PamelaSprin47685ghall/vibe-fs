# guidance-delivery — WHAT（唯一 normative 合同）

> 命题 = 当前世界必须同时成立的事实。编号 `GUIDANCE-DELIVERY-NNN`（下文简称
> `GD-NNN`）。每条末尾的证据指针 → `PROOF.md` 行号。
> 边界：diagnosis 是否成立归 `behavior-diagnosis`；provider projection mechanics
> 归 `provider-projection`；horizon admission general law 归 `participant-horizon`；
> interaction authority 创建/继续权归 `interaction-authority`。

## A. 两轴分离

### GD-001 交付前沿 ≠ 语义覆盖，不得压成单一 durable bool

Main tip 交付有两个正交轴：

```text
TipDeliveryFrontier    哪些 TipOccurrence 已交付给该 Main
                       durable、monotonic、occurrence-based
                       ContextReanchored 不重置

TipSemanticCoverage    哪些 TipName 的 full main.md 语义此刻仍可从当前 provider
                       horizon 恢复
                       TipName-based、horizon-relative
                       ContextReanchored 可重置 / 重导
```

- 含义：诊断 occurrence 的交付历史与「全文此刻是否还在 horizon」是两回事
  （ENFORCER-071）；把二者压成一个 durable bool 必然在 reanchor 后误删已交付
  事实或假装全文仍在。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_002/006`；
  `tip-delivery-projection.test.mjs` `TDP_001/002/004/005`；`PROOF.md` 行 10。

### GD-002 首次交付 = Full main.md

`TipOccurrence ∉ Frontier` ∨ `TipSemanticCoverage` 表明该 TipName 全文不可恢复 →
`TipPresentation.Full`：`# Enforcer Tip` + `tip = "<name>"` + `main.md` 全文（按
owner 语言取叶子）。Full 且 occurrence ∉ Frontier → append
`HostFact.TipGuidanceDelivered { Full }`（推进 Frontier）。

- 含义：Main 第一次不只看到名字，它完整看到「问题意味着什么/现在做什么/为什么/
  不要做什么/如何验证/何时算完成」（Rulebook §14）。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_001`；
  `PROOF.md` 行 11。

### GD-003 重复交付 = IdentityOnly，不重复全文

`TipOccurrence ∈ Frontier` ∧ `TipSemanticCoverage` 仍可恢复全文 →
`TipPresentation.IdentityOnly`：紧凑 `tip: <name>`。不重复 `main.md` 全文、
不推进 Frontier、不得把 Identity 写成「全文永久可恢复」durable bool。

- 含义：dedupe 的落点——第一次教完整处置协议，后续用稳定身份唤醒已有语义，
  避免无界全文膨胀（Rulebook §15）。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_002`；
  `tip-delivery-projection.test.mjs` `TDP_002/003`；`PROOF.md` 行 13。

### GD-004 交付决策只 fold durable facts，restart-safe

Full/Identity 判定唯一 substrate = `TipDeliveryProjection`（fold
`TipGuidanceDelivered` 等 durable facts），按 Main session 隔离；禁止
process-local「已发送」集合、`delivered-tips.json`、文件 tip ledger 或内存猜测。
restart / recovery / crash / retry 后判定不漂移。

- 含义：第一次/重复判定在重启后仍正确（Rulebook §16/A46）；内存集合会忘记或分叉。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_003`
  （latest 与 resolve 一致）；`tip-delivery-projection.test.mjs` `TDP_001/002`；
  `PROOF.md` 行 14。

### GD-005 reanchor/compaction：语义恢复 ≠ 新 occurrence

`ContextReanchored`（HOST-006）清空 TipSemanticCoverage（≠ Frontier）。Coverage
不可恢复后再次给出 full main.md = **semantic restoration**，不是新 TipOccurrence、
不推进 TipDeliveryFrontier。禁止用过期 IdentityOnly 搁浅 post-reanchor transcript。

- 含义：压缩改变 horizon 形状，但不重写已发生的交付历史；Main 不会因 compaction
  丢失处置手册，也不会把恢复误记成又一次世界教训。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_006`；
  `tip-delivery-projection.test.mjs` `TDP_004/005`；`PROOF.md` 行 15。

## B. 决策路径

### GD-006 owner 解析与 None 语义

入参可以是 Main session id 或 Blogger satellite id；经 `SessionAssociation` 解析到
owner Main session 再取最近已提交 tip。无 tip / 无 association / 目录查无规则 →
`None`，不发明 guidance。

- 含义：交付永远挂在 owner Main 上；解析失败就安静地不给，不编造。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_004/005`；
  `latest-tip-nudge.test.mjs` `ENFORCER_TIP_NUDGE_002/003`；`PROOF.md` 行 16。

### GD-007 `latestTipGuidance` / `latestTipNudge` 同义

`latestTipGuidance` = resolve 的 Text；`latestTipNudge` 是同义别名（Full/Identity
文本，不是旧 Nudge 字段）。两者返回同一字节。

- 含义：旧命名不复活旧语义；对外只有一条交付文本路径。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_003`；
  `latest-tip-nudge.test.mjs` `ENFORCER_TIP_NUDGE_001`；`PROOF.md` 行 17。

## C. audience 分离

### GD-008 detection 与 remediation 面向不同 audience，不泄漏职责

`enforcer.md`（检测边界）只进 Blogger effective system；`main.md`（处置手册）只进
Main Full/Identity 交付；`previous_enforcer_tip` 是低信任 Blogger 历史
（`[[do_not_exec]]`、role=assistant），不得进 Main Authority。共享 TipName 身份，
但两边 renderer 不得互用。

- 含义：诊断语料与补救语料同源不同权（Rulebook §20/§27/§28）；Main 指导泄漏进
  Blogger system 或检测散文冒充 Main 指令都是违规。
- 证据：`audience-separation.test.mjs` `AUDIENCE_001/002/003`；REUSE
  `tests/unit/enforcer/tip-v2-contract.test.mjs` `ENFORCER_TIP_13`
  （work record 含 previous_enforcer_tip 块）；`PROOF.md` 行 18。

### GD-009 交付不创建 interaction authority

Main tip guidance 只经 `TipGuidanceDelivered` 投影 + auto-injected tool-call/
tool-result pair 进入 horizon，不注入工程 fake-user message、不 mint 新 Authority
Root；delivery 不改变 authority/personhood。

- 含义：guidance 是 Host-adopted 提示，不是第二 Authority 解释器
  （ENFORCER-071 / 边界 card）。
- 证据：`tip-guidance-delivery.test.mjs` `ENFORCER_TIP_DELIVERY_001`（交付形状 =
  tip header + main.md）；`latest-tip-nudge.test.mjs` `CTX_002_GUIDELINE_001/002`
  （auto-injected marker 机制）；`audience-separation.test.mjs` `AUDIENCE_003`；
  `PROOF.md` 行 19。

### GD-010 检测语料跨 family 冲突门（A40 机械替代）

`scripts/checks/enforcer-cross-family-collision.mjs` 解析每篇 enforcer.md 的
Trigger When + Definition，对非 sibling 的近义词法重叠 fail closed（trigger
Jaccard ≥ 0.90 或 Levenshtein ≥ 0.95）；warn/note 级证据照常输出保证 A40 被记录
而非跳过。

- 含义：detection 语料的可区分性是 delivery 质量的前置——选择一条 tip 时不能被
  另一条的同词触发条件污染（Rulebook A40；PROOF-MAP Phase D 归属本包）。
- 边界：门是词法机械替代，**不冒充**人类 tournament（`archive/changes/completed/rulebook.md`
  Final outcome 诚实声明）。
- 证据：REUSE `tests/unit/verify/enforcer-cross-family-collision.test.mjs`
  `enforcer_collision_*`；`PROOF.md` 行 20。

## D. 历史字节

### GD-011 已投递 auto-injected 字节按原文冻结

每个 auto-injected pair 以 `PairProgrammingGuideline { Ordinal; CallId;
MarkerText; CallGap; ResultGap }` 持久化（HOST-013）：`MarkerText` = provider
当时实际看到的精确正文；replay 必须 byte-identical 恢复原文，不随 authored
`main.md` 版本演进改写。fold 拒绝：ordinal 乱序、重复 CallId、重复 placement
（SessionId + CallGap + ResultGap 至多一对）。

- 含义：历史交付是 EventStore 事实不是文件旁路；restart 后 Main 看到的就是当时
  收到的那一版（Rulebook §17）。
- 证据：`guideline-projection.test.mjs` `GP_001..006`；`latest-tip-nudge.test.mjs`
  `CTX_002_GUIDELINE_001/002`（marker 正文透传）；`PROOF.md` 行 20。

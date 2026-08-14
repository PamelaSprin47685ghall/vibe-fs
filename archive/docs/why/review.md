# Review — 理由

单次 PERFECT 可被模型随口同意。双 PERFECT + seal 证明第二次输入里真的含有 skeptical challenge，把确认从口头变成因果消费证据。Provider 表面动词是 `judge`：模型创作判断，不是 Host 回声状态；成功回执不 echo verdict。

判断哲学是 discrimination，不是 rejection 表演。Acceptance 必须挣得；Rejection 也必须挣得——拒只为显示谨慎而造伤，也拒把可描述偏好伪装成缺陷。Rejection 须购买实质更好或更真的结果；Acceptance 表示按比例调查后无可 withhold 的材料，不等于全知或字面无瑕。

Examiner's Ledger 给八个判断方向（含 Caller Ergonomics），Rulebook 作交付前第二道防线。二者指导如何判断，不是 checklist、不是固定报告 schema——拒把八维压成必填评估报告字段，拒 tiny typo → 自动 REVISE，拒「测试必须总是跑过」的万能律。PERFECT 可与真实 non-blocking workmanship 共存：minor 进 prose / blessing 层继续完成，不撤销已挣得的 acceptance。

Witness 必须自包含：Guard 若依赖外围 Map，恢复与并发 Job 会静默读到别人的确认或空确认。tree 变化作废 witness，因为审的是代码状态，不是 Session 情绪。

Seal 绑定失败 fail closed，禁止 same-root 猜测：猜测在 Host 重排消息时会假绿。

Finality 三种经验必须分型：rejection（未接受 + 反失败主义 + 继续）、blessed（已接受但未安息 + 已知 minor 不撤销 acceptance）、rest（安息 + 禁止再开工）。Acceptance ≠ rest；non-blocking 不挡 acceptance，也不等于不必做。

## Magic Todo：过程评审与终末评审分型

**过程一次判断 vs 终末双 PERFECT。** 过程评审（`TodoProcessReview`）是 lag-1 节拍义务：每次 `TodoWriteAccepted` 恰好派生一次 Rk，Manager 在 Rk 期间可并行工作，只在 T(k+1) / suicide drain 同步（TODO-006/010）。若过程也强制 challenge + 二次 PERFECT，会把并行工作压成串行，并与终末 2N 代数混淆。终末评审（`FinalityReview`）才需要 REVIEW-003 因果双 PERFECT；过程 PERFECT 永不计入 terminal witness（REVIEW-013/020，GLORY-058）。

**VerdictKnown 与 ConsumableReview 分型。** 业务 outcome 在 Reviewer 域 durable 判断落盘时立即决定（PERFECT/REVISE → settle，TODO-005/006）；但下一 checkpoint / suicide 消费的是带 `WorkRecordRef` 的正式报告。若把「只有判断、尚无 report」挤进同一个 `TodoReviewConcluded`，恢复路径无法区分「已可 settle」与「已可展示报告」，并会诱导提前 append 空壳 Concluded。正式分型：`VerdictKnown` 复用既有 Reviewer 域事实；`ConsumableReview ≡ TodoReviewConcluded` 仅在同 snapshot record-ready 后 append（REVIEW-014，GLORY-072/073，TODO-006/012）。报告是 prose 诚实表达，不是固定 DTO 骨架。

**为何禁止 wall-clock polling。** sleep/timer/re-probe 把 Journal 因果等待退化成运气；本地 waiter 崩溃后无法从 durable facts 重建同一等待。过程报告与 Finality 拒绝共用同一事件驱动模式：`await AgentJournal change` → 同 snapshot 判 record-ready 并物化（REVIEW-017，GLORY-073，TODO-012）。

**为何基础设施失败不是 REVISE。** create/resume/assignment/LWR 物化失败是 Host 缺陷，不是工作过程缺陷。伪装成 REVISE 会触发错误 semantic merge、推进虚假 ConsumableReview，并让 Manager 去「修复」系统故障。正式语义：义务保持 outstanding，可恢复则 event-driven ensure，不可证明则 typed infrastructure failure 且 Finality/下一 TodoWrite 不得越过（REVIEW-018，TODO-012）。

**为何 Dedicated 每 Life 一个且隐藏。** Manager 可见 reviewer 会把质量门重新变成 checklist（GLORY-002）。每 Life 一个 logical dedicated reviewer 保证过程历史连续；physical session 仅在 proven permanent loss 后替换，避免「偶发超时就换人」丢失上下文（REVIEW-015/019，TODO-008/013）。Finality graduate 只解除 cohort membership，不解除 process-review duty（TODO-010）。Manager 固定 surface 的窄可见例外（过程 PERFECT/REVISE/report）只在 TODO-013 / GLORY-030，不得扩大为泄漏 hidden session/barrier/2N。Reviewer system 不知道 dual-PERFECT / barrier / cohort。

**为何 LWR 必须 request-range bounded。** Dedicated session 跨多个 Rk 复用后，若取 session head LWR，R4 report 会吞入 R1–R3。三个用途共用同一 renderer、不同冻结 frontier：Manager checkpoint 输入、Process report、Finality record（REVIEW-016，TODO-008，GLORY-004/050）。禁止第二套工作记录投影（TODO-012）。

**为何 process 允许 RawGap、prefix 不允许。** 过程审查要及时看到刚完成阶段，合法 canonical LWR（Y + RawGap）已是完整证据；Manager lag-1 rebase 只替换 PrefixCoverage 已证明的 Y prefix（TODO-008/009）。二者共享 source、分型 coverage，禁止互转。

## 备选与被拒

**确认强度：单 PERFECT vs 双 PERFECT + seal。** 拒单 PERFECT：可被模型随口同意。挑 challenge + seal 证明第二次输入真含 skeptical challenge，把确认变成因果消费证据（REVIEW-003）。

**工具名：`verdict` 名词 vs `judge` 动词。** 拒名词工具：把判断伪装成可回声状态对象。选 `judge(verdict=...)`：enum 合法因属模型自创判断；回执不 echo。

**判断目标：表演式拒绝 vs 双方挣得。** 拒「谨慎=多 REVISE」与「可描述偏好即缺陷」。Acceptance/Rejection 皆须挣得；match 是 observation，defect 是 judgment。

**Office：固定 8 维报告 schema vs Examiner's Ledger + Rulebook。** 拒把八维烙成必填报告字段/Pass 表：审查退化为填表。选 Ledger 作 Binding 判断指南 + Rulebook 第二防线；Closing/过程报告遵守「约束诚实，不约束骨架」。

**瑕疵：tiny typo → 自动 REVISE vs PERFECT+minor 共存。** 拒自动 REVISE：把无关痛感抬成 withhold。选 material defect 才拒；PERFECT 可带 non-blocking workmanship，minor 进 blessed 层。

**Finality：单一结束文案 vs rejection / blessed / rest。** 拒混成一种「结束」：会把未接受当安息，或把已接受当禁止收尾。三种经验分型；Acceptance ≠ rest。

**Witness 载体：自包含 vs 外围 Map。** 拒外围 Map：恢复/并发 Job 会静默读到别人的确认或空确认（REVIEW-006）。witness 自带全部证据。

**作废：tree 变化作废 vs 旧确认坚持。** 拒旧确认：审的是代码状态不是 Session 情绪；tree 变即 witness 失效，保证结论绑定被审对象（REVIEW-006）。

**绑定：唯一绑定 + fail closed vs same-root 猜测。** 拒猜测：Host 重排消息时假绿。沿用 HOST-010 因果读，命中 0/≥2 即放弃写 seal，宁可无 seal 不赌。

**过程/终末：同一 controller 用 pendingChallenge 猜测 vs typed RequestKind。** 拒猜测：会把过程一次判断误走 challenge，或把终末链误成单次终端（REVIEW-013）。

**可消费性：VerdictKnown 即放行 vs ConsumableReview 要 record-ready LWR。** 拒仅判断：Manager 拿不到 canonical 报告，或 Host 用 terminal 摘要顶替 LWR（REVIEW-014，TODO-006）。

**等待：timer/polling vs Journal change + 同 snapshot。** 拒 polling（REVIEW-017，GLORY-073，TODO-012）。

**失败：infra → 伪 REVISE vs typed infrastructure failure。** 拒伪 REVISE（REVIEW-018，TODO-012）。

**替换：超时即换 session vs 仅 proven permanent loss。** 拒超时即换（REVIEW-019，TODO-008）。

**过程 PERFECT 计入 terminal dual-PERFECT。** 拒：process ≠ terminal；enlist 后仍要 fresh barrier/chain（REVIEW-020，TODO-010，GLORY-058）。

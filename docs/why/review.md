# Review — 理由

单次 PERFECT 可被模型随口同意。双 PERFECT + seal 证明第二次输入里真的含有 skeptical challenge，把确认从口头变成因果消费证据。

明确 8 大代码质量支柱（包含 Caller Ergonomics / 用户体验）防止审查退化为泛泛而谈或仅看测试是否通过。通过强约束 8 维评估报告，逼迫模型穷尽审视算法、简洁度、结构、粒度、测试覆盖、逻辑无瑕、调用方体验与完整性。

Witness 必须自包含：Guard 若依赖外围 Map，恢复与并发 Job 会静默读到别人的确认或空确认。tree 变化作废 witness，因为审的是代码状态，不是 Session 情绪。

Seal 绑定失败 fail closed，禁止 same-root 猜测：猜测在 Host 重排消息时会假绿。

## Magic Todo：过程评审与终末评审分型

**过程一次 verdict vs 终末双 PERFECT。** 过程评审（`TodoProcessReview`）是 lag-1 节拍义务：每次 `TodoWriteAccepted` 恰好派生一次 Rk，Manager 在 Rk 期间可并行工作，只在 T(k+1) / suicide drain 同步（TODO-006/010）。若过程也强制 challenge + 二次 PERFECT，会把并行工作压成串行，并与终末 2N 代数混淆。终末评审（`FinalityReview`）才需要 REVIEW-003 因果双 PERFECT；过程 PERFECT 永不计入 terminal witness（REVIEW-013/020，GLORY-058）。

**VerdictKnown 与 ConsumableReview 分型。** 业务 outcome 在 Reviewer 域 durable verdict 落盘时立即决定（PERFECT/REVISE → settle，TODO-005/006）；但下一 checkpoint / suicide 消费的是带 `WorkRecordRef` 的正式报告。若把「只有 verdict、尚无 report」挤进同一个 `TodoReviewConcluded`，恢复路径无法区分「已可 settle」与「已可展示报告」，并会诱导提前 append 空壳 Concluded。正式分型：`VerdictKnown` 复用既有 Reviewer 域事实；`ConsumableReview ≡ TodoReviewConcluded` 仅在同 snapshot record-ready 后 append（REVIEW-014，GLORY-072/073，TODO-006/012）。

**为何禁止 wall-clock polling。** sleep/timer/re-probe 把 Journal 因果等待退化成运气；本地 waiter 崩溃后无法从 durable facts 重建同一等待。过程报告与 Finality 拒绝共用同一事件驱动模式：`await AgentJournal change` → 同 snapshot 判 record-ready 并物化（REVIEW-017，GLORY-073，TODO-012）。

**为何基础设施失败不是 REVISE。** create/resume/assignment/LWR 物化失败是 Host 缺陷，不是工作过程缺陷。伪装成 REVISE 会触发错误 semantic merge、推进虚假 ConsumableReview，并让 Manager 去「修复」系统故障。正式语义：义务保持 outstanding，可恢复则 event-driven ensure，不可证明则 typed infrastructure failure 且 Finality/下一 TodoWrite 不得越过（REVIEW-018，TODO-012）。

**为何 Dedicated 每 Life 一个且隐藏。** Manager 可见 reviewer 会把质量门重新变成 checklist（GLORY-002）。每 Life 一个 logical dedicated reviewer 保证过程历史连续；physical session 仅在 proven permanent loss 后替换，避免「偶发超时就换人」丢失上下文（REVIEW-015/019，TODO-008/013）。Finality graduate 只解除 cohort membership，不解除 process-review duty（TODO-010）。Manager 固定 surface 的窄可见例外（过程 PERFECT/REVISE/report）只在 TODO-013 / GLORY-030，不得扩大为泄漏 hidden session/barrier/2N。

**为何 LWR 必须 request-range bounded。** Dedicated session 跨多个 Rk 复用后，若取 session head LWR，R4 report 会吞入 R1–R3。三个用途共用同一 renderer、不同冻结 frontier：Manager checkpoint 输入、Process report、Finality record（REVIEW-016，TODO-008，GLORY-004/050）。禁止第二套工作记录投影（TODO-012）。

**为何 process 允许 RawGap、prefix 不允许。** 过程审查要及时看到刚完成阶段，合法 canonical LWR（Y + RawGap）已是完整证据；Manager lag-1 rebase 只替换 PrefixCoverage 已证明的 Y prefix（TODO-008/009）。二者共享 source、分型 coverage，禁止互转。

## 备选与被拒

**确认强度：单 PERFECT vs 双 PERFECT + seal。** 拒单 PERFECT：可被模型随口同意。挑 challenge + seal 证明第二次输入真含 skeptical challenge，把确认变成因果消费证据（REVIEW-003）。

**Witness 载体：自包含 vs 外围 Map。** 拒外围 Map：恢复/并发 Job 会静默读到别人的确认或空确认（REVIEW-006）。witness 自带全部证据。

**作废：tree 变化作废 vs 旧确认坚持。** 拒旧确认：审的是代码状态不是 Session 情绪；tree 变即 witness 失效，保证结论绑定被审对象（REVIEW-006）。

**绑定：唯一绑定 + fail closed vs same-root 猜测。** 拒猜测：Host 重排消息时假绿。沿用 HOST-010 因果读，命中 0/≥2 即放弃写 seal，宁可无 seal 不赌。

**过程/终末：同一 controller 用 pendingChallenge 猜测 vs typed RequestKind。** 拒猜测：会把过程一次 verdict 误走 challenge，或把终末链误成单次终端（REVIEW-013）。

**可消费性：VerdictKnown 即放行 vs ConsumableReview 要 record-ready LWR。** 拒仅 verdict：Manager 拿不到 canonical 报告，或 Host 用 terminal 摘要顶替 LWR（REVIEW-014，TODO-006）。

**等待：timer/polling vs Journal change + 同 snapshot。** 拒 polling（REVIEW-017，GLORY-073，TODO-012）。

**失败：infra → 伪 REVISE vs typed infrastructure failure。** 拒伪 REVISE（REVIEW-018，TODO-012）。

**替换：超时即换 session vs 仅 proven permanent loss。** 拒超时即换（REVIEW-019，TODO-008）。

**过程 PERFECT 计入 terminal dual-PERFECT。** 拒：process ≠ terminal；enlist 后仍要 fresh barrier/chain（REVIEW-020，TODO-010，GLORY-058）。

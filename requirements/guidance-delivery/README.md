# guidance-delivery

## 一句话 WHY

diagnosis 成立不等于必须立刻重复告知全文；「是否再次进入当前 horizon、给全文还是
给身份」是独立于 diagnosis truth 的 occurrence / coverage / dedupe / horizon-relative
delivery 问题。

## WHAT 概览（唯一 normative 合同见 `WHAT.md`）

- **两轴分离**：`TipDeliveryFrontier`（occurrence 单调、durable、reanchor 不重置）
  ⊥ `TipSemanticCoverage`（TipName、horizon-relative、reanchor 可清空）；不得压成
  单一 durable bool。
- **Full / Identity**：首次（∉ Frontier ∨ Coverage 不可恢复）给 `main.md` 全文；
  重复（∈ Frontier ∧ Coverage 可恢复）只给 `tip: <name>`，不重复烧上下文。
- **restart-safe 决策**：只 fold `TipGuidanceDelivered`（`TipDeliveryProjection`），
  禁 process-local set / delivered-tips.json / 文件 ledger。
- **reanchor 语义恢复**：compaction 丢 coverage 后重新给全文 = semantic
  restoration，**不是**新 occurrence、不推进 Frontier。
- **audience 分离**：detection 材料（enforcer.md）只进 Blogger system，remediation
  材料（main.md）只进 Main 交付；`previous_enforcer_tip` 是低信任历史，不进 Main
  Authority；共享 TipName 身份，不互相泄漏职责。
- **交付不造 authority**：Main tip 只经投影 + auto-injected marker 进 horizon，
  不注入 fake-user message、不 mint Authority Root。
- **历史字节冻结**：已投递的 auto-injected MarkerText 按当时实际字节持久化，
  replay byte-identical（HOST-013 `GuidelineProjection`）。

**不归本包**：diagnosis 是否成立（`behavior-diagnosis`）；provider projection
mechanics（`provider-projection`）；horizon admission general law
（`participant-horizon`）；`main.md/enforcer.md` 物理布局；interaction authority
的创建/继续权（`interaction-authority`）。

## HOW 概览（实现模型见 `HOW.md`）

| 层 | 位置 | 职责 |
|---|---|---|
| 交付决策 | `src/Wanxiangshu/Enforcer/Guidance/Tip.fs` | `resolveTipGuidance`：读 Frontier+Coverage → Full/Identity；只 fold durable facts |
| 投影 | `src/Wanxiangshu/Enforcer/Guidance/DeliveryProjection.fs` + `src/Wanxiangshu/OpenCode/Host/PairProgramming/GuidelineProjection.fs` | Full 历史（reanchor 可清）、auto-injected pair 历史（byte 冻结） |
| marker 注入 | `src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs` | `tryInject`：auto-injected tool pair |

## Proof 概览（落点表见 `PROOF.md`）

- 本包自有测试：`requirements/guidance-delivery/tests/`（7 文件，全部 `node --test`
  单跑绿）。
- 交叉证明（REUSE）：`tests/unit/enforcer/{tip-v2-contract,enforcer-cycle-protocol,
  blogger-convergence-gaps}.test.mjs` 中的 delivery 锚点。
- 本包无 semantic-anchors.mjs anchor id。

## 阅读顺序（保姆级导航）

1. `WHY.md` —— 为什么 delivery 必须与 diagnosis 分离；历史上 RED 是什么样。
2. `WHAT.md` —— 编号命题（唯一 normative 合同）。
3. `HOW.md` —— `src/` 精确实现模型 + 失败模式 + 历史与弃权。
4. `PROOF.md` —— 每条命题 → 测试锚点 + 运行命令。
5. `tests/` —— 可执行 proof 本体。

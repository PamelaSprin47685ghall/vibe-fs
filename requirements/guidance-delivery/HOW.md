# guidance-delivery — HOW（实现模型与约束）

> 非 normative。WHAT 命题的落点见 `PROOF.md`；本文件解释 `src/` 里每个概念
> 的精确位置、约束与失败模式，末尾是「历史与弃权」。

## 1. 交付决策：`src/Wanxiangshu/Session/EnforcerTipGuidance.fs`

```fsharp
type TipGuidance =
    { TipName: string
      Presentation: TipPresentation        // Full | IdentityOnly
      Text: string }                        // Full = header + main.md；Identity = "tip: <name>"
```

- `resolveTipGuidance journal mainOrBloggerSession`（GD-002/003/004/006）：
  1. `tryOwnerMainSession`：Blogger satellite id 经 `SessionAssociationProjection`
     解析到 owner Main；无 association → None（不发明 guidance）。
  2. `latestOwnerTipField`：取该 Main 最近已提交 RecentTip 的 FieldName。
  3. 目录查规则（`EnforcerCatalog.tryFindByField`，`RuntimeResources`），无 → None。
  4. `hasFullTipDelivered` 读 `TipDeliveryProjection`：
     - 未 Full → `TipPresentation.Full`，文本 = 语言化 header + `rule.MainText`；
       异步 append `HostFact.TipGuidanceDelivered { Full }`（推进 Frontier）。
     - 已 Full → `IdentityOnly`，文本 = `tip: <name>`；不推进 Frontier、不写
       durable bool。
- `recordFullTipDelivered`：append 失败只发 `tip-guidance-delivery-append-failed`
  diagnostic（交付仍以 Full 文本继续），后续 Identity 不会静默搁浅。
- `latestTipGuidance` / `latestTipNudge`（GD-007）：同义别名，返回 resolve 的 Text。

> 注意（诚实性）：当前实现把「Coverage 可恢复」近似为「该 Main session 的
> TipDeliveryProjection 未经历 reanchor 清空」——`TipSemanticCoverage` 是
> `TipDeliveryProjection.applyReanchor` 的语义（HOST-006 `ContextReanchored` 清空
> Full 历史），没有独立的字节级 horizon 探测。两轴在投影层分离（GD-001），
> 机器可证明的部分见 §2 与 PROOF.md。

## 2. 投影：`src/Wanxiangshu/Journal/TipDeliveryProjection.fs`

```fsharp
type TipDeliveryProjectionState = { FullDeliveredTips: Set<string> }
```

- `apply tipName presentation state`：Full → 加入集合；IdentityOnly → audit-only
  （不加入，防止把重复身份误记为「全文永久可恢复」）。
- `applyReanchor state` → `empty`：HOST-006 重锚清空 Full 历史（Coverage 丢失），
  但这是**同轴内**的清空；occurrence 维度的单调性由「Full 事实被追加」表达，
  reanchor 本身不追加新 Full 事实 → 重发全文 = restoration，不是新 occurrence
  （GD-005；`TDP_004/005` 锁定）。
- `hasFullDelivered`：判定函数，null/空 tipName → false。

## 3. 历史字节：`src/Wanxiangshu/Journal/GuidelineProjection.fs`（GD-011）

```fsharp
type PairProgrammingGuideline =
    { Ordinal: int64; CallId: ToolCallId; MarkerText: string
      CallGap: TranscriptGap; ResultGap: TranscriptGap }
```

- `apply ordinal callId markerText callGap resultGap state`：三拒绝——ordinal ≠
  next（`NonSequentialOrdinal`）、CallId 重复（`DuplicateCallId`）、placement 重复
  （`DuplicatePlacement`，SessionId 隐含 + CallGap + ResultGap 至多一对）。
- `pairs`：存储 newest-first，返回 oldest-first（replay 顺序）。
- `MarkerText` 原样存储 → replay byte-identical（HOST-013「当时实际看到的精确
  正文」）。substrate 是 Journal fold，不是私有 delivery 文件。

## 4. marker 注入：`src/Wanxiangshu/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.fs`

- `tryInject`：生成 auto-injected tool-call/tool-result pair，marker text =
  tip guidance（Full/Identity）+ `"\n\n"` + pair-programming guideline 正文；
  锚到 transcript 的 CallGap/ResultGap。Main 侧没有 fake-user message
  （GD-009；`CTX_002_GUIDELINE_001/002` 锁定 marker 输出形状）。

## 5. 检测冲突门：`scripts/checks/enforcer-cross-family-collision.mjs`（GD-010）

- 解析 `resources/enforcer/*/enforcer.md` 的 `Trigger When` + `Definition` 段，
  tokenize（去 stopword、短 token），算 jaccard/cosine/levenshtein。
- fail 判据：trigger Jaccard ≥ 0.90 或 trigger Levenshtein ≥ 0.95，且非 mutual
  siblings（`Distinguish From` 互相承认）；warn/note 级照常输出 top-N evidence。
- 本体测试：`tests/unit/verify/enforcer-cross-family-collision.test.mjs`（KEEP，
  合成 temp dir，不依赖生产文案）。**不冒充**人类 A40 tournament（Rulebook
  Final outcome 诚实声明）。

## 6. 失败模式速查（红了说明什么）

| 症状 | 断裂的命题 | 排查入口 |
|---|---|---|
| 重复 resolve 仍给全文 | GD-003 | `resolveTipGuidance` 是否跳过 `hasFullTipDelivered` |
| IdentityOnly 后 reanchor 仍给身份 | GD-005 | `applyReanchor` 是否被接线（ContextReanchored fold） |
| restart 后第一次判定漂移 | GD-004 | 交付决策是否读进程内存 |
| main.md 进了 Blogger system | GD-008 | `composeBloggerSystemPromptFor` 是否混入 MainText |
| 历史 marker 字节被改写 | GD-011 | `GuidelineProjection` 是否按原文存储/重放 |
| 非 sibling 触发词撞车 | GD-010 | `enforcer-cross-family-collision.mjs` fail 判定 |

## 7. 验证命令

```text
node --test requirements/guidance-delivery/tests/<file>   # 单文件（每文件必须绿）
node tests/unit/run.mjs                                    # 全单元（cutover 时由 lead 执行）
```

## 8. 依赖

- `behavior-diagnosis`：交付消费已成立的 diagnosis occurrence（RecentTip）。
- `participant-horizon`：Coverage 是 horizon-relative 概念；本包只区分
  Frontier/Coverage 两轴，不定义 horizon admission 一般律。
- `durable-events`：交付事实（TipGuidanceDelivered）与历史 pair 是 durable fold
  的输入。

## 9. 历史与弃权

| 源 | 裁决 | 记录 |
|---|---|---|
| 「tip 不投 Main」单消费者叙述 | GARBAGE（双消费者裁决：Blogger 历史 ≠ Main 指令面） | `archive/changes/completed/rulebook.md` §27 |
| `delivered-tips.json` / process-local HashSet / 文件 tip ledger | GARBAGE（交付 substrate 只能是 EventStore fold） | `archive/changes/completed/rulebook.md` §16 |
| Main fake-user enforcement overlay / NudgeAnchored / NudgeConsumed | GARBAGE（clean break；交付不 mint authority） | `archive/changes/completed/enforcer.md` §10；`archive/docs/why/enforcer.md` |
| 每次 Full 或仅 Identity | GARBAGE（被拒方案：烧上下文 or 首次不可执行） | `archive/docs/why/enforcer.md` 备选与被拒 |
| 单一 durable bool 压 Frontier+Coverage | GARBAGE（reanchor 后误删/假装仍在） | `archive/docs/why/enforcer.md` 交付前沿 vs 语义覆盖 |
| 历史 pair 随 main.md 版本改写 | GARBAGE（byte-identical replay 冻结） | `archive/changes/completed/rulebook.md` §17 |
| enforcer-cross-family-collision.mjs | KEEP proof（机械 A40 替代，不冒充人类 tournament） | `requirements-design/PROOF-MAP.md` Phase D；`archive/changes/completed/rulebook.md` Final outcome |
| `enforcer-rulebook-gate.mjs` | 已退休空壳（2026-08-12）；本包不依赖任何 prose 形状门 | `requirements-design/HANDOFF.md` §24 |
| 当前实现中 TipSemanticCoverage 与 TipDeliveryProjection 同投影（applyReanchor 清空） | HOW（horizon 可恢复性以投影近似表达；字节级 horizon 探测是未来增强，不改变两轴分离合同） | 本文件 §1 诚实性注 |

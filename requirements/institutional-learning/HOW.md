# institutional-learning — HOW（非 normative）

## 目标实现形状

维持一个私有、短生命周期 Enhancer，而不是新 agent society：

```text
Experience(kind = Celebrate | Regret, text)
  -> Enhancer(currentRulebook, experience)
  -> Absorb existingRule | Birth candidateRule | Discard reason
  -> behavior-diagnosis institutional admission when Birth
```

Enhancer 不获得 provider-facing tool surface；`celebrate/regret` 是唯一入口。它可以使用已有模型调用能力完成抽象，但输入必须被限制在 experience + current canonical Rulebook，不展开新调查。具体模型/tier 是 HOW。

## canonical Enforcer admission

保持 `behavior-diagnosis` 的“一个 live Rulebook / 一个 TipName namespace”不变。BIRTH 生成双语 candidate，调用该 owner 的 institutional admission；ABSORB/DISCARD 都不写任何 rule。admission 负责 durable `InstitutionalRuleBorn` 与 live union validation，不引入 learned-rules database，也不要求安装目录可写。

这使 Enhancer 本身保持纯粹：它只做 generalization/disposition，不获得 repository filesystem mutation 权限。

## celebrate 与 defer 的组合

celebrate 先得到 learning disposition，再调用 `attention-regulation` 的窄 staging surface 读取“本 occurrence 若成功应 resurface 哪一批”，不先写 drain。BIRTH 同理先调用 `behavior-diagnosis` 的 pure admission precheck。全部 precheck 通过后，一次 EventStore atomic commit 写入：

```text
LearningDispositionCommitted(occurrence, frozenResult, disposition)
[InstitutionalRuleBorn(...)]             # Birth only
[DeferredWorkResurfaced(...)]             # Celebrate only, 0..N
```

tool result 先写 learning outcome，最后追加 frozen deferred items。replay 先查 `LearningDispositionCommitted`，命中则直接返回 frozen result，不重新跑 Enhancer/precheck/projection。

## DEPENDS ON

`institutional-learning → attention-regulation, behavior-diagnosis, durable-events`

## 验证与测试落点

可执行 proof 在 review 后由 GAP 建立：

| WHAT | 最低充分 proof |
|---|---|
| INSTITUTIONAL-LEARNING-001 | codec/tool contract：single string + polarity，不要求 schema 化经验 |
| INSTITUTIONAL-LEARNING-002 | pure disposition algebra；每 evaluation attempt one Enhancer；stale revision 最多一次 fresh re-evaluation；最终 ≤1 committed disposition |
| INSTITUTIONAL-LEARNING-003 | capability/architecture：Enhancer 无 repository/web investigation surface |
| INSTITUTIONAL-LEARNING-004 | integration：只有 BIRTH 调 canonical institutional admission；ABSORB/DISCARD 零 mutation、安装目录零写入 |
| INSTITUTIONAL-LEARNING-005 | validation/property examples：缺 trigger/negative/distinction/novelty 时不能 BIRTH |
| INSTITUTIONAL-LEARNING-006 | positive success mechanism 可诚实保留，不强制惩罚式改写 |
| INSTITUTIONAL-LEARNING-007 | temporal：enhance → validate → resurface；regret 不 drain |
| INSTITUTIONAL-LEARNING-008 | atomic learning transaction：precheck/stale RulebookRevision fail zero-commit；BIRTH/receipt/defer resurfacing 无半提交；replay 不再跑 Enhancer/不重 drain |

## 历史与弃权

- 不新增 `enhance` provider tool；Enhancer 是 `celebrate/regret` 的私有 consequence。
- 不把 ABSORB/BIRTH/DISCARD 暴露成三个工具。
- 经验可能被现有 rule 或 primitive（如 `enough`）完整吸收，此时应 ABSORB/DISCARD，不为“学习过”强造新 rule。

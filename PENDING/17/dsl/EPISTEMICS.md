# Meditator 认识论语义（140 版）

> 本文记录认识论语义的**明确决策**与**已知未决策项**。140 版评审（认识论方向
> 重新优先）指出：grade 聚合语义必须先行定义，不能先定格结构再寻找解释。

## 1. grade 的语义：报告保证等级（report assurance level）

**决策：`EpistemicGrade` 表示"报告对每条被引用证据的保证下界"（meet 语义）。**

$$G(\text{report}) = \bigwedge_{\text{引用证据 } w} G(w)$$

含义：

- 报告声称的每个 grade 分量（Directness/Reliability/Coverage/Reproducibility），
  **每条被报告引用的证据都达到**（逐维 meet）；
- **Independence 例外**：该维度 = 引用证据的依赖簇数（增强维度，不参与 meet）——
  更多独立簇表示更多独立佐证，不会因加入一条独立证据而下降（见 §2 表格）；
- 报告**不引用**的证据不影响报告 grade（139 版起按引用集计算，非全账本）；
- 加入一条较弱证据**且被报告引用** → Directness/Reliability 等维诚实下降（报告选择了更弱的保证）；
- 加入较弱证据**但不引用** → 保证不变（引用集未变）。

**明确不是**：

- 不是"证据集合的总体支持强度"（无加权/平均/多数——被禁止）；
- 不是"最佳可用证据质量"（不引用弱证据≠假装它不存在；报告必须用
  `EvidenceLimitations`/`Unknowns` 显式标注被排除的证据）；
- 不是"现实概率"（`Credence` 与 `ControlScore` 独立类型，无转换）。

推论：**"加入弱证据使整体 grade 下降"是设计语义，不是 bug**——当且仅当报告
引用该弱证据时成立；报告可通过不引用 + 附注排除它。报告引用集的完整性由
`verifyCanonicalReport` 的 `unreferencedProofDigests` 检查保证（停证引用 ⊆
报告展示）。

## 2. 五维分量语义（逐维 meet-semilattice）

| 分量 | 语义（报告保证的维度） | 序 |
|---|---|---|
| Directness | 证据到结论的直接性（观察/推导/启发） | Direct > Derivational > Heuristic |
| Reliability | 证据来源可靠性（由 Strength 初始化） | Confirmed > Corroborated > Tentative |
| Independence | 独立证据簇数（provenance/witness 连通分量） | 停证级：引用集簇数（增强统计）；报告级：跨侧 meet 取 min（保守保证——报告声称的簇数两侧都达到） |
| Coverage | 覆盖证书（OpenWorld/ClosedWorld） | 仅由 certificate 升级 |
| Reproducibility | 重放验证状态 | 仅由成功重放升级 |

**已知未决策项**（140 版评审 §2）：

- "总体支持强度"（多条中等证据的总和强度）未建模——当前拒绝单总分，
  未来若引入必须与保证等级分开（如报告附注维度）；
- "证据覆盖范围"（搜索空间被覆盖的比例）未建模——`Coverage` 是证书不是比例；
- Strength 的获得尚无校准定义（见 §4）。

## 3. Belnap 极性是信息结构标签，不是认识结论

**决策：`SupportedOnly / RefutedOnly / Contested / Unknown` 描述"信息是否出现"
（warrant 集合非空性），不判断"哪侧更强 / 是否达到断言阈值"。**

推论：

- 一条弱支持 warrant 即产生 `SupportedOnly`——它只意味着"存在支持信息"；
- 报告必须逐维展示 grade 与限制，禁止仅凭极性标签下结论；
- `TargetRefuted` **不得**仅由 `RefutedOnly` 极性位产生——必须携带
  `RefutationRule`（见 §5）。

## 4. Strength → Reliability 的语义状态

**现状**：`Strong → Confirmed / Moderate → Corroborated / Weak → Tentative`
是版本化 policy 的初始赋值（`gradeOfWarrant`），不是校准结果。

**未决策项**：Strong 如何获得（观察协议质量/样本量/测量误差/来源可靠性/
可复现次数/推理规则有效性/对替代解释的排除程度）——需要校准 profile。
当前诚实表述：grade 是类型标签，不是经验校准的概率。

## 5. 反驳语义：RefutationRule

**决策**：`TargetRefuted` 出口由 `RefutationRule` 决定，注册表版本化：

- P0 注册 `LogicalCounterexample`（全称/存在形态命题的逻辑反例）；
- `StatisticalRefutation`（统计/普遍性经验命题的反驳，需统计模型）未注册；
- 无 rule 或 rule 不在注册表 → 出口拒绝。

推论：单个反例不能反驳统计性命题（"锻炼有助于健康"）——该命题的反驳
需要未注册的 `StatisticalRefutation`，P0 诚实拒绝。

## 6. ClaimTest 成功语义：MinimalDialecticCompleted

**决策**：P0 的 `ContractSatisfied`（ClaimTest 契约）证明的是：

```text
目标已 frame（scope 敏感 ClaimId）
义务全部解除（双侧各至少一条 warrant）
停证引用集 ⊆ 报告展示
```

**不证明**：充分测试、搜索范围足够、强证据无遗漏、两侧可比性。
合同语义 = "至少收集一条正方与一条反方证据的结构化双侧记录"。
报告中以此语义呈现，禁止声称"已充分验证"。

## 7. 认识论正确性的验证分层（140 版评审四阶段）

1. 纯数学性质（本仓库属性测试）：极性单调、推导保守、引用集隔离、簇语义；
2. 有限可判定领域 E2E（`Tests.GraphReachability.fs`）：ground truth 机械判定，
   验证 SupportedOnly↔真、RefutedOnly↔假、grade 随证据质量变化、stop 时机；
3. 反例驱动测试（`Tests.Counterexamples.fs`）：10 个认识论最小反例；
4. 真实任务基准（需 LLM oracle 装配，当前未实现——合成基准先行）。

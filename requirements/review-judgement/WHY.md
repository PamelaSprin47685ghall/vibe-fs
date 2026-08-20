# review-judgement — WHY

## 1. 存在理由与核心矛盾

万象术质量门的核心在于 Reviewer 对交付工作的判断，并最终汇入 mission 的终局（Finality）与过程节拍（TodoProcessReview）。该链条最关键的语义前提在于：

> Reviewer 输出的 `PERFECT` 或 `REVISE` 在被系统消费之前，必须具有明确且不可替代的领域意义。

如果缺乏健全的判断哲学，系统将退化为以下失败形态：

1. **表演式拒绝**：Reviewer 认为「拒绝越多越谨慎」，将无关痛痒的细节抬格为阻断项（withhold）。REVISE 不再代表工作尚未挣得 acceptance，而是退化为姿态表演，迫使执行者修复虚构缺陷。
2. **固定 Checklist 机械打分**：将判断压缩为必须逐项打勾的表格。审查退化为填表，八维全过即 PERFECT，任一不符即 REVISE，判断的区分力（discrimination）被死板结构替代。
3. **无证据偏好冒充缺陷**：Reviewer 将个人实现偏好（「我会写得不同」）定性为缺陷。REVISE 无法购买任何实质改进，仅表达个人口味。
4. **PERFECT 被误读为全知或字面无瑕**：Reviewer 因担心破坏 PERFECT 结论而隐瞒真实且非阻断的工艺观察，或者反之因微小拼写错误而机械 REVISE。
5. **过程与终结回执语义混淆**：过程评审单次判断即已完成本轮请求，回执应指示结束；终审首个 PERFECT 仅为确认协议的前半段，须要求再次评估。若混淆两类回执，将导致 Reviewer 收到相互冲突的控制指令。

`review-judgement` 保证：**PERFECT 与 REVISE 必须由 discrimination 挣得**。Acceptance 需要证据挣得，Rejection 同样需要证据挣得；唯有 material defect 才能扣留 acceptance；非阻断工艺观察可与 acceptance 共存；PERFECT 不代表全知；REVISE 必须购买实质上更好、更真实的结果。

## 2. 独立存在测试（Independent Change Test）

若重写 Role Law 或 Examiner's Ledger 的判断方向（例如引入新的重要性判定准则），只要 `judge` 工具面与 verdict 枚举不变，`review-assurance` 的因果确认协议、witness 数据结构与 record-ready 代数完全无需修改。

反之，若重写 dual-PERFECT 的因果编排与状态流转，judgement 的 discrimination 语义同样保持独立。两者分属不同失败域：
- `review-judgement` 失败意味着审查者可凭表演、偏好或死板表格做出裁决。
- `review-assurance` 失败意味着系统消费了针对旧代码状态、未见 challenge 或缺失报告的裁决。

## 3. 核心不变量与失败判定

系统在以下任一情况发生时判定为 RED：

- Reviewer 凭表演式谨慎、死板 checklist 或无证据偏好做出 accept/reject 裁决。
- `judge` 的 verdict 被作为可回声的状态对象，或工具面包含描述字段及已废弃名称。
- 判断脱离 root requirement 与当前被审对象，受审查者主观情绪或口味支配。
- PERFECT 被视作全知无瑕的承诺，导致非阻断观察被强制噤声。
- REVISE 未购买任何实质上更好或更真实的产出。
- Examiner's Ledger 被固化为必填 report schema、Pass 表或固定标题模板。
- 过程评审与终审 challenge 的回执语义混淆，导致单次请求收到相互矛盾的终止与继续指令。

## 4. 依赖边界

```text
DEPENDS ON: cognitive-environment, participant-horizon
```

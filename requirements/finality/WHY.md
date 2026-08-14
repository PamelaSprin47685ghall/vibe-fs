# finality — WHY

## 1. 不可替代的存在理由

Manager 说「我做完了」只是 participant 的**自宣**。world 允许不可逆结束（LifeCompleted、terminal、
释放资源）之前，必须回答三个独立问题：

1. **当前义务**：Manager 是否已经进入并遵守了 checkpoint 协议、抽干了未消费的过程评审
   （obligation-ledger）？
2. **当前被审对象**：终结所依据的 review 是否针对当前 tree / 当前 request，而不是旧证据
   （review-assurance）？
3. **证据合格**：有没有合格的终结评审证据（fresh barrier + dual-PERFECT witness）？

如果跳过这些，系统会退化成以下坏世界：

- **participant 自宣即结束**：Manager 调用 `suicide` 就完成，零 checkpoint、零评审也能通过——
  终局质量门变成可勾选的 checklist 步骤。
- **rejection / acceptance / rest 被压成含混 terminal state**：未接受当安息，
  或已接受却被禁止收尾；「结束」只有一个模糊状态，Manager 无法区分「被拒后继续工作」、
  「已被接受但还有 minor work」与「真正安息」。
- **接受被后续 non-blocking 撤销**：already-accepted 的 blessing 因为 minor findings 被推翻，
  Acceptance 失去保护。
- **隐藏评审泄漏**：Manager 看到 Reviewer、barrier、witness、2N，把终局质量门重新变成
  「显式最后一步」，在工作未收敛时机械执行。

**finality 保证：不可逆 mission end 的资格建立在 obligations + current tree + qualified review
evidence 上；rejection / blessed / rest 是三种不同经验；Acceptance 与 rest 是不同阈。**

## 2. 独立存在测试（Independent Change Test）

把 `suicide` 的 UX / 工具名 / hidden reviewer cohort 形状整体重写（例如把工具改名、把 cohort
换成另一种 causally verifiable confirmation protocol）——只要「只有合格证据才允许 life
completion」的 WHAT 不变，obligation-ledger、review-assurance、participant-horizon 的 WHAT
一律不需要改。

反过来，把「零 checkpoint fail closed」或「rejection ≠ rest」改掉，会让 Manager 绕过义务与
评审直接结束——这是独立的失败域（与账本、与 witness 协议都不同）。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. participant 可绕过 outstanding obligations / review 直接结束（零 checkpoint 进 Finality、
   悬挂 Rk 不 drain、无 fresh barrier/tree 就 bless）；
2. acceptance / rejection / rest 被压成一个含混 terminal state（未接受当安息、已接受禁止收尾、
   non-blocking 事后升格为 blocker 撤销 acceptance）；
3. Manager 面出现隐藏 Reviewer / barrier / witness / 2N / cohort 编排（hidden mechanism 变成
   Manager checklist）；
4. 状态来自故事文本反解而非 typed facts（`suicide`/`glory`/`distant future` 文本推导状态）。

## 4. 历史考古（为什么曾经 RED）

旧 prompt 把 review 显式化为 checklist 的最后一步（调查→修改→测试→review→返回），Manager 会在
工作尚未收敛时机械执行最后一项。完成顺序被反转：Manager 主动请求终结（`suicide`）→ Host 才开始
隐藏 Finality 审查；Reviewer 存在本身、双 PERFECT、barrier、witness、2N 全部对 Manager 隐藏
（GLORY-002），把终局质量门从「可勾选的步骤」变成「不可见的命运」。

历史上还有：`verdict`/`judge` 工具名之争（`judge` 属 Reviewer、`suicide` 属 Manager，因果身份不同）；
单一结束文案把未接受当安息（why「Finality：单一结束文案 vs rejection/blessed/rest」裁决）；
结构化 `FinalityFinding` schema 被拒（第二事实源、摘要漂移，GLORY-004/049/050）。
完整推导见历史 why/glory 与历史 why/review 条款；被拒方案记录在 HOW.md「历史与弃权」。

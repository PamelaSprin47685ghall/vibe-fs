# obligation-ledger — WHY

## 1. 不可替代的存在理由

Manager 是长期 mission 的执行者。系统随时要回答同一个问题：

> 这个 mission 现在还欠用户什么？

如果答案不是「一个持续维护的诚实账本」，就会退化成以下任一坏世界：

- **phase/status 伪装进度**：`pending → in_progress → reviewing → completed` 描述的是「我现在在哪个工作阶段」，
  不是「用户请求仍缺哪些真」。计划、等待、评审被冒充成债务，系统把过程动作当成用户仍欠的结果。
- **meta-work 冒充债务**：`make a plan` / `analyze first` / `write todos` 被写进账本——
  把 Planning Table 本身伪装成 mission debt，还会过早触发 T1 揭幕。
- **reviewer 拥有账本写权**：PERFECT/REVISE 决定哪个 account 才「真正生效」，
  制造 accepted-but-not-current 半态、回滚语义和第二 writer；崩溃恢复要重演 merge 策略。
- **崩溃后只能猜**：用内存 Stage、布尔组合、时间猜测恢复，而不是重放 durable Accepted 链。

**obligation-ledger 保证：当前 mission debt 有一个唯一真相源（last `TodoWriteAccepted` 对应的完整
`{name,work}` account），由 Journal fold 纯推导，任何阶段机、meta-work、reviewer settlement 或 Host
UI 表都无权改写它。**

## 2. 独立存在测试（Independent Change Test）

把当前 `todowrite` 的 UI / schema / 工具名整体重写（例如改成另一种 obligation authoring surface）——
只要「canonical account + checkpoint + supersession」语义不变，其它包（finality、review-assurance、
work-record、prefix-stability）的 WHAT 一律不需要改。

反过来，把「Accepted 立即 supersede CurrentObligations」改成「先等 reviewer 批准」或引入
`kind/id/status` 冷状态机，会让 finality 的 drain、crash 恢复、过程评审节拍全部失真——这是一个独立的失败域。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. 当前 mission debt 无唯一真相源（Host TodoTable、reviewer、内存 Stage 都可以当 current）；
2. workflow / meta-work 被当成用户仍欠的结果（`make a plan` 之类占 obligation）；
3. REVISE 能静默回滚已经 accepted 的 account（reviewer settlement / semanticMerge 复活）；
4. 崩溃恢复不重放 Accepted 链而靠 Stage / 布尔 / 时间猜；
5. 同一 message 多个 todowrite 有 winner 仲裁，或 infra 失败被降格成 tool 红字。

## 4. 历史考古（为什么曾经 RED）

`archive/changes/completed/magic-todo.md` 的 GrandRewrite 之前，provider 冷状态带
`kind/id/status/priority/reviewing`，`settled/proposed/semanticMerge` 三态 + status min-merge 决定
「preview 是否生效」。被拒方案与裁决：

| 被拒方向 | 裁决 |
|---|---|
| 同 message 多 todowrite 按 hook 到达顺序仲裁 winner | 全部作为语法/协议错误拒绝；无 ordinal winner |
| V2 runner 裸奔（无 hook parity） | Attempt construction fail closed；错入则 fatal |
| Host 用自然语言关键词分类器识别 meta-work | 分类器会把脆弱启发式重新变成隐藏状态机；语义只在 provider decision surface 表达 |
| reviewer 以 PERFECT/REVISE 决定哪个 account 生效 | reviewer 只判断并报告；REVISE 不涂改 Tk |
| 每 Life 从 Host TodoTable adopt 旧项 | 新 Life canonical 空；仅升级瞬间一次 seed |
| 用 `TodoStage`/`AwaitingReview` 程序计数器 | 恢复只从 durable facts 推导 |

完整推导见 `archive/docs/why/todo.md`；这些被拒方案记录在 HOW.md「历史与弃权」。

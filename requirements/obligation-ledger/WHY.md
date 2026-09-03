# obligation-ledger — WHY

## 1. 存在理由与核心矛盾

Manager 作为长期 mission 的执行者，系统必须随时回答核心问题：

> 这个 mission 现在还欠用户什么？

如果缺乏一个持续维护的诚实账本，执行过程将退化为不可接受的失败形态：

1. **以过程阶段伪装真实债务**：将调查、等待、评审等过程动作编码为任务状态，把执行活动本身冒充为交付给用户的真实结果。
2. **混淆规划债务与使命债务**：在计划完备前，模型需要记录调查与分解工作；若协议禁止记录，模型会被迫伪造假的 mission debt，使账本失真并过早触发完备承诺。
3. **评审者篡夺账本写权**：由 PERFECT/REVISE 决定哪份 account 生效，制造 accepted-but-not-current 半态与多 writer 冲突，破坏崩溃恢复的可重演性。
4. **状态推导依赖脆弱内存猜测**：崩溃恢复依赖 Stage 状态机或布尔标志，而非重放 durable Accepted 事实链。
5. **执行前沿缺乏透视分辨率**：以平面粒度展开所有未来任务，导致近处不够可执行、远处提前展开脆弱步骤；或者只保留粗颗粒度，使当前前沿失去闭环动作。

`obligation-ledger` 保证：当前 owed work 拥有单一事实源（最新 `TodoWriteAccepted` 对应的完整 account）。在 `planComplete=false` 时，它诚实记录完成计划所需的 planning work；首次 accepted `planComplete=true` 是不可逆的 commitment（T1），此后同一账本只记录 mission debt。`workingOn` 是当前唯一实际推进的焦点，也是 `near/mid/far` 透视分辨率的原点。

## 2. 独立存在测试（Independent Change Test）

若重写 `todowrite` 的 UI、schema 或工具名，只要保持「canonical account + checkpoint + supersession」语义不变，`finality`、`review-assurance` 与 `prefix-stability` 的规范定义无需改动。

反之，若破坏「Accepted 立即 supersede CurrentObligations」、引入评审者写权或加入 `status` 冷状态机，`finality` 的判定、崩溃恢复与义务账本将同时失效。这是一个独立的失败域。

## 3. 核心不变量与失败判定

系统在以下任一情况发生时判定为 RED：

- 当前 mission debt 失去单一真相源（Host 表、reviewer 或内存状态可充当 current）。
- `planComplete=false` 时 planning work 被迫伪装为 mission debt，或 commitment 后仍将 planning placeholder 记为 mission debt。
- 崩溃恢复不重放 Accepted 链，而是依赖 Stage、布尔标志或时间推算。
- 同一 message 中多个已 materialize 的 todowrite 破坏顺序执行语义，或基础设施故障被降格为工具红字。
- 当前执行前沿缺少可闭环的细粒度义务，或遥远债务被迫以细粒度提前展开；`near/mid/far` 被误作生命周期状态机。

## 4. 依赖边界

```text
DEPENDS ON: durable-events, effect-accounting, semantic-trace
```

## Physical fatal boundary

MagicTodo input/materialization conflict与accepted checkpoint settlement由obligation-ledger拥有；process termination是Host effect。direct fatal会让codec/membrane跨过durable evidence边界，并可能把普通provider-visible rejection误作进程事故。

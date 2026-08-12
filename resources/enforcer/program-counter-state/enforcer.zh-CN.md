# program-counter-state — Enforcer 中文版

## 定义
Program-counter state 出现于：持久化/共享字段主要不是描述现实，而是在描述“解释器下一步该执行哪段代码”。`currentStep`、`nextAction`、`phase = CallFooThenBar`、某些 generation/owner flags 都可能把 instruction pointer 伪装成 domain state。

这会把实现顺序冻结进数据模型：重构 control flow 变成 migration，recovery 变成“跳回旧函数中间继续跑”，而不是从 durable facts 重新决定下一步。

## 何时触发
- DB 里存函数名、step number、next handler；
- crash recovery 依赖“恢复到第 N 步继续执行”；
- 字段存在的唯一理由是告诉 code branch 下一行去哪里；
- lifecycle 被 implementation stages 命名，而不是 domain-observable facts；
- 改内部 sequencing 必须迁移历史 state，即使业务世界没有变化。

## 不要误判
- `Draft/Submitted/Approved` 若是产品真实承诺的 workflow 状态，就是 domain fact；
- lease/generation 若代表真实 ownership/fencing fact，也不是 program counter；
- operation-local continuation 不持久化、不共享，不构成本规则；
- saga/process manager 可以持久化业务 progress facts，但不应持久化“下一函数地址”替代事实。

## 刀口
问：**如果今天把实现从 callback 改成 state machine、从两步合成一步，外部 domain observer 是否仍会关心这个字段？**

若不会，它大概率属于 interpreter，不属于 world。

## 与近邻区分
`phase-flag-accumulation` 用多个 flags 拼隐式 phase；这里直接把执行位置当 durable data。

`command-event-confusion` 混淆意图与事实；这里混淆事实与“接下来执行什么”。

## 例子
- 正例：`job.step = "upload_blob"`，restart 后 switch 到 `upload_blob()` 中间继续。
- 近邻：`PaymentAuthorized` 是已发生事实，recovery 根据事实重新推导下一 command。
- 反例：customer-visible `Submitted` 真的决定允许/禁止哪些业务行为。

## 提醒
持久化世界发生了什么，不要持久化解释器当时走到哪一行。

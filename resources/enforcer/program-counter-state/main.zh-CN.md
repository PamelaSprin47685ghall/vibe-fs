# program-counter-state — Main 中文版

## 现在该做什么
把 durable state 分成两类：外部世界事实保留；纯执行位置移回 structured control flow。Recovery 应从 durable facts 重新推导当前允许的下一动作，而不是跳回历史代码的某个 step。

## 为什么这很重要
一旦 instruction pointer 进入持久化 schema，实现细节就获得了比代码本身更长的寿命。函数重命名、步骤合并、异步模型改变都可能让旧数据“指向不存在的代码位置”。

更糟的是 program counter 通常不能证明 side effect 是否已发生：`step=3` 只说明程序认为自己走到这里，不说明 step 2 的远端效果是否 committed。

## 修复策略
- 找出每个 step 背后真正的 durable fact；
- 记录 `Requested/Accepted/Committed/Observed/...` 这类现实事实，而非 handler 名；
- restart 时 fold facts，再由当前代码决定合法 continuation；
- 对外部 unknown outcome 使用 reconciliation/idempotency，不用 step number 猜测；
- 只有真正 domain-visible workflow status 才进入 durable model。

## 常见假修复
- 把 `currentStep` 改名 `status`。
- 存更多 sub-step 以“精确恢复”。
- 保存 function name / continuation token，强迫未来代码兼容旧 instruction pointer。
- 把 step 与事实双写，最后再 reconcile 两者。
- 仅靠 migration 更新 step number，继续保留同一错误模型。

## 验证
尝试大幅重排内部 control flow，但不改变 domain facts。旧 durable history 应仍能由新代码重放/恢复，无需理解旧函数编号。

Crash injection 后，recovery 的判断依据应能指向实际 durable evidence，而不是“我们上次记得自己执行到第几步”。

## 完成条件
持久化数据描述世界；structured control flow 描述程序。两者不再互相冒充。

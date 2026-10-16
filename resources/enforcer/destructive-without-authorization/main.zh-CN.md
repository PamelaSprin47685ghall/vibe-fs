# destructive-without-authorization — Main 中文版

## 现在该做什么

把 destructive step 前置成两道独立检查：authority proof 与 target identity proof。对 broad target 先 inspect/dry-run，缩成具体对象；执行时再次绑定该 identity，而不是重新计算一个可能漂移的 selector。

## 为什么重要

删除错误目标的问题无法靠“后面修正”抵消。越不可恢复，越不能允许 inferred authority、stale path、ambiguous glob、默认 branch 这类弱证据进入执行链。

## 修复策略

通过权威 identity 解析目标，清楚呈现 destructive effect，并要求合适的 authorization boundary。对自动化，让 target 与 scope 可被机器核验，而不是依赖路径相似或 operator 记忆。

## 决策分支

- 若这类破坏的 authority 缺失，在任何 irreversible step 之前停下并取得它。
- 若 authority 存在但 target identity 来自推断、已过期或长得像，重新解析具体对象并确认。
- 若 artifact 是 ephemeral、scoped、且 contract 可再生产，在该 scoped cleanup 下继续；本规则不阻止它。

## 常见假修复

- 只加“Are you sure?”，但用户仍不知道系统将删哪个 target。
- 只验证 path 存在，不验证它是获授权对象。
- 用更严格的 glob，却仍没有 concrete identity。
- 每个普通 temp cleanup 都要求人类确认，制造 confirmation fatigue；明确 scoped ownership 应自动清理。

## 验证

让 target path/name 发生可控歧义或漂移，destructive step 应 fail closed，而不是“选一个最像的”。无 authority 的调用也必须在 mutation 前失败。

## 完成条件

所有不可逆操作都能指出同一时刻成立的两份证据：有权做，以及做的是对的对象。
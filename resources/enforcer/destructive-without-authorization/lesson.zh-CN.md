# destructive-without-authorization — Lesson

## 评价

### 定义

破坏性操作需要比普通 mutation 更高的证明负担。真正的缺陷不是“用了 rm/delete”，而是 irreversible action 在没有同时证明**谁授权**与**具体目标是谁**的情况下执行。

Authority 与 identity 是两个独立事实：任务允许删某类东西，不代表当前 path 就是那个东西；目标看起来像 intended target，也不代表执行者有权删。

### 支配原则

不可逆性改变证明负担。对于可添加或可检查的动作，错误往往还能事后修正；删除则坍缩了可能的未来。因此“我大概知道这是什么”不足以作为证据。安全的破坏需要两个独立事实：有权决定的人已授权这类动作，且即将被破坏的具体对象正是 intended object。

### 何时触发

- 删除 branch/worktree/data/resource 前靠路径/名字猜 target；
- authorization 来自旧上下文、默认惯例或“用户大概就是这个意思”；
- overwrite credential/config/history 无明确 authority；
- destructive command 使用 broad glob/relative path，却没有最后目标核验；
- 不可恢复动作被当成普通 cleanup step。

### 不应触发

- 同一 scoped operation 刚创建的可再生 temp/build artifact，cleanup contract 已明确；
- dry-run/inspect-only 不改变世界；
- owner 在当前任务明确点名具体 target，执行前 identity 可再次核实；
- reversible local edit 不应被本规则无限升级成 confirmation theater。

### 与相邻规则区分

`scope-creep` 管的是超出意图的工作；`secret-in-code` 管的是凭据暴露。本规则专门管不可逆变更前缺失 authority/identity 证明。Tie-break：如果动作不可逆、且 authority 或 target identity 任一来自推断，即使更广的任务本身在范围内，这个案子也归本规则。

### 判定程序

执行前必须分别回答：

1. 谁授权了这类 destructive action？
2. 什么证据证明眼前这个 concrete target 正是获授权的那个？

任何一个答案是“应该是”，就还没达到 irreversible proof burden。

### 例子

- positive：因为当前路径“看起来像”目标就删除 branch/directory，没有独立 identity check。
- near-miss：在显式 cleanup contract 下，删除同一 process 刚创建的 job-scoped temp directory。
- counterexample：在 destructive step 前要求显式 authority，并立即核验 concrete target。

## 建议

### 现在该做什么

把 destructive step 前置成两道独立检查：authority proof 与 target identity proof。对 broad target 先 inspect/dry-run，缩成具体对象；执行时再次绑定该 identity，而不是重新计算一个可能漂移的 selector。

### 为什么重要

删除错误目标的问题无法靠“后面修正”抵消。越不可恢复，越不能允许 inferred authority、stale path、ambiguous glob、默认 branch 这类弱证据进入执行链。

### 修复策略

通过权威 identity 解析目标，清楚呈现 destructive effect，并要求合适的 authorization boundary。对自动化，让 target 与 scope 可被机器核验，而不是依赖路径相似或 operator 记忆。

### 决策分支

- 若这类破坏的 authority 缺失，在任何 irreversible step 之前停下并取得它。
- 若 authority 存在但 target identity 来自推断、已过期或长得像，重新解析具体对象并确认。
- 若 artifact 是 ephemeral、scoped、且 contract 可再生产，在该 scoped cleanup 下继续；本规则不阻止它。

### 常见假修复

- 只加“Are you sure?”，但用户仍不知道系统将删哪个 target。
- 只验证 path 存在，不验证它是获授权对象。
- 用更严格的 glob，却仍没有 concrete identity。
- 每个普通 temp cleanup 都要求人类确认，制造 confirmation fatigue；明确 scoped ownership 应自动清理。

### 验证

让 target path/name 发生可控歧义或漂移，destructive step 应 fail closed，而不是“选一个最像的”。无 authority 的调用也必须在 mutation 前失败。

### 完成条件

所有不可逆操作都能指出同一时刻成立的两份证据：有权做，以及做的是对的对象。

### Nudge

破坏不是“更强的写操作”。它是在删除未来选项。没有 authority + identity 两份证据，不要用不可逆性替推测下注。

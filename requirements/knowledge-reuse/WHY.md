# WHY — 为什么 knowledge-reuse 必须独立存在

## 不可替代的存在理由

> Inspector 的一次调用天然形成知识单元（Question → 调查 → Answer），调用结束后只存在于 transcript；后续 Inspector 面对相同或高度相关的问题必须重新调查，已消耗的 read/glob/grep 证据无法复用。Casebook 让旧答案可 fetch、按当前 worktree 重放 observations、无变化时直接复用——**best-effort semantic cache，不是知识数据库**（历史 why/casebook 条款）。

本包保证的是一条**复用规则**，而不是「复用内容」：

```text
Case = one question + one answer + supporting replayable observations
fetch 前先重放 observations → freshness hint
no-delta ≠ correctness proof
检测到变化 → 基于已提供证据重塑 Case（maintenance 不自动获得回 repository 取证权）
并发 fork → 显式 DomainConflict，绝不 (revision, wall_clock) LWW
feature 完全 opt-in；未启用 repository 行为保持不变
```

## 为什么不能并入其它包

- 不是 `repository-investigation`：investigation 拥有「当前 repository fact 如何被真实观察建立」；本包拥有「**已建立的旧知识如何被安全复用**」。两者 failure meaning 不同：investigation RED = 推理冒充证据；本包 RED = 旧 Q/A 被当当前事实 / freshness 被当 correctness / 并发静默丢分支。reuse 必须**依赖** investigation（replay 是真实观察），但这不是同一个 WHY。
- 不是 `durable-events`：EventStore 是 Case 的物理 substrate，但「Case 是什么、何时 fetch/refresh、freshness 语义」是领域语义；store proof 不拥有 feature 语义（PROOF-MAP：feature event semantics 不归 store proof）。
- 不是 `durable-convergence`：replica 按对象语义收敛的**一般律**归它；本包只拥有 Case 对象的复用/维护语义，并**消费** convergence 提供的 DomainConflict 表达。
- 不是 `epistemic-reasoning` / `speculative-investigation`：Casebook 不生成新知识、不猜、不做认识状态求解；它是已支付成本的复用。

独立 change 测试：Case maintenance 从 Bookkeeper agent 改成 deterministic merge + optional LLM，而 Case reuse semantics 不变——本包命题全部成立（`17-repository.md` INDEPENDENT CHANGE）。

## RED 是什么样（失败模式）

```text
RED = 旧 Q/A 被当作当前事实（无 replay / replay 被跳过）
    ∨ freshness hint 被当作 correctness proof
    ∨ 并发更新的分支被静默丢弃（LWW）
    ∨ fetch 改了 subject worktree
    ∨ 未启用 feature 的 repository 行为被改变
```

具体可观察形态（来自历史 change（perm-inspector）的失败面）：

| 形态 | 违反 |
|---|---|
| fetch 不重放 observations 直接返回旧 A | 004/005 |
| 把「没检测到 observation 变化」说成「答案正确」 | 005 |
| 用 timestamp / revision 决定 merge winner | 011 |
| Bookkeeper 拥有 filesystem capability，能回 repository 再取证 | 006 |
| marker 缺失时 fetch 工具仍可见 / 仍可执行 | 009 |
| 每个 return 都 finalize 一个 Case（碎片化） | 010 |
| 崩溃/删除后自动 reconstruct + synthesize 旧 Case | 010 |
| provider 看到 session id / freshness 机器字段 | 012 |

## 历史背景（为什么这些命题不是纸上谈兵）

历史 change（perm-inspector）（Inspector Casebook）确立的**设计姿态**：

> Casebook 是 hopefully useful 的 best-effort semantic cache，不是证明系统。旧答案可能因 observation capture 不完整、shell 阅读未识别、未观察到的新文件、并发变化、Bookkeeper 失败而过时——这些是**允许的产品行为**。

机械安全边界（fail closed）与 best-effort 性质（允许过时）是两类不同命题：前者进 WHAT（001/003/006/007/009/010/011/012），后者也进 WHAT（004/005 的「不证明正确」）。任何机制都不得被提升为 correctness proof。

## 历史拒绝方案（被拒 ≠ 永久命题，记录 WHY）

| 被拒方案 | 拒绝理由 | 现行命题 |
|---|---|---|
| 独立 Git store / refs / hook | feature store 无法共享 Persist 的 merge/CAS/恢复；remote 同步是 dumb-remote ConvergeStore 的职责 | 007 |
| timestamp / revision 决定 freshness 与 merge winner | 时间戳不证明内容未变；revision 排序制造第二真相 | 004/005/011 |
| 逐调用 finalize | 每个 return 一次 provider 事务，复用调查被碎片化；ReuseScope close 一次 finalize 才对应一个 Case | 010 |
| 从 transcript 文本推断 observation | 文本推断重放时不可靠；capture 必须来自工具执行的 typed 结果 | 003 |
| full knowledge base | Casebook 不保证历史 Q/A 可追溯为产品 API、不建 commit history、不改 subject worktree | 001 |
| 无 marker 也运行（opt-out） | 未启用 repository 的行为必须与现状逐字节一致 | 009 |
| `edit-qa(document, old_text, new_text)` 双文档字符串替换 | Q/A 分文件竞态；多次短语编辑无法保证 Case 仍描述同一世界 | 006 |
| Bookkeeper 借用 Inspector self-model | 调查自我模型暗示可回世界取证，破坏「证据已供给」边界 | 006 |

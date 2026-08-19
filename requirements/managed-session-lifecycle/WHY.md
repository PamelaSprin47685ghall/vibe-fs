# WHY — managed-session-lifecycle

## 一句话

只要系统创建 managed session，就必须有唯一 owner 负责创建、复用、停止、回收与 replacement；
否则每个 feature 都会复制 parent map、cancel、retire、restore 规则。

## 不可替代的存在理由

1. **所有权事实多个 owner = 恢复与级联必然分叉**。为 SyncDelegate 或 Strength 复制独立
   parent/child map、恢复、取消、retire 框架（历史 why/host §15 被拒方案），崩溃恢复与
   级联取消会各自按自己的半套规则走，同一 child 在不同路径下得到不同结局。
2. **已回收状态必须不可重新激活**。`Retired` / `Abandoned` 是 durable terminal；没有 tombstone
   语义，restart 会把已消费的 completion 再次投递（EXEC-009），或把「已完成的 child」重新 fork
   成新逻辑人。
3. **reuse 必须可证明地安全**。restart 后按 journal 关联（SessionId + agent + title）精确匹配才
   复用；无关联一律新建、不收养同 root 下别人的 child；查询失败、重复候选、归属冲突 fail closed
   （HOST-015 / REVIEW-019）。否则「恢复」会静默绑错 session。
4. **Reusable vs OneShot 是两条互斥生命周期**。SyncDelegate 的 dispose-after 不得套在 dedicated
   Session 上，反之亦然（EXEC-028）；ReuseScope 是 Dedicated 绑定的真正生命周期，不是 owner
   Session 的 dispose（universal.md §11）。
5. **单一 writer**。`HandleLinked / HandleCompleted / HandleAbandoned / HandleRetired` 只有一个
   writer（`HandleController`），否则 fork 路径、completion 路径、cancel 路径各知一部分顺序，
   谁也看不见别人是否同意（EXEC-009 注释）。
6. **级联取消必须完成后才宣告父 abort**。`AbortChildren` 是异步物理效果；调用后丢弃 Task 会让
   父 `TurnAborted` terminal / teardown 抢先完成。Companion Blogger 于是可能仍在 provider flight 中，
   是否被真正 interrupt 取决于调度时序，形成同一输入时好时坏的竞态。
7. **停止当前 attempt ≠ 取消 logical session**。Loop/NeedHelp/Finality/Fission 等内部控制只可能收束
   managed sub-session 的当前 physical attempt；它们不能借 `AbortSession` 获得 parent-cancel 权限。
   user-facing/root 没有内部 interrupt 权限：除 Host 已观测到外部用户主动中断外，插件不得主动
   interrupt root。否则一次局部控制动作会同时杀掉 Manager 与全部 coder，随后 durable handle 仍是
   `Active`，horizon 又会把已经死亡的 child 报成仍在工作。
8. **每个内部 attempt interrupt 必须同时拥有 successor**。物理 abort 本身不是业务终态：Loop 必须
   进入 AABB，NeedHelp 必须在 fresh idle 后进入 assistance，Fission/Reviewer cleanup 必须由各自已存在
   的 replacement/finality owner 接管；没有 successor 的 invariant/fail-closed stop 必须转成明确
   `Failed` terminal，使 fork handle 完成并唤醒 parent join。禁止“abort 成功 → 无 recovery、无 terminal、
   无 parent wake”的 orphan attempt。若 Host abort 请求失败，任何预先 arm 的 cause/claim 必须立即撤销，
   不得污染下一 attempt。

## RED 是什么样

```text
RED = 同一 logical owner 可得到两个活跃 replacement，或 restart/cancel 后 ownership 无法收敛。
```

具体症状：

- 同一 `(ReuseScopeId, role)` 出现两个 live dedicated Session → RED。
- restart 后同一 handle id 绑定到不同 child session，或同一 child 恢复成两种 lifecycle → RED。
- `consume` 后 restart 又把同一条 completion 投递一次 → RED（retire tombstone 缺失）。
- 父取消后子仍在运行 / 已 Abandoned 的 handle 被 join 消费 → RED。
- 父 `TurnAborted` 已对外完成，但 Blogger/其它 running child 的 `AbortSession` 仍未完成 → RED。
- 内部 loop/needhelp/tool 收束 interrupt user-facing/root，或 attempt-only stop 级联 abort descendants → RED。
- 内部 attempt interrupt 后既没有 AABB/assistance/replacement successor，也没有 `Failed` terminal + parent wake → RED。
- abort transport 失败后 Loop/NeedHelp cause 仍 armed，下一次无关 abort 被误分类 → RED。
- child 已被 parent abort 物理停止，但 durable handle 仍 `Active`，导致 horizon 报 “still away” → RED。
- Hidden（HostOwnedHidden）handle 泄漏进父的 list/join/guard/恢复 → RED（EXEC-014 回归：
  Distiller child 泄漏会阻塞 caller 的 suicide）。

## 边界（DOES NOT OWN）

- 什么 session kind 存在（`session-ontology` 拥有分类；`AttachmentKind` 增删不属本包）。
- delegation 的业务含义 / SyncDelegate 的 batch / canonical / serialization（→ `delegation`）。
- participant identity（→ `participant-identity`）。
- generic crash reconciliation（→ `crash-reconciliation`）；本包只定义 session-specific 合法恢复结果。
- Host 的具体 session API（→ `host-boundary`）。
- 假 completion 补偿的 outcome 分型（→ `effect-accounting`；EXEC-021/022）——本包拥有的是
  handle 状态机对补偿事实的拒绝行为（rejectFalseCompletion），不是「假 abort ≠ 成功」的判定本身。

## Independent Change Test

把当前 runtime registry 换成 durable locator + Host lookup，而不改变 session ontology /
delegation 的 WHAT —— 生命周期合同不动，机制可换（boundary card）。

# WHY —— effect-accounting

## 一句话

**Requested ≠ Accepted，unknown outcome ≠ not happened，unknown outcome ≠ success。**
外部效果（发 Prompt、建 worktree、publish、写 Todo checkpoint）的「请求了」「可能已经
发生了」「系统确认发生了」是三个不同事实；压成一个 bool 会在中断窗口造成**重复
effect** 或**虚假成功**。

## 为什么必须独立存在

`docs/why/persist.md` 与 `docs/what/persist.md`（PERSIST-009）反复确认同一个失败模式：
**内存记账「好像做了」会在崩溃后无法按效果身份核对。**

外部世界的 effect 与内部 event 有本质区别：event 一旦 append 就是事实；effect 是
**对世界的一次请求**，它的结局由世界决定，可能在我们确认之前就发生了。因此：

- 崩溃窗口里，`Requested` 已 durable、`Accepted` 还没写 → 结局**未知**。此时绝不能
  假装「未发生」重发（重复 effect），也不能假装「成功」（虚假成功）。
- 只有**先核对物理效果身份**（worktree 存在吗？ref 前进了吗？provider 收到了吗？），
  证明效果不存在、且该效果的合同允许幂等重试，才能重试。

这个失败 meaning 与「event 怎么存」无关——`durable-events` 只保证 append 的机械面；
「未知结局意味着什么、什么时候能重试」是独立的语义，跨 Prompt、Git publish、worktree、
repository transaction、Todo checkpoint 共享。

## 三个不可退让的支柱

1. **分型。** 每个外部效果有两类 typed durable fact：Request/Claim（意图）与
   Accepted/Created/Published（已确认）。没有「一个 status 字段」——0.5.1 的通用
   `DurableEffectRequested/Accepted` union 已被 typed facts 取代（拒绝 decode）。
2. **先记账后行动。** durable intent 先于权威内存状态更新，也先于物理 effect：
   `WorktreeCreateRequested` 先于 `git worktree add`，`TodoWritePrepared` 先于 provider
   调用。崩溃后重放只能看到「已经请求过」。
3. **Accepted 不可逆。** Accepted/Created/Published 不折回 Requested；重复 acceptance
   幂等。错误事实用新事实纠正，不 rewrite 旧事实（`durable-events` append-only 的自然
   推论，这里是它在 effect 层的语义）。

## 失败模式（RED 长什么样）

- Requested-only 被当成「未发生」→ 盲重发 → **重复 effect**（两封 prompt、两个 worktree、
  两次 publish）。
- Requested-only 被当成「成功」→ 崩溃后恢复跳过核对 → **虚假成功**。
- Accepted 被折回 Requested（重放 retry 重写旧事实）→ 已完成的 effect 被撤销。
- aborted 被当成 agent 终态 → 恢复/fallback 走错分支（EXEC-020 的 false finality）。
- 写盘失败后假装 committed → 内存看见无证据的未来。
- 先执行后记账 → 崩溃窗口里 effect 发生了但系统不知道，无法 reconcile。

## 被拒方案（考古）

| 方案 | 为什么拒 |
|---|---|
| 内存「好像做了」记账 | 崩溃后无法按效果身份核对（PERSIST-009 拒绝面） |
| 通用 `DurableEffectRequested/Accepted` union | 一个通用 DU 无法区分效果类型与各自的 reconcile 规则；0.5.1 已弃，decode 拒绝（FactCodec pre050 marker） |
| unknown 一律自动重试 | 未知 ≠ 未发生；未核对物理证据的重试是重复 effect 的配方（PERSIST-009） |
| ABORTED 当 agent 终态 | 取消是控制面不是业务结果；把 abort 洗成终态让恢复/fallback 走错分支（EXEC-020/021/022） |
| CommitUnknown 永久无法确定 | 提交结局由 canonical root 判定（`durable-events` 006）；本包承接「未知后的政策」 |
| Prompt 盲重发 | at-most-one：按 PromptKey 检索；policy 归 `dispatch-protocol`，本包钉「先证后重试」律 |

## 与相邻包的边界（谁不归我）

- **`durable-events`**：event 的 append/CAS/commit witness。本包消费它，但「怎么写盘」
  不归本包。
- **`dispatch-protocol`**：Prompt 的 PromptKey、claim lifecycle、no-blind-resend policy。
  本包拥有通用的 Requested/Accepted 分型律；Prompt 特有政策归 dispatch。
- **`change-integration`**：Git publish/worktree/repository transaction 的编排。
  本包拥有其中的 effect 记账律（PublishClaimed 三分支、Worktree Requested/Created）。
- **`crash-reconciliation`**：进程中断后如何从 durable facts + 物理观察重入普通程序
  （P0 §十 recovery 侧）；本包拥有其中的 aborted≠terminal / false-finality 半边。
- **`obligation-ledger`**：Todo checkpoint 的义务语义；本包拥有
  `TodoWritePrepared → TodoWriteAccepted` 的 effect 身份律。

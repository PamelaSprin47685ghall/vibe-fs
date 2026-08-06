# Fallback — 理由

AABB 要解决的是：单模型持续失败时，在**不重选 Authority** 的前提下换 Peer 再试，同时防止无限自动烧钱。

cursor 推进收敛到单一 Controller，消灭「retry 事件改一次、continuation 再改一次」的双写；同一失败 attempt 静默推进两次会让预算与侧边全部失真。

不观察 Host Attempt 序号：那是传输层计数，与领域连续失败不是同一量纲；混用会在 Host 重置 Attempt 时错误清零或错误耗尽预算。

armed 必须合取「紧邻失败」与奇数槽：仅看 Offset 奇偶会在成功停在奇数 Offset 后每轮都压缩，把历史碾到预算地板。

## 备选与被拒

**Offset 表示：byte/int 计数 vs DU，以及反序列化异常处理。** 拒 byte：0–255 皆可构造，`side` 对非法字节无分支，非法态在类型层就能溜进来。拒绝在 `ofByte` 反序列化非法字节时抛出 `invalidOp` 异常：持久化数据损坏是可预见异常，抛出异常会将数据校验失败变为程序事故并破坏运行时单链。选择 `Result<FallbackOffset, string>` 返回 Error，使 Journal fold 能捕获非法 envelope 并干净拒绝，触发 Fail-Closed Reconcile。

**`armedByFailure` 物理标志 vs 持久化 Offset 奇偶。** 拒绝将 `armed` 标记写盘或纯粹依据持久化的奇数 Offset 判定：若上次主请求成功，Offset 停在奇数，仅看 Offset 会导致后续请求在未发生任何失败的情况下错误触发 squash，破坏“两次 squash 之间必须隔一次真实失败”的铁律。选择内存局部变量 `armedByFailure`：仅在紧邻物理 attempt 失败推进时置 `true`，崩溃后归零（安全侧 Fail-Closed）。

**成功写归零事实 vs 成功不写。** 拒写：多一个 `FallbackCursorAdvanced` 变体会引入第二写入口（VERIFY-005 单一写入口），且归零可从 Host snapshot 的 Completed 派生。选派生：cursor 事实只记录「失败推进」这一物理真实事件，成功态是积分结果不是事件。

**侧循环判死 vs 预算判死。** 拒侧上限：换侧是合法恢复策略，循环本身不构成错误。真正要防的是无限烧钱，故判死收敛到有界预算（AutoRecoveryBudget=12）落 `FallbackExhausted`；侧循环保持无界。

**Host Attempt 计数 vs 领域连续失败计数。** 拒混用：Attempt 是传输层序号，可被 Host 重置/重复，与「连续失败」不同量纲，混用会在重启时错误清零或错误耗尽。领域计数只在确认失败的 `ProviderRunIdentity` 上推进（FALLBACK-010）。

**预算固定 vs 动态。** 拒动态：按模型/上下文调阈值不可测且造特例森林（同 CTX-001 精神）。固定有限正整数，必要时配置。


# Fallback — 理由

AABB 要解决的是：单模型持续失败时，在**不重选 Authority** 的前提下换 Peer 再试，同时防止无限自动烧钱。

cursor 推进收敛到单一 Controller，消灭「retry 事件改一次、continuation 再改一次」的双写；同一失败 attempt 静默推进两次会让预算与侧边全部失真。

不观察 Host Attempt 序号：那是传输层计数，与领域连续失败不是同一量纲；混用会在 Host 重置 Attempt 时错误清零或错误耗尽预算。

armed 必须合取「紧邻失败」与奇数槽：仅看 Offset 奇偶会在成功停在奇数 Offset 后每轮都压缩，把历史碾到预算地板。

## 备选与被拒

**Offset 表示：byte/int 计数 vs DU。** 拒 byte：0–255 皆可构造，`side` 对非法字节无分支，非法态在类型层就能溜进来，只能靠运行 fold 兜底。选 DU（Fork0..Fork3）：0–3 即类型，非法态编译期造不出；byte 只在序列化边界出现（评审修正）。

**成功写归零事实 vs 成功不写。** 拒写：多一个 `FallbackCursorAdvanced` 变体会引入第二写入口（VERIFY-005 单一写入口），且归零可从 Host snapshot 的 Completed 派生。选派生：cursor 事实只记录「失败推进」这一物理真实事件，成功态是积分结果不是事件。

**侧循环判死 vs 预算判死。** 拒侧上限：换侧是合法恢复策略，循环本身不构成错误。真正要防的是无限烧钱，故判死收敛到有界预算（AutoRecoveryBudget=12）落 `FallbackExhausted`；侧循环保持无界。

**Host Attempt 计数 vs 领域连续失败计数。** 拒混用：Attempt 是传输层序号，可被 Host 重置/重复，与「连续失败」不同量纲，混用会在重启时错误清零或错误耗尽。领域计数只在确认失败的 `ProviderRunIdentity` 上推进（FALLBACK-010）。

**预算固定 vs 动态。** 拒动态：按模型/上下文调阈值不可测且造特例森林（同 CTX-001 精神）。固定有限正整数，必要时配置。


# Fallback — 理由

AABB 要解决的是：单模型持续失败时，在**不重选 Authority** 的前提下换 Peer 再试，同时防止无限自动烧钱。

cursor 推进收敛到单一 Controller，消灭「retry 事件改一次、continuation 再改一次」的双写；同一失败 attempt 静默推进两次会让预算与侧边全部失真。

不观察 Host Attempt 序号：那是传输层计数，与领域连续失败不是同一量纲；混用会在 Host 重置 Attempt 时错误清零或错误耗尽预算。

armed 必须合取「紧邻失败」与奇数槽：仅看 Offset 奇偶会在成功停在奇数 Offset 后每轮都压缩，把历史碾到预算地板。

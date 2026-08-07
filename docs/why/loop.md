# LOOP — 理由

退化循环会污染 transcript 尾部并推迟有效恢复槽。传感器是 ARCH-002 的定点例外：只提取字符流更新检测器，不把 part.delta 拼成业务事实。

强杀后桥接 Failed 并复用 AABB，避免第二套自动恢复预算。固定参数（统一按代码阈值）拒绝「按角色/自然语言动态改阈值」——后者不可测且制造特例森林。

## 备选与被拒

**可变状态封装：公开可变字段 vs 私有封装与生命周期隔离。** 拒绝在 `LoopDetector` 中公开 `mutable Step`、`Value: float[][]` 等可变数组字段：公开可变数组极易被传出 attempt 边界或被诊断日志并发读取，破坏 O(1) 无锁假设。选择严格私有封装（`private` 类型），仅对外提供只读快照与 `feed` 接口；检测器严格绑定至单次 `ProviderRunIdentity`，Turn 结束立即释放。

**恢复桥接：独立 Loop 恢复机制 vs 桥接 FallbackController。** 拒绝为循环强杀另造一套重试计数器与恢复动作：这会造成第二状态机并破坏 FALLBACK-003 唯一写入口。选择桥接：保留 provider-turn `TurnAborted` 事实；`LoopKillArmed` 命中后调用与普通 provider failure 共用的恢复函数，驱动 `FallbackController.recordConfirmedFailure`，复用统一的 AABB cursor 与预算。

**检测：滑动窗口计数/精确重复 vs 4-gram + 指数核。** 拒窗口计数：需要历史窗，跨窗遗忘生硬。拒绝确重复表：对无限流不可行。选慢指数核逼近 Zipf 型无限历史：每 4-gram O(K²) 递推，固定内存 O(HASH_BUCKETS·K)，`N_eff` 收敛于「相当于多少个不同 4-gram」的物理量（LOOP-003/004）。

**记忆结构：无限 Map vs 固定哈希桶。** 拒无限 Map：随流长度增长。接受哈希桶碰撞（HHI 略偏高、更敏感），禁止为「更准」改回无限结构——换的是可预测内存。

**冷启动：无先验 vs 正常代码先验。** 拒 `MIN_NGRAMS` 预热窗：冷启动期无判定盲区。选 LOOP-004 的正常代码先验，无罪推定，真实输出按核淡出先验。滤空白后正常代码多样性显著更高，先验可信。

**阈值：角色/语言动态 vs 固定。** 拒动态：不可测。LOOP-004 在 `N_eff` 空间选择固定中点而非 HHI 中点，因为判定物理量是 N_eff。

**并发：无锁依赖**见 how/loop.md 并发模型：每 attempt 单事件泵串行投递 delta，检测器不外泄。

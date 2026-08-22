# context-compression — WHY

Provider 会话历史可能超过模型的可用上下文窗口。如果压缩机制依赖主动预测窗口容量、将未经验证的候选前缀预先提交再回滚、或按照错误文本猜测失败原因，就会将模型容量猜测与未发生的世界状态固化为系统事实，进而破坏 KV-cache 与前缀稳定性。

**context-compression 保证：何时以及哪些历史有资格被语义替换，严格由真实失败信号与已证明的证据边界决定。**

## 核心不变量与张力

- **失败驱动 vs 预测式压缩**：严禁在请求前主动估算上下文窗口或预测溢出；真实失败是唯一合法的恢复触发信号。
- **证据证明 vs 投机提交**：候选替换前缀在 probe 成功前绝非事实；失败的 probe 不产生任何持久状态，无需回滚。
- **覆盖边界 vs 辅助残留**：X→Y 压缩冷边界发生后，旧 horizon 的辅助注入（如 guideline、tip、grounding read）可见性彻底归零，防止历史噪音在重锚后重新膨胀。
- **宪章 Opening vs 工作历史**：只有真实 Opening 消息是不可替换宪章；Manager 的 pre-T1 规划材料与 post-T1 普通历史遵循同一压缩规则，不能因 planning stage 获得额外 X 常驻权。
- **语义账本 vs 摘要替换**：承载 `todowrite` 的 Host 回合是 Manager 当前义务账本的原始证据；即使其前后历史已被 Y 覆盖，该回合仍必须以 X 原文留在 provider context。
- **durable open ownership vs 进程并发**：`BloggerRequestMaterialized` 是 Blogger 当前请求所有权的 durable 事实；同一 Blogger 的 materialize / bind / abandon 命令必须经 process-local admission 串行，但 admission 只拥有物理并发，不得取代 durable open request。live flight 允许同 RequestId 刷新，严禁不同 RequestId 覆盖。

## 违反边界的失败意义

- 未提交的候选前缀污染真实历史（如预先写入事实后试图回滚）。
- 压缩覆盖不完整证据（如将半 turn 冒充为完整 prefix 证明，或改写侵吞真实 Opening / `todowrite` 原始回合）。
- 依据模型窗口大小或上下文比例主动触发改写。
- 重锚后将已退休的辅助注入重新灌回 provider context。
- recovery 主请求已经成功却把 fallback cursor 留在 A′/B′，使下一次真实 overflow 先跨到另一侧普通槽并再次 overflow，直到第二次失败才触发压缩。
- 两个并发 materialize 都从旧 snapshot 推导“可开新 request”，随后第二个 durable fact 被 canonical fold 拒绝并触发 semantic cut；或不同 RequestId 静默覆盖 live flight，使 durable ownership 与物理执行分叉。

## DEPENDS ON

- `semantic-trace`
- `provider-projection`

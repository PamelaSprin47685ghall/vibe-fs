# guidance-delivery — WHY

## 领域动力与核心张力

「诊断是否成立」与「何时以何种形式向 Main 呈现处置手册」属于两个完全不同的领域问题。前者由 `behavior-diagnosis` 依据证据法则裁决，而后者必须解决：**如何将已成立的诊断转化为当前 horizon 内可恢复、不引发上下文无界膨胀且不伪造新事件的交付事实。**

工程监督系统在交付层面极易陷入两个极端：
1. **重复投递全文**：每轮均向 Main 灌入完整的 `main.md`，导致上下文爆炸且无法区分已交付与未交付状态；
2. **重锚后信息丢失或产生悬空引用**：上下文压缩（compaction）或重锚（reanchor）将全文移出 horizon 后，若继续仅发送简写身份（`tip: <name>`），Main 将面临无处置正文可用的悬空引用；若重新发送全文，又极易被误记为一次新的病理发生。

`guidance-delivery` 的核心存在理由是确立交付前沿（Frontier）与语义覆盖（Coverage）的双轴正交分离模型，确保重复交付自动去重、重锚后正确执行语义恢复，且历史投递内容严格按原字节冻结。

## 核心不变量

1. **两轴正交分离**：
   - `TipDeliveryFrontier`：记录哪些诊断事件已向 Main 交付，基于 occurrence 单调递增，ContextReanchored 时不重置；
   - `TipSemanticCoverage`：记录哪些规则的处置全文当前仍可在 provider horizon 内恢复，基于 TipName，ContextReanchored 时可清空重导。
2. **首次 Full 与重复 IdentityOnly**：未交付或覆盖丢失时给出 Full 全文并推进 Frontier；覆盖范围内重复交付仅呈现紧凑的 `tip: <name>` 身份，不重复全文，不推进 Frontier。
3. **语义恢复不造假**：重锚后因覆盖丢失而再次给出 Full 全文属于语义恢复（semantic restoration），不作为新的诊断 occurrence 记录。
4. **受众隔离与权限中立**：检测语料（`enforcer.md`）仅供 Blogger 使用，处置手册（`main.md`）仅供 Main 交付；交付通过合成的 `skill` 工具对投影到 horizon，不注入伪造的用户消息，不派生新的 Interaction Authority。
5. **历史字节确定性冻结**：每个已投递的 auto-injected guideline 按实际 wire 字节持久化，历史重放时完全复现原字节，不随本地规则库版本的演进而改写。

## 边界与失效模式

- **不负责诊断成立与否**：诊断证据与规则命名空间归 `behavior-diagnosis`。
- **不负责通用信息准入律**：horizon 准入的一般法则归 `participant-horizon`。
- **不负责 Authority 派生**：交互权限的创建与流转归 `interaction-authority`。

**失效表现（RED）**：
- Guidance 无界重复发送全文；
- Reanchor 后发送悬空的 IdentityOnly，导致 Main 无法获知处置正文；
- 上下文重锚后的语义恢复被误记为新的病理 occurrence；
- 交付过程注入伪造的用户消息或篡改了历史投递字节。

## DEPENDS ON

`guidance-delivery → behavior-diagnosis, participant-horizon, durable-events, concern-routing`

# behavior-diagnosis — HOW

## 架构机制与核心模型

### 1. 规则库装载与合流模型

1. **Catalog 结构与合流**：
   - 规则目录由 built-in 资源与经校验的 `InstitutionalRuleBorn` durable 事件动态合流，生成统一的 `EnforcerCatalog`。
   - 纯函数 `validate` 校验规则唯一性、连续派生 LexicalOrder `1..N` 及双语正文非空性。
   - `EnforcerCatalogResource.loadFor` 静态调用 `EnforcerCatalog.validate 1 rules`，并传播非法 catalog 的失败。resource shard 显式依赖 catalog 与 provider-language；不得动态查找编译模块或在校验器缺失时伪造成功结果。
   - `resolveByField` 执行精确匹配与 Levenshtein 编辑距离归一化，并列时按 LexicalOrder 决胜。

2. **System Prompt 确定性合成**：
   - 按 LexicalOrder 拼接 `# Enforcer Rulebook` 与全部规则正文，保证相同 catalog 输入生成完全一致的 prompt 字节。
   - `main.md` 补救手册不进入 Blogger system prompt，保持检测与补救的受众隔离。

### 2. Cycle 校验、提交与投影

1. **基数门禁与解码**：
   - 原始 assistant 步骤中 `chronicle` 调用数必须精确为 1。0 次或 2+ 次直接转入协议修复。
   - 解码器解析 `entry`、`tip` 与可选 `evidence`，验证文本非空；`EnforcerCycle.validateContentBounds` 唯一拥有内容大小阈值、字节计数结果、typed rejection 与 text-first 拒绝顺序（文本 ≤ 512 KiB，证据 ≤ 128 KiB）。Decode 与 JS Surface 只注入 canonical UTF-8 `byteCount` 并消费同一纯 decision。
   - 若无活跃 Blogger cycle，产生类型化 `NoLiveCycle` 结果并清理过时会话。

2. **原子提交与 Coverage 出生门**：
   - `BloggerMainContext.mainContextFromChunk` 单独拥有出生门及 context 构造；原 `Enforcer/Host.fs(.fsi)` 已退役，cursor 映射与 prefix digest helper 随唯一消费者迁入 MainContext，不在 Recovery 保留副本或转发器。`fromProjection` 直接静态调用，不以动态模块缺失伪装为没有新材料。
   - `BlogSurface.coverageBirth` 通过显式 trace sequences 调用真实 `XTraceProjection.applyPart` 和上述生产函数，不再复制不等式作为 oracle。出生门测试覆盖非推进、无法映射、严格推进及同一 turn 内推进保留原 prefix digest；无法映射的正序列反例在旧 Surface 返回成功，新实现拒绝。
   - 已删除源码形状断言及未经过真实 writer 的三项伪 precheck 测试；这些出生门证据不证明提交阶段的 cutoff／epoch 不一致恢复与弃置，也不能据此宣布 behavior-diagnosis-013 的提交前校验全部已证明。
   - 提交前校验待覆盖序列严格单调递增，且 staged cursor/cutoff/epoch 与当前投影一致。
   - 写入日志与证据 blob 后，原子追加 `BlogObservationCommitted` 事件，同步推进 coverage 与日志记录。
   - 提交后派生单一 RecentTip，在投影中维护容量为 8 的有序滑动窗口。

3. **配对与压缩**：
   - `pairTipsAndFrames` 将 tips 与 frame digests 组合为配对观察单元，提供自洽的历史诊断事实。
   - squash 操作以 1:1 比例协同折叠 frames 并共同裁剪 RecentTips，不产生新事件。

### 3. 有界协议修复与 Life 冻结

1. **有界 Nudge 与 AABB 机制**：
   - 首次无效 terminal 由 `SessionIdle` 触发专用 Nudge，每个 RequestId 仅限一次。
   - 再次无效必须由新的 ProviderRunIdentity 证明后才进入 AABB；同一 terminal 的重复 Nudge admission 返回 typed `AlreadyAdmitted` 并等待，不得作为失败推进恢复。
   - send admission 使用 typed outcome 区分 `Sent / AlreadyAdmitted / Superseded / Retired / NotSent / Failed`；`NotSent` 仅表示 acceptance 前确定拒绝，可安全释放 exact gate reservation，禁止通过错误字符串反解析幂等或重试状态。
   - 恢复重判仅承认包含 `chronicle` 的 completed 状态，杜绝模糊猜测。

2. **RulebookRevision 冻结**：
   - Blogger life 在创建时固定其绑定的 `RulebookRevision`，保持 system prompt、工具枚举与解码器版本完全一致。
   - 新规则产生仅更新全局最新 revision，当前活跃 cycle 继续在原 revision 下完成，待下一 fresh life 生效。


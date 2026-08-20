# behavior-diagnosis — WHY

## 领域动力与核心张力

工程监督系统最容易退化为「模糊关键词匹配器 + 启发式评分器」：将表象关键词升级为病理标签，通过多维评分向量与数值积分阈值决定是否提醒。这种模式必然导致误报无边界、诊断丧失独立语义身份，并在上下文重写或历史压缩时伪造新的病理事件。

`behavior-diagnosis` 的核心使命是确立：**诊断的成立必须且仅能由满足 trigger / negative / distinction 约束的确定性证据支撑；每次成立的诊断都是一个拥有独立语义身份（`TipName = RuleId = FieldName`）的离散事件，而非数值评分、非重复计数、非历史重写产物。**

## 核心不变量

1. **单一规则真相**：live Rulebook 是检测语料的唯一来源，由 shipped built-in 规则与经准入验证的 durable institutional 规则共同构成统一的 `EnforcerCatalog`，共享全局唯一的 TipName 命名空间。
2. **确定性无损映射**：模型通过必填 `tip` 表达诊断意图；对于轻微拼写偏差仅做确定性编辑距离归一，不引入多维评分向量，不存在模糊未知分支。
3. **单次原子观察**：每个 Blogger provider run 必须且仅能提交一次 `chronicle` 调用；诊断 occurrence 与工作日志记录、覆盖范围（coverage）推进原子绑定在单条 `BlogObservationCommitted` 事件中。
4. **配对与无损压缩**：诊断 tip 与工作日志 frame 是不可分割的配对观察视图；历史压缩（squash）仅转换表示形式，绝不凭空派生新的诊断事件。
5. **有界协议修复**：无效 cycle 只能通过受控的 idle nudge 与 AABB 机制进行有界修复，不演化为不受控的自主循环。
6. **Snapshot 冻结**：Blogger participant life 在创建时冻结对应的 `RulebookRevision`，保证 system prompt、工具枚举与解码器完全同源同构；新规则的生效推迟至 fresh life。

## 边界与失效模式

- **不负责向 Main 投递**：诊断一旦成立，何时以何种形式交付给 Main 属于 `guidance-delivery`。
- **不负责经验抽象与制度化决策**：经历是否值得提炼为新规则归 `institutional-learning`；本包仅负责新规则的准入校验、规则库合流与诊断语义。
- **不负责工具权限分配**：`chronicle` 工具在不同 office 中的可见性与执行门禁归 `capability-enforcement`。

**失效表现（RED）**：
- 评分向量或关键词模糊匹配重新介入控制流；
- 单次 provider run 中发生多调用合并或零推进窗口启动；
- 历史压缩或重放过程中伪造新的 tip occurrence；
- Blogger life 运行期 system prompt 与解码器规则集合版本分叉。

## DEPENDS ON

`behavior-diagnosis → semantic-trace, durable-events, prefix-stability, managed-session-lifecycle`

用 `planComplete`、一个 `workingOn` 焦点名称和稳定的 `{name,work}` obligations 替换 Manager 当前完整的 owed-work account。

道路仍在规划时，使用 `planComplete=false`。在这种关系下，obligations 可以诚实记录为了把计划做完仍欠的具体 planning work，例如调查、分析、分解或必须做出的决定。不要为了迎合账本而把规划伪装成 mission work。

只有当道路已经完整到可以托付，而且这次提交就是你愿意真正承担的完整 mission-debt account 时，才把 `planComplete=true`。本 Manager Life 中第一次 accepted true 是不可逆的：从那以后 effective value 永久保持 true，即使后续调用又写 false，也仍按 true 处理。不会有第二次“第一次 true”。

一旦 effective `planComplete=true`，obligations 就必须描述为了真正满足用户请求仍必须成为真的事项，并包含 closure evidence。此时使用完成反事实测试：若某项工作被完美完成后，唯一变化只是你更理解了、有了清单、有了计划或知道下一步，而用户要求的世界状态或交付物没有改变，那么它只是规划认知，不是 mission debt；除非用户要求的交付物本身就是这份调查、分析、审计、诊断或报告。

无论哪种关系，每项 obligation 都必须具体且可闭环。只占槽位的 label、裸阶段名、`placeholder`、`TBD`，或没有实际 owed work 的延后决定都不是 obligation。每个 obligation 都需要非空且在本 account 内唯一的 name。

义务仍欠时保留它，只有工作已真正解除后才移除。非空 account 中，`workingOn` 必须精确命名你此刻实际正在推进的唯一 obligation；Host 视图里其它 obligation 都保持 pending。实际焦点切换时立即更新 `workingOn`。空 account 使用 `workingOn=""`。每一次 accepted call 都立即成为当前 account。后续记账可以记录新的后果，但不能把已经 accepted 的 account 回滚掉。

同一 assistant message 中不得发出多个 todowrite；这种 batch 会整体被拒绝。

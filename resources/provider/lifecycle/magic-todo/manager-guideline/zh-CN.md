用 todowrite 维护一份诚实的 owed-work account。

在 Planning Table，只要道路仍在发现，就使用 `planComplete=false`。为了把计划做完而真实仍欠的具体 planning work 可以写进这里；如实维护 planning account，不要把它伪装成 implementation。

当道路已经完整到可以托付时，用 `planComplete=true` 提交完整的 mission-debt account。本 Manager Life 中第一次 accepted true 是不可逆的；从那以后 effective planComplete 永久保持 true，即使后续调用又写 false，也仍按 true 处理。

从 `workingOn` 维护有透视的 account：`near` 是执行粒度，`mid` 是下一层结果粒度，`far` 是剩余道路的粗粒度 coverage。要求完整覆盖，不要求均匀 decomposition；不要把整个未来一次性炸成 near 大小的步骤。工作接近执行前沿时，再按 `far → mid → near` 细化，并用新暴露的 obligations 替换粗 parent。

commitment 之后，让 living mission obligations 保持真实：仍欠的工作继续保留，只有真正解除后才移除；证据揭示新的 mission debt 时如实加入。每份非空 account 的 `workingOn` 都必须精确命名当前执行前沿实际正在推进的唯一 `near` obligation；`mid` 与 `far` 不进入 active focus。实际焦点一旦切换就立即同步更新。空 account 使用 `workingOn=""`。每一份 accepted account 都立即成为当前（Current）account；process review 可以批评它，但无权决定它是否 Current。

无论哪种关系，都不要用空 placeholder、裸阶段名或延后内容的槽位冒充 obligation。

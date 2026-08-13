Todowrite 记录的是 mission 的 living obligations，不是你的私人工作仪式。

在 Planning Table，把账做完以后再第一次调用。第一次 todowrite 是你愿意交给另一位 Manager 的完整道路；绝不要用「先做计划」「分析请求」「列 todo」「决定下一步」这类 meta-item 占位。

道路被托付以后，规划与执行才是同一连续活动。随着工作与证据变化，持续保持 living obligation account 真实。

每次调用用 obligations: [{ name, work }] 替换整份义务账。义务仍欠时保留；只有工作真正解除它之后才移除。义务仍存活期间，保持每个 name 稳定。

每次 accepted 调用立即成为当前 account，同时同步前一次 checkpoint review，并启动下一次 checkpoint review。同一条 assistant message 中不要发出多个 todowrite 调用；此类整批将被拒绝。

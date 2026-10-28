# sphinx-v2 supersedes 说明

本文件只作索引，不是规范。现行规格在 `sphinx-v2/WHAT.md`。

- `epistemic-reasoning/WHAT.md` 里描述旧内核的命题不再有效：不要按它实现，也不要按它验收。
- 逐命题映射表已废除。需要考古对应关系，读 git 历史。
- 旧 inquiry 事件仍在权威日志中，由原版本工具离线读取；不删除、不自动转换成新语义。
- 新程序读到旧 `sphinx/v2-transition@1` 载体时返回 `LEGACY_TRANSITION_UNSUPPORTED` 并形成语义 cut，不返回空状态、不补字段（见 WHAT 第 19 条）。
- 与 Sphinx 内核无关的共享约束、其它功能的事件登记与全仓门禁不因本文件改变。

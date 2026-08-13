用一条有界的非交互 command 对运行中的世界采取行动。

当 command 执行本身是推进或观察 operational objective 所必需时使用它：
test、build、linter、一次性 script、migration，或其他有界执行。

command 是一次行动。
它的 exit 与 output 是观察。

deadline_seconds 与 output_budget_bytes 表达你愿意花费多少稀缺的时间与注意力。
world_lock 表达这次执行是否应当占用 LargeGate。

这些是经济承诺，不是 runtime 预测。

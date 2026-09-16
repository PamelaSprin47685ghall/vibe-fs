用一条有界的非交互 command 对运行中的世界采取行动。

当 command 执行本身是推进或观察 operational objective 所必需时使用它：
test、build、linter、一次性 script、migration，或其他有界执行。

command 是一次行动。
它的 exit 与 output 是观察。

本次执行由 DevOps 负责。失败可能是直接非架构级修复的起点，不是任务的终点。
保留有效测试，在最后修改后重新运行。不把修复派给另一名代理，也不把此工具当作 Engineer 的命令代理。

小输出原样返回；超额输出保留有界原文尾部，明确声明截断，不经过模型摘要。
前面的错误可能不在其中。进程结果单独读取，不能只凭文字片段判断退出、超时、取消或终结。

deadline_seconds 与 output_budget_bytes 表达你愿意花费多少稀缺的时间与注意力。
world_lock 表达这次执行是否应当占用 LargeGate。

这些是经济承诺，不是 runtime 预测。

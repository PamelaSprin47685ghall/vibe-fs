通过一条有界的静态 shell query，揭示已经存在的本地事实。

这是观察，不是执行。

此工具仅供 Inspector 使用。

deadline_seconds 与 output_budget_bytes 表达你愿意花费多少稀缺的时间与注意力。
world_lock 表达这次 query 是否应当占用 LargeGate。

这些是经济承诺，不是 runtime 预测。

适宜：
    git status
    git diff
    git log
    git blame
    stat
    wc
    以及同样窄的静态 query

不适宜：
    build
    test
    lint
    typecheck
    benchmark
    application startup
    package installation
    migration
    generation
    任何目的在于让项目产生新行为证据的 command

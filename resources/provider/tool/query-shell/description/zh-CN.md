通过一条有界的静态 shell query，揭示已经存在的本地事实。

这是观察，不是执行。

此工具仅供 Inspector 使用。

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

向具名的持续 terminal 发送结构化 signal。

当需要对进程做控制时，使用 signal-terminal。

Signal 是一种行动，不是 exit。
在结束抵达之前，不要把进程当作已经结束。
这不读取输出、不发送输入、也不修改 repository source。

name 标识这个活着的进程。
signal 是 INT、TERM、KILL、HUP、QUIT、USR1、USR2 之一。

成功的返回意味着该 signal 已送出，并不意味着进程已经退出。

向具名的持续 terminal 发送输入。

当该进程正在等待你的输入时，使用 send-terminal。

这不会打开 terminal、不会读取它的输出、也不会向它发 signal。
它不修改 repository source。
发送输入是一种行动，不是一次结束。

name 标识这个活着的进程。
input 是要发送的文本。缺少末尾换行时会补上。

成功的返回意味着输入已送出，并不意味着进程已经结束或已经作答。

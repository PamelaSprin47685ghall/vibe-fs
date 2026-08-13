用一条 command 打开具名的、持续存在的交互式进程。

当交互状态本身必须跨回合保持在场时，使用 open-terminal：REPL、长驻服务、向导、
等待输入的进程。

这不是一次寻求结束的有界命令；那是 run。
它不修改 repository source。
它不发送输入、不读取输出、也不向进程发 signal。

name 是这个活着的进程借以被认出的人名。
command 是启动它的命令。
只有在先前的结束已被听见之后，这个 name 才可以再次使用。

成功的返回意味着该具名 terminal 已打开，并不意味着 operational objective 已经完成。

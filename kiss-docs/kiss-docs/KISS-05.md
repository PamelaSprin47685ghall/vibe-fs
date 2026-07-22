# Process / Executor / PTY

进程 = 资源作用域。对外甜；对内泵序与绝对 Deadline。

```
process {
    use! child = p.Spawn(command)
    return! child.RunToCompletion()
}
```

---

## 一、Command [NORMATIVE]

```
type Command =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string option
      Environment: Map<string, string> option
      Stdin: string option
      Deadline: Deadline option }   // 绝对 expiresAt；见 KISS-Tools
```

[FORBIDDEN] `TimeSpan` 作唯一 Deadline 在每层重置完整预算。  
[FORBIDDEN] 第二套 `WithDeadline` API。

入口（Tool）创建一次 `Deadline`；下层只 `remaining`。

`Deadline` 是绝对时刻，不是“每个 API 重新拥有一段时长”。例如 Tool 总预算为 30 秒，Spawn、等待退出、泵 drain、Kill 等步骤共享同一个过期时刻；任何一步耗尽都必须停止，而不是重新开始 30 秒。

命令结果至少区分：

```
    { ExitCode: int
      Stdout: string
      Stderr: string
      StdoutTruncated: bool
      StderrTruncated: bool }
```

非零退出码是外部程序事实。调用方可以根据命令语义把它视为业务失败、可重试失败或普通结果，但 Process 层不能武断地把所有非零退出都标为 transient。

---

## 二、泵序 [NORMATIVE]

[FORBIDDEN] `WriteInput → WaitForExit → ReadOutput`（管道死锁）。

[NORMATIVE]

```
Spawn 返回前：已安装 stdout/stderr 有界 pump（防极快输出在 RunToCompletion 前填满管道）
→ 若 Stdin = Some s → Write → Close stdin；None → 按协议关 stdin
→ await exit
→ await pumps
→ ProcessResult { ExitCode; Stdout; Stderr; Truncated }
```

`ExitCode <> 0` = 结果值，不默认 Transient。

### 2.1 为什么 pump 必须在 Spawn 返回前安装

子进程可能在父调用 `RunToCompletion` 前立即写出大量数据。若 `Spawn` 只返回裸进程句柄，泵延迟到后续方法，短命进程仍可能先填满管道。Spawn 的资源构造阶段必须完成 stdout/stderr reader 的安装，并把 pump task 绑定到 handle 生命周期。

### 2.2 取消与失败顺序

```
Cancellation / Deadline
→ 发送 Kill(entireProcessTree)
→ 等待进程退出（剩余预算）
→ 让 pumps 读完可见尾部或取消
→ 等待 pumps 完成
→ DisposeAsync
→ 映射为 ProcessCancelled / Timeout / ProcessFailure
```

如果 Kill 或 drain 本身失败，必须保留原始取消/超时原因与清理诊断；不能用空 catch 把资源泄漏伪装成成功。

---

## 三、异步释放 [NORMATIVE]

```
Kill(entireProcessTree)
→ 有界等退出（Deadline 剩余）
→ 完成/取消 pumps
→ DisposeAsync
→ Flow 返回
```

[FORBIDDEN] 同步 Dispose 堵线程 fire-and-forget Kill；吞尽异常无诊断；孤儿 PID。

`using` 的责任边界是创建者。调用者不能把 ProcessHandle 放入 Registry 等第三方清理，也不能在 Flow 返回后继续使用已经进入 Dispose 的 stdout/stderr task。成功、Flow Error、异常和取消四条路径都必须覆盖释放测试。

---

## 四、PTY：组合

```
ReadUntilExit / ReadUntilIdle / ReadUntilMarker
```

[FORBIDDEN] 无界 `ReadOutput()`。  
不强制 OOP 继承树。

PTY 与普通管道的差异是输出是交互式流，不代表它可以无限等待。每次读取必须选择一个终止条件：

|操作|终止条件|
|---|---|
|`ReadUntilExit`|进程退出且输出泵结束|
|`ReadUntilIdle`|一段 idle 时间内无新字节，仍受总 Deadline 限制|
|`ReadUntilMarker`|读到指定协议标记，仍受总 Deadline 限制|

Resize、WriteInput、Read 操作共享同一 CancellationToken 和绝对 Deadline。

---

## 五、私有状态

`Running | Killing | Exited` 可留实现文件。不进业务 Journal Phase。  
Process durable 事实：字段够清孤儿或第一版不写盘（KISS-04）。

---

## 六、Executor

薄封装 → Spawn + RunToCompletion。无自建 timeout 塔。

Executor 工具只负责把用户参数解码成 `Command`，再把 `ProcessResult` 编码为工具输出。它不维护进程表、不重复实现 kill、不把 shell 文本解析成第二套协议。工作目录、环境、stdin、输出上限和总 Deadline 都在边界一次确定。

---

## 七、出口

无嵌套 timeout 塔；取消收敛；Dispose 后无 PID/端口；错误不 poison 后续。

验收必须包含：

1. stdout 写满而 stderr 同时写满，命令仍能退出；
2. stdin 写完后正确关闭 stdin，避免子进程继续等 EOF；
3. 极快退出进程的输出不丢；
4. 超时杀死整个进程树；
5. 取消后无后台 pump、孤儿 PID 或开放句柄；
6. 单次输出超过上限只截断，不耗尽内存；
7. 一个命令失败后下一个 Executor 仍可运行。

---

*KISS-05 终。*

# process-execution — 为什么必须独立存在

## 不可替代的存在理由

系统里有真实的进程控制：DevOps 开终端、跑命令、发信号、读输出、等退出；还有 bounded `run`。
这些 act 作用在物理世界上。没有本包，以下事故都会发生：

1. **stdout 冒充完成**：进程没退出，只是输出看起来结束了——join 与父状态机被假完成污染。
2. **signal 冒充 exit**：发送 SIGKILL 被当成「进程已结束」，waiter 在进程真正死亡前返回。
3. **无界执行**：没有 finite hard limit，runaway 进程与无界等待不可区分。
4. **DTO 冒充物理事实**：`status`/`code`/`message` 字符串让模型猜「完成了吗」，而不是读物理 exit。
5. **取消不干净**：mid-wait cancel 卡在 exit 上，或 kill 后不等真实退出就继续。

RED 判定：stdout/transport 状态可冒充 process completion，或 participant 无法可靠区分 act、
observation、exit。此时世界 RED。

## 独立变化测试

更换 terminal backend（例如从 bun-pty 换到另一 PTY 实现），而 command/exit/cancel semantics 不变——
本包 WHAT 全部不动。反之，把 `open-terminal` 改成 `start-terminal`，语义合同不变则 WHAT 不动
（工具名 = HOW）。

## 历史失败模式（为什么现在是这个形状）

- **stdout 启发式假完成**（历史 why/execution 条款）：曾用「看起来结束了」判断 PTY 完成，假完成污染
  join 与父状态机。拒启发式：PTY completion 只信 backend `onExit`（EXEC-015）。
- **timeout flag 存在进程上**（`src/Wanxiangshu/Process/NodeProcessHost.fs` 注释）：旧 `Exit` 是
  `int * bool`，bool 表示「超时了」——但进程不知道自己是否被 deadline 等过，该 flag 是 waiter 的知识
  放在进程上，让 timeout 路径自己填造 `(-1, true)` 而不是等真实 exit。新形状 `Exit: TaskCompletionSource<int>`
  只带真实 exit code；`Kill` 不设置 `Exited`——发送 SIGKILL 不是进程结束，二者混同会让每个 waiter
  把「kill 已发」看成「exit 已见」。
- **hard limit 形同虚设**（`ProcessRequest.fs` 注释）：旧实现 clamp 到 36500 天并称其为 bound——那个
  尺度下 runaway 与无界不可区分。新 `DefaultHardLimit = 1h` 是真实有限值。
- **estimate 膨胀**（`ProcessRequest.fs` GrandRewrite）：旧实现 provider willingness ×3；现在
  `min(deadline_seconds, hard limit)` 按面值应用。
- **PTY 完成角色错乱**（`PtyTypes.fs` 注释）：旧 `PtyHandle` 持 `AgentId option` + `Role option`，
  `PtyPort.Fork` 从不传两者，于是每条 PTY completion 都报角色 `Executor`、名字 `fast-distiller`。
  新形状把 name+role 合成一个 parsed `ManagedAgent`，让这种不一致不可表示。
- **大输出无界缓冲**（`ProcessOutput.fs`）：跨输出预算必须切 spool；内存积压封顶
  （`MemoryBufferBudget`），避免跨过阈值瞬间一次性 dump 全部积压字节。

## 与相邻包的边界

- deadline 的纯代数（`effectiveDeadline`、`Deadline.remaining`、timer 分段）→ `time-capability`；
  本包只拥有「执行必须物理有界」这一使用义务。
- 大输出如何被蒸馏（`Distillation.fs`、`ToolResultBound.fs`、`LargeGate` 预算合同）→
  `output-distillation`；本包只拥有「物理捕获有界、spool 是输入」。
- office authority（谁能开终端）→ `office-capability` / `capability-enforcement`。
- 恢复时从 Journal 重入（permit 门、AwaitAgentWithPermit）→ `crash-reconciliation`。

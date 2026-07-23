# 万象阵 Squad Flow

可选应用；默认 Profile 不加载。

---

## 一、并行与合并 [NORMATIVE]

工作并行；共享目标分支 FF **串行**。

```
prepareTask: worktree 覆盖至 Publish → slave → verify → PublishVerified
runSquad: for wave → RunParallel(prepare) → MergeOrder 串行 FastForward → AcceptWave
```

`RunParallel` = KISS-01 mapBounded（Task 层、独立上下文）。  
[FORBIDDEN] 共享目标并行 FF；串行伪并行；平铺 Unfinished 打乱 DAG。  
resume：恢复 wave/DAG 后同一 `runSquad`。

Worktree：合并前保留；失败是否删 = 配置。Slave：async dispose。

HTTP = 只读投影，非第二控制路径。

### 1.1 为什么 Work 与 FF 分离

同一 wave 的任务可以在独立 worktree 中并行计算、运行测试和生成 verified result；共享目标分支却是一个有顺序的 Git ref。若每个 `runTask` 在并行 worker 内直接 `FastForward`，两个 worker 可能同时读同一 ref、分别计算可快进状态，再互相覆盖或令其中一个失败。

因此 `prepareTask` 只返回已经验证的结果，不触碰共享目标 ref。父流程以 `MergeOrder` 得到确定顺序后逐个 FF：

```
parallel: CreateWorktree → StartSlave → Work → Verify → PublishVerified
serial:   MergeOrder → FastForward → AcceptWave
```

这不是全局锁。它只是共享 Git ref 的真实顺序约束，且只存在于本次 Squad Flow 的结构化代码中。

### 1.2 Worktree 资源边界

Worktree 的作用域必须覆盖 `PublishVerified`，并根据策略覆盖到 `FastForward` 完成。过早离开 `use!` 会在结果仍需读取或合并时删除目录；过晚释放则会残留目录。

失败清理需明确区分：

|情况|默认动作|
|---|---|
|任务尚未产生结果|Kill slave，删除临时 worktree|
|已 Verify、待 FF|保留至该结果被 Accept/明确放弃|
|FF 冲突|保留诊断与 worktree，便于重试/人工检查；策略可删|
|父取消|异步 Kill、等待退出、再清理|

不允许同步 `Directory.Delete` 掩盖仍在运行的 slave。

---

## 二、删除

Scheduler/CoordinatorOps/TaskLifecycle*/Routes/Replay 框架碎片。

恢复不需要另一套 Replay 框架：`Restore(squadId)` 读取事实/投影，重建 DAG 与 waves，再重新进入同一个 `runSquad`。`CreateWorktree`、`PublishVerified`、`FastForward` 等领域动词自己承担幂等；主流程不传 stepId。

---

## 三、迁移序

Config → DAG → Worktree → Slave → Verify → Publish → 串行 FF → 并行 wave → 多 wave → 取消 → 孤儿 → 重启 → 日志 → 只读 HTTP。

每一步出口：

1. DAG 能由输入计划确定地产生 waves；
2. 同一 wave 的独立任务确实使用独立 worktree；
3. stdout/stderr 与 slave 释放遵守 Process 的异步资源协议；
4. Verify 失败不会进入共享 ref；
5. MergeOrder 稳定，重复恢复不会改变 FF 顺序；
6. 重启不会把 `Unfinished` 平铺成破坏依赖顺序的列表；
7. HTTP 只能读取 Squad 投影，不能发起第二条执行路径。

### 3.1 事实与投影

Squad 需要持久化的是“任务已验证”“wave 已接受”“FF 已完成”等外部事实，而不是 Scheduler 当前执行到第几个循环。`TaskIndex`、`CurrentWaveStage`、`MergeLease` 之类字段如果只是程序计数器，恢复时应由 DAG、已提交事实和幂等动词重新推导。

---

*KISS-12 终。*

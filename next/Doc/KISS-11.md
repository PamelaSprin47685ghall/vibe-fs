# 完整 Session Flow

---

## 一、生产主程序 [NORMATIVE]

```
let finishTodo s =
    session {
        while s.Todo.Unfinished do
            do! s.ContinueWork()
    }

let passReview s =
    session {
        do! finishTodo s
        while s.Review.Required do
            do! s.RequestReview()
            do! finishTodo s
    }

let run s =
    session {
        do! passReview s
        return! s.Finish()
    }
```

- `Finish`：root 结算 / child 回传。  
- **启动**：Gateway 在该 LocalEpoch **初始 Host turn 终态**后激活**本 Runtime** Driver（KISS-Driver）；非 Human 到达瞬间。  
- 仅本侧 Driver 执行；他 Runtime 同 Session 可并行，互不可见直至重启归并（KISS-04）。  

主程序故意不出现以下内容：Journal append、OpenCode client、Inbox.Receive、retry governor、PromptKey、取消协调和资源 Registry。它只表达业务顺序；这些可靠性细节由领域动词和本 Runtime Driver 承担。

### 1.1 主程序的进入点

```
Human message
    → 本 Runtime 写 HumanTurnStarted { TurnId }
    → 等待宿主原生首轮 terminal / idle 终态
    → 激活 run s
    → finishTodo
    → passReview
    → Finish
```

空 Session 创建不等于业务主程序已经可以结束。没有首轮宿主结果时，Todo/Review 可能仍是启动快照的旧投影；过早执行 `passReview` 会把“尚未开始”误判为“已经完成”。

同一 Session 的另一个 Runtime 可以拥有自己的 Driver，但它不进入本 Runtime 的 Inbox、不共享本 Runtime 的 mutable state，也不会改变本 Runtime 当前的循环条件。重启建立新 BootSnapshot 后，两个 Runtime 的事实才进入同一条时间归并历史。

---

## 二、已删概念

Owner / Stage / 多 Generation / Lease / 复合 State / FallbackPhase / Nudge* → Driver、LocalEpoch/TurnId、while、尾递归、PromptProtocol。

这些替代不是一一对应的重命名：

|旧机制|真正替代|
|---|---|
|SessionOwner|本 Runtime Driver + 本地 PromptProtocol|
|ContinuationStage|CE 的 await / `ChildSession.Run` |
|FallbackPhase|`tryAttempts` / `tryModels` 尾递归|
|NudgeStage|`while s.Todo.Unfinished` 或 `while s.Review.Required`|
|Generation 家族|本 Runtime LocalEpoch + Host MessageOrigin (TurnId)|
|PendingLease|PromptRequested/Submitted/Terminal 事实|
|全局 SessionState|BootSnapshot + Stream 投影集合|

---

## 三、辅助动词落点

`CompactIfNeeded` / `UpdateContextBudget` / `EnsureTitle` 写入明确结构化位置（如 ContinueWork 成功后或 Finish 前），禁止仅 EventBus 挂载。

每个辅助动词必须回答三个问题：

1. 它属于哪一个领域流程，而不是“哪个事件监听器”；
2. 成功后更新哪一个本 Runtime 投影；
3. 若进程在盘写成功后崩溃，重启从哪条事实恢复并跳过重复效果。

例如标题生成若只是 UI 派生值，可以是只读投影或 Finish 前的明确查询；若标题需要持久化，则必须有具体 Fact，不得用 `TitleStage` 记录程序计数器。

---

## 四、轨迹测

无工作 Finish；Todo 轮；网错；模型链；Review 轮；Invalid 有界；取消；parent 关；重启；重复事件；迟到 LocalEpoch；NoProgress；Plugin 消息不升 LocalEpoch。

建议将轨迹测试分为三组：

|组|覆盖|
|---|---|
|业务流程|Todo 0/1/N 轮、Review Passed/NeedsChanges/Invalid、Fallback 成功/耗尽|
|本 Runtime 协议|FIFO Cancel、Prompt terminal 相关、Tool CommandPort、Driver 单实例、Read Your Writes|
|重启归并|活跃日志固定 Frontier、半行、单源损坏、同 Session 多 Runtime、时间相同的稳定排序|

测试断言公开结果和事实，不断言旧 Registry、Stage、Coordinator 或文件跳转形状。

---

## 五、门禁

≤15 行级主函数；缩进≤2；循环条件纯投影；有进展动词；无 Journal/client/mutable/旧 SM；Manager/Coordinator/Registry/Stage/Phase/Lease/Owner 名 → 暂停。

“无 mutable”只针对主流程本身。纯函数内部的局部累加器、Journal Writer 的文件句柄、DriverSlot 和 Inbox 队列属于实现外壳；它们不能泄漏成业务 API，也不能让 Hook 直接修改。

---

*KISS-11 终。*

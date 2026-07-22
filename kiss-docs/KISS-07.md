# Subagent Flow

---

## 一、生命周期二分 [NORMATIVE]

|对象|寿命|
|---|---|
|物理 Child Session|可 continue；显式 Close / parent abort|
|本轮 Run attachment|Run 结束释放|

[FORBIDDEN] `use!` 结束即毁物理 session 却又 Resume。

物理 Child Session 是 OpenCode 中可被继续使用的事实；本轮 attachment 只是监听器、terminal waiter、取消链接和结果读取资源。两者混在一个 `IDisposable` 中，会在第一次运行结束时误删可继续的 child。

```
let runChild c request =
    child {
        let! session = c.GetOrCreateSession(request)
        return! session.Run(request.Prompt)
    }
```

### Run 序 [NORMATIVE]

```
进入 await 循环 / 注册 waiter（先于 send）
→ sendOnce
→ terminal（Host parentID 相关）
→ ChildCompleted
→ 释本轮监听

先注册 waiter 再发送 prompt 不是形式要求，而是事件时序要求。发送后立即完成的 terminal 可能在 `Send` 返回前抵达；如果实现先 Send、后订阅，就会把真实终态变成永远等待。waiter 必须按 PromptKey 与宿主 `UserMessageId/parentID` 相关，而不是假设 OpenCode 回传插件自造的 DispatchId。
```

---

## 二、API

```
GetOrCreateSession / ResumeSession / Run / Close
RunParallel → mapBounded Task 层 + 每请求独立上下文（KISS-01）
```

`ChildResult` = 值 DU。普通失败给父匹配；仅不可恢复才 ChildFlow 终止。

建议的概念签名：

```
    member Id: ChildId
    member Run: Prompt -> ChildFlow<ChildResult>
    member Close: unit -> ChildFlow<unit>
```

`Run` 释放本轮 attachment，`Close` 才表达物理 child 不再需要。Parent abort 可以调用 Close，但普通 continue 不得隐式 Close。

---

## 三、Continue

`ResumeSession` + `Run`；物理 session 须仍在。

恢复路径不依赖内存中的旧 handle：新 Runtime 从启动快照读取 `ChildCreated`/`ChildCompleted` 等事实，再通过 ChildId 获取或确认宿主 child session。旧 Runtime 是否仍存活不改变新 Runtime 的读取语义；它只看启动 Frontier 内已可见的历史。

---

## 四、RunParallel

mapBounded：保序、全 await、独立 CT/上下文、失败在 `'u` DU。无共享可变 ChildContext 并发写。

明确语义：

- 输入顺序决定输出顺序；完成先后不改变结果排列；
- 父 CancellationToken 传播到每个任务；
- 每个请求有独立 Child/Task Context；
- semaphore 在成功、失败、取消路径均 finally 释放；
- 没有 fire-and-forget；父 Flow 返回前所有子任务都已 await；
- fail-fast 若需要，由 linked CTS 的薄包装表达，不让默认结果丢失已完成任务。

Child 失败应使用可匹配的 `ChildResult` 变体，例如 `Completed`、`Cancelled`、`Incomplete`、`Failed of ChildFailure`。只有父流程无法继续的程序错误才进入 ChildFlow error。

---

## 五、Child 内策略

失败重试在 Child 流程内；不进全局 Fallback 总线。不用 `match! Ok|Error`。

重试必须重新计算稳定 PromptKey 的 Attempt 部分，并保留上一轮失败作为事实或诊断；不能复用同一 key 发送不同 payload。AcceptanceUnknown 时不得无条件换模型重发，必须先按 PromptProtocol reconcile。

---

## 六、删除

Actor/Service/Router/Decision*/Draining/Evidence/Registry/SubagentIo/Batch 碎片。

---

## 七、迁移序

单 child → 提取 → 隔离 → 取消 → abort → continue → iterator 事实 → 并行 → 部分失败 → 重启 → 内策略。

每一步的出口问题：

1. 物理 child 是否仍可被 ChildId 找到；
2. Send 后立刻 terminal 是否可收；
3. parent abort 是否等待资源清理；
4. 重启是否只依赖日志与宿主现状，不依赖旧 Runtime 内存；
5. 并行是否真的启动并 await，而不是 `for` 内逐个 `let!` 的串行伪并发。

---

*KISS-07 终。*

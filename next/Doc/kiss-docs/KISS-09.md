# Fallback 降级

Fallback ≠ 独立状态机。= `ContinueWork` 内模型选择与重试。**局部递归/循环变量，不持久化 FallbackState 机。**

---

## 一、用户本质

```
失败 → 是否可恢复 → 同模重试或换模 → sendOnce → 一次投递
```

删除 Execution/Core/Intent/Dispatch/Governor/Registry/Lease/… 塔。

---

## 二、实现：尾递归 [NORMATIVE]

[FORBIDDEN]

```
for model in models do
    match! trySend with
    | Completed o -> return o   // monadic for 不中止外层；类型常错

let result = s.SendContinue(...)  // Flow 未执行
AcceptanceUnknown 后仍切下一模型
.Head/.Tail 空表
Ignore 无返回路径
return! SendContinue 绕过 CommitTodo
持久化 RemainingModels / Exhausted 状态机
```

[NORMATIVE]

```
let rec tryAttempts s model attempt : SessionFlow<AssistantOutcome option> =
    session {
        if attempt > s.Config.MaxRetriesPerModel then
            return None
        else
            match! s.TrySendContinue(model, attempt) with
            | Completed outcome ->
                return Some outcome

            | RetryableFailure _ ->
                return! tryAttempts s model (attempt + 1)

            | FatalFailure error ->
                return! session.fail error

            | AcceptanceUnknown ->
                // 停止切换模型；协议未清前不重发
                return! session.fail SessionError.PromptUncertain
    }

let rec tryModels s models : SessionFlow<AssistantOutcome> =
    session {
        match models with
        | [] ->
            return! session.fail SessionError.FallbackExhausted

        | model :: remaining ->
            match! tryAttempts s model 1 with
            | Some outcome ->
                return outcome
            | None ->
                return! tryModels s remaining
    }

let continueWork (s: SessionScript) : SessionFlow<unit> =
    session {
        let! outcome = tryModels s s.Config.FallbackModels
        do! s.CommitTodoFrom(outcome)
    }
```

调用者只写 `do! s.ContinueWork()`。

`TrySendContinue` 内部：FIFO await（KISS-Driver）+ Host MessageId 相关 + sendOnce。

可选纯辅助 `nextStep`：不 IO、不 timer、不盘、不升格子系统。

---

## 三、实现语义补充

Fallback 的真实状态由当前调用栈表达：model、attempt、remaining models 都是一次 `ContinueWork` 的局部变量。只有外部已发生的事实进入 Journal；不把“正在第几个重试”当成恢复协议。

递归停止证明：`tryAttempts` 每次增加 attempt，超过上限返回 `None`；`tryModels` 每次消费一个 model，空列表返回 `FallbackExhausted`；`FatalFailure` 与 `AcceptanceUnknown` 不进入下一分支。因此没有空列表 `.Head`、成功后继续换模或绕过 `CommitTodoFrom`。

AcceptanceUnknown 不等于普通网络超时：它说明宿主可能已接受 prompt。继续换模型会制造重复 assistant 回合，必须停在 PromptProtocol。

Runtime A 与 B 可以各自对同一 Session 运行 Fallback；双方只保证本地 sendOnce。重启时所有 Prompt 事实按 `ObservedAt → RuntimeId → LocalSeq` 归并，存储层不选择其中一支。

## 四、AcceptanceUnknown

属 Prompt 协议。Unknown 时 **禁止** 试下一模型。  
恢复：宿主确认 | 新 LocalEpoch / TurnId 抢占 | reconcile Deadline → 可诊断错误。

---

## 五、Journal

`PromptFact` / 必要审计事实。不记 Retrying/Scanning/… 作 PC。  
无持久化 `FallbackState`。

---

## 六、删除目录

```
FallbackPhase ContinuationStage
FallbackExecution* Intent* Dispatch* Governor* Registry*
Lease* RuntimeStore RetryDispatchGovernor…
```

---

## 七、测试

transient；不可重试；模型顺序；exhausted；空输出；幂等；AcceptanceUnknown；重启不重发；child 隔离；取消；新 LocalEpoch/TurnId 抢占；尾递归在 MaxRetries 边界停。

补充断言：Attempt 单调递增；model 耗尽后才换模；成功后 CommitTodo 恰好一次；Unknown 后不调用第二模型；空模型列表返回领域错误；本 Runtime 重启可识别已 flush 的 PromptKey；多 Runtime 事实全部保留。

---

*KISS-09 终。*

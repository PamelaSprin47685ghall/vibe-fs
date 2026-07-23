# Review Flow

---

## 一、形态 [NORMATIVE]

```
let reviewOnce (r: ReviewScript) : ReviewFlow<ReviewVerdict> =
    review {
        use! reviewer = r.StartReviewer()  // 一次性 child，可安全释放
        let! report = reviewer.Review()    // 内部 = Child.Run 原子序；错误 → ReviewError
        return! r.AcceptVerdict(report)
    }
```

[FORBIDDEN] `failwith`。全部 `review.fail` / 成功 DU。

---

## 二、View 与 Required [NORMATIVE]

```
type ReviewView =
    { Required: bool
      Verdict: ReviewVerdict option
      Round: int
      MaxRound: int }
```

```
type ReviewVerdict =
    | Passed
    | NeedsChanges of ChangeRequest list
    | Invalid
// [FORBIDDEN] ReviewVerdict.Exhausted
// Exhausted 不是 reviewer 结论，是 Flow 控制结果 → ReviewError.ReviewExhausted
```

**Required**（投影）：

```
Required =
    config.ReviewEnabled
    && 有待审工作
    && verdict <> Some Passed
```

[FORBIDDEN]

```
Required = … && Round < MaxRound
// Round 达上限时 Required=false → while 退出 → 误 Finish 当通过
```

`Round >= MaxRound` 且仍需审：`RequestReview` **前置失败** → `ReviewExhausted`，while 进入后明确 fail，非静默成功。

初始：启用且有待审工作 → `Required = true`。

---

## 三、AcceptVerdict = 复合事实 [NORMATIVE]

```
member _.AcceptVerdict(report) =
    review {
        let verdict = VerdictParser.parse report.Text  // 纯函数
        let todoSnapshot = todoFromVerdict verdict     // NeedsChanges → Some snapshot；Passed → …
        do! commit (
            Review (
                ReviewApplied {|
                    Verdict = verdict
                    Round = currentRound + 1
                    ResultingTodo = todoSnapshot
                |}
            )
        )
        // 单 Envelope → ReviewView + TodoView；无双 append 窗口
        return verdict
    }
```

---

## 四、Session 集成 [NORMATIVE]

```
let passReview s =
    session {
        do! finishTodo s
        while s.Review.Required do
            do! s.RequestReview()
            do! finishTodo s
    }
```

### 4.1 Invalid 有界：尾递归 [NORMATIVE]

[FORBIDDEN] `for` 体内 `return verdict`（类型与非局部退出皆错）。

[FORBIDDEN] 无界 `do! s.RequestReview()` 自递归。

[NORMATIVE]

```
let rec requestValidReview (r: ReviewScript) (remaining: int) : ReviewFlow<ReviewVerdict> =
    review {
        if remaining <= 0 then
            return! review.fail ReviewError.ReviewExhausted
        else
            match! reviewOnce r with
            | Invalid ->
                return! requestValidReview r (remaining - 1)
            | (Passed | NeedsChanges _) as verdict ->
                return verdict
    }

let requestReview (s: SessionScript) : SessionFlow<unit> =
    session {
        if s.Review.Round >= s.Review.MaxRound
           && s.Review.Verdict <> Some Passed then
            return! session.fail SessionError.ReviewExhausted

        let! verdict =
            ReviewFlow.run (fun r -> requestValidReview r s.Config.MaxInvalidRetries)

        match verdict with
        | Passed -> ()
        | NeedsChanges _ -> ()  // Todo 已在 ReviewApplied 中
        | Invalid ->
            return! session.fail SessionError.ReviewExhausted

        // ProgressStamp 随 commit Seq 前进；禁手增
    }
```

总 Deadline / MaxRound 来自配置。

---

## 五、复审语义补充

Review 是领域动词，不是 Session Driver 外的第二调度器。`StartReviewer` 获取一次性 reviewer child；`Review` 负责发送 prompt、等待对应 terminal、提取报告；`AcceptVerdict` 负责纯解析与复合事实提交。调用者只面对 `Passed`、`NeedsChanges` 或明确 `ReviewExhausted`。

`Required` 是投影查询，不是“上一次 RequestReview 是否调用过”的布尔缓存。它依赖 Review 配置、待审工作、最近 verdict 与终止状态。`Round >= MaxRound` 仍可 Required，前置检查负责明确失败，不能让 while 静默退出后误 Finish。

复合事实消除了崩溃窗口：

```
错误：ReviewReturned 已 flush → 进程崩溃 → TodoChanged 未写入
正确：一个 ReviewApplied Envelope 同时驱动 ReviewView 与 TodoView
```

这不是通用事务框架。两个 Runtime 仍可在陈旧快照上各自产生 ReviewApplied；下一次启动由时间归并和领域 Fold 处理全部事实。

`RequestReview` 成功后读取本侧最新投影；调用者不追加 `Refresh`，也不对 reviewer 文案做脆弱正则判断。

## 六、行为覆盖

submit；真实 reviewer child；工具权限；affected files；  
Passed / NeedsChanges / Invalid；空输出；WIP；parent abort；  
并发 submit 策略；重启；append 失败不假装未发生；复合事实原子可见性。

---

## 七、删除

```
ReviewSession Registry / 双份 Runtime State
Loop Coordinator / Stage / Phase / 手写 SM
ReviewVerdict.Exhausted
```

---

*KISS-08 终。*

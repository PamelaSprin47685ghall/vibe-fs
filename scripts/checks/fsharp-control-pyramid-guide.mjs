export const CONTROL_PYRAMID_GUIDE = String.raw`
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
F# CONTROL PYRAMID — REPAIR MANUAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

这个门禁不是说 match 坏。
这个门禁也不是要求零分支。
它只拒绝一种形状：

    一个 decision 的 body
        继续长第二个 decision
            第二个 body
                再长第三个 decision

这时主要因果顺序已经由缩进承担。
读者必须数空格才能知道下一步。
门禁把这种 lexical control pyramid 当作债务。

扫描器故意激进。
它允许 false positive。
false positive 的处理方式不是加 suppression。
false positive 的处理方式是人工检查这个 control-flow boundary。
如果嵌套确实表达领域事实，就给 decision 一个名字并把边界压平。

────────────────────────────────────────────────────────────
0. 先读诊断，不要先改 gate
────────────────────────────────────────────────────────────

失败输出先列 todo：

    [match-pyramid] src/Foo.fs:120 depth=3 chain=match → match! → match
        match state with

你真正需要读的是四个字段。

file:line
    内层 decision 的精确位置。

depth
    当前 decision 是第几层 lexical decision。
    depth=2 已经触发。
    depth=5 不是“更高级”，只是更难读。

chain
    从外到内的控制链。
    match → match! 常见于 Task<Result<_,_>>。
    if → match 常见于 prerequisite 后再解释状态。
    match → if → try 说明一个函数同时拥有多个 decision level。

text
    被抓到的内层语句。
    它是修复入口，不一定是根因所在行。

常用命令：

    node scripts/checks/fsharp-control-pyramid.mjs --explain

只看完整修复手册，不扫源码。

    node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --show-all

看当前全部 pyramid 债务，而不是只看相对 baseline 的新增债务。

    node scripts/check.mjs

跑正式门禁入口。

    node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs

只跑扫描器合同。

    node --test requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs

只跑先修 vocabulary 合同。

不要做这些事：

    不要改缩进骗 scanner。
    不要换变量名骗 scanner。
    不要加无意义注释隔开 decision。
    不要把整棵 pyramid 原样搬进 horribleHelper。
    不要给文件加 allowlist。
    不要给单行加 suppression。
    不要把 baseline 数字调高。
    不要删教程测试。
    不要把 depth 阈值从 2 调成 3。

目标不是“门禁变绿”。
目标是让主要流程恢复成从上到下阅读。

────────────────────────────────────────────────────────────
1. 本仓先修已经存在：先用现成 vocabulary
────────────────────────────────────────────────────────────

纯 Result / Option / collection helpers 来自：

    open FsToolkit.ErrorHandling

本仓固定依赖 FsToolkit.ErrorHandling 5.2.0。
不要在单个 feature 里再造 ResultBuilder。
不要复制 Sphinx 曾经有过的私有 builder。

Task<Result<_,_>> 使用本仓自己的 Fable-compatible CE：

    open Wanxiangshu.Foundation

然后：

    taskResult {
        let! value = operationReturningTaskResult ()
        return value
    }

为什么不是直接依赖库里的 .NET taskResult？
因为本仓只有 Fable 平台，不存在 .NET 编译目标。
当前依赖的 FsToolkit Fable source surface 没有暴露对应 taskResult CE。
所以 Foundation/TaskResult.fs 是项目级 async Result vocabulary。

这个 builder 故意不提供模糊的 Task<'T> Bind overload。
原因是：

    Task<Result<T,E>>

同时也是：

    Task<'T>

如果两个 Bind overload 同时存在，F# 推断会出现候选歧义。
本仓明确区分：

    Task<Result<T,E>>
        直接 let!

    Result<T,E>
        直接 let! / do!

    Task<T>
        先 TaskResultCE.ofTask

例：

    taskResult {
        let! snapshot = readSnapshot ()
        let! local = materializeLocal raw commonDir |> TaskResultCE.ofTask
        return local, snapshot
    }

不要把普通 Task 再包成：

    task {
        let! x = operation ()
        return Ok x
    }

除非你正在定义新的 reusable adapter。
调用点直接用 TaskResultCE.ofTask。

只做 plumbing 映射时使用项目 vocabulary，不要引用 FsToolkit 的 .NET-only Task helpers：

    operation |> TaskValue.map f
    operationResult |> TaskResult.mapError mapError
    items |> TaskResultList.traverseM readOne

这些名字只表达 functor / error-map / short-circuit traversal；真正业务 decision 仍应拥有领域名字。

先修自检：

    ① 文件是否 open FsToolkit.ErrorHandling？
    ② Task<Result<_,_>> workflow 是否 open Wanxiangshu.Foundation？
    ③ 普通 Task 是否经 TaskResultCE.ofTask？
    ④ async Result plumbing 是否只用 TaskValue / TaskResult / TaskResultList？
    ⑤ 是否引用 FsToolkit 的 Task.map / TaskResult.* / List.traverseTaskResultM？
    ⑥ 是否又出现新的 ResultBuilder / TaskResultBuilder 私有复制？

如果 ⑤ 或 ⑥ 是 YES，先停。
项目级 vocabulary 已经存在。

────────────────────────────────────────────────────────────
2. 最常见：Result 的 Error 只是原样传播
────────────────────────────────────────────────────────────

坏形状：

    match readA () with
    | Error error -> Error error
    | Ok a ->
        match readB a with
        | Error error -> Error error
        | Ok b ->
            match readC b with
            | Error error -> Error error
            | Ok c ->
                Ok (finish a b c)

这里的 match 没有表达三个业务 decision。
它重复表达同一条 plumbing law：

    Error -> 停止
    Ok -> 继续

这就是 bind。

改成：

    open FsToolkit.ErrorHandling

    result {
        let! a = readA ()
        let! b = readB a
        let! c = readC b
        return finish a b c
    }

返回 Result<unit,_> 时优先 do!：

    result {
        do! validateA ()
        do! validateB ()
        do! persist ()
    }

判断是不是机械 bind，问：

    Error 分支是否只是 Error error？
    Error 分支是否只是 return Error error？
    Error 分支是否只是 return! Error error？
    Ok 分支是否只是解包后继续下一步？

四项高度吻合时，不要保留 nested match。

如果 Error 需要转换：

    readA ()
    |> Result.mapError asStorage

然后再进入 result CE。

如果每一步错误映射不同：

    result {
        let! a = readA () |> Result.mapError ReadAFailed
        let! b = readB a |> Result.mapError ReadBFailed
        return b
    }

不要因为 mapError 存在就退回 match pyramid。
错误映射和控制流是两个问题。

如果成功值不需要：

    do! validate input

不要写：

    let! () = validate input

两者都能工作，但 do! 明确表达“只关心成功/失败”。

修复后检查：

    主要流程能否按 A → B → C → finish 阅读？
    Error plumbing 是否消失？
    真正业务分支是否仍然可见？

────────────────────────────────────────────────────────────
3. Task<Result<_,_>>：这是本仓最重要的糖化对象
────────────────────────────────────────────────────────────

坏形状：

    task {
        match! readA () with
        | Error e -> return Error e
        | Ok a ->
            match! readB a with
            | Error e -> return Error e
            | Ok b ->
                match! readC b with
                | Error e -> return Error e
                | Ok c ->
                    return Ok c
    }

普通 task 的 let! 只解决：

    Task<X> -> X

如果 X 又是：

    Result<T,E>

你只 await 了 Task。
你没有结构化 Result failure propagation。

本仓正确形状：

    open Wanxiangshu.Foundation

    taskResult {
        let! a = readA ()
        let! b = readB a
        let! c = readC b
        return c
    }

如果中间一步是纯 Result：

    taskResult {
        let! a = readA ()
        do! validate a
        let! b = readB a
        return b
    }

如果中间一步是普通 Task：

    taskResult {
        let! a = readA ()
        let! metadata = readMetadata a |> TaskResultCE.ofTask
        let! b = readB metadata
        return b
    }

如果要捕获异常并转领域错误：

    taskResult {
        try
            let! a = readA ()
            return a
        with ex ->
            return! Error(Transport ex.Message)
    }

不要在 taskResult 里面再写：

    match! readA () with
    | Error e -> return! Error e
    | Ok a -> ...

除非 Error 和 Ok 本身就是不同业务语义，而不是 failure propagation。

看到连续两次：

    match! ... with
    | Error ...
    | Ok ... ->
        match! ...

第一反应：

    缺 taskResult bind。

第二反应才是：

    这里是否真有两个领域 decision？

本仓 WriterStreamSync.readRemote 就是参考形状。
读它，不要重新发明。

────────────────────────────────────────────────────────────
4. Option：None 只是传播时不要筑塔
────────────────────────────────────────────────────────────

坏：

    match tryA () with
    | None -> None
    | Some a ->
        match tryB a with
        | None -> None
        | Some b ->
            Some (finish a b)

简单两步可以写：

    tryA ()
    |> Option.bind (fun a ->
        tryB a
        |> Option.map (fun b -> finish a b))

步骤多时，使用 FsToolkit.ErrorHandling 的 option CE：

    open FsToolkit.ErrorHandling

    option {
        let! a = tryA ()
        let! b = tryB a
        return finish a b
    }

判断方法：

    None 是否只是 None？
    Some 是否只是拿值继续？

如果是，match 没有业务信息。

但是：

    match maybeLease with
    | None -> LeaseMissing
    | Some lease when lease.IsExpired -> LeaseExpired lease.Id
    | Some lease -> LeaseReady lease

这是领域 decision。
可以保留。

问题只在于它的 branch body 又长出第二棵树。

────────────────────────────────────────────────────────────
5. 两个或多个值共同决定分支：tuple match
────────────────────────────────────────────────────────────

坏：

    match a with
    | Some a ->
        match b with
        | Some b -> useBoth a b
        | None -> missingB ()
    | None -> missingA ()

好：

    match a, b with
    | Some a, Some b -> useBoth a b
    | None, _ -> missingA ()
    | _, None -> missingB ()

Result 同理：

    match ra, rb with
    | Error e, _ -> Error e
    | _, Error e -> Error e
    | Ok a, Ok b -> combine a b

三个独立状态同理：

    match enabled, accepted, hasLease with
    | false, _, _ -> Disabled
    | _, false, _ -> NotAccepted
    | _, _, false -> LeaseMissing
    | true, true, true -> Ready

关键问题：

    B 的计算是否依赖 A 的成功值？

如果不依赖：

    不要串两个 match。
    一次 pattern match 让状态空间显式出现。

如果依赖：

    看它是不是 Option/Result bind。
    如果不是，再考虑 named decision。

tuple match 的价值不是“少两行”。
它让一个 decision 一次展示完整状态空间。

────────────────────────────────────────────────────────────
6. 真正的领域 match 可以保留，但必须局部、扁平
────────────────────────────────────────────────────────────

好：

    match admission with
    | Rejected reason -> reject reason
    | Replay replay -> replayExisting replay
    | Fresh plan -> executeFresh plan

这里 case 名本身就是信息。
删除 match 反而损害模型。

坏：

    match admission with
    | Fresh plan ->
        match checkpoint with
        | Some checkpoint ->
            match status with
            | ...
    | ...

不要为了消灭它强行套 Result。
先识别内层 decision 的责任。

例如：

    let decideFresh checkpoint status =
        match checkpoint, status with
        | ...

    match admission with
    | Rejected reason -> reject reason
    | Replay replay -> replayExisting replay
    | Fresh plan ->
        decideFresh checkpoint status
        |> executeFresh plan

这里 helper 合法，因为它获得真实责任：

    decideFresh

下面不合法：

    let handleInnerStuff x =
        match x with
        | A ->
            match y with
            | ...

这只是搬家。
scanner 仍会在 helper 内抓到。

给 helper 起名时问：

    它决定什么？
    输入是什么 evidence？
    输出是什么 decision？
    调用者只看名字+签名能理解业务承诺吗？

如果答不出，边界还没找到。

优先名词/动词来自领域：

    decideAdmission
    classifyCheckpoint
    validateUnion
    chooseRecovery
    determineDisposition

拒绝模糊名字：

    handleStuff
    processInner
    continue2
    doWork
    helper
    fixMatch

────────────────────────────────────────────────────────────
7. if / elif：prerequisite 不要做成楼梯
────────────────────────────────────────────────────────────

坏：

    if enabled then
        if accepted then
            if hasLease then
                execute ()
            else
                LeaseMissing
        else
            NotAccepted
    else
        Disabled

如果它们是 prerequisite，先做 guard-shaped decision：

    if not enabled then
        Disabled
    elif not accepted then
        NotAccepted
    elif not hasLease then
        LeaseMissing
    else
        execute ()

门禁把 if/elif 视作一个 decision level。
这是刻意的。

因为读者可以从上到下看同一组条件。

如果多个 bool 实际代表一个领域状态：

    let decision =
        match enabled, accepted, hasLease with
        | false, _, _ -> Disabled
        | _, false, _ -> NotAccepted
        | _, _, false -> LeaseMissing
        | true, true, true -> Ready

    match decision with
    | Disabled -> ...
    | NotAccepted -> ...
    | LeaseMissing -> ...
    | Ready -> execute ()

更进一步：

如果 enabled / accepted / hasLease 是长期组合状态，
先检查类型设计是否已经制造非法状态。
门禁不是替代 state-product gate。
它只是告诉你 lexical tree 已经泄漏了复杂度。

命名 predicate：

    let canPublish evidence = ...
    let hasCanonicalOwner evidence = ...

然后：

    if not (canPublish evidence) then ...

比在条件里塞五个 && / || 更容易审查。

────────────────────────────────────────────────────────────
8. collection：别手写重复 short-circuit machinery
────────────────────────────────────────────────────────────

常见坏形状：

    let rec loop xs acc =
        match xs with
        | [] -> Ok(List.rev acc)
        | x :: rest ->
            match decode x with
            | Error e -> Error e
            | Ok value ->
                loop rest (value :: acc)

它不是永远错误。
但先检查是否只是 traverse。

本仓 FsToolkit.ErrorHandling 的 Fable surface 提供纯 Result traversal：

    List.traverseResultM
    List.sequenceResultM

异步 Result traversal 归 Wanxiangshu.Foundation：

    TaskResultList.traverseM

不要写 FsToolkit 的 List.traverseTaskResultM / sequenceTaskResultM；
它们在当前依赖中位于 #if !FABLE_COMPILER，Fable 根本不存在。

纯 Result：

    items
    |> List.traverseResultM decode

已经有 Result list：

    results
    |> List.sequenceResultM

Task<Result<_,_>>：

    items
    |> TaskResultList.traverseM readOne

M 后缀表示 monadic short-circuit 语义。
如果你需要 accumulate 所有错误，不要偷偷换 M。
那是不同业务语义。

如果 collection loop 还承担：

    index-aware validation
    bounded retry
    resource lifetime
    stateful dedupe

不要机械换 traverse。
先把这些责任拆成具名 operation。
然后看剩下的循环是否只是 traverse。

有界递归仍然合法。
门禁抓的是递归 body 内继续长控制树。
不是递归这个词本身。

────────────────────────────────────────────────────────────
9. match! 不是 Result bind
────────────────────────────────────────────────────────────

这个误解必须彻底消除。

    match! operation () with
    | Error e -> ...
    | Ok value -> ...

大致等价于：

    let! temp = operation ()
    match temp with
    | Error e -> ...
    | Ok value -> ...

match! 把 await 和 match 写在一起。
它没有自动传播 Error。

所以：

    task {
        match! a () with
        | Ok x ->
            match! b x with
            | Ok y ->
                match! c y with
                | ...

不是结构化 Result flow。
只是 await 和 branching 叠在一棵树里。

如果 case 是：

    Pending
    Completed
    Cancelled

而不是 Error / Ok plumbing，match! 可能完全正确。
但它的 branch body 仍应保持扁平。

关键不是语法 token。
关键是这个 case 是否承载真实领域信息。

────────────────────────────────────────────────────────────
10. try / with：异常边界不能包住整棵业务树
────────────────────────────────────────────────────────────

坏：

    try
        match admission with
        | Fresh plan ->
            if ready plan then
                match status with
                | ...
    with ex ->
        ...

这时 try 成了巨大 lexical owner。

先缩小异常可能发生的位置：

    let readPhysical () =
        try
            Ok(readUnsafe ())
        with ex ->
            Error(Transport ex.Message)

然后业务 workflow：

    result {
        let! input = readPhysical ()
        return decide input
    }

TaskResult 同理：

    taskResult {
        let! input = readPhysicalAsync ()
        return decide input
    }

如果一整段 effect 必须共享同一异常映射，
taskResult 的 try/with 可以保留。
但 try body 仍然应该是线性 CE：

    taskResult {
        try
            let! a = acquire ()
            do! validate a
            let! b = persist a
            return b
        with ex ->
            return! Error(Transport ex.Message)
    }

不是：

    try
        if ...
            match ...
                if ...
    with ...

异常边界越大，越难知道哪个 operation 真会抛。

────────────────────────────────────────────────────────────
11. while / for：循环 body 只能拥有有限 decision depth
────────────────────────────────────────────────────────────

坏：

    for item in items do
        match classify item with
        | A ->
            if valid item then
                match persist item with
                | ...
        | B -> ...

先判断这是：

    map / choose / partition？
    traverse / sequence？
    fold？
    bounded retry？
    真正 imperative physical loop？

如果是 collection transform：

    把 item -> result 的 decision 做成纯函数。
    再用 List.map / List.choose / traverse。

如果是 bounded retry：

    给递归一个明确 budget。
    每轮 operation 返回一个领域 decision。

例如：

    let rec retry remaining input =
        taskResult {
            let! outcome = attempt input
            return!
                match outcome with
                | Done value -> Ok value
                | Retry next when remaining > 0 -> retry (remaining - 1) next
                | Retry _ -> Error RetryBudgetExhausted
        }

这里 match 是局部 decision。
它没有继续长第二个 match。

如果是物理 loop：

    保持 body 短。
    把一次迭代变成 runOneIteration。
    名字必须描述一次迭代的完整物理责任。

不要用 mutable flag 把 nested branch 转成第二运行时。
那会触发别的 structured-workflow gate。

────────────────────────────────────────────────────────────
12. active patterns：只在它减少重复解析时使用
────────────────────────────────────────────────────────────

active pattern 不是“消灭 match”的魔法。
它适合把重复的识别逻辑命名。

例如多个地方都在识别：

    non-empty canonical identifier
    validated path segment
    normalized provider token

可以把识别做成 active pattern。
调用处仍然是扁平 match。

不要把三层 business workflow 塞进 active pattern。
那只是把 control tree 隐藏到模式求值里。

判断标准：

    active pattern 是否只是 classify / parse / recognize？

如果它会：

    I/O
    persist
    retry
    spawn
    mutate long-lived state

它就不该只是一个 pattern helper。

────────────────────────────────────────────────────────────
13. 一个 decision 里真的需要另一个 decision 怎么办？
────────────────────────────────────────────────────────────

这是本门禁故意逼你回答的问题。

答案通常有五类。

A. 内层其实是 bind

    用 result / option / taskResult。

B. 两个 decision 独立

    用 tuple match。

C. 内层是独立领域判断

    提取 Evidence -> Decision 的具名纯函数。

D. 内层是 prerequisite

    用 if/elif guard-shaped flow 或先形成 Decision DU。

E. 函数拥有太多责任

    重切 workflow boundary。

不要回答：

    “F# 就是要这样写。”

不是。
F# 的模式匹配很强。
强模式匹配更应该把状态空间一次摊平。

也不要回答：

    “这个 false positive 合理，所以加 suppression。”

本门禁没有 suppression 机制。
这是设计，不是缺功能。

如果人工确认嵌套最清楚：

    先尝试给内层 decision 命名。
    如果命名后更清楚，提取。
    如果命名完全重复外层语义，尝试 tuple match。
    如果两者都不行，检查类型是否把两个状态轴错误耦合。

门禁目标不是形式纯洁。
门禁目标是把“为什么要嵌套”变成一次显式设计审查。

────────────────────────────────────────────────────────────
14. baseline：它是债务账本，不是许可证
────────────────────────────────────────────────────────────

仓库已有历史 pyramid。
一次把全仓改完会扩大变更面。
所以正式 gate 使用 per-file baseline ratchet。

baseline 只记录：

    某文件当前有多少个 depth>=2 decision。

它不记录：

    合法理由。
    suppression 注释。
    pattern 白名单。
    owner 白名单。

规则：

    新文件从 0 开始。
    旧文件不能超过自己的 baseline。
    修掉债务后 baseline 只能向下改。
    baseline 不能向上改来让 CI 绿。

查看全部债务：

    node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --show-all

查看当前 baseline snapshot：

    node scripts/checks/fsharp-control-pyramid.mjs --root=src/Wanxiangshu --snapshot

snapshot 只打印 JSON。
它不会改文件。
它不是“重新生成 baseline”按钮。

正确 baseline 更新流程：

    1. 修源码。
    2. 跑 --show-all 确认命中减少。
    3. 跑 --snapshot 看新计数。
    4. 只降低对应文件数字。
    5. 跑 node scripts/check.mjs。
    6. review diff，确认没有任何 baseline 增长。

错误流程：

    1. 新增 pyramid。
    2. gate 红。
    3. 复制 --snapshot 覆盖 baseline。

这等于拆门。

────────────────────────────────────────────────────────────
15. 修复顺序：每个命中都按同一棵 decision tree
────────────────────────────────────────────────────────────

命中后按下面顺序判断。

① Error / None 是否只是原样传播？

    YES -> Result/Option bind。
    多步优先 CE。

② 是 Task<Result<_,_>> 吗？

    YES -> taskResult。
    普通 Task 用 TaskResultCE.ofTask。

③ 多个值是否互不依赖、共同决定一个分支？

    YES -> match a, b with ...

④ 内层 match 是否是独立领域 decision？

    YES -> Evidence -> Decision 具名纯函数。

⑤ 多层 if 是否是 prerequisite？

    YES -> if/elif guard-shaped flow 或 Decision DU。

⑥ collection 是否重复“成功继续、失败停止”？

    YES -> traverse / sequence / fold CE。

⑦ try 是否覆盖了过大的业务区域？

    YES -> 缩小异常边界，先转 typed error。

⑧ loop body 是否同时 classify + validate + effect？

    YES -> 拆一次迭代 responsibility，再选择 map/traverse/bounded recursion。

⑨ 上面都不是？

    不要忽略 gate。
    重新切函数边界。
    一个函数很可能同时拥有太多 decision level。

这个顺序非常重要。
先消 plumbing。
再合并独立状态。
最后才重切领域边界。

反过来做会制造一堆只包 plumbing 的 helper。

────────────────────────────────────────────────────────────
16. 修完后的目标形状
────────────────────────────────────────────────────────────

坏：

    outer
        branch
            inner
                branch
                    inner
                        branch
                            work

好：

    acquire
    validate
    decide
    execute
    persist

或者：

    taskResult {
        let! evidence = acquire ()
        do! validate evidence
        let decision = decide evidence
        return! execute decision
    }

其中 decide 可以继续使用 pattern matching：

    let decide evidence =
        match evidence with
        | A -> DecisionA
        | B -> DecisionB
        | C -> DecisionC

关键是 decide 本身一次看完。

另一个好形状：

    let decision =
        match admission, checkpoint with
        | ...

    match decision with
    | ...

两个 match 是顺序的。
不是 lexical nested。
读者不需要维护两层缩进上下文。

────────────────────────────────────────────────────────────
17. PR review 时怎么读一个命中
────────────────────────────────────────────────────────────

Reviewer 不要只问“能不能改写”。
按下面问题审查。

Q1. 外层 decision 在决定什么？

    如果答不上，先命名外层责任。

Q2. 内层 decision 在决定什么？

    如果答案和外层相同，考虑一次 match / tuple match。

Q3. 内层是否只是 effect plumbing？

    Error/Ok -> result/taskResult。
    None/Some -> option。

Q4. 两个 decision 的输入是否独立？

    独立 -> tuple match。

Q5. 内层是否可以变成纯 Evidence -> Decision？

    可以 -> 提取并命名。

Q6. 提取后的 helper 是否仍有 pyramid？

    有 -> 没修，只搬家。

Q7. 新 helper 名字是否声明业务承诺？

    否 -> 边界仍模糊。

Q8. 是否引入新的 mutable / stage / phase 来“压平”？

    是 -> 方向错误。

Q9. 是否改变了错误传播语义？

    short-circuit 与 accumulate-errors 不能混。

Q10. 是否改变 effect 顺序？

    CE 糖化必须保持原来顺序，除非需求明确改变。

────────────────────────────────────────────────────────────
18. 常见坏修法
────────────────────────────────────────────────────────────

坏修法 A：只提取大 helper

    let runInner x =
        原封不动的三层 pyramid

    match outer with
    | A -> runInner x

门禁会继续抓 runInner。
正确。

坏修法 B：把 match 改成 if

    match x with ...

变成：

    if isA x then
        if isB y then ...

形状没变。
门禁会继续抓。
正确。

坏修法 C：把 Result 变 exception

    Result.get
    raise
    try/with

这是把 typed failure 退化成不可见控制流。
拒绝。

坏修法 D：加 mutable done

    let mutable done = false
    while not done do ...

这是把 lexical pyramid 变成 runtime state machine。
更差。

坏修法 E：增加万能 DU case

    Other of obj

只是让 exhaustive match 失去价值。

坏修法 F：baseline +1

这不是修复。
这是记账造假。

坏修法 G：在 scanner 加特例路径

如果某文件天然复杂，应该重切 ownership。
不是让 scanner 失明。

────────────────────────────────────────────────────────────
19. Fable 边界注意事项
────────────────────────────────────────────────────────────

本仓不是纯 .NET 应用。
最终还要经 Fable 预编译。
所以“在 dotnet F# Interactive 能跑”不够。

修改 error-handling vocabulary 后至少跑：

    node scripts/build.mjs

这会走真实 Fable precompile。

不要从 .NET 文档看到一个 CE 就假设 Fable package surface 相同。
先看本仓 build。

Foundation/TaskResult.fs 的存在就是这个教训的永久化结果。

如果将来 FsToolkit.ErrorHandling 的 Fable surface 原生提供等价 taskResult：

    先写迁移 proof。
    证明现有语义和 Fable 产物等价。
    再删除本地 builder。

不要同时保留两个 taskResult vocabulary。
单一真理源优先。

────────────────────────────────────────────────────────────
20. 快速配方：看到形状就选工具
────────────────────────────────────────────────────────────

形状：

    Error -> return Error
    Ok -> continue

工具：

    result { let! / do! }

形状：

    Task<Result<_,_>> + repeated match!

工具：

    taskResult { let! / do! }

形状：

    taskResult 内普通 Task

工具：

    TaskResultCE.ofTask

形状：

    None -> None
    Some -> continue

工具：

    Option.bind / Option.map / option CE

形状：

    a 决定后再看 b，且 a/b 独立

工具：

    match a, b with

形状：

    List recursion + each item Result short-circuit

工具：

    List.traverseResultM

形状：

    List recursion + each item Task<Result> short-circuit

工具：

    List.traverseTaskResultM

形状：

    domain DU cases

工具：

    保留 flat exhaustive match

形状：

    nested prerequisites

工具：

    if / elif 或先形成 Decision

形状：

    nested domain decision

工具：

    named Evidence -> Decision

形状：

    try 包住巨大 workflow

工具：

    缩小 exception boundary，转 typed error

形状：

    for/while body 长树

工具：

    map/traverse/fold/bounded recursion/runOneIteration

────────────────────────────────────────────────────────────
21. 最后验收清单
────────────────────────────────────────────────────────────

修完一个命中后逐项检查。

    [ ] 内层 decision 消失或获得真实责任名字。
    [ ] 主要因果顺序可从上到下阅读。
    [ ] Error/None plumbing 没有手写重复。
    [ ] Task<Result<_,_>> 使用 taskResult。
    [ ] 普通 Task 在 taskResult 内显式 TaskResultCE.ofTask。
    [ ] 纯 Result 使用 result 或 Result combinator。
    [ ] 独立状态用 tuple match，而不是串行 nesting。
    [ ] 领域 match 保留 exhaustive case 信息。
    [ ] helper 不是 pyramid 搬家。
    [ ] 没有新增 mutable control flag。
    [ ] 没有新增 Stage/Phase/Next 伪程序计数器。
    [ ] effect 顺序保持原合同。
    [ ] short-circuit / accumulate-errors 语义没有偷换。
    [ ] Fable build 通过。
    [ ] targeted gate tests 通过。
    [ ] baseline 只下降，不上升。

如果全部成立，才算修复。

────────────────────────────────────────────────────────────
22. 最后的判断标准
────────────────────────────────────────────────────────────

不要问：

    “我能不能向门禁证明这段嵌套合理？”

先问：

    “一个读者能不能从上往下看到 operation 的主要因果顺序？”

如果答案是否定的，继续压平。

不要问：

    “有没有某个更聪明的语法把嵌套藏掉？”

先问：

    “这层缩进到底在表达 plumbing、状态空间，还是一个独立 decision？”

plumbing -> bind / CE。
状态空间 -> 一次 pattern match。
独立 decision -> 命名边界。

不要追求零 match。
追求每一个 match 都局部、扁平、一次看完。

不要追求最少函数。
追求每个函数只拥有它能准确命名的 decision depth。

不要追求代码极短。
追求读者不需要脑内维护 lexical stack。

这就是门禁的全部立场。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
APPENDIX A — 逐命中现场操作卡
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

现场 01：先看 chain。
现场 02：找到最内层 decision。
现场 03：确认它的输入来自哪里。
现场 04：确认它的输出去哪里。
现场 05：标记它是否只是 Error/Ok plumbing。
现场 06：标记它是否只是 None/Some plumbing。
现场 07：标记它是否 await 后继续 match Result。
现场 08：标记它是否与外层输入独立。
现场 09：标记它是否是领域 DU 的真实 case。
现场 10：标记它是否只是 prerequisite。
现场 11：标记它是否只为 exception mapping 存在。
现场 12：标记它是否在 collection loop 内重复。
现场 13：先消除 plumbing。
现场 14：再合并独立状态。
现场 15：再提取独立 decision。
现场 16：最后才考虑重切大 workflow。
现场 17：给新 function 一个领域名字。
现场 18：确认名字不是 handle/process/helper。
现场 19：确认 helper 内没有同样 pyramid。
现场 20：确认没有引入 mutable flag。
现场 21：确认没有引入 stage/phase。
现场 22：确认没有改变 effect 顺序。
现场 23：确认错误映射保持一致。
现场 24：确认 Result short-circuit 保持一致。
现场 25：确认 Option None 语义保持一致。
现场 26：确认 tuple match 覆盖完整状态空间。
现场 27：确认 exhaustive match 没有万能 _ 吞新 case。
现场 28：确认 taskResult 中 plain Task 已 ofTask。
现场 29：确认没有第二个 ResultBuilder。
现场 30：跑 targeted tests。
现场 31：跑 Fable build。
现场 32：跑 --show-all 看本文件命中是否下降。
现场 33：只降低本文件 baseline。
现场 34：跑 scripts/check.mjs。
现场 35：review baseline diff。
现场 36：baseline 有任何增长则拒绝。
现场 37：review 新 helper 名字。
现场 38：review 新类型是否制造非法状态。
现场 39：review 新异常边界是否扩大。
现场 40：review collection helper 的 M/A 语义。
现场 41：确认 retry 有显式 budget。
现场 42：确认 loop body 只做一次迭代责任。
现场 43：确认 active pattern 只做 classify/parse。
现场 44：确认物理 I/O 没藏进 pattern helper。
现场 45：确认真正领域 match 仍然可见。
现场 46：确认调用点故事比改前更线性。
现场 47：确认没有为了 gate 牺牲类型精度。
现场 48：确认没有为了 gate 牺牲错误类型。
现场 49：确认没有为了 gate 复制 code。
现场 50：确认最终 diff 能解释为一个控制边界改进。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
APPENDIX B — Reviewer 反逃逸卡
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

发现 rename-only：拒绝。
发现 formatting-only：拒绝。
发现 comment-only：拒绝。
发现 helper 搬家：拒绝。
发现 suppression：拒绝。
发现 allowlist：拒绝。
发现 baseline 增长：拒绝。
发现 depth 阈值放宽：拒绝。
发现 Result -> exception：拒绝。
发现 typed error -> stringly error：拒绝。
发现 mutable done flag：拒绝。
发现 stage/phase program counter：拒绝。
发现 catch-all _ 吞掉领域 case：要求解释。
发现 taskResult 内裸 plain Task：改用 TaskResultCE.ofTask。
发现私有 ResultBuilder：删，使用 FsToolkit.ErrorHandling。
发现私有 TaskResultBuilder：删，使用 Wanxiangshu.Foundation。
发现重复 collection recursion：先检查 traverse/sequence。
发现 repeated match! Error/Ok：优先 taskResult。
发现 repeated match Error/Ok：优先 result。
发现 repeated match Some/None：优先 option/combinator。
发现 independent nested match：优先 tuple match。
发现 domain nested match：要求 named Decision boundary。
发现 try 包住业务森林：缩异常边界。
发现 loop 包住业务森林：拆一次迭代。
发现“因为 F# 就这样”：要求具体语义解释。
发现“false positive”：要求展示压平尝试结果。
发现“这样文本短”：短不是标准。
发现“这样性能快”：要求性能证据，且不得以可读性猜测换性能。
发现“这是 legacy”：baseline 已负责 legacy，不允许新增。
发现“以后再改”：新债务不进主干。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
APPENDIX C — 一分钟判断表
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

看到 Error/Ok 重复？ -> result。
看到 Task<Result> 重复？ -> taskResult。
看到 plain Task in taskResult？ -> TaskResultCE.ofTask。
看到 Some/None 重复？ -> option / Option.bind。
看到两个独立值？ -> tuple match。
看到领域 DU？ -> 保留 flat match。
看到 branch 内另一业务 match？ -> named Decision。
看到 nested if prerequisite？ -> elif / Decision。
看到 list short-circuit？ -> traverseResultM / traverseTaskResultM。
看到 try 套森林？ -> typed error boundary。
看到 loop 套森林？ -> one-iteration operation。
看到 helper 仍套森林？ -> 没修。
看到 baseline 要 +1？ -> 停。

最终目标：

    evidence
    validate
    decide
    effect

而不是：

    evidence
        success
            condition
                case
                    effect

读者应该读 operation。
不应该读缩进。
`

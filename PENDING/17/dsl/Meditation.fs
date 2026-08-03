// Meditator DSL — 货币层。语法化：演算 §1 判断 S;K ⊢ M ⇓ S′;K′ 的作用域 + H-par + §9.1（journal 合同）。
// 本文件零领域知识：不知道命题、warrant、方法论；journal 只见 envelope 的 canonical 行（NDJSON）。
// 事件编解码是 Ledger 边界的契约面（VERIFY-008）——journal 的 Replay 返回原始行，decode 在 Kernel 侧。
// 编译顺序：1（无依赖）。
module Meditator.Meditation

open System
open System.Threading
open System.Threading.Tasks

/// 矛盾：同一 scoped proposition 的支持与反对同时位于当前义务的成功谓词路径（演算 §11 升级判据）。
type Contradiction =
    { SubjectId: string
      SupportDigest: string
      OpposeDigest: string }

/// 解锁阻塞的最小输入。
type RequiredInput = { What: string; WhyNeeded: string }

/// 未决问题快照：短路时移交报告层。义务本体在 Obligation.fs；此处只存稳定身份。
type UnresolvedProblem =
    { ObligationId: string
      Kind: string
      Description: string }

/// 短路通道：只含非成功结局。
/// 成功（ContractSatisfied/OpenWorldReportReady/TargetRefuted）不是 stop——它经 Ok 通道由 conclude 正常返回（O1 裁决，细化 §12.3）。
type MeditationStop =
    | Inconclusive of UnresolvedProblem list
    | Blocked of RequiredInput list
    | Inconsistent of Contradiction list
    | BudgetExhausted of UnresolvedProblem list

/// journal 写回执（PERSIST-002：没有部分写入；演算 §2 S2：幂等身份）。
type AppendOutcome =
    | Committed
    | AlreadyCommitted // 同 EventId 同字节：幂等重放成功（S2）
    | Conflict // 同 EventId 异字节：S2 违规，非法状态
    | CommitUnknown
    | WrongExpectedRevision of actual: int // 138 版：行数 != 期望 revision（并发追加冲突）

/// CommitUnknown 的确认结果（PERSIST-003 fail closed 的出口）。
type ReconcileOutcome =
    | Reconciled of AppendOutcome // 已确认：Committed 或 AlreadyCommitted 或 Conflict
    | StillUnknown

/// Transcript 冻结回执（P0-2）：同 key 同字节幂等，同 key 异字节 = 非法状态（S2 同构）。
type TranscriptPutOutcome =
    | Stored // 新冻结
    | AlreadyStored // 同 key 同字节：幂等
    | TranscriptConflict // 同 key 异字节：同一 invocationKey 两个不同 accepted transcript

/// accepted transcript 的专用存储（P0-2）：与事件 journal 职责分离。
/// TryFindAccepted 曾把"缓存查询"与"事件行"混在 IMeditationJournal 里，
/// 导致完整 transcript（validate 需要）与事件行（只存 digest）语义分裂——
/// 本接口存完整 canonical transcript；OracleInvocationAccepted 事件仍由
/// Kernel 统一 append+fold（审计与 OracleCalls 计数），两者不再互相覆盖。
type IAcceptedTranscriptStore =
    abstract TryGet: invocationKey: string -> string option
    abstract PutIfAbsent: invocationKey: string * transcript: string -> TranscriptPutOutcome

/// journal 只认识 envelope 的 canonical 行（NDJSON，字符串）。事件 schema 的编解码在 Ledger 边界——
/// codec 边界即契约面（VERIFY-008）；Replay 返回原始行，由调用方 decode + 版本校验 + fold。
/// P0-2 后不再承担 oracle transcript 缓存（迁移至 IAcceptedTranscriptStore）。
type IMeditationJournal =
    abstract Replay: ct: CancellationToken -> Task<string list>
    /// 乐观并发控制（138 版）：expectedRevision = 当前行数（事件序列）——两个进程同时从
    /// 同一 revision 追加会被拒绝（WrongExpectedRevision），不再等 Replay 才发现 sequence 冲突。
    /// 138 版 security-review：**实现必须原子**（行数检查与写入在同一临界区，如 O_APPEND + 锁
    /// 或事务性追加）——检查与写入之间的 TOCTOU 会允许并发双写同 sequence 行，被 Replay 的
    /// sequence 连续校验拒绝（fail closed，可用性 DoS；完整性由 EventId/sequence/digest 链兜底）。
    abstract Append: expectedRevision: int -> envelopeLine: string -> ct: CancellationToken -> Task<AppendOutcome>
    /// Reconcile（139 版）：携带 expectedLine——adapter 验证存储的是**本次尝试的精确行**
    /// （同 EventId 异字节 → Reconciled(Conflict)，不是"确认已写入"）。
    /// PERSIST-003 的"确认这一次写入"在接口层表达完整。
    abstract Reconcile: eventId: string -> expectedLine: string -> ct: CancellationToken -> Task<ReconcileOutcome>

/// 环境：外壳职责的容器。oracle 不住这里——oracle 走方法函数的显式参数（P5）。
/// Clock 是 Provenance.ProducedAt 的唯一合法来源（演算 §3/§4：时间戳外部注入，
/// 禁止以 transcript digest 冒充时间）。
/// TranscriptStore（P0-2）：accepted transcript 的专用存储，与事件 journal 分离。
type MeditationEnvironment =
    { Journal: IMeditationJournal
      TranscriptStore: IAcceptedTranscriptStore
      PolicyVersion: string
      ReducerVersion: string
      Clock: unit -> string }

/// Meditation<'a> 是真正执行工作的函数，不是程序 AST（§12.3）。
/// 不存在 AskOracle/Evaluate/Sequence 节点类型；CE 只组合函数，不建 AST。
type Meditation<'a> = MeditationEnvironment -> CancellationToken -> Task<Result<'a, MeditationStop>>

type MeditationBuilder() =
    member this.Return(x) : Meditation<'a> = fun _env _ct -> Task.FromResult(Ok x)

    member this.ReturnFrom(m: Meditation<'a>) : Meditation<'a> = m

    member this.Bind(m: Meditation<'a>, f: 'a -> Meditation<'b>) : Meditation<'b> =
        fun env ct ->
            task {
                let! r = m env ct

                match r with
                | Ok x -> return! (f x) env ct
                | Error stop -> return Error stop
            }

    member this.Zero() : Meditation<unit> = this.Return(())

    member this.Delay(f: unit -> Meditation<'a>) : Meditation<'a> = fun env ct -> (f ()) env ct

    member this.Combine(a: Meditation<unit>, b: Meditation<'a>) : Meditation<'a> = this.Bind(a, (fun () -> b))

    member this.TryWith(m: Meditation<'a>, handler: exn -> Meditation<'a>) : Meditation<'a> =
        fun env ct ->
            task {
                try
                    return! m env ct
                with e ->
                    return! (handler e) env ct
            }

    member this.TryFinally(m: Meditation<'a>, compensation: unit -> unit) : Meditation<'a> =
        fun env ct ->
            task {
                try
                    return! m env ct
                finally
                    compensation ()
            }

    member this.Using(resource: 'r :> IDisposable, body: 'r -> Meditation<'a>) : Meditation<'a> =
        this.TryFinally(body resource, (fun () -> resource.Dispose()))

    member this.While(guard: unit -> bool, body: Meditation<unit>) : Meditation<unit> =
        fun env ct ->
            task {
                // 局部可变 = 草稿纸：不改入参、不碰外部、同入同出（宝典）。
                let mutable go = guard ()
                let mutable halted: MeditationStop option = None

                while go && halted.IsNone do
                    let! r = body env ct

                    match r with
                    | Ok() -> go <- guard ()
                    | Error stop -> halted <- Some stop

                match halted with
                | Some stop -> return Error stop
                | None -> return Ok()
            }

[<AutoOpen>]
module MeditationOps =

    let meditation = MeditationBuilder()

    /// 短路：把非成功结局抬进 Error 通道。
    let halt (stop: MeditationStop) : Meditation<'a> =
        fun _env _ct -> Task.FromResult(Error stop)

    /// 读环境：唯一合法的环境查询。
    let ask: Meditation<MeditationEnvironment> = fun env _ct -> Task.FromResult(Ok env)

    /// 抬升外壳效应。task 异常不经 Result——事故通道，与可预见结局分离（宝典铁律）。
    let ofTask (work: CancellationToken -> Task<'a>) : Meditation<'a> =
        fun _env ct ->
            task {
                let! x = work ct
                return Ok x
            }

    let map (f: 'a -> 'b) (m: Meditation<'a>) : Meditation<'b> =
        fun env ct ->
            task {
                let! r = m env ct
                return Result.map f r
            }

    /// H-par / ARCH-009：唯一并行原语。并发经信号量有界；结果按输入位置排列——
    /// 完成顺序不得进入领域值。任一 action 抛异常 → 整体拒绝（Task.WhenAll 故障传播）；
    /// Error stop 不中断同伴，按输入序取首个 Error；许可 finally 归还；取消在获取许可处观察。
    /// 生产映射：src/Wanxiangshu.Next/Kernel/Flow.fs 的 Parallel.mapBounded。
    let mapBounded (maxConcurrency: int) (items: 't list) (action: 't -> Meditation<'u>) : Meditation<'u list> =
        if maxConcurrency <= 0 then
            invalidArg (nameof maxConcurrency) "must be positive and finite"

        fun env ct ->
            task {
                use semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency)

                let! results =
                    items
                    |> List.map (fun item ->
                        task {
                            do! semaphore.WaitAsync(ct)

                            try
                                return! action item env ct
                            finally
                                semaphore.Release() |> ignore
                        })
                    |> Task.WhenAll

                return
                    results
                    |> Array.tryPick (function
                        | Error stop -> Some stop
                        | Ok _ -> None)
                    |> function
                        | Some stop -> Error stop
                        | None ->
                            Ok(
                                results
                                |> Array.choose (function
                                    | Ok x -> Some x
                                    | Error _ -> None)
                                |> Array.toList
                            )
            }

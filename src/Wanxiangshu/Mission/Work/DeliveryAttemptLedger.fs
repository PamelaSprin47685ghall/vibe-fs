namespace Wanxiangshu.Mission.Work

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// 交付 attempt 台账（进程内）。
///
/// 条款 3：交付是否成立只由真实交付事实决定——view 声明、助手正文与 provider
/// stop 都不构成完成见证。条款 4：中断「已发出」不等于「已观察到中断成功」。
/// 条款 6：提交、呈现与中断分开取证，交付已提交不因后续中断未确认而消失。
///
/// 台账只登记四类可分别取证的事实：
///   * 已成立（提交面）：本次 attempt 的交付已提交；
///   * 呈现面：宿主已接受该交付的工具结果；
///   * 中断的「已发出」；
///   * 中断的「已观察到结算结果」。
///
/// 键是精确 attempt 围栏 (session, providerRun, toolCallId)；登记时的物理
/// 用户消息是防误停新输入的围栏——物理输入已变就不再瞄准（不得误停新输入
/// 或后任）。
///
/// 交付是否成立以 durable 交付见证为准，进程内台账只是快速路径。读入口有
/// 两个：
///   * tryFindEstablished：进程内台账（快速路径，命中即可直接用）；
///   * tryFindDurableEstablished：durable 交付见证（判据本身，键仍是
///     session * providerRun * toolCallId）。
/// 台账不命中必须回查 durable 见证：进程重启后台账为空，只认台账会把「已
/// 成立的交付」误判成「没交付过」，宿主就会继续发本应停止的请求。
module DeliveryAttemptLedger =

    /// 精确 attempt 围栏：本次调用 + 本次 provider run + 本次工具调用。
    type Fence =
        { SessionId: SessionId
          ProviderRun: ProviderRunIdentity
          CallId: ToolCallId }

    /// 中断的两个可分别取证的事实。
    type InterruptFacts =
        { Issued: bool
          Observed: Result<unit, string> option }

    type Record =
        { Fence: Fence
          Physical: PhysicalUserMessageId option
          Established: bool
          Presented: bool
          Interrupt: InterruptFacts
          InterruptTask: Task option
          Sequence: int }

    let private gate = obj ()
    // DSL-MUTABLE: resource — process-local delivery attempt ledger
    let private records = Dictionary<Fence, Record>()
    // DSL-MUTABLE: resource — monotonic ledger sequence for newest-fact ordering
    let mutable private sequence = 0

    let private emptyFacts: InterruptFacts = { Issued = false; Observed = None }

    let private emptyRecord (fence: Fence) : Record =
        { Fence = fence
          Physical = None
          Established = false
          Presented = false
          Interrupt = emptyFacts
          InterruptTask = None
          Sequence = 0 }

    let private update fence f =
        lock gate (fun () ->
            sequence <- sequence + 1

            let current =
                match records.TryGetValue fence with
                | true, record -> record
                | _ -> emptyRecord fence

            let next = f current

            records.[fence] <- { next with Sequence = sequence })

    /// 交付已成立（提交面事实）：本次 attempt 的交付已提交。登记时保存当时的
    /// 物理用户消息；已有的物理围栏不因后来的登记而改写。
    let recordEstablished fence physical =
        update fence (fun current ->
            { current with
                Established = true
                Physical =
                    match current.Physical with
                    | Some saved -> Some saved
                    | None -> physical })

    /// 呈现面事实：宿主已接受该交付的工具结果。呈现不改变提交面事实。
    let recordPresented fence =
        update fence (fun current -> { current with Presented = true })

    /// 本 session、且登记时物理输入仍是当前物理输入的最近一次已成立交付。
    /// 物理输入已变 → 不返回（不得误停新输入或后任）。
    let tryFindEstablished sessionId physical =
        lock gate (fun () ->
            records.Values
            |> Seq.filter (fun record ->
                record.Fence.SessionId = sessionId
                && record.Established
                && record.Physical = physical)
            |> Seq.sortBy (fun record -> record.Sequence)
            |> Seq.toList
            |> List.rev
            |> List.tryHead
            |> Option.map (fun record -> record.Fence, record))

    /// 本 session 是否存在任何一次已成立的交付（不绑定物理输入）。
    let hasEstablished sessionId =
        lock gate (fun () ->
            records.Values
            |> Seq.exists (fun record -> record.Fence.SessionId = sessionId && record.Established))

    /// 条款 3：以 durable 交付见证为准的「本 session 是否存在一次已成立的交付」。
    ///
    /// 判据：本 session 下存在一条已提交的 durable 见证，且该见证围栏登记的
    /// 物理用户消息仍是当前请求的物理输入——物理输入已变就不再命中（不得误停
    /// 新输入或后任）。围栏键仍是 (session, providerRun, toolCallId)：每条候选
    /// 见证都用精确围栏读取复核一次，命中才算成立。
    ///
    /// 查不到见证时返回 None。这不是「没交付过」的断言，只是「无法证明成立」：
    /// 条款 3 只认真实交付事实，所以查不到见证就不能当作已完成。
    ///
    /// 围栏复核是独立的一步：一条候选见证先按 session 与物理输入过滤，再
    /// 用它自己的围栏字段复核一次，两步都命中才算「本次交付已成立」。
    let private confirmedFence
        (durable: AgentJournal)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId)
        (witness: Wanxiangshu.Host.TaskDeliveryWitness)
        : Fence option =
        match witness.Fence.PhysicalUserMessageId = physical with
        | false -> None
        | true ->
            TaskDeliveryJournal.tryFindByFenceFields
                durable
                sessionId
                witness.Fence.ProviderRun
                (Some witness.Fence.ToolCallId)
                None
                (Some physical)
                // 这条见证自身的围栏已登记逻辑运行与交付任务：
                // 用它们做同一围栏的复核，别的逻辑运行 / 别的交付任务的
                // 见证不得满足「本次交付已成立」的判定。
                (Some witness.Fence.LogicalRunId)
                (Some witness.Fence.Task)
            |> Option.map (fun confirmed ->
                { SessionId = sessionId
                  ProviderRun = confirmed.Fence.ProviderRun
                  CallId = confirmed.Fence.ToolCallId })

    let tryFindDurableEstablished
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (physical: PhysicalUserMessageId option)
        : Fence option =
        match journal, physical with
        | Some durable, Some physical ->
            (AgentJournal.snapshot durable).AgentProjections.Deliveries
            |> Map.toList
            |> List.filter (fun ((session, _, _), _) -> session = sessionId)
            |> List.choose (fun (_, witness) -> confirmedFence durable sessionId physical witness)
            // 同一条物理输入下有多条见证时取映射序的最后一条（确定），裁决只需
            // 知道「存在一次已成立的交付」，围栏只用来登记本次中断的事实。
            |> List.tryLast
        | _ -> None

    /// 本 session 是否存在任何一次已成立的交付（durable 见证口径，不绑定物理
    /// 输入）。进程内台账之外的第二份证据：进程重启后台账为空，见证仍在。
    let hasDurableEstablished (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | Some durable ->
            (AgentJournal.snapshot durable).AgentProjections.Deliveries
            |> Map.exists (fun key _ ->
                let (session, _, _) = key
                session = sessionId)
        | None -> false

    /// 中断「已发出」：只登记请求已经发出，不表示中断已生效。
    let recordInterruptIssued fence (issued: Task) =
        update fence (fun current ->
            { current with
                Interrupt = { current.Interrupt with Issued = true }
                InterruptTask = Some issued })

    /// 中断「已观察到」：只有在这里才承认中断的结算结果。
    let recordInterruptObserved fence outcome =
        update fence (fun current ->
            { current with
                Interrupt =
                    { current.Interrupt with
                        Observed = Some outcome } })

    let interruptFacts fence =
        lock gate (fun () ->
            match records.TryGetValue fence with
            | true, record -> record.Interrupt
            | _ -> emptyFacts)

    /// 本 session 精确 run 的自有中断任务（对照 LoopSensor.ActiveInterruptTask）：
    /// 观察者等待的是自己发起的那个中断任务，run 不匹配时等待不到别的。
    let activeInterruptTask sessionId providerRun =
        lock gate (fun () ->
            records.Values
            |> Seq.filter (fun record ->
                record.Fence.SessionId = sessionId
                && record.Fence.ProviderRun = providerRun
                && record.InterruptTask.IsSome)
            |> Seq.sortBy (fun record -> record.Sequence)
            |> Seq.toList
            |> List.rev
            |> List.tryHead
            |> Option.bind (fun record -> record.InterruptTask))

    /// 会话释放时丢弃该 session 的全部交付事实。
    let dropSession sessionId =
        lock gate (fun () ->
            for key in
                records.Keys
                |> Seq.filter (fun fence -> fence.SessionId = sessionId)
                |> Seq.toArray do
                records.Remove key |> ignore)

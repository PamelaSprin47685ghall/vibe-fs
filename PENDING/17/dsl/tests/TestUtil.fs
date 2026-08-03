// Meditator DSL — 第 1 层测试骨架（VERIFY-001/002：纯函数测试，先于资源契约与 Fake Host）。
// 自含断言（check 系列），经 xunit [<Fact>] 接入 dotnet test（Tests.Entry.fs）；
// 失败计数非零 → 对应 Fact 断言失败。Program.fs 保留控制台入口（dotnet run 同语义）。
// 编译顺序：本目录按 fsproj 内 Compile 顺序。
module Meditator.Tests.TestUtil

open System
open System.Collections.Generic
open System.Text
open System.Threading
open System.Threading.Tasks
open Meditator.Meditation
open Meditator.Kernel
open Meditator.Stop
open Meditator.Report

let mutable failures = 0
let mutable passed = 0

let check (name: string) (cond: bool) : unit =
    if cond then
        passed <- passed + 1
        printfn "  PASS %s" name
    else
        failures <- failures + 1
        printfn "  FAIL %s" name

let checkEq (name: string) (expected: 'a) (actual: 'a) : unit =
    check $"{name} (expected {expected}, actual {actual})" (expected = actual)

let checkSome (name: string) (value: 'a option) : unit =
    match value with
    | Some _ -> check name true
    | None -> check name false

// ── canonical 行字段解析（V:P:R:Q:I:D:E 长度前缀；与 EventCodec 同构，测试侧只读不写）。

let private fieldTags = [ "V"; "P"; "R"; "Q"; "I"; "D"; "E" ]

let private collectFields (line: string) : (string * string) list =
    let rec go (remaining: string list) (s: string) (acc: (string * string) list) : (string * string) list =
        match remaining with
        | [] -> List.rev acc
        | t :: rest ->
            let prefix = t + ":"

            if not (s.StartsWith(prefix, StringComparison.Ordinal)) then
                failwith $"collectFields: missing tag {t} in {line}"

            let after = s.Substring(prefix.Length)

            match after.IndexOf(':', StringComparison.Ordinal) with
            | -1 -> failwith $"collectFields: no length for tag {t}"
            | i ->
                match Int32.TryParse(after.Substring(0, i)) with
                | true, len when len >= 0 && i + 1 + len <= after.Length ->
                    let value = after.Substring(i + 1, len)
                    go rest (after.Substring(i + 1 + len)) ((t, value) :: acc)
                | _ -> failwith $"collectFields: bad length for tag {t}"

    go fieldTags line []

let field (line: string) (tag: string) : string option =
    collectFields line
    |> List.tryPick (fun (t, v) -> if t = tag then Some v else None)

let eventIdOf (line: string) : string =
    match field line "I" with
    | Some id -> id
    | None -> failwith "no EventId field in line"

/// 同 EventId 异字节：替换指定字段的值（I 不动），模拟 S2 冲突或行损坏。
let replaceField (line: string) (tag: string) (newValue: string) : string =
    let encode (t: string) (v: string) =
        $"{t}:{Encoding.UTF8.GetByteCount v}:{v}"

    collectFields line
    |> List.map (fun (t, v) -> if t = tag then encode t newValue else encode t v)
    |> String.concat ""

// ── 内存 Journal：实现 IMeditationJournal 并支持故障注入。
// 故障轴（正交）：
//   crashAfterAppend    —— 行已写、fold 前抛异常（模拟崩溃在 append 后 fold 前）
//   unknownFirstAppend  —— 首个 append 返回 CommitUnknown（行实际已写）
//   neverReconcile      —— Reconcile 恒返回 StillUnknown（无法确认 → fail closed 阻塞）
type InMemoryJournal
    (
        seed: string list,
        ?crashAfterAppend: bool,
        ?unknownFirstAppend: bool,
        ?neverReconcile: bool,
        ?unknownEveryAppend: bool
    ) =
    let lines = ResizeArray<string>(seed)
    let accepted = Dictionary<string, string>()
    let crashAfterAppend = defaultArg crashAfterAppend false
    let unknownFirstAppend = defaultArg unknownFirstAppend false
    let unknownEveryAppend = defaultArg unknownEveryAppend false
    let neverReconcile = defaultArg neverReconcile false
    let mutable firstAppend = true

    member _.Lines = lines |> Seq.toList
    member _.LineCount = lines.Count

    member _.SeedAccepted(key: string, value: string) : unit = accepted.[key] <- value

    member _.TryGetAccepted(key: string) : string option =
        match accepted.TryGetValue key with
        | true, v -> Some v
        | _ -> None

    interface IMeditationJournal with
        member _.Replay(_ct: CancellationToken) : Task<string list> = Task.FromResult(lines |> Seq.toList)

        member _.Append (expectedRevision: int) (line: string) (_ct: CancellationToken) : Task<AppendOutcome> =
            task {
                let id = eventIdOf line

                // 139 版：幂等顺序统一——同 EventId 同字节 → AlreadyCommitted（重试语义），
                // 优先于 revision 检查；同 EventId 异字节 → Conflict；均无 → revision 检查后追加。
                match lines |> Seq.tryFind (fun l -> eventIdOf l = id) with
                | Some existing when existing = line -> return AppendOutcome.AlreadyCommitted
                | Some _ -> return AppendOutcome.Conflict
                | None ->
                    if lines.Count <> expectedRevision then
                        return AppendOutcome.WrongExpectedRevision lines.Count
                    else if crashAfterAppend then
                        lines.Add line
                        return raise (System.Exception "SIMULATED CRASH: line appended but not folded")
                    elif (unknownFirstAppend && firstAppend) || unknownEveryAppend then
                        firstAppend <- false
                        lines.Add line
                        return AppendOutcome.CommitUnknown
                    else
                        lines.Add line
                        return AppendOutcome.Committed
            }

        member _.Reconcile (eventId: string) (expectedLine: string) (_ct: CancellationToken) : Task<ReconcileOutcome> =
            task {
                if neverReconcile then
                    return ReconcileOutcome.StillUnknown
                else
                    // 139 版：expectedLine 参与——同 EventId 异字节 → Conflict（不是"确认本次写入"）。
                    match lines |> Seq.tryFind (fun l -> eventIdOf l = eventId) with
                    | Some stored when stored = expectedLine ->
                        return ReconcileOutcome.Reconciled AppendOutcome.Committed
                    | Some _ -> return ReconcileOutcome.Reconciled AppendOutcome.Conflict
                    | None -> return ReconcileOutcome.StillUnknown
            }

    /// P0-2：accepted transcript 专用存储（与事件 journal 分离）。
    interface IAcceptedTranscriptStore with
        member _.TryGet key =
            match accepted.TryGetValue key with
            | true, v -> Some v
            | _ -> None

        member _.PutIfAbsent(key, transcript) =
            match accepted.TryGetValue key with
            | true, existing when existing = transcript -> TranscriptPutOutcome.AlreadyStored
            | true, _ -> TranscriptPutOutcome.TranscriptConflict
            | false, _ ->
                accepted.[key] <- transcript
                TranscriptPutOutcome.Stored

/// 运行 meditate（同步展开，测试专用）。
/// 类型标注用 open 引入的短名：F# 的 open 只引入模块内容，不引入模块短名
/// （`Stop.ExitProver` 需要 `open Meditator` 才可用）。
let runMeditation
    (env: MeditationEnvironment)
    (intent: MeditationIntent)
    (execute: ObligationExecutor)
    (provers: ExitProver list)
    (compileCanonical: ReportCompiler)
    : Result<MeditationReport, MeditationStop> =
    (meditate execute provers compileCanonical intent) env CancellationToken.None
    |> fun t -> t.GetAwaiter().GetResult()

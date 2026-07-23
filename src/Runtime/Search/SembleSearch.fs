module Wanxiangshu.Runtime.SembleSearch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.OpencodeReadSynth
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SembleSearchTypes

type SembleResult = SembleSearchTypes.SembleResult
let debugEnabled = SembleSearchTypes.debugEnabled
let trace = SembleSearchTypes.trace

let private breakpointsKey = "wanxiangshu.semble_breakpoints"

let private getBreakpoints (scope: RuntimeScope) : Map<string, int> =
    match scope.TryFindKey breakpointsKey with
    | Some v -> unbox<Map<string, int>> v
    | None -> Map.empty

let private setBreakpoints (scope: RuntimeScope) (breakpoints: Map<string, int>) : unit =
    scope.Add(breakpointsKey, box breakpoints)

let dumpInjection
    (sessionID: string)
    (agent: string)
    (context: string)
    (results: SembleResult list)
    (pairCount: int)
    : unit =
    if not (debugEnabled ()) then
        ()
    else
        let resultLines =
            results
            |> List.mapi (fun i r -> $"  [{i}] {r.filePath}:{r.startLine}-{r.endLine} score={r.score}")
            |> String.concat "\n"

        let ctxHead = context.[.. min 199 (context.Length - 1)]

        let detail =
            $"session={sessionID} agent={agent} pairs={pairCount} ctxLen={context.Length}\n"
            + $"  ctx: {ctxHead}\n"
            + resultLines

        trace "INJECT" detail

let private shortGuid () =
    let g = System.Guid.NewGuid().ToString("N")
    g.[..7]

let buildReadToolParts (assistantId: string) (sessionID: string) (results: SembleResult list) : obj array =
    results
    |> List.mapi (fun i r ->
        let g = shortGuid ()
        let limit = max 1 (r.endLine - r.startLine + 1)

        let input =
            createObj
                [ "filePath", box r.filePath
                  "offset", box r.startLine
                  "limit", box limit ]

        let state = buildCompletedReadState r.filePath r.content r.startLine r.totalLines input

        box (
            createObj
                [ "type", box "tool"
                  "tool", box "read"
                  // Use a normal read tool callID prefix instead of "semble-call-"
                  // so the LLM cannot distinguish synthetic reads from real ones.
                  "callID", box $"rd-{g}"
                  "id", box $"prt_{g}"
                  "sessionID", box sessionID
                  "messageID", box assistantId
                  "state", box state ]
        ))
    |> Array.ofList

let isBreakpoint (final: obj array) : bool =
    if final.Length = 0 then
        false
    else
        let last = final.[final.Length - 1]
        let info = Dyn.get last "info"
        Dyn.str info "role" = "toolResult"

let breakpointStart (scope: RuntimeScope) (sessionID: string) : int option =
    Map.tryFind sessionID (getBreakpoints scope)

let markBreakpoint (scope: RuntimeScope) (sessionID: string) (index: int) : unit =
    let breakpoints = getBreakpoints scope
    setBreakpoints scope (Map.add sessionID index breakpoints)

let clearBreakpoint (scope: RuntimeScope) (sessionID: string) : unit =
    let breakpoints = getBreakpoints scope
    setBreakpoints scope (Map.remove sessionID breakpoints)

let extractContextFromMessages (startIndex: int) (messages: Message<'raw> list) : string =
    let rec safeSkip n xs =
        if n <= 0 then
            xs
        else
            match xs with
            | [] -> []
            | _ :: t -> safeSkip (n - 1) t

    safeSkip startIndex messages
    |> List.collect (fun m ->
        match m.info.role with
        | User
        | Assistant ->
            m.parts
            |> List.collect (fun part ->
                match part with
                | TextPart t when t <> "" -> [ t ]
                | RawPart raw ->
                    let r = box raw

                    if Dyn.str r "type" = "reasoning" then
                        let txt = Dyn.str r "text"
                        if txt <> "" then [ txt ] else []
                    else
                        []
                | _ -> [])
        | _ -> [])
    |> String.concat "\n"
    |> fun s -> s.Trim()

let setClientForTest (c: SembleSearchClient.Client option) : unit = SembleSearchClient.setClientForTest c

let search (query: string) (repoPath: string) (topK: int) : JS.Promise<SembleResult list> =
    SembleSearchClient.search query repoPath topK

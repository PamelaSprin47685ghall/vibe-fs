module Wanxiangshu.Shell.SembleSearch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleSearchTypes

type SembleResult = SembleSearchTypes.SembleResult
let debugEnabled = SembleSearchTypes.debugEnabled
let trace = SembleSearchTypes.trace

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

        box (
            createObj
                [ "type", box "tool"
                  "tool", box "read"
                  "callID", box $"semble-call-{g}"
                  "id", box $"prt_{g}"
                  "sessionID", box sessionID
                  "messageID", box assistantId
                  "state",
                  box (
                      createObj
                          [ "status", box "completed"
                            "input",
                            box (
                                createObj [ "filePath", box r.filePath; "offset", box r.startLine; "limit", box 2000 ]
                            )
                            "output", box (formatReadOutput r.filePath r.content r.startLine (Some r.totalLines))
                            "title", box $"Read {r.filePath}"
                            "metadata",
                            box (
                                createObj
                                    [ "preview", box true
                                      "truncated", box false
                                      "loaded", box true
                                      "display", box true ]
                            )
                            "time",
                            box (
                                let t = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() in
                                createObj [ "start", box t; "end", box (t + 1L) ]
                            ) ]
                  ) ]
        ))
    |> Array.ofList

let isBreakpoint (final: obj array) : bool =
    if final.Length = 0 then
        false
    else
        let last = final.[final.Length - 1]
        let info = Dyn.get last "info"
        Dyn.str info "role" = "toolResult"

let mutable private lastBreakpoint: Map<string, int> = Map.empty

let breakpointStart (sessionID: string) : int option = Map.tryFind sessionID lastBreakpoint

let markBreakpoint (sessionID: string) (index: int) : unit =
    lastBreakpoint <- Map.add sessionID index lastBreakpoint

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

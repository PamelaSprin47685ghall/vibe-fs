module Wanxiangshu.Hosts.Opencode.SembleInjection

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.SembleMcp
open Wanxiangshu.Runtime.SembleSearch

let private getBreakpointState (agent: string) (sessionID: string) (messages: Message<obj> list) (encodedLen: int) =
    if agent <> "inspector" && agent <> "reviewer" then
        SembleSearch.markBreakpoint sessionID encodedLen
        Error encodedLen
    else
        match SembleSearch.breakpointStart sessionID with
        | None ->
            SembleSearch.markBreakpoint sessionID encodedLen
            SembleMcp.trace "DECIDE" $"reseed: no prior breakpoint, skip this turn (agent={agent}, len={encodedLen})"
            Error encodedLen
        | Some stored when stored > List.length messages ->
            SembleSearch.markBreakpoint sessionID encodedLen
            SembleMcp.trace "DECIDE" $"reseed: breakpoint {stored} > len {List.length messages}, compaction reset"
            Error encodedLen
        | Some startIndex ->
            let context = SembleSearch.extractContextFromMessages startIndex messages

            if context.Length = 0 then
                SembleMcp.trace "DECIDE" $"skip: empty context (start={startIndex}, len={encodedLen})"
                Error encodedLen
            else
                Ok context

let private findLastAssistant (encoded: obj array) =
    let rec loop i =
        if i < 0 then
            None
        else
            let m = encoded.[i]
            let role = Wanxiangshu.Runtime.Dyn.str (Wanxiangshu.Runtime.Dyn.get m "info") "role"
            if role = "assistant" then Some m else loop (i - 1)

    loop (encoded.Length - 1)

let private performSembleSearchAndAttach
    (directory: string)
    (agent: string)
    (sessionID: string)
    (encoded: obj array)
    (context: string)
    : JS.Promise<obj array> =
    promise {
        let! results = SembleSearch.search context directory 3

        if results.IsEmpty then
            SembleMcp.trace "DECIDE" $"skip: no results (context len={context.Length}, len={encoded.Length})"
            return encoded
        else
            match findLastAssistant encoded with
            | None ->
                SembleMcp.trace "DECIDE" "skip: no assistant to attach reads"
                return encoded
            | Some lastAssistant ->
                let assistantId =
                    Wanxiangshu.Runtime.Dyn.str (Wanxiangshu.Runtime.Dyn.get lastAssistant "info") "id"

                let newToolParts = SembleSearch.buildReadToolParts assistantId sessionID results

                if Array.isEmpty newToolParts then
                    SembleMcp.trace "DECIDE" "skip: no tool parts"
                    return encoded
                else
                    let originalParts = Wanxiangshu.Runtime.Dyn.get lastAssistant "parts"

                    let cleaned =
                        if
                            Wanxiangshu.Runtime.Dyn.isNullish originalParts
                            || not (Wanxiangshu.Runtime.Dyn.isArray originalParts)
                        then
                            [||]
                        else
                            (originalParts :?> obj array)
                            |> Array.filter (fun p ->
                                not ((Wanxiangshu.Runtime.Dyn.str p "callID").StartsWith("semble-call-")))

                    lastAssistant?parts <- box (Array.append cleaned newToolParts)
                    SembleSearch.markBreakpoint sessionID encoded.Length
                    SembleSearch.dumpInjection sessionID agent context results newToolParts.Length
                    return encoded
    }

let injectSembleIntoEncoded
    (directory: string)
    (agent: string)
    (sessionID: string)
    (encoded: obj array)
    : JS.Promise<obj array> =
    promise {
        let messages = MessagingCodec.decodeMessages encoded

        match getBreakpointState agent sessionID messages encoded.Length with
        | Error _ -> return encoded
        | Ok context -> return! performSembleSearchAndAttach directory agent sessionID encoded context
    }

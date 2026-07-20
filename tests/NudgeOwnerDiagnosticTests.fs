module Wanxiangshu.Tests.NudgeOwnerDiagnosticTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ReviewRuntime

[<Import("default", "fs")>]
let private nodeFs: obj = jsNative

[<Emit("process.env[$0]")>]
let private getEnv (key: string) : string = jsNative

[<Emit("process.env[$0] = $1")>]
let private setEnv (key: string) (value: string) : unit = jsNative

let private countOccurrences (substr: string) (s: string) : int =
    if s = "" || substr = "" then
        0
    else
        (s.Length - s.Replace(substr, "").Length) / substr.Length

/// N-06: production owner inference failure is a diagnostic state, not a
/// silent permanent NoOwner dead zone. Must emit durable nudge_owner_unknown
/// with structured feature/session/reason fields.
let resolveOwner_emitsNudgeOwnerUnknownInProduction () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-unknown-"
        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        let sessionID = "s-owner-unknown"

        let! owner = resolveOwner ctx runtime sessionID false (createObj []) "session.idle"

        equal "owner is NoOwner" SessionOwner.NoOwner owner

        let! contentOpt = tryReadFileAsync (dir + "/.wanxiangshu.ndjson")

        match contentOpt with
        | None -> failwith "Expected .wanxiangshu.ndjson to be created"
        | Some content ->
            check "event kind nudge_owner_unknown" (content.Contains("nudge_owner_unknown"))
            check "session field present" (content.Contains("\"session\":\"" + sessionID + "\""))
            check "feature nudge present" (content.Contains("\"feature\":\"nudge\""))
            check "reason field present" (content.Contains("\"reason\""))
            check "event field present" (content.Contains("\"event\":\"nudge_owner_unknown\""))

        do! rmAsync dir
    }

/// N-06: known owner is returned as-is; no owner-unknown diagnostic noise.
let resolveOwner_knownOwnerSkipsDiagnostic () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-known-"
        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        let sessionID = "s-owner-known"

        runtime.UpdateSession(
            sessionID,
            Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure.transferOwnership SessionOwner.Human
        )

        let! owner = resolveOwner ctx runtime sessionID false (createObj []) "session.idle"
        equal "owner is Human" SessionOwner.Human owner

        let! contentOpt = tryReadFileAsync (dir + "/.wanxiangshu.ndjson")

        match contentOpt with
        | None -> check "no diagnostic file when owner known" true
        | Some content -> check "no owner_unknown when owner known" (not (content.Contains("nudge_owner_unknown")))

        do! rmAsync dir
    }

let resolveOwner_productionNoOwnerDoubleIdleEmitsDiagnosticOnce () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-double-"
        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        let sessionID = "s-double-idle"
        runtime.GetOrCreateState(sessionID) |> ignore

        let host = Wanxiangshu.Kernel.HostTools.opencode
        let reviewStore = createReviewStore ()

        let trigger =
            Wanxiangshu.Hosts.Opencode.NudgeTrigger.createNudgeTrigger
                host
                ctx
                runtime
                reviewStore
                (fun _ -> ())
                (fun _ -> ())
                (fun _ -> false)

        let props = createObj [ "sessionID" ==> sessionID; "generation" ==> 0 ]

        let envelope1 =
            { EventType = "session.status"
              Props = createObj [ "sessionID" ==> sessionID; "status" ==> "idle"; "generation" ==> 0 ] }

        let envelope2 =
            { EventType = "session.idle"
              Props = props }

        do! trigger.HandleNaturalStop(Some envelope1)
        do! trigger.HandleNaturalStop(Some envelope2)

        let! contentOpt = tryReadFileAsync (dir + "/.wanxiangshu.ndjson")

        match contentOpt with
        | None -> failwith "Expected .wanxiangshu.ndjson to be created"
        | Some content ->
            let lines =
                content.Split('\n') |> Array.filter (fun s -> s.Contains("nudge_owner_unknown"))

            let count = lines.Length
            equal "nudge_owner_unknown emitted exactly once" 1 count

        do! rmAsync dir
    }

let resolveOwner_productionRestoresHumanOwnerFromEventLog () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-human-"
        let sessionID = "s-human-from-log"

        do! appendHumanTurnStartedOrFail dir sessionID "t-1" "openai" "gpt-4" "default" "agent" 1 "msg-1"

        let mockClient =
            createObj
                [ "messages"
                  ==> (fun _ ->
                      Promise.lift (
                          box
                              {| data =
                                  [| createObj
                                         [ "info"
                                           ==> createObj [ "role" ==> "user"; "synthetic" ==> false; "id" ==> "msg-1" ] ]
                                     createObj
                                         [ "info"
                                           ==> createObj
                                                   [ "role" ==> "assistant"; "parentID" ==> "msg-1"; "id" ==> "msg-2" ] ] |] |}
                      )) ]

        let ctx = createObj [ "directory", box dir; "client", mockClient ]
        let runtime = FallbackRuntimeStore()
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Runtime.RuntimeScope.RuntimeScope()
        scope.Add("fallbackRuntime", runtime)
        let host = Wanxiangshu.Kernel.HostTools.opencode

        do! Wanxiangshu.Runtime.EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated host reviewStore scope dir

        let! owner = resolveOwner ctx runtime sessionID false (createObj []) "session.idle"
        equal "owner is Human after event log restore" SessionOwner.Human owner

        do! rmAsync dir
    }

let resolveOwner_restoreFailureEmitsSessionRestoreFailed () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-restore-fail-"
        let sessionID = "s-restore-fail"

        // 创建同名目录，强制引发 EISDIR 异常以进入 error 块
        let! _ = unbox<JS.Promise<unit>> (nodeFs?promises?mkdir (dir + "/.wanxiangshu.ndjson"))

        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        runtime.GetOrCreateState(sessionID) |> ignore
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Runtime.RuntimeScope.RuntimeScope()
        scope.Add("fallbackRuntime", runtime)
        let host = Wanxiangshu.Kernel.HostTools.opencode

        let errors = ResizeArray<string>()

        emitJsStatement
            (errors)
            "
            var prev = console.error;
            console.error = function(arg) {
                if (arg && arg.event === 'session_restore_failed') {
                    errors.push(arg.event);
                }
                prev.apply(console, arguments);
            };
            globalThis.__restore_console = prev;
        "

        try
            do!
                Wanxiangshu.Runtime.EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated host reviewStore scope dir
                |> Promise.catch (fun _ -> ())
        finally
            emitJsStatement
                ()
                "
                if (globalThis.__restore_console) {
                    console.error = globalThis.__restore_console;
                    delete globalThis.__restore_console;
                }
            "

        let session = runtime.GetSession sessionID
        equal "owner is RecoveryRequired on restore failure" FallbackLifecycle.RecoveryRequired session.Core.Lifecycle
        check "session_restore_failed event written" (errors.Contains("session_restore_failed"))

        do! rmAsync dir
    }

let run () =
    promise {
        do! resolveOwner_emitsNudgeOwnerUnknownInProduction ()
        do! resolveOwner_knownOwnerSkipsDiagnostic ()
        do! resolveOwner_productionNoOwnerDoubleIdleEmitsDiagnosticOnce ()
        do! resolveOwner_productionRestoresHumanOwnerFromEventLog ()
        do! resolveOwner_restoreFailureEmitsSessionRestoreFailed ()
    }

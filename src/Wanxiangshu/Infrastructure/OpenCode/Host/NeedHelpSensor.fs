namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// HOST-027: process-local exact-sentinel sensor over reasoning deltas.
/// It owns only stream suffixes + armed attempt identities; business escalation
/// begins after the physical abort has reconciled.
type NeedHelpSensor(isOwned: SessionId -> bool, abortSession: SessionId -> Task<Result<unit, string>>) =

    let sentinelText = AssistancePrompt.Sentinel

    let gate = obj ()
    let suffixes = Dictionary<string, string>()
    let armed = HashSet<string>()

    let attemptKey (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

    let sessionPrefix (sessionId: SessionId) = SessionId.value sessionId + "\u001f"

    let keepSuffix (text: string) =
        let keep = sentinelText.Length - 1

        if text.Length <= keep then
            text
        else
            text.Substring(text.Length - keep)

    member _.Sentinel = sentinelText

    member _.IsArmed(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () -> armed.Contains(attemptKey sessionId providerRun))

    member _.TryArm(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () -> armed.Add(attemptKey sessionId providerRun))

    /// Claim an assistance abort exactly once for the reconciled provider run.
    /// The stream suffix dies with the attempt so detector memory is bounded by
    /// currently live provider runs rather than session lifetime.
    member _.TryTake(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun
            suffixes.Remove key |> ignore
            armed.Remove key)

    member _.DropAttempt(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun
            suffixes.Remove key |> ignore
            armed.Remove key |> ignore)

    member _.DropSession(sessionId: SessionId) =
        lock gate (fun () ->
            let prefix = sessionPrefix sessionId

            let suffixKeys =
                suffixes.Keys
                |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
                |> Seq.toArray

            let armedKeys =
                armed
                |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
                |> Seq.toArray

            suffixKeys |> Array.iter (fun key -> suffixes.Remove key |> ignore)
            armedKeys |> Array.iter (fun key -> armed.Remove key |> ignore))

    member private this.RequestAbort(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        if this.TryArm(sessionId, providerRun) then
            Diagnostic.emit "needhelp" [ "session_id", SessionId.value sessionId; "result", "armed" ]

            task {
                try
                    match! abortSession sessionId with
                    | Ok() ->
                        Diagnostic.emit "needhelp" [ "session_id", SessionId.value sessionId; "result", "aborted" ]
                    | Error reason ->
                        Diagnostic.emit
                            "needhelp"
                            [ "session_id", SessionId.value sessionId
                              "result", "abort-failed"
                              "provider_error", reason ]
                with ex ->
                    Diagnostic.emit
                        "needhelp"
                        [ "session_id", SessionId.value sessionId
                          "result", "abort-failed"
                          "provider_error", ex.Message ]
            }
            |> ignore

    member this.Observe(raw: obj) =
        match NeedHelpEventCodec.tryDecodeReasoningDelta raw with
        | None -> ()
        | Some delta when not (isOwned delta.SessionId) -> ()
        | Some delta when this.IsArmed(delta.SessionId, delta.ProviderRun) -> ()
        | Some delta ->
            let hit =
                lock gate (fun () ->
                    let key = attemptKey delta.SessionId delta.ProviderRun

                    let previous =
                        match suffixes.TryGetValue key with
                        | true, value -> value
                        | false, _ -> ""

                    let combined = previous + delta.Delta
                    let found = combined.Contains(sentinelText, StringComparison.Ordinal)
                    suffixes.[key] <- keepSuffix combined
                    found)

            if hit then
                this.RequestAbort(delta.SessionId, delta.ProviderRun)

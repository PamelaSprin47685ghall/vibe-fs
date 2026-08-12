namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// HOST-027: process-local exact-sentinel sensor over reasoning deltas.
/// It owns only stream part identity, rolling suffixes, and armed attempt
/// identities; business escalation begins after the physical abort reconciles.
type NeedHelpSensor(isOwned: SessionId -> bool, abortSession: SessionId -> Task<Result<unit, string>>) =

    let sentinelText = AssistancePrompt.Sentinel

    let gate = obj ()
    let suffixes = Dictionary<string, string>()
    let reasoningParts = HashSet<string>()
    let armed = HashSet<string>()

    let attemptKey (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

    let sessionPrefix (sessionId: SessionId) = SessionId.value sessionId + "\u001f"

    let partPrefix (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        attemptKey sessionId providerRun + "\u001f"

    let partKey (sessionId: SessionId) (providerRun: ProviderRunIdentity) (partId: string) =
        partPrefix sessionId providerRun + partId

    let removePartsByPrefix prefix =
        reasoningParts
        |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
        |> Seq.toArray
        |> Array.iter (fun key -> reasoningParts.Remove key |> ignore)

    let keepSuffix (text: string) =
        let keep = sentinelText.Length - 1

        if text.Length <= keep then
            text
        else
            text.Substring(text.Length - keep)

    member _.Sentinel = sentinelText

    member _.IsArmed(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () -> armed.Contains(attemptKey sessionId providerRun))

    /// HOST-027: coarse abort ownership is decided by the exact typed armed
    /// attempt, not by the generic MessageAborted transport signal. This query
    /// exists only so HostSignalBootstrap can avoid revoking the idle right of
    /// an abort that this sensor itself requested.
    member _.HasArmedSession(sessionId: SessionId) =
        let prefix = sessionPrefix sessionId

        lock gate (fun () ->
            armed
            |> Seq.exists (fun key -> key.StartsWith(prefix, StringComparison.Ordinal)))

    member _.TryArm(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () -> armed.Add(attemptKey sessionId providerRun))

    /// Claim an assistance abort exactly once for the reconciled provider run.
    /// Stream state dies with the attempt so detector memory is bounded by
    /// currently live provider runs rather than session lifetime.
    member _.TryTake(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun
            suffixes.Remove key |> ignore
            removePartsByPrefix (partPrefix sessionId providerRun)
            armed.Remove key)

    member _.DropAttempt(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun
            suffixes.Remove key |> ignore
            removePartsByPrefix (partPrefix sessionId providerRun)
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
            removePartsByPrefix prefix
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

    member _.IsReasoningDelta(raw: obj) =
        match NeedHelpEventCodec.tryDecodeDelta raw with
        | None -> false
        | Some delta ->
            NeedHelpEventCodec.isNeedHelpDelta raw
            || lock gate (fun () -> reasoningParts.Contains(partKey delta.SessionId delta.ProviderRun delta.PartId))

    member private this.ObserveDelta(raw: obj, delta: NeedHelpEventCodec.StreamDelta) =
        if
            not (isOwned delta.SessionId)
            || this.IsArmed(delta.SessionId, delta.ProviderRun)
        then
            ()
        elif this.IsReasoningDelta raw then
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

    member this.Observe(raw: obj) =
        match NeedHelpEventCodec.tryDecodePartUpdated raw with
        | Some part when isOwned part.SessionId ->
            lock gate (fun () ->
                let key = partKey part.SessionId part.ProviderRun part.PartId

                if String.Equals(part.PartType, "reasoning", StringComparison.OrdinalIgnoreCase) then
                    reasoningParts.Add key |> ignore
                else
                    reasoningParts.Remove key |> ignore)
        | _ ->
            match NeedHelpEventCodec.tryDecodeDelta raw with
            | Some delta -> this.ObserveDelta(raw, delta)
            | None -> ()

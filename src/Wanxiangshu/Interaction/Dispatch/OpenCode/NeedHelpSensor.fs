namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength

/// Host boundary typed one-shot capability: an assistance abort claim.
/// Observed after abort, consumed exactly once by the owning CE behind fresh idle (SW-017 ②).
/// Carries the exact attempt identity so the claim cannot be confused across provider runs.
[<RequireQualifiedAccess>]
type AssistanceAbortClaim =
    private
        { SessionId: SessionId
          ProviderRun: ProviderRunIdentity }

    static member Create(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        { SessionId = sessionId
          ProviderRun = providerRun }

module AssistanceAbortClaim =
    let sessionId (claim: AssistanceAbortClaim) = claim.SessionId
    let providerRun (claim: AssistanceAbortClaim) = claim.ProviderRun

module NeedHelpSensor =

    let createInterruptiblePredicate
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        : SessionId -> bool =
        fun sessionId ->
            ownedSessions.Contains(SessionId.value sessionId)
            && sessionParents.ContainsKey(SessionId.value sessionId)

    let createEligibilityPredicate
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (strengthRuntime: StrengthRuntime)
        : SessionId -> bool =
        let isOwned sessionId =
            ownedSessions.Contains(SessionId.value sessionId)

        let hasPhysicalParent sessionId =
            sessionParents.ContainsKey(SessionId.value sessionId)

        let isInternalAttemptInterruptible sessionId =
            isOwned sessionId && hasPhysicalParent sessionId

        let isEligibleRole (profile: PromptAuthority.AuthorityExecutionProfile) =
            match profile.CanonicalRole with
            | Role.Blogger
            | Role.Distiller -> false
            | _ -> true

        let isCompanionSession sessionId =
            journal
            |> Option.exists (fun durable ->
                SessionAssociationProjection.isCompanion
                    sessionId
                    (AgentJournal.snapshot durable).AgentProjections.Associations)

        let profileIsEligible sessionId =
            match HostSessionNudge.tryActiveProfile journal sessionId with
            | Some profile -> isEligibleRole profile
            | None -> false

        fun sessionId ->
            if not (isInternalAttemptInterruptible sessionId) then
                false
            elif strengthRuntime.TryFindByReplica sessionId |> Option.isSome then
                false
            elif isCompanionSession sessionId then
                false
            else
                profileIsEligible sessionId

/// HOST-027: process-local exact-sentinel sensor over reasoning deltas.
/// It owns only stream part identity, rolling suffixes, and armed attempt
/// identities; business escalation begins after the physical abort reconciles.
type NeedHelpSensor(isOwned: SessionId -> bool, abortSession: SessionId -> Task<Result<unit, string>>) =

    let sentinelText = AssistancePrompt.Sentinel

    let gate = obj ()
    /// DSL-cross-callback-proof: physical — bounded rolling stream text used only by exact-sentinel detection/cleanup
    // DSL-MUTABLE: resource — rolling suffix registry by attempt key
    let suffixes = Dictionary<string, string>()
    // DSL-MUTABLE: resource — reasoning part identity set
    let reasoningParts = HashSet<string>()
    /// DSL-cross-callback-proof: physical single-flight — one-shot armed attempt mark, consumed as typed AssistanceAbortClaim
    // DSL-MUTABLE: single-flight — one-shot armed attempt mark
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

    /// Observe the typed claim without consuming its idle successor authority.
    member _.TryObserveAssistanceClaim
        (sessionId: SessionId, providerRun: ProviderRunIdentity)
        : AssistanceAbortClaim option =
        lock gate (fun () ->
            if armed.Contains(attemptKey sessionId providerRun) then
                Some(AssistanceAbortClaim.Create(sessionId, providerRun))
            else
                None)

    /// Host boundary: consume the one-shot assistance abort claim for the exact attempt.
    /// Returns Some typed claim exactly once; subsequent calls return None (SW-017 ② one-shot).
    member _.TryConsumeAssistanceClaim
        (sessionId: SessionId, providerRun: ProviderRunIdentity)
        : AssistanceAbortClaim option =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun

            if not (armed.Remove key) then
                None
            else
                suffixes.Remove key |> ignore
                removePartsByPrefix (partPrefix sessionId providerRun)
                Some(AssistanceAbortClaim.Create(sessionId, providerRun)))

    /// Consume and discard any armed claims for the session (used when assistance defers to another typed cause).
    member _.DiscardArmedForSession(sessionId: SessionId) =
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

    member private _.TryArm(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () -> armed.Add(attemptKey sessionId providerRun))

    member private _.RollbackArm(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun
            suffixes.Remove key |> ignore
            removePartsByPrefix (partPrefix sessionId providerRun)
            armed.Remove key |> ignore)

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

    member private _.ReportAbortOutcome(sessionId: SessionId, outcome: Result<unit, string>) =
        match outcome with
        | Ok() -> Diagnostic.emit "needhelp" [ "session_id", SessionId.value sessionId; "result", "aborted" ]
        | Error reason ->
            Diagnostic.emit
                "needhelp"
                [ "session_id", SessionId.value sessionId
                  "result", "abort-failed"
                  "provider_error", reason ]

    member private this.ApplyAbortOutcome
        (sessionId: SessionId, providerRun: ProviderRunIdentity, outcome: Result<unit, string>)
        =
        match outcome with
        | Ok() -> this.ReportAbortOutcome(sessionId, outcome)
        | Error reason ->
            this.RollbackArm(sessionId, providerRun)
            this.ReportAbortOutcome(sessionId, Error reason)

    member private this.AbortAndReport(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        task {
            try
                let! outcome = abortSession sessionId
                this.ApplyAbortOutcome(sessionId, providerRun, outcome)
            with ex ->
                this.RollbackArm(sessionId, providerRun)

                Diagnostic.emit
                    "needhelp"
                    [ "session_id", SessionId.value sessionId
                      "result", "abort-failed"
                      "provider_error", ex.Message ]
        }

    member private this.RequestAbort(sessionId: SessionId, providerRun: ProviderRunIdentity) =
        if this.TryArm(sessionId, providerRun) then
            Diagnostic.emit "needhelp" [ "session_id", SessionId.value sessionId; "result", "armed" ]
            this.AbortAndReport(sessionId, providerRun) |> ignore

    member _.IsReasoningDelta(raw: obj) =
        match NeedHelpEventCodec.tryDecodeDelta raw with
        | None -> false
        | Some delta ->
            NeedHelpEventCodec.isNeedHelpDelta raw
            || lock gate (fun () -> reasoningParts.Contains(partKey delta.SessionId delta.ProviderRun delta.PartId))

    member private this.ProbeReasoningDelta(delta: NeedHelpEventCodec.StreamDelta) =
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

    member private this.ObserveDelta(raw: obj, delta: NeedHelpEventCodec.StreamDelta) =
        let alreadyArmed =
            lock gate (fun () -> armed.Contains(attemptKey delta.SessionId delta.ProviderRun))

        if not (isOwned delta.SessionId) || alreadyArmed then
            ()
        elif this.IsReasoningDelta raw then
            this.ProbeReasoningDelta delta

    member private _.ObserveUpdatedPart(part: NeedHelpEventCodec.PartIdentity) =
        lock gate (fun () ->
            let key = partKey part.SessionId part.ProviderRun part.PartId

            match String.Equals(part.PartType, "reasoning", StringComparison.OrdinalIgnoreCase) with
            | true -> reasoningParts.Add key |> ignore
            | false -> reasoningParts.Remove key |> ignore)

    member this.Observe(raw: obj) =
        match NeedHelpEventCodec.tryDecodePartUpdated raw, NeedHelpEventCodec.tryDecodeDelta raw with
        | Some part, _ when isOwned part.SessionId -> this.ObserveUpdatedPart part
        | _, Some delta -> this.ObserveDelta(raw, delta)
        | _ -> ()

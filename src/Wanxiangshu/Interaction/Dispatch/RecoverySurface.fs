namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Host.Ports

/// Recovery-owned JS boundary. Host transcript evidence is projected here;
/// PromptRecovery's typed claims and outcomes never cross into JavaScript.
[<RequireQualifiedAccess>]
module RecoverySurface =

    /// The JS boundary's view of one session's messages, projected onto the
    /// neutral transcript records. The raw host messages are read exactly as
    /// the Host path reads them; the projection onto the neutral vocabulary is
    /// the only translation that happens here.
    ///
    /// The neutral role vocabulary is closed, while the host role string is
    /// free-form. A role outside the neutral vocabulary is carried as
    /// `System`: the claim law only ever proves a landing on a `user` message,
    /// so such a message could never have been the evidence either way, and
    /// keeping it in the list keeps the recovery tail window — the newest
    /// `RecoveryTailWindow` messages — exactly as wide as it was.
    let private neutralRole (role: string) =
        match role with
        | "user"
        | "assistant"
        | "system" -> role
        | _ -> "system"

    type private Transcript(rawMessages: obj array) =
        interface ITranscriptPort with
            member _.Messages(_sessionId: SessionId) : Task<Result<TranscriptMessage list, HostError>> =
                let projected =
                    SessionSnapshotPort.projectMessages rawMessages
                    |> List.choose (fun message ->
                        match TranscriptPort.project { message with Role = neutralRole message.Role } with
                        | Ok record -> Some record
                        | Error _ -> None)

                Task.FromResult(Ok projected)

    let private outcomeView (value: PromptRecovery.Reconciled) : obj =
        let session = SessionId.value value.SessionId
        let promptKey = PromptKey.value value.PromptKey

        match value.Outcome with
        | PromptRecovery.ClaimOutcome.Proven physical ->
            box
                {| session = session
                   promptKey = promptKey
                   outcome = "Proven"
                   physicalMessageId = box (PhysicalUserMessageId.value physical)
                   hasReceipt = null
                   reason = null |}
        | PromptRecovery.ClaimOutcome.StillPending hasReceipt ->
            box
                {| session = session
                   promptKey = promptKey
                   outcome = "StillPending"
                   physicalMessageId = null
                   hasReceipt = box hasReceipt
                   reason = null |}
        | PromptRecovery.ClaimOutcome.Unreadable reason ->
            box
                {| session = session
                   promptKey = promptKey
                   outcome = "Unreadable"
                   physicalMessageId = null
                   hasReceipt = null
                   reason = box reason |}

    /// Reconcile all currently unsettled claims against raw Host messages. The
    /// same production SessionSnapshotPort projection used by the Host path is
    /// applied before PromptRecovery searches role=user + PromptKey evidence.
    let reconcile (handle: JournalHandle) (rawMessages: obj array) : Task<obj array> =
        task {
            let messages = if isNull rawMessages then [||] else rawMessages
            let transcript = Transcript(messages) :> ITranscriptPort
            let! values = PromptRecovery.reconcile (Some handle.Journal) (Some transcript)
            return values |> List.map outcomeView |> List.toArray
        }

    /// snapshot-unreadable ambiguity state: the Host snapshot port fails, so
    /// production reconcile must report Unreadable without proving or deleting
    /// the claim. The failing port is the only fixture; journal, dispatcher,
    /// and outcome projection all run production code.
    let reconcileWithUnreadableSnapshot (handle: JournalHandle) (reason: string) : Task<obj array> =
        task {
            let transcript =
                { new ITranscriptPort with
                    member _.Messages(_sessionId) =
                        Task.FromResult(Error(HostError.Transport reason)) }

            let! values = PromptRecovery.reconcile (Some handle.Journal) (Some transcript)
            return values |> List.map outcomeView |> List.toArray
        }

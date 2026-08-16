namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Recovery-owned JS boundary. Host transcript evidence is projected here;
/// PromptRecovery's typed claims and outcomes never cross into JavaScript.
[<RequireQualifiedAccess>]
module RecoverySurface =

    type private Snapshot(rawMessages: obj array) =
        interface ISessionSnapshotPort with
            member _.GetMessages(_sessionId) =
                Task.FromResult(Ok(SessionSnapshotPort.projectMessages rawMessages))

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
            let snapshot = Snapshot(messages) :> ISessionSnapshotPort
            let! values = PromptRecovery.reconcile (Some handle.Journal) (Some snapshot)
            return values |> List.map outcomeView |> List.toArray
        }

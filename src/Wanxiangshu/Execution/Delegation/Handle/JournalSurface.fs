namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Delegation.Handle

module JournalSurface =

    let private failure error : obj = box {| ok = false; error = error |}

    let private abandonReason value =
        match value with
        | "ParentCancelled" -> Ok HandleAbandonReason.ParentCancelled
        | "DeadlineExceeded" -> Ok HandleAbandonReason.DeadlineExceeded
        | "HostSessionGone" -> Ok HandleAbandonReason.HostSessionGone
        | "Voided" -> Ok HandleAbandonReason.Voided
        | other -> Error $"unknown abandon reason '{other}'"

    let private workId agent child root : HandleWorkId =
        { Handle = HandleId.Agent(AgentHandleId.create agent)
          ChildSessionId = SessionId.create child
          AuthorityRoot = AuthorityRootUserMessageId.create root }

    let recordAbandon
        (handle: JournalHandle)
        (parent: string)
        (agent: string)
        (child: string)
        (root: string)
        (reasonName: string)
        : Task<obj> =
        task {
            let parentId = SessionId.create parent
            let work = workId agent child root

            match
                abandonReason reasonName,
                HandleProjection.tryWork work (AgentJournal.handleProjection handle.Journal parentId)
            with
            | Error error, _ -> return failure error
            | _, None -> return failure "WorkNotAdmitted"
            | Ok reason, Some _ ->
                let! result =
                    HandleController.abandonWork
                        (AgentJournalPortAdapter.fromAgentJournal handle.Journal)
                        parentId
                        work
                        reason

                return
                    match result with
                    | Ok() -> box {| ok = true |}
                    | Error error -> failure error
        }

    let private lifecycle =
        function
        | Active -> "Active"
        | CompletedAwaitingJoin _ -> "CompletedAwaitingJoin"
        | Retired -> "Retired"
        | Abandoned _ -> "Abandoned"

    /// The durable view of one work unit.
    ///
    /// `lifecycle` is the work's own lifecycle; `lifecycleKind` is the kind of
    /// the **last completion** the work ever accepted (Terminal, SendFailure,
    /// Cancelled) and is `null` while the work has accepted no completion at
    /// all. `completionRef` / `completionDigest` are the durable identity of
    /// that completion — `null` while the work has accepted none.
    ///
    /// These two are the non-waiting observation of a pending work: a work
    /// that has settled nothing carries no digest. They never require waiting
    /// for a completion that may never arrive.
    let private recordView (record: HandleWork) : obj =
        let reason =
            match record.Lifecycle with
            | Abandoned reason -> Some(string reason)
            | _ -> None

        let completionKind =
            record.LastCompletion
            |> Option.map (fun completion -> string completion.Kind)
            |> Option.defaultValue null

        let completionRef =
            record.LastCompletion
            |> Option.bind (fun completion -> completion.CompletionRef)
            |> Option.map BlobRef.value
            |> Option.defaultValue null

        let completionDigest =
            record.LastCompletion
            |> Option.bind (fun completion -> completion.CompletionDigest)
            |> Option.map BlobDigest.value
            |> Option.defaultValue null

        box
            {| lifecycle = lifecycle record.Lifecycle
               abandonReason = reason
               child = SessionId.value record.Work.ChildSessionId
               lifecycleKind = completionKind
               completionRef = completionRef
               completionDigest = completionDigest |}

    let snapshot (handle: JournalHandle) (parent: string) (agent: string) (child: string) (root: string) : obj =
        let parentId = SessionId.create parent
        let projection = AgentJournal.handleProjection handle.Journal parentId

        box
            {| record =
                HandleProjection.tryWork (workId agent child root) projection
                |> Option.map recordView
                |> Option.defaultValue null
               revision = JournalRevision.value (AgentJournal.revision handle.Journal) |> string
               horizonVisible = HandleProjection.horizonVisible projection |> List.length
               views =
                {| joinable =
                    HandleProjection.joinable projection
                    |> List.map (fun record -> HandleId.describe record.Handle)
                    |> List.toArray |} |}

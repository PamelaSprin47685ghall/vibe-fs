namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.Journal

/// Workspace-host-owned shared journal surface.
/// Runtime paths and EventStore capabilities stay opaque; callers observe only
/// identity, keyed Current presence, and append outcomes.
type WorkspaceJournalHandle private (journal: AgentJournal) =
    // DSL-MUTABLE: resource — one-shot shared journal release latch
    let mutable released = false

    member internal _.Journal =
        if released then invalidOp "Workspace journal handle is released"
        journal

    member internal _.Release() =
        if not released then
            released <- true
            SharedAgentJournal.release (Some journal)

    static member internal Create(journal: AgentJournal) = WorkspaceJournalHandle(journal)

[<RequireQualifiedAccess>]
module WorkspaceEventStoreSurface =

    let private errorText (error: FoldRejection) = $"{error.Fact}: {error.Reason}"

    let acquire
        (retiredDirectory: string)
        (commonDirectory: string)
        (processId: int)
        (startedAt: string)
        : Task<obj> =
        task {
            let boot = WorkspaceEventStore.bootPort commonDirectory

            let openJournal runtimeId processIdValue processStartedAt =
                task {
                    let! result = boot.ResumeOrCreate(runtimeId, processIdValue, processStartedAt)

                    match result with
                    | Ok(writer, _, projection) ->
                        return AgentJournal.createFromProjection writer projection
                    | Error error -> return Error error
                }

            let! result =
                SharedAgentJournal.acquire
                    retiredDirectory
                    processId
                    (DateTimeOffset.Parse startedAt)
                    openJournal

            match result with
            | Ok journal -> return box {| ok = true; journal = WorkspaceJournalHandle.Create journal |}
            | Error error -> return box {| ok = false; error = errorText error |}
        }

    let release (handle: WorkspaceJournalHandle) : unit = handle.Release()

    let same (left: WorkspaceJournalHandle) (right: WorkspaceJournalHandle) : bool =
        obj.ReferenceEquals(left.Journal, right.Journal)

    let appendClosed (handle: WorkspaceJournalHandle) (session: string) : Task<obj> =
        task {
            let fact = CompanionFact.CompanionBloggerClosed {| SessionId = SessionId.create session |}
            let! result =
                AgentJournal.appendAgent
                    (StreamId.Session(SessionId.create session))
                    None
                    fact
                    handle.Journal

            match result with
            | Ok projection ->
                let present =
                    AgentProjection.tryFind (SessionId.create session) projection.AgentProjections
                    |> Option.isSome

                return box {| ok = true; session = present |}
            | Error error -> return box {| ok = false; error = error.ToString() |}
        }

    let hasCurrent (commonDirectory: string) : bool =
        match WorkspaceEventStore.tryCurrent commonDirectory with
        | None -> false
        | Some store -> store.TryCurrent("Journal") |> Option.isSome

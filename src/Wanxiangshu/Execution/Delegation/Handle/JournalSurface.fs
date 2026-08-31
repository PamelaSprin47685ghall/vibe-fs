namespace Wanxiangshu.Execution.Delegation

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Delegation.Handle

type HandleJournalResource internal (journal: AgentJournal) =
    member internal _.Journal = journal

    interface IDisposable with
        member _.Dispose() = (journal :> IDisposable).Dispose()

module JournalSurface =

    let private success () : obj = box {| ok = true |}

    let private failure error : obj = box {| ok = false; error = error |}

    let private role value =
        match Roles.tryParseRole value with
        | Some canonicalRole -> Ok canonicalRole
        | None -> Error $"unknown role '{value}'"

    let private abandonReason value =
        match value with
        | "ParentCancelled" -> Ok HandleAbandonReason.ParentCancelled
        | "DeadlineExceeded" -> Ok HandleAbandonReason.DeadlineExceeded
        | "HostSessionGone" -> Ok HandleAbandonReason.HostSessionGone
        | other -> Error $"unknown abandon reason '{other}'"

    let openJournal (commonDir: string) (runtimeId: string) (processId: int) (startedAt: string) : Task<obj> =
        task {
            let store =
                EventStore.createLocal commonDir (Guid.NewGuid().ToString("N")) (CanonicalIntegrator.create ())

            match!
                EventStoreJournalWriter.resumeOrCreate (
                    RuntimeId.create runtimeId,
                    processId,
                    DateTimeOffset.Parse startedAt,
                    store
                )
            with
            | Error rejection -> return failure $"{rejection.Fact}: {rejection.Reason}"
            | Ok(writer, _, projection) ->
                match AgentJournal.createFromProjection writer projection with
                | Error rejection ->
                    writer.Release()
                    return failure $"{rejection.Fact}: {rejection.Reason}"
                | Ok journal ->
                    return
                        box
                            {| ok = true
                               journal = new HandleJournalResource(journal) |}
        }

    let dispose (resource: HandleJournalResource) : unit = (resource :> IDisposable).Dispose()

    let link
        (resource: HandleJournalResource)
        (parentId: string)
        (agentId: string)
        (childId: string)
        (targetAgent: string)
        (roleName: string)
        : Task<obj> =
        task {
            match role roleName with
            | Error error -> return failure error
            | Ok canonicalRole ->
                match!
                    HandleController.link
                        (Some resource.Journal)
                        (SessionId.create parentId)
                        agentId
                        (SessionId.create childId)
                        targetAgent
                        canonicalRole
                        HandleOwnership.DurableParentHandle
                with
                | Ok() -> return success ()
                | Error error -> return failure error
        }

    let recordAbandon
        (resource: HandleJournalResource)
        (parentId: string)
        (agentId: string)
        (reasonName: string)
        (abandonedAt: string)
        : Task<obj> =
        task {
            match abandonReason reasonName with
            | Error error -> return failure error
            | Ok reason ->
                match!
                    HandleController.recordAbandon
                        (Some resource.Journal)
                        (SessionId.create parentId)
                        agentId
                        reason
                        (DateTimeOffset.Parse abandonedAt)
                with
                | Ok() -> return success ()
                | Error error -> return failure error
        }

    let snapshot (resource: HandleJournalResource) (parentId: string) (handle: obj) : obj =
        let projection =
            AgentJournal.handleProjection resource.Journal (SessionId.create parentId)
            |> HandleSurface.HandleProjectionState

        box
            {| record = HandleSurface.read projection handle
               views = HandleSurface.views projection |}

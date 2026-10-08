namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Knowledge.Casebook

module PluginHostWiringSurface =

    let createBoot (input: obj) : Task<obj> =
        task {
            let! boot = PluginBoot.create input
            return box boot
        }

    let bootHasJournal (boot: obj) : bool =
        (unbox<PluginBoot.Boot> boot).Journal.IsSome

    let disposeBoot (boot: obj) : Task =
        (unbox<PluginBoot.Boot> boot).Scope.DisposeAsync()

    let private describeSettlement (settled: CaseFinalizeSettlement) : obj =
        let commitment, reason =
            match settled.Commitment with
            | CaseFinalizeCommitment.Finalized -> "Finalized", null
            | CaseFinalizeCommitment.NothingToFinalize -> "NothingToFinalize", null
            | CaseFinalizeCommitment.NotCommitted reason -> "NotCommitted", reason
            | CaseFinalizeCommitment.Unknown reason -> "Unknown", reason
            | CaseFinalizeCommitment.PhaseConflict reason -> "PhaseConflict", reason
            | CaseFinalizeCommitment.PersistenceFailed _ -> "PersistenceFailed", null

        let failure =
            match settled.Commitment with
            | CaseFinalizeCommitment.PersistenceFailed failure -> CasebookAppendSurface.failureToJs failure
            | _ -> null

        box
            {| identity = settled.Identity.DelegateSessionId
               commitment = commitment
               reason = reason
               releasesIdentity = CaseFinalizeSettlement.releasesIdentity settled
               persistenceFailure = failure |}

    let finalizeDraft (workspaceRoot: string) (store: obj) (delegateSessionId: string) (owner: obj) : Task<obj> =
        task {
            let! settled =
                PluginHostWiring.tryFinalizeWithin
                    (unbox<CasebookSettlementOwner> owner)
                    workspaceRoot
                    (unbox<EventStoreHandle> store).Store
                    delegateSessionId

            return describeSettlement settled
        }

    let finalizeDraftWithBoot (workspaceRoot: string) (store: obj) (delegateSessionId: string) (boot: obj) : Task<obj> =
        finalizeDraft workspaceRoot store delegateSessionId (box (unbox<PluginBoot.Boot> boot).CasebookSettlements)

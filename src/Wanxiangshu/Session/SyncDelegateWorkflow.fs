/// Composition-internal CE module: referenced only by SyncDelegateRuntime.
module internal SyncDelegateWorkflow

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open SyncDelegateWait

/// Everything SyncDelegateRuntime.Invoke needs from its host, so the workflow
/// runs against the store + dependencies without touching runtime fields.
type Dependencies =
    { Attached: AttachedSessionRuntime
      ResolveOwnerTier: SessionId -> AgentTier option
      CreateChild: SessionId -> string -> string option -> Task<Result<SessionId, string>>
      OnDelegateReady: SessionId -> string -> unit
      NoteInspectorPrompt: string -> string -> unit
      CleanupInspectorDraft: string -> unit
      Directory: string option
      SendPrompt: SyncDelegateCall -> string -> Task<Result<unit, string>>
      DescribeWait: SyncDelegateWait -> DiagnosticWait }

/// EXEC-026 / EXEC-031: reusable SyncDelegate CE (Acquire → GetOrCreate → Send →
/// await ordinary Completion / bounded WorkRecord). No return tool / dual-await.
let invoke
    (store: SyncDelegateCallStore)
    (deps: Dependencies)
    (ownerSessionKey: string)
    (role: SyncDelegateRole)
    (message: string)
    : Task<Result<string, string>> =
    task {
        let owner = SessionId.create ownerSessionKey
        let ownerScope = ReuseScope.ofSession owner

        if role = SyncDelegateRole.Inspector then
            let staleDeletedInspector = store.TryTakeDeletedInspector ownerScope

            staleDeletedInspector
            |> Option.iter (fun sessionId -> deps.CleanupInspectorDraft(SessionId.value sessionId))

        match deps.ResolveOwnerTier owner with
        | None -> return Error "sync delegate rejected: owner tier unknown"
        | Some ownerTier ->
            let claimed = store.TryAcquireFlight ownerScope

            if not claimed then
                return Error "sync delegate rejected: another sync delegate call is in flight"
            else
                let tier = SyncDelegate.tierForOwner ownerTier
                let agentName = SyncDelegate.agentNameFor role tier

                try
                    match!
                        deps.Attached.GetOrCreate(
                            owner,
                            role,
                            agentName,
                            deps.Directory,
                            deps.CreateChild,
                            deps.OnDelegateReady
                        )
                    with
                    | Error error ->
                        store.ReleaseFlight ownerScope
                        return Error error
                    | Ok delegateSession ->
                        let call, registration =
                            store.BeginCall(owner, ownerScope, role, delegateSession, agentName)

                        use _registration = registration

                        match! deps.SendPrompt call message with
                        | Error error ->
                            store.FailCall(call, error)
                            store.RemoveCall call
                            store.ReleaseFlight ownerScope
                            return Error error
                        | Ok _ ->
                            if role = SyncDelegateRole.Inspector then
                                deps.NoteInspectorPrompt (SessionId.value delegateSession) message

                            let! answered =
                                CausalAwait.awaitTask
                                    CausalWaitHub.observer
                                    (deps.DescribeWait(DelegateCompletion(owner, delegateSession, role)))
                                    call.Answer.Task

                            store.ReleaseFlight ownerScope
                            return answered
                with ex ->
                    store.ReleaseFlight ownerScope
                    return Error ex.Message
    }

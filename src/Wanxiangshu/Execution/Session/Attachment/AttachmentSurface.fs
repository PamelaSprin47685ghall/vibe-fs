namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for the attached-session owner. The runtime and callback
/// resources stay opaque; callers receive only binding snapshots.
module AttachmentSurface =
    let classifyObservation (observation: string) : obj =
        let existing = SessionId.create "host-child-existing"

        let evidence =
            match observation with
            | "missing" -> AttachedChildObservation.Missing
            | "matching" -> AttachedChildObservation.Matching existing
            | _ -> AttachedChildObservation.Conflicting [ existing; SessionId.create "host-child-conflict" ]

        let decision, children =
            match AttachedChildObservation.decide evidence with
            | AttachedChildDecision.Create -> "Create", [||]
            | AttachedChildDecision.Adopt childId -> "Adopt", [| SessionId.value childId |]
            | AttachedChildDecision.RejectConflict childIds ->
                "RejectConflict", childIds |> List.map SessionId.value |> List.toArray

        box
            {| observation = observation
               decision = decision
               children = children |}

    let scenario (owner: string) (role: string) (firstAgent: string) (secondAgent: string) (usable: bool) : Task<obj> =
        task {
            let roleValue =
                if role = "Coder" then
                    SyncDelegateRole.Coder
                else
                    SyncDelegateRole.Inspector

            if not usable then
                return
                    box
                        {| owner = owner
                           role = SyncDelegate.roleLabel roleValue
                           firstChild = "child-1"
                           firstAgent = firstAgent
                           secondChild = "child-2"
                           secondAgent = secondAgent
                           created = 2 |}
            else
                // DSL-MUTABLE: algorithm-scratch — attachment id counter
                let next = ref 0
                let runtime = AttachedSessionRuntime()

                let createChild
                    (_: SessionId)
                    (_: ReuseScopeId)
                    (_: SyncDelegateRole)
                    (_agent: string)
                    (_directory: string option)
                    : Task<Result<SessionId, string>> =
                    task {
                        next.Value <- next.Value + 1
                        return Ok(SessionId.create (sprintf "child-%d" next.Value))
                    }

                let observeChild (_: SessionId) (_: ReuseScopeId) (_: SyncDelegateRole) (_agent: string) =
                    Task.FromResult(Ok AttachedChildObservation.Missing)

                let bindChild (_: SessionId) (_: SessionId) (_agent: string) = ()
                let onReady (_: SessionId) (_agent: string) = ()

                let! first =
                    runtime.GetOrCreate(
                        SessionId.create owner,
                        roleValue,
                        firstAgent,
                        None,
                        observeChild,
                        createChild,
                        bindChild,
                        onReady
                    )

                let! second =
                    runtime.GetOrCreate(
                        SessionId.create owner,
                        roleValue,
                        secondAgent,
                        None,
                        observeChild,
                        createChild,
                        bindChild,
                        onReady
                    )

                match first, second with
                | Ok firstValue, Ok secondValue ->
                    return
                        box
                            {| owner = owner
                               role = SyncDelegate.roleLabel roleValue
                               firstChild = SessionId.value (fst firstValue)
                               firstAgent = snd firstValue
                               secondChild = SessionId.value (fst secondValue)
                               secondAgent = snd secondValue
                               created = next.Value |}
                | Error firstError, _ ->
                    return
                        box
                            {| owner = owner
                               role = SyncDelegate.roleLabel roleValue
                               error = firstError
                               created = next.Value |}
                | _, Error secondError ->
                    return
                        box
                            {| owner = owner
                               role = SyncDelegate.roleLabel roleValue
                               error = secondError
                               created = next.Value |}
        }

    let reconciliationScenario (observation: string) : Task<obj> =
        task {
            let owner = SessionId.create "owner"
            let agent = "deep-inspector"
            let existing = SessionId.create "host-child-existing"
            let createdCount = ref 0
            let observedCount = ref 0
            let registeredCount = ref 0
            let boundCount = ref 0
            let readyCount = ref 0

            let runtime =
                AttachedSessionRuntime(registerParent = (fun _ _ -> registeredCount.Value <- registeredCount.Value + 1))

            let observeChild (_: SessionId) (_: ReuseScopeId) (_: SyncDelegateRole) (_: string) =
                observedCount.Value <- observedCount.Value + 1

                match observation with
                | "missing" -> Task.FromResult(Ok AttachedChildObservation.Missing)
                | "matching" -> Task.FromResult(Ok(AttachedChildObservation.Matching existing))
                | "conflicting" ->
                    Task.FromResult(
                        Ok(AttachedChildObservation.Conflicting [ existing; SessionId.create "host-child-conflict" ])
                    )
                | _ -> Task.FromResult(Error "host child query failed")

            let createChild (_: SessionId) (_: ReuseScopeId) (_: SyncDelegateRole) (_: string) (_: string option) =
                createdCount.Value <- createdCount.Value + 1
                Task.FromResult(Ok(SessionId.create "host-child-created"))

            let bindChild (_: SessionId) (_: SessionId) (_: string) =
                boundCount.Value <- boundCount.Value + 1

            let onReady (_: SessionId) (_: string) =
                readyCount.Value <- readyCount.Value + 1

            let! outcome =
                runtime.GetOrCreate(
                    owner,
                    SyncDelegateRole.Inspector,
                    agent,
                    None,
                    observeChild,
                    createChild,
                    bindChild,
                    onReady
                )

            let child, error =
                match outcome with
                | Ok(childId, _) -> SessionId.value childId, ""
                | Error detail -> "", detail

            return
                box
                    {| observation = observation
                       observed = observedCount.Value
                       created = createdCount.Value
                       registered = registeredCount.Value
                       bound = boundCount.Value
                       ready = readyCount.Value
                       child = child
                       error = error |}
        }

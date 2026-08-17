namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for the attached-session owner. The runtime and callback
/// resources stay opaque; callers receive only binding snapshots.
module AttachmentSurface =
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
                    (_agent: string)
                    (_directory: string option)
                    : Task<Result<SessionId, string>> =
                    task {
                        next.Value <- next.Value + 1
                        return Ok(SessionId.create (sprintf "child-%d" next.Value))
                    }

                let onReady (_: SessionId) (_agent: string) = ()

                let! first =
                    runtime.GetOrCreate(SessionId.create owner, roleValue, firstAgent, None, createChild, onReady)

                let! second =
                    runtime.GetOrCreate(SessionId.create owner, roleValue, secondAgent, None, createChild, onReady)

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

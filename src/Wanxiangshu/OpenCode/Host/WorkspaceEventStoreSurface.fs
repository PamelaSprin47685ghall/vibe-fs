namespace Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module WorkspaceEventStoreSurface =
    let acquire (commonDir: string) : obj =
        WorkspaceEventStore.acquire commonDir |> box

    let journalCurrent (store: obj) : obj =
        match (unbox<IEventStore> store).TryCurrent "Journal" with
        | None -> box {| available = false; sessions = null |}
        | Some current ->
            let projection = unbox<ProjectionSet> current

            let sessions =
                projection.AgentProjections.Sessions
                |> Map.toArray
                |> Array.map (fun (sessionId, _) -> SessionId.value sessionId)

            box
                {| available = true
                   sessions = sessions |}

    let release (commonDir: string) : unit = WorkspaceEventStore.release commonDir

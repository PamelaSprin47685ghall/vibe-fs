module VibeFs.Shell.KnowledgeGraphMaintenanceRun

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.PromiseQueue

type MaintenanceRunHost =
    { WorkspaceRoot: string
      GetState: unit -> KnowledgeGraphState
      SetState: KnowledgeGraphState -> unit
      Now: unit -> System.DateTime
      TryLaunch: string -> KnowledgeGraphFile list -> KnowledgeGraphProjection -> unit }

let runMaintenanceIfDue (commandQueue: SerialQueue) (host: MaintenanceRunHost) : JS.Promise<unit> =
    let root = host.WorkspaceRoot
    if not (knowledgeGraphDirExists root) then Promise.lift ()
    else
        commandQueue.Enqueue(fun () ->
            promise {
                let! files = readKnowledgeGraphFiles root
                let projection = projectLatestWins files
                let dailyDue = dueMaintenance files (host.Now ())
                dailyDue
                |> List.iter (fun date ->
                    let key, launch = bookkeeperMaintenanceLaunch root date
                    let state = host.GetState ()
                    let first, nextState = recordLaunchOnce state key launch
                    host.SetState nextState
                    if first then host.TryLaunch date files projection)
            })
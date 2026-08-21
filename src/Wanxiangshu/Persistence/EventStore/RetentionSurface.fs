namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module RetentionSurface =

    let readLastCompleteLine (path: string) : string =
        match ProcessEventLog.readLastCompleteLine path with
        | Ok(Some line) -> line
        | Ok None -> null
        | Error error -> failwith error

    let retainedWriterIdsAt (commonDir: string) (nowMs: float) : string[] =
        match ProcessEventLog.readStreamsAt commonDir nowMs with
        | Ok streams -> streams |> List.map fst |> List.toArray
        | Error error -> failwith (sprintf "%A" error)

    let syncAt (repoPath: string) (commonDir: string) (remoteRoot: string) (nowMs: float) : Task<obj> =
        task {
            let raw = ProcessGitRawStore.create repoPath

            let remote =
                if String.IsNullOrWhiteSpace remoteRoot then
                    None
                else
                    Some { RootOid = remoteRoot |> GitObjectId.create |> RootOid.create }

            match! WriterStreamSync.syncWriterStreamsAt raw commonDir remote nowMs with
            | Ok snapshot ->
                return
                    createObj
                        [ "ok" ==> true
                          "root" ==> (snapshot.RootOid |> RootOid.value |> GitObjectId.value) ]
            | Error error -> return createObj [ "ok" ==> false; "error" ==> sprintf "%A" error ]
        }

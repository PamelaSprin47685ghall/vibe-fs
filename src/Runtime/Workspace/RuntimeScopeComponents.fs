namespace Wanxiangshu.Runtime.Workspace

open Fable.Core
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime

type CapsCache() =
    let mutable capsFiles = Map.empty<string, CapsFile list>
    let mutable capsInflight = Map.empty<string, JS.Promise<CapsFile list>>

    member _.TryGetCapsFiles(key: string) : CapsFile list option = Map.tryFind key capsFiles

    member _.AddCapsFilesIfAbsent(key: string, files: CapsFile list) : unit =
        if not (Map.containsKey key capsFiles) then
            capsFiles <- Map.add key files capsFiles

    member _.Clear() : unit =
        capsFiles <- Map.empty
        capsInflight <- Map.empty

    member _.ClearForSession(prefix: string) : unit =
        capsFiles <- capsFiles |> Map.filter (fun k _ -> not (k.StartsWith prefix))

    member _.ClearInflightForSession(prefix: string) : unit =
        capsInflight <- capsInflight |> Map.filter (fun k _ -> not (k.StartsWith prefix))

    member _.GetOrLoadInflight(key: string, load: unit -> JS.Promise<CapsFile list>) : JS.Promise<CapsFile list> =
        match Map.tryFind key capsInflight with
        | Some p -> p
        | None ->
            let p =
                load ()
                |> Promise.map (fun files ->
                    capsInflight <- Map.remove key capsInflight
                    files)
                |> Promise.catch (fun ex ->
                    capsInflight <- Map.remove key capsInflight
                    raise ex)

            capsInflight <- Map.add key p capsInflight
            p

    member _.CapsFileCount = Map.count capsFiles
    member _.CapsInflightCount = Map.count capsInflight


type TempFileRegistry() =
    let mutable tempFilesByPrompt = Map.empty<string, string list>

    member _.Register(prompt: string, files: string list) : unit =
        let key = if isNull prompt then "" else prompt.Trim()

        if key <> "" then
            tempFilesByPrompt <- Map.add key files tempFilesByPrompt

    member _.TryGet(prompt: string) : string list option =
        let key = if isNull prompt then "" else prompt.Trim()
        if key = "" then None else Map.tryFind key tempFilesByPrompt

    member _.ClearForPrompt(prompt: string) : unit =
        let key = if isNull prompt then "" else prompt.Trim()

        if key <> "" then
            tempFilesByPrompt <- Map.remove key tempFilesByPrompt

    member _.TryRemoveForPrompt(prompt: string) : bool =
        let key = if isNull prompt then "" else prompt.Trim()

        if key = "" then
            false
        else
            let existed = Map.containsKey key tempFilesByPrompt
            tempFilesByPrompt <- Map.remove key tempFilesByPrompt
            existed

    member _.RemoveSession(sessionId: string) : unit =
        tempFilesByPrompt <-
            tempFilesByPrompt
            |> Map.filter (fun k _ -> not (k.StartsWith(sessionId + "\u0000")))

    member _.TempFileMapCount = Map.count tempFilesByPrompt


type SessionLockRegistry() =
    let mutable sessionLocks = Map.empty<string, SessionReaderWriterLock>

    member _.GetOrCreate(sessionId: string) : SessionReaderWriterLock =
        match Map.tryFind sessionId sessionLocks with
        | Some l -> l
        | None ->
            let l = SessionReaderWriterLock()
            sessionLocks <- Map.add sessionId l sessionLocks
            l

    member _.Clear() : unit = sessionLocks <- Map.empty

    member _.Remove(sessionId: string) : unit =
        sessionLocks <- Map.remove sessionId sessionLocks

    member _.SessionLockCount = Map.count sessionLocks

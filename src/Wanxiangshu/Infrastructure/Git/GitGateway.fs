namespace Wanxiangshu.Infrastructure.Git

open System
open System.Threading.Tasks
open Wanxiangshu.Infrastructure.Persist

/// Transport / process failure for Wanxiang-initiated Git (§3.1).
[<RequireQualifiedAccess>]
type GitError =
    | Transport of reason: string
    | Failed of exitCode: int * stderr: string

/// Sole Wanxiang-initiated Git transport port (storage.md Phase 3.1).
type IGitGateway =
    abstract Fetch: remote: string -> Task<Result<unit, GitError>>
    abstract Pull: remote: string -> Task<Result<unit, GitError>>
    abstract Push: remote: string * refspec: string -> Task<Result<unit, GitError>>
    abstract ConvergeStore: remote: string -> Task<Result<StoreSnapshot, ConvergeError>>

/// Sync git runner used by GitGateway transport (args after `git`, optional env overlay).
type GitGatewaySyncRunner = string list * Map<string, string> option -> int * string * string

[<RequireQualifiedAccess>]
module GitGateway =
    /// Recursion guard env name — shared with HookDispatcher (Wave B). Do not rename.
    let SyncActiveEnv = "WANXIANG_GIT_SYNC_ACTIVE"

    // Process-local recursion latch for nested ConvergeStore (hooks also check env).
    let private syncActiveDepth = ref 0

    let private asTransport (error: GitError) : ConvergeError =
        match error with
        | GitError.Transport reason -> ConvergeError.Transport reason
        | GitError.Failed(_, stderr) ->
            ConvergeError.Transport(
                if String.IsNullOrWhiteSpace stderr then
                    "git transport failed"
                else
                    stderr.Trim()
            )

    let private failureStdoutStderr stdout stderr =
        if String.IsNullOrWhiteSpace stderr then stdout else stderr

    let private runOk (run: GitGatewaySyncRunner) (args: string list) (env: Map<string, string> option) =
        let code, stdout, stderr = run (args, env)

        if code = 0 then
            Ok()
        else
            Error(GitError.Failed(code, failureStdoutStderr stdout stderr))

    let private withSyncGuard (env: Map<string, string> option) : Map<string, string> option =
        let baseMap =
            match env with
            | Some m -> m
            | None -> Map.empty

        Some(baseMap |> Map.add SyncActiveEnv "1")

    let private readSnapshot (store: IGitRawStore) (refName: string) : StoreSnapshot option =
        store.ReadRef refName
        |> Option.map (fun oid -> { RootOid = RootOid.create oid })

    let private emptySnapshot (store: IGitRawStore) : StoreSnapshot =
        match GitRawStore.materializeSnapshot store [] with
        | Ok snapshot -> snapshot
        | Error err -> failwith (sprintf "empty store materialization failed: %A" err)

    let private localSnapshot (store: IGitRawStore) : StoreSnapshot =
        match readSnapshot store StoreRef.canonical with
        | Some snapshot -> snapshot
        | None -> emptySnapshot store

    let private validateMerged (store: IGitRawStore) (snapshot: StoreSnapshot) : Result<unit, ConvergeError> =
        match EventStoreMergeSpec.merge store (MergeInput.ofList [ snapshot ]) with
        | Error(MergeError.StorageInvalid err) -> Error(ConvergeError.StorageInvalid err)
        | Ok events ->
            match EventStoreFold.validate events with
            | Error(FoldError.StorageInvalid err) -> Error(ConvergeError.StorageInvalid err)
            | Ok() ->
                match PayloadClosure.validatePresent store (PayloadClosure.ofEvents events) with
                | Error err -> Error(ConvergeError.StorageInvalid err)
                | Ok() -> Ok()

    let private expectedOldFor (store: IGitRawStore) (baseSnapshot: StoreSnapshot) : GitObjectId option =
        match store.ReadRef StoreRef.canonical with
        | None -> None
        | Some _ -> Some(RootOid.value baseSnapshot.RootOid)

    let private isMissingRemoteRef (stderr: string) =
        let text = stderr.ToLowerInvariant()

        text.Contains("couldn't find remote ref")
        || text.Contains("does not exist")
        || text.Contains("no such ref")
        || text.Contains("unable to find")

    /// Fetch remote canonical store into local remote-tracking ref (§14).
    /// Missing remote store ref is Ok (Absent) — first publish uses Absent lease.
    let private fetchStoreRef (run: GitGatewaySyncRunner) (remote: string) : Result<unit, GitError> =
        let tracking = StoreRef.remoteTracking remote
        let refspec = sprintf "+%s:%s" StoreRef.canonical tracking
        let code, stdout, stderr = run ([ "fetch"; remote; refspec ], withSyncGuard None)

        if code = 0 then
            Ok()
        elif isMissingRemoteRef (failureStdoutStderr stdout stderr) then
            Ok()
        else
            Error(GitError.Failed(code, failureStdoutStderr stdout stderr))

    let private leasePush
        (run: GitGatewaySyncRunner)
        (remote: string)
        (expectedRemote: GitObjectId option)
        (newOid: GitObjectId)
        : Result<unit, GitError> =
        let dest = sprintf "%s:%s" (GitObjectId.value newOid) StoreRef.canonical

        let leaseArg =
            match expectedRemote with
            | None -> "--force-with-lease=" + StoreRef.canonical + ":"
            | Some oid -> "--force-with-lease=" + StoreRef.canonical + ":" + GitObjectId.value oid

        runOk run [ "push"; leaseArg; remote; dest ] (withSyncGuard None)

    let private observedOrEmpty
        (store: IGitRawStore)
        (remote: string)
        (fallback: StoreSnapshot option)
        : StoreSnapshot * GitObjectId option =
        match store.ReadRef(StoreRef.remoteTracking remote) with
        | Some oid -> { RootOid = RootOid.create oid }, Some oid
        | None ->
            match fallback with
            | Some snap -> snap, Some(RootOid.value snap.RootOid)
            | None -> emptySnapshot store, None

    /// Core bidirectional converge. When `skipFetch`, uses `observedRemote` and does not fetch (§14).
    let private convergeLoop
        (store: IGitRawStore)
        (run: GitGatewaySyncRunner)
        (maxRetries: int)
        (remote: string)
        (skipFetch: bool)
        (initialObserved: StoreSnapshot)
        (initialLeaseExpected: GitObjectId option)
        : Result<StoreSnapshot, ConvergeError> =
        let rec loop (remoteSnap: StoreSnapshot) (leaseExpected: GitObjectId option) (retriesLeft: int) =
            let local = localSnapshot store

            match EventStoreMerge.merge store (MergeInput.ofList [ local; remoteSnap ]) with
            | Error(MergeError.StorageInvalid err) -> Error(ConvergeError.StorageInvalid err)
            | Ok merged ->
                match validateMerged store merged with
                | Error e -> Error e
                | Ok() ->
                    let expectedLocal = expectedOldFor store local
                    let newOid = RootOid.value merged.RootOid

                    let casOk = store.CompareAndSwapRef(StoreRef.canonical, expectedLocal, newOid)

                    if casOk then
                        match leasePush run remote leaseExpected newOid with
                        | Ok() -> Ok merged
                        | Error gitErr ->
                            if retriesLeft <= 0 then
                                if maxRetries <= 0 then
                                    Error ConvergeError.ConvergeCasRejected
                                else
                                    Error ConvergeError.ConvergeRetryExhausted
                            elif skipFetch then
                                // Observed-remote path: do not fetch; surface transport / retry.
                                Error(asTransport gitErr)
                            else
                                match fetchStoreRef run remote with
                                | Error e -> Error(asTransport e)
                                | Ok() ->
                                    let refreshed, nextLease = observedOrEmpty store remote None
                                    loop refreshed nextLease (retriesLeft - 1)
                    elif retriesLeft <= 0 then
                        if maxRetries <= 0 then
                            Error ConvergeError.ConvergeCasRejected
                        else
                            Error ConvergeError.ConvergeRetryExhausted
                    elif skipFetch then
                        // Local lost CAS without fetch: retry against same observed remote + fresh local.
                        loop remoteSnap leaseExpected (retriesLeft - 1)
                    else
                        match fetchStoreRef run remote with
                        | Error e -> Error(asTransport e)
                        | Ok() ->
                            let refreshed, nextLease = observedOrEmpty store remote None
                            loop refreshed nextLease (retriesLeft - 1)

        loop initialObserved initialLeaseExpected maxRetries

    let convergeStore
        (store: IGitRawStore)
        (run: GitGatewaySyncRunner)
        (maxRetries: int)
        (remote: string)
        : Result<StoreSnapshot, ConvergeError> =
        if syncActiveDepth.Value > 0 then
            Ok(localSnapshot store)
        else
            syncActiveDepth.Value <- syncActiveDepth.Value + 1

            try
                match fetchStoreRef run remote with
                | Error e -> Error(asTransport e)
                | Ok() ->
                    let observed, leaseExpected = observedOrEmpty store remote None
                    convergeLoop store run maxRetries remote false observed leaseExpected
            finally
                syncActiveDepth.Value <- syncActiveDepth.Value - 1

    /// Hook-facing helper: reuse already-observed remote-tracking snapshot; no nested fetch (§14).
    let convergeStoreWithObservedRemote
        (store: IGitRawStore)
        (run: GitGatewaySyncRunner)
        (maxRetries: int)
        (remote: string)
        (observedRemote: StoreSnapshot)
        : Result<StoreSnapshot, ConvergeError> =
        if syncActiveDepth.Value > 0 then
            Ok(localSnapshot store)
        else
            syncActiveDepth.Value <- syncActiveDepth.Value + 1

            try
                // Lease expectation comes from remote-tracking when present (hook path).
                // Absent tracking ⇒ Absent lease (first remote publication).
                let leaseExpected = store.ReadRef(StoreRef.remoteTracking remote)
                convergeLoop store run maxRetries remote true observedRemote leaseExpected
            finally
                syncActiveDepth.Value <- syncActiveDepth.Value - 1

    let createWithSyncRunner
        (store: IGitRawStore)
        (repoPath: string)
        (run: GitGatewaySyncRunner)
        (maxRetries: int)
        : IGitGateway =
        ignore repoPath

        let converge remote =
            convergeStore store run maxRetries remote

        { new IGitGateway with
            member _.Fetch(remote) =
                task {
                    match fetchStoreRef run remote with
                    | Ok() -> return Ok()
                    | Error e -> return Error e
                }

            member _.Pull(remote) =
                task {
                    match converge remote with
                    | Ok _ -> return Ok()
                    | Error(ConvergeError.Transport reason) -> return Error(GitError.Transport reason)
                    | Error other -> return Error(GitError.Transport(sprintf "%A" other))
                }

            member _.Push(remote, refspec) =
                task {
                    match converge remote with
                    | Error(ConvergeError.Transport reason) -> return Error(GitError.Transport reason)
                    | Error other -> return Error(GitError.Transport(sprintf "%A" other))
                    | Ok _ ->
                        match runOk run [ "push"; remote; refspec ] (withSyncGuard None) with
                        | Ok() -> return Ok()
                        | Error e -> return Error e
                }

            member _.ConvergeStore(remote) = task { return converge remote } }

    /// Bind `IEventStore.Converge` to this gateway's sync ConvergeStore path.
    let bindEventStore (store: IGitRawStore) (run: GitGatewaySyncRunner) (maxRetries: int) : IEventStore =
        EventStore.createWithConverge store maxRetries (convergeStore store run maxRetries)

    let create
        (store: IGitRawStore)
        (repoPath: string)
        (runAsync: string * string list * Map<string, string> option -> Task<int * string * string>)
        (maxRetries: int)
        : IGitGateway =
        let runSync: GitGatewaySyncRunner =
            fun (args, env) ->
                let t = runAsync (repoPath, args, env)
                t.Result

        createWithSyncRunner store repoPath runSync maxRetries

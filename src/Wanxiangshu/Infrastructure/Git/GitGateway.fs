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

/// Async git runner used by GitGateway transport (args after `git`, optional env overlay).
type GitGatewayRunner = string list * Map<string, string> option -> Task<int * string * string>

/// Sync runner retained only as a test/embedding adapter. Production transport never blocks on Task.
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

    let private runOk (run: GitGatewayRunner) (args: string list) (env: Map<string, string> option) =
        task {
            let! code, stdout, stderr = run (args, env)

            if code = 0 then
                return Ok()
            else
                return Error(GitError.Failed(code, failureStdoutStderr stdout stderr))
        }

    let private withSyncGuard (env: Map<string, string> option) : Map<string, string> option =
        let baseMap =
            match env with
            | Some m -> m
            | None -> Map.empty

        Some(baseMap |> Map.add SyncActiveEnv "1")

    let private readSnapshot (store: IGitRawStore) (refName: string) : Task<StoreSnapshot option> =
        task {
            let! oid = store.ReadRef refName
            return oid |> Option.map (fun value -> { RootOid = RootOid.create value })
        }

    let private emptySnapshot (store: IGitRawStore) : Task<StoreSnapshot> =
        task {
            match! GitRawStore.materializeSnapshot store [] with
            | Ok snapshot -> return snapshot
            | Error err -> return failwith (sprintf "empty store materialization failed: %A" err)
        }

    let private localSnapshot (store: IGitRawStore) : Task<StoreSnapshot> =
        task {
            match! readSnapshot store StoreRef.canonical with
            | Some snapshot -> return snapshot
            | None -> return! emptySnapshot store
        }

    let private validateMerged (store: IGitRawStore) (snapshot: StoreSnapshot) : Task<Result<unit, ConvergeError>> =
        task {
            match! EventStoreMergeSpec.merge store (MergeInput.ofList [ snapshot ]) with
            | Error(MergeError.StorageInvalid err) -> return Error(ConvergeError.StorageInvalid err)
            | Ok events ->
                match EventStoreFold.validate events with
                | Error(FoldError.StorageInvalid err) -> return Error(ConvergeError.StorageInvalid err)
                | Ok() ->
                    match! PayloadClosure.validatePresent store (PayloadClosure.ofEvents events) with
                    | Error err -> return Error(ConvergeError.StorageInvalid err)
                    | Ok() -> return Ok()
        }

    let private expectedOldFor (store: IGitRawStore) (baseSnapshot: StoreSnapshot) : Task<GitObjectId option> =
        task {
            match! store.ReadRef StoreRef.canonical with
            | None -> return None
            | Some _ -> return Some(RootOid.value baseSnapshot.RootOid)
        }

    let private isMissingRemoteRef (stderr: string) =
        let text = stderr.ToLowerInvariant()

        text.Contains("couldn't find remote ref")
        || text.Contains("does not exist")
        || text.Contains("no such ref")
        || text.Contains("unable to find")

    /// Fetch remote canonical store into local remote-tracking ref (§14).
    /// Missing remote store ref is Ok (Absent) — first publish uses Absent lease.
    let private fetchStoreRef (run: GitGatewayRunner) (remote: string) : Task<Result<unit, GitError>> =
        task {
            let tracking = StoreRef.remoteTracking remote
            let refspec = sprintf "+%s:%s" StoreRef.canonical tracking
            let! code, stdout, stderr = run ([ "fetch"; remote; refspec ], withSyncGuard None)

            if code = 0 then
                return Ok()
            elif isMissingRemoteRef (failureStdoutStderr stdout stderr) then
                return Ok()
            else
                return Error(GitError.Failed(code, failureStdoutStderr stdout stderr))
        }

    let private leasePush
        (run: GitGatewayRunner)
        (remote: string)
        (expectedRemote: GitObjectId option)
        (newOid: GitObjectId)
        : Task<Result<unit, GitError>> =
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
        : Task<StoreSnapshot * GitObjectId option> =
        task {
            match! store.ReadRef(StoreRef.remoteTracking remote) with
            | Some oid -> return { RootOid = RootOid.create oid }, Some oid
            | None ->
                match fallback with
                | Some snap -> return snap, Some(RootOid.value snap.RootOid)
                | None ->
                    let! empty = emptySnapshot store
                    return empty, None
        }

    /// Core bidirectional converge. When `skipFetch`, uses `observedRemote` and does not fetch (§14).
    let private convergeLoop
        (store: IGitRawStore)
        (run: GitGatewayRunner)
        (maxRetries: int)
        (remote: string)
        (skipFetch: bool)
        (initialObserved: StoreSnapshot)
        (initialLeaseExpected: GitObjectId option)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        let rec loop (remoteSnap: StoreSnapshot) (leaseExpected: GitObjectId option) (retriesLeft: int) =
            task {
                let! local = localSnapshot store

                match! EventStoreMerge.merge store (MergeInput.ofList [ local; remoteSnap ]) with
                | Error(MergeError.StorageInvalid err) -> return Error(ConvergeError.StorageInvalid err)
                | Ok merged ->
                    match! validateMerged store merged with
                    | Error e -> return Error e
                    | Ok() ->
                        let! expectedLocal = expectedOldFor store local
                        let newOid = RootOid.value merged.RootOid
                        let! casOk = store.CompareAndSwapRef(StoreRef.canonical, expectedLocal, newOid)

                        if casOk then
                            match! leasePush run remote leaseExpected newOid with
                            | Ok() -> return Ok merged
                            | Error gitErr ->
                                if retriesLeft <= 0 then
                                    if maxRetries <= 0 then
                                        return Error ConvergeError.ConvergeCasRejected
                                    else
                                        return Error ConvergeError.ConvergeRetryExhausted
                                elif skipFetch then
                                    return Error(asTransport gitErr)
                                else
                                    match! fetchStoreRef run remote with
                                    | Error e -> return Error(asTransport e)
                                    | Ok() ->
                                        let! refreshed, nextLease = observedOrEmpty store remote None
                                        return! loop refreshed nextLease (retriesLeft - 1)
                        elif retriesLeft <= 0 then
                            if maxRetries <= 0 then
                                return Error ConvergeError.ConvergeCasRejected
                            else
                                return Error ConvergeError.ConvergeRetryExhausted
                        elif skipFetch then
                            return! loop remoteSnap leaseExpected (retriesLeft - 1)
                        else
                            match! fetchStoreRef run remote with
                            | Error e -> return Error(asTransport e)
                            | Ok() ->
                                let! refreshed, nextLease = observedOrEmpty store remote None
                                return! loop refreshed nextLease (retriesLeft - 1)
            }

        loop initialObserved initialLeaseExpected maxRetries

    let convergeStore
        (store: IGitRawStore)
        (run: GitGatewayRunner)
        (maxRetries: int)
        (remote: string)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        task {
            if syncActiveDepth.Value > 0 then
                let! local = localSnapshot store
                return Ok local
            else
                syncActiveDepth.Value <- syncActiveDepth.Value + 1

                try
                    match! fetchStoreRef run remote with
                    | Error e -> return Error(asTransport e)
                    | Ok() ->
                        let! observed, leaseExpected = observedOrEmpty store remote None
                        return! convergeLoop store run maxRetries remote false observed leaseExpected
                finally
                    syncActiveDepth.Value <- syncActiveDepth.Value - 1
        }

    /// Hook-facing helper: reuse already-observed remote-tracking snapshot; no nested fetch (§14).
    let convergeStoreWithObservedRemote
        (store: IGitRawStore)
        (run: GitGatewayRunner)
        (maxRetries: int)
        (remote: string)
        (observedRemote: StoreSnapshot)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        task {
            if syncActiveDepth.Value > 0 then
                let! local = localSnapshot store
                return Ok local
            else
                syncActiveDepth.Value <- syncActiveDepth.Value + 1

                try
                    // Lease expectation comes from remote-tracking when present (hook path).
                    // Absent tracking ⇒ Absent lease (first remote publication).
                    let! leaseExpected = store.ReadRef(StoreRef.remoteTracking remote)
                    return! convergeLoop store run maxRetries remote true observedRemote leaseExpected
                finally
                    syncActiveDepth.Value <- syncActiveDepth.Value - 1
        }

    let createWithRunner
        (store: IGitRawStore)
        (repoPath: string)
        (run: GitGatewayRunner)
        (maxRetries: int)
        : IGitGateway =
        ignore repoPath

        let converge remote =
            convergeStore store run maxRetries remote

        { new IGitGateway with
            member _.Fetch(remote) =
                task {
                    match! fetchStoreRef run remote with
                    | Ok() -> return Ok()
                    | Error e -> return Error e
                }

            member _.Pull(remote) =
                task {
                    match! converge remote with
                    | Ok _ -> return Ok()
                    | Error(ConvergeError.Transport reason) -> return Error(GitError.Transport reason)
                    | Error other -> return Error(GitError.Transport(sprintf "%A" other))
                }

            member _.Push(remote, refspec) =
                task {
                    match! converge remote with
                    | Error(ConvergeError.Transport reason) -> return Error(GitError.Transport reason)
                    | Error other -> return Error(GitError.Transport(sprintf "%A" other))
                    | Ok _ ->
                        match! runOk run [ "push"; remote; refspec ] (withSyncGuard None) with
                        | Ok() -> return Ok()
                        | Error e -> return Error e
                }

            member _.ConvergeStore(remote) = converge remote }

    let createWithSyncRunner
        (store: IGitRawStore)
        (repoPath: string)
        (run: GitGatewaySyncRunner)
        (maxRetries: int)
        : IGitGateway =
        let asyncRun: GitGatewayRunner = fun argsAndEnv -> Task.FromResult(run argsAndEnv)
        createWithRunner store repoPath asyncRun maxRetries

    /// Bind `IEventStore.Converge` directly to the async gateway path.
    let bindEventStore (store: IGitRawStore) (run: GitGatewayRunner) (maxRetries: int) : IEventStore =
        EventStore.createWithConverge store maxRetries (convergeStore store run maxRetries)

    let bindEventStoreWithSyncRunner (store: IGitRawStore) (run: GitGatewaySyncRunner) (maxRetries: int) : IEventStore =
        let asyncRun: GitGatewayRunner = fun argsAndEnv -> Task.FromResult(run argsAndEnv)
        bindEventStore store asyncRun maxRetries

    let create
        (store: IGitRawStore)
        (repoPath: string)
        (runAsync: string * string list * Map<string, string> option -> Task<int * string * string>)
        (maxRetries: int)
        : IGitGateway =
        let run: GitGatewayRunner = fun (args, env) -> runAsync (repoPath, args, env)
        createWithRunner store repoPath run maxRetries

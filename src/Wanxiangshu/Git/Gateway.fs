namespace Wanxiangshu.Git

open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.EventStore

/// Hook-process Git transport helper. It is not a product-process remote API:
/// OpenCode/Wanxiangshu never owns user fetch/pull/push triggers.
type GitGatewayRunner = string list -> Task<int * string * string>

[<RequireQualifiedAccess>]
module GitGateway =

    [<Literal>]
    let SyncActiveEnv = "WANXIANG_GIT_SYNC_ACTIVE"

    let private trackingRef remote = StoreRef.remoteTracking remote

    let private transportError stdout stderr =
        let detail = if String.IsNullOrWhiteSpace stderr then stdout else stderr

        ConvergeError.Transport(
            if String.IsNullOrWhiteSpace detail then
                "git transport failed"
            else
                detail.Trim()
        )

    let private tryObjectIdFromLsRemoteLine (line: string) =
        let trimmed = line.Trim()
        let fields = trimmed.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)

        if String.IsNullOrWhiteSpace trimmed || fields.Length < 1 then
            None
        else
            Some(GitObjectId.create fields.[0])

    let private parseLsRemote (stdout: string) : GitObjectId option =
        stdout.Split('\n')
        |> Array.toList
        |> List.tryPick tryObjectIdFromLsRemoteLine

    let private requireExit0 stdout stderr code =
        if code <> 0 then Error(transportError stdout stderr) else Ok()

    let private requireFetchedObjectId (actual: string) =
        if String.IsNullOrWhiteSpace actual then
            Error(ConvergeError.Transport "fetched Wanxiang store ref has no object id")
        else
            Ok(GitObjectId.create actual)

    let private fetchRemoteRoot (run: GitGatewayRunner) (remote: string) : Task<Result<GitObjectId, ConvergeError>> =
        taskResult {
            let tracking = trackingRef remote
            let refspec = sprintf "+%s:%s" StoreRef.canonical tracking
            let! code, stdout, stderr = run [ "fetch"; "--no-tags"; remote; refspec ] |> TaskResultCE.ofTask
            do! requireExit0 stdout stderr code
            let! verifyCode, verifyOut, verifyErr = run [ "rev-parse"; "--verify"; tracking ] |> TaskResultCE.ofTask
            do! requireExit0 verifyOut verifyErr verifyCode
            return! requireFetchedObjectId (verifyOut.Trim())
        }

    let private materializeRemotePresence run remote =
        taskResult {
            let! actual = fetchRemoteRoot run remote
            return Some { RootOid = RootOid.create actual }, Some actual
        }

    let private afterLsRemote run remote stdout =
        match parseLsRemote stdout with
        | None -> taskResult { return None, None }
        | Some _advertisedOid -> materializeRemotePresence run remote

    /// Discover the current remote store root and ensure its object graph exists
    /// locally. Absence is a valid empty remote.
    let discoverRemote
        (run: GitGatewayRunner)
        (remote: string)
        : Task<Result<StoreSnapshot option * GitObjectId option, ConvergeError>> =
        taskResult {
            let! code, stdout, stderr = run [ "ls-remote"; "--refs"; remote; StoreRef.canonical ] |> TaskResultCE.ofTask

            if code <> 0 then
                return! Error(transportError stdout stderr)
            else
                return! afterLsRemote run remote stdout
        }

    let private pushSnapshot
        (run: GitGatewayRunner)
        (remote: string)
        (expectedRemote: GitObjectId option)
        (snapshot: StoreSnapshot)
        : Task<Result<unit, ConvergeError>> =
        task {
            let next = RootOid.value snapshot.RootOid

            let lease =
                match expectedRemote with
                | None -> "--force-with-lease=" + StoreRef.canonical + ":"
                | Some oid -> "--force-with-lease=" + StoreRef.canonical + ":" + GitObjectId.value oid

            let refspec = GitObjectId.value next + ":" + StoreRef.canonical

            // --no-verify avoids recursively entering pre-push; the hook process
            // also exports WANXIANG_GIT_SYNC_ACTIVE for every internal Git child.
            let! code, stdout, stderr = run [ "push"; "--no-verify"; lease; remote; refspec ]

            if code = 0 then
                return Ok()
            else
                return Error(transportError stdout stderr)
        }

    /// Full bidirectional convergence. `observedRemote` is only an optimization
    /// for reference-transaction: the algorithm after root discovery is identical
    /// to pre-push. Lease races refetch and repeat boundedly.
    let converge
        (raw: IGitRawStore)
        (commonDir: string)
        (run: GitGatewayRunner)
        (maxRetries: int)
        (remote: string)
        (observedRemote: StoreSnapshot option)
        : Task<Result<StoreSnapshot, ConvergeError>> =
        let rec loop remoteSnapshot expectedRemote retriesLeft =
            taskResult {
                let! merged = WriterStreamSync.syncWriterStreams raw commonDir remoteSnapshot
                let! pushResult = pushSnapshot run remote expectedRemote merged |> TaskResultCE.ofTask

                match pushResult with
                | Ok() -> return merged
                | Error _ when retriesLeft > 0 ->
                    let! nextSnapshot, nextExpected = discoverRemote run remote
                    return! loop nextSnapshot nextExpected (retriesLeft - 1)
                | Error _ when maxRetries <= 0 -> return! Error ConvergeError.ConvergeCasRejected
                | Error _ -> return! Error ConvergeError.ConvergeRetryExhausted
            }

        taskResult {
            match observedRemote with
            | Some snapshot ->
                let expected = Some(RootOid.value snapshot.RootOid)
                return! loop (Some snapshot) expected maxRetries
            | None ->
                let! snapshot, expected = discoverRemote run remote
                return! loop snapshot expected maxRetries
        }

    [<Import("execFile", "node:child_process")>]
    let private execFile
        (file: string)
        (args: string array)
        (options: obj)
        (callback: obj -> obj -> obj -> unit)
        : unit =
        jsNative

    [<Emit("Buffer.isBuffer($0) ? $0.toString('utf8') : String($0 ?? '')")>]
    let private asText (value: obj) : string = jsNative

    [<Emit("($0 && typeof $0.code === 'number') ? $0.code : 1")>]
    let private errorCode (error: obj) : int = jsNative

    let private completeExec (tcs: TaskCompletionSource<int * string * string>) error stdout stderr =
        if isNull error then
            tcs.SetResult(0, asText stdout, asText stderr)
        else
            tcs.SetResult(errorCode error, asText stdout, asText stderr)

    let createDefaultRunner (repoPath: string) : GitGatewayRunner =
        fun args ->
            let tcs = TaskCompletionSource<int * string * string>()
            let argv = Array.append [| "-C"; repoPath |] (List.toArray args)

            let options =
                createObj [ "encoding" ==> "utf8"; "maxBuffer" ==> (64 * 1024 * 1024) ]

            try
                execFile GitSubject.Executable argv options (fun error stdout stderr ->
                    completeExec tcs error stdout stderr)
            with ex ->
                tcs.SetResult(1, "", ex.Message)

            tcs.Task

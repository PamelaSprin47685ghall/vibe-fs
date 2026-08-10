namespace Wanxiangshu.Infrastructure.Git

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Infrastructure.Persist

/// reference-transaction + pre-push shim (§14 / §15 / §20 / §21).
/// Converge protocol is injected — HookDispatcher never fetches or merges itself.
[<RequireQualifiedAccess>]
module HookDispatcher =

    [<Literal>]
    let SyncActiveEnv = "WANXIANG_GIT_SYNC_ACTIVE"

    [<Literal>]
    let OwnershipMarker = "wanxiang-hook-dispatcher"

    [<Literal>]
    let IncompleteDiagnosis = "Git integration incomplete"

    type ConvergeFull = string -> Task<Result<StoreSnapshot, ConvergeError>>

    type ConvergeObserved = string -> StoreSnapshot -> Task<Result<StoreSnapshot, ConvergeError>>

    type HookDispatcherDeps =
        {
            ConvergeFull: ConvergeFull
            ConvergeObserved: ConvergeObserved
            SyncRemote: string
        }

    type ReferenceUpdate =
        {
            RefName: string
            OldOid: string option
            NewOid: string option
            IsCommitted: bool
        }

    type HookDispatchResult =
        | NoOp of reason: string
        | Converged of StoreSnapshot
        | Failed of ConvergeError
        | Incomplete of diagnosis: string

    type HookKind =
        | ReferenceTransaction
        | PrePush

    type HookInstallVerdict =
        | Installed
        | AlreadyOwned
        | ForeignHook of path: string
        | DiagnoseIncomplete of reason: string

    let isSyncActive () : bool =
        Environment.GetEnvironmentVariable SyncActiveEnv = "1"

    [<Emit("process.env[$0] = $1")>]
    let private setEnv (key: string) (value: string) : unit = jsNative

    [<Emit("delete process.env[$0]")>]
    let private clearEnv (key: string) : unit = jsNative

    /// Test helper: set recursion guard, run, restore prior value.
    let withSyncActive (action: unit -> 'a) : 'a =
        let previous = Environment.GetEnvironmentVariable SyncActiveEnv

        try
            setEnv SyncActiveEnv "1"
            action ()
        finally
            if isNull previous then
                clearEnv SyncActiveEnv
            else
                setEnv SyncActiveEnv previous

    let createDeps
        (convergeFull: ConvergeFull)
        (convergeObserved: ConvergeObserved)
        (syncRemote: string)
        : HookDispatcherDeps =
        {
            ConvergeFull = convergeFull
            ConvergeObserved = convergeObserved
            SyncRemote =
                if String.IsNullOrWhiteSpace syncRemote then
                    "origin"
                else
                    syncRemote
        }

    let private isAbsentOid (oid: string option) : bool =
        match oid with
        | None -> true
        | Some value when String.IsNullOrWhiteSpace value -> true
        | Some value -> value |> Seq.forall (fun ch -> ch = '0')

    let private observedSnapshot (oid: string) : StoreSnapshot =
        { RootOid = RootOid.create (GitObjectId.create oid) }

    let private mapConverge (result: Result<StoreSnapshot, ConvergeError>) : HookDispatchResult =
        match result with
        | Ok snapshot -> Converged snapshot
        | Error error -> Failed error

    let private matchingStoreUpdate (syncRemote: string) (update: ReferenceUpdate) : string option =
        if not update.IsCommitted then
            None
        elif update.RefName <> StoreRef.remoteTracking syncRemote then
            None
        elif isAbsentOid update.NewOid then
            None
        else
            update.NewOid

    /// reference-transaction: store remote-tracking committed tip → ConvergeObserved only (§14).
    let onReferenceTransaction (deps: HookDispatcherDeps) (updates: ReferenceUpdate list) : Task<HookDispatchResult> =
        task {
            if isSyncActive () then
                return NoOp "recursion guard"
            else
                match
                    updates
                    |> List.choose (matchingStoreUpdate deps.SyncRemote)
                    |> List.tryLast
                with
                | None -> return NoOp "no store remote-tracking update"
                | Some oid ->
                    let! result = deps.ConvergeObserved deps.SyncRemote (observedSnapshot oid)
                    return mapConverge result
        }

    /// pre-push: full bidirectional ConvergeFull before user push continues (§15).
    let onPrePush (deps: HookDispatcherDeps) (remote: string) : Task<HookDispatchResult> =
        task {
            if isSyncActive () then
                return NoOp "recursion guard"
            else
                let target =
                    if String.IsNullOrWhiteSpace remote then
                        deps.SyncRemote
                    else
                        remote

                let! result = deps.ConvergeFull target
                return mapConverge result
        }

    let private containsOwnershipMarker (body: string) : bool =
        // Avoid IndexOf(..., StringComparison): Fable maps the enum to fromIndex.
        body.Contains OwnershipMarker

    /// Pure ownership probe (no filesystem). None = would install.
    let classifyExistingHook (existingBody: string option) : HookInstallVerdict =
        match existingBody with
        | None -> Installed
        | Some body when containsOwnershipMarker body -> AlreadyOwned
        | Some _ -> ForeignHook ""

    let private hookFileName (kind: HookKind) : string =
        match kind with
        | ReferenceTransaction -> "reference-transaction"
        | PrePush -> "pre-push"

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string) (encoding: string) : string = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (content: string) (options: obj) : unit = jsNative

    [<Import("chmodSync", "node:fs")>]
    let private chmodSync (path: string) (mode: int) : unit = jsNative

    [<Import("join", "node:path")>]
    let private joinPath (left: string) (right: string) : string = jsNative

    let private tryReadHook (path: string) : string option =
        if existsSync path then
            Some(readFileSync path "utf8")
        else
            None

    let private writeShim (path: string) (shimBody: string) : unit =
        writeFileSync path shimBody (createObj [ "encoding", box "utf8"; "mode", box 0o755 ])

        try
            chmodSync path 0o755
        with _ ->
            ()

    /// Install Wanxiang shim when safe; never overwrite foreign hooks (§20 / §3.3).
    let installOrDiagnose (hooksDir: string) (kind: HookKind) (shimBody: string) : HookInstallVerdict =
        if not (containsOwnershipMarker shimBody) then
            DiagnoseIncomplete(sprintf "%s: shim body missing ownership marker" IncompleteDiagnosis)
        else
            let path = joinPath hooksDir (hookFileName kind)

            match classifyExistingHook (tryReadHook path) with
            | Installed ->
                writeShim path shimBody
                Installed
            | AlreadyOwned ->
                writeShim path shimBody
                AlreadyOwned
            | ForeignHook _ ->
                DiagnoseIncomplete(sprintf "%s: foreign hook at %s" IncompleteDiagnosis path)
            | DiagnoseIncomplete reason -> DiagnoseIncomplete reason

    /// Canonical shim header body fragment (tests / install callers embed this).
    let shimHeaderComment: string =
        sprintf
            "# %s\n# ownership: Wanxiangshu HookDispatcher — do not replace with unrelated hooks"
            OwnershipMarker

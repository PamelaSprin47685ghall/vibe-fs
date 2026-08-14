namespace Wanxiangshu.Git.Hook

open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Git
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

/// DURABLE-CONVERGENCE-008. Product startup only ENSURES the Git hook membrane.
/// Actual full bidirectional convergence runs later in an independent Git-hook
/// process through resources/git/wanxiang-hook.mjs + HookSync.
[<RequireQualifiedAccess>]
module HookDispatcher =

    [<Literal>]
    let SyncActiveEnv = "WANXIANG_GIT_SYNC_ACTIVE"

    [<Literal>]
    let OwnershipMarker = "wanxiang-hook-dispatcher"

    [<Literal>]
    let IncompleteDiagnosis = "Git integration incomplete"

    type HookKind =
        | ReferenceTransaction
        | PrePush

    type HookInstallVerdict =
        | Installed
        | AlreadyOwned
        | ForeignHook of path: string
        | DiagnoseIncomplete of reason: string

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

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    let private hookFileName =
        function
        | HookKind.ReferenceTransaction -> "reference-transaction"
        | HookKind.PrePush -> "pre-push"

    let private hookRunnerArgument =
        function
        | HookKind.ReferenceTransaction -> "reference-transaction"
        | HookKind.PrePush -> "pre-push"

    let private tryReadHook path =
        if existsSync path then
            Some(readFileSync path "utf8")
        else
            None

    let private containsOwnershipMarker (body: string) = body.Contains OwnershipMarker

    let classifyExistingHook (existingBody: string option) : HookInstallVerdict =
        match existingBody with
        | None -> Installed
        | Some body when containsOwnershipMarker body -> AlreadyOwned
        | Some _ -> ForeignHook ""

    let private writeShim path body =
        writeFileSync path body (createObj [ "encoding" ==> "utf8"; "mode" ==> 0o755 ])

        try
            chmodSync path 0o755
        with _ ->
            ()

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
            | ForeignHook _ -> ForeignHook path
            | DiagnoseIncomplete reason -> DiagnoseIncomplete reason

    let shimHeaderComment =
        sprintf "# %s\n# ownership: Wanxiangshu HookDispatcher" OwnershipMarker

    let private shellQuote (value: string) =
        "'" + value.Replace("'", "'\"'\"'") + "'"

    /// Compiled HookDispatcher lives at dist/Git/Hook. The independently shipped
    /// runner lives at resources/git under the same package root.
    let private runnerPath () =
        let here = dirname (fileURLToPath importMetaUrl)
        let packageRoot = dirname (dirname (dirname here))
        joinPath (joinPath (joinPath packageRoot "resources") "git") "wanxiang-hook.mjs"

    let private shimBody kind =
        String.concat
            "\n"
            [ "#!/bin/sh"
              shimHeaderComment
              sprintf "if [ \"${%s:-}\" = \"1\" ]; then exit 0; fi" SyncActiveEnv
              sprintf "exec /usr/bin/env node %s %s \"$@\"" (shellQuote (runnerPath ())) (hookRunnerArgument kind)
              "" ]

    let private hooksDirectory workspace =
        GitSubject.execIn workspace [| "rev-parse"; "--path-format=absolute"; "--git-path"; "hooks" |]
        |> fun value -> value.Trim()

    let private remotes workspace =
        let output = GitSubject.execIn workspace [| "remote" |]

        output.Split('\n')
        |> Array.map (fun value -> value.Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    let private remoteFetchSpecs workspace remote =
        try
            let output =
                GitSubject.execIn workspace [| "config"; "--get-all"; sprintf "remote.%s.fetch" remote |]

            output.Split('\n')
            |> Array.map (fun value -> value.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.toList
        with _ ->
            []

    let private ensureRemoteStoreFetchRefspec workspace remote =
        let expected = sprintf "+%s:%s" StoreRef.canonical (StoreRef.remoteTracking remote)

        if remoteFetchSpecs workspace remote |> List.contains expected then
            ()
        else
            GitSubject.execIn workspace [| "config"; "--add"; sprintf "remote.%s.fetch" remote; expected |]
            |> ignore

    /// Startup-only ensure. There is intentionally no fetch/pull/push here.
    /// Both installed hooks later launch the same standalone FULL converge path.
    let ensure (workspace: string) : Result<unit, string> =
        try
            let hooksDir = hooksDirectory workspace

            let verdicts =
                [ HookKind.ReferenceTransaction; HookKind.PrePush ]
                |> List.map (fun kind -> kind, installOrDiagnose hooksDir kind (shimBody kind))

            match
                verdicts
                |> List.tryPick (fun (_, verdict) ->
                    match verdict with
                    | ForeignHook path -> Some(sprintf "%s: foreign hook at %s" IncompleteDiagnosis path)
                    | DiagnoseIncomplete reason -> Some reason
                    | Installed
                    | AlreadyOwned -> None)
            with
            | Some error -> Error error
            | None ->
                for remote in remotes workspace do
                    ensureRemoteStoreFetchRefspec workspace remote

                Ok()
        with ex ->
            Error(sprintf "%s: %s" IncompleteDiagnosis ex.Message)

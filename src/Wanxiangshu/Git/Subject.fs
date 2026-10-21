namespace Wanxiangshu.Git

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Subject verbs that own the production `git` executable token (§37).
/// Source-control and read-only worktree topology callers use this boundary.
module GitSubject =

    /// Sole production spelling of the git executable name.
    [<Literal>]
    let Executable = "git"

    [<Import("execFile", "node:child_process")>]
    let private execFileJs (fileName: string) (arguments: string array) (options: obj) (callback: obj) : unit = jsNative

    [<Emit("""new Promise(function(resolve, reject) { $0($1, $2, $3, function(error, stdout) { if (error) reject(error); else resolve(stdout); }); })""")>]
    let private execFileAsync (exec: obj) (fileName: string) (arguments: string array) (options: obj) : Task<string> =
        jsNative

    let private utf8 = createObj [ "encoding", box "utf8" ]

    let private quietUtf8 =
        createObj [ "encoding", box "utf8"; "stdio", box [| "ignore"; "pipe"; "pipe" |] ]

    /// Run git with `-C directory` then `arguments`.
    let execIn (directory: string) (arguments: string array) : Task<string> =
        execFileAsync execFileJs Executable (Array.append [| "-C"; directory |] arguments) utf8

    // --- worktree root ---

    /// The worktree root of `directory`, not the entry directory and not the common Git dir.
    let revParseShowToplevel (directory: string) : Task<string> =
        task {
            let! raw = execIn directory [| "rev-parse"; "--show-toplevel" |]
            return raw.TrimEnd('\r', '\n')
        }

module GitPorcelain =
    type private Messages =
        abstract worktrees: output: string -> obj array
        abstract primaryWorktree: outputZ: string -> obj
        abstract branches: output: string -> string array
        abstract shortBranch: reference: string -> string
        abstract isManagerBranch: reference: string -> bool
        abstract managerBranchOf: jobId: string -> string
        abstract rebaseStatePaths: merge: obj * apply: obj -> string array
        abstract staleWorktrees: entries: obj array * owned: string array -> string array
        abstract staleBranches: branches: string array * owned: string array -> string array

    [<Import("GitPorcelain", "wanxiangli/git-porcelain")>]
    let private factory: unit -> Messages = jsNative

    let private law: Messages = factory ()

    let worktrees (output: string) : (string * string option) list =
        law.worktrees output
        |> Array.map (fun entry ->
            let branch: obj = entry?branch
            (unbox<string> entry?path), (if isNull branch then None else Some(unbox<string> branch)))
        |> Array.toList

    let primaryWorktree (outputZ: string) : Result<string, string> =
        let result = law.primaryWorktree outputZ

        if unbox<bool> result?ok then
            Ok(unbox<string> result?path)
        else
            Error(unbox<string> result?code)

    let branches (output: string) : string list = law.branches output |> Array.toList

    let shortBranch (reference: string) : string = law.shortBranch reference

    let isManagerBranch (reference: string) : bool = law.isManagerBranch reference

    let managerBranchOf (jobId: string) : string = law.managerBranchOf jobId

    let rebaseStatePaths (merge: int * string) (apply: int * string) : string list =
        let mergeCode, mergePath = merge
        let applyCode, applyPath = apply

        law.rebaseStatePaths (
            createObj [ "code" ==> mergeCode; "path" ==> mergePath ],
            createObj [ "code" ==> applyCode; "path" ==> applyPath ]
        )
        |> Array.toList

    let staleWorktrees (entries: (string * string option) list) (owned: string list) : string list =
        let wire =
            entries
            |> List.map (fun (path, branch) ->
                createObj
                    [ "path" ==> path
                      "branch"
                      ==> (match branch with
                           | Some value -> box value
                           | None -> null) ])
            |> List.toArray

        law.staleWorktrees (wire, List.toArray owned) |> Array.toList

    let staleBranches (branches: string list) (owned: string list) : string list =
        law.staleBranches (List.toArray branches, List.toArray owned) |> Array.toList

namespace Wanxiangshu.Infrastructure.Persist

open System
open System.Text
open Fable.Core
open Fable.Core.JsInterop

/// Sync git runner: `(args, stdinBytes) -> exitCode * stdoutBytes * stderrText`.
/// `args` are `git` argv after `-C <repo>` (added by the default runner).
type GitRawSyncRunner = string list * byte[] option -> int * byte[] * string

module private ProcessGitTree =
    let sortEntries (entries: TreeEntry list) : TreeEntry list =
        entries
        |> List.map (fun entry ->
            { entry with
                Mode = StoreTree.normalizeMode entry.Mode })
        |> List.sortWith (fun a b ->
            let key (entry: TreeEntry) =
                if StoreTree.isTreeMode entry.Mode then
                    entry.Name + "/"
                else
                    entry.Name

            compare (key a) (key b))

/// Production `IGitRawStore` over a real Git object database + refs (§2.3 / §9).
/// Plumbing only (`hash-object` / `mktree` / `cat-file` / `ls-tree` / `update-ref`).
/// Never touches HEAD, index, commits, branches, or tags as store history.
type ProcessGitRawStore(_repoPath: string, run: GitRawSyncRunner) =
    let zeroOid = String('0', 40)

    let utf8 (bytes: byte[]) = Encoding.UTF8.GetString(bytes)

    let runText (args: string list) : int * string * string =
        let code, stdout, stderr = run (args, None)
        code, utf8 stdout, stderr

    let runWithStdin (args: string list) (stdin: byte[]) : int * string * string =
        let code, stdout, stderr = run (args, Some stdin)
        code, utf8 stdout, stderr

    let ensureOk (label: string) (code: int) (stdout: string) (stderr: string) =
        if code <> 0 then
            let detail = if String.IsNullOrWhiteSpace stderr then stdout else stderr

            failwith (sprintf "git %s failed (%d): %s" label code (detail.Trim()))

    let oidOrNone (code: int) (stdout: string) : GitObjectId option =
        if code <> 0 then
            None
        else
            let trimmed = stdout.Trim()

            if String.IsNullOrWhiteSpace trimmed || trimmed.Length <> 40 then
                None
            else
                Some(GitObjectId.create trimmed)

    interface IGitRawStore with
        member _.WriteBlob(content: byte[]) =
            let code, stdout, stderr = runWithStdin [ "hash-object"; "-w"; "--stdin" ] content
            ensureOk "hash-object" code stdout stderr

            match oidOrNone code stdout with
            | Some oid -> oid
            | None -> failwith (sprintf "hash-object returned invalid oid: %s" (stdout.Trim()))

        member _.WriteTree(entries: TreeEntry list) =
            let sorted = ProcessGitTree.sortEntries entries

            let lines =
                sorted
                |> List.map (fun entry ->
                    let mode =
                        if StoreTree.isTreeMode entry.Mode then
                            "040000"
                        else
                            entry.Mode

                    let objectType = if StoreTree.isTreeMode entry.Mode then "tree" else "blob"

                    sprintf "%s %s %s\t%s" mode objectType (GitObjectId.value entry.Oid) entry.Name)
                |> String.concat "\n"

            let payload = Encoding.UTF8.GetBytes(if lines = "" then "" else lines + "\n")

            let code, stdout, stderr = runWithStdin [ "mktree" ] payload
            ensureOk "mktree" code stdout stderr

            match oidOrNone code stdout with
            | Some oid -> oid
            | None -> failwith (sprintf "mktree returned invalid oid: %s" (stdout.Trim()))

        member _.ReadObject(oid: GitObjectId) =
            let tip = GitObjectId.value oid
            let typeCode, typeOut, _ = runText [ "cat-file"; "-t"; tip ]

            if typeCode <> 0 then
                None
            else
                match typeOut.Trim() with
                | "blob"
                | "tree" as objectType ->
                    let code, stdoutBytes, _ = run ([ "cat-file"; objectType; tip ], None)

                    if code <> 0 then None else Some stdoutBytes
                | _ -> None

        member _.ReadTree(oid: GitObjectId) =
            let tip = GitObjectId.value oid
            let code, stdout, _ = runText [ "ls-tree"; "--full-tree"; "-z"; tip ]

            if code <> 0 then
                None
            else
                let entries =
                    stdout.Split([| '\u0000' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.choose (fun row ->
                        let tab = row.IndexOf('\t')

                        if tab < 0 then
                            None
                        else
                            let meta = row.Substring(0, tab)
                            let name = row.Substring(tab + 1)
                            let parts = meta.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)

                            match parts with
                            | [| mode; _objectType; oidText |] when oidText.Length = 40 ->
                                Some
                                    { Mode = StoreTree.normalizeMode mode
                                      Name = name
                                      Oid = GitObjectId.create oidText }
                            | _ -> None)
                    |> Array.toList

                Some(ProcessGitTree.sortEntries entries)

        member _.ReadRef(refName: string) =
            let code, stdout, _ = runText [ "rev-parse"; "--verify"; "--quiet"; refName ]
            oidOrNone code stdout

        member _.CompareAndSwapRef(refName, expectedOld, newOid) =
            let next = GitObjectId.value newOid

            let expected =
                match expectedOld with
                | None -> zeroOid
                | Some oid -> GitObjectId.value oid

            let code, _, _ = runText [ "update-ref"; refName; next; expected ]
            code = 0

[<RequireQualifiedAccess>]
module ProcessGitRawStore =
    [<Import("execFileSync", "node:child_process")>]
    let private execFileSync (fileName: string) (arguments: string array) (options: obj) : obj = jsNative

    /// Default sync runner: `git -C <repoPath> …` with binary stdout/stdin.
    let createDefaultRunner (repoPath: string) : GitRawSyncRunner =
        fun (args: string list, stdin: byte[] option) ->
            let argv = Array.append [| "-C"; repoPath |] (args |> List.toArray)

            try
                let options =
                    match stdin with
                    | Some bytes ->
                        createObj
                            [ "encoding", box "buffer"
                              "input", box bytes
                              "maxBuffer", box (64 * 1024 * 1024) ]
                    | None -> createObj [ "encoding", box "buffer"; "maxBuffer", box (64 * 1024 * 1024) ]

                let stdoutObj = execFileSync "git" argv options
                let stdout: byte[] = emitJsExpr stdoutObj "Buffer.from($0)"
                0, stdout, ""
            with ex ->
                let status: int =
                    emitJsExpr ex "($0 && typeof $0.status === 'number') ? $0.status : 1"

                let stdout: byte[] =
                    emitJsExpr ex "($0 && $0.stdout) ? Buffer.from($0.stdout) : Buffer.alloc(0)"

                let stderr: string =
                    emitJsExpr
                        ex
                        """
                        (function (e) {
                          if (!e || e.stderr == null) return (e && e.message) || String(e);
                          return Buffer.from(e.stderr).toString('utf8');
                        })($0)
                        """

                status, stdout, stderr

    let createWithRunner (repoPath: string) (run: GitRawSyncRunner) : IGitRawStore =
        ProcessGitRawStore(repoPath, run) :> IGitRawStore

    let create (repoPath: string) : IGitRawStore =
        createWithRunner repoPath (createDefaultRunner repoPath)

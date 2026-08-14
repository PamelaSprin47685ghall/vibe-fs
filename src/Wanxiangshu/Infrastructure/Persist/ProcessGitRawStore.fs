namespace Wanxiangshu.Infrastructure.Persist

open System
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel

/// Async git runner: `(args, stdinBytes) -> Task<exitCode * stdoutBytes * stderrText>`.
/// `args` are `git` argv after `-C <repo>` (added by the default runner).
type GitRawRunner = string list * byte[] option -> Task<int * byte[] * string>

module private ProcessGitTree =
    let sortEntries (entries: TreeEntry list) : TreeEntry list = StoreTree.canonicalOrder entries

module private ProcessGitTreeHash =
    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    /// Content digest of a tree entry list — the writtenTreeCache key, not an oid.
    /// Same bytes in, same digest out; a cache key must be small and deterministic.
    let sha256Hex (data: byte[]) : string =
        let hash = createHash "sha256"
        hash?update (box data) |> ignore
        unbox<string> (hash?digest (box "hex"))

/// Where a repository keeps its objects and refs: immutable facts about a path, so they are
/// resolved once per repository per process. Asking `git rev-parse` per store instance meant one
/// process spawn every time a session opened the store — measured 27 `--git-common-dir` spawns in
/// a single canary, all answering the same question about the same directory.
module private ProcessGitLayout =
    let private answers = Collections.Generic.Dictionary<string, Task<string option>>()

    let resolve (key: string) (ask: unit -> Task<string option>) : Task<string option> =
        match answers.TryGetValue key with
        | true, cached -> cached
        | _ ->
            let answer = ask ()
            answers.[key] <- answer
            answer

/// Production `IGitRawStore` over a real Git object database + refs (§2.3 / §9).
/// Plumbing only (`hash-object` / `mktree` / `cat-file` / `ls-tree` / `update-ref`).
/// Never touches HEAD, index, commits, branches, or tags as store history.
///
/// ── why this instance memoizes ───────────────────────────────────────────────
///
/// Every CLI fallback is one `git` spawn (~2.5ms), and a single-event append
/// measured **24 spawns / ~60ms** against 0.5ms for the identical EventStore logic on the
/// in-memory raw store. Twelve of those spawns were `ls-tree` of six distinct oids and seven
/// were `mktree` of trees already written moments earlier: the delta snapshot and the merge
/// rebuild the same shard path, so the same immutable object was re-read and re-written through
/// a fresh process each time. The runner is now async so a spawn yields the Host event loop,
/// but spawn count still dominates wall time — memoize immutable oids.
///
/// Git objects are content-addressed and immutable, which is what makes memoization exact
/// rather than a heuristic: an oid's bytes cannot change, and `mktree` of the same entry list
/// cannot yield a different oid. Absence is deliberately NOT cached — an object we have not
/// written yet may appear later.
type ProcessGitRawStore(_repoPath: string, run: GitRawRunner) =
    let zeroOid = String('0', 40)

    let objectCache = Collections.Generic.Dictionary<string, byte[]>()
    let treeCache = Collections.Generic.Dictionary<string, TreeEntry list>()
    let writtenTreeCache = Collections.Generic.Dictionary<string, GitObjectId>()

    let utf8 (bytes: byte[]) = Encoding.UTF8.GetString(bytes)

    let runText (args: string list) : Task<int * string * string> =
        task {
            let! code, stdout, stderr = run (args, None)
            return code, utf8 stdout, stderr
        }

    let runWithStdin (args: string list) (stdin: byte[]) : Task<int * string * string> =
        task {
            let! code, stdout, stderr = run (args, Some stdin)
            return code, utf8 stdout, stderr
        }

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

    /// The ODB directory, resolved once. `--git-path objects` answers correctly for a worktree,
    /// a bare repo and an alternates-free submodule alike, which is why it is asked rather than
    /// assembled from `_repoPath`.
    let objectsDirectory =
        lazy
            (ProcessGitLayout.resolve (_repoPath + "\u001fobjects") (fun () ->
                task {
                    let! code, stdout, _ = runText [ "rev-parse"; "--git-path"; "objects" ]
                    let trimmed = stdout.Trim()

                    if code <> 0 || trimmed = "" then return None
                    elif trimmed.StartsWith "/" then return Some trimmed
                    else return Some(_repoPath + "/" + trimmed)
                }))

    /// The git directory that owns refs, resolved once. `--git-common-dir` rather than
    /// `--git-dir`: a linked worktree keeps its own HEAD but shares `refs/`, and the store ref is
    /// shared state, not per-worktree state.
    let refsDirectory =
        lazy
            (ProcessGitLayout.resolve (_repoPath + "\u001fcommon") (fun () ->
                task {
                    let! code, stdout, _ = runText [ "rev-parse"; "--path-format=absolute"; "--git-common-dir" ]

                    let trimmed = stdout.Trim()

                    if code <> 0 || not (trimmed.StartsWith "/") then
                        return None
                    else
                        return Some trimmed
                }))

    interface IGitRawStore with
        member _.WriteBlob(content: byte[]) =
            task {
                match! objectsDirectory.Force() with
                | Some objects ->
                    let! oid = GitObjectDatabase.writeBlob objects content
                    return GitObjectId.create oid
                | None ->
                    let! code, stdout, stderr = runWithStdin [ "hash-object"; "-w"; "--stdin" ] content
                    ensureOk "hash-object" code stdout stderr

                    match oidOrNone code stdout with
                    | Some oid -> return oid
                    | None -> return failwith (sprintf "hash-object returned invalid oid: %s" (stdout.Trim()))
            }

        member _.WriteTree(entries: TreeEntry list) =
            task {
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

                let cacheKey = ProcessGitTreeHash.sha256Hex (Encoding.UTF8.GetBytes lines)

                match writtenTreeCache.TryGetValue cacheKey with
                | true, oid -> return oid
                | _ ->
                    let! written =
                        task {
                            match! objectsDirectory.Force() with
                            | Some objects ->
                                let! oid = GitObjectDatabase.writeTree objects sorted
                                return Some(GitObjectId.create oid)
                            | None ->
                                let payload = Encoding.UTF8.GetBytes(if lines = "" then "" else lines + "\n")
                                let! code, stdout, stderr = runWithStdin [ "mktree" ] payload
                                ensureOk "mktree" code stdout stderr
                                return oidOrNone code stdout
                        }

                    match written with
                    | Some oid ->
                        writtenTreeCache.[cacheKey] <- oid
                        treeCache.[GitObjectId.value oid] <- sorted
                        return oid
                    | None -> return failwith "tree write returned an invalid oid"
            }

        member _.ReadObject(oid: GitObjectId) =
            task {
                let tip = GitObjectId.value oid

                match objectCache.TryGetValue tip with
                | true, bytes -> return Some bytes
                | _ ->
                    let! objects = objectsDirectory.Force()

                    let! loose =
                        match objects with
                        | Some dir -> GitObjectDatabase.tryReadObject dir tip
                        | None -> Task.FromResult None

                    match loose with
                    | Some bytes ->
                        objectCache.[tip] <- bytes
                        return Some bytes
                    | None ->
                        // Packed (post-`gc`) or genuinely absent: the CLI is the only pack reader.
                        let! typeCode, typeOut, _ = runText [ "cat-file"; "-t"; tip ]

                        if typeCode <> 0 then
                            return None
                        else
                            match typeOut.Trim() with
                            | "blob"
                            | "tree" as objectType ->
                                let! code, stdoutBytes, _ = run ([ "cat-file"; objectType; tip ], None)

                                if code <> 0 then
                                    return None
                                else
                                    objectCache.[tip] <- stdoutBytes
                                    return Some stdoutBytes
                            | _ -> return None
            }

        member _.ReadTree(oid: GitObjectId) =
            task {
                let tip = GitObjectId.value oid

                match treeCache.TryGetValue tip with
                | true, entries -> return Some entries
                | _ ->
                    let! objects = objectsDirectory.Force()

                    let! loose =
                        match objects with
                        | Some dir -> GitObjectDatabase.tryReadTree dir tip
                        | None -> Task.FromResult None

                    match loose with
                    | Some entries ->
                        let sorted = ProcessGitTree.sortEntries entries
                        treeCache.[tip] <- sorted
                        return Some sorted
                    | None ->
                        let! code, stdout, _ = runText [ "ls-tree"; "--full-tree"; "-z"; tip ]

                        if code <> 0 then
                            return None
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

                            let sorted = ProcessGitTree.sortEntries entries
                            treeCache.[tip] <- sorted
                            return Some sorted
            }

        member _.ReadRef(refName: string) =
            task {
                match! refsDirectory.Force() with
                | Some gitDir ->
                    let! oid = GitObjectDatabase.tryReadRef gitDir refName
                    return oid |> Option.map GitObjectId.create
                | None ->
                    let! code, stdout, _ = runText [ "rev-parse"; "--verify"; "--quiet"; refName ]
                    return oidOrNone code stdout
            }

        member _.CompareAndSwapRef(refName, expectedOld, newOid) =
            task {
                let next = GitObjectId.value newOid

                match! refsDirectory.Force() with
                | Some gitDir ->
                    return!
                        GitObjectDatabase.compareAndSwapRef
                            gitDir
                            refName
                            (expectedOld |> Option.map GitObjectId.value)
                            next
                | None ->
                    let expected =
                        match expectedOld with
                        | None -> zeroOid
                        | Some oid -> GitObjectId.value oid

                    let! code, _, _ = runText [ "update-ref"; refName; next; expected ]
                    return code = 0
            }

[<RequireQualifiedAccess>]
module ProcessGitRawStore =
    [<Import("execFile", "node:child_process")>]
    let private execFile
        (fileName: string)
        (arguments: string array)
        (options: obj)
        (callback: obj -> obj -> obj -> unit)
        : unit =
        jsNative

    /// Default async runner: `git -C <repoPath> …` with binary stdout/stdin.
    /// Uses `execFile` (callback) so the Node event loop can run other work while git runs.
    let createDefaultRunner (repoPath: string) : GitRawRunner =
        fun (args: string list, stdin: byte[] option) ->
            let tcs = TaskCompletionSource<int * byte[] * string>()
            let argv = Array.append [| "-C"; repoPath |] (args |> List.toArray)

            let options =
                match stdin with
                | Some bytes ->
                    createObj
                        [ "encoding", box "buffer"
                          "input", box bytes
                          "maxBuffer", box (64 * 1024 * 1024) ]
                | None -> createObj [ "encoding", box "buffer"; "maxBuffer", box (64 * 1024 * 1024) ]

            try
                execFile "git" argv options (fun error stdout stderr ->
                    if isNull error then
                        let outBytes: byte[] = emitJsExpr stdout "Buffer.from($0)"
                        AsyncSupport.trySetResult tcs (0, outBytes, "") |> ignore
                    else
                        let status: int =
                            emitJsExpr error "($0 && typeof $0.status === 'number') ? $0.status : 1"

                        let outBytes: byte[] =
                            emitJsExpr error "($0 && $0.stdout) ? Buffer.from($0.stdout) : Buffer.alloc(0)"

                        let errText: string =
                            emitJsExpr
                                error
                                """
                                (function (e) {
                                  if (!e || e.stderr == null) return (e && e.message) || String(e);
                                  return Buffer.from(e.stderr).toString('utf8');
                                })($0)
                                """

                        AsyncSupport.trySetResult tcs (status, outBytes, errText) |> ignore)
            with ex ->
                AsyncSupport.trySetResult tcs (1, Array.empty, ex.Message) |> ignore

            tcs.Task

    let createWithRunner (repoPath: string) (run: GitRawRunner) : IGitRawStore =
        ProcessGitRawStore(repoPath, run) :> IGitRawStore

    let create (repoPath: string) : IGitRawStore =
        createWithRunner repoPath (createDefaultRunner repoPath)

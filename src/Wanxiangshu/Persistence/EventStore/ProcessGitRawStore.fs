namespace Wanxiangshu.Persistence.EventStore

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Process

type GitRawRequest =
    { Arguments: string list
      Input: byte[] option
      OutputLimit: int }

type GitRawRunner = GitRawRequest -> Task<GitRawResult>

module private GitBinaryReply =
    type Reply =
        { Code: int
          Output: GitRawOutput
          Result: GitRawResult }

    let copyBytes (bytes: byte[]) : byte[] = emitJsExpr bytes "Uint8Array.from($0)"

    let fail operation reason result =
        Error
            { Operation = operation
              Reason = reason
              Result = result }

    let invalid operation reason =
        fail operation reason (GitRawResult.NotStarted(NoExecution.InvalidRequest reason))

    let private complete operation result =
        match result with
        | GitRawResult.Observed({ End = EndEvidence.Confirmed(ProgramEnd.Exited code)
                                  StopRequested = None
                                  Fault = None } as observation) when observation.Output.Complete ->
            Ok
                { Code = code
                  Output = observation.Output
                  Result = result }
        | GitRawResult.Observed _ -> fail operation "Execution or complete output was not confirmed" result
        | _ -> fail operation "Git did not start" result

    let private unknown error =
        GitRawResult.Observed
            { End = EndEvidence.Unconfirmed
              StopRequested = None
              Output =
                { Stdout = Array.empty
                  Stderr = Array.empty
                  Complete = false }
              Fault = Some error }

    let execute (run: GitRawRunner) operation args input outputLimit =
        task {
            let! result =
                task {
                    try
                        return!
                            run
                                { Arguments = args
                                  Input = input
                                  OutputLimit = outputLimit }
                    with error ->
                        return unknown error.Message
                }

            return complete operation result
        }

    let requireZero operation reply =
        if reply.Code = 0 then
            Ok reply
        else
            fail operation (sprintf "Git exited with code %d" reply.Code) reply.Result

    let utf8 operation reply bytes =
        try
            let text: string =
                emitJsExpr bytes "new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode($0)"

            Ok text
        with _ ->
            fail operation "Git returned invalid UTF-8" reply.Result

    let isOid text : bool =
        emitJsExpr text "/^[0-9a-f]{40}$/.test($0)"

    let oid operation reply =
        result {
            let! text = utf8 operation reply reply.Output.Stdout

            if text.Length = 41 && text.[40] = '\n' && isOid (text.Substring(0, 40)) then
                return GitObjectId.create (text.Substring(0, 40))
            else
                return! fail operation "Git returned an invalid object id" reply.Result
        }

    let validateOid operation oid =
        let text = (GitObjectId.value oid).ToLowerInvariant()

        if isOid text then
            Ok text
        else
            invalid operation "Expected a 40-digit hexadecimal object id"

    let private presentMetadata operation reply objectType sizeText =
        let validSize: bool =
            emitJsExpr
                sizeText
                "/^(0|[1-9][0-9]*)$/.test($0) && Number.isSafeInteger(Number($0)) && Number($0) <= 2147483647"

        if validSize && List.contains objectType [ "blob"; "tree"; "commit"; "tag" ] then
            Ok(objectType, int (float sizeText))
        else
            fail operation "Git returned invalid object metadata" reply.Result

    let metadata operation tip reply (line: string) =
        match line.Split(' ') with
        | [| actual; "missing" |] when actual = tip ->
            fail operation "Git did not return the requested object; physical absence was not confirmed" reply.Result
        | [| actual; objectType; sizeText |] when actual = tip -> presentMetadata operation reply objectType sizeText
        | _ -> fail operation "Git returned invalid object metadata" reply.Result

    let checkMetadata operation tip reply =
        result {
            let! text = utf8 operation reply reply.Output.Stdout

            if not (text.EndsWith "\n") || text.IndexOf('\n') <> text.Length - 1 then
                return! fail operation "Git returned an incomplete metadata record" reply.Result

            return! metadata operation tip reply (text.Substring(0, text.Length - 1))
        }

    let captureLimit operation reply (bytes: int64) =
        if bytes > int64 Int32.MaxValue then
            fail operation "Object representation exceeds byte[] capacity" reply.Result
        else
            Ok(int bytes)

module private GitBinaryTree =
    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    let cacheKey (bytes: byte[]) : string =
        let hash = createHash "sha256"
        hash?update bytes |> ignore
        unbox<string> (hash?digest "hex")

    let private validName (name: string) =
        not (String.IsNullOrEmpty name)
        && name <> "."
        && name <> ".."
        && name.IndexOfAny [| '/'; '\u0000' |] < 0
        && (emitJsExpr
                name
                "new TextDecoder('utf-8', {fatal: true, ignoreBOM: true}).decode(new TextEncoder().encode($0)) === $0"
        : bool)

    let private validEntry (entry: TreeEntry) =
        validName entry.Name
        && GitBinaryReply.isOid (GitObjectId.value entry.Oid)
        && List.contains (GitTree.normalizeMode entry.Mode) [ "40000"; "100644"; "100755"; "120000" ]

    let validate (entries: TreeEntry list) =
        List.forall validEntry entries
        && (entries |> List.map (fun entry -> entry.Name) |> Set.ofList |> Set.count) = entries.Length

    let payload (entries: TreeEntry list) =
        entries
        |> List.map (fun entry ->
            let mode =
                if GitTree.isTreeMode entry.Mode then
                    "040000"
                else
                    entry.Mode

            let objectType = if GitTree.isTreeMode entry.Mode then "tree" else "blob"
            sprintf "%s %s %s\t%s\u0000" mode objectType (GitObjectId.value entry.Oid) entry.Name)
        |> String.concat ""
        |> Encoding.UTF8.GetBytes

    let private checkedEntry mode objectType oid name =
        let entry =
            { Mode = GitTree.normalizeMode mode
              Name = name
              Oid = GitObjectId.create oid }

        let expectedType = if GitTree.isTreeMode mode then "tree" else "blob"

        if validEntry entry && objectType = expectedType then
            Some entry
        else
            None

    let private entryFromFields name fields =
        match fields with
        | [| mode; objectType; oid |] -> checkedEntry mode objectType oid name
        | _ -> None

    let private row (text: string) =
        let tab = text.IndexOf '\t'

        if tab < 0 then
            None
        else
            entryFromFields (text.Substring(tab + 1)) (text.Substring(0, tab).Split(' '))

    let private parseRows operation (reply: GitBinaryReply.Reply) (text: string) =
        let rows = text.Substring(0, text.Length - 1).Split('\u0000') |> Array.toList
        let parsed = rows |> List.choose row

        if parsed.Length = rows.Length && validate parsed then
            Ok(GitTree.canonicalOrder parsed)
        else
            GitBinaryReply.fail operation "Git returned invalid or duplicate tree entries" reply.Result

    let parse operation reply =
        result {
            let! text = GitBinaryReply.utf8 operation reply reply.Output.Stdout

            if text = "" then
                return []
            elif not (text.EndsWith "\u0000") then
                return! GitBinaryReply.fail operation "Git returned an incomplete tree record" reply.Result
            else
                return! parseRows operation reply text
        }

type ProcessGitRawStore(_repoPath: string, run: GitRawRunner) =
    let objects = Collections.Generic.Dictionary<string, byte[]>()
    let trees = Collections.Generic.Dictionary<string, TreeEntry list>()
    let writtenTrees = Collections.Generic.Dictionary<string, GitObjectId>()

    let execute operation args input limit =
        GitBinaryReply.execute run operation args input limit

    let runZero operation args input limit =
        taskResult {
            let! reply = execute operation args input limit
            return! GitBinaryReply.requireZero operation reply
        }

    let readMetadata tip =
        taskResult {
            let! reply =
                runZero "cat-file" [ "cat-file"; "--batch-check" ] (Some(Encoding.UTF8.GetBytes(tip + "\n"))) 66560

            let! metadata = GitBinaryReply.checkMetadata "cat-file" tip reply
            return reply, metadata
        }

    let writeBlob content =
        taskResult {
            let! reply = runZero "hash-object" [ "hash-object"; "-w"; "--stdin" ] (Some content) 66560
            let! oid = GitBinaryReply.oid "hash-object" reply
            return oid
        }

    let writeTree entries =
        taskResult {
            if not (GitBinaryTree.validate entries) then
                return! GitBinaryReply.invalid "mktree" "Invalid or duplicate tree entries"

            let sorted = GitTree.canonicalOrder entries
            let payload = GitBinaryTree.payload sorted
            let key = GitBinaryTree.cacheKey payload

            match writtenTrees.TryGetValue key with
            | true, oid -> return oid
            | _ ->
                let! reply = runZero "mktree" [ "mktree"; "-z" ] (Some payload) 66560
                let! oid = GitBinaryReply.oid "mktree" reply
                writtenTrees.[key] <- oid
                trees.[GitObjectId.value oid] <- sorted
                return oid
        }

    let readObjectBody tip metadataReply objectType size =
        taskResult {
            let! limit = GitBinaryReply.captureLimit "cat-file" metadataReply (int64 size + 65536L)
            let! reply = runZero "cat-file" [ "cat-file"; objectType; tip ] None limit
            let bytes = reply.Output.Stdout

            if bytes.Length <> size then
                return! GitBinaryReply.fail "cat-file" "Git returned truncated or excess object bytes" reply.Result

            objects.[tip] <- GitBinaryReply.copyBytes bytes
            return bytes
        }

    let readObjectUncached tip =
        taskResult {
            let! metadataReply, (objectType, size) = readMetadata tip
            return! readObjectBody tip metadataReply objectType size
        }

    let readObject oid =
        taskResult {
            let! tip = GitBinaryReply.validateOid "cat-file" oid

            match objects.TryGetValue tip with
            | true, bytes -> return GitBinaryReply.copyBytes bytes
            | _ -> return! readObjectUncached tip
        }

    let readTreeEntries tip metadataReply size =
        taskResult {
            let! limit = GitBinaryReply.captureLimit "ls-tree" metadataReply (3L * int64 size + 66560L)
            let! reply = runZero "ls-tree" [ "ls-tree"; "--full-tree"; "-z"; tip ] None limit
            let! entries = GitBinaryTree.parse "ls-tree" reply

            if (size = 0) <> List.isEmpty entries then
                return! GitBinaryReply.fail "ls-tree" "Tree output disagrees with object metadata" reply.Result

            trees.[tip] <- entries
            return entries
        }

    let readTreeUncached tip =
        taskResult {
            let! metadataReply, metadata = readMetadata tip

            match metadata with
            | "tree", size -> return! readTreeEntries tip metadataReply size
            | _ -> return! GitBinaryReply.fail "ls-tree" "Expected a tree object" metadataReply.Result
        }

    let readTree oid =
        taskResult {
            let! tip = GitBinaryReply.validateOid "ls-tree" oid

            match trees.TryGetValue tip with
            | true, entries -> return entries
            | _ -> return! readTreeUncached tip
        }

    let readRef (refName: string) =
        taskResult {
            if not (refName.StartsWith "refs/") then
                return! GitBinaryReply.invalid "rev-parse" "Expected a full ref name"

            let! _ = runZero "check-ref-format" [ "check-ref-format"; refName ] None 66560
            let! exists = execute "show-ref" [ "show-ref"; "--exists"; refName ] None 66560

            if exists.Code = 2 && exists.Output.Stdout.Length = 0 then
                return None
            elif exists.Code = 129 then
                return! GitBinaryReply.fail "show-ref" "Git >= 2.43 is required for exact ref lookup" exists.Result
            else
                let! _ = GitBinaryReply.requireZero "show-ref" exists
                let! reply = runZero "rev-parse" [ "rev-parse"; "--verify"; "--quiet"; refName ] None 66560
                let! oid = GitBinaryReply.oid "rev-parse" reply
                return Some oid
        }

    interface IGitRawStore with
        member _.WriteBlob content =
            writeBlob (GitBinaryReply.copyBytes content)

        member _.WriteTree entries = writeTree entries
        member _.ReadObject oid = readObject oid
        member _.ReadTree oid = readTree oid
        member _.ReadRef refName = readRef refName

module private GitBinaryProcess =
    type private Capture(limit: int) =
        let stdout = ResizeArray<byte[]>()
        let stderr = ResizeArray<byte[]>()
        // DSL-MUTABLE: resource — this execution owns its bounded capture.
        let mutable size = 0
        let mutable collecting = true

        let store (chunks: ResizeArray<byte[]>) (bytes: byte[]) =
            let available = min bytes.Length (limit - size)

            if available > 0 then
                chunks.Add bytes.[0 .. available - 1]

            size <- size + available

            if available < bytes.Length then
                failwith "Git output exceeded its combined byte limit"

        member _.Stdout bytes =
            if collecting then
                store stdout bytes

        member _.Stderr bytes =
            if collecting then
                store stderr bytes

        member _.Read complete =
            { Stdout = stdout |> Seq.toArray |> Array.concat
              Stderr = stderr |> Seq.toArray |> Array.concat
              Complete = complete }

        member _.Close() =
            collecting <- false
            stdout.Clear()
            stderr.Clear()

    let private observe (child: NodeProcessHost.ChildProcess) (capture: Capture) stop =
        let ending = child.Observe()

        let fault =
            child.Failure.Value
            |> Option.orElse (
                if child.Exited.Value && ending = EndEvidence.Unconfirmed then
                    Some "Target end was not confirmed"
                else
                    None
            )

        match child.Rejection.Value with
        | Some reason -> GitRawResult.NotStarted reason
        | None ->
            GitRawResult.Observed
                { End = ending
                  StopRequested = stop
                  Output = capture.Read(child.Exited.Value && fault.IsNone && stop.IsNone)
                  Fault = fault }

    let private stopAfterFailure (child: NodeProcessHost.ChildProcess) message =
        child.Failure.Value <- child.Failure.Value |> Option.orElse (Some message)

        try
            child.Kill()
        with _ ->
            ()

    let private wait child capture deadline budget =
        task {
            try
                let! waited = NodeProcessWait.waitForExit child deadline CancellationToken.None

                let stop =
                    waited.StopRequested
                    |> Option.map (fun _ -> StopRequest.DeadlineStopRequested budget)

                return observe child capture stop
            with error ->
                stopAfterFailure child error.Message
                return observe child capture None
        }

    let private start repoPath request (capture: Capture) deadline budget =
        task {
            match!
                NodeProcessHost.spawnBinary
                    "git"
                    ("-C" :: repoPath :: request.Arguments)
                    repoPath
                    request.Input
                    capture.Stdout
                    capture.Stderr
            with
            | Error(ProcessResult.NotStarted reason) -> return GitRawResult.NotStarted reason
            | Error(ProcessResult.Observed observation) ->
                return
                    GitRawResult.Observed
                        { End = observation.End
                          StopRequested = observation.StopRequested
                          Output = capture.Read false
                          Fault = observation.Fault }
            | Ok child -> return! wait child capture deadline budget
        }

    let private tryStart repoPath request capture deadline budget =
        task {
            try
                return! start repoPath request capture deadline budget
            with error ->
                return
                    GitRawResult.Observed
                        { End = EndEvidence.Unconfirmed
                          StopRequested = None
                          Output = capture.Read false
                          Fault = Some error.Message }
        }

    let run repoPath (budget: TimeSpan) (request: GitRawRequest) =
        task {
            let capture = Capture(request.OutputLimit)
            let deadline = Deadline.ofBudget DateTimeOffset.UtcNow budget

            try
                return! tryStart repoPath request capture deadline budget
            finally
                capture.Close()
        }

[<RequireQualifiedAccess>]
module ProcessGitRawStore =
    [<Import("resolve", "node:path")>]
    let private absolutePath (path: string) : string = jsNative

    let createRunner repoPath (hardLimit: TimeSpan) : GitRawRunner =
        let repository = absolutePath repoPath

        fun request ->
            if
                Double.IsNaN hardLimit.TotalMilliseconds
                || Double.IsInfinity hardLimit.TotalMilliseconds
                || hardLimit <= TimeSpan.Zero
            then
                Task.FromResult(
                    GitRawResult.NotStarted(NoExecution.InvalidRequest "Git hard limit must be finite and positive")
                )
            elif request.OutputLimit < 0 then
                Task.FromResult(
                    GitRawResult.NotStarted(NoExecution.InvalidRequest "Git output limit must be non-negative")
                )
            else
                GitBinaryProcess.run repository hardLimit request

    let createDefaultRunner repoPath =
        createRunner repoPath (TimeSpan.FromSeconds 120.0)

    let createWithRunner repoPath run : IGitRawStore =
        ProcessGitRawStore(repoPath, run) :> IGitRawStore

    let create repoPath =
        createWithRunner repoPath (createDefaultRunner repoPath)

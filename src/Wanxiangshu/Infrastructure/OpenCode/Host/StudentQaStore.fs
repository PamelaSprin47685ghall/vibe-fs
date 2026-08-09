namespace Wanxiangshu.OpenCode

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

module private StudentQaNode =
    [<Import("execFileSync", "node:child_process")>]
    let execFileSync (file: string, args: string array, options: obj) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, options: obj) : unit = jsNative

    [<Import("chmodSync", "node:fs")>]
    let chmodSync (path: string, mode: int) : unit = jsNative

    [<Import("openSync", "node:fs")>]
    let openSync (path: string, flags: string, mode: int) : int = jsNative

    [<Import("writeSync", "node:fs")>]
    let writeSync (fd: int, bytes: byte array) : int = jsNative

    [<Import("fsyncSync", "node:fs")>]
    let fsyncSync (fd: int) : unit = jsNative

    [<Import("closeSync", "node:fs")>]
    let closeSync (fd: int) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string) : obj = jsNative

    [<Import("renameSync", "node:fs")>]
    let renameSync (source: string, destination: string) : unit = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let unlinkSync (path: string) : unit = jsNative

    [<Import("rmdirSync", "node:fs")>]
    let rmdirSync (path: string) : unit = jsNative

    [<Import("readdirSync", "node:fs")>]
    let readdirSync (path: string) : string array = jsNative

    [<Import("join", "node:path")>]
    let join2 (a: string, b: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let dirname (path: string) : string = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true }).decode($0)")>]
    let decodeUtf8Fatal (bytes: obj) : string = jsNative

/// PERSIST-011: sole writer for the private, unstructured QA authority file.
/// The path is derived only from trusted typed identities and the repository's
/// absolute Git private directory; QA never appears in the worktree or index.
type StudentQaStore private (gitDirectory: string) =
    let safeSegment = Regex("^[A-Za-z0-9._-]+$")

    let validate label value =
        if String.IsNullOrWhiteSpace value || not (safeSegment.IsMatch value) then
            Error(sprintf "Unsafe %s for Student QA path" label)
        else
            Ok value

    let directoryFor sessionId logicalRunId =
        let session = SessionId.value sessionId
        let run = LogicalRunId.value logicalRunId

        match validate "SessionId" session, validate "LogicalRunId" run with
        | Ok safeSession, Ok safeRun ->
            Ok(
                StudentQaNode.join2 (
                    StudentQaNode.join2 (StudentQaNode.join2 (gitDirectory, "wanxiangshu"), "student"),
                    safeSession
                )
                |> fun parent -> StudentQaNode.join2 (parent, safeRun)
            )
        | Error error, _
        | _, Error error -> Error error

    let pathFor sessionId logicalRunId =
        directoryFor sessionId logicalRunId
        |> Result.map (fun directory -> StudentQaNode.join2 (directory, "QA.md"))

    let readFatal path =
        try
            if StudentQaNode.existsSync path then
                Ok(StudentQaNode.decodeUtf8Fatal (StudentQaNode.readFileSync path))
            else
                Ok ""
        with ex ->
            Error(sprintf "Student QA UTF-8 read failed: %s" ex.Message)

    let fsyncDirectory directory =
        let fd = StudentQaNode.openSync (directory, "r", 0o700)

        try
            StudentQaNode.fsyncSync fd
        finally
            StudentQaNode.closeSync fd

    let ensureDirectory directory =
        StudentQaNode.mkdirSync (directory, {| recursive = true; mode = 0o700 |})

        // Every directory introduced below the Git-private root is private,
        // including parents mkdir({recursive=true}) may have created under a
        // permissive process umask.
        let sessionDirectory = StudentQaNode.dirname directory
        let studentDirectory = StudentQaNode.dirname sessionDirectory
        let wanxiangshuDirectory = StudentQaNode.dirname studentDirectory

        [ wanxiangshuDirectory; studentDirectory; sessionDirectory; directory ]
        |> List.iter (fun owned -> StudentQaNode.chmodSync (owned, 0o700))

    let writeAtomic directory path (expected: string) =
        let temp =
            StudentQaNode.join2 (directory, ".QA." + Guid.NewGuid().ToString("N") + ".tmp")

        let bytes = System.Text.Encoding.UTF8.GetBytes expected
        // DSL-MUTABLE: resource — atomic rename success latch for QA write
        let mutable renamed = false

        try
            let fd = StudentQaNode.openSync (temp, "wx", 0o600)

            try
                let written = StudentQaNode.writeSync (fd, bytes)

                if written <> bytes.Length then
                    raise (InvalidOperationException "Student QA write was partial")

                StudentQaNode.fsyncSync fd
            finally
                StudentQaNode.closeSync fd

            StudentQaNode.renameSync (temp, path)
            renamed <- true
            StudentQaNode.chmodSync (path, 0o600)
            fsyncDirectory directory
            Ok()
        with ex ->
            if not renamed && StudentQaNode.existsSync temp then
                try
                    StudentQaNode.unlinkSync temp
                with _ ->
                    ()

            // rename/fsync can throw after the namespace update. The complete
            // expected bytes are the only evidence that licenses acceptance.
            match readFatal path with
            | Ok actual when actual = expected -> Ok()
            | _ -> Error(sprintf "Student QA atomic commit failed: %s" ex.Message)

    member _.GitDirectory = gitDirectory

    member _.Path(sessionId: SessionId, logicalRunId: LogicalRunId) = pathFor sessionId logicalRunId

    /// Durable completion evidence for StudentCompile (EXEC-026): QA gone means
    /// final return already committed delete. Path validation failures and FS
    /// exceptions stay typed — callers must not let existsSync throw across Host.
    member _.Exists(sessionId: SessionId, logicalRunId: LogicalRunId) : Result<bool, string> =
        match pathFor sessionId logicalRunId with
        | Error error -> Error error
        | Ok path ->
            try
                Ok(StudentQaNode.existsSync path)
            with ex ->
                Error(sprintf "Student QA exists check failed: %s" ex.Message)

    member _.Read(sessionId: SessionId, logicalRunId: LogicalRunId) =
        pathFor sessionId logicalRunId |> Result.bind readFatal

    member _.Append(sessionId: SessionId, logicalRunId: LogicalRunId, entry: string) : Result<string, string> =
        match directoryFor sessionId logicalRunId, pathFor sessionId logicalRunId with
        | Ok directory, Ok path ->
            try
                ensureDirectory directory

                match readFatal path with
                | Error error -> Error error
                | Ok current ->
                    let next = StudentTeacher.appendIdempotentTail current entry

                    if next = current then
                        Ok path
                    else
                        writeAtomic directory path next |> Result.map (fun () -> path)
            with ex ->
                Error(sprintf "Student QA append failed: %s" ex.Message)
        | Error error, _
        | _, Error error -> Error error

    member _.Delete(sessionId: SessionId, logicalRunId: LogicalRunId) : Result<unit, string> =
        match directoryFor sessionId logicalRunId, pathFor sessionId logicalRunId with
        | Ok directory, Ok path ->
            try
                if StudentQaNode.existsSync path then
                    StudentQaNode.unlinkSync path
                    fsyncDirectory directory

                if StudentQaNode.existsSync path then
                    raise (InvalidOperationException "Student QA still exists after delete")

                let sessionDirectory = StudentQaNode.dirname directory
                let studentDirectory = StudentQaNode.dirname sessionDirectory

                // Only the run and now-empty session directories are owned here.
                try
                    StudentQaNode.rmdirSync directory
                with _ ->
                    ()

                if StudentQaNode.existsSync directory then
                    raise (InvalidOperationException "Student QA run directory is not empty after cleanup")

                if StudentQaNode.existsSync sessionDirectory then
                    fsyncDirectory sessionDirectory

                try
                    StudentQaNode.rmdirSync sessionDirectory
                with _ ->
                    ()

                if
                    StudentQaNode.existsSync sessionDirectory
                    && StudentQaNode.readdirSync(sessionDirectory).Length = 0
                then
                    raise (InvalidOperationException "Empty Student QA session directory remains after cleanup")

                if StudentQaNode.existsSync studentDirectory then
                    fsyncDirectory studentDirectory

                Ok()
            with ex ->
                Error(sprintf "Student QA delete failed: %s" ex.Message)
        | Error error, _
        | _, Error error -> Error error

    static member Create(workspaceDirectory: string) : Result<StudentQaStore, string> =
        try
            let gitDirectory =
                StudentQaNode.execFileSync (
                    "git",
                    [| "rev-parse"; "--absolute-git-dir" |],
                    {| cwd = workspaceDirectory
                       encoding = "utf8"
                       stdio = [| "ignore"; "pipe"; "pipe" |] |}
                )
                |> fun output -> output.Trim()

            if
                String.IsNullOrWhiteSpace gitDirectory
                || not (StudentQaNode.existsSync gitDirectory)
            then
                Error "Cannot prove the repository Git private directory"
            else
                Ok(StudentQaStore gitDirectory)
        with ex ->
            Error(sprintf "Cannot resolve the repository Git private directory: %s" ex.Message)

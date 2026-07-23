namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

module private NodeFsGateway =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, opts: obj) : unit = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

    [<Import("pid", "node:process")>]
    let nodeProcessId: int = jsNative

[<RequireQualifiedAccess>]
type GatewayError =
    | StorageFailed of reason: string
    | BootFailed of reason: string

type Gateway =
    inherit IGateway
    inherit IAsyncDisposable
    abstract BootSnapshot: BootSnapshot
    abstract RuntimeSnapshot: RuntimeSnapshot
    abstract JournalPath: string
    abstract JournalWriter: JournalWriter option

module Gateway =

    type private GatewayImpl
        (
            runtimeId: RuntimeId,
            bootSnapshot: BootSnapshot,
            initialProjectionSet: ProjectionSet,
            initialRuntimeSnapshot: RuntimeSnapshot,
            journalPath: string,
            journalWriter: JournalWriter option,
            cts: CancellationTokenSource
        ) =

        let lockObj = obj ()
        let mutable currentProjectionSet = initialProjectionSet
        let mutable currentRuntimeSnapshot = initialRuntimeSnapshot

        interface Gateway with
            member _.RuntimeId = runtimeId
            member _.BootSnapshot = bootSnapshot
            member _.ProjectionSet = lock lockObj (fun () -> currentProjectionSet)
            member _.RuntimeSnapshot = lock lockObj (fun () -> currentRuntimeSnapshot)
            member _.JournalPath = journalPath
            member _.JournalWriter = journalWriter

            member _.Append stream turnId fact =
                match journalWriter with
                | None ->
                    CommitUnknown(
                        EventId.create (Guid.NewGuid().ToString("N")),
                        JournalFailure.WriteFailed "JournalWriter not initialized"
                    )
                | Some writer ->
                    let commitRes = writer.Append stream turnId fact

                    match commitRes with
                    | Committed env ->
                        lock lockObj (fun () ->
                            let updatedProj = Fold.foldEnvelope currentProjectionSet env

                            let updatedSnapshot: RuntimeSnapshot =
                                { currentRuntimeSnapshot with
                                    Projections = updatedProj
                                    OwnLocalSeq = writer.LocalSeq }

                            currentProjectionSet <- updatedProj
                            currentRuntimeSnapshot <- updatedSnapshot)

                        commitRes
                    | CommitUnknown _ -> commitRes

            member _.DisposeAsync() =
                cts.Cancel()
                cts.Dispose()
                match journalWriter with
                | Some w -> (w :> IAsyncDisposable).DisposeAsync()
                | None -> Fable.Core.JS.Constructors.Promise.resolve() |> unbox<ValueTask>

    let private createWriterWithRetry
        (runtimesDir: string)
        (processId: int)
        (startedAt: DateTimeOffset)
        (maxAttempts: int)
        : Result<RuntimeId * JournalWriter * Envelope, GatewayError> =
        let rec loop attemptsLeft =
            if attemptsLeft <= 0 then
                Error(
                    GatewayError.StorageFailed(
                        sprintf
                            "Failed to create JournalWriter after %d attempts due to RuntimeId collision"
                            maxAttempts
                    )
                )
            else
                let runtimeIdStr = Guid.NewGuid().ToString("N")
                let runtimeId = RuntimeId.create runtimeIdStr
                let path = NodeFsGateway.pathJoin (runtimesDir, runtimeIdStr + ".ndjson")

                if NodeFsGateway.existsSync path then
                    loop (attemptsLeft - 1)
                else
                    try
                        let writer, initEnv = JournalWriter.create runtimesDir runtimeId processId startedAt
                        Ok(runtimeId, writer, initEnv)
                    with ex ->
                        Error(GatewayError.StorageFailed ex.Message)

        loop maxAttempts

    let start (baseDir: string) (cancellationToken: CancellationToken) : Task<Result<Gateway, GatewayError>> =
        task {
            try
                let runtimesDir =
                    NodeFsGateway.pathJoin (baseDir, NodeFsGateway.pathJoin (".wanxiangshu-next", "runtimes"))

                if not (NodeFsGateway.existsSync runtimesDir) then
                    NodeFsGateway.mkdirSync (runtimesDir, {| recursive = true |}) |> ignore

                let processId = NodeFsGateway.nodeProcessId

                let bootSnapshot = Boot.boot runtimesDir
                let projectionSet = Fold.apply Fold.empty bootSnapshot.Envelopes

                let startedAt = DateTimeOffset.UtcNow

                match createWriterWithRetry runtimesDir processId startedAt 10 with
                | Error err -> return Error err
                | Ok(runtimeId, journalWriter, initEnv) ->
                    let finalProjectionSet = Fold.foldEnvelope projectionSet initEnv

                    let runtimeSnapshot: RuntimeSnapshot =
                        { Frontier = bootSnapshot.Frontier
                          Projections = finalProjectionSet
                          OwnRuntimeId = Some runtimeId
                          OwnLocalSeq = journalWriter.LocalSeq }

                    let cts = new CancellationTokenSource()

                    let gatewayInstance =
                        new GatewayImpl(
                            runtimeId,
                            bootSnapshot,
                            finalProjectionSet,
                            runtimeSnapshot,
                            journalWriter.FilePath,
                            Some journalWriter,
                            cts
                        )
                        :> Gateway

                    return Ok gatewayInstance
            with ex ->
                return Error(GatewayError.BootFailed($"[{ex.Message}]"))
        }

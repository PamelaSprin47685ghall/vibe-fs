namespace Wanxiangshu.Next.OpenCode

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

[<RequireQualifiedAccess>]
type GatewayError =
    | StorageFailed of reason: string
    | BootFailed of reason: string

type Gateway =
    inherit IAsyncDisposable
    abstract RuntimeId: RuntimeId
    abstract BootSnapshot: BootSnapshot
    abstract ProjectionSet: ProjectionSet
    abstract RuntimeSnapshot: RuntimeSnapshot
    abstract JournalPath: string
    abstract JournalWriter: JournalWriter option

module Gateway =

    type private GatewayImpl
        (
            runtimeId: RuntimeId,
            bootSnapshot: BootSnapshot,
            projectionSet: ProjectionSet,
            runtimeSnapshot: RuntimeSnapshot,
            journalPath: string,
            journalWriter: JournalWriter option,
            cts: CancellationTokenSource
        ) =

        interface Gateway with
            member _.RuntimeId = runtimeId
            member _.BootSnapshot = bootSnapshot
            member _.ProjectionSet = projectionSet
            member _.RuntimeSnapshot = runtimeSnapshot
            member _.JournalPath = journalPath
            member _.JournalWriter = journalWriter

            member _.DisposeAsync() =
                ValueTask(
                    task {
                        cts.Cancel()
                        cts.Dispose()

                        match journalWriter with
                        | Some w -> do! (w :> IAsyncDisposable).DisposeAsync().AsTask()
                        | None -> ()
                    }
                )

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
                let path = Path.Combine(runtimesDir, runtimeIdStr + ".ndjson")

                if File.Exists(path) then
                    loop (attemptsLeft - 1)
                else
                    try
                        let writer, initEnv = JournalWriter.create runtimesDir runtimeId processId startedAt
                        Ok(runtimeId, writer, initEnv)
                    with
                    | :? IOException as ex when File.Exists(path) -> loop (attemptsLeft - 1)
                    | :? IOException as ex -> Error(GatewayError.StorageFailed ex.Message)
                    | ex -> Error(GatewayError.StorageFailed ex.Message)

        loop maxAttempts

    let start (baseDir: string) (cancellationToken: CancellationToken) : Task<Result<Gateway, GatewayError>> =
        task {
            try
                let runtimesDir = Path.Combine(baseDir, ".wanxiangshu-next", "runtimes")
                Directory.CreateDirectory(runtimesDir) |> ignore

                let bootSnapshot = Boot.boot runtimesDir
                let projectionSet = Fold.apply Fold.empty bootSnapshot.Envelopes

                let processId = System.Diagnostics.Process.GetCurrentProcess().Id
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

                    let cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

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
                return Error(GatewayError.BootFailed ex.Message)
        }

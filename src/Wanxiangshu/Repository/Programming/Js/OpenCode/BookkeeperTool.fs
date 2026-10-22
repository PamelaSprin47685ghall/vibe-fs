namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js

/// Bookkeeper provider verb: one JavaScript program atomically reshapes the
/// staged question/answer Case. The sandbox has no filesystem capability.
///
/// The product decisions (prose, DSL, write-once rule, rendering) live in
/// `BookkeeperPolicy`; this binding keeps the sandbox call and the host tool
/// shape.
module JsBookkeeperTool =

    let private prose language path = BookkeeperPolicy.prose language path

    let private executeProgram language (args: HostToolArguments) (sessionId: SessionId) =
        taskResult {
            let! txId =
                match BookkeeperRuntime.tryTxId (SessionId.value sessionId) with
                | None -> Error(prose language BookkeeperPolicy.Path.NoTransaction)
                | Some txId -> Ok txId

            let! program =
                match args.OptionalText "program" with
                | None -> Error(prose language BookkeeperPolicy.Path.ProgramRequired)
                | Some program -> Ok program

            let! question, answer = BookkeeperStaging.snapshot txId
            let staged = BookkeeperPolicy.newStaged ()
            let api = BookkeeperPolicy.createApi language question answer staged

            let! handle =
                JsSandbox.runSurface
                    BookkeeperPolicy.runtimeBaseClass
                    program
                    api
                    10000
                    (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L)
                |> TaskResultCE.ofTask

            let! json = JsSandbox.awaitResult handle (1 <<< 20) |> TaskResult.mapError JsFailure.reason
            let! value = JsToolsData.parse json |> Result.mapError JsFailure.reason
            do! BookkeeperStaging.apply txId staged.Question staged.Answer
            return value
        }

    /// One call. The session is the only fact the tool needs from its caller:
    /// the staged transaction is bound to it.
    let execute (args: HostToolArguments) (sessionId: SessionId) =
        task {
            let language = BookkeeperPolicy.callLanguage ()
            let! result = executeProgram language args sessionId

            return
                match result with
                | Error reason -> BookkeeperPolicy.failed language reason
                | Ok value -> BookkeeperPolicy.succeeded language value
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = BookkeeperPolicy.descriptionLanguage ()

        { Name = "todowrite-for-bookkeeper"
          Description = BookkeeperPolicy.assembleDescription language
          Arguments =
            [ "program", ToolHostCodec.stringSchemaDescribed (prose language BookkeeperPolicy.Path.ArgProgram) factory ]
          Execute = fun args ctx -> execute args (SessionId.create ctx.SessionId) }

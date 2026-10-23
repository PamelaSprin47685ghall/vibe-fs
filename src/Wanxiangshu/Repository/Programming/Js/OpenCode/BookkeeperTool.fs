namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
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

    /// Resolve the staged transaction and the program source; every refusal is
    /// rendered at this boundary, so the sandbox only ever sees a proven call.
    let private stageRequest
        (language: ProviderLanguage)
        (args: HostToolArguments)
        (sessionId: SessionId)
        : Result<string * string * string * string, string> =
        result {
            let! txId =
                BookkeeperRuntime.tryTxId (SessionId.value sessionId)
                |> Result.requireSome (prose language BookkeeperPolicy.Path.NoTransaction)

            let! program =
                args.OptionalText "program"
                |> Result.requireSome (prose language BookkeeperPolicy.Path.ProgramRequired)

            let! question, answer = BookkeeperStaging.snapshot txId
            return txId, program, question, answer
        }

    /// Parse and commit the sandbox's returned JSON. The parse and the staged
    /// apply are two sequential Result steps, not a decision ladder.
    let private finishBookkeeper
        (language: ProviderLanguage)
        (txId: string)
        (staged: BookkeeperPolicy.StagedMutation)
        (json: string)
        : string =
        result {
            let! value =
                JsToolsData.parse json
                |> Result.mapError (fun failure -> BookkeeperPolicy.failed language (JsFailure.reason failure))

            do!
                BookkeeperStaging.apply txId staged.Question staged.Answer
                |> Result.mapError (fun err -> BookkeeperPolicy.failed language err)

            return BookkeeperPolicy.succeeded language value
        }
        |> function
            | Ok result -> result
            | Error rendered -> rendered

    let private runBookkeeperSandbox
        (language: ProviderLanguage)
        (txId: string)
        (program: string)
        (question: string)
        (answer: string)
        : Task<string> =
        task {
            let staged = BookkeeperPolicy.newStaged ()
            let api = BookkeeperPolicy.createApi language question answer staged

            let! handle =
                JsSandbox.runSurface
                    BookkeeperPolicy.runtimeBaseClass
                    program
                    api
                    10000
                    (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L)

            let! sandboxResult = JsSandbox.awaitResult handle (1 <<< 20)

            match sandboxResult with
            | Error failure ->
                let reason = JsFailure.reason failure
                return BookkeeperPolicy.failed language reason
            | Ok json -> return finishBookkeeper language txId staged json
        }

    /// One call. The session is the only fact the tool needs from its caller:
    /// the staged transaction is bound to it.
    let execute (args: HostToolArguments) (sessionId: SessionId) =
        let language = BookkeeperPolicy.callLanguage ()

        match stageRequest language args sessionId with
        | Error err -> Task.FromResult(BookkeeperPolicy.failed language err)
        | Ok(txId, program, question, answer) -> runBookkeeperSandbox language txId program question answer

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = BookkeeperPolicy.descriptionLanguage ()

        { Name = "todowrite-for-bookkeeper"
          Description = BookkeeperPolicy.assembleDescription language
          Arguments =
            [ "program", ToolHostCodec.stringSchemaDescribed (prose language BookkeeperPolicy.Path.ArgProgram) factory ]
          Execute = fun args ctx -> execute args (SessionId.create ctx.SessionId) }

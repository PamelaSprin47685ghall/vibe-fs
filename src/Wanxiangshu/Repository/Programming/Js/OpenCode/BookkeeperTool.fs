namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
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

    /// One call. The session is the only fact the tool needs from its caller:
    /// the staged transaction is bound to it.
    let execute (args: HostToolArguments) (sessionId: SessionId) =
        task {
            let language = BookkeeperPolicy.callLanguage ()

            match BookkeeperRuntime.tryTxId (SessionId.value sessionId) with
            | None -> return BookkeeperPolicy.failed language (prose language BookkeeperPolicy.Path.NoTransaction)
            | Some txId ->
                match args.OptionalText "program" with
                | None -> return BookkeeperPolicy.failed language (prose language BookkeeperPolicy.Path.ProgramRequired)
                | Some program ->
                    match BookkeeperStaging.snapshot txId with
                    | Error err -> return BookkeeperPolicy.failed language err
                    | Ok(question, answer) ->
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
                        | Ok json ->
                            match JsToolsData.parse json with
                            | Error failure ->
                                let reason = JsFailure.reason failure
                                return BookkeeperPolicy.failed language reason
                            | Ok value ->
                                match BookkeeperStaging.apply txId staged.Question staged.Answer with
                                | Error err -> return BookkeeperPolicy.failed language err
                                | Ok() -> return BookkeeperPolicy.succeeded language value
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = BookkeeperPolicy.descriptionLanguage ()

        { Name = "todowrite-for-bookkeeper"
          Description = BookkeeperPolicy.assembleDescription language
          Arguments =
            [ "program", ToolHostCodec.stringSchemaDescribed (prose language BookkeeperPolicy.Path.ArgProgram) factory ]
          Execute = fun args ctx -> execute args (SessionId.create ctx.SessionId) }

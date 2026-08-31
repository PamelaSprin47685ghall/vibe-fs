namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// JS-native owner boundary for the Bookkeeper runtime and its staged provider
/// transaction. Session ports remain opaque capabilities; staging snapshots,
/// result envelopes, and tool metadata are plain JavaScript values.
module CasebookBookkeeperSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private requiredString (fieldName: string) (value: obj) : Result<string, string> =
        let isString: bool = emitJsExpr value "typeof $0 === 'string'"

        if not isString || String.IsNullOrWhiteSpace(unbox<string> value) then
            Error(sprintf "invalid bookkeeper owner descriptor: %s must be a non-empty string" fieldName)
        else
            Ok(unbox<string> value)

    let private ownerProfile (descriptor: obj) =
        if isNullish descriptor then
            Error "invalid bookkeeper owner descriptor: descriptor must be an object"
        else
            result {
                let! sessionId = requiredString "sessionId" descriptor?sessionId
                let! logicalRunId = requiredString "logicalRunId" descriptor?logicalRunId

                let! authorityRootUserMessageId =
                    requiredString "authorityRootUserMessageId" descriptor?authorityRootUserMessageId

                let! agent = requiredString "agent" descriptor?agent

                let! rootSelection =
                    ParticipantIdentity.resolveAtRoot agent
                    |> Result.map PromptAuthority.IdentitySeed.RootSelection
                    |> Result.mapError (sprintf "invalid bookkeeper owner descriptor agent: %A")

                let! profile =
                    PromptAuthority.createAuthorityExecutionProfileFromSeed
                        (SessionId.create sessionId)
                        (LogicalRunId.create logicalRunId)
                        (AuthorityRootUserMessageId.create authorityRootUserMessageId)
                        PromptAuthority.RootAuthorityKind.HumanRoot
                        rootSelection

                return sessionId, profile
            }

    let private ownerLookup (ownerDescriptors: obj) =
        let isArray: bool = emitJsExpr ownerDescriptors "Array.isArray($0)"

        if not isArray then
            Error "invalid bookkeeper owner descriptors: expected an array"
        else
            unbox<obj array> ownerDescriptors
            |> Array.toList
            |> List.traverseResultM ownerProfile
            |> Result.bind (fun owners ->
                let lookup = Map.ofList owners

                if Map.count lookup <> List.length owners then
                    Error "invalid bookkeeper owner descriptors: duplicate sessionId"
                else
                    Ok lookup)

    /// Configure the Host session capability with explicit immutable authority owners.
    let setRuntime (port: obj) (ownerDescriptors: obj) : obj =
        match ownerLookup ownerDescriptors with
        | Error error -> box {| ok = false; error = error |}
        | Ok owners ->
            BookkeeperRuntime.setRuntime
                (unbox<Wanxiangshu.OpenCode.ISessionHostPort> port)
                (SessionId.value >> (fun sessionId -> Map.tryFind sessionId owners))

            box {| ok = true |}

    let resetRuntime () : unit = BookkeeperRuntime.resetRuntime ()

    let bindSession (sessionId: string) (txId: string) (ownerSessionId: string) : unit =
        BookkeeperRuntime.bindSession sessionId txId ownerSessionId

    let txIdFor (sessionId: string) : string = BookkeeperRuntime.txIdFor sessionId

    let beginTransaction (txId: string) (question: string) (answer: string) : unit =
        BookkeeperStaging.beginTransaction txId question answer

    let abort (txId: string) : unit = BookkeeperStaging.abort txId

    let private stagedToJs (result: Result<string * string, string>) : obj =
        match result with
        | Ok(question, answer) ->
            box
                {| ok = true
                   value = box [| box question; box answer |] |}
        | Error message -> box {| ok = false; error = message |}

    let snapshot (txId: string) : obj =
        BookkeeperStaging.snapshot txId |> stagedToJs

    let take (txId: string) : obj =
        BookkeeperStaging.take txId |> stagedToJs

    /// Execute one provider program against the currently bound transaction.
    /// Host argument/context decoding and ToolResultBound remain owner-private.
    let runProgram (sessionId: string) (program: string) : Task<string> =
        let args =
            Wanxiangshu.OpenCode.HostToolArguments(createObj [ "program" ==> program ])

        let context: Wanxiangshu.OpenCode.HostToolContext =
            { SessionId = sessionId
              Agent = None
              ToolCallId = None
              ProviderRunId = None
              PromptText = None
              AttachAbort = fun _ -> fun () -> () }

        task {
            let! result = JsBookkeeperTool.execute args context
            return ToolResultBound.bound result
        }

    /// Provider-visible metadata without exposing ToolSpec or HostSchema.
    let contract (toolModule: obj) : obj =
        let spec =
            JsBookkeeperTool.spec (Wanxiangshu.OpenCode.ToolHostCodec.factory toolModule)

        box
            {| name = spec.Name
               description = spec.Description
               argumentNames = spec.Arguments |> List.map fst |> List.toArray |}

    // These constructors keep the injected session capability opaque to JS
    // tests. They are only useful for implementing a test Host port; no DU
    // representation is returned by the semantic operations above.
    let sessionId (value: string) : obj = box (SessionId.create value)

    let sessionValue (value: obj) : string =
        SessionId.value (unbox<SessionId> value)

    let acceptedSession (value: string) : obj =
        box (Ok(SessionId.create value): Result<SessionId, string>)

    let acceptedPrompt () : obj =
        box (AdmittedWithReceipt(TransportReceipt.create "bookkeeper-accepted"))

    let failedPrompt (reason: string) : obj = box (Retryable reason)

    let aborted () : obj = box (Ok())

    let completed (value: string) : obj =
        box (
            Wanxiangshu.OpenCode.TerminalOutcome.Completed
                { SessionId = SessionId.create value
                  AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "bookkeeper"
                  ProviderRun = ProviderRunIdentity.create "bookkeeper"
                  Role = Role.Inspector
                  Directory = None
                  TerminalText = "bookkeeper completed"
                  TurnFormalText = "bookkeeper completed" }
        )

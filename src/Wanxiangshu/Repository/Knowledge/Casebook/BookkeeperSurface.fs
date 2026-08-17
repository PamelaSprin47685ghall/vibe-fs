namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// JS-native owner boundary for Bookkeeper refresh and its staged provider
/// transaction. Session ports remain opaque capabilities; staging snapshots,
/// result envelopes, and tool metadata are plain JavaScript values.
module CasebookBookkeeperSurface =

    let private storeOf (value: obj) : IEventStore = (unbox<EventStoreHandle> value).Store

    let private resultToJs (result: Result<'value, string>) (valueToJs: 'value -> obj) : obj =
        match result with
        | Ok value -> box {| ok = true; value = valueToJs value |}
        | Error message -> box {| ok = false; error = message |}

    /// Run the maintenance refresh against the canonical EventStore Current.
    let refreshStale (store: obj) (root: string) (sessionId: string) : Task<obj> =
        task {
            let! result = CasebookBookkeeper.refreshStale (storeOf store) root sessionId
            return resultToJs result box
        }

    /// Configure the opaque Host session capability used by BookkeeperRuntime.
    let setSessionPort (port: obj) : unit =
        BookkeeperRuntime.setSessionPort (unbox<Wanxiangshu.OpenCode.ISessionHostPort> port)

    let resetSessionPort () : unit = BookkeeperRuntime.resetSessionPort ()

    let bindSession (sessionId: string) (txId: string) (ownerSessionId: string) : unit =
        BookkeeperRuntime.bindSession sessionId txId ownerSessionId

    let txIdFor (sessionId: string) : string = BookkeeperRuntime.txIdFor sessionId

    let beginTransaction (txId: string) (question: string) (answer: string) : unit =
        BookkeeperStaging.beginTransaction txId question answer

    let abort (txId: string) : unit = BookkeeperStaging.abort txId

    let private stagedToJs (result: Result<string * string, string>) : obj =
        resultToJs result (fun (question, answer) -> box [| box question; box answer |])

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

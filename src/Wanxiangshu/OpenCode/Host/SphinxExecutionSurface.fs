namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Plain JS probe of the production executor. The probe supplies only the
/// physical Engineer port; inquiry, durable acceptance and tool code are real.
module SphinxExecutionSurface =
    let createWithOwner (store: EventStoreHandle) (invoke: obj -> Task<string>) (cancel: string -> Task<unit>) (ownerOf: string -> string) : obj =
        let port =
            { new ISphinxEngineerPort with
                member _.LogicalOwnerOf present = SessionId.create (ownerOf (SessionId.value present))
                member _.Invoke(owner, charge, admitted, isCancelled) =
                    task {
                        try
                            let request =
                                createObj
                                    [ "ownerSessionId" ==> SessionId.value owner
                                      "charge" ==> charge
                                      "admitted" ==> (fun child -> admitted (SessionId.create child))
                                      "isCancelled" ==> isCancelled ]
                            let! response = invoke request
                            return Ok response
                        with error ->
                            return Error error.Message
                    }
                member _.Cancel(child) = cancel (SessionId.value child) }
        box (SphinxExecution(store.Store, port))

    let create (store: EventStoreHandle) (invoke: obj -> Task<string>) (cancel: string -> Task<unit>) : obj =
        createWithOwner store invoke cancel id

    let run (execution: obj) sessionId invocationId question expectTurns (attachAbort: obj) =
        let context: HostToolContext =
            { SessionId = sessionId
              Agent = Some "engineer"
              ToolCallId = None
              ProviderRunId = None
              PromptText = Some question
              AttachAbort =
                fun callback ->
                    // JS supplies a curried callback factory, while Fable record
                    // functions use its own uncurrying marker. Adapt explicitly
                    // so registration happens now, not at eventual detach.
                    let detach: obj = Fable.Core.JsInterop.emitJsExpr (attachAbort, callback) "$0($1)"
                    fun () -> Fable.Core.JsInterop.emitJsStatement detach "$0()" }
        (unbox<SphinxExecution> execution).Run(context, invocationId, question, expectTurns)

    let tool (execution: obj) (toolModule: obj) =
        let factory = ToolHostCodec.factory toolModule
        SphinxTool.spec factory (Some(unbox<SphinxExecution> execution))
        |> ToolHostCodec.register factory

    let dispose (execution: obj) = (unbox<SphinxExecution> execution).DisposeAsync()

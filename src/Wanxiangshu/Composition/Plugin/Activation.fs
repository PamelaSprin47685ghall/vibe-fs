namespace Wanxiangshu.Composition.Plugin

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

type private ProductToolGate(ports: HostPorts) =
    interface IToolGate with
        member _.BeforeTool(call: ToolCallView) : Task<ToolCallVerdict> =
            task {
                if System.String.IsNullOrWhiteSpace call.Tool then
                    return ToolCallVerdict.Refuse "missing tool name"
                else
                    return ToolCallVerdict.Allow
            }

        member _.AfterTool(_call: ToolCallView, _result: ToolOutcome) : Task<ToolResultEdit> =
            Task.FromResult ToolResultEdit.Keep

        member _.DefineTool(_definition: ToolDefinitionView) : ToolDefinitionEdit = ToolDefinitionEdit.Keep

type private ProductTurnTransform(ports: HostPorts) =
    interface ITurnTransform with
        member _.AdmitTurn(request: TurnRequest) : Task<TurnAdmission> =
            task { return TurnAdmission.Admit request.Context.Route }

        member _.SystemPrompt(_context: TurnContext, system: string list) : Task<string list> = Task.FromResult system

        member _.ContextMessages(_context: TurnContext, _messages: ContextMessage list) : Task<ContextEdit> =
            Task.FromResult ContextEdit.Keep

        member _.ProviderParams(_context: TurnContext, _parameters: ProviderParams) : Task<ParamsVerdict> =
            Task.FromResult ParamsVerdict.Accept

type ProductHostPlugin() =

    let describeTerminal (terminal: TurnTerminal) : string =
        match terminal with
        | TurnTerminal.Completed _ -> "completed"
        | TurnTerminal.Cancelled -> "cancelled"
        | TurnTerminal.Interrupted -> "interrupted"
        | TurnTerminal.Failed f -> sprintf "failed:%A" f.Kind

    let onSignal (ports: HostPorts) (signal: HostSignal) : Task =
        task {
            match signal with
            | HostSignal.SessionIdle session ->
                ports.Diagnostics.Emit("product-signal-idle", [ "session_id", SessionId.value session ])
            | HostSignal.SessionCreated info ->
                ports.Diagnostics.Emit("product-signal-created", [ "session_id", SessionId.value info.Id ])
            | HostSignal.SessionDeleted(session, parent) ->
                let fields =
                    [ "session_id", SessionId.value session ]
                    @ (parent
                       |> Option.map (fun p -> [ "parent_id", SessionId.value p ])
                       |> Option.defaultValue [])

                ports.Diagnostics.Emit("product-signal-deleted", fields)
            | HostSignal.AttemptStarted attempt ->
                let fields =
                    [ "session_id", SessionId.value attempt.Session ]
                    @ (attempt.Message
                       |> Option.map (fun m -> [ "message_id", TranscriptMessageAddress.value m ])
                       |> Option.defaultValue [])

                ports.Diagnostics.Emit("product-signal-attempt-started", fields)
            | HostSignal.AttemptFinished(attempt, terminal) ->
                let termStr = describeTerminal terminal

                ports.Diagnostics.Emit(
                    "product-signal-attempt-finished",
                    [ "session_id", SessionId.value attempt.Session; "terminal", termStr ]
                )
            | _ -> ()
        }

    interface IHostPlugin with
        member this.Activate(ports: HostPorts) : HostResult<ProductHooks> =
            let toolGate = ProductToolGate(ports) :> IToolGate
            let turnTransform = ProductTurnTransform(ports) :> ITurnTransform

            let hooks: ProductHooks =
                { ToolGate = Some toolGate
                  Turn = Some turnTransform
                  Compaction = CompactionGate.create ()
                  Spawn = None
                  OnSignal = Some(onSignal ports)
                  Dispose = fun () -> Task.FromResult() }

            Task.FromResult(Ok hooks)

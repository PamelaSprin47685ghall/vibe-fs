namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Sphinx

module SphinxTool =
    let admission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.isAllowed role ToolPermission.Sphinx)

    let createExecution (sessions: ISessionHostPort) directory (delegates: SyncDelegateRuntime option) logicalOwnerFor =
        match directory, delegates with
        | Some root, Some runtime ->
            WorkspaceEventStore.tryCurrent (RuntimePath.gitCommonDir root)
            |> Option.map (fun store ->
                let engineers =
                    { new ISphinxEngineerPort with
                        member _.LogicalOwnerOf present = logicalOwnerFor present

                        member _.Invoke(owner, charge, admitted, isCancelled) =
                            let prepare () =
                                runtime.TryFind(owner, SyncDelegateRole.Engineer) |> Option.iter admitted
                                Task.FromResult(LlmFacing.instruction charge)

                            runtime.InvokeResponsePrepared(
                                SessionId.value owner,
                                SyncDelegateRole.Engineer,
                                charge,
                                prepare,
                                isCancelled = isCancelled
                            )

                        member _.Cancel(child) =
                            task {
                                runtime.CancelSession child
                                do! sessions.AbortChildren child
                                let! result = sessions.AbortSession child

                                return
                                    result
                                    |> Result.defaultWith (fun reason ->
                                        invalidOp ("Sphinx Engineer cancellation failed: " + reason))
                            } }

                SphinxExecution(store, engineers))
        | _ -> None

    let spec factory (execution: SphinxExecution option) : ToolSpec =
        { Name = "sphinx"
          Description =
            "Give Sphinx a question and receive its answer. The program owns investigation, standard Engineer work, evidence checks and stopping. expectTurns sets the shared expected work-item budget through a calibrated per-turn price, not a hard limit or quota. Complete inquiries support 5..511; default 12."
          Arguments =
            [ "question", ToolHostCodec.stringSchemaDescribed "The complete question to investigate." factory
              "expectTurns",
              ToolHostCodec.optionalBoundedIntegerSchema
                  TurnBudget.minimum
                  TurnBudget.maximum
                  "Expected total Engineer work items, integer 5..511 (default 12). Shared across nested inquiries, not provider calls or a quota."
                  factory ]
          Admission = admission
          Execute =
            fun args context ->
                task {
                    let expected =
                        match args.OptionalNonNegativeInteger "expectTurns" with
                        | Error _ -> invalidArg "expectTurns" "expectTurns must be a positive integer"
                        | Ok value ->
                            value
                            |> Option.iter (fun count ->
                                TurnBudget.validate count
                                |> Result.defaultWith (invalidArg "expectTurns")
                                |> ignore)

                            value

                    let runtime =
                        execution
                        |> Option.defaultWith (fun () ->
                            invalidOp "Sphinx requires the managed Engineer runtime and workspace EventStore")

                    let callId =
                        context.ToolCallId
                        |> Option.map ToolCallId.value
                        |> Option.defaultWith (fun () -> invalidOp "Sphinx tool call identity is required")

                    let invocationId = "tool:" + context.SessionId + ":" + callId
                    let! answer = runtime.Run(context, invocationId, args.Text "question", expected)
                    return JS.JSON.stringify answer
                } }

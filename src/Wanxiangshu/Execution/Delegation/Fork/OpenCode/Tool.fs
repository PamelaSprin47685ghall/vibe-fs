namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Domain
open Wanxiangshu.Host.Ports
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

/// OpenCode adapter for the fork / resume / commission commands: builds one
/// `DelegationEnvironment` per OpenCode tool call and registers the
/// declarations `ForkCommand` makes. The rules live in `ForkCommand`.
module ForkTool =

    let private arguments (args: HostToolArguments) =
        { new ICommandArguments with
            member _.Text name = args.Text name
            member _.OptionalText name = args.OptionalText name
            member _.Texts name = args.Texts name
            member _.OptionalCount name = args.OptionalNonNegativeInteger name }

    let private environment (scope: ToolRuntimeScope) (context: HostToolContext) : DelegationEnvironment =
        { Caller =
            { Session =
                if String.IsNullOrWhiteSpace context.SessionId then
                    None
                else
                    Some(SessionId.create context.SessionId)
              ProviderRunId = context.ProviderRunId
              ToolCallId = context.ToolCallId }

          Journal = scope.Journal

          ForkRuntime =
            fun () ->
                match scope.RuntimeFor context with
                | Ok runtime -> Ok runtime
                | Error reason -> Error(HostError.Refused reason)

          LogicalOwnerFor = scope.LogicalOwnerFor
          EnsureWorkDevOpsBound = scope.EnsureWorkDevOpsBound

          WorkspaceDirectory = scope.WorkspaceDirectory |> Option.map (fun path -> WorkspacePath path)

          ViewForSession = Some(fun sessionId -> scope.ViewForSession(SessionId.value sessionId))

          ManagerCapabilityFactsFor = Some(fun sessionId -> scope.ManagerCapabilityFactsFor(SessionId.value sessionId))

          CallerRole = Some(fun () -> scope.EnsureRoleFor context)
          NewHandleId = Some(fun () -> ToolHostCodec.newHandleId ())

          OpenManagerJob =
            Some(fun (jobId, managerAgent, prompt, byname, expectedToolCalls) ->
                task {
                    let! result =
                        (scope.OrchestratorHostFor context.SessionId)
                            .ForkManagerJob(
                                jobId,
                                managerAgent,
                                prompt,
                                byname = byname,
                                ?expectedToolCalls = expectedToolCalls
                            )

                    return Result.mapError (fun reason -> HostError.Refused reason) result
                })

          ContinueManagerJob =
            Some(fun (jobId, prompt, callerProviderRun, callerToolCallId, expectedToolCalls) ->
                task {
                    let! result =
                        (scope.OrchestratorHostFor context.SessionId)
                            .ContinueManagerJob(
                                jobId,
                                prompt,
                                callerProviderRun,
                                callerToolCallId,
                                ?expectedToolCalls = expectedToolCalls
                            )

                    return Result.mapError (fun reason -> HostError.Refused reason) result
                }) }

    let private schema factory (argument: CommandArgument) =
        let describe =
            match argument.Shape with
            | ArgumentShape.Text -> ToolHostCodec.stringSchemaDescribed
            | ArgumentShape.OptionalText -> ToolHostCodec.optionalStringSchemaDescribed
            | ArgumentShape.OptionalChoice choices -> ToolHostCodec.optionalEnumSchemaDescribed choices
            | ArgumentShape.OptionalCount -> ToolHostCodec.optionalNonNegativeIntegerSchemaDescribed

        argument.Name, describe argument.Description factory

    let private wire (reply: DelegationReply) =
        match reply with
        | DelegationReply.Placed instruction
        | DelegationReply.Refused instruction -> ToolResultText.tomlObjectWithInstructions [ instruction ] []

    let private spec command (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        let declaration =
            ForkCommand.declaration command (ProviderLanguageBinding.readGlobalPreference ())

        { Name = declaration.Name
          Description = declaration.Description
          Arguments = declaration.Arguments |> List.map (schema factory)
          Execute =
            fun args context ->
                task {
                    let! reply = ForkCommand.run command (environment scope context) (arguments args)
                    return wire reply
                } }

    let managerSpec factory scope =
        spec ForkCommand.Command.Fork factory scope

    let resumeSpec factory scope =
        spec ForkCommand.Command.Resume factory scope

    let orchestratorSpec factory scope =
        spec ForkCommand.Command.Commission factory scope

    let executeReceipt (scope: ToolRuntimeScope) (context: HostToolContext) (args: HostToolArguments) =
        ForkCommand.receipt (environment scope context) (arguments args)

    let executeResumeReceipt (scope: ToolRuntimeScope) (context: HostToolContext) (args: HostToolArguments) =
        ForkCommand.resumeReceipt (environment scope context) (arguments args)

    /// The placement receipt a fork or a resume hands back. It is aliased here,
    /// in the adapter namespace, under a name of its own, so that a caller
    /// outside the delegation shard can name the receipt without reaching into
    /// the product namespace.
    type PlacementReceipt = ForkReceipt

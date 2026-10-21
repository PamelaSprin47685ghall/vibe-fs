namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Domain
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

/// OpenCode adapter for the fork / resume / commission commands: binds
/// `IDelegationCommandHost` to one OpenCode tool call and registers the
/// declarations `ForkCommand` makes. The rules live in `ForkCommand`.
module ForkTool =

    let private arguments (args: HostToolArguments) =
        { new ICommandArguments with
            member _.Text name = args.Text name
            member _.OptionalText name = args.OptionalText name
            member _.OptionalCount name = args.OptionalNonNegativeInteger name }

    let private caller (scope: ToolRuntimeScope) (context: HostToolContext) =
        { new IDelegationCallerHost with
            member _.Caller =
                { Session =
                    if String.IsNullOrWhiteSpace context.SessionId then
                        None
                    else
                        Some(SessionId.create context.SessionId)
                  ProviderRunId = context.ProviderRunId
                  ToolCallId = context.ToolCallId }

            member _.Journal = scope.Journal
            member _.LogicalOwnerFor sessionId = scope.LogicalOwnerFor sessionId

            member _.EnsureWorkDevOpsBound parentSessionId =
                scope.EnsureWorkDevOpsBound parentSessionId

            member _.ForkRuntime() = scope.RuntimeFor context }

    let private host (scope: ToolRuntimeScope) (context: HostToolContext) =
        let caller = caller scope context

        { new IDelegationCommandHost with
            member _.WorkspaceDirectory = scope.WorkspaceDirectory
            member _.ViewForSession sessionId = scope.ViewForSession sessionId

            member _.ManagerCapabilityFactsFor sessionId =
                scope.ManagerCapabilityFactsFor sessionId

            member _.CallerRole() = scope.EnsureRoleFor context
            member _.NewHandleId() = ToolHostCodec.newHandleId ()

            member _.OpenManagerJob(jobId, managerAgent, prompt, byname, expectedToolCalls) =
                (scope.OrchestratorHostFor context.SessionId)
                    .ForkManagerJob(
                        jobId,
                        managerAgent,
                        prompt,
                        byname = byname,
                        ?expectedToolCalls = expectedToolCalls
                    )

            member _.ContinueManagerJob(jobId, prompt, callerProviderRun, callerToolCallId, expectedToolCalls) =
                (scope.OrchestratorHostFor context.SessionId)
                    .ContinueManagerJob(
                        jobId,
                        prompt,
                        callerProviderRun,
                        callerToolCallId,
                        ?expectedToolCalls = expectedToolCalls
                    )
          interface IDelegationCallerHost with
              member _.Caller = caller.Caller
              member _.Journal = caller.Journal
              member _.LogicalOwnerFor sessionId = caller.LogicalOwnerFor sessionId

              member _.EnsureWorkDevOpsBound parentSessionId =
                  caller.EnsureWorkDevOpsBound parentSessionId

              member _.ForkRuntime() = caller.ForkRuntime() }

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
          Admission = ToolAdmission.OfficeRole(fun _ role -> ForkCommand.admits command role)
          Execute =
            fun args context ->
                task {
                    let! reply = ForkCommand.run command (host scope context) (arguments args)
                    return wire reply
                } }

    let managerSpec factory scope =
        spec ForkCommand.Command.Fork factory scope

    let resumeSpec factory scope =
        spec ForkCommand.Command.Resume factory scope

    let orchestratorSpec factory scope =
        spec ForkCommand.Command.Commission factory scope

    let executeReceipt (scope: ToolRuntimeScope) (context: HostToolContext) (args: HostToolArguments) =
        ForkCommand.receipt (host scope context) (arguments args)

    let executeResumeReceipt (scope: ToolRuntimeScope) (context: HostToolContext) (args: HostToolArguments) =
        ForkCommand.resumeReceipt (host scope context) (arguments args)

    /// The placement receipt a fork or a resume hands back. It is aliased here,
    /// in the adapter namespace, under a name of its own, so that a caller
    /// outside the delegation shard can name the receipt without reaching into
    /// the product namespace.
    type PlacementReceipt = ForkReceipt

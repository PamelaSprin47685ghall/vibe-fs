namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open ToolHostCodec

/// Synchronous Inspector delegation via reusable SyncDelegate Session.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module InspectorTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/inspect/description"

        [<Literal>]
        let ArgCharge = "tool/inspect/arg-charge"

        [<Literal>]
        let ArgKeywords = "tool/inspect/arg-keywords"

        [<Literal>]
        let Unavailable = "tool/inspect/unavailable"

        [<Literal>]
        let AuthorityRequired = "tool/inspect/authority-required"

        [<Literal>]
        let NeedsCharge = "tool/inspect/needs-charge"

        [<Literal>]
        let Incomplete = "tool/inspect/incomplete"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private consequence ctx path subs =
        tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

    let private invoke
        (sd: SyncDelegateRuntime)
        (role: SyncDelegateRole)
        (context: HostToolContext)
        (charge: string)
        (prepareProviderPrompt: unit -> Task<LlmFacing.Document>)
        (batch: SyncDelegateBatch option)
        (expectedToolCalls: int option)
        =
        match batch with
        | Some semanticBatch ->
            sd.InvokeBatchPrepared(
                context.SessionId,
                role,
                charge,
                semanticBatch,
                prepareProviderPrompt,
                ?expectedToolCalls = expectedToolCalls
            )
        | None ->
            sd.InvokePrepared(
                context.SessionId,
                role,
                charge,
                prepareProviderPrompt,
                ?expectedToolCalls = expectedToolCalls
            )
            |> TaskValue.map (Result.map SyncDelegateInvocationResult.WorkRecord)

    let private renderResult (context: HostToolContext) (result: Result<SyncDelegateInvocationResult, string>) =
        match result with
        | Ok(SyncDelegateInvocationResult.WorkRecord workRecord) ->
            let instructions = [ workRecord ] |> List.filter (String.IsNullOrWhiteSpace >> not)
            tomlObjectWithInstructions instructions []
        | Ok(SyncDelegateInvocationResult.MergedInto canonicalCall) ->
            tomlObjectWithInstructions [ SyncDelegateBatching.mergedInstruction (lang context) canonicalCall ] []
        | Error _ -> consequence context Path.Incomplete Map.empty

    let private execute
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            let charge = args.Text "charge"
            let keywords = args.Text "keywords"
            let estimate = DelegatedToolEstimate.decode args

            match
                syncDelegate, String.IsNullOrWhiteSpace context.SessionId, estimate, String.IsNullOrWhiteSpace charge
            with
            | None, _, _, _ -> return consequence context Path.Unavailable Map.empty
            | Some _, true, _, _ -> return consequence context Path.AuthorityRequired Map.empty
            | Some _, false, Error _, _ -> return consequence context DelegatedToolEstimate.InvalidPath Map.empty
            | Some _, false, Ok _, true -> return consequence context Path.NeedsCharge (Map [ "tool", "inspect" ])
            | Some sd, false, Ok expectedToolCalls, false ->
                let prepareProviderPrompt () =
                    RepositoryWarmStart.prepareDocument
                        (SessionId.create context.SessionId)
                        Role.Inspector
                        scope.WorkspaceDirectory
                        keywords
                        charge
                    |> TaskValue.map (Result.defaultValue (LlmFacing.instruction charge))

                let! batch = SyncDelegateBatching.resolve sd scope SyncDelegateRole.Inspector context

                let! result =
                    invoke sd SyncDelegateRole.Inspector context charge prepareProviderPrompt batch expectedToolCalls

                return renderResult context result
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "inspect"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "charge",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgCharge Map.empty) factory
              "keywords",
              ToolHostCodec.optionalStringSchemaDescribed
                  (ProviderProse.render language Path.ArgKeywords Map.empty)
                  factory
              "expected_tool_calls", DelegatedToolEstimate.schema language factory ]
          Execute = execute scope syncDelegate }

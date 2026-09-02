namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Investigation.WarmStart
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
        ProviderLanguageBinding.forSessionText ctx.SessionId

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

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Inspect)

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
          Admission = admission
          Execute = execute scope syncDelegate }

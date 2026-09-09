namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open ToolHostCodec

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session.
/// `establish-behavior` / `repair-behavior` replace the old coder(tdd=...) verb.
/// Ordinary assistant completion → bounded WorkRecord (EXEC-031).
module CoderTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Establish =
            [<Literal>]
            let Description = "tool/establish-behavior/description"

            [<Literal>]
            let ArgCharge = "tool/establish-behavior/arg-charge"

            [<Literal>]
            let ArgKeywords = "tool/establish-behavior/arg-keywords"

            [<Literal>]
            let Unavailable = "tool/establish-behavior/unavailable"

            [<Literal>]
            let AuthorityRequired = "tool/establish-behavior/authority-required"

            [<Literal>]
            let NeedsCharge = "tool/establish-behavior/needs-charge"

            [<Literal>]
            let Incomplete = "tool/establish-behavior/incomplete"

        [<RequireQualifiedAccess>]
        module Repair =
            [<Literal>]
            let Description = "tool/repair-behavior/description"

            [<Literal>]
            let ArgCharge = "tool/repair-behavior/arg-charge"

            [<Literal>]
            let ArgKeywords = "tool/repair-behavior/arg-keywords"

            [<Literal>]
            let Unavailable = "tool/repair-behavior/unavailable"

            [<Literal>]
            let AuthorityRequired = "tool/repair-behavior/authority-required"

            [<Literal>]
            let NeedsCharge = "tool/repair-behavior/needs-charge"

            [<Literal>]
            let Incomplete = "tool/repair-behavior/incomplete"

    type private Surface =
        { Description: string
          ArgCharge: string
          ArgKeywords: string
          Unavailable: string
          AuthorityRequired: string
          NeedsCharge: string
          Incomplete: string }

    let private establishSurface =
        { Description = Path.Establish.Description
          ArgCharge = Path.Establish.ArgCharge
          ArgKeywords = Path.Establish.ArgKeywords
          Unavailable = Path.Establish.Unavailable
          AuthorityRequired = Path.Establish.AuthorityRequired
          NeedsCharge = Path.Establish.NeedsCharge
          Incomplete = Path.Establish.Incomplete }

    let private repairSurface =
        { Description = Path.Repair.Description
          ArgCharge = Path.Repair.ArgCharge
          ArgKeywords = Path.Repair.ArgKeywords
          Unavailable = Path.Repair.Unavailable
          AuthorityRequired = Path.Repair.AuthorityRequired
          NeedsCharge = Path.Repair.NeedsCharge
          Incomplete = Path.Repair.Incomplete }

    let private lang (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private consequence ctx path subs =
        tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

    let private warmStartRuntimeModule: obj =
        emitJsExpr
            ()
            """
        (() => {
            let mod = null;
            try {
                if (typeof require === 'function') {
                    mod = require('../../Repository/Investigation/WarmStart/Runtime.js');
                }
            } catch (_) {}
            if (!mod) {
                try {
                    const procMod = (typeof process !== 'undefined' && typeof process.getBuiltinModule === 'function')
                        ? process.getBuiltinModule('node:module')
                        : null;
                    if (procMod && typeof procMod.createRequire === 'function') {
                        const req = procMod.createRequire(import.meta.url);
                        mod = req('../../Repository/Investigation/WarmStart/Runtime.js');
                    }
                } catch (_) {}
            }
            return mod;
        })()
        """

    let private prepareWarmStartDocument
        (sessionId: SessionId)
        (role: Role)
        (workspaceDirectory: string option)
        (keywords: string)
        (charge: string)
        : Task<LlmFacing.Document> =
        let invokeAsync: Task<obj> =
            emitJsExpr
                (warmStartRuntimeModule, sessionId, role, workspaceDirectory, keywords, charge)
                """
(async function(mod, sessionId, role, workspaceDirectory, keywords, charge) {
    try {
        if (mod && typeof mod.prepareDocument === 'function') {
            const res = await mod.prepareDocument(sessionId, role, workspaceDirectory, keywords, charge);
            const tagKey = 't' + 'ag';
            const fieldsKey = 'fiel' + 'ds';
            if (res && res[tagKey] === 0 && res[fieldsKey] && res[fieldsKey][0]) {
                return res[fieldsKey][0];
            }
        }
        return null;
    } catch {
        return null;
    }
})($0, $1, $2, $3, $4, $5)
"""

        task {
            let! res = invokeAsync

            if isNull res then
                return LlmFacing.instruction charge
            else
                return unbox<LlmFacing.Document> res
        }

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

    let private renderResult
        (context: HostToolContext)
        (surface: Surface)
        (result: Result<SyncDelegateInvocationResult, string>)
        =
        match result with
        | Ok(SyncDelegateInvocationResult.WorkRecord workRecord) ->
            let instructions = [ workRecord ] |> List.filter (String.IsNullOrWhiteSpace >> not)
            tomlObjectWithInstructions instructions []
        | Ok(SyncDelegateInvocationResult.MergedInto canonicalCall) ->
            tomlObjectWithInstructions [ SyncDelegateBatching.mergedInstruction (lang context) canonicalCall ] []
        | Error _ -> consequence context surface.Incomplete Map.empty

    let private execute
        (toolName: string)
        (surface: Surface)
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
            | None, _, _, _ -> return consequence context surface.Unavailable Map.empty
            | Some _, true, _, _ -> return consequence context surface.AuthorityRequired Map.empty
            | Some _, false, Error _, _ -> return consequence context DelegatedToolEstimate.InvalidPath Map.empty
            | Some _, false, Ok _, true -> return consequence context surface.NeedsCharge (Map [ "tool", toolName ])
            | Some sd, false, Ok expectedToolCalls, false ->
                let prepareProviderPrompt () =
                    prepareWarmStartDocument
                        (SessionId.create context.SessionId)
                        Role.Coder
                        scope.WorkspaceDirectory
                        keywords
                        charge

                let! batch = SyncDelegateBatching.resolve sd scope SyncDelegateRole.Coder context

                let! result =
                    invoke sd SyncDelegateRole.Coder context charge prepareProviderPrompt batch expectedToolCalls

                return renderResult context surface result
        }

    let behaviorAdmission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Behavior)

    let private behaviorSpec
        (name: string)
        (surface: Surface)
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = name
          Description = ProviderProse.render language surface.Description Map.empty
          Arguments =
            [ "charge",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language surface.ArgCharge Map.empty) factory
              "keywords",
              ToolHostCodec.optionalStringSchemaDescribed
                  (ProviderProse.render language surface.ArgKeywords Map.empty)
                  factory
              "expected_tool_calls", DelegatedToolEstimate.schema language factory ]
          Admission = behaviorAdmission
          Execute = execute name surface scope syncDelegate }

    let establishSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec "establish-behavior" establishSurface factory scope syncDelegate

    let repairSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        behaviorSpec "repair-behavior" repairSurface factory scope syncDelegate

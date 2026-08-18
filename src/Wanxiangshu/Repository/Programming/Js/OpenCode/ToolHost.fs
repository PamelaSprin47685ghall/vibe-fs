namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
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
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
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
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.OpenCode

/// Coexistence seam between builtin filesystem fallbacks and generated js-*.
/// GrandRewrite keeps read/edit/write/glob/grep as normal primitive fallback:
/// their Host descriptions are left untouched. Preference for intent-level
/// programs is taught inside the generated js-* contract and its Ultra Example.
module BuiltinToolDescriptionHook =

    [<Emit("$0===undefined||$0===null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// The builtin filesystem tools the hook may annotate (凡存在者).
    let BuiltinFilesystemTools =
        set [ "read"; "edit"; "write"; "glob"; "grep"; "patch" ]

    let annotate (_builtinName: string) (description: string) (_jsRoleToolName: string) : string = description

    /// JS-003: the hook must not recommend a tool the provider cannot see.
    /// `visibleToolNames` is the current Attempt's tool set; a recommendation
    /// outside it is a lying hook and fails closed.
    let validateRecommendation (jsRoleToolName: string) (visibleToolNames: Set<string>) : Result<unit, string> =
        if Set.contains jsRoleToolName visibleToolNames then
            Ok()
        else
            Error(
                ProviderProse.render
                    (ProviderLanguageBinding.readGlobalPreference ())
                    JsCanonicalDescription.Path.HookNotVisible
                    (Map [ "tool", jsRoleToolName ])
            )

/// PROMPT-019: load already-localized js-program prose. Domain assembles;
/// this module binds language.
module JsDescriptionAssets =

    let private text (lang: ProviderLanguage) (path: string) =
        ProviderProse.render lang path Map.empty

    let private template (lang: ProviderLanguage) (path: string) =
        ProviderResources.requireLanguagePair path
        ProviderResources.readText lang path

    let load (lang: ProviderLanguage) : JsCanonicalDescription.Prose =
        { Header = text lang JsCanonicalDescription.Path.Header
          Footer = text lang JsCanonicalDescription.Path.Footer
          Contract = template lang JsCanonicalDescription.Path.Contract
          ContractParallelEdits = template lang JsCanonicalDescription.Path.ContractParallelEdits
          ContractParallelReads = template lang JsCanonicalDescription.Path.ContractParallelReads
          VerbRead = text lang JsCanonicalDescription.Path.VerbRead
          VerbSearch = text lang JsCanonicalDescription.Path.VerbSearch
          VerbTransform = text lang JsCanonicalDescription.Path.VerbTransform
          VerbRewrite = text lang JsCanonicalDescription.Path.VerbRewrite
          VerbCreate = text lang JsCanonicalDescription.Path.VerbCreate
          ReadRules = text lang JsCanonicalDescription.Path.ReadRules
          GlobRules = text lang JsCanonicalDescription.Path.GlobRules
          GrepRules = text lang JsCanonicalDescription.Path.GrepRules
          EditRules = text lang JsCanonicalDescription.Path.EditRules
          WriteRules = text lang JsCanonicalDescription.Path.WriteRules
          MutationRules = text lang JsCanonicalDescription.Path.MutationRules
          UltraFraming = text lang JsCanonicalDescription.Path.UltraFraming
          UltraUnavailable = text lang JsCanonicalDescription.Path.UltraUnavailable
          MechanicalSemantic = text lang JsCanonicalDescription.Path.MechanicalSemantic
          CommentAnchorOwnSearch = text lang JsCanonicalDescription.Path.CommentAnchorOwnSearch
          CommentIgnoreGy = text lang JsCanonicalDescription.Path.CommentIgnoreGy
          CommentHostCapability = text lang JsCanonicalDescription.Path.CommentHostCapability
          ReasonEmptyStringPattern = text lang JsCanonicalDescription.Path.ReasonEmptyStringPattern
          ReasonInvalidRegexp = text lang JsCanonicalDescription.Path.ReasonInvalidRegexp
          ReasonPatternType = text lang JsCanonicalDescription.Path.ReasonPatternType
          ReasonAnchorEmptyNames = text lang JsCanonicalDescription.Path.ReasonAnchorEmptyNames
          ReasonAnchorReserved = text lang JsCanonicalDescription.Path.ReasonAnchorReserved
          ReasonAnchorNamesDiffer = text lang JsCanonicalDescription.Path.ReasonAnchorNamesDiffer
          ReasonAnchorNamesUnique = text lang JsCanonicalDescription.Path.ReasonAnchorNamesUnique
          ReasonAnchorNotFound = text lang JsCanonicalDescription.Path.ReasonAnchorNotFound
          ReasonUnknownAnchor = text lang JsCanonicalDescription.Path.ReasonUnknownAnchor
          ReasonInvalidSlice = text lang JsCanonicalDescription.Path.ReasonInvalidSlice
          ReasonFileReadFailed = text lang JsCanonicalDescription.Path.ReasonFileReadFailed
          ReasonRunUnimplemented = text lang JsCanonicalDescription.Path.ReasonRunUnimplemented }

    let argProgram (lang: ProviderLanguage) =
        text lang JsCanonicalDescription.Path.ArgProgram

    let missingProgram (lang: ProviderLanguage) =
        text lang JsCanonicalDescription.Path.MissingProgram

/// JS-073/JS-074: a generated js-* tool spec — the dynamic counterpart of the
/// static baseSpecs. Built from a generated surface (JS-002); execution goes
/// through JsToolWorkflow (sandbox → staging → preflight → durable facts →
/// commit) and the result renders through JsToolsResult (JS-016).
module JsToolSpec =

    [<Emit("$0===undefined||$0===null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// Build a ToolSpec for one generated surface.
    ///
    /// `workspaceRoot` is where the tool executes; `persistence` enables the
    /// durable transaction facts (JS-012) when the caller has an EventStore.
    /// `modelSourceProvider` supplies the model program (the tool arguments);
    /// the first version reads it from the Host arguments payload.
    let create
        (factory: HostToolFactory)
        (surface: JsSurface)
        (workspaceRoot: string)
        (persistence: IJsTransactionPersistence option)
        (groundingAdmission: (HostToolContext -> string list -> Task<Result<unit, JsFailure>>) option)
        : ToolSpec =
        let readProgram (args: HostToolArguments) : string option = args.OptionalText "program"

        let runProgram ctx programSource =
            let deadlineEpochMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L

            match groundingAdmission with
            | None ->
                JsToolWorkflow.run
                    workspaceRoot
                    surface.BaseClassSource
                    programSource
                    10000
                    deadlineEpochMs
                    (1 <<< 20)
                    persistence
            | Some admit ->
                JsToolWorkflow.runWithMutationAdmission
                    workspaceRoot
                    surface.BaseClassSource
                    programSource
                    10000
                    deadlineEpochMs
                    (1 <<< 20)
                    persistence
                    (admit ctx)

        { Name = surface.ToolName
          Description = surface.Description
          Arguments =
            [ "program",
              ToolHostCodec.stringSchemaDescribed
                  (JsDescriptionAssets.argProgram (ProviderLanguageBinding.readGlobalPreference ()))
                  factory ]
          Execute =
            fun args ctx ->
                task {
                    match readProgram args with
                    | None ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error",
                                  ToolHostCodec.TString(
                                      JsDescriptionAssets.missingProgram (
                                          ProviderLanguageBinding.readGlobalPreference ()
                                      )
                                  ) ]
                    | Some programSource ->
                        // 10 s sandbox deadline; 1 MiB output bound (JS-054).
                        let! outcome = runProgram ctx programSource

                        return JsToolsResult.render outcome
                } }

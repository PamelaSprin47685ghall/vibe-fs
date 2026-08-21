namespace Wanxiangshu.OpenCode

open System
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

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
/// Provider schema: required `entry` + required `tip`; no legacy blog/text alias.
module ChronicleTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/chronicle/description"

        [<Literal>]
        let Remembered = "tool/chronicle/remembered"

        [<Literal>]
        let NothingToRemember = "tool/chronicle/nothing-to-remember"

        [<Literal>]
        let MissingTip = "tool/chronicle/missing-tip"

    let EmptyTextError = "CHRONICLE_EMPTY_ENFORCER_061"

    let NoLiveCycleError = "CHRONICLE_NO_LIVE_CYCLE"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    let hasLiveCycle (bloggerHost: IBloggerRuntimeHost option) (sessionId: string) : bool =
        match bloggerHost with
        | None -> false
        | Some host -> host.HasFlight sessionId

    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    let tipFieldNames () : string list =
        EnforcerCatalog.fieldNames (enforcerRules ())

    let private remembered language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.Remembered ] []

    let private nothingToRemember language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.NothingToRemember ] []

    let private missingTip language =
        ToolHostCodec.tomlObjectWithInstructions [ prose language Path.MissingTip ] []

    let private terminateSessionIfPresent (runtime: ToolRuntimeScope) sessionId : System.Threading.Tasks.Task =
        task {
            if not (String.IsNullOrWhiteSpace sessionId) then
                let! _ = runtime.TerminateSession(sessionId, NoLiveCycleError)
                ()
        }

    let private applyNoLiveCycleEffect runtime (ctx: HostToolContext) : System.Threading.Tasks.Task =
        task {
            Diagnostic.emit "chronicle-execute" [ "session_id", ctx.SessionId; "result", NoLiveCycleError ]
            do! terminateSessionIfPresent runtime ctx.SessionId
        }

    let private resultForTip language tipRaw =
        if String.IsNullOrWhiteSpace tipRaw then
            missingTip language
        else
            EnforcerCatalog.resolveByField tipRaw (enforcerRules ())
            |> Option.map (fun _ -> remembered language)
            |> Option.defaultValue (missingTip language)

    let private executeValidEntry language (args: HostToolArguments) : string =
        match tryCanonicalText (args.Text "entry") with
        | Error _ -> nothingToRemember language
        | Ok _ -> resultForTip language (args.Text "tip")

    let private executeChronicle
        (runtime: ToolRuntimeScope)
        (bloggerHost: IBloggerRuntimeHost option)
        language
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : System.Threading.Tasks.Task<ChronicleExecution> =
        task {
            let execution =
                if hasLiveCycle bloggerHost ctx.SessionId then
                    ChronicleExecution.decide true (executeValidEntry language args)
                else
                    ChronicleExecution.decide false ""

            match execution with
            | ChronicleExecution.Completed _ -> return execution
            | ChronicleExecution.NoLiveCycle ->
                do! applyNoLiveCycleEffect runtime ctx
                return execution
        }

    let spec
        (factory: HostToolFactory)
        (runtime: ToolRuntimeScope)
        (bloggerHost: IBloggerRuntimeHost option)
        : ToolSpec =
        let fields = tipFieldNames ()
        let ruleCount = List.length fields

        let catalogDescription =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.Description
                (Map [ "rule_count", string ruleCount ])

        { Name = "chronicle"
          Description = catalogDescription
          Arguments =
            [ "entry", ToolHostCodec.stringSchema factory
              "tip", ToolHostCodec.enumSchema fields factory ]
          Execute =
            fun args ctx ->
                task {
                    let language = lang ctx
                    let! execution = executeChronicle runtime bloggerHost language args ctx

                    match execution with
                    | ChronicleExecution.Completed value -> return value
                    | ChronicleExecution.NoLiveCycle ->
                        let hostError = InvalidOperationException(NoLiveCycleError)
                        return raise hostError
                } }

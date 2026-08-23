namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// JS-native owner surface for the Distiller role contract.
///
/// Distillation owns the distinction between its private fixed-cost runtime and
/// the provider-visible execution verb. Role vocabulary and tool permission
/// translation stay in their canonical owners; this surface publishes only the
/// bounded facts that output-distillation promises to callers.
module DistillationSurface =

    let private distillerRole = Role.Distiller

    /// The internal role label used by the Host-owned runtime.
    let roleLabel: string = Roles.roleLabel distillerRole

    /// The fixed fast-tier managed identity used for bounded-tail distillation.
    let managedAgentName: string = Roles.managedAgentName AgentTier.Fast distillerRole

    /// Distiller is an internal Host-owned runtime.
    let isInternalRuntime: bool = Roles.isInternal distillerRole

    /// Distiller is not a provider-visible fork or horizon target.
    let canBeForkedOrHorizonTarget: bool = not isInternalRuntime

    /// Distiller is a leaf runtime and never receives a Blogger companion.
    let hasBloggerCompanion: bool =
        CompanionTransform.allowsBloggerCompanionForAgentName managedAgentName

    /// Distiller has no execution, mutation, or judgement permissions.
    let permissionLabels: string array = RolesSurface.permissions roleLabel

    /// Distillation is invoked by the provider-visible run surface.
    let executionToolName: string = ExecutorTool.RunToolName

    /// Stable JSON-shaped contract for consumers that need one observation.
    let contract: obj =
        box
            {| role = roleLabel
               managedAgent = managedAgentName
               internalRuntime = isInternalRuntime
               publicTarget = canBeForkedOrHorizonTarget
               bloggerCompanion = hasBloggerCompanion
               permissions = permissionLabels
               executionTool = executionToolName |}

    let private languageOf (raw: string) =
        ProviderLanguage.tryParse raw |> Option.defaultValue ProviderLanguage.English

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    [<Emit("Promise.resolve($0.fork($1, $2, $3))")>]
    let private callFork (runtime: obj) (agentId: string) (prompt: string) (payload: obj) : Task<obj> = jsNative

    [<Emit("Promise.resolve($0.awaitAgent($1, $2))")>]
    let private callAwait (runtime: obj) (agentId: string) (timeoutMs: int) : Task<obj> = jsNative

    [<Emit("Promise.resolve($0.awaitRecoveryReadiness($1))")>]
    let private callReadiness (runtime: obj) (agentId: string) : Task<obj> = jsNative

    [<Emit("$0.cancel($1)")>]
    let private callCancel (runtime: obj) (agentId: string) : unit = jsNative

    let private textField (value: obj) (name: string) (fallback: string) =
        let raw = field value name
        if isNullish raw then fallback else string raw

    let private boolField (value: obj) (name: string) =
        let raw = field value name
        if isNullish raw then false else unbox<bool> raw

    let private forkResult (agentId: string) (value: obj) : Result<ForkResult, string> =
        if boolField value "ok" then
            Ok(ForkResult.Created(textField value "agentId" agentId))
        else
            Error(textField value "error" (sprintf "fork failed for %s" agentId))

    let private completion (agentId: string) (value: obj) : Result<RunCompletion, ForkError> =
        if boolField value "ok" then
            let runId = textField value "runId" (sprintf "run-%s" agentId)
            let workRecord = textField value "workRecord" ""
            let outcome = AgentCompletion.ofSimpleText agentId runId Role.Distiller workRecord

            Ok
                { RunId = runId
                  AgentName = managedAgentName
                  Role = Role.Distiller
                  Outcome = outcome
                  CompletedAt = DateTimeOffset.UtcNow }
        else
            match textField value "kind" "not-found" with
            | "waiting" -> Error ForkError.TimedOut
            | kind -> Error(ForkError.NotFound(textField value "error" (sprintf "%s:%s" kind agentId)))

    let private runtimeOf (runtime: obj) : Distillation.IDistillationRuntime =
        // DSL-MUTABLE: resource — await/readiness correlation key
        let mutable lastAwaitedAgent = ""

        { new Distillation.IDistillationRuntime with
            member _.Fork(agentId, _role, prompt, payload) =
                task {
                    let payload = payload |> Option.map box |> Option.defaultValue null
                    let! result = callFork runtime agentId prompt payload
                    return forkResult agentId result
                }

            member _.AwaitAgentWithPermit(agentId, timeoutMs) =
                task {
                    lastAwaitedAgent <- agentId
                    let! result = callAwait runtime agentId (timeoutMs |> Option.defaultValue 0)
                    return completion agentId result
                }

            member _.CurrentJournalRevision() = JournalRevision.initial

            member _.AwaitJournalChangeFrom(_fromRevision) =
                task {
                    let! _ = callReadiness runtime lastAwaitedAgent
                    return Unchecked.defaultof<JournalChange>
                }

            member _.CancelAgent(agentId) = callCancel runtime agentId }

    /// Distillation prompt contracts rendered through a plain language label.
    let distillFragmentPrompt (language: string) =
        Distillation.distillFragmentPrompt (languageOf language)

    /// Run fixed-cost tail distillation through a JSON-shaped callback runtime. The adapter keeps
    /// ForkResult, ForkError and RunCompletion representations inside this owner.
    let distillSpool (runtime: obj) (spoolPath: string) (language: string) : Task<string> =
        Distillation.distillSpool (runtimeOf runtime) spoolPath (languageOf language)

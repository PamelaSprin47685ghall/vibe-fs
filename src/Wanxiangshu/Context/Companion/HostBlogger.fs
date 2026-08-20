namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources

module internal CompanionHostBlogger =

    type BloggerDeps =
        { Sessions: ISessionHostPort
          PrimaryId: SessionId
          Durable: ICompanionDurablePort option
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          Companion: Companion
          Journal: AgentJournal option
          EffectiveAgent: string }

    /// CTX-012: covered frame count = ceil(m / 2).
    let coveredFrameCount (frameCount: int) : int =
        if frameCount <= 0 then 0 else (frameCount + 1) / 2

    /// Build typed Squash context from durable frames (exact digests, fail closed if short).
    /// Freezes RequestId + ObservedPrefixEpochId at materialization (C5).
    let tryBuildSquashContext
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        : BloggerRequestContext option =
        let m = List.length blog.Frames
        let k = coveredFrameCount m

        let selected =
            if k < 1 then
                None
            else
                let frames = List.truncate k (BlogProjection.frames blog)
                Some frames |> Option.filter (fun selected -> List.length selected = k)

        selected
        |> Option.map (fun selected ->
            let digests = selected |> List.map (fun f -> f.Digest)

            let requestId =
                BloggerRequestId.create (
                    HostDigest.sha256Hex (
                        String.concat
                            "|"
                            [ SessionId.value mainSessionId
                              SessionId.value bloggerSessionId
                              "squash"
                              string (FrameEpochId.value blog.FrameEpochId)
                              string k
                              (digests |> List.map BlobDigest.value |> String.concat ",") ]
                    )
                )

            BloggerRequestContext.Squash
                { RequestId = requestId
                  MainSessionId = mainSessionId
                  BloggerSessionId = bloggerSessionId
                  FrameEpochId = blog.FrameEpochId
                  CoveredFrameCount = k
                  FrameDigests = digests
                  ObservedPrefixEpochId = observedEpoch })


    let private sendBloggerPrompt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        : Task<Result<PromptKey, string>> =
        task {
            match deps.Journal with
            | None -> return Error "No journal: a Blogger prompt cannot be claimed"
            | Some journal ->
                let dispatcher = PromptDispatcher.forJournal journal

                // PROMPT-007 Detached: Blogger dispatch does not wait for PhysicalAccepted.
                return!
                    dispatcher.SendAgentOwnerRoot
                        deps.Sessions
                        childId
                        prompt
                        deps.EffectiveAgent
                        None
                        PromptDispatcher.AwaitMode.Detached
                        None
        }

    /// Physical send from frozen typed context (Main or Squash). No terminal wait.
    /// Transform rebuilds the provider view from durable frames + this context.
    let startFromContext (deps: BloggerDeps) (ctx: BloggerRequestContext) : Task<Result<PromptKey, string>> =
        task {
            let! childId = deps.EnsureBlogger()

            match ctx with
            | BloggerRequestContext.Main _ ->
                // Physical claim is the stable normal instruction only. Delta Toml lives in
                // CurrentRequest / durable materialize; transform rebuilds Working Record +
                // New Work + instruction. Sending raw Toml as the claim body fails mock
                // matching after restart when rebuild cannot run yet.
                let prompt =
                    ProviderProse.instructionLines
                        (ProviderProse.languageOf deps.PrimaryId)
                        CompanionPrompt.Normal
                        Map.empty
                    |> CompanionPrompt.asCommentedInstruction

                return! sendBloggerPrompt deps childId prompt
            | BloggerRequestContext.Squash _ ->
                // Physical claim body is the stable squash instruction only.
                // Frames arrive via rebuildFromContext on the transform (no raw transcript).
                let prompt =
                    ProviderProse.instructionLines
                        (ProviderProse.languageOf deps.PrimaryId)
                        CompanionPrompt.Squash
                        Map.empty
                    |> CompanionPrompt.asCommentedInstruction

                return! sendBloggerPrompt deps childId prompt
        }

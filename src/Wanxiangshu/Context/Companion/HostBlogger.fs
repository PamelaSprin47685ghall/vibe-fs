namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
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
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources

module internal CompanionHostBlogger =

    /// DSL-class: PhysicalHandle — Host dispatch handles only: `Sessions`/`Durable` own the Host session and durable ports, `PrimaryId`/`Participant` are physical dispatch coordinates, `EnsureBlogger` owns the child session creation task, `Gate` is the process-local lock object, `Companion` is the process-local runtime cell, `Journal` owns the durable journal writer; owner context-compression, law context-compression-024, proof blogger-runtime.
    type BloggerDeps =
        { Sessions: ISessionHostPort
          PrimaryId: SessionId
          Durable: ICompanionDurablePort option
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          Companion: Companion
          Journal: AgentJournal option
          Participant: string }

    let private sendClaimedBloggerPrompt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        (journal: AgentJournal)
        : Task<Result<PromptKey, string>> =
        let dispatcher = PromptDispatcher.forPrompts (PromptJournalAdapter.create journal)

        dispatcher.SendManagedAssignment
            (DispatchSessionPort.ofSessionPort deps.Sessions)
            childId
            prompt
            (fun () ->
                PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed
                    (AgentJournal.snapshot journal).AgentProjections
                    deps.PrimaryId
                    deps.Participant)
            None
            None

    let private sendBloggerPrompt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        : Task<Result<PromptKey, string>> =
        match deps.Journal with
        | None -> Task.FromResult(Error "No journal: a Blogger prompt cannot be claimed")
        | Some journal -> sendClaimedBloggerPrompt deps childId prompt journal

    /// Physical send from frozen typed context. No terminal wait.
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
        }

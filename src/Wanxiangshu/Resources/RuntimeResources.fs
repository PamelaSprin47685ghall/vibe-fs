namespace Wanxiangshu.Resources
open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

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
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Bundle of package runtime data loaded once at plugin init.
type RuntimeResources =
    {
        Prompts: PromptCatalog
        /// Rulebook selected for this RuntimeResources view (legacy/internal callers).
        EnforcerRules: EnforcerRule list
        EnglishEnforcerRules: EnforcerRule list
        SimplifiedChineseEnforcerRules: EnforcerRule list
        /// Phase 2: bilingual provider tree roots present (`resources/provider/{en,zh-CN}`).
        ProviderLanguageRootsReady: bool
    }

module RuntimeResources =

    // DSL-MUTABLE: resource — process-local installed runtime resources singleton
    let mutable private installed: RuntimeResources option = None

    let loadFor (lang: ProviderLanguage) : RuntimeResources =
        // PROMPT-017: preload both complete Rulebook locales once. Runtime session
        // projection can then select by immutable ProviderLanguage without request-time I/O.
        let englishRules = EnforcerCatalogResource.loadFor ProviderLanguage.English

        let simplifiedChineseRules =
            EnforcerCatalogResource.loadFor ProviderLanguage.SimplifiedChinese

        let rules =
            match lang with
            | ProviderLanguage.English -> englishRules
            | ProviderLanguage.SimplifiedChinese -> simplifiedChineseRules

        let prompts = PromptResources.loadForLanguage lang

        let promptsWithRulebook =
            { prompts with
                BloggerSystemPrompt =
                    EnforcerCatalogResource.composeBloggerSystemPromptFor lang prompts.BloggerSystemPrompt rules }

        { Prompts = promptsWithRulebook
          EnforcerRules = rules
          EnglishEnforcerRules = englishRules
          SimplifiedChineseEnforcerRules = simplifiedChineseRules
          ProviderLanguageRootsReady = ProviderResources.languageRootsPresent () }

    let load () : RuntimeResources = loadFor ProviderLanguage.English

    /// Single install site: plugin constructor before any consumer runs.
    let install (resources: RuntimeResources) : unit = installed <- Some resources

    let current () : RuntimeResources =
        match installed with
        | Some resources -> resources
        | None ->
            raise (
                InvalidOperationException(
                    "RuntimeResources not installed; call RuntimeResources.install (RuntimeResources.load ()) at plugin init"
                )
            )

    let enforcerRulesFor (lang: ProviderLanguage) : EnforcerRule list =
        let resources = current ()

        match lang with
        | ProviderLanguage.English -> resources.EnglishEnforcerRules
        | ProviderLanguage.SimplifiedChinese -> resources.SimplifiedChineseEnforcerRules

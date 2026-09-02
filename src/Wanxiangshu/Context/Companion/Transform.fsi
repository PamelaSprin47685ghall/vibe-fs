namespace Wanxiangshu.Context.Companion

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module CompanionTransform =

    val allowsBloggerCompanionForAgentName: agentName: string -> bool

    val coordinateBloggerContext:
        scope: PluginRuntimeScope ->
        companion: CompanionHost ->
        journal: AgentJournal option ->
        context: BloggerRequestContext ->
            Task<BloggerCoordinator.DecisionEffect>

    /// Main-session transform: Host view unchanged; material decision is sole coordinator.
    val handleCompanionTransform:
        companions: Dictionary<string, CompanionHost> ->
        gate: obj ->
        scope: PluginRuntimeScope ->
        sessionPort: ISessionHostPort ->
        journal: AgentJournal option ->
        onBloggerCreated: (SessionId -> unit) option ->
        workspaceDirectory: string option ->
        inObj: obj ->
        rawOutObj: obj ->
            Task<unit>

    val applyCompanionForOrdinaryMaterial:
        companions: Dictionary<string, CompanionHost> ->
        gate: obj ->
        scope: PluginRuntimeScope ->
        sessionPort: ISessionHostPort ->
        journal: AgentJournal option ->
        onBloggerCreated: (SessionId -> unit) option ->
        workspaceDirectory: string option ->
        isExplicitResume: (string option -> obj -> bool) ->
        projectionSessionIdOpt: string option ->
        inObj: obj ->
        outObj: obj ->
            Task<unit>

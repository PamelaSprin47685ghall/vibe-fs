namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.OpenCode
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
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity

module EnforcerCycleDecode =

    [<Literal>]
    val EmptyTextError: string = "blog cycle text is empty after canonicalisation (ENFORCER-043)"

    val lastAssistantStep: obj list -> (string * obj list * bool) option

    val extractCalls:
        obj list ->
            (string *
            (int * Wanxiangshu.Foundation.Identity.ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list *
            bool) option

    val validateCycle:
        string ->
        (int * Wanxiangshu.Foundation.Identity.ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list ->
            Result<
                (Wanxiangshu.Enforcer.Cycle.EnforcerCycle.CanonicalCycle *
                Wanxiangshu.Foundation.Identity.ToolCallId list),
                string
             >

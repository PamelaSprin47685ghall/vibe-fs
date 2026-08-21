namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

/// Host/Application hand-off for one reconciled turn. `ClaimedButUnresolved`
/// means the abort belonged to assistance, but the continuation could not be
/// established; normal terminal ownership may run, while recovery/fallback must not.
[<RequireQualifiedAccess>]
type AssistanceTurnDisposition =
    | NotAssistance
    | Handled
    | ClaimedButUnresolved

/// AGENT-031 / PROMPT-018 provider-facing assistance continuations.
/// Paths + LlmFacing plane classification here; Class A prose lives in ProviderResources.
[<RequireQualifiedAccess>]
module AssistancePrompt =

    [<Literal>]
    let Sentinel = "[NEEDHELP]"

    [<Literal>]
    let EscalationPath = "delegation/assistance-escalation"

    [<Literal>]
    let ConsultationPath = "delegation/assistance-consultation"

    [<Literal>]
    let ReturnPath = "delegation/assistance-return"

    [<Literal>]
    let ConsultationFailedPath = "delegation/assistance-consultation-failed"

    /// The sentinel is control-plane syntax, never engineering evidence. XTrace
    /// capture removes the exact bytes from reasoning before persistence while
    /// preserving every surrounding byte.
    let stripSentinel (text: string) =
        if System.String.IsNullOrEmpty text then
            text
        else
            text.Replace(Sentinel, "")

    let escalation (instructions: string list) =
        LlmFacing.renderInstructions instructions

    let advice (instructions: string list) (childWorkRecord: string) =
        LlmFacing.renderInstructions (instructions @ [ childWorkRecord ])

    let consultationFailed (instructions: string list) (reason: string) =
        LlmFacing.renderInstructions (instructions @ [ reason ])

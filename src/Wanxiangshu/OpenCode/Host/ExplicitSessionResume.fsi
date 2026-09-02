namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Persistence.Journal

/// CRASH-018: explicit, user-visible session resume. Nothing in this module is
/// reachable from plugin load or ordinary turns. `/continue` only discovers and
/// process-locally re-enlists surviving child sessions; it never repairs the old
/// tool call, appends recovery facts, or sends a prompt on the user's behalf.
[<RequireQualifiedAccess>]
module ExplicitSessionResume =

    /// CRASH-018 process-local reenlistment of the explicit-resume provider turn.
    /// The runtime scope supplies only its managed-session observation; durable
    /// authority/profile interpretation stays here.
    val observeChatMessage:
        observeManagedSession: (SessionId -> unit) ->
        journal: AgentJournal option ->
        decoded: PromptIngressCodec.DecodedMessage ->
            unit

    [<Literal>]
    val CommandName: string = "continue"

    val registerCommand: config: obj -> unit

    type AdoptExistingChild = SessionId -> HandleRecord -> Result<unit, string>

    val before:
        journal: AgentJournal option ->
        snapshot: ISessionSnapshotPort option ->
        adopt: AdoptExistingChild ->
        input: obj ->
        output: obj ->
            Task<unit>

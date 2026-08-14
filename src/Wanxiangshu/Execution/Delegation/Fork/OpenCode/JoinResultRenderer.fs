namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
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
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Orchestrator
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

/// EXEC-004 / EXEC-017 / EXEC-030: LLM-facing join wire — natural language + WorkRecord only.
/// No status / count / ordinal / kind / agent / code / message DTO plane.
module JoinResultRenderer =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let AgentReturned = "tool/join/agent-returned"

        [<Literal>]
        let AgentFailed = "tool/join/agent-failed"

        [<Literal>]
        let AgentDidNotReturn = "tool/join/agent-did-not-return"

        [<Literal>]
        let PtyEnded = "tool/join/pty-ended"

        [<Literal>]
        let PtyInterrupted = "tool/join/pty-interrupted"

        [<Literal>]
        let TerminalLabel = "tool/join/terminal-label"

        [<Literal>]
        let InterruptOperatorAbort = "tool/join/interrupt-operator-abort"

        [<Literal>]
        let InterruptUserMessage = "tool/join/interrupt-user-message"

        [<Literal>]
        let InterruptDeadline = "tool/join/interrupt-deadline"

        [<Literal>]
        let OrchestratorPublished = "tool/join/orchestrator-published"

        [<Literal>]
        let OrchestratorRejectedDirty = "tool/join/orchestrator-rejected-dirty"

        [<Literal>]
        let OrchestratorNeedsReview = "tool/join/orchestrator-needs-review"

        [<Literal>]
        let OrchestratorIntegrationFailed = "tool/join/orchestrator-integration-failed"

        [<Literal>]
        let OrchestratorEmpty = "tool/join/orchestrator-empty"

        [<Literal>]
        let ForkNothingToJoin = "tool/join/fork-nothing-to-join"

        [<Literal>]
        let ForkCancelled = "tool/join/fork-cancelled"

        [<Literal>]
        let ForkJoinInProgress = "tool/join/fork-join-in-progress"

        [<Literal>]
        let ForkNotFound = "tool/join/fork-not-found"

        [<Literal>]
        let ForkTimedOut = "tool/join/fork-timed-out"

        [<Literal>]
        let ForkMaterializationFailed = "tool/join/fork-materialization-failed"

    let private prose lang path subs = ProviderProse.render lang path subs

    let private bynameLine lang path name =
        prose lang path (Map [ "byname", name ])

    let private ensureInstruction (text: string) = text.Trim()

    let private renderEntry (instructions: string list) (body: string list) : string =
        SyntheticToml.document (instructions |> List.map ensureInstruction) body

    let private joinBlocks (blocks: string list) : string =
        (String.concat "\n\n" (blocks |> List.filter (fun s -> s <> ""))) + "\n"

    let private byname (resolveAgentName: string -> string) (agentId: string) (agentNameRaw: string) : string =
        let presented = resolveAgentName agentId

        if not (String.IsNullOrWhiteSpace presented) then
            presented.Trim()
        elif not (String.IsNullOrWhiteSpace agentNameRaw) then
            match ManagedAgent.tryParse agentNameRaw with
            | Some agent -> agent.Name
            | None -> agentNameRaw
        else
            agentId

    let private tryParseExitCode (outcome: string) : int option =
        let trimmed = outcome.Trim()

        if String.IsNullOrWhiteSpace trimmed then
            None
        elif trimmed.StartsWith("exit ", StringComparison.OrdinalIgnoreCase) then
            let rest = trimmed.Substring(5).Trim()

            match Int32.TryParse rest with
            | true, code -> Some code
            | false, _ -> None
        else
            match Int32.TryParse trimmed with
            | true, code -> Some code
            | false, _ -> None

    let private terminalBody (outcome: string) (detail: string option) : string list =
        let fields =
            match tryParseExitCode outcome with
            | Some code -> [ SyntheticToml.field "exit_code" (string code) ]
            | None -> []

        let outputText =
            match detail with
            | Some text when not (String.IsNullOrWhiteSpace text) -> text
            | _ ->
                match tryParseExitCode outcome with
                | Some _ -> ""
                | None -> outcome

        if String.IsNullOrWhiteSpace outputText then
            fields
        else
            fields
            @ [ SyntheticToml.field "output" (SyntheticToml.renderString outputText) ]

    let private terminalLabel lang (resolveTerminalLabel: string -> string) (ptyId: string) =
        let resolved = resolveTerminalLabel ptyId

        if String.IsNullOrWhiteSpace resolved then
            prose lang Path.TerminalLabel Map.empty
        else
            resolved.Trim()

    /// EXEC-017: interrupt consequences are natural language, not error DTO.
    let renderInterrupted (lang: ProviderLanguage) (reason: JoinInterruptReason) : string =
        let path =
            match reason with
            | JoinInterruptReason.OperatorAbort -> Path.InterruptOperatorAbort
            | JoinInterruptReason.UserMessageArrived -> Path.InterruptUserMessage
            | JoinInterruptReason.DeadlineExpired -> Path.InterruptDeadline

        renderEntry [ prose lang path Map.empty ] []

    let private renderAgentCompleted
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (payload: AgentCompletionPayload)
        : string =
        let name = byname resolveAgentName completion.AgentId completion.AgentName
        let instructions = [ bynameLine lang Path.AgentReturned name ]

        let body =
            if String.IsNullOrWhiteSpace payload.WorkRecord then
                []
            else
                [ SyntheticToml.comment payload.WorkRecord ]

        renderEntry instructions body

    let private renderAgentFailed
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (payload: AgentFailurePayload)
        : string =
        let name = byname resolveAgentName completion.AgentId completion.AgentName
        let instructions = [ bynameLine lang Path.AgentFailed name ]

        let body =
            if String.IsNullOrWhiteSpace payload.Message then
                []
            else
                [ SyntheticToml.comment payload.Message ]

        renderEntry instructions body

    let private renderAgentAbandoned
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (agentId: string)
        (agentNameRaw: string)
        : string =
        let name = byname resolveAgentName agentId agentNameRaw
        renderEntry [ bynameLine lang Path.AgentDidNotReturn name ] []

    let private renderPtyEnded
        (lang: ProviderLanguage)
        (resolveTerminalLabel: string -> string)
        (templatePath: string)
        (ptyId: string)
        (outcome: string)
        (detail: string option)
        : string =
        let label = terminalLabel lang resolveTerminalLabel ptyId
        renderEntry [ prose lang templatePath (Map [ "label", label ]) ] (terminalBody outcome detail)

    let private renderAgentJoinItem
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (item: AgentJoinItem)
        : string =
        match item with
        | AgentCompletedItem payload -> renderAgentCompleted lang resolveAgentName completion payload
        | AgentFailedItem payload -> renderAgentFailed lang resolveAgentName completion payload
        | AgentAbandonedItem(agentId, _) -> renderAgentAbandoned lang resolveAgentName agentId completion.AgentName

    let private renderPtyJoinItem
        (lang: ProviderLanguage)
        (resolveTerminalLabel: string -> string)
        (item: PtyJoinItem)
        : string =
        match item with
        | PtyExited payload -> renderPtyEnded lang resolveTerminalLabel Path.PtyEnded payload.PtyId payload.Outcome None
        | PtyFailed payload ->
            renderPtyEnded lang resolveTerminalLabel Path.PtyEnded payload.PtyId payload.Outcome (Some payload.Message)
        | PtyAborted payload ->
            renderPtyEnded
                lang
                resolveTerminalLabel
                Path.PtyInterrupted
                payload.PtyId
                payload.Outcome
                (Some payload.Message)

    let private renderJoinItem
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (resolveTerminalLabel: string -> string)
        (item: JoinItem)
        : string =
        match item with
        | AgentItem agentItem ->
            let nameStub =
                match agentItem with
                | AgentCompletedItem p ->
                    { RunId = p.RunId
                      AgentId = p.AgentId
                      AgentName = ""
                      Role = p.Role
                      Outcome = AgentCompleted p
                      CompletedAt = DateTimeOffset.UtcNow }
                | AgentFailedItem p ->
                    { RunId = p.RunId
                      AgentId = p.AgentId
                      AgentName = ""
                      Role = defaultArg p.Role Role.Distiller
                      Outcome = AgentFailed p
                      CompletedAt = DateTimeOffset.UtcNow }
                | AgentAbandonedItem(agentId, reason) ->
                    { RunId = "abandoned-" + agentId
                      AgentId = agentId
                      AgentName = ""
                      Role = Role.Distiller
                      Outcome = AgentAbandoned(agentId, reason)
                      CompletedAt = DateTimeOffset.UtcNow }

            renderAgentJoinItem lang resolveAgentName nameStub agentItem
        | PtyItem ptyItem -> renderPtyJoinItem lang resolveTerminalLabel ptyItem

    let private renderCompletionItem
        (lang: ProviderLanguage)
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (resolveTerminalLabel: string -> string)
        (completion: RunCompletion)
        : string =
        match JoinItem.ofRunCompletion (isPtyRun completion.RunId) completion with
        | AgentItem agentItem -> renderAgentJoinItem lang resolveAgentName completion agentItem
        | PtyItem ptyItem -> renderPtyJoinItem lang resolveTerminalLabel ptyItem

    /// EXEC-004 / EXEC-018 / EXEC-020: JoinItem batch (production JoinTool path).
    let renderJoinItemBatch
        (lang: ProviderLanguage)
        (resolveAgentName: string -> string)
        (batch: NonEmptyBatch<JoinItem>)
        (resolveTerminalLabel: string -> string)
        : string =
        NonEmptyBatch.toList batch
        |> List.map (renderJoinItem lang resolveAgentName resolveTerminalLabel)
        |> joinBlocks

    /// EXEC-004 / EXEC-018: compat surface for tests / ofRunCompletion path.
    let renderCompletedBatch
        (lang: ProviderLanguage)
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (batch: NonEmptyBatch<RunCompletion>)
        (resolveTerminalLabel: string -> string)
        : string =
        NonEmptyBatch.toList batch
        |> List.map (renderCompletionItem lang isPtyRun resolveAgentName resolveTerminalLabel)
        |> joinBlocks

    let private orchestratorLine (lang: ProviderLanguage) (verdict: OrchestratorVerdict) : string =
        let path =
            match verdict with
            | OrchestratorVerdict.Published _ -> Path.OrchestratorPublished
            | OrchestratorVerdict.RejectedDirty _ -> Path.OrchestratorRejectedDirty
            | OrchestratorVerdict.NeedsReview _ -> Path.OrchestratorNeedsReview
            | OrchestratorVerdict.IntegrationFailed _ -> Path.OrchestratorIntegrationFailed
            | OrchestratorVerdict.Empty -> Path.OrchestratorEmpty

        prose lang path Map.empty

    /// EXEC-019: orchestrator verdict batch (FIFO; caller already capped at MaxJoinBatch).
    let renderOrchestratorBatch (lang: ProviderLanguage) (verdicts: NonEmptyBatch<OrchestratorVerdict>) : string =
        NonEmptyBatch.toList verdicts
        |> List.map (fun verdict -> renderEntry [ orchestratorLine lang verdict ] [])
        |> joinBlocks

    /// True ForkError path — natural language only (not user interrupt).
    let renderForkError (lang: ProviderLanguage) (error: ForkError) (resolveAgentName: string -> string) : string =
        let line =
            match error with
            | ForkError.NothingToJoin
            | ForkError.Empty -> prose lang Path.ForkNothingToJoin Map.empty
            | ForkError.Cancelled -> prose lang Path.ForkCancelled Map.empty
            | ForkError.JoinInProgress -> prose lang Path.ForkJoinInProgress Map.empty
            | ForkError.Abandoned(id, _) ->
                let name = byname resolveAgentName id ""
                bynameLine lang Path.AgentDidNotReturn name
            | ForkError.NotFound _ -> prose lang Path.ForkNotFound Map.empty
            | ForkError.TimedOut -> prose lang Path.ForkTimedOut Map.empty
            | ForkError.TerminalMaterializationFailed _ -> prose lang Path.ForkMaterializationFailed Map.empty

        renderEntry [ line ] []

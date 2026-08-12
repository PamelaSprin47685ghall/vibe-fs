namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Orchestrator
open Wanxiangshu.Session

/// EXEC-004 / EXEC-017 / EXEC-030: LLM-facing join wire — natural language + WorkRecord only.
/// No status / count / ordinal / kind / agent / code / message DTO plane.
module JoinResultRenderer =

    let private ensureInstruction (text: string) = text.Trim()

    let private renderEntry (instructions: string list) (body: string list) : string =
        SyntheticToml.document (instructions |> List.map ensureInstruction) body

    let private joinBlocks (blocks: string list) : string =
        (String.concat "\n\n" (blocks |> List.filter (fun s -> s <> ""))) + "\n"

    let private byname (resolveAgentName: string -> string) (agentId: string) (agentNameRaw: string) : string =
        if not (String.IsNullOrWhiteSpace agentNameRaw) then
            match ManagedAgent.tryParse agentNameRaw with
            | Some agent -> agent.Name
            | None -> agentNameRaw
        else
            let raw = resolveAgentName agentId

            if String.IsNullOrWhiteSpace raw then
                agentId
            else
                match ManagedAgent.tryParse raw with
                | Some agent -> agent.Name
                | None -> raw

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

    /// EXEC-017: interrupt consequences are natural language, not error DTO.
    let renderInterrupted (reason: JoinInterruptReason) : string =
        let line =
            match reason with
            | JoinInterruptReason.OperatorAbort -> "Your waiting was interrupted."
            | JoinInterruptReason.UserMessageArrived -> "Something nearer has arrived."
            | JoinInterruptReason.DeadlineExpired -> "No return reached you before your waiting ended."

        renderEntry [ line ] []

    let private renderAgentCompleted
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (payload: AgentCompletionPayload)
        : string =
        let name = byname resolveAgentName completion.AgentId completion.AgentName
        let instructions = [ sprintf "%s has returned." name ]

        let body =
            if String.IsNullOrWhiteSpace payload.WorkRecord then
                []
            else
                [ SyntheticToml.comment payload.WorkRecord ]

        renderEntry instructions body

    let private renderAgentFailed
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (payload: AgentFailurePayload)
        : string =
        let name = byname resolveAgentName completion.AgentId completion.AgentName
        let instructions = [ sprintf "%s could not complete the charge." name ]

        let body =
            if String.IsNullOrWhiteSpace payload.Message then
                []
            else
                [ SyntheticToml.comment payload.Message ]

        renderEntry instructions body

    let private renderAgentAbandoned
        (resolveAgentName: string -> string)
        (agentId: string)
        (agentNameRaw: string)
        : string =
        let name = byname resolveAgentName agentId agentNameRaw
        renderEntry [ sprintf "%s did not return from this charge." name ] []

    let private renderPtyEnded
        (resolveTerminalLabel: string -> string)
        (labelPrefix: string)
        (ptyId: string)
        (outcome: string)
        (detail: string option)
        : string =
        let label =
            resolveTerminalLabel ptyId
            |> fun name ->
                if String.IsNullOrWhiteSpace name then
                    "Terminal"
                else
                    name.Trim()

        renderEntry [ sprintf "%s %s." label labelPrefix ] (terminalBody outcome detail)

    let private renderAgentJoinItem
        (resolveAgentName: string -> string)
        (completion: RunCompletion)
        (item: AgentJoinItem)
        : string =
        match item with
        | AgentCompletedItem payload -> renderAgentCompleted resolveAgentName completion payload
        | AgentFailedItem payload -> renderAgentFailed resolveAgentName completion payload
        | AgentAbandonedItem(agentId, _) -> renderAgentAbandoned resolveAgentName agentId completion.AgentName

    let private renderPtyJoinItem (resolveTerminalLabel: string -> string) (item: PtyJoinItem) : string =
        match item with
        | PtyExited payload -> renderPtyEnded resolveTerminalLabel "has ended" payload.PtyId payload.Outcome None
        | PtyFailed payload ->
            renderPtyEnded resolveTerminalLabel "has ended" payload.PtyId payload.Outcome (Some payload.Message)
        | PtyAborted payload ->
            renderPtyEnded resolveTerminalLabel "was interrupted" payload.PtyId payload.Outcome (Some payload.Message)

    let private renderJoinItem
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

            renderAgentJoinItem resolveAgentName nameStub agentItem
        | PtyItem ptyItem -> renderPtyJoinItem resolveTerminalLabel ptyItem

    let private renderCompletionItem
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (resolveTerminalLabel: string -> string)
        (completion: RunCompletion)
        : string =
        match JoinItem.ofRunCompletion (isPtyRun completion.RunId) completion with
        | AgentItem agentItem -> renderAgentJoinItem resolveAgentName completion agentItem
        | PtyItem ptyItem -> renderPtyJoinItem resolveTerminalLabel ptyItem

    /// EXEC-004 / EXEC-018 / EXEC-020: JoinItem batch (production JoinTool path).
    let renderJoinItemBatch
        (resolveAgentName: string -> string)
        (batch: NonEmptyBatch<JoinItem>)
        (resolveTerminalLabel: string -> string)
        : string =
        NonEmptyBatch.toList batch
        |> List.map (renderJoinItem resolveAgentName resolveTerminalLabel)
        |> joinBlocks

    /// EXEC-004 / EXEC-018: compat surface for tests / ofRunCompletion path.
    let renderCompletedBatch
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (batch: NonEmptyBatch<RunCompletion>)
        (resolveTerminalLabel: string -> string)
        : string =
        NonEmptyBatch.toList batch
        |> List.map (renderCompletionItem isPtyRun resolveAgentName resolveTerminalLabel)
        |> joinBlocks

    let private orchestratorLine (verdict: OrchestratorVerdict) : string =
        match verdict with
        | OrchestratorVerdict.Published _ -> "The charge was integrated."
        | OrchestratorVerdict.RejectedDirty _ -> "The tree was not clean enough to integrate."
        | OrchestratorVerdict.NeedsReview _ -> "The charge needs further review."
        | OrchestratorVerdict.IntegrationFailed _ -> "Integration did not succeed."
        | OrchestratorVerdict.Empty -> "There is nothing away to receive."

    /// EXEC-019: orchestrator verdict batch (FIFO; caller already capped at MaxJoinBatch).
    let renderOrchestratorBatch (verdicts: NonEmptyBatch<OrchestratorVerdict>) : string =
        NonEmptyBatch.toList verdicts
        |> List.map (fun verdict -> renderEntry [ orchestratorLine verdict ] [])
        |> joinBlocks

    /// True ForkError path — natural language only (not user interrupt).
    let renderForkError (error: ForkError) (resolveAgentName: string -> string) : string =
        let line =
            match error with
            | ForkError.NothingToJoin
            | ForkError.Empty -> "There is nothing away to receive."
            | ForkError.Cancelled -> "The wait was cancelled."
            | ForkError.JoinInProgress -> "Another join is already in progress."
            | ForkError.Abandoned(id, _) ->
                let name = byname resolveAgentName id ""
                sprintf "%s did not return from this charge." name
            | ForkError.NotFound _ -> "No one by that name is away."
            | ForkError.TimedOut -> "No return reached you before your waiting ended."
            | ForkError.TerminalMaterializationFailed _ -> "A return could not be gathered."

        renderEntry [ line ] []

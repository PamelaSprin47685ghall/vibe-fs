namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Manager fork / Orchestrator commission. Each public tool has its own typed
/// request and schema; PTY is intentionally absent.
module ForkTool =

    type Request =
        { Name: string
          Charge: string
          Keywords: string }

    let private decode (args: HostToolArguments) =
        { Name = args.Text "name"
          Charge = args.Text "charge"
          Keywords = args.Text "keywords" }

    let private consequence (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private successInstruction (text: string) =
        ToolHostCodec.tomlObjectWithInstructions [ text ] []

    let private unknownCallingConsequence () =
        "Unknown or unavailable calling."

    let private managedForRecord (record: AgentRecord) =
        if String.IsNullOrWhiteSpace record.Agent then
            None
        else
            ManagedAgent.tryParse record.Agent

    /// GLORY-032: provider-facing denial for any target the Manager cannot
    /// reach (the Host-owned Reviewer among them). Generic — it must not prove
    /// the hidden target exists.
    let HiddenTargetDeniedText = "Unknown or unavailable managed agent."

    let private forbiddenManagerRole (managed: ManagedAgent) =
        match managed.Role with
        | Role.Distiller
        | Role.Blogger
        | Role.Orchestrator
        | Role.Manager
        | Role.Reviewer -> true
        | _ -> false

    let private hasKeywords (request: Request) =
        not (String.IsNullOrWhiteSpace request.Keywords)

    let private warmStartAllowed role =
        RepositoryWarmStartPrompt.isDirectConsumer role

    let private warmStartError =
        "repository warm-start keywords are only available when fork targets Coder, Inspector, or DevOps"

    let private prepareForkPrompt (scope: ToolRuntimeScope) (runtime: HostForkRuntime) (role: Role) (request: Request) =
        task {
            let basePrompt =
                ForkChildPayload.relay request.Charge (runtime.ParentWorkRecordOf runtime.ParentId) [] None

            match! RepositoryWarmStart.appendToBase role scope.WorkspaceDirectory request.Keywords basePrompt with
            | Ok prompt -> return prompt
            | Error _ -> return basePrompt
        }

    let private bynameOf (request: Request) (fallback: string) =
        if String.IsNullOrWhiteSpace request.Name then
            fallback
        else
            request.Name.Trim()

    let private executeManager (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if request.Name = Wanxiangshu.Process.Pty.AgentName then
                return consequence "Terminal work belongs through the terminal tools, not fork."
            elif String.IsNullOrWhiteSpace request.Name then
                return consequence "A name is required."
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence "A charge is required."
            else
                let assignment = request.Charge

                match scope.RuntimeFor context with
                | Error _ -> return consequence "A charge cannot be placed from this execution context."
                | Ok runtime ->
                    let abandoned =
                        match runtime.IsRetiredHandle request.Name with
                        | Some true -> true
                        | _ -> false

                    let pty =
                        match abandoned with
                        | true -> None
                        | false -> runtime.TryPty request.Name

                    match abandoned, pty, runtime.TryFindAgent request.Name with
                    | true, _, None -> return consequence "That person is no longer available for another charge."
                    | _, Some _, _ ->
                        return consequence "Terminal work belongs through the terminal tools, not fork."
                    | _, None, Some record ->
                        match managedForRecord record with
                        | Some managed when forbiddenManagerRole managed -> return consequence HiddenTargetDeniedText
                        | _ when hasKeywords request && not (warmStartAllowed record.Role) ->
                            return consequence warmStartError
                        | _ ->
                            let activeRun =
                                lock runtime.Gate (fun () -> runtime.PendingRuns.ContainsKey request.Name)

                            let! reuseResult =
                                if hasKeywords request && not activeRun then
                                    task {
                                        let! rendered = prepareForkPrompt scope runtime record.Role request
                                        return! runtime.Reuse(request.Name, assignment, renderedPrompt = rendered)
                                    }
                                else
                                    runtime.Reuse(request.Name, assignment)

                            match reuseResult with
                            | Error _ -> return consequence "That person cannot take another charge yet."
                            | Ok _ ->
                                let label =
                                    match managedForRecord record with
                                    | Some managed -> managed.Name
                                    | None -> record.Agent

                                return successInstruction (sprintf "%s carries this charge now." label)
                    | _, None, None ->
                        match ManagedAgent.tryParse request.Name with
                        | Some managed when forbiddenManagerRole managed -> return consequence HiddenTargetDeniedText
                        | Some managed when
                            managed.Visibility = AgentVisibility.Public
                            && List.contains managed.Name ManagedAgent.managerForkableNames
                            ->
                            let role = AgentRoleIdentity.ofManaged managed.Role

                            if hasKeywords request && not (warmStartAllowed role) then
                                return consequence warmStartError
                            else
                                let! forkResult =
                                    if hasKeywords request then
                                        task {
                                            let! rendered = prepareForkPrompt scope runtime role request

                                            return!
                                                runtime.Fork(
                                                    ToolHostCodec.newHandleId (),
                                                    role,
                                                    managed.Name,
                                                    assignment,
                                                    None,
                                                    renderedPrompt = rendered
                                                )
                                        }
                                    else
                                        runtime.Fork(ToolHostCodec.newHandleId (), role, managed.Name, assignment, None)

                                match forkResult with
                                | Ok _ ->
                                    return
                                        successInstruction (
                                            sprintf "%s carries this charge now." (bynameOf request managed.Name)
                                        )
                                | Error _ -> return consequence "The charge could not be placed."
                        | Some _ -> return consequence HiddenTargetDeniedText
                        | None when ToolHostCodec.looksLikeHandleId request.Name ->
                            return consequence "No continuing person is known by that name."
                        | None -> return consequence (unknownCallingConsequence ())
        }

    let private executeOrchestrator (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if String.IsNullOrWhiteSpace context.SessionId then
                return consequence "A road cannot be commissioned before the caller's authority is established."
            elif String.IsNullOrWhiteSpace request.Name then
                return consequence "A name is required."
            elif String.IsNullOrWhiteSpace request.Charge then
                return consequence "A charge is required."
            else
                match ManagedAgent.tryParse request.Name with
                | Some managed when managed.Role = Role.Manager && managed.Visibility = AgentVisibility.Public ->
                    let managerId = ManagerJobId.create (ToolHostCodec.newHandleId ())
                    let host = scope.OrchestratorHostFor context.SessionId

                    match! host.ForkManagerJob(managerId, managed.Name, request.Charge) with
                    | Ok _ ->
                        return
                            successInstruction (sprintf "%s has taken your charge." (bynameOf request managed.Name))
                    | Error _ -> return consequence "That road could not be opened."
                | Some _ -> return consequence "Only a Manager can take an independent road."
                | None ->
                    // GLORY-068: reuse an existing ManagerJob — same worktree/session.
                    if ToolHostCodec.looksLikeHandleId request.Name then
                        let host = scope.OrchestratorHostFor context.SessionId
                        let jobId = ManagerJobId.create request.Name

                        match! host.ContinueManagerJob(jobId, request.Charge) with
                        | Ok _ ->
                            return
                                successInstruction (
                                    sprintf "%s has taken your charge." (bynameOf request request.Name)
                                )
                        | Error _ -> return consequence "No continuing road is known by that name."
                    else
                        return consequence (unknownCallingConsequence ())
        }

    let managerSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork"
          Description =
            "Commission another witness within a mission. Pass name + charge; reuse by the same name when the existing sub-session has compatible context."
          Arguments =
            [ "name", ToolHostCodec.managedOrHandleSchema ManagedAgent.managerForkableNames factory
              "charge", ToolHostCodec.optionalStringSchema factory
              "keywords", ToolHostCodec.optionalStringSchema factory ]
          Execute = fun args context -> executeManager scope (decode args) context }

    let orchestratorSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "commission"
          Description =
            "Entrust an independent road to a Manager. Pass name + charge; reuse an existing road by passing its handle as name."
          Arguments =
            [ "name", ToolHostCodec.managedOrHandleSchema ManagedAgent.orchestratorForkableNames factory
              "charge", ToolHostCodec.stringSchema factory ]
          Execute = fun args context -> executeOrchestrator scope (decode args) context }
⚠ 1 unresolved conflict detected
- ours = HEAD
- theirs = master
NOTICE: Inspect a block by reading `conflict://<N>` (add `/ours` / `/theirs` / `/base` to render a single side). Resolve with `write({ path: "conflict://<N>", content })`, or bulk-resolve every registered conflict with `write({ path: "conflict://*", content })`. Writes replace ONLY the marker block (markers + all sides) — never repeat the lines before/after it; they stay in place.
`content` shorthand: a line that is exactly `@ours` / `@theirs` / `@base` / `@both` expands to that recorded section. `@both` is ours-then-theirs with no separator — only for additive conflicts where each side adds something different; NEVER for competing edits of the same lines (pick a side or write the combined text). Lines that are not a token pass through verbatim, so `"// keep both\n@ours\n@theirs"` literally writes the comment, then ours, then theirs.
Per-id bulk: `write({ path: "conflict://*", content: "1: @ours\n2: @theirs\n…" })` resolves each listed id with that side in ONE call — the cheapest way through many pick-one conflicts; unlisted ids stay registered.
Resolve each block faithfully: keep one side (`@ours`/`@theirs`), or combine them when both intents apply — never invent content beyond the recorded sides, and never stack both sides of competing edits. Resolve several conflicts in a single turn by issuing multiple `write` calls at once; ids stay valid as earlier blocks are resolved.

──── #3  L143-181 ────
<<< ours
                            if hasKeywords request && not (warmStartAllowed role) then
                                return consequence warmStartError
                            else
                                let! forkResult =
                                    if hasKeywords request then
                                        task {
… (22 more lines)
>>> theirs
                            match! runtime.Fork(ToolHostCodec.newHandleId (), role, managed.Name, assignment, None) with
                            | Ok _ ->
                                return
                                    successInstruction (
                                        sprintf "%s carries this charge now." (bynameOf request managed.Name)
                                    )
… (2 more lines)
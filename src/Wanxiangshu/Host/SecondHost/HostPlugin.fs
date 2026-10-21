namespace Wanxiangshu.Host.SecondHost

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources
open Wanxiangshu.Git

/// The product composition root for the second host (HNI P-11).
///
/// The host supplies ports; this module supplies the product behaviour:
/// a durable work opening, a durable Ownership scope, the paired-bilingual
/// system documents, one product tool, and the neutral product gates that
/// have no second-host binding yet.
module HostPlugin =

    [<Emit("JSON.parse($0)")>]
    let private parseJson (_text: string) : obj = jsNative

    [<Emit("$0 && typeof $0.text === 'string' ? $0.text : ''")>]
    let private jsonTextField (_value: obj) : string = jsNative

    /// The product default role for a root session: the Engineer law. The
    /// product's retired roles carry no provider prompt, so they fall back
    /// to the same default rather than to a hand-written document.
    let private defaultRole = Role.Engineer

    /// The role of the turn context, resolved to the product's role law.
    /// An unknown or retired role label uses the product default role.
    let private roleOf (context: TurnContext) : Role =
        match context.Role with
        | Some (AgentRole label) ->
            match Roles.tryParseRole label with
            | Some role ->
                try
                    // `roleSemanticPath` rejects retired roles; those fall back.
                    PromptResources.systemForRole ProviderLanguage.English role |> ignore
                    role
                with _ ->
                    defaultRole
            | None -> defaultRole
        | None -> defaultRole

    /// The paired-bilingual tool description. The English and the Chinese
    /// line both come from the product's own resource tree
    /// (`resources/provider/<semantic>/<en.md|zh-CN.md>`), never hand-written.
    let private bilingualDescription () : string =
        let tryRead (language: ProviderLanguage) (semanticPath: string) =
            try
                Some (ProviderResources.readText language semanticPath).Trim()
            with _ ->
                None

        let candidates = [ "tool/deliver"; "tool/join/description" ]

        let chosen =
            candidates
            |> List.tryPick (fun semanticPath ->
                match tryRead ProviderLanguage.English semanticPath, tryRead ProviderLanguage.SimplifiedChinese semanticPath with
                | Some english, Some chinese when not (String.IsNullOrWhiteSpace english) && not (String.IsNullOrWhiteSpace chinese) ->
                    Some(english, chinese)
                | _ -> None)

        match chosen with
        | Some(english, chinese) -> english + "\n\n" + chinese
        | None ->
            // No product tool description resource resolves for this host.
            // The product's own bilingual role law is used instead of
            // hand-written text; both languages come from the product's
            // resource tree.
            PromptResources.systemForRole ProviderLanguage.English defaultRole
            + "\n\n"
            + PromptResources.systemForRole ProviderLanguage.SimplifiedChinese defaultRole

    /// Deterministic work id for a session or a workspace.
    /// The second host has no host-supplied session identity at activation
    /// time, so the work identity is derived from the workspace digest; the
    /// child session opened by the deliver tool derives its own work id from
    /// its session id. Both are stable across activations of the same
    /// workspace and of the same session.
    let private workIdOf (seed: string) : string =
        "host2-" + (HostDigest.sha256Hex seed).Substring(0, 32)

    /// The assistant reply of a child session: the last assistant message
    /// that carries a terminal state and non-empty text.
    let private assistantReply (messages: TranscriptMessage list) : string option =
        messages
        |> List.filter (fun message -> message.Role = TranscriptRole.Assistant && message.Terminal.IsSome)
        |> List.tryPick (fun message ->
            let text =
                message.Parts
                |> List.collect (fun part ->
                    match part with
                    | TranscriptPart.Text value -> [ value ]
                    | _ -> [])
                |> String.concat ""
                |> fun value -> value.Trim()

            if String.IsNullOrWhiteSpace text then None else Some text)

    /// The plugin value. `Activate` performs the durable product wiring and
    /// returns the product hooks for the second host.
    let plugin: IHostPlugin =
        { new IHostPlugin with
            member _.Activate(ports: HostPorts) : HostResult<ProductHooks> =
                task {
                    let workspace =
                        match ports.Paths.ProjectRoot with
                        | WorkspacePath path -> path

                    // The product's own resolution of the work tree root and
                    // of the machine state directory. `RuntimePath.stateRoot`
                    // is the resolver used by the product's lifecycle
                    // opening (`Mission/Manager/Workflow.fs`); in this
                    // codebase the durable journal lives in the state root,
                    // so the state root is the journal's gitCommonDir.
                    let! root = GitSubject.revParseShowToplevel workspace

                    if String.IsNullOrWhiteSpace root then
                        return Error (HostError.Transport "the second host activation failed: Git did not identify a worktree root")

                    let! stateRoot = RuntimePath.stateRoot root

                    if String.IsNullOrWhiteSpace stateRoot then
                        return Error (HostError.Transport "the second host activation failed: the machine state directory is missing")

                    let workId = workIdOf (ports.Paths.ProjectId + "|" + root)
                    let ownerId = workId

                    // The durable work opening of this host's root session.
                    // A session-root opening is not an execution tenure, so
                    // the plan sequence is the right one.
                    match
                        DurableWorkHost.ensureOpened
                            stateRoot
                            workId
                            (workId + ":open:0")
                            root
                            (DurableWorkHost.planSequence ())
                            ownerId
                            0.0
                    with
                    | Error reason ->
                        return Error (HostError.Transport ("the second host activation failed to open its durable work: " + reason))
                    | Ok _ ->
                        ()

                    // A real Ownership fact: the activation scope is opened in
                    // the durable ownership registry, so the scope that owns
                    // this host's work is a durable fact, not an in-memory one.
                    let registryId = "host-activation"
                    let scopeId = "host-activation:" + ports.Paths.ProjectId

                    let receipt =
                        DurableWorkHost.executeOwnershipDurable
                            stateRoot
                            registryId
                            (scopeId + ":open")
                            (DurableWorkHost.ownershipMessage
                                "OpenScope"
                                [ "scopeId", box scopeId
                                  "parentId", null ])

                    let committed =
                        receipt.Status = "Committed"
                        || receipt.Status = "Replayed"
                        // An idempotent re-opening of the same scope is an
                        // observation of the same durable scope, not a failure.
                        || (receipt.Status = "Observed" && receipt.Kind = "ScopeOpened")

                    if not committed then
                        return
                            Error (
                                HostError.Transport (
                                    "the second host activation failed to open its ownership scope: "
                                    + receipt.Status
                                    + (if String.IsNullOrWhiteSpace receipt.Reason then "" else " (" + receipt.Reason + ")")
                                )
                            )

                    // The one product tool of this host: it hands a message to
                    // a child session and returns the child's reply.
                    let deliverSpec: ToolSpec =
                        { Name = "wxs_deliver"
                          Description = bilingualDescription ()
                          Schema =
                            JsonSchema (
                                "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}},"
                                + "\"required\":[\"text\"]}"
                            )
                          Execute =
                            fun invocation ->
                                task {
                                    let text =
                                        match invocation.Args with
                                        | CanonicalJsonText raw ->
                                            try
                                                jsonTextField (parseJson raw)
                                            with _ ->
                                                ""
                                        | _ -> ""

                                    if String.IsNullOrWhiteSpace text then
                                        return
                                            { Text = "wxs_deliver requires a non-empty `text` field."
                                              Truncated = false
                                              IsError = true
                                              Details = None }
                                    else
                                        let! child = ports.Sessions.Create(Some invocation.Session, { Title = Some "wxs_deliver"; Role = None; Workspace = None })

                                        match child with
                                        | Error error ->
                                            return
                                                { Text = "wxs_deliver could not create the child session: " + string error
                                                  Truncated = false
                                                  IsError = true
                                                  Details = None }
                                        | Ok childSession ->
                                            // The child session's own durable
                                            // work, derived from its session
                                            // id so it is deterministic.
                                            let childWorkId = workIdOf (Identity.SessionId.value childSession)

                                            match
                                                DurableWorkHost.ensureOpened
                                                    stateRoot
                                                    childWorkId
                                                    (childWorkId + ":open:0")
                                                    root
                                                    (DurableWorkHost.planSequence ())
                                                    childWorkId
                                                    0.0
                                            with
                                            | Error reason ->
                                                return
                                                    { Text = "wxs_deliver could not open the child session's durable work: " + reason
                                                      Truncated = false
                                                      IsError = true
                                                      Details = None }
                                            | Ok _ ->
                                                let! sent =
                                                    ports.Prompts.Send(
                                                        childSession,
                                                        { Text = text
                                                          Route = None
                                                          Role = None
                                                          Workspace = None
                                                          Correlation = None
                                                          ToolPolicy = None },
                                                        None
                                                    )

                                                match sent with
                                                | Outcome.Fatal reason ->
                                                    return
                                                        { Text = "wxs_deliver could not deliver the message: " + reason
                                                          Truncated = false
                                                          IsError = true
                                                          Details = None }
                                                | Outcome.Retryable reason ->
                                                    return
                                                        { Text = "wxs_deliver could not deliver the message: " + reason
                                                          Truncated = false
                                                          IsError = true
                                                          Details = None }
                                                | Outcome.AcceptanceUnknown reason ->
                                                    return
                                                        { Text = "wxs_deliver could not confirm the delivery: " + reason
                                                          Truncated = false
                                                          IsError = true
                                                          Details = None }
                                                | _ ->
                                                    // Poll the child's transcript
                                                    // for its terminal reply.
                                                    let deadline = DateTimeOffset.UtcNow.AddSeconds 120.0
                                                    let mutable lastObservation = "no transcript observation yet"

                                                    let rec poll () : Task<string option> =
                                                        task {
                                                            let! messages = ports.Transcript.Messages childSession

                                                            match messages with
                                                            | Ok observed ->
                                                                let observedText =
                                                                    observed
                                                                    |> List.map (fun message ->
                                                                        let body =
                                                                            message.Parts
                                                                            |> List.collect (fun part ->
                                                                                match part with
                                                                                | TranscriptPart.Text value -> [ value ]
                                                                                | _ -> [])
                                                                            |> String.concat ""

                                                                        body)

                                                                lastObservation <-
                                                                    "observed "
                                                                    + string observed.Length
                                                                    + " message(s) in the child transcript; no terminal assistant reply yet"

                                                                match assistantReply observed with
                                                                | Some reply -> return Some reply
                                                                | None ->
                                                                    if DateTimeOffset.UtcNow >= deadline then
                                                                        return None
                                                                    else
                                                                        do! Task.Delay 250
                                                                        return! poll ()
                                                            | Error error ->
                                                                lastObservation <-
                                                                    "the child transcript could not be read: " + string error

                                                                if DateTimeOffset.UtcNow >= deadline then
                                                                    return None
                                                                else
                                                                    do! Task.Delay 250
                                                                    return! poll ()
                                                        }

                                                    let! reply = poll ()

                                                    match reply with
                                                    | Some text ->
                                                        return
                                                            { Text = text
                                                              Truncated = false
                                                              IsError = false
                                                              Details = None }
                                                    | None ->
                                                        return
                                                            { Text =
                                                                "wxs_deliver timed out after 120s waiting for the child session's terminal reply; "
                                                                + lastObservation
                                                              Truncated = false
                                                              IsError = true
                                                              Details = None }
                                } }

                    match ports.Tools.Register [ deliverSpec ] with
                    | Error error ->
                        return Error error
                    | Ok () ->
                        ()

                    let turn: ITurnTransform =
                        { new ITurnTransform with
                            member _.AdmitTurn(_request: TurnRequest) =
                                Task.FromResult(TurnAdmission.Admit None)

                            member _.SystemPrompt(context: TurnContext, system: string list) =
                                task {
                                    // The host's own system documents are kept
                                    // as they are; the product appends its own
                                    // paired-bilingual documents for the role.
                                    let role = roleOf context

                                    let english = PromptResources.systemForRole ProviderLanguage.English role
                                    let chinese = PromptResources.systemForRole ProviderLanguage.SimplifiedChinese role

                                    return system @ [ english; chinese ]
                                }

                            member _.ContextMessages(_context: TurnContext, _messages: ContextMessage list) =
                                Task.FromResult(ContextEdit.Keep)

                            member _.ProviderParams(_context: TurnContext, _parameters: ProviderParams) =
                                Task.FromResult(ParamsVerdict.Accept) }

                    let toolGate: IToolGate =
                        { new IToolGate with
                            member _.BeforeTool(_call: ToolCallView) =
                                // The product gate for tool admission is owned by
                                // the host-boundary admission surface; it has no
                                // second-host binding yet, so the neutral default
                                // applies: no restriction, no rewrite.
                                Task.FromResult(ToolCallVerdict.Allow)

                            member _.AfterTool(_call: ToolCallView, _result: ToolOutcome) =
                                // No product result rewrite exists for this host
                                // yet; the neutral default keeps the host's result.
                                Task.FromResult(ToolResultEdit.Keep)

                            member _.DefineTool(_definition: ToolDefinitionView) =
                                // No product description/schema rewrite exists
                                // for this host yet; the neutral default keeps
                                // the host's definition.
                                Task.FromResult(ToolDefinitionEdit.Keep) }

                    let compaction: ICompactionGate =
                        { new ICompactionGate with
                            member _.OnCompacting(_session: SessionId) =
                                // The product compaction policy (Host/CompactionPolicy)
                                // owns the contribution; it has no second-host
                                // binding yet, so the neutral default contributes
                                // nothing to the host's compaction.
                                Task.FromResult(CompactionContribution.Nothing)

                            member _.AutoContinue(_session: SessionId) =
                                // The same product owner decides auto-continue;
                                // without a second-host binding the neutral
                                // default lets the host continue.
                                Task.FromResult(ContinueVerdict.Allow) }

                    let onSignal (signal: HostSignal) : Task =
                        task {
                            // The product's durable command vocabulary for a
                            // host's tool and attempt facts (ToolStarted,
                            // AttemptFinished) is owned by the product's
                            // per-turn journal; it has no second-host binding
                            // yet, so no durable command kind is issued here.
                            // The fact is not silently dropped: the host's
                            // diagnostic sink records it.
                            ports.Diagnostics.Emit(
                                "second-host-signal-unmapped",
                                [ "signal", string signal
                                  "reason", "no product command kind exists for host tool/attempt facts in the second host" ]
                            )
                        }

                    let dispose () : Task =
                        // Nothing is held open across the activation: the
                        // durable work and the ownership scope live in the
                        // machine state directory, not in this process.
                        Task.FromResult()

                    return
                        Ok
                            { ToolGate = toolGate
                              Turn = turn
                              Compaction = compaction
                              Spawn = None
                              OnSignal = onSignal
                              Dispose = dispose }
                }
        }

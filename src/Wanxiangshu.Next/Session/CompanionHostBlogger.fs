namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host

module internal CompanionHostBlogger =

    type BloggerDeps =
        {
            Sessions: ISessionHostPort
            PrimaryId: SessionId
            Durable: ICompanionDurablePort option
            EnsureBlogger: unit -> Task<SessionId>
            Gate: obj
            Companion: Companion
            RequestKind: ProviderRequestKind ref
            SquashFrameCount: int option ref
            BloggerNeedsReset: bool ref
            Journal: AgentJournal option
            EffectiveAgent: string
            /// CTX-006 step 5: record the squash attempt's plan on the Y chain.
            /// Supplied by `CompanionHost` as a closure over `PluginRuntimeScope` so
            /// this module does not grow a second scope dependency.
            RecordSquashPlan: SessionId -> ProviderRunIdentity -> unit
        }

    /// CTX-007 for one squash attempt: the terminal resolved into an AttemptOutcome.
    /// `Completed` means the Host said Completed AND `TerminalValidity` accepted the
    /// text (CTX-004) — this is the only place that check runs on the squash chain.
    let private squashOutcomeOf (terminal: TerminalOutcome) : AttemptOutcome * string =
        match terminal with
        | Completed result ->
            match TerminalValidity.check result.TurnFormalText with
            | Ok() -> AttemptOutcome.Completed, result.TurnFormalText
            | Error rejection -> AttemptOutcome.CompletedInvalid, TerminalValidity.describe rejection
        | Failed error -> AttemptOutcome.Failed, error
        | Aborted reason -> AttemptOutcome.Aborted, reason

    let private failBlog (message: string) : BloggerCompletion =
        raise (InvalidOperationException message)

    /// FALLBACK-003 on the Y chain: a failed squash is a confirmed failed slot, so the
    /// cursor advances through the single writer. Recorded under the squash attempt's
    /// own ProviderRun, which is what makes the squash attempt — not the main it
    /// preempts — the one `FallbackCursorAdvanced` of this slot (FALLBACK-011).
    let private advanceSquashCursor (deps: BloggerDeps) (providerRun: ProviderRunIdentity) (reason: string) =
        match deps.Journal with
        | Some journal ->
            FallbackController.recordConfirmedFailure
                journal
                AgentPairCursor.DefaultAutoRecoveryBudget
                deps.PrimaryId
                providerRun
                reason
            |> ignore
        | None -> ()

    let private resetSquashFlags (deps: BloggerDeps) =
        deps.RequestKind.Value <- ProviderRequestKind.BloggerMain
        deps.SquashFrameCount.Value <- None

    /// COMPANION-002: the Blogger is prompted like any other agent-owned child.
    ///
    /// PROMPT-005 applies unchanged. The previous version sent directly through the
    /// session port when no journal was present, producing a prompt with no
    /// PromptKey in its metadata — unrecoverable by PROMPT-011 and unclassifiable
    /// by PromptIngress. A Blogger prompt is not exempt from being a durable act.
    let private sendBloggerPrompt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        : Task<Result<PromptKey, string>> =
        task {
            match deps.Journal with
            | None -> return Error "No journal: a Blogger prompt cannot be claimed"
            | Some journal ->
                let dispatcher = PromptDispatcher.forJournal journal

                return! dispatcher.SendAgentOwnerRoot deps.Sessions childId prompt deps.EffectiveAgent None None
        }


    /// Send one squash-shaped prompt with the BloggerSquash request kind set for the
    /// transform, await its terminal, and restore the flags.
    let private sendSquashAttempt
        (deps: BloggerDeps)
        (childId: SessionId)
        (prompt: string)
        (frameCount: int)
        : Task<TerminalOutcome> =
        task {
            let completion =
                TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

            let mutable terminalDelivered = false

            let onTerminal _ outcome =
                if not terminalDelivered then
                    terminalDelivered <- true
                    completion.SetResult outcome

            use subscription = deps.Sessions.SubscribeTerminal(childId, onTerminal)
            deps.RequestKind.Value <- ProviderRequestKind.BloggerSquash
            deps.SquashFrameCount.Value <- Some frameCount

            let! sent = sendBloggerPrompt deps childId prompt

            match sent with
            | Error error ->
                resetSquashFlags deps
                return Failed error
            | Ok _ ->
                let! outcome = completion.Task
                resetSquashFlags deps
                return outcome
        }

    /// CTX-006 / CTX-007 / CTX-012: one recovery-slot squash on the Y chain.
    ///
    /// The decision lives in `RecoverySlot.onSquashOutcome`; this function executes
    /// it. `CommitSquashThenMain` is the only terminal that writes
    /// `BlogSquashCommitted`, through the durable port's `AppendSquash` — the single
    /// constructor. `FailSlot` advances the cursor and returns Error, which the
    /// caller reads as "skip this slot's main request" (design §13.4).
    let squash (deps: BloggerDeps) (frameCount: int) : Task<Result<BlogProjectionState, string>> =
        task {
            let! childId = deps.EnsureBlogger()
            let! terminal = sendSquashAttempt deps childId CompanionPrompt.SquashInstruction frameCount
            let outcome, detail = squashOutcomeOf terminal

            match RecoverySlot.onSquashOutcome outcome with
            | SlotDecision.CommitSquashThenMain ->
                match deps.Durable, terminal with
                | Some port, Completed result ->
                    // CTX-006 step 5: bind this attempt's plan on the Y chain before
                    // the commit, so reconcile finds the squash attempt accounted for.
                    deps.RecordSquashPlan childId result.ProviderRun
                    return port.AppendSquash(deps.PrimaryId, childId, frameCount, detail, result.ProviderRun)
                | _ -> return Error "squash terminal lost between validity check and commit"
            | SlotDecision.MainWithoutSquash ->
                // FALLBACK-008: a repair would spend the one-repair budget on a
                // compression; the frames are intact, so the slot just continues.
                return Error detail
            | SlotDecision.FailSlot ->
                let providerRun =
                    match terminal with
                    | Completed result -> result.ProviderRun
                    | _ -> ProviderRunIdentity.create (sprintf "%s-squash-%d" (SessionId.value childId) frameCount)

                advanceSquashCursor deps providerRun detail
                return Error detail
            | decision -> return Error(sprintf "squash slot decision %A is not a squash outcome" decision)
        }

    let blog
        (deps: BloggerDeps)
        (projection: ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : Task<BloggerCompletion> =
        task {
            let! childId = deps.EnsureBlogger()

            let completion =
                TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

            let mutable terminalDelivered = false

            let onTerminal _ outcome =
                if not terminalDelivered then
                    terminalDelivered <- true
                    completion.SetResult outcome

            use subscription = deps.Sessions.SubscribeTerminal(childId, onTerminal)

            let reset = lock deps.Gate (fun () -> deps.BloggerNeedsReset.Value)

            deps.RequestKind.Value <- ProviderRequestKind.BloggerMain
            deps.SquashFrameCount.Value <- None

            // COMPANION-004: restart/reset goes through the SAME delta projector as
            // a normal request. The prior bare-English splice (`sprintf` + EffectiveFrames +
            // JSON semantic dump) is gone: the reset sends the full current
            // projection as a data-only TOML delta, exactly like a normal chunk but
            // without the ingest-cursor gap — the Companion re-anchors on the whole
            // history because its prior context was lost.
            let prompt =
                if reset then
                    let fullDelta =
                        BloggerToml.render (
                            XTrace.flatten projection.Messages
                            |> List.map (fun entry ->
                                { Role = entry.Role
                                  Part =
                                    match entry.Part with
                                    | SemanticText text -> BloggerDeltaPart.TextPart text
                                    | SemanticReasoning text -> BloggerDeltaPart.ReasoningPart text
                                    | SemanticToolCall(name, args) -> BloggerDeltaPart.ToolCallPart(name, args)
                                    | SemanticToolResult result -> BloggerDeltaPart.ToolResultPart result
                                    | SemanticMedia(mediaType, _digest) ->
                                        if mediaType |> Option.exists (fun value -> value.StartsWith "image/") then
                                            BloggerDeltaPart.ImageOmitted mediaType
                                        else
                                            BloggerDeltaPart.MediaOmitted mediaType
                                  Truncated = false })
                        )

                    fullDelta
                else
                    chunk.Toml

            let! sent = sendBloggerPrompt deps childId prompt

            match sent with
            | Error error -> return failBlog error
            | Ok _ ->
                let! outcome = completion.Task

                match outcome with
                | Completed result ->
                    let text = result.TurnFormalText

                    match TerminalValidity.check text with
                    | Error rejection -> return failBlog (TerminalValidity.describe rejection)
                    | Ok() ->
                        lock deps.Gate (fun () -> deps.BloggerNeedsReset.Value <- false)

                        return
                            { BloggerSessionId = childId
                              ProviderRun = result.ProviderRun
                              Text = text
                              NextCursor = chunk.NextCursor
                              NextCoverableTurnCutoffExclusive = chunk.NextCoverableTurnCutoffExclusive
                              NextCoveredPrefixDigest =
                                let coveredMessages =
                                    projection.Messages
                                    |> List.truncate (
                                        min chunk.NextCoverableTurnCutoffExclusive (List.length projection.Messages)
                                    )

                                HostDigest.sha256Hex (
                                    ProviderProjection.renderSemantic
                                        { projection with
                                            Messages = coveredMessages }
                                ) }
                | Aborted reason -> return failBlog reason
                | Failed error -> return failBlog error
        }

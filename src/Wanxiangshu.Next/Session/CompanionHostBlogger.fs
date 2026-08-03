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
            Journal: AgentJournal option
            EffectiveAgent: string
            /// CTX-006 step 5: record the squash attempt's plan on the Y chain.
            /// Supplied by `CompanionHost` as a closure over `PluginRuntimeScope` so
            /// this module does not grow a second scope dependency.
            RecordSquashPlan: SessionId -> ProviderRunIdentity -> unit
            /// ENFORCER-045: stage the typed request context before the prompt
            /// goes out. The continuation transform consumes it for the coverage
            /// advance (fail closed when absent).
            StageBloggerContext: SessionId -> BloggerRequestContext -> unit
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

    /// CTX-012: squash prompt = Working Record frames + squash instruction,
    /// built by the same projection builder as normal (no raw transcript).
    let private squashPrompt (deps: BloggerDeps) (frameCount: int) : string =
        let blog = deps.Companion.Memory.Blog

        let frameBodies =
            blog.Frames
            |> List.truncate frameCount
            |> List.choose (fun frame ->
                match deps.Durable with
                | Some port ->
                    match port.Load deps.PrimaryId with
                    | Ok(Some memory) ->
                        memory.Blog.Frames
                        |> List.tryFind (fun f -> f.Digest = frame.Digest)
                        |> Option.bind (fun f ->
                            match deps.Journal with
                            | Some journal ->
                                match journal.Writer.BlobWriter.Read f.TextRef with
                                | Ok text -> Some text
                                | Error _ -> None
                            | None -> None)
                    | _ -> None
                | None -> None)

        let wrapped =
            frameBodies |> List.map (fun body -> CompanionPrompt.workingRecordMessage body)

        String.concat "\n\n" (wrapped @ [ CompanionPrompt.SquashInstruction ])

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
            let prompt = squashPrompt deps frameCount
            let! terminal = sendSquashAttempt deps childId prompt frameCount
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

    /// ENFORCER-047 / C2: physical send from a frozen typed Main context.
    /// CurrentRequest was already written by BloggerCoordinator before this call.
    /// No raw TOML extraction, no full-X reset replay, no post-send stage.
    let startMainFromContext
        (deps: BloggerDeps)
        (ctx: BloggerRequestContext)
        : Task<Result<PromptKey, string>> =
        task {
            match ctx with
            | BloggerRequestContext.Squash _ ->
                return Error "startMainFromContext requires BloggerRequestContext.Main"
            | BloggerRequestContext.Main main ->
                let! childId = deps.EnsureBlogger()
                deps.RequestKind.Value <- ProviderRequestKind.BloggerMain
                deps.SquashFrameCount.Value <- None
                return! sendBloggerPrompt deps childId main.Toml
        }

    /// Legacy chunk entry — only used by tests that still call Companion.Submit.
    /// Production main material goes through BloggerCoordinator + startMainFromContext.
    let blog
        (deps: BloggerDeps)
        (projection: ProviderSemanticProjection)
        (chunk: BloggerDeltaChunk)
        : Task<Result<PromptKey, string>> =
        task {
            let blog = deps.Companion.Memory.Blog
            let xTrace = deps.Companion.Memory.XTrace
            let ctx = EnforcerHost.mainContextFromChunk blog xTrace projection chunk
            return! startMainFromContext deps ctx
        }

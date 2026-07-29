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

module internal CompanionHostBlogger =

    type BloggerDeps =
        { Sessions: ISessionHostPort
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          BloggerNeedsReset: bool ref
          Companion: Companion
          Journal: AgentJournal option
          EffectiveAgent: string }

    let failBlog (message: string) : string =
        raise (InvalidOperationException message)

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

    let blog (deps: BloggerDeps) (currentProjection: ProjectionSnapshot) (delta: ProjectionSnapshot) : Task<BlogText> =
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

            // Read pending-reset WITHOUT clearing: the flag is cleared only
            // after a terminal (Completed with non-empty output), so any
            // failure (send Error, Aborted, Failed, empty) leaves it set and
            // the next blog call re-sends the FULL reset frame.
            let reset = lock deps.Gate (fun () -> deps.BloggerNeedsReset.Value)

            let prompt =
                if reset then
                    sprintf
                        "You are the blogger of a coding agent session. This session resumed after a restart and your prior companion context was lost. Re-anchor on the FULL current companion context B and the FULL CURRENT projection, then continue. FULL B:\n%s\nFULL PROJECTION:\n%s"
                        (deps.Companion.Memory.LatestB |> Option.defaultValue "")
                        currentProjection
                else
                    sprintf
                        "You are the blogger of a coding agent session. Write one dense paragraph for these delta messages.\n%s"
                        delta

            let! sent = sendBloggerPrompt deps childId prompt

            match sent with
            | Error error -> return failBlog error
            | Ok _ ->
                let! outcome = completion.Task

                match outcome with
                | Completed result ->
                    let text = result.FormalText

                    if String.IsNullOrWhiteSpace text then
                        return failBlog "Blogger returned no formal assistant text"
                    else
                        lock deps.Gate (fun () -> deps.BloggerNeedsReset.Value <- false)
                        return text
                | Aborted reason -> return failBlog reason
                | Failed error -> return failBlog error
        }

    let selfRebaseBlog (deps: BloggerDeps) (currentB: BlogText) : Task<BlogText> =
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

            let prompt =
                sprintf
                    "You are the blogger of a coding agent session. Condense the following FULL companion context into a single dense paragraph that preserves every durable fact, decision, and instruction. Output ONLY the condensed paragraph.\n%s"
                    currentB

            let! sent = sendBloggerPrompt deps childId prompt

            match sent with
            | Error error -> return failBlog error
            | Ok _ ->
                let! outcome = completion.Task

                match outcome with
                | Completed result ->
                    let text = result.FormalText

                    if String.IsNullOrWhiteSpace text then
                        return failBlog "Blogger returned no formal assistant text"
                    else
                        return text
                | Aborted reason -> return failBlog reason
                | Failed error -> return failBlog error
        }

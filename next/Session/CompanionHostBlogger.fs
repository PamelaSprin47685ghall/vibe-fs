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
          Model: Result<OpencodeModel, string>
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          BloggerNeedsReset: bool ref
          Companion: Companion
          OutputWatermark: SessionId -> int
          AssistantOutput: SessionId -> int -> string }

    let failBlog (message: string) : string =
        raise (InvalidOperationException message)

    let blog (deps: BloggerDeps) (currentProjection: ProjectionSnapshot) (delta: ProjectionSnapshot) : Task<BlogText> =
        task {
            match deps.Model with
            | Error error -> return failBlog error
            | Ok model ->
                let! childId = deps.EnsureBlogger()

                let completion =
                    TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                let watermark = deps.OutputWatermark childId

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
                            (deps.Companion.Memory.CurrentB |> Option.defaultValue "")
                            currentProjection
                    else
                        sprintf
                            "You are the blogger of a coding agent session. Write one dense paragraph for these delta messages.\n%s"
                            delta

                let! sent =
                    deps.Sessions.SendPrompt(
                        childId,
                        prompt,
                        { Model = Some model
                          Agent = Some "blogger"
                          Directory = None }
                    )

                match sent with
                | Error error -> return failBlog error
                | Ok _ ->
                    let! outcome = completion.Task

                    match outcome with
                    | Completed _ ->
                        let text = deps.AssistantOutput childId watermark

                        if String.IsNullOrWhiteSpace text then
                            return failBlog "Blogger returned no assistant text"
                        else
                            // Success-after-clear: the blogger confirmed the
                            // anchor, so drop the pending-reset flag and let
                            // Companion.Submit re-base on currentProjection.
                            lock deps.Gate (fun () -> deps.BloggerNeedsReset.Value <- false)
                            return text
                    | Aborted reason -> return failBlog reason
                    | Failed error -> return failBlog error
        }

    let selfRebaseBlog (deps: BloggerDeps) (currentB: BlogText) : Task<BlogText> =
        task {
            match deps.Model with
            | Error error -> return failBlog error
            | Ok model ->
                let! childId = deps.EnsureBlogger()

                let completion =
                    TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                let watermark = deps.OutputWatermark childId

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

                let! sent =
                    deps.Sessions.SendPrompt(
                        childId,
                        prompt,
                        { Model = Some model
                          Agent = Some "blogger"
                          Directory = None }
                    )

                match sent with
                | Error error -> return failBlog error
                | Ok _ ->
                    let! outcome = completion.Task

                    match outcome with
                    | Completed _ ->
                        let text = deps.AssistantOutput childId watermark

                        if String.IsNullOrWhiteSpace text then
                            return failBlog "Blogger returned no assistant text"
                        else
                            return text
                    | Aborted reason -> return failBlog reason
                    | Failed error -> return failBlog error
        }

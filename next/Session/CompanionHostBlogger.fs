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
          AssistantOutput: SessionId -> int -> string }

    let failBlog (message: string) : string =
        raise (InvalidOperationException message)

    let blog (deps: BloggerDeps) (delta: ProjectionSnapshot) : Task<BlogText> =
        task {
            match deps.Model with
            | Error error -> return failBlog error
            | Ok model ->
                let! childId = deps.EnsureBlogger()

                let completion =
                    TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                let watermark = deps.Sessions.GetSessionOutput childId |> List.length

                use subscription =
                    deps.Sessions.SubscribeTerminal(childId, (fun _ outcome -> completion.SetResult outcome))

                let reset =
                    lock deps.Gate (fun () ->
                        if deps.BloggerNeedsReset.Value then
                            deps.BloggerNeedsReset.Value <- false
                            true
                        else
                            false)

                let prompt =
                    if reset then
                        sprintf
                            "You are the blogger of a coding agent session. This session resumed after a restart and your prior companion context was lost. Re-anchor on the FULL current companion context B and the FULL current projection, then continue. FULL B:\n%s\nFULL PROJECTION:\n%s"
                            (deps.Companion.Memory.CurrentB |> Option.defaultValue "")
                            (deps.Companion.Memory.LastSuccessfulProjection |> Option.defaultValue "")
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

                let watermark = deps.Sessions.GetSessionOutput childId |> List.length

                use subscription =
                    deps.Sessions.SubscribeTerminal(childId, (fun _ outcome -> completion.SetResult outcome))

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

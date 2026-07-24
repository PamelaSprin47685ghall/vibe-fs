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

type AgentJournalCompanionPort(journal: AgentJournal) =
    let append (sessionId: SessionId) (fact: AgentFact) =
        match AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(sprintf "%A" failure.Failure)

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : CompanionMemory option =
            let projection = AgentJournal.snapshot journal

            projection.AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> Option.bind (fun session ->
                session.Companion
                |> Option.map (fun companion ->
                    { LastSuccessfulProjection = companion.LastSuccessfulProjection
                      CurrentB = companion.CurrentB
                      BloggerBusy = false
                      ReplacementActive = companion.ReplacementActive }))

        member _.AppendSuccessful(sessionId, projection, content) =
            match
                append
                    sessionId
                    (AgentFact.CompanionBaselineSet
                        {| SessionId = sessionId
                           Projection = projection |})
            with
            | Error error -> Error error
            | Ok() ->
                append
                    sessionId
                    (AgentFact.CompanionCheckpointReplaced
                        {| SessionId = sessionId
                           Content = content |})

        member _.EnableReplacement(sessionId) =
            append
                sessionId
                (AgentFact.CompanionReplacementActiveSet
                    {| SessionId = sessionId
                       Active = true |})

type CompanionHost(primaryId: SessionId, sessions: ISessionHostPort, ?durable: ICompanionDurablePort) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let mutable bloggerTask: Task<SessionId> option = None

    let ensureBlogger () =
        lock gate (fun () ->
            match bloggerTask with
            | Some task -> task
            | None ->
                let task =
                    task {
                        let! created =
                            sessions.CreateChildSession(
                                primaryId,
                                { Title = Some "blogger"
                                  Agent = Some "blogger" }
                            )

                        match created with
                        | Ok id -> return id
                        | Error error -> return raise (InvalidOperationException error)
                    }

                bloggerTask <- Some task
                task)

    let assistantOutput childId watermark =
        let output = sessions.GetSessionOutput childId

        output
        |> List.skip (min watermark output.Length)
        |> List.filter (fun line -> not (line.StartsWith("Prompt: ")) && not (line.StartsWith("ChildPrompt: ")))
        |> String.concat "\n"

    let failBlog (message: string) : string =
        raise (InvalidOperationException message)

    let blog (delta: ProjectionSnapshot) : Task<BlogText> =
        task {
            let! childId = ensureBlogger ()

            let completion =
                TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

            let watermark = sessions.GetSessionOutput childId |> List.length

            use subscription =
                sessions.SubscribeTerminal(childId, (fun _ outcome -> completion.SetResult outcome))

            let prompt =
                sprintf
                    "You are the blogger of a coding agent session. Write one dense paragraph for these delta messages.\n%s"
                    delta

            let! sent = sessions.SendPrompt(childId, prompt, { Model = None; Agent = Some "blogger" })

            match sent with
            | Error error -> return failBlog error
            | Ok _ ->
                let! outcome = completion.Task

                match outcome with
                | Completed _ ->
                    let text = assistantOutput childId watermark

                    if String.IsNullOrWhiteSpace text then
                        return failBlog "Blogger returned no assistant text"
                    else
                        return text
                | Aborted reason -> return failBlog reason
                | Failed error -> return failBlog error
        }

    let jsonOfMessages (messages: obj list) =
        Fable.Core.JS.JSON.stringify (List.toArray messages)

    let prefixLength (previous: string) (current: string) (maximum: int) =
        try
            let oldMessages = Fable.Core.JS.JSON.parse previous
            let newMessages = Fable.Core.JS.JSON.parse current
            let stringify value = Fable.Core.JS.JSON.stringify value

            let messageId value =
                if isNull value || isNull value?info || isNull value?info?id then
                    None
                else
                    Some(unbox<string> value?info?id)

            let sameMessage oldValue newValue =
                match messageId oldValue, messageId newValue with
                | Some oldId, Some newId -> oldId = newId
                | _ -> stringify oldValue = stringify newValue

            let mutable index = 0
            let mutable stopped = false

            while index < maximum && not stopped do
                let oldValue: obj = emitJsExpr (oldMessages, index) "$0[$1]"
                let newValue: obj = emitJsExpr (newMessages, index) "$0[$1]"

                if not (sameMessage oldValue newValue) then
                    stopped <- true
                else
                    index <- index + 1

            index
        with _ ->
            0

    member _.SubmitProjection(projection: ProjectionSnapshot) : CompanionOutcome = companion.Submit(projection, blog)

    member _.EnablePrefixReplacement() : bool = companion.TryEnableReplacement()

    member _.Memory = companion.Memory

    member _.WaitInFlightAsync() = companion.WaitInFlightAsync()

    member this.TransformRaw(messages: obj list) : obj list =
        let current = jsonOfMessages messages
        let before = companion.Memory

        let watermark =
            match before.LastSuccessfulProjection with
            | Some previous -> prefixLength previous current (List.length messages)
            | None -> 0

        companion.Submit(current, blog) |> ignore

        match before.ReplacementActive, before.CurrentB, before.LastSuccessfulProjection with
        | true, Some b, Some _ when watermark > 0 ->
            let synthetic = createObj [ "role", box "system"; "text", box b ]
            synthetic :: (messages |> List.skip watermark)
        | _ -> messages

    member _.ReplacePrefix(messages: HostMessage list, watermarkIndex: int) =
        Companion.compressPrefix messages companion.Memory.CurrentB watermarkIndex

    member _.BloggerSession =
        lock gate (fun () -> bloggerTask |> Option.map (fun task -> task.Result))

    member _.PrimarySessionId = primaryId

module VibeFs.Opencode.ExecutorActor

open Fable.Core

type private ExecutorEnvelope =
    { work: unit -> Async<string>
      reply: AsyncReplyChannel<Result<string, exn>> }

type ExecutorActor() =
    let agents = System.Collections.Generic.Dictionary<string, MailboxProcessor<ExecutorEnvelope>>()

    let createSessionAgent () =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () = async {
                let! message = inbox.Receive()
                let! outcome = Async.Catch(message.work())
                match outcome with
                | Choice1Of2 result -> message.reply.Reply(Ok result)
                | Choice2Of2 error -> message.reply.Reply(Error error)
                return! loop ()
            }
            loop ())

    let getSessionAgent (sessionID: string) =
        match agents.TryGetValue sessionID with
        | true, agent -> agent
        | false, _ ->
            let agent = createSessionAgent ()
            agents.[sessionID] <- agent
            agent

    member _.Post(sessionID: string, work: unit -> JS.Promise<string>) : JS.Promise<string> =
        async {
            let agent = getSessionAgent sessionID
            let! outcome = agent.PostAndAsyncReply(fun reply ->
                { work = fun () -> work () |> Async.AwaitPromise
                  reply = reply })
            match outcome with
            | Ok result -> return result
            | Error error -> return raise error
        }
        |> Async.StartAsPromise

let private actor = ExecutorActor()

let post (sessionID: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
    actor.Post(sessionID, work)

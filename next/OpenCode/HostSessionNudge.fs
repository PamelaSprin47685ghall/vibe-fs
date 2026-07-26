namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity

module HostSessionNudge =

    /// Fire-and-forget host prompt used by ReviewGuard and empty-turn continuation.
    /// Listener-before-send is preserved. Starts a Task/Promise under Fable; callers
    /// that need the send to land (unit tests) must await a microtask after Observe.
    let send
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (options: SessionPromptOptions)
        onAccepted
        =
        let listener = sessionPort.SubscribeTerminal(sessionId, (fun _ _ -> ()))

        task {
            try
                let! result = sessionPort.SendPrompt(sessionId, prompt, options)

                match result with
                | Ok messageId -> onAccepted messageId
                | Error _ -> ()
            finally
                listener.Dispose()
        }
        |> ignore

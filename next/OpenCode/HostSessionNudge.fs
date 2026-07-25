namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity

module HostSessionNudge =

    let send
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (options: SessionPromptOptions)
        onAccepted
        =
        let listener = sessionPort.SubscribeTerminal(sessionId, (fun _ _ -> ()))
        let pending = sessionPort.SendPrompt(sessionId, prompt, options)

        Async.StartImmediate(
            async {
                try
                    let! result = pending |> Async.AwaitTask

                    match result with
                    | Ok messageId -> onAccepted messageId
                    | Error _ -> ()
                finally
                    listener.Dispose()
            }
        )

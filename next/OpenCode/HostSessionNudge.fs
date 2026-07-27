namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostSessionNudge =

    /// Fire-and-forget host prompt used by ReviewGuard and empty-turn continuation.
    /// Listener-before-send is preserved. Starts a Task/Promise under Fable; callers
    /// that need the send to land (unit tests) must await a microtask after Observe.
    ///
    /// Defense-in-depth: when a journal is supplied, a session already Dead (4
    /// consecutive fallback failures, SSOT §6) is skipped silently — no prompt is
    /// sent to a dead session. Callers (HostEventRouter, HostReviewGuard) also
    /// gate on sessionDead before calling, so this is a second line of defense.
    let send
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (options: SessionPromptOptions)
        onAccepted
        (journal: AgentJournal option)
        =
        match journal with
        | Some j when DurableFallback.isDead sessionId (AgentJournal.snapshot j) -> ()
        | _ ->
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

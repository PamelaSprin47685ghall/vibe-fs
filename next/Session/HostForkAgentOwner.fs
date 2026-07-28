namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module HostForkAgentOwner =

    let sendFirstPrompt
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<MessageId, string>> =
        task {
            match journal with
            | Some j ->
                let svc = PromptDispatcher.forJournal j

                let! outcome = svc.SendAgentOwnerRoot sessions childId prompt agent directory None

                match outcome with
                | Ok messageId -> return Ok messageId
                | Error err -> return Error err
            | None ->
                return!
                    sessions.SendPrompt(
                        childId,
                        prompt,
                        { Model = None
                          Agent = Some agent
                          Directory = directory
                          Metadata = None }
                    )
        }

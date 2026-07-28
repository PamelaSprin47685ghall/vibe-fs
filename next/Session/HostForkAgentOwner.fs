namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module HostForkAgentOwner =

    let sendFirstPrompt
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (modelResolver: ModelResolver.ModelConfig option)
        (childId: SessionId)
        (role: AgentRole)
        (directory: string option)
        (prompt: string)
        : Task<Result<MessageId, string>> =
        task {
            let model = HostPendingRun.resolveAuthorityDefault modelResolver journal childId
            let agent = role.ToString().ToLowerInvariant()

            match journal with
            | Some j ->
                let svc = PromptDispatcher.forJournal j

                let! outcome =
                    svc.SendAgentOwnerRoot sessions childId prompt agent model None directory None

                match outcome with
                | Ok (messageId, _) -> return Ok messageId
                | Error err -> return Error err
            | None ->
                return!
                    sessions.SendPrompt(
                        childId,
                        prompt,
                        { Model = model
                          Agent = Some agent
                          Directory = directory
                          Metadata = None }
                    )
        }

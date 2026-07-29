namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session.AgentRoleHelpers

/// Restart recovery for linked children: rebuild unjoined completions from
/// transcript when possible; otherwise mark Interrupted instead of faking Busy.
module HostForkRestart =

    let private textOfParts (parts: MessagePart array) =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text -> Some text
                | _ -> None)
            |> String.concat ""

    let private lastByRole (messages: SessionMessage list) (role: string) =
        messages
        |> List.rev
        |> List.tryFind (fun message -> message.Role.Equals(role, StringComparison.OrdinalIgnoreCase))

    let private isTerminalCompleted (assistant: SessionMessage) =
        match assistant.Finish with
        | Some finish when finish.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
            not (String.IsNullOrWhiteSpace(textOfParts assistant.Parts))
        | _ -> false

    let recoverChild
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (agentId: string)
        (childSessionId: SessionId)
        (role: AgentRole)
        (agent: string)
        : Task<unit> =
        task {
            runtime.Restore(agentId, role, agent = agent)
            runtime.BindChildSession(agentId, SessionId.value childSessionId)

            match snapshot with
            | None -> runtime.MarkInterrupted(agentId, "host restart: no session snapshot")
            | Some port ->
                match! port.GetMessages childSessionId with
                | Error reason -> runtime.MarkInterrupted(agentId, sprintf "host restart: snapshot failed: %s" reason)
                | Ok messages ->
                    match lastByRole messages "assistant" with
                    | None -> ()
                    | Some assistant when isTerminalCompleted assistant ->
                        let text = textOfParts assistant.Parts

                        let root =
                            lastByRole messages "user"
                            |> Option.map (fun user -> MessageId.value user.Id)
                            |> Option.defaultValue ""

                        let payload =
                            AgentCompletion.completed
                                agentId
                                (SessionId.value childSessionId)
                                ("run-restored-" + agentId)
                                role
                                root
                                (MessageId.value assistant.Id)
                                text
                                None
                                ""

                        runtime.PublishCompletion
                            { RunId = "run-restored-" + agentId
                              AgentId = agentId
                              AgentName = agent
                              Role = role
                              Outcome = payload
                              CompletedAt = DateTimeOffset.UtcNow }
                    | Some assistant ->
                        runtime.MarkInterrupted(
                            agentId,
                            sprintf "host restart: child not terminal (finish=%s)" (defaultArg assistant.Finish "none")
                        )
        }

    let recoverAll
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (children: IDictionary<string, SessionId * AgentRole * string>)
        : Task<unit> =
        task {
            for KeyValue(agentId, (childSessionId, role, agent)) in children do
                do! recoverChild runtime snapshot agentId childSessionId role agent
        }

    let restoreLinkedChildren
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (childCreatedDir: string -> SessionId -> string option -> unit)
        (directoryOf: string -> string option)
        : Task =
        let projection = AgentJournal.snapshot journal

        match Map.tryFind parentId projection.AgentProjections.Sessions with
        | Some session when session.Linkage.IsSome ->
            let linkage = session.Linkage.Value
            let recovered = Dictionary<string, SessionId * AgentRole * string>()

            // Companion and other system associations share the session graph,
            // but they never belong to this ForkRuntime's join mailbox.
            for KeyValue(childId, agentId) in linkage.ForkedChildren do
                let managedName =
                    linkage.LinkedRoles |> Map.tryFind childId |> Option.defaultValue ""

                let role = AgentRoleHelpers.roleOfString managedName

                match role with
                | Some role ->
                    let childSessionId = SessionId.create (ChildId.value childId)
                    let agent = AgentRoleHelpers.defaultFastManagedName role
                    children.[agentId] <- childSessionId
                    recovered.[agentId] <- (childSessionId, role, agent)
                    childCreatedDir agentId childSessionId (directoryOf agentId)
                | None -> ()

            recoverAll runtime snapshot recovered :> Task
        | _ -> task { return () } :> Task

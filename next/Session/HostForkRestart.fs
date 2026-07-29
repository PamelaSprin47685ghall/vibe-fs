namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

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
            runtime.Restore(agentId, role, agent)
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

    /// EXEC-009 restart recovery: rebuild this parent's join mailbox from the
    /// durable handle records.
    ///
    /// SHOCK-UNMIGRATED[EXEC-009]: `HandleLinked` cannot supply what this needs.
    /// It carries `{ ParentSessionId; Handle; TargetAgent; CanonicalRole }`, and
    /// recovery additionally needs the child's SessionId — the old projection had
    /// it because `ForkedChildren` was keyed by ChildId, whereas `Handles` is keyed
    /// by the agent handle id and records no session.
    ///
    /// Inventing one is not available: a child session id is issued by the Host,
    /// so deriving it from the handle id would fabricate an identity every later
    /// operation silently no-ops against. Either `HandleLinked` gains the field or
    /// EXEC-009 states that recovery re-resolves children some other way. Recorded
    /// as a blocker for package F; SSOT exception protocol applies.
    let restoreLinkedChildren
        (_runtime: ForkRuntime)
        (_snapshot: ISessionSnapshotPort option)
        (_journal: AgentJournal)
        (_parentId: SessionId)
        (_children: Dictionary<string, SessionId>)
        (_childCreatedDir: string -> SessionId -> string option -> unit)
        (_directoryOf: string -> string option)
        : Task =
        failwith
            "SHOCK-UNMIGRATED[EXEC-009]: HandleLinked records no child SessionId, so restart recovery cannot rebind children"

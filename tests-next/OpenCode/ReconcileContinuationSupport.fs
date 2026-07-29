namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module ReconcileContinuationSupport =

    let textPart text =
        createObj [ "id", box "text-1"; "type", box "text"; "text", box text ]

    let reasoningPart text =
        createObj [ "id", box "reasoning-1"; "type", box "reasoning"; "text", box text ]

    let bookkeepingPart kind =
        createObj [ "id", box ("bookkeeping-" + kind); "type", box kind ]

    let msg id role agent finish parts : SessionMessage =
        { Id = MessageId.create id
          Role = role
          Agent = agent
          Finish = finish
          ErrorName = None
          Model = None
          Parts = parts
          Raw = createObj [] }

    let reasoningOnlyMessages agent : SessionMessage list =
        [ msg "u1" "user" (Some agent) None [||]
          msg
              "a1"
              "assistant"
              (Some agent)
              (Some "stop")
              [| bookkeepingPart "step-start"
                 reasoningPart "The work is complete, but no formal report was emitted."
                 bookkeepingPart "step-finish" |] ]

    let brokenXmlMessages agent : SessionMessage list =
        [ msg "u1" "user" (Some agent) None [||]
          msg
              "a1"
              "assistant"
              (Some agent)
              (Some "stop")
              [| textPart "result ready <tool_call name=\"executor\"" |] ]

    let outcomeOf (messages: SessionMessage list) =
        let assistant =
            messages
            |> List.find (fun (message: SessionMessage) -> message.Role = "assistant")

        (CompletedTurnClassifier.buildTurn
            (SessionId.create "classify")
            (MessageId.create "u1")
            (MessageId.create "u1")
            assistant
            (Some AgentRole.DevOps)
            "/tmp/ws")
            .Outcome

    let bind (reconciler: SessionReconciler) sessionId role =
        reconciler.BindActiveRun
            { SessionId = sessionId
              RunId = None
              RootUserMessageId = Some(MessageId.create "u1")
              PhysicalUserMessageId = Some(MessageId.create "u1")
              ContinuationMessageIds = Set.empty
              AgentRole = Some role
              Directory = "/tmp/ws" }

    let recordingPort (prompts: ResizeArray<string * string>) =
        let active = HashSet<string>()

        { new ISessionHostPort with
            member _.SubscribeTerminal(sessionId, _) =
                let key = SessionId.value sessionId
                active.Add key |> ignore

                { new IDisposable with
                    member _.Dispose() = active.Remove key |> ignore }

            member _.SendPrompt(sessionId, text, _) =
                let key = SessionId.value sessionId

                if active.Contains key then
                    prompts.Add(key, text)
                    Task.FromResult(Ok(MessageId.create ("accepted-" + key)))
                else
                    Task.FromResult(Error "AG-LISTENER-BEFORE-SEND")

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok(SessionId.create "child"))
            member _.GetSessionOutput(_) = [] }

    let fallbackFailures (journal: AgentJournal) sessionId =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind sessionId
        |> Option.bind (fun session -> session.Fallback)
        |> Option.map (fun fallback -> List.length fallback.RecentFailureIds)
        |> Option.defaultValue 0

    type FixedSnapshot(messages: SessionMessage list) =
        interface ISessionSnapshotPort with
            member _.GetMessages(_) = Task.FromResult(Ok messages)

    type SequencedSnapshot(snapshots: SessionMessage list list) =
        let mutable remaining = snapshots

        interface ISessionSnapshotPort with
            member _.GetMessages(_) =
                let next =
                    match remaining with
                    | head :: tail ->
                        remaining <- tail
                        head
                    | [] -> []

                Task.FromResult(Ok next)

    type GatedSnapshot(unknown: SessionMessage list, terminal: SessionMessage list) =
        let first = TaskCompletionSource<Result<SessionMessage list, string>>()
        let mutable calls = 0

        interface ISessionSnapshotPort with
            member _.GetMessages(_) =
                calls <- calls + 1

                match calls with
                | 1 -> first.Task
                | 2
                | 3 -> Task.FromResult(Ok unknown)
                | _ -> Task.FromResult(Ok terminal)

        member _.Calls = calls
        member _.ReleaseFirst() = first.SetResult(Ok unknown)

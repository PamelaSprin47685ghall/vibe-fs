namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic

type CasebookTurn = { Q: string; A: string option }

type CasebookDraft = { Turns: CasebookTurn list }

module CasebookDraftStore =
    let private drafts = Dictionary<string, CasebookDraft>()
    let private gate = obj ()

    let setQ (sessionId: string) (q: string) =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, draft ->
                match List.rev draft.Turns with
                | last :: earlier when last.A.IsNone ->
                    drafts.[sessionId] <- { Turns = List.rev ({ last with Q = q } :: earlier) }
                | _ -> drafts.[sessionId] <- { Turns = draft.Turns @ [ { Q = q; A = None } ] }
            | false, _ -> drafts.[sessionId] <- { Turns = [ { Q = q; A = None } ] })

    let setA (sessionId: string) (a: string) =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, draft ->
                match List.rev draft.Turns with
                | last :: earlier -> drafts.[sessionId] <- { Turns = List.rev ({ last with A = Some a } :: earlier) }
                | [] -> drafts.[sessionId] <- { Turns = [ { Q = ""; A = Some a } ] }
            | false, _ -> drafts.[sessionId] <- { Turns = [ { Q = ""; A = Some a } ] })

    let tryTake (sessionId: string) : CasebookDraft option =
        lock gate (fun () ->
            match drafts.TryGetValue sessionId with
            | true, d ->
                drafts.Remove sessionId |> ignore
                Some d
            | false, _ -> None)

    let clear (sessionId: string) =
        lock gate (fun () -> drafts.Remove sessionId |> ignore)

    let transcript (turns: CasebookTurn list) : string =
        turns
        |> List.mapi (fun index turn ->
            let n = string (index + 1)
            let answer = defaultArg turn.A ""
            "Q" + n + " = " + turn.Q + "\nA" + n + " = " + answer)
        |> String.concat "\n"

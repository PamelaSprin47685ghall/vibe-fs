namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module ReviewerGuardState =

    let guard (sessionParents: Dictionary<string, string>) (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> None
        | Some j ->
            let sessions = (AgentJournal.snapshot j).AgentProjections.Sessions

            match sessionParents.TryGetValue reviewerKey with
            | true, managerKey ->
                sessions
                |> Map.tryFind (SessionId.create managerKey)
                |> Option.bind (fun session -> session.ReviewGuard)
            | false, _ ->
                let child = ChildId.create reviewerKey

                sessions
                |> Map.tryPick (fun _ session ->
                    match session.Linkage, session.ReviewGuard with
                    | Some linkage, Some reviewGuard when Map.containsKey child linkage.LinkedChildren ->
                        Some reviewGuard
                    | _ -> None)

    let submitted sessionParents journal reviewerKey =
        guard sessionParents journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            reviewGuard.IsConfirmed
            || reviewGuard.ConsecutivePerfects > 0
            || not (List.isEmpty reviewGuard.RecentToolCallIds))

    let pendingConfirmation sessionParents journal reviewerKey =
        guard sessionParents journal reviewerKey
        |> Option.exists (fun reviewGuard -> reviewGuard.ConsecutivePerfects = 1 && not reviewGuard.IsConfirmed)

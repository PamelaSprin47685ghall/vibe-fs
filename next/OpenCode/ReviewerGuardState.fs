namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module ReviewerGuardState =

    let reviewOwner
        (sessionParents: Dictionary<string, string>)
        (journal: AgentJournal option)
        (reviewerKey: string)
        =
        match journal with
        | None -> None
        | Some j ->
            let sessions = (AgentJournal.snapshot j).AgentProjections.Sessions

            match sessionParents.TryGetValue reviewerKey with
            | true, managerKey -> Some(SessionId.create managerKey)
            | false, _ ->
                let child = ChildId.create reviewerKey

                sessions
                |> Map.tryPick (fun parentId session ->
                    match session.Linkage with
                    | Some linkage when Map.containsKey child linkage.LinkedChildren -> Some parentId
                    | _ -> None)

    let guard (sessionParents: Dictionary<string, string>) (journal: AgentJournal option) (reviewerKey: string) =
        match journal, reviewOwner sessionParents journal reviewerKey with
        | Some j, Some owner ->
            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.tryFind owner
            |> Option.bind (fun session -> session.ReviewGuard)
        | _ -> None

    let submitted sessionParents journal reviewerKey =
        guard sessionParents journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            reviewGuard.IsConfirmed
            || ReviewWitness.isPerfectPending reviewGuard.Witness
            || ReviewWitness.isRevision reviewGuard.Witness
            || not (List.isEmpty reviewGuard.RecentToolCallIds))

    let pendingConfirmation sessionParents journal reviewerKey =
        guard sessionParents journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            ReviewWitness.isPerfectPending reviewGuard.Witness
            && not reviewGuard.IsConfirmed)

    let confirmedOwner sessionParents journal reviewerKey physicalUserMessageId =
        match reviewOwner sessionParents journal reviewerKey, guard sessionParents journal reviewerKey with
        | Some owner, Some reviewGuard
            when reviewGuard.IsConfirmed
                 && reviewGuard.ConfirmedReviewerSessionId = Some(SessionId.create reviewerKey)
                 && (reviewGuard.AuthorityRootUserMessageId = Some physicalUserMessageId
                     || reviewGuard.ConfirmationPhysicalMessageId = Some physicalUserMessageId) ->
            Some owner
        | _ -> None

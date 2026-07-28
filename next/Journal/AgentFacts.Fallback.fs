namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsFallback =

    let private failureIdentity
        (logicalRunId: string)
        (authorityRootUserMessageId: string)
        (providerAttempt: string)
        =
        sprintf "%s|%s|%s" logicalRunId authorityRootUserMessageId providerAttempt

    let private rememberFailureId (ids: string list) (identity: string) =
        let next = identity :: (ids |> List.filter ((<>) identity))
        next |> List.truncate 4

    let private emptyEpoch logicalRunId authorityRoot =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Side = SideA
          FailuresOnCurrentSide = 0
          TotalFailures = 0
          IsDead = false
          RecentFailureIds = [] }

    let foldFallbackFailureRecorded
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               Reason: string
               AssistantMessageId: string
               ProviderAttempt: string |})
        : AgentProjectionSet =
        let logicalRunId =
            if String.IsNullOrWhiteSpace p.LogicalRunId then
                "unknown-run"
            else
                p.LogicalRunId

        let authorityRoot =
            if String.IsNullOrWhiteSpace p.AuthorityRootUserMessageId then
                "unknown-root"
            else
                p.AuthorityRootUserMessageId

        let identity = failureIdentity logicalRunId authorityRoot p.ProviderAttempt

        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let baseline =
                        match s.Fallback with
                        | Some existing when existing.LogicalRunId = logicalRunId -> existing
                        | _ -> emptyEpoch logicalRunId authorityRoot

                    let fb =
                        if List.contains identity baseline.RecentFailureIds then
                            baseline
                        elif baseline.IsDead then
                            baseline
                        else
                            let newTotal = baseline.TotalFailures + 1
                            let ids = rememberFailureId baseline.RecentFailureIds identity

                            match baseline.Side with
                            | SideA ->
                                if baseline.FailuresOnCurrentSide < 1 then
                                    { baseline with
                                        Side = SideA
                                        FailuresOnCurrentSide = baseline.FailuresOnCurrentSide + 1
                                        TotalFailures = newTotal
                                        IsDead = false
                                        RecentFailureIds = ids }
                                else
                                    { baseline with
                                        Side = SideB
                                        FailuresOnCurrentSide = 0
                                        TotalFailures = newTotal
                                        IsDead = false
                                        RecentFailureIds = ids }
                            | SideB ->
                                if baseline.FailuresOnCurrentSide < 1 then
                                    { baseline with
                                        Side = SideB
                                        FailuresOnCurrentSide = baseline.FailuresOnCurrentSide + 1
                                        TotalFailures = newTotal
                                        IsDead = false
                                        RecentFailureIds = ids }
                                else
                                    { baseline with
                                        Side = SideB
                                        FailuresOnCurrentSide = 2
                                        TotalFailures = newTotal
                                        IsDead = true
                                        RecentFailureIds = ids }

                    { s with Fallback = Some fb })
                proj.Sessions

        { proj with Sessions = sessions }

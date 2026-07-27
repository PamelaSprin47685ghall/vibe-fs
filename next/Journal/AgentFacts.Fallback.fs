namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module internal AgentFactsFallback =

    let private failureIdentity (assistantMessageId: string) (providerAttempt: string) =
        sprintf "%s|%s" assistantMessageId providerAttempt

    let private rememberFailureId (ids: string list) (identity: string) =
        let next = identity :: (ids |> List.filter ((<>) identity))
        next |> List.truncate 4

    let foldFallbackFailureRecorded
        (proj: AgentProjectionSet)
        (p:
            {| SessionId: SessionId
               Reason: string
               AssistantMessageId: string
               ProviderAttempt: string |})
        : AgentProjectionSet =
        let identity = failureIdentity p.AssistantMessageId p.ProviderAttempt

        let sessions =
            updateSession
                p.SessionId
                (fun s ->
                    let fb =
                        match s.Fallback with
                        | Some existing when List.contains identity existing.RecentFailureIds -> existing
                        | Some existing when existing.IsDead -> existing
                        | Some existing ->
                            let newTotal = existing.TotalFailures + 1
                            let ids = rememberFailureId existing.RecentFailureIds identity

                            match existing.Side with
                            | SideA ->
                                if existing.FailuresOnCurrentSide < 1 then
                                    { Side = SideA
                                      FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                                else
                                    { Side = SideB
                                      FailuresOnCurrentSide = 0
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                            | SideB ->
                                if existing.FailuresOnCurrentSide < 1 then
                                    { Side = SideB
                                      FailuresOnCurrentSide = existing.FailuresOnCurrentSide + 1
                                      TotalFailures = newTotal
                                      IsDead = false
                                      RecentFailureIds = ids }
                                else
                                    { Side = SideB
                                      FailuresOnCurrentSide = 2
                                      TotalFailures = newTotal
                                      IsDead = true
                                      RecentFailureIds = ids }
                        | None ->
                            { Side = SideA
                              FailuresOnCurrentSide = 1
                              TotalFailures = 1
                              IsDead = false
                              RecentFailureIds = [ identity ] }

                    { s with Fallback = Some fb })
                proj.Sessions

        { proj with Sessions = sessions }

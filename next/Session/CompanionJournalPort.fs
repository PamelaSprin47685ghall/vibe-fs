namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

type AgentJournalCompanionPort(journal: AgentJournal) =
    let append (sessionId: SessionId) (fact: AgentFact) =
        match AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(sprintf "%A" failure.Failure)

    interface ICompanionDurablePort with
        member _.Load(sessionId: SessionId) : CompanionMemory option =
            let projection = AgentJournal.snapshot journal
            projection.AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> Option.bind (fun session ->
                session.Companion
                |> Option.map (fun companion ->
                    { LastSuccessfulProjection = companion.LastSuccessfulProjection
                      LatestB = companion.LatestB
                      ActivePrefixEpoch =
                          companion.ActivePrefixEpoch
                          |> Option.map (fun epoch ->
                              { EpochId = epoch.EpochId
                                FrozenB = epoch.FrozenB
                                CutoffMessageIndex = epoch.CutoffMessageIndex
                                CoveredPrefixDigest = epoch.CoveredPrefixDigest })
                      PrefixReplacementEnabled = companion.ReplacementActive }))

        member _.AppendSuccessful(sessionId, projection, content) =
            append sessionId
                (AgentFact.CompanionAdvanced
                    {| SessionId = sessionId; Projection = projection; Content = content |})

        member _.AppendEpochSwitched(sessionId, epoch) =
            append sessionId
                (AgentFact.CompanionEpochSwitched
                    {| SessionId = sessionId; EpochId = epoch.EpochId; FrozenB = epoch.FrozenB
                       CutoffMessageIndex = epoch.CutoffMessageIndex; CoveredPrefixDigest = epoch.CoveredPrefixDigest |})

        member _.EnableReplacement(sessionId) =
            append sessionId
                (AgentFact.CompanionReplacementActiveSet
                    {| SessionId = sessionId; Active = true |})

        member _.AppendLink(sessionId, childId, targetAgent, role) =
            append sessionId
                (AgentFact.AgentLinked
                    {| ParentId = sessionId; ChildId = childId; TargetAgent = targetAgent; Role = role |})

        member _.AppendUnlink(sessionId, childId) =
            append sessionId
                (AgentFact.AgentUnlinked
                    {| ParentId = sessionId; ChildId = childId |})

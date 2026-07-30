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
        | Error failure -> Error(JournalAppendFailure.describe failure)

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
                      BloggerSessionId = companion.BloggerSessionId }))

        member _.AppendSuccessful(sessionId, projection, content) =
            append
                sessionId
                (AgentFact.CompanionAdvanced
                    {| SessionId = sessionId
                       Projection = projection
                       Content = content |})

        member _.LinkBlogger(sessionId, bloggerSessionId, bloggerAgent) =
            append
                sessionId
                (AgentFact.CompanionBloggerLinked
                    {| SessionId = sessionId
                       BloggerSessionId = bloggerSessionId
                       BloggerAgent = bloggerAgent |})

        member _.CloseBlogger(sessionId) =
            append sessionId (AgentFact.CompanionBloggerClosed {| SessionId = sessionId |})

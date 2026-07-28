namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Domain
open AgentFactsFoldHelpers

module internal AgentFactsFallback =

    let private rememberFailureId (ids: string list) (identity: string) =
        let next = identity :: (ids |> List.filter ((<>) identity))
        // Keep enough recent identities for restart-safe dedupe across long runs.
        next |> List.truncate 32

    let private emptyEpoch logicalRunId authorityRoot =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Offset = 0uy
          LastProviderAttempt = None
          RecentFailureIds = [] }

    let private tryParseAttempt (providerAttempt: string) : int64 option =
        match Int64.TryParse providerAttempt with
        | true, n -> Some n
        | _ -> None

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

        let identity =
            AgentPairCursor.failureIdentity (
                AgentPairCursor.attemptIdentity logicalRunId authorityRoot p.ProviderAttempt
            )

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
                        else
                            { baseline with
                                Offset = AgentPairCursor.advance baseline.Offset
                                LastProviderAttempt = tryParseAttempt p.ProviderAttempt
                                RecentFailureIds = rememberFailureId baseline.RecentFailureIds identity }

                    { s with Fallback = Some fb })
                proj.Sessions

        { proj with Sessions = sessions }

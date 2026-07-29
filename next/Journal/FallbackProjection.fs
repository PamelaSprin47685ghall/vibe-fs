namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Domain

type FallbackProjection =
    { LogicalRunId: string
      AuthorityRootUserMessageId: string
      Offset: byte
      LastProviderAttempt: int64 option
      RecentFailureIds: string list }

/// The only durable fold for the modulo-4 Agent pair cursor.
module FallbackProjection =

    let empty =
        { LogicalRunId = ""
          AuthorityRootUserMessageId = ""
          Offset = 0uy
          LastProviderAttempt = None
          RecentFailureIds = [] }

    let forAuthority logicalRunId authorityRoot =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Offset = 0uy
          LastProviderAttempt = None
          RecentFailureIds = [] }

    let private remember identity ids =
        identity :: (ids |> List.filter ((<>) identity)) |> List.truncate 32

    let private parseAttempt (providerAttempt: string) =
        match Int64.TryParse providerAttempt with
        | true, attempt -> Some attempt
        | false, _ -> None

    let recordRetry logicalRunId authorityRoot providerAttempt current =
        let runId = if String.IsNullOrWhiteSpace logicalRunId then "unknown-run" else logicalRunId
        let root = if String.IsNullOrWhiteSpace authorityRoot then "unknown-root" else authorityRoot

        let baseline =
            match current with
            | Some existing when existing.LogicalRunId = runId -> existing
            | _ -> forAuthority runId root

        let identity =
            AgentPairCursor.failureIdentity (AgentPairCursor.attemptIdentity runId root providerAttempt)

        if List.contains identity baseline.RecentFailureIds then
            baseline
        else
            { baseline with
                Offset = AgentPairCursor.advance baseline.Offset
                LastProviderAttempt = parseAttempt providerAttempt
                RecentFailureIds = remember identity baseline.RecentFailureIds }

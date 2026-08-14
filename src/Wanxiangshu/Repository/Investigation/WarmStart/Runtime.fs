namespace Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// AGENT-032: Infrastructure adapter from explicit keywords to neutral warm-start
/// material. Semble remains internal and fail-open; provider rendering stays Domain.
[<RequireQualifiedAccess>]
module RepositoryWarmStart =

    type Search = string -> string -> int -> Task<SembleMcp.Hit list>

    let private toHint keywordOrdinal localRank (hit: SembleMcp.Hit) : RepositoryWarmStartHint =
        { KeywordOrdinal = keywordOrdinal
          LocalRank = localRank
          FilePath = hit.FilePath
          StartLine = hit.StartLine
          EndLine = hit.EndLine
          Content = hit.Content
          Score = hit.Score
          TotalLines = hit.TotalLines }

    let private collectWithSearch
        (search: Search)
        (role: Role)
        (workspaceDirectory: string option)
        (keywordsRaw: string)
        : Task<Result<RepositoryWarmStartSearch list option, string>> =
        task {
            let keywords = RepositoryWarmStartPrompt.normalizeKeywords keywordsRaw

            if List.isEmpty keywords then
                // None = true zero-work fast path. Callers preserve their base prompt byte-for-byte.
                return Ok None
            elif not (RepositoryWarmStartPrompt.isDirectConsumer role) then
                return Error "repository warm-start keywords are only available to Coder, Inspector, or DevOps targets"
            else
                match workspaceDirectory with
                | None -> return Ok None
                | Some repo when String.IsNullOrWhiteSpace repo || not (Directory.Exists repo) -> return Ok None
                | Some repo ->
                    let indexed = keywords |> List.mapi (fun index query -> index + 1, query)

                    let! searches =
                        Parallel.mapBounded
                            RepositoryWarmStartPrompt.MaxKeywords
                            CancellationToken.None
                            (fun (ordinal, query) _ ->
                                task {
                                    let! hits =
                                        task {
                                            try
                                                return! search query repo RepositoryWarmStartPrompt.TopKPerKeyword
                                            with _ ->
                                                // One query is an optimization shard; its failure
                                                // cannot fail or serialize the invocation.
                                                return []
                                        }

                                    let neutral =
                                        hits
                                        |> List.truncate RepositoryWarmStartPrompt.TopKPerKeyword
                                        |> List.mapi (fun index hit -> toHint ordinal (index + 1) hit)

                                    return
                                        { RepositoryWarmStartSearch.Ordinal = ordinal
                                          Query = query
                                          Hints = neutral }
                                })
                            indexed

                    // Parallel.mapBounded preserves input ordering; sorting makes
                    // keyword-ordinal merge explicit and implementation-independent.
                    return Ok(Some(searches |> List.sortBy (fun item -> item.Ordinal)))
        }

    let prepareWithSearch
        (search: Search)
        (sessionId: SessionId)
        (role: Role)
        (workspaceDirectory: string option)
        (keywordsRaw: string)
        (charge: string)
        : Task<Result<string, string>> =
        task {
            match! collectWithSearch search role workspaceDirectory keywordsRaw with
            | Error error -> return Error error
            | Ok None -> return Ok charge
            | Ok(Some searches) ->
                let lang = ProviderProse.languageOf sessionId

                let instructions =
                    ProviderProse.instructionLines
                        lang
                        RepositoryWarmStartPrompt.ChargeEnvelope
                        (Map [ "charge", charge ])

                return Ok(RepositoryWarmStartPrompt.render instructions charge searches)
        }

    /// Fork path: preserve ForkChildPayload's assignment + commissioner record
    /// and append repository_search/repository_hint tables as low-trust data.
    let appendToBaseWithSearch
        (search: Search)
        (sessionId: SessionId)
        (role: Role)
        (workspaceDirectory: string option)
        (keywordsRaw: string)
        (basePrompt: string)
        : Task<Result<string, string>> =
        task {
            match! collectWithSearch search role workspaceDirectory keywordsRaw with
            | Error error -> return Error error
            | Ok None -> return Ok basePrompt
            | Ok(Some searches) ->
                let appendix =
                    ProviderProse.instructionLines
                        (ProviderProse.languageOf sessionId)
                        RepositoryWarmStartPrompt.Appendix
                        Map.empty

                return Ok(RepositoryWarmStartPrompt.appendToProviderPrompt appendix basePrompt searches)
        }

    let prepare
        (sessionId: SessionId)
        (role: Role)
        (workspaceDirectory: string option)
        (keywordsRaw: string)
        (charge: string)
        : Task<Result<string, string>> =
        prepareWithSearch SembleMcpClient.searchFromEnvironment sessionId role workspaceDirectory keywordsRaw charge

    let appendToBase
        (sessionId: SessionId)
        (role: Role)
        (workspaceDirectory: string option)
        (keywordsRaw: string)
        (basePrompt: string)
        : Task<Result<string, string>> =
        appendToBaseWithSearch
            SembleMcpClient.searchFromEnvironment
            sessionId
            role
            workspaceDirectory
            keywordsRaw
            basePrompt

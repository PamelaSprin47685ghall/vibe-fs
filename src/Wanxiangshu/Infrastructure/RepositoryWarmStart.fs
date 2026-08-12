namespace Wanxiangshu.Infrastructure

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
        prepareWithSearch
            SembleMcpClient.searchFromEnvironment
            sessionId
            role
            workspaceDirectory
            keywordsRaw
            charge

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

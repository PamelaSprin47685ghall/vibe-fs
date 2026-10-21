namespace Wanxiangshu.Repository.Investigation.WarmStart

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Repository.Investigation.Semble

[<RequireQualifiedAccess>]
module RepositoryWarmStart =
    type Search = string -> string -> int -> Task<SembleMcp.Hit list>

    val prepareWithSearch:
        search: Search ->
        sessionId: SessionId ->
        role: Role ->
        workspaceDirectory: string option ->
        keywordsRaw: string ->
        charge: string ->
            Task<Result<string, string>>

    val appendToBaseWithSearch:
        search: Search ->
        sessionId: SessionId ->
        role: Role ->
        workspaceDirectory: string option ->
        keywordsRaw: string ->
        basePrompt: string ->
            Task<Result<string, string>>

    val prepare:
        sessionId: SessionId ->
        role: Role ->
        workspaceDirectory: string option ->
        keywordsRaw: string ->
        charge: string ->
            Task<Result<string, string>>

    val appendToBaseDocument:
        sessionId: SessionId ->
        role: Role ->
        workspaceDirectory: string option ->
        keywordsRaw: string ->
        baseDocument: LlmFacing.Document ->
            Task<Result<LlmFacing.Document, string>>

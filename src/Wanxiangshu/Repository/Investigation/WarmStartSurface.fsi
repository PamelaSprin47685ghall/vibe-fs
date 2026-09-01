namespace Wanxiangshu.Repository.Investigation

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module RepositoryWarmStartSurface =
    val maxKeywords: int
    val topKPerKeyword: int
    val maxHintsTotal: int
    val maxWarmStartBytes: int
    val normalizeKeywords: raw: string -> string array
    val render: instructions: string array -> charge: string -> searches: obj array -> string

    val appendToProviderPrompt:
        appendixInstructions: string array -> basePrompt: string -> searches: obj array -> string

    val prepareWithSearch:
        capability: obj ->
        sessionId: string ->
        roleLabel: string ->
        workspaceDirectory: obj ->
        keywordsRaw: string ->
        charge: string ->
            Task<obj>

    val appendToBaseWithSearch:
        capability: obj ->
        sessionId: string ->
        roleLabel: string ->
        workspaceDirectory: obj ->
        keywordsRaw: string ->
        basePrompt: string ->
            Task<obj>

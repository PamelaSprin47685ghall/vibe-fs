namespace Wanxiangshu.Repository.Investigation

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart

/// JS-native owner boundary for repository warm-start admission and rendering.
/// Search is an injected opaque capability; repository hints and prompt output
/// cross as plain data only after the warm-start owner has applied its laws.
[<RequireQualifiedAccess>]
module RepositoryWarmStartSurface =

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0?.[$1]"

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private intOf (value: obj) (fallback: int) =
        if isNullish value then
            fallback
        else
            match value with
            | :? int as number -> number
            | :? float as number -> int number
            | _ ->
                match Int32.TryParse(string value) with
                | true, number -> number
                | _ -> fallback

    let private floatOf (value: obj) (fallback: float) =
        if isNullish value then
            fallback
        else
            match value with
            | :? float as number -> number
            | :? int as number -> float number
            | _ ->
                match Double.TryParse(string value) with
                | true, number -> number
                | _ -> fallback

    let private contentLineCount (content: string) =
        if String.IsNullOrEmpty content then 0 else content.Split([| '\n' |], StringSplitOptions.None).Length

    let private hitOfJs (value: obj) : SembleMcp.Hit option =
        if isNullish value then
            None
        else
            let filePath = stringOf (property value "filePath")
            if String.IsNullOrWhiteSpace filePath then
                None
            else
                let content = stringOf (property value "content")
                let startLine = intOf (property value "startLine") 1
                let endLine = intOf (property value "endLine") startLine
                let declaredTotal = intOf (property value "totalLines") 0
                Some
                    { FilePath = filePath
                      StartLine = startLine
                      EndLine = endLine
                      Content = content
                      Score = floatOf (property value "score") 0.0
                      TotalLines = if declaredTotal > 0 then declaredTotal else max endLine (contentLineCount content) }

    let private hitsOfJs (value: obj) : SembleMcp.Hit list =
        if isNullish value then
            []
        else
            unbox<obj array> value |> Array.toList |> List.choose hitOfJs

    let private hintOfJs (value: obj) : RepositoryWarmStartHint =
        { KeywordOrdinal = intOf (property value "keywordOrdinal") 0
          LocalRank = intOf (property value "localRank") 0
          FilePath = stringOf (property value "filePath")
          StartLine = intOf (property value "startLine") 1
          EndLine = intOf (property value "endLine") 1
          Content = stringOf (property value "content")
          Score = floatOf (property value "score") 0.0
          TotalLines = intOf (property value "totalLines") 0 }

    let private searchOfJs (value: obj) : RepositoryWarmStartSearch =
        let hintsValue = property value "hints"
        let hints =
            if isNullish hintsValue then
                []
            else
                unbox<obj array> hintsValue |> Array.toList |> List.map hintOfJs

        { Ordinal = intOf (property value "ordinal") 0
          Query = stringOf (property value "query")
          Hints = hints }

    let private searchesOfJs (value: obj array) = value |> Array.toList |> List.map searchOfJs

    [<Emit("$0($1,$2,$3)")>]
    let private apply3 (fn: obj) (first: obj) (second: obj) (third: obj) : obj = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private promiseOf (value: obj) : JS.Promise<obj> = jsNative

    let private searchCapability (capability: obj) : RepositoryWarmStart.Search =
        fun query repo topK ->
            task {
                if isNullish capability then
                    return []
                else
                    let! raw = unbox<Task<obj>>(promiseOf (apply3 capability (box query) (box repo) (box topK)))
                    return hitsOfJs raw
            }

    let private optionalWorkspace (value: obj) : string option =
        if isNullish value then None else Some(string value)

    let private resultObject (result: Result<string, string>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = value |}
        | Error error -> box {| ok = false; error = error |}

    let private roleOf (value: string) : Result<Role, string> =
        match Roles.tryParseRole value with
        | Some role -> Ok role
        | None -> Error(sprintf "unknown repository warm-start role: %s" value)

    let maxKeywords = RepositoryWarmStartPrompt.MaxKeywords
    let topKPerKeyword = RepositoryWarmStartPrompt.TopKPerKeyword
    let maxHintsTotal = RepositoryWarmStartPrompt.MaxHintsTotal
    let maxWarmStartBytes = RepositoryWarmStartPrompt.MaxWarmStartBytes

    let normalizeKeywords (raw: string) : string array =
        RepositoryWarmStartPrompt.normalizeKeywords raw |> List.toArray

    let render (instructions: string array) (charge: string) (searches: obj array) : string =
        RepositoryWarmStartPrompt.render (instructions |> Array.toList) charge (searchesOfJs searches)

    let appendToProviderPrompt (appendixInstructions: string array) (basePrompt: string) (searches: obj array) : string =
        RepositoryWarmStartPrompt.appendToProviderPrompt
            (appendixInstructions |> Array.toList)
            basePrompt
            (searchesOfJs searches)

    let prepareWithSearch
        (capability: obj)
        (sessionId: string)
        (roleLabel: string)
        (workspaceDirectory: obj)
        (keywordsRaw: string)
        (charge: string)
        : Task<obj> =
        task {
            match roleOf roleLabel with
            | Error error -> return box {| ok = false; error = error |}
            | Ok role ->
                let! result =
                    RepositoryWarmStart.prepareWithSearch
                        (searchCapability capability)
                        (SessionId.create sessionId)
                        role
                        (optionalWorkspace workspaceDirectory)
                        keywordsRaw
                        charge

                return resultObject result
        }

    let appendToBaseWithSearch
        (capability: obj)
        (sessionId: string)
        (roleLabel: string)
        (workspaceDirectory: obj)
        (keywordsRaw: string)
        (basePrompt: string)
        : Task<obj> =
        task {
            match roleOf roleLabel with
            | Error error -> return box {| ok = false; error = error |}
            | Ok role ->
                let! result =
                    RepositoryWarmStart.appendToBaseWithSearch
                        (searchCapability capability)
                        (SessionId.create sessionId)
                        role
                        (optionalWorkspace workspaceDirectory)
                        keywordsRaw
                        basePrompt

                return resultObject result
        }

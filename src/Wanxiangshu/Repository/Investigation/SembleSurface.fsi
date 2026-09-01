namespace Wanxiangshu.Repository.Investigation

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module RepositorySembleSurface =
    val serverName: string
    val defaultRef: string
    val repo: string
    val toolName: string
    val maxSnippetLines: int
    val uvxCommand: gitRef: string -> string array
    val fixtureCommand: fixturePath: string -> string array
    val launchFromVars: vars: obj -> obj
    val parseText: text: string -> obj array
    val parseToolResult: result: obj -> obj array
    val search: launch: obj -> query: string -> repoPath: string -> topK: int -> Task<obj array>

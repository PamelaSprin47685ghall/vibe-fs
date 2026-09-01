namespace Wanxiangshu.Repository.Investigation.Semble

open System.Threading.Tasks
open Wanxiangshu.Foundation

module SembleMcpClient =
    val launchFromVars: vars: obj -> McpLaunch
    val launchFromEnvironment: unit -> McpLaunch
    val search: launch: McpLaunch -> query: string -> repoPath: string -> topK: int -> Task<SembleMcp.Hit list>
    val searchFromEnvironment: query: string -> repoPath: string -> topK: int -> Task<SembleMcp.Hit list>

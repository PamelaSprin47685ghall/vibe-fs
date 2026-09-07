namespace Wanxiangshu.Repository.Investigation.Semble

open System.Threading.Tasks

module SembleMcpClient =
    val launchFromVars: vars: obj -> SembleMcp.Launch
    val launchFromEnvironment: unit -> SembleMcp.Launch
    val search: launch: SembleMcp.Launch -> query: string -> repoPath: string -> topK: int -> Task<SembleMcp.Hit list>
    val searchFromEnvironment: query: string -> repoPath: string -> topK: int -> Task<SembleMcp.Hit list>

namespace Wanxiangshu.Repository.Investigation.Semble

open System.Threading.Tasks

module SembleMcpStdio =
    val callTool:
        command: string -> args: string array -> toolName: string -> toolArgs: obj -> timeoutMs: int -> Task<obj option>

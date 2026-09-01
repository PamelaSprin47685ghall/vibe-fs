namespace Wanxiangshu.Execution.Agent

[<RequireQualifiedAccess>]
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

type AgentContext =
    { SessionId: string; AgentName: string }

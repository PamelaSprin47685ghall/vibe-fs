namespace Wanxiangshu.Host.Ports

open Wanxiangshu.Foundation.Identity

/// HNI P-01: raw session verbs. Family-root resolution, managed-child registry and ancestry
/// proof stay in the product (Execution/Session); the port only performs the host action.
type SessionSpec =
    { Title: string option
      Role: AgentRole option
      Workspace: WorkspacePath option }

type SessionInfo =
    { Id: SessionId
      Parent: SessionId option
      Role: AgentRole option
      Title: string option }

type ISessionPort =
    abstract Create: parent: SessionId option * spec: SessionSpec -> HostResult<SessionId>
    abstract Parent: session: SessionId -> HostResult<SessionId option>
    abstract Children: parent: SessionId -> HostResult<SessionInfo list>
    /// Stop the whole session (all attempts).
    abstract Abort: session: SessionId -> HostResult<unit>
    /// Stop only the running provider attempt.
    abstract Interrupt: session: SessionId -> HostResult<unit>
    abstract Close: session: SessionId -> HostResult<unit>
    abstract FindByTitle: title: string -> HostResult<SessionId list>

namespace Wanxiangshu.Host.Ports

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.TerminalSignal

/// HNI P-17 (transitional): the Fission lane's own name for the session
/// lifecycle seam it consumes. Today that seam is the Foundation legacy
/// `ISessionHostPort`; when P-01 lands, this name keeps its meaning and only
/// its definition changes. Fission speaks this name, never the legacy one.
type FissionSessionPort = ISessionHostPort

/// HNI P-17 (transitional): the Fission lane's own name for the terminal
/// observation seam it consumes. Today that seam is the Foundation legacy
/// `IEventObservationPort`; when P-07 lands, this name keeps its meaning and
/// only its definition changes. Fission speaks this name, never the legacy one.
type FissionEventPort = IEventObservationPort

/// HNI port (fission lane): how a delegated PTY run ended, as Fission sees it.
/// The host runtime's own join item stays in the product domain; the port
/// carries the neutral facts Fission broadcasts to its lanes.
type FissionPtyStatus =
    | Exited
    | Failed
    | Aborted

type FissionPtyCompletion =
    { PtyId: string
      Status: FissionPtyStatus
      Outcome: string
      Code: string option
      Message: string option }

/// HNI port (fission lane): the delegated runs a Fission owner still has
/// outstanding at admission. Fission broadcasts each pre-Fission completion to
/// every lane, so it needs exactly these three capabilities and nothing else
/// of the host's per-session fork runtime.
type FissionOwnerRuns =
    {
        /// Outstanding delegated agent runs as (agent id, child session).
        OutstandingAgentRuns: unit -> (string * SessionId) list
        /// Outstanding PTY runs by PTY id.
        OutstandingPtyRuns: unit -> string list
        /// Observe PTY completions until the returned resource is disposed.
        SubscribePtyCompletion: (FissionPtyCompletion -> unit) -> IDisposable
    }

/// HNI port (fission lane): everything a host supplies so Fission can replace
/// a participant's physical present with sibling lanes. Shaped by what Fission
/// needs, not by any host's tool payload; OpenCode (and later omp) implement it
/// once per tool call. Fission never sees a host tool context.
type FissionHostPort =
    {
        /// Session lifecycle: sibling creation, parent lookup, abort, interrupt, prompts.
        Sessions: FissionSessionPort
        /// Terminal observation, when the host publishes it.
        Events: FissionEventPort option
        /// The working directory a session runs in, if the host knows it.
        DirectoryFor: SessionId -> string option
        /// The workspace directory used when the caller's own is unknown.
        WorkspaceDirectory: string option
        /// The logical owner a physical caller session acts for.
        LogicalOwnerFor: SessionId -> SessionId
        /// The caller's outstanding delegated runs. Called once, after the
        /// authority checks; an Error means the host has no runtime for the caller.
        OwnerRuns: unit -> Result<FissionOwnerRuns, string>
        /// Record a created lane's physical parent and directory with the host.
        RegisterLane: SessionId -> SessionId option -> string option -> unit
        /// Tell the host a lane run started under this role and directory.
        LaneStarted: SessionId -> Role -> string option -> unit
    }

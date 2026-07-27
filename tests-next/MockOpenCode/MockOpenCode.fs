namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module MockOpenCode =

    /// Mutable state container for MockOpenCodePort.
    /// Record avoids Fable class init issues with let-bindings in interfaces.
    type State =
        { mutable Sent: (SessionId * string * OpenCodePromptOptions) list
          mutable Aborted: SessionId list
          mutable Closed: SessionId list
          mutable Created: (SessionId * OpenCodeChildOptions) list
          ParentChild: Dictionary<SessionId, SessionId>
          mutable SendHandler: (SessionId -> string -> OpenCodePromptOptions -> Task<SendOutcome>) option
          mutable CreateHandler: (SessionId -> OpenCodeChildOptions -> Task<Result<SessionId, string>>) option
          CreatedSessions: ResizeArray<SessionId> }

    let createState () : State =
        { Sent = []
          Aborted = []
          Closed = []
          Created = []
          ParentChild = Dictionary<SessionId, SessionId>()
          SendHandler = None
          CreateHandler = None
          CreatedSessions = ResizeArray<SessionId>() }

    let ofState (state: State) : IOpenCodePort =
        { new IOpenCodePort with
            member _.SendPrompt(sId) text opts =
                state.Sent <- (sId, text, opts) :: state.Sent
                match state.SendHandler with
                | Some fn -> fn sId text opts
                | None ->
                    let mid = MessageId.create ("msg-" + Guid.NewGuid().ToString("N").Substring(0, 8))
                    Task.FromResult(Delivered mid)

            member _.AbortSession(sId) =
                state.Aborted <- sId :: state.Aborted
                Task.FromResult(Ok())

            member _.CreateChildSession(parentId) opts =
                state.Created <- (parentId, opts) :: state.Created
                match state.CreateHandler with
                | Some fn -> fn parentId opts
                | None ->
                    let childId = SessionId.create ("ch-" + Guid.NewGuid().ToString("N").Substring(0, 8))
                    state.ParentChild.[childId] <- parentId
                    state.CreatedSessions.Add(childId)
                    Task.FromResult(Ok childId)

            member _.CloseChildSession(childId) =
                state.Closed <- childId :: state.Closed
                Task.FromResult(Ok()) }

    /// Wire a MockOpenCode state into a full test environment.
    let createHost () : State * IEventObservationPort * ISessionHostPort =
        let state = createState ()
        let port = ofState state
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sessionPort = InjectedSessionPort(Some port, eventPort) :> ISessionHostPort
        (state, eventPort, sessionPort)
namespace Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

/// Exclusive XTrace range for one LWR materialization (COMPANION-015 / EXEC-031).
module MagicTodoLwr =

    /// Inclusive start, exclusive end on XTrace for one invocation / request.
    type BoundedRange =
        {
            /// Inclusive start cursor (often WorkRecordStart / invocation send head).
            StartInclusive: XTraceCursor
            /// Exclusive end frontier (ReviewFrontier / invocation completion head).
            EndExclusive: XTraceCursor
        }

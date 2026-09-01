namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo

module ManagerOpeningFloor =
    val workRecordStart:
        life: LifeProjection ->
        magic: MagicTodoProjection.LifeMagicTodoState option ->
        xTrace: XTraceProjectionState ->
            XTraceCursor option

    val effectiveOpeningFloor:
        life: LifeProjection option ->
        _magicTodo: MagicTodoProjection.MagicTodoProjectionState ->
        _xTrace: XTraceProjectionState ->
            XTraceCursor option

    val floorSequence: sessionId: SessionId -> projections: AgentProjectionSet -> int64 option

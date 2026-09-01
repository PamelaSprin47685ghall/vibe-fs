namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo

module MagicTodoSurface =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val TodoWriteDescription: string = "lifecycle/magic-todo/todowrite-description"
        [<Literal>]
        val PlanCompleteDescription: string = "lifecycle/magic-todo/plan-complete-description"
        [<Literal>]
        val WorkingOnDescription: string = "lifecycle/magic-todo/working-on-description"
        [<Literal>]
        val ObligationNameDescription: string = "lifecycle/magic-todo/obligation-name-description"
        [<Literal>]
        val ObligationHorizonDescription: string = "lifecycle/magic-todo/obligation-horizon-description"
        [<Literal>]
        val ObligationWorkDescription: string = "lifecycle/magic-todo/obligation-work-description"
        [<Literal>]
        val ObligationWriteResult: string = "lifecycle/magic-todo/obligation-write-result"
        [<Literal>]
        val ObligationAcceptedEpilogue: string = "lifecycle/magic-todo/obligation-accepted-epilogue"

    val shouldProjectManagerGuideline: canonicalRole: string -> todowriteProviderVisible: bool -> bool
    val todoWriteJsonSchema: string

    type CompatibilityTodoRow =
        { Content: string
          Status: string
          Priority: string }

    val obligationsToCompatibilityRows: workingOn: string -> items: ObligationList -> CompatibilityTodoRow list
    val renderObligationListWire: items: ObligationList -> string
    val obligationWriteSubs: acceptedEpilogue: string -> Map<string, string>

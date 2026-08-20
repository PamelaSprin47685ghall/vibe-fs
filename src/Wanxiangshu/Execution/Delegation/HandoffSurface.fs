namespace Wanxiangshu.Execution.Delegation

[<RequireQualifiedAccess>]
module HandoffSurface =

    let private rangeView (range: Wanxiangshu.Mission.Obligation.Todo.MagicTodoLwr.BoundedRange) =
        box
            {| start = int range.StartInclusive.Sequence
               ``end`` = int range.EndExclusive.Sequence |}

    let handoffWindow (previousEnd: obj) (currentEnd: int) : obj =
        let previous =
            if isNull previousEnd then
                None
            else
                Some(int64 (string previousEnd))

        let handoff = DelegationHandoff.window previous (int64 currentEnd)

        box
            {| start = int handoff.Range.StartInclusive.Sequence
               ``end`` = int handoff.Range.EndExclusive.Sequence
               isInitial = handoff.IsInitial |}

    let render (charge: string) (parentRecord: string) : string =
        DelegationHandoff.renderPrompt charge (Option.ofObj parentRecord)

    let childRange (startInclusive: int) (endExclusive: int) : obj =
        DelegationHandoff.childRange (int64 startInclusive) (int64 endExclusive)
        |> rangeView

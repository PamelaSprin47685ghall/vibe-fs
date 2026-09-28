namespace Wanxiangshu.Participant.Cognition

module TodoSinkSurface =
    let projectArgs (args: obj) : obj =
        match AssumeAdmission.tryDecode args with
        | Error rejection ->
            box
                {| ok = false
                   error = AssumeAdmission.Rejection.message rejection |}
        | Ok(_, todos) ->
            let snapshot = AssumeSnapshot.ofJson "null" todos

            box
                {| ok = true
                   todos =
                    TodoSink.compatibilityArgs snapshot.Todos
                    |> List.map (fun row ->
                        {| content = row.Content
                           status = row.Status
                           priority = row.Priority |})
                    |> List.toArray |}

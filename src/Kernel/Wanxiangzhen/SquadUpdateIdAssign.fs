module Wanxiangshu.Kernel.Wanxiangzhen.SquadUpdateIdAssign

open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent

type IdGen =
    { Generate: unit -> string
      RefExists: string -> bool }

let assignTaskIds
    (existingIds: Set<string>)
    (raw: (string option * string * string * string list) list)
    (gen: IdGen)
    : Result<TaskItem list, unit> =
    let rec genUnique (used: Set<string>) (remaining: int) : string option =
        if remaining <= 0 then
            None
        else
            let cand = gen.Generate()

            if Set.contains cand existingIds || Set.contains cand used || gen.RefExists cand then
                genUnique used (remaining - 1)
            else
                Some cand

    let rec go
        (used: Set<string>)
        (tasks: (string option * string * string * string list) list)
        : Result<TaskItem list, unit> =
        match tasks with
        | [] -> Ok []
        | (idOpt, title, desc, deps) :: rest ->
            match idOpt with
            | Some id ->
                go (Set.add id used) rest
                |> Result.map (fun tail ->
                    { taskId = id; title = title; description = desc; dependsOn = deps } :: tail)
            | None ->
                match genUnique used 10 with
                | Some tid ->
                    go (Set.add tid used) rest
                    |> Result.map (fun tail ->
                        { taskId = tid; title = title; description = desc; dependsOn = deps } :: tail)
                | None -> Error()

    go Set.empty raw

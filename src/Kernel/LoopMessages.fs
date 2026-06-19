module VibeFs.Kernel.LoopMessages

let loopFooter =
    [ "- report: a detailed description of what you did and why"
      "- affectedFiles: list of every file you modified or created"
      ""
      "A reviewer will examine your submission. If accepted, you are done. If rejected, you will receive specific feedback to address." ]

let buildLoopMessage (task: string) (bodyLines: string list) : string =
    [ "Task (loop): " + task; "" ] @ bodyLines @ loopFooter |> String.concat "\n"

module Wanxiangshu.Tests.HostToolsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools

let todoWriteToolNameOpenCode () =
    equal "todoWriteToolName Opencode" "todowrite" (todoWriteToolName Opencode)

let todoWriteToolNameMimo () =
    equal "todoWriteToolName Mimocode" "task" (todoWriteToolName Mimocode)

let todoWriteToolNameMux () =
    equal "todoWriteToolName Mux" "todowrite" (todoWriteToolName Mux)

let todoWriteToolNameOmp () =
    equal "todoWriteToolName Omp" "todowrite" (todoWriteToolName Omp)

let todoWritePromptNameOpenCode () =
    equal "todoWritePromptName Opencode" "todo_write" (todoWritePromptName Opencode)

let todoWritePromptNameMimo () =
    equal "todoWritePromptName Mimocode" "task" (todoWritePromptName Mimocode)

let taskToolNameOpenCode () =
    equal "taskToolName Opencode" "task" (taskToolName Opencode)

let taskToolNameMimo () =
    equal "taskToolName Mimocode" "actor" (taskToolName Mimocode)

let normalizeToolNameForMuxEdit () =
    equal "file_edit_" "edit" (normalizeToolNameForMux "file_edit_replace_string")

let normalizeToolNameForMuxRead () =
    equal "file_read" "read" (normalizeToolNameForMux "file_read")



let normalizeToolNameForMuxTodoWrite () =
    equal "todo_write" "todowrite" (normalizeToolNameForMux "todo_write")

let normalizeToolNameForMuxSkill () =
    equal "agent_skill_" "skill" (normalizeToolNameForMux "agent_skill_read")

let normalizeToolNameForMuxQuestion () =
    equal "ask_user_question" "question" (normalizeToolNameForMux "ask_user_question")

let normalizeToolNameForMuxPassThrough () =
    equal "pass through" "read" (normalizeToolNameForMux "read")

let normalizeToolNameOpenCodeTodoWrite () =
    equal "Opencode todo_write→todowrite" "todowrite" (normalizeToolName Opencode "todo_write")

let normalizeToolNameMimocodeTask () =
    equal "Mimocode task→todowrite" "todowrite" (normalizeToolName Mimocode "task")

let normalizeToolNameMimocodeActor () =
    equal "Mimocode actor→task" "task" (normalizeToolName Mimocode "actor")

let normalizeToolNameOmpTodoWrite () =
    equal "Omp todo_write→todowrite" "todowrite" (normalizeToolName Omp "todo_write")

let normalizeToolNamePassThrough () =
    equal "Opencode read→read" "read" (normalizeToolName Opencode "read")

let isTodoWriteToolNameTodowrite () =
    check "todowrite recognized" (isTodoWriteToolName "todowrite")

let isTodoWriteToolNameTask () =
    check "task recognized" (isTodoWriteToolName "task")

let isTodoWriteToolNameTodoRead () =
    check "todo_read recognized" (isTodoWriteToolName "todo_read")

let isTodoWriteToolNameFalse () =
    check "read not todo write" (not (isTodoWriteToolName "read"))

let allToolNames () =
    let names = allToolNames Opencode
    check "contains coder" (Array.contains "coder" names)
    check "contains task" (Array.contains "task" names)
    check "contains distinct" (Array.length names = (names |> Array.distinct |> Array.length))
    let mimo = allToolNames Mimocode
    check "Mimocode has actor" (Array.contains "actor" mimo)

let run () =
    todoWriteToolNameOpenCode ()
    todoWriteToolNameMimo ()
    todoWriteToolNameMux ()
    todoWriteToolNameOmp ()
    todoWritePromptNameOpenCode ()
    todoWritePromptNameMimo ()
    taskToolNameOpenCode ()
    taskToolNameMimo ()
    normalizeToolNameForMuxEdit ()
    normalizeToolNameForMuxRead ()
    normalizeToolNameForMuxTodoWrite ()
    normalizeToolNameForMuxSkill ()
    normalizeToolNameForMuxQuestion ()
    normalizeToolNameForMuxPassThrough ()
    normalizeToolNameOpenCodeTodoWrite ()
    normalizeToolNameMimocodeTask ()
    normalizeToolNameMimocodeActor ()
    normalizeToolNameOmpTodoWrite ()
    normalizeToolNamePassThrough ()
    isTodoWriteToolNameTodowrite ()
    isTodoWriteToolNameTask ()
    isTodoWriteToolNameTodoRead ()
    isTodoWriteToolNameFalse ()
    allToolNames ()

namespace Wanxiangshu.Participant.Cognition


[<RequireQualifiedAccess>]
module AssumeAdmission =

    type Rejection =
        | MissingUpdate
        | UpdateNotString
        | MissingTodos
        | TodosNotArray
        | TodoRowNotObject of index: int
        | TodoContentMissing of index: int
        | TodoContentNotString of index: int
        | TodoContentBlank of index: int
        | TodoStatusMissing of index: int
        | TodoStatusUnknown of index: int * value: string
        | TodoPriorityUnknown of index: int * value: string
        | UnknownArgument of name: string

    [<RequireQualifiedAccess>]
    module Rejection =
        val message: rejection: Rejection -> string

    [<RequireQualifiedAccess>]
    type UpdateOutcome =
        | OneCanvas of canvasJson: string
        | NoOutput
        | MultipleOutputs of count: int
        | ProgramError of message: string

    [<RequireQualifiedAccess>]
    module UpdateOutcome =
        val toRejection: outcome: UpdateOutcome -> Rejection

    val tryDecode: args: obj -> Result<string * (string * TodoStatus * TodoPriority) list, Rejection>

namespace Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
module TerminalValidity =
    [<RequireQualifiedAccess>]
    type Rejection =
        | Empty
        | XmlOnly

    val describe: rejection: Rejection -> string
    val check: text: string -> Result<unit, Rejection>
    val isValid: text: string -> bool

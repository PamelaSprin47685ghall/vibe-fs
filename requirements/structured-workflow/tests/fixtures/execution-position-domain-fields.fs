module DomainProfile

open ForeignProtocol

type Profile =
    { DisplayName: string
      IsVerified: bool
      PreferredInstruction: RemoteInstruction }

let render profile =
    sprintf "%s (%b): %A" profile.DisplayName profile.IsVerified profile.PreferredInstruction

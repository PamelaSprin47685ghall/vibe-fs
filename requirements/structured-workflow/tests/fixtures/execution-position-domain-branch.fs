module DomainProfile

type Profile =
    { IsVerified: bool }

type Badge =
    { Label: string
      Priority: int }

let badge profile =
    match profile.IsVerified with
    | true -> { Label = "verified"; Priority = 1 }
    | false -> { Label = "unverified"; Priority = 0 }

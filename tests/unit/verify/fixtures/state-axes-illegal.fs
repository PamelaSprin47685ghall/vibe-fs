module Sample

type Availability =
    | Open
    | Closed

type Confirmation =
    | Pending
    | Confirmed

type RuntimeFacts = {
    Availability: Availability
    Confirmation: Confirmation
}

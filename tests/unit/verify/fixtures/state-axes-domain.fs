module Sample

type Availability =
    | Open
    | Closed

type Confirmation =
    | Pending
    | Confirmed

/// DSL-state-combination: domain
type RuntimeFacts = {
    Availability: Availability
    Confirmation: Confirmation
}

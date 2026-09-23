namespace Wanxiangshu.Sphinx.V2.Core

type ResourceKind =
    | Consumed of unitName: string
    | Capacity of slotName: string

type ResourceSpec =
    { Name: string
      Kind: ResourceKind
      AuthorizedLimit: float }

type Reservation =
    { WorkId: WorkId
      Attempt: Attempt
      Resources: Map<string, float>
      /// Money is kept in the smallest currency unit so binary float addition cannot
      /// accumulate a fiscal drift across a long inquiry.
      MoneyMinor: int64 option }

type SettledUsage =
    { WorkId: WorkId
      Attempt: Attempt
      Resources: Map<string, float>
      MoneyMinor: int64 option
      /// True when the provider never reported usage; the reserved amount stays.
      UsageUnresolved: bool
      /// True when the settled usage exceeded the reservation. Recorded, not rejected.
      Overrun: bool }

type BudgetError = { Code: string; Message: string }

module Budget =
    val validateSpecs: ResourceSpec list -> Result<unit, BudgetError>
    val signedFree: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float
    val availableForNewWork: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float
    val observedOverrun: ResourceSpec list -> Map<string, float> -> Map<string, float> -> string -> float

    val tryReserve:
        ResourceSpec list -> Map<string, float> -> Map<string, float> -> Reservation ->
            Result<Map<string, float>, BudgetError>

    val settle:
        Map<string, float> ->
        Map<string, float> ->
            Reservation ->
            SettledUsage ->
                Result<Map<string, float> * Map<string, float>, BudgetError>

    val mergeReservations: Reservation list -> Map<string, float>
    val mergeReserved: Map<string, float> list -> Map<string, float>

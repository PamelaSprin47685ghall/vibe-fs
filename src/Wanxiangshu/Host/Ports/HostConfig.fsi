namespace Wanxiangshu.Host.Ports

/// HNI P-19: the host's activation config as product-shaped data.
///
/// The product states which settings must hold; the adapter owns where the host stores them.
/// Values are scalars only: a product never reads or writes an opaque host object, and never
/// parses a host config tree.
[<RequireQualifiedAccess>]
type HostSettingValue =
    | Bool of value: bool
    | Text of value: string

type RequiredSetting =
    { Path: string list
      Value: HostSettingValue }

/// The first required setting the host could not establish.
type SettingGap =
    { Path: string list
      Reason: string }

/// HNI P-19: actuator for `Wanxiangshu.Host.HostCompactionPolicy.requiredSettings`-style policy.
///
/// The policy (which settings, and why) stays in the product. The port only establishes them,
/// so an adapter contains no product clause text and no verdict.
type IHostConfigPort =
    /// Read one scalar setting; `Ok None` when the host does not expose it at this path.
    abstract Read: path: string list -> Result<HostSettingValue option, HostError>
    /// Establish every setting in order.
    /// `Ok None` = every setting established.
    /// `Ok(Some gap)` = the host refused that one setting; the core owns the verdict.
    /// `Error` = the round trip did not complete, so nothing about the config may be assumed.
    abstract Apply: settings: RequiredSetting list -> Result<SettingGap option, HostError>

module Wanxiangshu.Runtime.Serialization.TomlValue

type TomlValue =
    | String of string
    | Integer of int
    | Boolean of bool
    | StringArray of string list
    | TableArray of (string * TomlValue) list list
    | Table of (string * TomlValue) list

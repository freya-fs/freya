﻿//----------------------------------------------------------------------------
//
// Copyright (c) 2014
//
//    Ryan Riley (@panesofglass) and Andrew Cherry (@kolektiv)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------

[<AutoOpen>]
module Freya.Recorder.Types

open System
open Aether
open Aether.Operators
open Fleece
open Fleece.Operators
open Freya.Core

(* Types *)

type FreyaRecorderRecord =
    { Id: Guid
      Timestamp: DateTime
      Data: Map<string, obj> }

    static member ToJSON (x: FreyaRecorderRecord) =
        jobj [
            "id" .= x.Id
            "timestamp" .= x.Timestamp
            "inspections" .= (x.Data |> Map.toList |> List.map fst) ]

    static member DataLens =
        (fun x -> x.Data), (fun d x -> { x with Data = d })

(* Lenses *)    

let freyaRecordDataPLens<'a> key =
    FreyaRecorderRecord.DataLens >-?> mapPLens key <?-> boxIso<'a>
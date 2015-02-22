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
module Freya.Router.Types

open Arachne.Http
open Freya.Pipeline

(* Routes *)

type FreyaRoute =
    { Method: FreyaRouteMethod
      Path: string
      Pipeline: FreyaPipeline }

and FreyaRouteMethod =
    | All
    | Methods of Method list

type FreyaRouteData =
    Map<string, string>

(* Computation Expression *)

type FreyaRouter = 
    FreyaRoute list -> unit * FreyaRoute list


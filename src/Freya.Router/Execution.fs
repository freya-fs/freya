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
//
//----------------------------------------------------------------------------

[<AutoOpen>]
module internal Freya.Router.Execution

open Freya.Core
open Freya.Core.Operators
open Freya.Pipeline
open Freya.Typed.Http

(* Find *)

let rec private findTrie path data (trie: CompilationTrie)  =
    freya {
        match path with
        | segment :: path -> return! (pick segment data >=> ret path) trie.Children
        | _ -> return Some (trie.Pipelines, data) }

and private ret path x =
    freya {
        match x with
        | Some (trie, data) -> return! findTrie path data trie
        | _ -> return None }

and private pick segment data tries =
    freya {
        match tries with
        | [] -> return None
        | tries ->
            let! state = Freya.getState

            let x, state =
                List.fold (fun (x, state) trie ->
                    match x with
                    | Some (trie, data) -> (Some (trie, data), state)
                    | None -> Async.RunSynchronously (recognize segment data trie state)) (None, state) tries

            do! Freya.setState state

            return x }

and private recognize segment data trie =
    freya {
        let result = addFreyaRouterExecutionRecord trie.Key segment

        match trie.Recognizer with
        | Capture x -> return! result Captured *> Freya.init (Some (trie, Map.add x segment data))
        | Ignore x when x = segment -> return! result Matched *> Freya.init (Some (trie, data))
        | _ -> return! result Failed *> Freya.init None }

(* Match *)

let private find meth x =
    freya {
        return List.tryFind (function | (Methods m, _) -> List.exists ((=) meth) m
                                      | _ -> true) x }

let private pair data x =
    freya {
        return Option.map (fun (_, pipeline) -> pipeline, data) x }

let private matchMethod meth x =
    freya {
        match x with
        | Some (pipelines, data) -> return! (find meth >=> pair data) pipelines
        | _ -> return None }

(* Execution *)

let executeCompilation trie =
    freya {
        let! meth = Freya.getLens Request.meth
        let! path = segmentize <!> Freya.getLens Request.path
        let! res = (findTrie path Map.empty >=> matchMethod meth) trie
        
        match res with
        | Some (pipeline, data) -> return! Freya.setLensPartial Route.values data *> pipeline
        | _ -> return Next }

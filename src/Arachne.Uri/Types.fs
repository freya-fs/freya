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

module Arachne.Uri

open System.ComponentModel
open System.Net
open System.Net.Sockets
open Arachne.Formatting
open Arachne.Parsing
open FParsec

(* RFC 3986

   Types, parsers and formatters implemented to mirror the specification of 
   URI semantics as defined in RFC 3986.

   Taken from [http://tools.ietf.org/html/rfc3986] *)

(* Characters

   Taken from RFC 3986, Section 2 Characters
   See [http://tools.ietf.org/html/rfc3986#section-2] *)

let private unreserved =
    Set.unionMany [
        Grammar.alpha
        Grammar.digit
        set [ '-'; '.'; '_'; '~' ] ]

//let private genDelims =
//    set [ ':'; '/'; '?'; '#'; '['; ']'; '@' ]

let private subDelims =
    set [ '!'; '$'; '&'; '\''; '('; ')'; '*'; '+'; ','; ';'; '=' ]

//let private reserved =
//    Set.unionMany [
//        genDelims
//        subDelims ]

(* Scheme

   Taken from RFC 3986, Section 3.1 Scheme
   See [http://tools.ietf.org/html/rfc3986#section-3.1] *)

(* Section 3.1 *)

type Scheme =
    | Scheme of string

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let schemeChars =
            Set.unionMany [
                Grammar.alpha
                Grammar.digit
                set [ '+'; '-'; '.' ] ]

        let schemeP =
            satisfy ((?>) Grammar.alpha) .>>. manySatisfy ((?>) schemeChars)
            |>> ((fun (x, xs) -> sprintf "%c%s" x xs) >> Scheme)

        let schemeF =
            function | Scheme x -> append x

        { Parse = schemeP
          Format = schemeF }

    static member Format =
        Formatting.format Scheme.TypeMapping.Format

    static member Parse =
        Parsing.parse Scheme.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse Scheme.TypeMapping.Parse

    override x.ToString () =
        Scheme.Format x

(* Authority

   Taken from RFC 3986, Section 3.2 Authority
   See [http://tools.ietf.org/html/rfc3986#section-3.2] *)

(* Section 3.2 *)

type Authority =
    { Host: Host
      Port: Port option
      UserInfo: UserInfo option }

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let authorityP =
            opt (attempt UserInfo.TypeMapping.Parse) 
            .>>. Host.TypeMapping.Parse 
            .>>. opt Port.TypeMapping.Parse
            |>> fun ((user, host), port) ->
                { Host = host
                  Port = port
                  UserInfo = user }

        let authorityF =
            function | { Host = h
                         Port = p
                         UserInfo = u } ->
                            let formatters =
                                [ (function | Some u -> UserInfo.TypeMapping.Format u 
                                            | _ -> id) u
                                  Host.TypeMapping.Format h
                                  (function | Some p -> Port.TypeMapping.Format p 
                                            | _ -> id) p ]

                            fun b -> List.fold (|>) b formatters

        { Parse = authorityP
          Format = authorityF }

    static member Format =
        Formatting.format Authority.TypeMapping.Format

    static member Parse =
        Parsing.parse Authority.TypeMapping.Parse
    
    static member TryParse =
        Parsing.tryParse Authority.TypeMapping.Parse

    override x.ToString () =
        Authority.Format x

(* Section 3.2.1 *)

and UserInfo =
    | UserInfo of string

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let userInfoChars =
            Set.unionMany [
                unreserved
                subDelims
                set [ ':' ] ]

        let userInfoP =
            manySatisfy ((?>) userInfoChars) .>> skipChar '@' |>> UserInfo

        let userInfoF =
            function | UserInfo x -> append x >> append "@"

        { Parse = userInfoP
          Format = userInfoF }

(* Section 3.2.2 *)

(* Note: In this instance we use the built in IP Address type and parser
   as the standard is implemented fully and seems to handle all suitable cases.

   We also make a slight restriction for practicality at the moment on
   implementing IP-literal as an IPv6 specific type, discarding IPvFuture. As
   it stands, that's unlikely to be an issue, but could perhaps be revisited. *)

and Host =
    | IPv4 of IPAddress
    | IPv6 of IPAddress
    | Name of string

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let ipv6Chars =
            Set.unionMany [
                Grammar.hexdig
                set [ ':' ] ]

        let ipv6AddressP =
            skipChar '[' >>. (many1Satisfy ((?>) ipv6Chars) >>= (fun x ->
                match IPAddress.TryParse x with
                | true, x when x.AddressFamily = AddressFamily.InterNetworkV6 -> preturn (IPv6 x)
                | _ -> pzero)) .>> skipChar ']'

        let ipv4Chars =
            Set.unionMany [
                Grammar.digit
                set [ '.' ] ]

        let ipv4AddressP =
            many1Satisfy ((?>) ipv4Chars) >>= (fun x ->
                match IPAddress.TryParse x with
                | true, x when x.AddressFamily = AddressFamily.InterNetwork -> preturn (IPv4 x)
                | _ -> pzero)

        let regNameChars =
            Set.unionMany [
                unreserved
                subDelims ]

        let regNameP =
            manySatisfy ((?>) regNameChars) |>> Name

        let hostP =
            choice [
                attempt ipv6AddressP
                attempt ipv4AddressP
                regNameP ]

        let hostF =
            function | IPv4 x -> append (string x)
                     | IPv6 x -> append "[" >> append (string x) >> append "]"
                     | Name x -> append x

        { Parse = hostP
          Format = hostF }

(* Section 3.2.3 *)

and Port =
    | Port of int

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let portP =
            skipChar ':' >>. puint32 |>> (int >> Port)

        let portF =
            function | Port x -> append ":" >> append (string x)

        { Parse = portP
          Format = portF }

(* Path

   Taken from RFC 3986, Section 3.3 Path
   See [http://tools.ietf.org/html/rfc3986#section-3.3] *)

let private pchar =
    Set.unionMany [
        unreserved
        subDelims
        set [ ':'; '@' ] ]

let private pcharNc =
    Set.remove ':' pchar

let private segmentP =
    manySatisfy ((?>) pchar)

let private segmentNzP =
    many1Satisfy ((?>) pchar)

let private segmentNzNcP =
    many1Satisfy ((?>) pcharNc)

(* Absolute Or Empty *)

type PathAbsoluteOrEmpty =
    | PathAbsoluteOrEmpty of string list

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let pathAbsoluteOrEmptyP =
            many (skipChar '/' >>. segmentP) |>> PathAbsoluteOrEmpty

        let pathAbsoluteOrEmptyF =
            function | PathAbsoluteOrEmpty [] -> id
                     | PathAbsoluteOrEmpty xs -> slashF >> join append slashF xs

        { Parse = pathAbsoluteOrEmptyP
          Format = pathAbsoluteOrEmptyF }

    static member Format =
        Formatting.format PathAbsoluteOrEmpty.TypeMapping.Format

    static member Parse =
        Parsing.parse PathAbsoluteOrEmpty.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse PathAbsoluteOrEmpty.TypeMapping.Parse

    override x.ToString () =
        PathAbsoluteOrEmpty.Format x

(* Absolute *)

type PathAbsolute =
    | PathAbsolute of string list

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let pathAbsoluteP =
            skipChar '/' >>. opt (segmentNzP .>>. many (skipChar '/' >>. segmentP))
            |>> function | Some (x, xs) -> PathAbsolute (x :: xs)
                         | _ -> PathAbsolute []

        let pathAbsoluteF =
            function | PathAbsolute xs -> slashF >> join append slashF xs

        { Parse = pathAbsoluteP
          Format = pathAbsoluteF }

    static member Format =
        Formatting.format PathAbsolute.TypeMapping.Format

    static member Parse =
        Parsing.parse PathAbsolute.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse PathAbsolute.TypeMapping.Parse

    override x.ToString () =
        PathAbsolute.Format x

(* No Scheme *)

type PathNoScheme =
    | PathNoScheme of string list

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let pathNoSchemeP =
            segmentNzNcP .>>. many (slashP >>. segmentP)
            |>> fun (x, xs) -> PathNoScheme (x :: xs)

        let pathNoSchemeF =
            function | PathNoScheme xs -> join append slashF xs

        { Parse = pathNoSchemeP
          Format = pathNoSchemeF }

    static member Format =
        Formatting.format PathNoScheme.TypeMapping.Format

    static member Parse =
        Parsing.parse PathNoScheme.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse PathNoScheme.TypeMapping.Parse

    override x.ToString () =
        PathNoScheme.Format x

(* Rootless *)

type PathRootless =
    | PathRootless of string list

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let pathRootlessP =
            segmentNzP .>>. many (skipChar '/' >>. segmentP)
            |>> fun (x, xs) -> PathRootless (x :: xs)

        let pathRootlessF =
            function | PathRootless xs -> join append slashF xs

        { Parse = pathRootlessP
          Format = pathRootlessF }

    static member Format =
        Formatting.format PathRootless.TypeMapping.Format

    static member Parse =
        Parsing.parse PathRootless.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse PathRootless.TypeMapping.Parse

    override x.ToString () =
        PathRootless.Format x

(* Empty *)

type PathEmpty =
    | PathEmpty

(* Query

   Taken from RFC 3986, Section 3.4 Query
   See [http://tools.ietf.org/html/rfc3986#section-3.4] *)

type Query =
    | Query of string

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let queryChars =
            Set.unionMany [
                pchar
                set [ '/'; '?' ] ]

        let queryP =
            skipChar '?' >>. manySatisfy ((?>) queryChars) |>> Query

        let queryF =
            function | Query x -> append "?" >> append x

        { Parse = queryP
          Format = queryF }

    static member Format =
        Formatting.format Query.TypeMapping.Format

    static member Parse =
        Parsing.parse Query.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse Query.TypeMapping.Parse

    override x.ToString () =
        Query.Format x

(* Fragment

   Taken from RFC 3986, Section 3.5 Fragment
   See [http://tools.ietf.org/html/rfc3986#section-3.5] *)

type Fragment =
    | Fragment of string

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =
    
        let fragmentChars =
            Set.unionMany [
                pchar
                set [ '/'; '?' ] ]

        let fragmentP =
            skipChar '#' >>. manySatisfy ((?>) fragmentChars) |>> Fragment

        let fragmentF =
            function | Fragment x -> append "#" >> append x

        { Parse = fragmentP
          Format = fragmentF }

    static member Format =
        Formatting.format Fragment.TypeMapping.Format

    static member Parse =
        Parsing.parse Fragment.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse Fragment.TypeMapping.Parse

    override x.ToString () =
        Fragment.Format x

(* URI

   Taken from RFC 3986, Section 3 URI
   See [http://tools.ietf.org/html/rfc3986#section-3] *)

(* Note: In the case of absolute paths in the hierarchy, which the parser
   will correctly determine, the type system cannot adequately protect
   against an absolute path being created with two initial empty
   segments (and in the absence of a strong dependent system, it's likely
   to stay that way without extreme convolution). (Created in this case
   refers to direct instance creation).

   It is therefore possible to create an invalid URI string using the URI
   type if some care is not taken. For now this will simply have to stand
   as an allowed but "known not ideal" behaviour, under review.
   
   It is of course also possible to create invalid paths by creating strings
   which are invalid segments. Though the parser will reject these, manual
   creation will still allow this case. *)

type Uri =
    { Scheme: Scheme
      Hierarchy: HierarchyPart
      Query: Query option
      Fragment: Fragment option }

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let uriP =
            Scheme.TypeMapping.Parse .>> skipChar ':'
            .>>. HierarchyPart.TypeMapping.Parse 
            .>>. opt Query.TypeMapping.Parse
            .>>. opt Fragment.TypeMapping.Parse
            |>> fun (((scheme, hierarchy), query), fragment) ->
                { Scheme = scheme
                  Hierarchy = hierarchy
                  Query = query
                  Fragment = fragment }

        let uriF =
            function | { Scheme = s
                         Hierarchy = h
                         Query = q
                         Fragment = f } -> 
                            let formatters =
                                [ Scheme.TypeMapping.Format s
                                  append ":"
                                  HierarchyPart.TypeMapping.Format h
                                  (function | Some q -> Query.TypeMapping.Format q | _ -> id) q
                                  (function | Some f -> Fragment.TypeMapping.Format f | _ -> id) f ]

                            fun b -> List.fold (fun b f -> f b) b formatters

        { Parse = uriP
          Format = uriF }

    static member Format =
        Formatting.format Uri.TypeMapping.Format

    static member Parse =
        Parsing.parse Uri.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse Uri.TypeMapping.Parse

    override x.ToString () =
        Uri.Format x

and HierarchyPart =
    | Authority of Authority * PathAbsoluteOrEmpty
    | Absolute of PathAbsolute
    | Rootless of PathRootless
    | Empty

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let authorityP =
            skipString "//" >>. Authority.TypeMapping.Parse 
            .>>. PathAbsoluteOrEmpty.TypeMapping.Parse 
            |>> Authority

        let hierarchyPartP =
            choice [
                authorityP
                PathAbsolute.TypeMapping.Parse |>> Absolute
                PathRootless.TypeMapping.Parse |>> Rootless
                preturn Empty ]

        let authorityF (a, p)=
                append "//" 
             >> Authority.TypeMapping.Format a 
             >> PathAbsoluteOrEmpty.TypeMapping.Format p

        let hierarchyPartF =
            function | Authority (a, p) -> authorityF (a, p)
                     | Absolute p -> PathAbsolute.TypeMapping.Format p
                     | Rootless p -> PathRootless.TypeMapping.Format p
                     | Empty -> id

        { Parse = hierarchyPartP
          Format = hierarchyPartF }

(* Relative Reference

   Taken from RFC 3986, Section 4.2 Relative Reference
   See [http://tools.ietf.org/html/rfc3986#section-4.2] *)

type RelativeReference =
    { Relative: RelativePart
      Query: Query option
      Fragment: Fragment option }

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let relativeReferenceP =
            RelativePart.TypeMapping.Parse
            .>>. opt Query.TypeMapping.Parse
            .>>. opt Fragment.TypeMapping.Parse
            |>> fun ((relative, query), fragment) ->
                { Relative = relative
                  Query = query
                  Fragment = fragment }

        let relativeReferenceF =
            function | { Relative = r
                         Query = q
                         Fragment = f } -> 
                            let formatters =
                                [ RelativePart.TypeMapping.Format r
                                  (function | Some q -> Query.TypeMapping.Format q | _ -> id) q
                                  (function | Some f -> Fragment.TypeMapping.Format f | _ -> id) f ]

                            fun b -> List.fold (fun b f -> f b) b formatters

        { Parse = relativeReferenceP
          Format = relativeReferenceF }

    static member Format =
        Formatting.format RelativeReference.TypeMapping.Format

    static member Parse =
        Parsing.parse RelativeReference.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse RelativeReference.TypeMapping.Parse

    override x.ToString () =
        RelativeReference.Format x

and RelativePart =
    | Authority of Authority * PathAbsoluteOrEmpty
    | Absolute of PathAbsolute
    | NoScheme of PathNoScheme
    | Empty

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let authorityP =
            skipString "//" >>. Authority.TypeMapping.Parse
            .>>. PathAbsoluteOrEmpty.TypeMapping.Parse 
            |>> Authority

        let relativePartP =
            choice [
                authorityP
                PathAbsolute.TypeMapping.Parse |>> Absolute
                PathNoScheme.TypeMapping.Parse |>> NoScheme
                preturn Empty ]

        let authorityF (a, p) =
                append "//" 
             >> Authority.TypeMapping.Format a 
             >> PathAbsoluteOrEmpty.TypeMapping.Format p

        let relativePartF =
            function | Authority (a, p) -> authorityF (a, p)
                     | Absolute p -> PathAbsolute.TypeMapping.Format p
                     | NoScheme p -> PathNoScheme.TypeMapping.Format p
                     | Empty -> id

        { Parse = relativePartP
          Format = relativePartF }

(* Absolute URI

   Taken from RFC 3986, Section 4.3 Absolute URI
   See [http://tools.ietf.org/html/rfc3986#section-4.3] *)

type AbsoluteUri =
    { Scheme: Scheme
      Hierarchy: HierarchyPart
      Query: Query option }

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let absoluteUriP =
            Scheme.TypeMapping.Parse .>> skipChar ':' 
            .>>. HierarchyPart.TypeMapping.Parse 
            .>>. opt Query.TypeMapping.Parse
            |>> fun ((scheme, hierarchy), query) ->
                { Scheme = scheme
                  Hierarchy = hierarchy
                  Query = query }

        let absoluteUriF =
            function | { AbsoluteUri.Scheme = s
                         Hierarchy = h
                         Query = q } -> 
                            let formatters =
                                [ Scheme.TypeMapping.Format s
                                  append ":"
                                  HierarchyPart.TypeMapping.Format h
                                  (function | Some q -> Query.TypeMapping.Format q | _ -> id) q ]

                            fun b -> List.fold (fun b f -> f b) b formatters

        { Parse = absoluteUriP
          Format = absoluteUriF }

    static member Format =
        Formatting.format AbsoluteUri.TypeMapping.Format

    static member Parse =
        Parsing.parse AbsoluteUri.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse AbsoluteUri.TypeMapping.Parse

    override x.ToString () =
        AbsoluteUri.Format x

(* URI Reference

   Taken from RFC 3986, Section 4.1 URI Reference
   See [http://tools.ietf.org/html/rfc3986#section-4.1] *)

type UriReference =
    | Uri of Uri
    | Relative of RelativeReference

    [<EditorBrowsable (EditorBrowsableState.Never)>]
    static member TypeMapping =

        let uriReferenceP =
            choice [
                attempt Uri.TypeMapping.Parse |>> Uri
                RelativeReference.TypeMapping.Parse |>> Relative ]

        let uriReferenceF =
            function | Uri x -> Uri.TypeMapping.Format x
                     | Relative x -> RelativeReference.TypeMapping.Format x

        { Parse = uriReferenceP
          Format = uriReferenceF }

    static member Format =
        Formatting.format UriReference.TypeMapping.Format

    static member Parse =
        Parsing.parse UriReference.TypeMapping.Parse

    static member TryParse =
        Parsing.tryParse UriReference.TypeMapping.Parse

    override x.ToString () =
        UriReference.Format x
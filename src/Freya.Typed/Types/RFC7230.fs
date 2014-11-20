﻿[<AutoOpen>]
module Freya.Typed.RFC7230

#nowarn "60"

open System
open FParsec

(* RFC 7230

   Types, parsers and formatters implemented to mirror the specification of 
   HTTP semantics as defined in RFC 7230.

   Taken from [http://tools.ietf.org/html/rfc7230] *)

(* Uniform Resource Identifiers

   Taken from RFC 7230, Section 2.7 Uniform Resource Identifiers
   See [http://tools.ietf.org/html/rfc7230#section-3.2.3] *)

type PartialUri =
    { Relative: Relative
      Query: Query option }

let internal partialUriF =
    function | { PartialUri.Relative = r
                 Query = q } ->
                    let formatters =
                        [ relativeF r
                          (function | Some q -> queryF q | _ -> id) q ]

                    fun b -> List.fold (fun b f -> f b) b formatters

let internal partialUriP =
    relativeP .>>. opt queryP
    |>> fun (relative, query) ->
        { Relative = relative
          Query = query }

type PartialUri with

    static member Format =
        format partialUriF

    static member Parse =
        parseExact partialUriP

    static member TryParse =
        parseOption partialUriP

    override x.ToString () =
        PartialUri.Format x

(* Whitespace

   Taken from RFC 7230, Section 3.2.3. Whitespace
   See [http://tools.ietf.org/html/rfc7230#section-3.2.3] *)

let internal owsP = 
    skipManySatisfy ((?>) RFC5234.wsp)

//    let rws =
//        skipMany1Satisfy (fun c -> Set.contains c wsp)

let internal bwsP =
    owsP

(* Field Value Components

   Taken from RFC 7230, Section 3.2.6. Field Value Components
   See [http://tools.ietf.org/html/rfc7230#section-3.2.6] *)

let internal tchar = 
    Set.unionMany [ 
        set [ '!'; '#'; '$'; '%'; '&'; '\''; '*'
              '+'; '-'; '.'; '^'; '_'; '`'; '|'; '~' ]
        RFC5234.alpha
        RFC5234.digit ]

let internal tokenP = 
    many1Satisfy ((?>) tchar)

let internal obsText =
    charRange 0x80 0xff

let internal qdtext =
    Set.unionMany [
        set [ RFC5234.htab; RFC5234.sp; char 0x21 ]
        charRange 0x23 0x5b
        charRange 0x5d 0x7e
        obsText ]

//let ctext =
//    Set.unionMany [
//        set [ htab; sp ]
//        charRange 0x21 0x27
//        charRange 0x2a 0x5b
//        charRange 0x5d 0x7e
//        obsText ]

let internal quotedPairChars =
    Set.unionMany [
        set [ RFC5234.htab; RFC5234.sp ]
        RFC5234.vchar
        obsText ]

let internal quotedPairP : Parser<char, unit> =
        skipChar '\\' 
    >>. satisfy ((?>) quotedPairChars)

let internal quotedStringP : Parser<string, unit> =
        skipChar RFC5234.dquote 
    >>. many (quotedPairP <|> satisfy ((?>) qdtext)) |>> (fun x -> string (String (List.toArray x)))
    .>> skipChar RFC5234.dquote

(* ABNF List Extension: #rule

   Taken from RFC 7230, Section 7. ABNF List Extension: #rule
   [http://tools.ietf.org/html/rfc7230#section-7] *)

let private infixHeadP s p =
    (attempt p |>> Some) <|> (s >>% None)

let private infixTailP s p =
    many (owsP >>? s >>? owsP >>? opt p)

(* Note:
   The infix and prefix parsers are designed to convey as accurately as possible the 
   meaning of the ABNF #rule extension including the laxity of specification for backward 
   compatibility. Whether they are a perfectly true representation is open to debate, 
   but they should perform sensibly under normal conditions. *)

let internal infixP s p = 
    infixHeadP s p .>>. infixTailP s p .>> owsP |>> fun (x, xs) -> x :: xs |> List.choose id

let internal infix1P s p =
    notEmpty (infixP s p)

let internal prefixP s p =
    many (owsP >>? s >>? owsP >>? p)

(* HTTP Version

   Taken from RFC 7230, Section 3.1 Request Line
   See [http://tools.ietf.org/html/rfc7230#section-3.1] *)

type HttpVersion =
    | HTTP of float 
    | Custom of string

let private httpVersionF =
    function | HttpVersion.HTTP x -> appendf1 "HTTP/{0:G4}" x 
             | HttpVersion.Custom x -> append x

let private httpVersionP =
    choice [
        skipString "HTTP/1.0" >>% HttpVersion.HTTP 1.0
        skipString "HTTP/1.1" >>% HttpVersion.HTTP 1.1
        restOfLine false |>> HttpVersion.Custom ]

let formatHttpVersion =
    format httpVersionF

type HttpVersion with

    static member Format =
        format httpVersionF

    static member Parse =
        parseExact httpVersionP

    override x.ToString () =
        HttpVersion.Format x

(* Content-Length

   Taken from RFC 7230, Section 3.3.2 Content-Length
   See [http://tools.ietf.org/html/rfc7230#section-3.3.2] *)

type ContentLength =
    | ContentLength of int

let private contentLengthF =
    function | ContentLength x -> append (string x)

let private contentLengthP =
    puint32 |>> (int >> ContentLength)

type ContentLength with

    static member Format =
        format contentLengthF

    static member Parse =
        parseExact contentLengthP

    static member TryParse =
        parseOption contentLengthP

    override x.ToString () =
        ContentLength.Format x

(* Host

   Taken from RFC 7230, Section 5.4 Host
   See [http://tools.ietf.org/html/rfc7230#section-5.4] *)

type Host =
    | Host of RFC3986.Host * Port option

let private hostF =
    function | Host (h, p) ->
                let formatters =
                    [ hostF h
                      (function | Some p -> portF p | _ -> id) p ]

                fun b -> List.fold (fun b f -> f b) b formatters

let private hostP =
    hostP .>>. opt portP |>> Host

type Host with

    static member Format =
        format hostF

    static member Parse =
        parseExact hostP

    static member TryParse =
        parseOption hostP

    override x.ToString () =
        Host.Format x

(* Connection

   Taken from RFC 7230, Section 6.1 Connection
   See [http://tools.ietf.org/html/rfc7230#section-6.1] *)

type Connection =
    | Connection of ConnectionOption list

and ConnectionOption =
    | ConnectionOption of string

let private connectionOptionF =
    function | ConnectionOption x -> append x

let private connectionF =
    function | Connection x -> join commaF connectionOptionF x

let private connectionP =
    infix1P commaP tokenP |>> (List.map ConnectionOption >> Connection)

type Connection with

    static member Format =
        format connectionF

    static member Parse =
        parseExact connectionP

    static member TryParse =
        parseOption connectionP

    override x.ToString () =
        Connection.Format x
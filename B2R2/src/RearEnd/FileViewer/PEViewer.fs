(*
  B2R2 - the Next-Generation Reversing Platform

  Copyright (c) SoftSec Lab. @ KAIST, since 2016

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*)

module B2R2.RearEnd.FileViewer.PEViewer

open B2R2
open B2R2.FrontEnd.BinFile
open B2R2.RearEnd.FileViewer.Helper
open System.Reflection.PortableExecutable

let badAccess _ _ =
  raise InvalidFileFormatException

let translateChracteristics chars =
  let enumChars =
    System.Enum.GetValues (typeof<Characteristics>)
    :?> Characteristics []
    |> Array.toList
  let rec loop acc chars = function
    | [] -> List.rev acc
    | enumChar :: tail ->
      if uint64 enumChar &&& chars = uint64 enumChar then
        loop ((" - " + enumChar.ToString ()) :: acc) chars tail
      else
        loop acc chars tail
  loop [] chars enumChars

let dumpFileHeader _ (file: PEBinFile) =
  let hdr = file.PE.PEHeaders.CoffHeader
  out.PrintTwoCols
    "Machine:"
    (String.u64ToHex (uint64 hdr.Machine)
    + String.wrapParen (hdr.Machine.ToString ()))
  out.PrintTwoCols
    "Number of sections:"
    (hdr.NumberOfSections.ToString ())
  out.PrintTwoCols
    "Time date stamp:"
    (hdr.TimeDateStamp.ToString ())
  out.PrintTwoCols
    "Pointer to symbol table:"
    (String.u64ToHex (uint64 hdr.PointerToSymbolTable))
  out.PrintTwoCols
    "Size of optional header:"
    (String.u64ToHex (uint64 hdr.SizeOfOptionalHeader))
  out.PrintTwoCols
    "Characteristics:"
    (String.u64ToHex (uint64 hdr.Characteristics))
  translateChracteristics (uint64 hdr.Characteristics)
  |> List.iter (fun str -> out.PrintTwoCols "" str)

let translateSectionChracteristics chars =
  let enumChars =
    System.Enum.GetValues (typeof<SectionCharacteristics>)
    :?> SectionCharacteristics []
    |> Array.toList
  if chars = uint64 0 then
    [ " - TypeReg" ]
  else
    let rec loop acc chars = function
      | [] -> List.rev acc
      | enumChar :: t ->
        if uint64 enumChar &&& chars = uint64 enumChar
          && (uint64 enumChar <> 0UL) then
          loop ((" - " + enumChar.ToString ()) :: acc) chars t
        else
          loop acc chars t
    loop [] chars enumChars

let dumpSectionHeaders (opts: FileViewerOpts) (file: PEBinFile) =
  let addrColumn = columnWidthOfAddr file |> LeftAligned
  if opts.Verbose then
    let cfg = [ LeftAligned 4; addrColumn; addrColumn; LeftAligned 24
                LeftAligned 8; LeftAligned 8; LeftAligned 8; LeftAligned 8
                LeftAligned 8; LeftAligned 8; LeftAligned 8; LeftAligned 8
                LeftAligned 8 ]
    out.PrintRow (true, cfg,
      [ "Num"; "Start"; "End"; "Name"
        "VirtSize"; "VirtAddr"; "RawSize"; "RawPtr"
        "RelocPtr"; "LineNPtr"; "RelocNum"; "LineNNum"
        "Characteristics" ])
    out.PrintLine "  ---"
    file.PE.SectionHeaders
    |> Array.iteri (fun idx s ->
      let startAddr = file.PE.BaseAddr + uint64 s.VirtualAddress
      let size =
        uint64 (if s.VirtualSize = 0 then s.SizeOfRawData else s.VirtualSize)
      let characteristics = uint64 s.SectionCharacteristics
      out.PrintRow (true, cfg,
        [ String.wrapSqrdBracket (idx.ToString ())
          (Addr.toString file.WordSize startAddr)
          (Addr.toString file.WordSize (startAddr + size - uint64 1))
          normalizeEmpty s.Name
          String.u64ToHex (uint64 s.VirtualSize)
          String.u64ToHex (uint64 s.VirtualAddress)
          String.u64ToHex (uint64 s.SizeOfRawData)
          String.u64ToHex (uint64 s.PointerToRawData)
          String.u64ToHex (uint64 s.PointerToRelocations)
          String.u64ToHex (uint64 s.PointerToLineNumbers)
          s.NumberOfRelocations.ToString ()
          s.NumberOfLineNumbers.ToString ()
          String.u64ToHex characteristics ])
      translateSectionChracteristics characteristics
      |> List.iter (fun str ->
        out.PrintRow (true, cfg, [ ""; ""; ""; ""; ""; ""; ""
                                   ""; ""; ""; ""; ""; str ])))
  else
    let cfg = [ LeftAligned 4; addrColumn; addrColumn; LeftAligned 24 ]
    out.PrintRow (true, cfg, [ "Num"; "Start"; "End"; "Name" ])
    out.PrintLine "  ---"
    file.GetSections ()
    |> Seq.iteri (fun idx s ->
      out.PrintRow (true, cfg,
        [ String.wrapSqrdBracket (idx.ToString ())
          (Addr.toString file.WordSize s.Address)
          (Addr.toString file.WordSize (s.Address + s.Size - uint64 1))
          normalizeEmpty s.Name ]))

let dumpSectionDetails (secname: string) (file: PEBinFile) =
  let idx =
    Array.tryFindIndex (fun (s: SectionHeader) ->
      s.Name = secname) file.PE.SectionHeaders
  match idx with
  | Some idx ->
    let section = file.PE.SectionHeaders[idx]
    let characteristics = uint64 section.SectionCharacteristics
    out.PrintTwoCols
      "Section number:"
      (String.wrapSqrdBracket (idx.ToString ()))
    out.PrintTwoCols
      "Section name:"
      section.Name
    out.PrintTwoCols
      "Virtual size:"
      (String.u64ToHex (uint64 section.VirtualSize))
    out.PrintTwoCols
      "Virtual address:"
      (String.u64ToHex (uint64 section.VirtualAddress))
    out.PrintTwoCols
      "Size of raw data:"
      (String.u64ToHex (uint64 section.SizeOfRawData))
    out.PrintTwoCols
      "Pointer to raw data:"
      (String.u64ToHex (uint64 section.PointerToRawData))
    out.PrintTwoCols
      "Pointer to relocations:"
      (String.u64ToHex (uint64 section.PointerToRelocations))
    out.PrintTwoCols
      "Pointer to line numbers:"
      (String.u64ToHex (uint64 section.PointerToLineNumbers))
    out.PrintTwoCols
      "Number of relocations:"
      (section.NumberOfRelocations.ToString ())
    out.PrintTwoCols
      "Number of line numbers:"
      (section.NumberOfLineNumbers.ToString ())
    out.PrintTwoCols
      "Characteristics:"
      (String.u64ToHex characteristics)
    translateSectionChracteristics characteristics
    |> List.iter (fun str -> out.PrintTwoCols "" str)
  | None -> out.PrintTwoCols "" "Not found."

let printSymbolInfo (file: PEBinFile) (symbols: seq<Symbol>) =
  let addrColumn = columnWidthOfAddr file |> LeftAligned
  let cfg = [ LeftAligned 3; LeftAligned 10
              addrColumn; LeftAligned 50; LeftAligned 15 ]
  out.PrintRow (true, cfg, [ "S/D"; "Kind"; "Address"; "Name"; "Lib Name" ])
  out.PrintLine "  ---"
  symbols
  |> Seq.sortBy (fun s -> s.Name)
  |> Seq.sortBy (fun s -> s.Address)
  |> Seq.sortBy (fun s -> s.Visibility)
  |> Seq.iter (fun s ->
    out.PrintRow (true, cfg,
      [ visibilityString s
        symbolKindString s
        Addr.toString file.WordSize s.Address
        normalizeEmpty s.Name
        (toLibString >> normalizeEmpty) s.LibraryName ]))

let dumpSymbols _ (file: PEBinFile) =
   file.GetSymbols ()
   |> printSymbolInfo file

let dumpRelocs _ (file: PEBinFile) =
  file.GetRelocationSymbols ()
  |> printSymbolInfo file

let dumpFunctions _ (file: PEBinFile) =
  file.GetFunctionSymbols ()
  |> printSymbolInfo file

let inline addrFromRVA baseAddr rva =
  uint64 rva + baseAddr

let dumpImports _ (file: PEBinFile) =
  let cfg = [ LeftAligned 50; LeftAligned 50; LeftAligned 20 ]
  out.PrintRow (true, cfg,
    [ "FunctionName"; "Lib Name"; "TableAddress" ])
  out.PrintLine "  ---"
  file.PE.ImportMap
  |> Map.iter (fun addr info ->
    match info with
    | PE.ImportInfo.ImportByOrdinal (ordinal, dllname) ->
      out.PrintRow (true, cfg,
        [ "#" + ordinal.ToString ()
          dllname
          String.u64ToHex (addrFromRVA file.PE.BaseAddr addr) ])
    | PE.ImportInfo.ImportByName (_, fname, dllname) ->
      out.PrintRow (true, cfg,
        [ fname
          dllname
          String.u64ToHex (addrFromRVA file.PE.BaseAddr addr) ]))

let dumpExports _ (file: PEBinFile) =
  let cfg = [ LeftAligned 45; LeftAligned 20 ]
  out.PrintRow (true, cfg, [ "FunctionName"; "TableAddress" ])
  out.PrintLine "  ---"
  file.PE.ExportMap
  |> Map.iter (fun addr names ->
    let rva = int (addr - file.PE.BaseAddr)
    match file.PE.FindSectionIdxFromRVA rva with
    | -1 -> ()
    | idx ->
      names
      |> List.iter (fun name ->
        out.PrintRow (true, cfg, [ name; String.u64ToHex addr ])))
  out.PrintLine ""
  out.PrintRow (true, cfg, [ "FunctionName"; "ForwardName" ])
  out.PrintLine "  ---"
  file.PE.ForwardMap
  |> Map.iter (fun name (bin, func) ->
    out.PrintRow (true, cfg, [ name; bin + "!" + func ]))

let translateDllChracteristcs chars =
  let enumChars =
    System.Enum.GetValues (typeof<DllCharacteristics>)
    :?> DllCharacteristics []
    |> Array.toList
  let rec loop acc chars = function
    | [] -> List.rev acc
    | enumChar :: tail as all ->
      if uint64 enumChar &&& chars = uint64 enumChar then
        loop ((" - " + enumChar.ToString ()) :: acc) chars tail
      elif uint64 0x0080 &&& chars = uint64 0x0080 then
        loop (" - ForceIntegrity" :: acc) (chars ^^^ uint64 0x0080) all
      elif uint64 0x4000 &&& chars = uint64 0x4000 then
        loop (" - ControlFlowGuard" :: acc) (chars ^^^ uint64 0x4000) all
      else
        loop acc chars tail
  loop [] chars enumChars

let dumpOptionalHeader _ (file: PEBinFile) =
  let hdr = file.PE.PEHeaders.PEHeader
  let imageBase = hdr.ImageBase
  let sizeOfImage = uint64 hdr.SizeOfImage
  let entryPoint = String.u64ToHex (imageBase + uint64 hdr.AddressOfEntryPoint)
  let startImage = String.u64ToHex imageBase
  let endImage = String.u64ToHex (imageBase + sizeOfImage - uint64 1)
  let exportDir = hdr.ExportTableDirectory
  let importDir = hdr.ImportTableDirectory
  let resourceDir = hdr.ResourceTableDirectory
  let exceptionDir = hdr.ExceptionTableDirectory
  let certificateDir = hdr.CertificateTableDirectory
  let baseRelocDir = hdr.BaseRelocationTableDirectory
  let debugDir = hdr.DebugTableDirectory
  let architectureDir = hdr.CopyrightTableDirectory
  let globalPtrDir = hdr.GlobalPointerTableDirectory
  let threadLoStorDir = hdr.ThreadLocalStorageTableDirectory
  let loadConfigDir = hdr.ThreadLocalStorageTableDirectory
  let boundImpDir = hdr.BoundImportTableDirectory
  let importAddrDir = hdr.ImportAddressTableDirectory
  let delayImpDir = hdr.DelayImportTableDirectory
  let comDescDir = hdr.CorHeaderTableDirectory
  out.PrintTwoCols
    "Magic:"
    (String.u64ToHex (uint64 hdr.Magic)
    + String.wrapParen (hdr.Magic.ToString ()))
  out.PrintTwoCols
    "Linker version:"
    (hdr.MajorLinkerVersion.ToString ()
    + "." + hdr.MinorLinkerVersion.ToString ())
  out.PrintTwoCols
    "Size of code:"
    (String.u64ToHex (uint64 hdr.SizeOfCode))
  out.PrintTwoCols
    "Size of initialized data:"
    (String.u64ToHex (uint64 hdr.SizeOfInitializedData))
  out.PrintTwoCols
    "Size of uninitialized data:"
    (String.u64ToHex (uint64 hdr.SizeOfUninitializedData))
  out.PrintTwoCols
    "Entry point:"
    entryPoint
  out.PrintTwoCols
    "Base of code:"
    (String.u64ToHex (uint64 hdr.BaseOfCode))
  out.PrintTwoCols
    "Image base:"
    (String.u64ToHex imageBase
    + String.wrapParen (startImage + " to " + endImage))
  out.PrintTwoCols
    "Section alignment:"
    (String.u64ToHex (uint64 hdr.SectionAlignment))
  out.PrintTwoCols
    "File Alignment:"
    (String.u64ToHex (uint64 hdr.FileAlignment))
  out.PrintTwoCols
    "Operating system version:"
    (hdr.MajorOperatingSystemVersion.ToString ()
     + "." + hdr.MinorOperatingSystemVersion.ToString ())
  out.PrintTwoCols
    "Image version:"
    (hdr.MajorImageVersion.ToString ()
     + "." + hdr.MinorImageVersion.ToString ())
  out.PrintTwoCols
    "Subsystem version:"
    (hdr.MajorSubsystemVersion.ToString ()
     + "." + hdr.MinorSubsystemVersion.ToString ())
  out.PrintTwoCols
    "Size of image:"
    (String.u64ToHex sizeOfImage)
  out.PrintTwoCols
    "Size of headers:"
    (String.u64ToHex (uint64 hdr.SizeOfHeaders))
  out.PrintTwoCols
    "Checksum:"
    (String.u64ToHex (uint64 hdr.CheckSum))
  out.PrintTwoCols
    "Subsystem:"
    (String.u64ToHex (uint64 hdr.Subsystem)
    + String.wrapParen (hdr.Subsystem.ToString ()))
  out.PrintTwoCols
    "DLL characteristics:"
    (String.u64ToHex (uint64 hdr.DllCharacteristics))
  translateDllChracteristcs (uint64 hdr.DllCharacteristics)
  |> List.iter (fun str -> out.PrintTwoCols "" str)
  out.PrintTwoCols
    "Size of stack reserve:"
    (String.u64ToHex (uint64 hdr.SizeOfStackReserve))
  out.PrintTwoCols
    "Size of stack commit:"
    (String.u64ToHex (uint64 hdr.SizeOfStackCommit))
  out.PrintTwoCols
    "Size of heap reserve:"
    (String.u64ToHex (uint64 hdr.SizeOfHeapReserve))
  out.PrintTwoCols
    "Size of heap commit:"
    (String.u64ToHex (uint64 hdr.SizeOfHeapCommit))
  out.PrintTwoCols
    "Loader flags (reserved):"
    "0x0"
  out.PrintTwoCols
    "Number of directories:"
    (hdr.NumberOfRvaAndSizes.ToString ())
  out.PrintTwoCols
    "RVA[size] of Export Table Directory:"
    (String.u64ToHex (uint64 exportDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 exportDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Import Table Directory:"
    (String.u64ToHex (uint64 importDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 importDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Resource Table Directory:"
    (String.u64ToHex (uint64 resourceDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 resourceDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Exception Table Directory:"
    (String.u64ToHex (uint64 exceptionDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 exceptionDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Certificate Table Directory:"
    (String.u64ToHex (uint64 certificateDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 certificateDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Base Relocation Table Directory:"
    (String.u64ToHex (uint64 baseRelocDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 baseRelocDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Debug Table Directory:"
    (String.u64ToHex (uint64 debugDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 debugDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Architecture Table Directory:"
    (String.u64ToHex (uint64 architectureDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 architectureDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Global Pointer Table Directory:"
    (String.u64ToHex (uint64 globalPtrDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 globalPtrDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Thread Storage Table Directory:"
    (String.u64ToHex (uint64 threadLoStorDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 threadLoStorDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Load Configuration Table Directory:"
    (String.u64ToHex (uint64 loadConfigDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 loadConfigDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Bound Import Table Directory:"
    (String.u64ToHex (uint64 boundImpDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 boundImpDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Import Address Table Directory:"
    (String.u64ToHex (uint64 importAddrDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 importAddrDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Delay Import Table Directory:"
    (String.u64ToHex (uint64 delayImpDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 delayImpDir.Size)))
  out.PrintTwoCols
    "RVA[size] of COM Descriptor Table Directory:"
    (String.u64ToHex (uint64 comDescDir.RelativeVirtualAddress)
    + String.wrapSqrdBracket (String.u64ToHex (uint64 comDescDir.Size)))
  out.PrintTwoCols
    "RVA[size] of Reserved Directory:"
    "0x0[0x0]"

let translateCorFlags flags =
  let enumFlags =
    System.Enum.GetValues (typeof<CorFlags>)
    :?> CorFlags []
    |> Array.toList
  let rec loop acc flags = function
    | [] -> List.rev acc
    | enumFlag :: tail ->
      if uint64 enumFlag &&& flags = uint64 enumFlag then
        loop ((" - " + enumFlag.ToString ()) :: acc) flags tail
      else
        loop acc flags tail
  loop [] flags enumFlags

let dumpCLRHeader _ (file: PEBinFile) =
  let hdr = file.PE.PEHeaders.CorHeader
  if isNull hdr then
    out.PrintTwoCols "" "Not found."
  else
    let metaDataDir = hdr.MetadataDirectory
    let resourcesDir = hdr.ResourcesDirectory
    let strongNameSigDir = hdr.StrongNameSignatureDirectory
    let codeMgrTblDir = hdr.CodeManagerTableDirectory
    let vTableFixups = hdr.VtableFixupsDirectory
    let exportAddrTblJmps = hdr.ExportAddressTableJumpsDirectory
    let managedNativeHdr = hdr.ManagedNativeHeaderDirectory
    out.PrintTwoCols
      "Runtime version:"
      (hdr.MajorRuntimeVersion.ToString ()
      + "." + hdr.MinorRuntimeVersion.ToString ())
    out.PrintTwoCols
      "RVA[size] of Meta Data Directory:"
      (String.u64ToHex (uint64 metaDataDir.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 metaDataDir.Size)))
    out.PrintTwoCols
      "Flags:"
      (String.u64ToHex (uint64 hdr.Flags))
    translateCorFlags (uint64 hdr.Flags)
    |> List.iter (fun str -> out.PrintTwoCols "" str)
    out.PrintTwoCols
      "RVA[size] of Resources Directory:"
      (String.u64ToHex (uint64 resourcesDir.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 resourcesDir.Size)))
    out.PrintTwoCols
      "RVA[size] of Strong Name Signature Directory:"
      (String.u64ToHex (uint64 strongNameSigDir.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 strongNameSigDir.Size)))
    out.PrintTwoCols
      "RVA[size] of Code Manager Table Directory:"
      (String.u64ToHex (uint64 codeMgrTblDir.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 codeMgrTblDir.Size)))
    out.PrintTwoCols
      "RVA[size] of VTable Fixups Directory:"
      (String.u64ToHex (uint64 vTableFixups.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 vTableFixups.Size)))
    out.PrintTwoCols
      "RVA[size] of Export Address Table Jumps Directory:"
      (String.u64ToHex (uint64 exportAddrTblJmps.RelativeVirtualAddress)
      + String.wrapSqrdBracket
          (String.u64ToHex (uint64 exportAddrTblJmps.Size)))
    out.PrintTwoCols
      "RVA[size] of Managed Native Header Directory:"
      (String.u64ToHex (uint64 managedNativeHdr.RelativeVirtualAddress)
      + String.wrapSqrdBracket (String.u64ToHex (uint64 managedNativeHdr.Size)))

let dumpDependencies _ (file: PEBinFile) =
  file.GetLinkageTableEntries ()
  |> Seq.map (fun e -> e.LibraryName)
  |> Set.ofSeq
  |> Set.iter (fun s -> out.PrintTwoCols "" s)

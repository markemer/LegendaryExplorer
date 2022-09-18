﻿//#define ISACTDEBUGLOG

using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Gammtek.Extensions.IO;

namespace LegendaryExplorerCore.Sound.ISACT
{
    /// <summary>
    /// Contains methods related to working with ISACT content
    /// </summary>
    public static class ISACTHelper
    {
        /// <summary>
        /// Generates the SoundNodeWaveStreamingData binary using C# implementation
        /// </summary>
        /// <param name="wsdExport"></param>
        /// <param name="icbPath"></param>
        /// <param name="isbPath"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateSoundNodeWaveStreamingDataCS(ExportEntry wsdExport, string icbPath, string isbPath)
        {
            if (icbPath is null || isbPath is null || wsdExport is null)
                throw new Exception("No arguments can be null");

            if (!File.Exists(icbPath))
                throw new Exception($"ICB path not available: {icbPath}");

            if (!File.Exists(isbPath))
                throw new Exception($"ISB path not available: {isbPath}");

            var streamingData = GetStreamingData(icbPath, isbPath);

            MemoryStream ms = new MemoryStream();
            ms.WriteInt32(0);
            streamingData.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            ms.WriteInt32((int)ms.Length - 4);
            wsdExport.WriteBinary(ms.ToArray());

            return null;
        }

        private static MemoryStream GetStreamingData(string icbPath, string isbPath)
        {
            ISACTBankPair ibp = new ISACTBankPair();
            using var icbFs = File.OpenRead(icbPath);
            using var isbFs = File.OpenRead(isbPath);
            ibp.ICBBank = new ISACTBank(icbFs);
            ibp.ISBBank = new ISACTBank(isbFs);

            ibp.ISBBank.StripSamples();
            MemoryStream ms = new MemoryStream(SerializePairedBanks(ibp));
            ms.Position = 0; // Reset to zero
            return ms;
        }

        // Todo: Eventually split to own classes and replace existing ISBank class

        /// <summary>
        /// Input data must start with the integer that matches the data to follow size
        /// </summary>
        /// <param name="binRawData"></param>
        /// <returns></returns>
        public static ISACTBankPair GetPairedBanks(byte[] binRawData)
        {
            ISACTBankPair pair = new ISACTBankPair();
            MemoryStream ms = new MemoryStream(binRawData);
            var isbOffset = ms.ReadInt32(); // We don't read this technically
            while (ms.Position < ms.Length && ms.ReadStringASCII(4) == "RIFF")
            {
                ms.Position -= 4;
                var bank = new ISACTBank(ms);
                switch (bank.BankType)
                {
                    case ISACTBankType.ICB:
                        pair.ICBBank = bank;
                        break;
                    case ISACTBankType.ISB:
                        pair.ISBBank = bank;
                        break;
                    default:
                        throw new Exception($"Unsupported bank type: {bank.BankType}");

                }
            }

            return pair;
        }

        public static byte[] SerializePairedBanks(ISACTBankPair banks)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteInt32(0); // ISB offset
            banks.ICBBank.Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            ms.WriteInt32((int)ms.Length);
            ms.Seek(0, SeekOrigin.End);
            banks.ISBBank.Write(ms);

            // Write size
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Contains a pair of ISACT banks (ICB + ISB)
    /// </summary>
    public class ISACTBankPair
    {
        public ISACTBankPair() { }

        public ISACTBank ICBBank { get; set; }
        public ISACTBank ISBBank { get; set; }
    }

    public enum ISACTBankType
    {
        ICB, // Content Bank
        ISB, // Sample Bank
        SAC // Not used by game
    }

    public class ISACTBank
    {
        public ISACTBankType BankType;
        public long BankRIFFPosition { get; }
        public List<BankChunk> BankChunks { get; set; }

        public ISACTBank(Stream inStream)
        {
            // Stream should start on RIFF
            BankRIFFPosition = inStream.Position;
            var riff = inStream.ReadStringASCII(4);
            if (riff != "RIFF")
                throw new Exception($"Input for bank does not start with RIFF! It starts with {riff}");

            var bankFileLen = inStream.ReadInt32() - 8;
            var bankStartPos = inStream.Position;

            var bankType = inStream.ReadStringASCII(4);
            switch (bankType)
            {
                case "icbf":
                    BankType = ISACTBankType.ICB;
                    break;
                case "isbf":
                    BankType = ISACTBankType.ISB;
                    break;
                case "isac":
                    BankType = ISACTBankType.SAC;
                    break;
                default:
                    throw new Exception($"Unsupported file type: {bankType}");
            }

            BankChunks = new List<BankChunk>();
            var endPos = bankFileLen + bankStartPos;
            while (inStream.Position < endPos)
            {
#if ISACTDEBUGLOG
                Debug.Write($"Reading chunk at 0x{inStream.Position:X8}, endpos: 0x{endPos:X8}");
#endif
                ReadChunk(inStream, BankChunks);
            }
        }

        public static void ReadChunk(Stream inStream, List<BankChunk> chunks)
        {
            var chunkName = inStream.ReadStringASCII(4);
#if ISACTDEBUGLOG
            Debug.WriteLine(chunkName); // finishes previous Debug.Write() line.
#endif
            int chunkLen = 0;
            // Some are subchunks that have a known fixed length. In this instance we don't read the len
            switch (chunkName)
            {

                //case "samp":
                //case "fldr":
                case "snde":
                    break; // Seems to be a marker of some kind for parser
                default:
                    chunkLen = inStream.ReadInt32();
                    break;
            }

            // Parse special types
            switch (chunkName)
            {
                case "snde":
                    chunks.Add(new NameOnlyBankChunk(chunkName)); // These are just markers in files it seems and don't have any actual standalone chunk data
                    break;
                case "cmpi":
                    chunks.Add(new CompressionInfoBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "sinf":
                    chunks.Add(new SampleInfoBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "soff":
                    chunks.Add(new SampleOffsetBankChunk(inStream));
                    break;
                case "chnk":
                    chunks.Add(new ChannelBankChunk(inStream)); // We need to know data for this to replace audio
                    break;
                case "LIST":
                    // These are special listing things.
                    chunks.Add(new ListBankChunk(chunkLen, inStream)); // We need to know data for this to replace audio
                    break;
                case "titl":
                    chunks.Add(new TitleBankChunk(chunkName, chunkLen, inStream)); // Easier to use for debugging
                    break;
                case "isgn":
                    chunks.Add(new GroupBankChunk(chunkName, chunkLen, inStream)); // Easier to use for debugging
                    break;
                case "dtsg":
                case "dtmp":
                case "dsec":
                case "tmcd":
                case "loop":
                case "trks":
                case "geix":
                case "indx":
                case "stri":
                case "msti":
                case "prel":
                case "s3di":
                    chunks.Add(new IntBankChunk(chunkName, inStream)); // size is always 4
                    break;
                case "gbst":
                    chunks.Add(new FloatBankChunk(chunkName, inStream)); // size is always 4
                    break;
                case "sync":
                    chunks.Add(new SyncBankChunk(inStream)); // size is always 4
                    break;
                case "cgvi":
                    chunks.Add(new ContentGlobalVarInfoBankChunk(inStream));
                    break;
                case "dist":
                    chunks.Add(new BufferSoundDistanceBankChunk(inStream));
                    break;
                case "sdst":
                    chunks.Add(new BufferDistanceBankChunk(inStream));
                    break;
                case "cone":
                    chunks.Add(new SoundConeBankChunk(inStream));
                    break;
                case "ctdx":
                    chunks.Add(new ContentIndexBankChunk(chunkLen, inStream));
                    break;
                case "info":
                    chunks.Add(new SoundEventInfoBankChunk(inStream));
                    break;
                default:
                    chunks.Add(new BankChunk(chunkName, chunkLen, inStream));
                    break;
            }
        }

        /// <summary>
        /// Writes this bank's info out to the memory stream
        /// </summary>
        /// <param name="ms"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Write(Stream ms)
        {
            ms.WriteStringASCII("RIFF");
            var riffLenPos = ms.Position;
            ms.WriteInt32(0); // length placeholder

            ms.WriteStringASCII(BankType.ToString().ToLower() + "f"); //icbf isbf

            foreach (var bank in BankChunks)
            {
                bank.Write(ms);
            }

            // Write size
            ms.Seek(riffLenPos, SeekOrigin.Begin);
            ms.WriteInt32((int)(ms.Length - riffLenPos - 4));
            ms.Seek(0, SeekOrigin.End);
        }

        /// <summary>
        /// Do not use this for serializing as it will contain enclosing chunks of chunks
        /// </summary>
        /// <returns></returns>
        public List<BankChunk> GetAllBankChunks()
        {
            List<BankChunk> chunks = new List<BankChunk>();
            foreach (var chunk in BankChunks)
            {
                chunks.Add(chunk); // Technically if this has subchunks it will also include it
                chunks.AddRange(chunk.GetAllSubChunks());
            }

            foreach (var c in chunks)
            {
#if ISACTDEBUGLOG
                Debug.WriteLine(c.ChunkName);
#endif
            }

            return chunks;
        }

        public void StripSamples()
        {
            // Replace data segment with soff
            stripSubchunks(BankChunks);
        }

        private void stripSubchunks(List<BankChunk> chunks)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                var bc = chunks[i];
                if (bc.ChunkName == "data")
                {
                    chunks[i] = new SampleOffsetBankChunk() { SampleOffset = (uint)bc.ChunkDataStartOffset };  // Points directly at OggS
                }
                if (bc.SubChunks.Any())
                    stripSubchunks(bc.SubChunks);
            }
        }
    }

    public class SyncBankChunk : BankChunk
    {
        public enum ISACTSyncStart
        {
            IMMEDIATE,
            CLOCK,
            BEAT,
            BAR,
            MARKER,
            COUNT
        }

        public ISACTSyncStart SyncStart { get; set; }
        public int Multiple { get; set; }


        public SyncBankChunk(Stream inStream)
        {
            ChunkName = "sync";
            ChunkDataStartOffset = inStream.Position;
            SyncStart = (ISACTSyncStart)inStream.ReadInt32();
            Multiple = inStream.ReadInt32();
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(8); // size
            outStream.WriteInt32((int)SyncStart);
            outStream.WriteInt32(Multiple);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Sync Start: {SyncStart}, Multiple: {Multiple}";
        }
    }

    public class ChannelBankChunk : BankChunk
    {
        public int ChannelCount;
        public ChannelBankChunk(Stream inStream)
        {
            ChunkName = "chnk";
            ChannelCount = inStream.ReadInt32();
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // size
            outStream.WriteInt32(ChannelCount);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName} Channel Count: {ChannelCount}";
        }
    }

    [DebuggerDisplay("ListBankChunk of type {ObjectType} with {SubChunks.Count} sub chunks")]
    public class ListBankChunk : BankChunk
    {
        public string ObjectType; // What this LIST object is
        public ListBankChunk(int chunkLen, Stream inStream)
        {
            ChunkDataStartOffset = inStream.Position;

            ChunkName = "LIST";
            var startPos = inStream.Position;
            var endPos = chunkLen + startPos - 4; // The length of LIST for some reason is +4.

            ObjectType = inStream.ReadStringASCII(4);

            while (inStream.Position < endPos)
            {
#if ISACTDEBUGLOG
                Debug.Write($"Reading ListBank SubChunk at 0x{inStream.Position:X8}, endpos: 0x{endPos:X8}: ");
#endif
                ISACTBank.ReadChunk(inStream, SubChunks);

                if (inStream.Position + 1 == endPos)
                    inStream.ReadByte(); // Even boundary
            }
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            var lenPos = outStream.Position;
            outStream.WriteInt32(0);
            outStream.WriteStringASCII(ObjectType);
            foreach (var chunk in SubChunks)
            {
                chunk.Write(outStream);
            }

            outStream.Seek(lenPos, SeekOrigin.Begin);
            var len = outStream.Length - lenPos - 4;
            outStream.WriteInt32((int)len);
            outStream.Seek(0, SeekOrigin.End);

            if ((len & 1L) != 0L)
                outStream.WriteByte(0); // Even boundary

        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName} Object ({SubChunks.Count} subitems)";
        }
    }

    public class NameOnlyBankChunk : BankChunk
    {
        public NameOnlyBankChunk(string chunkName)
        {
            ChunkName = chunkName;
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
        }
    }

    /// <summary>
    /// A generic 4 byte name 4 byte length block that is not parsed.
    /// </summary>
    [DebuggerDisplay("BankChunk {ChunkName}")]

    public class BankChunk
    {
        public string ChunkName;
        public byte[] RawData;

        /// <summary>
        /// Some chunks have subchunks
        /// </summary>
        public List<BankChunk> SubChunks = new List<BankChunk>(0);


        /// <summary>
        /// The offset in which this bank's data starts - + 8 after header & size. For name only this should not be used.
        /// </summary>
        public long ChunkDataStartOffset { get; init; }

        public BankChunk() { }
        public BankChunk(string chunkName, int chunkLen, Stream inStream)
        {
#if DEBUG && ISACTDEBUGLOG
            if (chunkName == "data")
            {
                Debug.WriteLine("FOUND 'data'!");
            }
#endif
            ChunkDataStartOffset = inStream.Position;
            ChunkName = chunkName;
            RawData = inStream.ReadToBuffer(chunkLen);
            //has to be 2-byte aligned
            if (chunkLen % 2 == 1)
            {
                inStream.ReadByte();
            }
        }


        public virtual void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(RawData.Length);
            outStream.Write(RawData);
            //has to be 2-byte aligned
            if (RawData.Length % 2 == 1)
            {
                outStream.WriteByte(0);
            }
        }

        public IEnumerable<BankChunk> GetAllSubChunks()
        {
            if (SubChunks.Count == 0) return Array.Empty<BankChunk>();
            List<BankChunk> returnList = new List<BankChunk>();
            returnList.AddRange(SubChunks);
            returnList.AddRange(SubChunks.SelectMany(x => x.GetAllSubChunks()));
            return returnList;
        }

        public virtual string ToChunkDisplay()
        {
            if (RawData == null)
            {
                return ChunkName;
            }

            return $"{ChunkName} ({RawData.Length} bytes)";
        }
    }

    [DebuggerDisplay("TitleBankChunk: {Value}")]
    public class TitleBankChunk : BankChunk
    {
        public string Value;
        public TitleBankChunk(string chunkName, int chunkLen, Stream inStream) : base(chunkName, chunkLen, inStream)
        {
            Value = Encoding.Unicode.GetString(RawData, 0, chunkLen - 2); //exclude null terminator
        }

        public override string ToChunkDisplay()
        {
            if (RawData == null)
            {
                return ChunkName;
            }

            return $"{ChunkName}: {Value}";
        }
    }

    [DebuggerDisplay("GroupBankChunk: {Value}")]
    public class GroupBankChunk : BankChunk
    {
        public string Value;
        public GroupBankChunk(string chunkName, int chunkLen, Stream inStream) : base(chunkName, chunkLen, inStream)
        {
            Value = Encoding.Unicode.GetString(RawData, 0, chunkLen - 2); //exclude null terminator
        }

        public override string ToChunkDisplay()
        {
            if (RawData == null)
            {
                return ChunkName;
            }

            return $"{ChunkName}: Group Name: {Value}";
        }
    }

    /// <summary>
    /// ISACT Compression Info bank chunk
    /// </summary>
    public class CompressionInfoBankChunk : BankChunk
    {
        /// <summary>
        /// The type of audio that is stored
        /// </summary>
        public enum ISACTCompressionFormat
        {
            PCM = 0,
            IMA4ADPCM = 1,
            OGGVORBIS = 2,
            WMA = 3,
            XMA = 4,
            MSMP3 = 5,
            MSADPCM = 6,
            MSPCMBIG = 7 // Big Endian
        }

        /// <summary>
        /// Should be same as Target in processed ISB
        /// </summary>
        public ISACTCompressionFormat CurrentFormat;
        /// <summary>
        /// Should be same as current in processed ISB
        /// </summary>
        public ISACTCompressionFormat TargetFormat;

        /// <summary>
        /// Size of the compressed data block
        /// </summary>
        public int TotalSize;
        /// <summary>
        /// Not sure
        /// </summary>
        public int PacketSize;
        /// <summary>
        /// Ratio chosen to compress data with
        /// </summary>
        public float CompressionRatio;
        /// <summary>
        /// Not sure
        /// </summary>
        public float CompressionQuality;
        public CompressionInfoBankChunk(Stream inStream)
        {
            ChunkDataStartOffset = inStream.Position;


            ChunkName = @"cmpi"; // We know the chunk name and len already so we don't need this.
            CurrentFormat = (ISACTCompressionFormat)inStream.ReadInt32();
            TargetFormat = (ISACTCompressionFormat)inStream.ReadInt32();
            TotalSize = inStream.ReadInt32();
            PacketSize = inStream.ReadInt32();
            CompressionRatio = inStream.ReadFloat();
            CompressionQuality = inStream.ReadFloat(); // Seems to not always be present
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(24); // Fixed size
            outStream.WriteInt32((int)CurrentFormat);
            outStream.WriteInt32((int)TargetFormat);
            outStream.WriteInt32(TotalSize);
            outStream.WriteInt32(PacketSize);
            outStream.WriteFloat(CompressionRatio);
            outStream.WriteFloat(CompressionQuality);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Compression Info\nCurrent Format: {CurrentFormat}\nTarget Format: {TargetFormat}\nTotal Size: {TotalSize}\nStreaming Packet Size: {PacketSize}\nCompression Ratio: {CompressionRatio}\nCompression Quality: {CompressionQuality}";
        }
    }

    /// <summary>
    /// ISACT info about the sample data
    /// </summary>
    public class SampleInfoBankChunk : BankChunk
    {
        public int BufferOffset;
        public int TimeLength;
        public int SamplesPerSecond;
        public int ByteLength;
        public ushort BitsPerSample;

        //Content of Padding doesn't matter, but we save it so that we can do an identical reserialization (Its value is inconsistent)
        private ushort Padding;
        public SampleInfoBankChunk(Stream inStream)
        {
            ChunkDataStartOffset = inStream.Position;

            ChunkName = @"sinf"; // We know the chunk name and len already so we don't need this.
            BufferOffset = inStream.ReadInt32();
            TimeLength = inStream.ReadInt32();
            SamplesPerSecond = inStream.ReadInt32();
            ByteLength = inStream.ReadInt32();
            BitsPerSample = inStream.ReadUInt16();
            Padding = inStream.ReadUInt16(); // Align 2 since struct size is 20 (align 4)
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(20); // Fixed size
            outStream.WriteInt32(BufferOffset);
            outStream.WriteInt32(TimeLength);
            outStream.WriteInt32(SamplesPerSecond);
            outStream.WriteInt32(ByteLength);
            outStream.WriteUInt16(BitsPerSample);
            outStream.WriteUInt16(Padding); // Align to 4 byte boundary.
        }

        public override string ToChunkDisplay()
        {
            return
                $"{ChunkName}: Sample Info\nBuffer Offset: {BufferOffset}\nTime Length: {TimeLength}\nSamples Per Second: {SamplesPerSecond}\nByte Length: {ByteLength}\nBits Per Sample: {BitsPerSample}";
        }
    }

    /// <summary>
    /// BioWare-specific: Sample offset in external ISB
    /// </summary>
    public class SampleOffsetBankChunk : BankChunk
    {
        public uint SampleOffset { get; set; }

        public SampleOffsetBankChunk(Stream inStream) : this()
        {
            ChunkDataStartOffset = inStream.Position;
            SampleOffset = inStream.ReadUInt32(); // Align 2 since struct size is 20 (align 4)
        }

        public SampleOffsetBankChunk()
        {
            ChunkName = @"soff"; // We know the chunk name and len already so we don't need this.
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // Fixed size
            outStream.WriteUInt32(SampleOffset);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: External ISB Sample Data Offset: 0x{SampleOffset:X8}";
        }
    }

    public class ContentGlobalVarInfoBankChunk : BankChunk
    {
        public int StartVarIndex { get; set; }
        public int StartStateIndex { get; set; }
        public int StopVarIndex { get; set; }
        public int StopStateIndex { get; set; }
        public int Flags { get; set; }

        public ContentGlobalVarInfoBankChunk(Stream inStream) : this()
        {
            ChunkDataStartOffset = inStream.Position;
            StartVarIndex = inStream.ReadInt32();
            StartStateIndex = inStream.ReadInt32();
            StopVarIndex = inStream.ReadInt32();
            StopStateIndex = inStream.ReadInt32();

            // This is optional; if size of chunk is not 20 then this field is not read.
            Flags = inStream.ReadInt32();
        }

        public ContentGlobalVarInfoBankChunk()
        {
            ChunkName = "cgvi";
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(20); // Fixed size
            outStream.WriteInt32(StartVarIndex);
            outStream.WriteInt32(StartStateIndex);
            outStream.WriteInt32(StopVarIndex);
            outStream.WriteInt32(StopStateIndex);
            outStream.WriteInt32(Flags);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Content Global Var Info\n\tStartVarIndex: {StartVarIndex}\n\tStartStateIndex: {StartStateIndex}\n\tStopVarIndex: {StopVarIndex}\n\tStopStateIndex: {StopStateIndex}\n\tFlags: {Flags}";
        }
    }

    /// <summary>
    /// Holds only an integer value
    /// </summary>
    public class IntBankChunk : BankChunk
    {
        public int Value { get; set; }
        public string HumanName { get; }

        public IntBankChunk(string chunkName, Stream inStream)
        {
            ChunkName = chunkName;
            ChunkDataStartOffset = inStream.Position;
            Value = inStream.ReadInt32();

            // Configure human name (if any defined)
            switch (chunkName)
            {
                case "dtsg":
                    HumanName = "Default Time";
                    break;
                case "dtmp":
                    HumanName = "Default Temp";
                    break;
                case "dsec":
                    HumanName = "Default Section";
                    break;
                case "tmcd":
                    HumanName = "Default Code";
                    break;
                case "loop":
                    HumanName = "Loop Count";
                    break;
                case "trks":
                    HumanName = "Track Count";
                    break;
                case "geix":
                    HumanName = "Global Effect Index";
                    break;
                case "indx":
                    HumanName = "Resource Index";
                    break;
                case "stri":
                    HumanName = "Streaming Info (Packet Size)";
                    break;
                case "msti":
                    HumanName = "Memory Streaming Info (Time Length)";
                    break;
                case "prel":
                    HumanName = "Preload Stream Packet";
                    break;
                case "s3di":
                    HumanName = "Sample 3D Info (Buffer Index)";
                    break;


            }
        }

        public IntBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // Fixed size
            outStream.WriteInt32(Value);
        }

        public override string ToChunkDisplay()
        {
            if (HumanName != null)
            {
                return $"{ChunkName}: {HumanName}: {Value}";
            }
            return $"{ChunkName}: {Value}";
        }
    }

    public class FloatBankChunk : BankChunk
    {
        public float Value { get; set; }
        public string HumanName { get; }

        public FloatBankChunk(string chunkName, Stream inStream)
        {
            ChunkName = chunkName;
            ChunkDataStartOffset = inStream.Position;
            Value = inStream.ReadFloat();

            // Configure human name (if any defined)
            switch (chunkName)
            {
                case "gbst":
                    HumanName = "Gain Boost";
                    break;


            }
        }

        public FloatBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(4); // Fixed size
            outStream.WriteFloat(Value);
        }

        public override string ToChunkDisplay()
        {
            if (HumanName != null)
            {
                return $"{ChunkName}: {HumanName}: {Value}";
            }
            return $"{ChunkName}: {Value}";
        }
    }

    // Not legacy
    public class BufferDistanceBankChunk : BankChunk
    {
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public float DistanceLevel { get; set; }
        public uint DistanceFlags { get; }

        /// <summary>
        /// Not legacy sdst version
        /// </summary>
        /// <param name="inStream"></param>
        public BufferDistanceBankChunk(Stream inStream)
        {
            ChunkName = "sdst";
            ChunkDataStartOffset = inStream.Position;
            MinDistance = inStream.ReadFloat();
            MaxDistance = inStream.ReadFloat();
            DistanceLevel = inStream.ReadFloat();
            DistanceFlags = inStream.ReadUInt32();
        }

        public BufferDistanceBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(16); // Fixed size
            outStream.WriteFloat(MinDistance);
            outStream.WriteFloat(MaxDistance);
            outStream.WriteFloat(DistanceLevel);
            outStream.WriteUInt32(DistanceFlags);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Buffer Sound Distance\n\tDistance Size: {MinDistance}\n\tDistance Level: {MaxDistance}\n\tDistance Modifier: {DistanceLevel}\n\tDistance Flags: {DistanceFlags}";
        }
    }

    public class BufferSoundDistanceBankChunk : BankChunk
    {
        public float DistanceSize { get; set; }
        public float DistanceLevel { get; set; }
        public float DistanceModifier { get; set; }
        public uint DistanceFlags { get; }

        /// <summary>
        /// Legacy dist version
        /// </summary>
        /// <param name="inStream"></param>
        public BufferSoundDistanceBankChunk(Stream inStream)
        {
            ChunkName = "dist";
            ChunkDataStartOffset = inStream.Position;
            DistanceSize = inStream.ReadFloat();
            DistanceLevel = inStream.ReadFloat();
            DistanceModifier = inStream.ReadFloat();
            DistanceFlags = inStream.ReadUInt32();
        }

        public BufferSoundDistanceBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(16); // Fixed size
            outStream.WriteFloat(DistanceSize);
            outStream.WriteFloat(DistanceLevel);
            outStream.WriteFloat(DistanceModifier);
            outStream.WriteUInt32(DistanceFlags);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Buffer Sound Distance (Legacy)\n\tDistance Size: {DistanceSize}\n\tDistance Level: {DistanceLevel}\n\tDistance Modifier: {DistanceModifier}\n\tDistance Flags: {DistanceFlags}";
        }
    }

    public class SoundConeBankChunk : BankChunk
    {
        public int InsideConeAngle { get; set; }
        public int OutsideConeAngle { get; set; }
        public int OutsideConeLevel { get; set; }
        public int OutsideConeHFLevel { get; set; }
        public uint ConeFlags { get; set; }

        public SoundConeBankChunk(Stream inStream)
        {
            ChunkName = "cone";
            ChunkDataStartOffset = inStream.Position;
            InsideConeAngle = inStream.ReadInt32();
            OutsideConeAngle = inStream.ReadInt32();
            OutsideConeLevel = inStream.ReadInt32();
            OutsideConeHFLevel = inStream.ReadInt32();
            ConeFlags = inStream.ReadUInt32();
        }

        public SoundConeBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(20); // Fixed size
            outStream.WriteInt32(InsideConeAngle);
            outStream.WriteInt32(OutsideConeAngle);
            outStream.WriteInt32(OutsideConeLevel);
            outStream.WriteInt32(OutsideConeHFLevel);
            outStream.WriteUInt32(ConeFlags);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Buffer Sound Cone\n\tInside Cone Angle: {InsideConeAngle}\n\tOutside Cone Angle: {OutsideConeAngle}\n\tOutside Cone Level: {OutsideConeLevel}\n\tOutside Cone HF Level: {OutsideConeHFLevel}\n\tCone Flags: {ConeFlags}";
        }
    }

    public class SoundEventInfoBankChunk : BankChunk
    {
        public enum ISACTSEEventSelection
        {
            USE_EVS_ORDER, // 0
            USE_EVS_CHANCE // 1
        }

        public ISACTSEEventSelection EventSelection { get; set; }
        public uint DefaultChance { get; set; }
        public int EqualChance { get; set; }
        public uint Flags { get; set; }
        public int ResetParamsOnLoop { get; set; }
        public int ResetSampleOnLoop { get; set; }

        public SoundEventInfoBankChunk(Stream inStream)
        {
            ChunkName = "info";
            ChunkDataStartOffset = inStream.Position;
            EventSelection = (ISACTSEEventSelection) inStream.ReadUInt32();
            DefaultChance = inStream.ReadUInt32();
            EqualChance = inStream.ReadInt32();
            Flags = inStream.ReadUInt32();
            ResetParamsOnLoop = inStream.ReadInt32();
            ResetSampleOnLoop = inStream.ReadInt32();
        }

        public SoundEventInfoBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            outStream.WriteInt32(24); // Fixed size
            outStream.WriteInt32((int)EventSelection);
            outStream.WriteUInt32(DefaultChance);
            outStream.WriteInt32(EqualChance);
            outStream.WriteUInt32(Flags);
            outStream.WriteInt32(ResetParamsOnLoop);
            outStream.WriteInt32(ResetSampleOnLoop);
        }

        public override string ToChunkDisplay()
        {
            return $"{ChunkName}: Sound Event Info\n\tEvent Selection: {EventSelection}\n\tDefault Chance: {DefaultChance}\n\tEqual Chance: {EqualChance}\n\tFlags: {Flags}\n\tReset Params On Loop: {ResetParamsOnLoop}\n\tReset Sample On Loop: {ResetSampleOnLoop}";
        }
    }

    class IndexPage
    {
        public uint EntryCount { get; set; }
        public IndexEntry[] IndexEntry;
        // public IndexPage NextPage;
    }

    class IndexEntry
    {
        public string Title { get; set; }
        public string ObjectType { get; set; }
        public uint ObjectIndex { get; set; }
    }

    public class ContentIndexBankChunk : BankChunk
    {
        private List<IndexPage> IndexPages;
        public ContentIndexBankChunk(int dataSize, Stream inStream)
        {
            ChunkName = "ctdx";
            ChunkDataStartOffset = inStream.Position;

            int dataAmountRead = 0;
            while (dataAmountRead < dataSize)
            {
                IndexPages ??= new List<IndexPage>();
                IndexPage Page = new IndexPage();
                var pageEntryCount = inStream.ReadInt32();
                Page.IndexEntry = new IndexEntry[pageEntryCount];
                for (int i = 0; i < pageEntryCount; i++)
                {
                    Page.IndexEntry[i] = new IndexEntry();
                    var endPos = inStream.Position + 0x100; // The string is an array of 128 chars. So it is 0x100 shorts. We read it as null string and skip the garbage data.
                    Page.IndexEntry[i].Title = inStream.ReadStringUnicodeNull();
                    inStream.Position = endPos;
                    Page.IndexEntry[i].ObjectType = inStream.ReadStringASCII(4); // This is an ascii string.
                    Page.IndexEntry[i].ObjectIndex = inStream.ReadUInt32();
                }
                IndexPages.Add(Page);

                // 4 (page count) + (0x100 + 0x8) (page entries)
                dataAmountRead += 4 + (264 * pageEntryCount);
            }
        }

        public ContentIndexBankChunk()
        {
        }

        public override void Write(Stream outStream)
        {
            outStream.WriteStringASCII(ChunkName);
            var sizePos = outStream.Position;
            outStream.WriteInt32(0); // placeholder

            foreach (var p in IndexPages)
            {
                outStream.WriteInt32(p.IndexEntry.Length);
                foreach (var entry in p.IndexEntry)
                {
                    var endPos = outStream.Position + 0x100;
                    outStream.WriteStringUnicodeNull(entry.Title);
                    while (outStream.Position < endPos)
                    {
                        outStream.WriteByte(0xCC); // Garbage alignment data.
                    }
                    outStream.WriteStringASCII(entry.ObjectType);
                    outStream.WriteUInt32(entry.ObjectIndex);
                }
            }

            // Write out the length.
            var finishPos = outStream.Position;
            outStream.Position = sizePos;
            outStream.WriteInt32((int)(finishPos - sizePos - 4)); // -4 to remove the size itself.
            outStream.Position = finishPos;
        }

        public override string ToChunkDisplay()
        {
            var str = $"{ChunkName}: Content Index ({IndexPages?.Count ?? 0} index pages)";
            foreach (var ip in IndexPages)
            {
                str += $"\n\tIndex Page ({ip.IndexEntry.Length} indexes)";
                foreach (var ie in ip.IndexEntry)
                {
                    str += $"\n\t\tIndex Entry {ie.Title}, type {ie.ObjectType}, index {ie.ObjectIndex}";
                }
            }

            return str;
        }
    }
}

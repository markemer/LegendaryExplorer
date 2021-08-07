﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UState : UStruct
    {
        public ulong ProbeMask; 
        public ulong IgnoreMask;
        public ushort LabelTableOffset;
        public EStateFlags StateFlags;
        public OrderedMultiValueDictionary<NameReference, UIndex> LocalFunctionMap;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ProbeMask);
            sc.Serialize(ref IgnoreMask);
            sc.Serialize(ref LabelTableOffset);
            sc.Serialize(ref StateFlags);
            sc.Serialize(ref LocalFunctionMap, SCExt.Serialize, SCExt.Serialize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.AddRange(LocalFunctionMap.Select((kvp, i) => (kvp.Value, $"LocalFunctions[{i}]")));
            return uIndices;
        }
        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(LocalFunctionMap.Select((kvp, i) => (kvp.Key, $"LocalFunctions[{i}]")));

            return names;
        }
    }

    public struct LabelTableEntry
    {
        public NameReference NameRef;
        public uint Offset; // standard bytescript MemOffs
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref EStateFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (EStateFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}
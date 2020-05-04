﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public abstract class UStruct : UField
    {
        public UIndex Children;
        private int Line; //ME1/ME2
        private int TextPos; //ME1/ME2
        public int ScriptBytecodeSize; //ME3
        public int ScriptStorageSize;
        public byte[] ScriptBytes;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.Game <= MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref Children);
            if (sc.Game <= MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref Line);
                sc.Serialize(ref TextPos);
            }

            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref ScriptBytecodeSize);
            }
            if (sc.IsSaving)
            {
                ScriptStorageSize = ScriptBytes.Length;
            }
            sc.Serialize(ref ScriptStorageSize);
            sc.Serialize(ref ScriptBytes, ScriptStorageSize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Children, "ChildListStart"));

            if (Export.ClassName == "Function")
            {
                if (Export.Game == MEGame.ME3)
                {
                    try
                    {
                        var func = UE3FunctionReader.ReadFunction(Export);
                        func.Decompile(new TextBuilder(), false); //parse bytecode
                        var entryRefs = func.EntryReferences;
                        uIndices.AddRange(entryRefs.Select(x =>
                            (new UIndex(x.Value.UIndex), "Reference inside of function")));

                        (List<Token> tokens, _) = Bytecode.ParseBytecode(ScriptBytes, Export);
                        foreach (var t in tokens)
                        {
                            {
                                var refs = t.inPackageReferences.Where(x => x.type == Token.INPACKAGEREFTYPE_ENTRY)
                                    .Select(x => x.value);
                                uIndices.AddRange(refs.Select(x => (new UIndex(x), "Reference inside of function")));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error decompiling function " + Export.FullPath);
                    }
                }
                else
                {
                    try
                    {
                        var func = UE3FunctionReader.ReadFunction(Export);
                        func.Decompile(new TextBuilder(), false); //parse bytecode
                        var entryRefs = func.EntryReferences;
                        uIndices.AddRange(entryRefs.Select(x =>
                            (new UIndex(x.Value.UIndex), "Reference inside of function")));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error decompiling function " + Export.FullPath);
                    }
                }
            }

            return uIndices;
        }
    }
}

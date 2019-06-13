﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using ME3Explorer.Packages;
using ME2Explorer.Unreal;
using ME1Explorer.Unreal;
using System.Diagnostics;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        public static bool isImmutable(string structType, MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.isImmutableStruct(structType);
                default:
                    return false;
            }
        }

        public static bool inheritsFrom(this IEntry entry, string baseClass)
        {
            switch (entry.FileRef.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.inheritsFrom(entry, baseClass);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.inheritsFrom(entry, baseClass);
                case MEGame.ME3:
                case MEGame.UDK: //use me3?
                    return ME3UnrealObjectInfo.inheritsFrom(entry, baseClass);
                default:
                    return false;
            }
        }

        public static string GetEnumType(MEGame game, string propName, string typeName, ClassInfo nonVanillaClassInfo = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case MEGame.ME3:
                case MEGame.UDK:
                    var enumType = ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName);
                    if (enumType == null && game == MEGame.UDK)
                    {
                        enumType = UDKUnrealObjectInfo.getEnumTypefromProp(typeName, propName);
                    }

                    return enumType;
            }
            return null;
        }

        public static List<NameReference> GetEnumValues(MEGame game, string enumName, bool includeNone)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, includeNone);
            }
            return null;
        }

        /// <summary>
        /// Gets the type of an array
        /// </summary>
        /// <param name="game">What game we are looking info for</param>
        /// <param name="propName">Name of the array property</param>
        /// <param name="className">Name of the class that should contain the information. If contained in a struct, this will be the name of the struct type</param>
        /// <param name="parsingEntry">Entry that is being parsed. Used for dynamic lookup if it's not in the DB</param>
        /// <returns></returns>
        public static ArrayType GetArrayType(MEGame game, string propName, string className, IEntry parsingEntry = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
                case MEGame.ME2:
                    var res2 = ME2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res2 == ArrayType.Int && ME2UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        ME2UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                        Debug.WriteLine("[ME2] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                    }
#endif
                    return res2;
                case MEGame.ME3:
                case MEGame.UDK:
                    var res = ME3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res == ArrayType.Int && ME3UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        if (game == MEGame.UDK)
                        {
                            var ures = UDKUnrealObjectInfo.getArrayType(className, propName: propName, export: parsingEntry as IExportEntry);
                            if (ures == ArrayType.Int && UDKUnrealObjectInfo.ArrayTypeLookupJustFailed)
                            {
                                Debug.WriteLine("[UDK] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                                UDKUnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                            }
                            else
                            {
                                return ures;
                            }
                        }
                        Debug.WriteLine("[ME3] Array type lookup failed for " + propName + " in class " + className + " in export " + parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex));
                        ME3UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                    }
#endif
                    return res;
            }
            return ArrayType.Int;
        }

        /// <summary>
        /// Gets property information for a property by name & containing class or struct name
        /// </summary>
        /// <param name="game">Game to lookup informatino from</param>
        /// <param name="propname">Name of property information to look up</param>
        /// <param name="containingClassOrStructName">Name of containing class or struct name</param>
        /// <param name="nonVanillaClassInfo">Dynamically built property info</param>
        /// <returns></returns>
        public static PropertyInfo GetPropertyInfo(MEGame game, string propname, string containingClassOrStructName, ClassInfo nonVanillaClassInfo = null)
        {
            bool inStruct = false;
            PropertyInfo p = null;
            switch (game)
            {
                case MEGame.ME1:
                    p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME2:
                    p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME3:
                case MEGame.UDK:
                    p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    if (p == null && game == MEGame.UDK)
                    {
                        p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    }
                    break;
            }
            if (p == null)
            {
                inStruct = true;
                switch (game)
                {
                    case MEGame.ME1:
                        p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME2:
                        p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME3:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.UDK:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        if (p == null && game == MEGame.UDK)
                        {
                            p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                        }
                        break;
                }
            }
            return p;
        }

        /// <summary>
        /// Gets the default values for a struct
        /// </summary>
        /// <param name="game">Game to pull info from</param>
        /// <param name="typeName">Struct type name</param>
        /// <param name="stripTransients">Strip transients from the struct</param>
        /// <returns></returns>
        internal static PropertyCollection getDefaultStructValue(MEGame game, string typeName, bool stripTransients)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
            }
            return null;
        }
    }

    public static class ME3UnrealObjectInfo
    {

        public class SequenceObjectInfo
        {
            public List<string> inputLinks;

            public SequenceObjectInfo()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<NameReference>> Enums = new Dictionary<string, List<NameReference>>();

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        private static readonly string jsonPath = Path.Combine(App.ExecFolder, "ME3ObjectInfo.json");

        public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static void loadfromJSON()
        {

            try
            {
                if (File.Exists(jsonPath))
                {
                    string raw = File.ReadAllText(jsonPath);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Classes, Structs, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string objectName)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (SequenceObjects.ContainsKey(objectName))
            {
                if (SequenceObjects[objectName].inputLinks != null && SequenceObjects[objectName].inputLinks.Count > 0)
                {
                    return SequenceObjects[objectName];
                }
                else
                {
                    return getSequenceObjectInfo(Classes[objectName].baseClass);
                }
            }
            return null;
        }

        public static string getEnumTypefromProp(string className, string propName)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            return p?.reference;
        }

        public static List<NameReference> getEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                var values = new List<NameReference>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, IExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false, containingExport: export);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true, containingExport: export);
            }
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.ClassParent;
                    p = getPropertyInfo(className, propName, false, currentInfo, containingExport: export);
                    if (p == null)
                    {
                        p = getPropertyInfo(className, propName, true, currentInfo, containingExport: export);
                    }
                }
            }
            return getArrayType(p);
        }

#if DEBUG
        public static bool ArrayTypeLookupJustFailed;
#endif

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                else if (Enums.ContainsKey(p.reference))
                {
                    return ArrayType.Enum;
                }
                else if (p.reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                else if (p.reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                else if (p.reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                else if (p.reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                else if (p.reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                else if (Structs.ContainsKey(p.reference))
                {
                    return ArrayType.Struct;
                }
                else
                {
                    return ArrayType.Object;
                }
            }
            else
            {
#if DEBUG
                ArrayTypeLookupJustFailed = true;
#endif
                Debug.WriteLine("ME3 Array type lookup failed due to no info provided, defaulting to int");
                if (ME3Explorer.Properties.Settings.Default.PropertyParsingME3UnknownArrayAsObject) return ArrayType.Object;
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, IExportEntry containingExport = null)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            bool infoExists = temp.TryGetValue(className, out ClassInfo info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    return propInfo;
                }
                //look in structs
                else if (inStruct)
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true, nonVanillaClassInfo, reSearch: true);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo, reSearch: true);
                    if (val != null)
                    {
                        return val;
                    }
                }
                else
                {
                    //Baseclass may be modified as well...
                    if (containingExport != null && containingExport.idxClassParent > 0)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        IExportEntry parentExport = containingExport.FileRef.getUExport(containingExport.idxClassParent);
                        return getPropertyInfo(parentExport.ClassParent, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients)
        {
            bool isImmutable = UnrealObjectInfo.isImmutable(className, MEGame.ME3);
            if (Structs.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                try
                {
                    PropertyCollection structProps = new PropertyCollection();
                    ClassInfo tempInfo = info;
                    while (tempInfo != null)
                    {
                        foreach ((string propName, PropertyInfo propInfo) in tempInfo.properties)
                        {
                            if (stripTransients && propInfo.transient)
                            {
                                continue;
                            }
                            if (getDefaultProperty(propName, propInfo, stripTransients, isImmutable) is UProperty uProp)
                            {
                                structProps.Add(uProp);
                            }
                        }
                        if (!Structs.TryGetValue(tempInfo.baseClass, out tempInfo))
                        {
                            tempInfo = null;
                        }
                    }
                    structProps.Add(new NoneProperty());
                    
                    string filepath = Path.Combine(ME3Directory.gamePath, "BIOGame", info.pccPath);
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    if (File.Exists(filepath))
                    {
                        using (ME3Package importPCC = MEPackageHandler.OpenME3Package(filepath))
                        {
                            var exportToRead = importPCC.getUExport(info.exportIndex);
                            byte[] buff = exportToRead.Data.Skip(0x24).ToArray();
                            PropertyCollection defaults = PropertyCollection.ReadProps(importPCC, new MemoryStream(buff), className);
                            foreach (var prop in defaults)
                            {
                                structProps.TryReplaceProp(prop);
                            }
                        }
                    }
                    return structProps;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private static UProperty getDefaultProperty(string propName, PropertyInfo propInfo, bool stripTransients = true, bool isImmutable = false)
        {
            switch (propInfo.type)
            {
                case PropertyType.IntProperty:
                    return new IntProperty(0, propName);
                case PropertyType.FloatProperty:
                    return new FloatProperty(0f, propName);
                case PropertyType.ObjectProperty:
                case PropertyType.DelegateProperty:
                    return new ObjectProperty(0, propName);
                case PropertyType.NameProperty:
                    return new NameProperty() { Value = "None", Name = propName };
                case PropertyType.BoolProperty:
                    return new BoolProperty(false, propName);
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return new EnumProperty(propInfo.reference, MEGame.ME3, propName);
                case PropertyType.ByteProperty:
                    return new ByteProperty(0, propName);
                case PropertyType.StrProperty:
                    return new StrProperty("", propName);
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(propName);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(0, propName);
                case PropertyType.ArrayProperty:
                    switch (getArrayType(propInfo))
                    {
                        case ArrayType.Object:
                            return new ArrayProperty<ObjectProperty>(ArrayType.Object, propName);
                        case ArrayType.Name:
                            return new ArrayProperty<NameProperty>(ArrayType.Name, propName);
                        case ArrayType.Enum:
                            return new ArrayProperty<EnumProperty>(ArrayType.Enum, propName);
                        case ArrayType.Struct:
                            return new ArrayProperty<StructProperty>(ArrayType.Struct, propName);
                        case ArrayType.Bool:
                            return new ArrayProperty<BoolProperty>(ArrayType.Bool, propName);
                        case ArrayType.String:
                            return new ArrayProperty<StrProperty>(ArrayType.String, propName);
                        case ArrayType.Float:
                            return new ArrayProperty<FloatProperty>(ArrayType.Float, propName);
                        case ArrayType.Int:
                            return new ArrayProperty<IntProperty>(ArrayType.Int, propName);
                        case ArrayType.Byte:
                            return new ArrayProperty<ByteProperty>(ArrayType.Byte, propName);
                        default:
                            return null;
                    }
                case PropertyType.StructProperty:
                    return new StructProperty(propInfo.reference, getDefaultStructValue(propInfo.reference, stripTransients), propName, isImmutable);
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    return null;
            }
        }

        public static bool inheritsFrom(IEntry entry, string baseClass)
        {
            string className = entry.ClassName;
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }

        #region Generating
        //call this method to regenerate ME3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            var NewClasses = new Dictionary<string, ClassInfo>();
            var NewStructs = new Dictionary<string, ClassInfo>();
            var NewEnums = new Dictionary<string, List<NameReference>>();
            var newSequenceObjects = new Dictionary<string, SequenceObjectInfo>();

            string path = ME3Directory.gamePath;
            string[] files = Directory.GetFiles(Path.Combine(path, "BIOGame"), "*.pcc", SearchOption.AllDirectories);
            string objectName;
            int length = files.Length;
            for (int i = 0; i < length; i++)
            {
                if (files[i].ToLower().EndsWith(".pcc"))
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(files[i]))
                    {
                        for (int j = 1; j <= pcc.ExportCount; j++)
                        {
                            IExportEntry exportEntry = pcc.getUExport(j);
                            if (exportEntry.ClassName == "Enum")
                            {
                                generateEnumValues(exportEntry, NewEnums);
                            }
                            else if (exportEntry.ClassName == "Class")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!NewClasses.ContainsKey(objectName))
                                {
                                    NewClasses.Add(objectName, generateClassInfo(exportEntry));
                                }
                                if ((objectName.Contains("SeqAct") || objectName.Contains("SeqCond") || objectName.Contains("SequenceLatentAction") ||
                                    objectName == "SequenceOp" || objectName == "SequenceAction" || objectName == "SequenceCondition") && !newSequenceObjects.ContainsKey(objectName))
                                {
                                    newSequenceObjects.Add(objectName, generateSequenceObjectInfo(j, pcc));
                                }
                            }
                            else if (exportEntry.ClassName == "ScriptStruct")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!NewStructs.ContainsKey(objectName))
                                {
                                    NewStructs.Add(objectName, generateClassInfo(exportEntry, isStruct: true));
                                }
                            }
                        }
                    }
                }
                // System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
            }


            #region CUSTOM ADDITIONS
            //Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or by new classes designed in the modding scene that must be present in order for parsing to work properly

            //Kinkojiro - New Class - SFXSeqAct_AttachToSocket
            Classes["SFXSeqAct_AttachToSocket"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("PSC2Component", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "ParticleSystemComponent"
                    }),
                    new KeyValuePair<string, PropertyInfo>("PSC1Component", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "ParticleSystemComponent"
                    }),
                    new KeyValuePair<string, PropertyInfo>("SkMeshComponent", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "SkeletalMeshComponent"
                    }),
                    new KeyValuePair<string, PropertyInfo>("TargetPawn", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "Actor"
                    }),
                    new KeyValuePair<string, PropertyInfo>("AttachSocketName", new PropertyInfo
                    {
                        type = PropertyType.NameProperty
                    })
                }
            };

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            Classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("bFromMainMenu", new PropertyInfo
                    {
                        type = PropertyType.BoolProperty,
                    }),
                    new KeyValuePair<string, PropertyInfo>("m_oGuiReferenced", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "GFxMovieInfo"
                    })
                }
            };

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            Classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0,
                properties =
                {
                    new KeyValuePair<string, PropertyInfo>("m_aoTargets", new PropertyInfo
                    {
                        type = PropertyType.ArrayProperty, reference = "Actor"
                    }),
                    new KeyValuePair<string, PropertyInfo>("m_pDefaultFaceFXAsset", new PropertyInfo
                    {
                        type = PropertyType.ObjectProperty, reference = "FaceFXAsset"
                    })
                }
            };

            #endregion

            File.WriteAllText(jsonPath,
                JsonConvert.SerializeObject(new { SequenceObjects = newSequenceObjects, Classes = NewClasses, Structs = NewStructs, Enums = NewEnums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        private static SequenceObjectInfo generateSequenceObjectInfo(int i, ME3Package pcc)
        {
            SequenceObjectInfo info = new SequenceObjectInfo();
            //+1 to get the Default__ instance
            var inLinks = pcc.getUExport(i + 1).GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inLinks != null)
            {
                foreach (var seqOpInputLink in inLinks)
                {
                    info.inputLinks.Add(seqOpInputLink.GetProp<StrProperty>("LinkDesc").Value);
                }
            }
            return info;
        }

        public static ClassInfo generateClassInfo(IExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new ClassInfo
            {
                baseClass = export.ClassParent,
                exportIndex = export.UIndex
            };
            if (pcc.FileName.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FileName.Skip(pcc.FileName.LastIndexOf("BIOGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FileName; //used for dynamic resolution of files outside the game directory.
            }
            int nextExport = BitConverter.ToInt32(export.Data, isStruct ? 0x14 : 0xC);
            while (nextExport > 0)
            {
                var entry = pcc.getUExport(nextExport);
                if (entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    if (!info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                }
                nextExport = BitConverter.ToInt32(entry.Data, 0x10);
            }
            return info;
        }

        private static void generateEnumValues(IExportEntry export, Dictionary<string, List<NameReference>> NewEnums = null)
        {
            var enumTable = NewEnums ?? Enums;
            string enumName = export.ObjectName;
            if (!enumTable.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                byte[] buff = export.Data;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = BitConverter.ToInt32(buff, 20) - 1;
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(export.FileRef.Names[BitConverter.ToInt32(buff, enumValIndex)], BitConverter.ToInt32(buff, enumValIndex + 4)));
                }
                enumTable.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(IExportEntry entry)
        {
            IMEPackage pcc = entry.FileRef;
            PropertyInfo p = new PropertyInfo();
            switch (entry.ClassName)
            {
                case "IntProperty":
                    p.type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    p.type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    p.type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    p.type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    p.type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    p.type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    p.type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    p.type = PropertyType.ObjectProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    p.type = PropertyType.StructProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    p.type = PropertyType.ByteProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    p.type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc.getUExport(BitConverter.ToInt32(entry.Data, 44)));
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.reference == "Class")
                                    p.reference = arrayTypeProp.type.ToString();
                                else
                                    p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                p.reference = arrayTypeProp.type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                System.Diagnostics.Debugger.Break();
                                p = null;
                                break;
                        }
                    }
                    else
                    {
                        p = null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    p = null;
                    break;
            }
            if (p != null && ((UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(entry.Data, 24)).HasFlag(UnrealFlags.EPropertyFlags.Transient))
            {
                //Transient
                p.transient = true;
            }
            return p;
        }
        #endregion

        #region CodeGen
        public static void GenerateCode()
        {
            GenerateEnums();
            GenerateStructs();
            GenerateClasses();
        }
        private static void GenerateClasses()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Classes.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Structs;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Classes", () =>
                {
                    writer.WriteBlock("public class Level", () =>
                    {
                        writer.WriteLine("public float ShadowmapTotalSize;");
                        writer.WriteLine("public float LightmapTotalSize;");
                    });
                    foreach ((string className, ClassInfo info) in Classes)
                    {
                        writer.WriteBlock($"public class {className}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.transient || propInfo.type == PropertyType.None)
                                {
                                    continue;
                                }
                                if (propName.Contains(":") || propName == className)
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} _{propName.Replace(":", "")};");
                                }
                                else
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName};");
                                }
                            }
                        });
                    }
                });
            }
        }

        private static void GenerateStructs()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Structs.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Classes;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Structs", () =>
                {
                    foreach ((string structName, ClassInfo info) in Structs)
                    {
                        writer.WriteBlock($"public class {structName}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.transient || propInfo.type == PropertyType.None)
                                {
                                    continue;
                                }
                                writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName.Replace(":", "")};");
                            }
                        });
                    }
                });
            }
        }

        private static void GenerateEnums()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Enums.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Enums", () =>
                {
                    foreach ((string enumName, List<NameReference> values) in Enums)
                    {
                        writer.WriteBlock($"public enum {enumName}", () =>
                        {
                            foreach (NameReference val in values)
                            {
                                writer.WriteLine($"{val.InstancedString},");
                            }
                        });
                    }
                });
            }
        }
        static string CSharpTypeFromUnrealType(PropertyInfo propInfo)
        {
            switch (propInfo.type)
            {
                case PropertyType.StructProperty:
                    return propInfo.reference;
                case PropertyType.IntProperty:
                    return "int";
                case PropertyType.FloatProperty:
                    return "float";
                case PropertyType.DelegateProperty:
                case PropertyType.ObjectProperty:
                    return "int";
                case PropertyType.NameProperty:
                    return nameof(NameReference);
                case PropertyType.BoolProperty:
                    return "bool";
                case PropertyType.BioMask4Property:
                    return "byte";
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return propInfo.reference;
                case PropertyType.ByteProperty:
                    return "byte";
                case PropertyType.ArrayProperty:
                    {
                        string type;
                        if (Enum.TryParse(propInfo.reference, out PropertyType arrayType))
                        {
                            type = CSharpTypeFromUnrealType(new PropertyInfo { type = arrayType });
                        }
                        else if (Classes.ContainsKey(propInfo.reference))
                        {
                            //ObjectProperty
                            type = "int";
                        }
                        else
                        {
                            type = propInfo.reference;
                        }

                        return $"{type}[]";
                    }
                case PropertyType.StrProperty:
                    return "string";
                case PropertyType.StringRefProperty:
                    return "int";
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
    }
}

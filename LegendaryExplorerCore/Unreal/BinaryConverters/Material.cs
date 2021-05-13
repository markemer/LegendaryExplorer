﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Material : ObjectBinary
    {
        public MaterialResource SM3MaterialResource;
        public MaterialResource SM2MaterialResource;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SM3MaterialResource);
            if (sc.Game.IsOTGame() && sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref SM2MaterialResource);
            }
            else if (sc.Game.IsLEGame())
            {
                // uhhhh
                // TODO: SERIALIZE THIS
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();
            uIndexes.AddRange(SM3MaterialResource.GetUIndexes(game));
            if (game != MEGame.UDK)
            {
                uIndexes.AddRange(SM2MaterialResource.GetUIndexes(game));
            }
            return uIndexes;
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SM3MaterialResource.GetNames(game));
            if (game != MEGame.UDK)
            {
                names.AddRange(SM2MaterialResource.GetNames(game));
            }

            return names;
        }
    }
    public class MaterialInstance : ObjectBinary
    {
        public MaterialResource SM3StaticPermutationResource;
        public StaticParameterSet SM3StaticParameterSet;
        public MaterialResource SM2StaticPermutationResource;
        public StaticParameterSet SM2StaticParameterSet;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SM3StaticPermutationResource);
            sc.Serialize(ref SM3StaticParameterSet);
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref SM2StaticPermutationResource);
                sc.Serialize(ref SM2StaticParameterSet);
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();
            uIndexes.AddRange(SM3StaticPermutationResource.GetUIndexes(game));
            if (game != MEGame.UDK)
            {
                uIndexes.AddRange(SM2StaticPermutationResource.GetUIndexes(game));
            }
            return uIndexes;
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SM3StaticPermutationResource.GetNames(game));
            names.AddRange(SM3StaticParameterSet.GetNames(game));
            if (game != MEGame.UDK)
            {
                names.AddRange(SM2StaticPermutationResource.GetNames(game));
                names.AddRange(SM2StaticParameterSet.GetNames(game));
            }

            return names;
        }
    }

    //structs

    public class MaterialResource
    {
        public class TextureLookup
        {
            public int TexCoordIndex;
            public int TextureIndex;
            public int UScale;
            public int VScale;
        }

        public string[] CompileErrors;
        public OrderedMultiValueDictionary<UIndex, int> TextureDependencyLengthMap;
        public int MaxTextureDependencyLength;
        public Guid ID;
        public uint NumUserTexCoords;
        public UIndex[] UniformExpressionTextures; //serialized for ME3, but will be set here for ME1 and ME2 as well
        //begin Not ME3
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        //end Not ME3
        public bool bUsesSceneColor;
        public bool bUsesSceneDepth;
        //begin ME3
        public bool bUsesDynamicParameter;
        public bool bUsesLightmapUVs;
        public bool bUsesMaterialVertexPositionOffset;
        public bool unkBool1;
        //end ME3
        public uint UsingTransforms; //ECoordTransformUsage
        public TextureLookup[] TextureLookups; //not ME1
        public uint udkUnk1;
        public uint udkUnk2;
        public uint udkUnk3;
        public uint udkUnk4;
        //begin ME1
        public ME1MaterialUniformExpressionsElement[] Me1MaterialUniformExpressionsList;
        public int unk1;
        public int unkCount
        {
            get => unkList?.Length ?? 0;
            set => Array.Resize(ref unkList, value);
        }
        public int unkInt2;
        public (int, float, int)[] unkList;
        //end ME1

        public virtual List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex uIndex, string)> uIndexes = TextureDependencyLengthMap.Keys().Select((uIndex, i) => (uIndex, $"TextureDependencyLengthMap[{i}]")).ToList();
            if (game >= MEGame.ME3)
            {
                uIndexes.AddRange(UniformExpressionTextures.Select((uIndex, i) => (uIndex, $"UniformExpressionTextures[{i}]")));
            }
            else
            {
                uIndexes.AddRange(UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                    .Select((flipParam, i) => (flipParam.TextureIndex, $"UniformPixelVectorExpressions[{i}]")));

                // Used in ME2 Carnage fireballPlasma Material
                uIndexes.AddRange(UniformPixelVectorExpressions.OfType<MaterialUniformExpressionTexture>()
                    .Select((flipParam, i) => (flipParam.TextureIndex, $"UniformPixelVectorExpressions[{i}]")));

                uIndexes.AddRange(UniformPixelScalarExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                    .Select((flipParam, i) => (flipParam.TextureIndex, $"UniformPixelScalarExpressions[{i}]")));
                uIndexes.AddRange(Uniform2DTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"Uniform2DTextureExpressions[{i}]")));
                uIndexes.AddRange(UniformCubeTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"UniformCubeTextureExpressions[{i}]")));
                if (game == MEGame.ME1)
                {
                    int j = 0;
                    foreach (ME1MaterialUniformExpressionsElement expressionsElement in Me1MaterialUniformExpressionsList)
                    {
                        uIndexes.AddRange(expressionsElement.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                            .Select((flipParam, i) => (flipParam.TextureIndex, $"MaterialUniformExpressions[{j}].UniformPixelVectorExpressions[{i}]")));
                        uIndexes.AddRange(expressionsElement.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                            .Select((flipParam, i) => (flipParam.TextureIndex, $"MaterialUniformExpressions[{j}].UniformPixelScalarExpressions[{i}]")));
                        uIndexes.AddRange(expressionsElement.Uniform2DTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"MaterialUniformExpressions[{j}].Uniform2DTextureExpressions[{i}]")));
                        uIndexes.AddRange(expressionsElement.UniformCubeTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"MaterialUniformExpressions[{j}].UniformCubeTextureExpressions[{i}]")));
                        ++j;
                    }
                }
            }
            return uIndexes;
        }

        public List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            if (game <= MEGame.ME2)
            {
                var uniformExpressionArrays = new List<(string, MaterialUniformExpression[])>
                {
                    (nameof(UniformPixelVectorExpressions), UniformPixelVectorExpressions),
                    (nameof(UniformPixelScalarExpressions), UniformPixelScalarExpressions),
                    (nameof(Uniform2DTextureExpressions), Uniform2DTextureExpressions),
                    (nameof(UniformCubeTextureExpressions), UniformCubeTextureExpressions),
                };
                if (game == MEGame.ME1)
                {
                    int j = 0;
                    foreach (ME1MaterialUniformExpressionsElement expressionsElement in Me1MaterialUniformExpressionsList)
                    {
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformPixelVectorExpressions", expressionsElement.UniformPixelVectorExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformPixelScalarExpressions", expressionsElement.UniformPixelScalarExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].Uniform2DTextureExpressions", expressionsElement.Uniform2DTextureExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformCubeTextureExpressions", expressionsElement.UniformCubeTextureExpressions));
                        ++j;
                    }
                }

                foreach ((string prefix, MaterialUniformExpression[] expressions) in uniformExpressionArrays)
                {
                    for (int i = 0; i < expressions.Length; i++)
                    {
                        MaterialUniformExpression expression = expressions[i];
                        names.Add((expression.ExpressionType, $"{prefix}[{i}].ExpressionType"));
                        switch (expression)
                        {
                            case MaterialUniformExpressionTextureParameter texParamExpression:
                                names.Add((texParamExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionScalarParameter scalarParameterExpression:
                                names.Add((scalarParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionVectorParameter vecParameterExpression:
                                names.Add((vecParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                        }

                        names.AddRange(expression.GetNames(game));
                    }
                }
            }

            return names;
        }
    }

    public class ME1MaterialUniformExpressionsElement
    {
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        public uint unk2;
        public uint unk3;
        public uint unk4;
        public uint unk5;
    }

    public class StaticParameterSet : IEquatable<StaticParameterSet>
    {
        public class StaticSwitchParameter
        {
            public NameReference ParameterName;
            public bool Value;
            public bool bOverride;
            public Guid ExpressionGUID;
        }
        public class StaticComponentMaskParameter
        {
            public NameReference ParameterName;
            public bool R;
            public bool G;
            public bool B;
            public bool A;
            public bool bOverride;
            public Guid ExpressionGUID;
        }
        public class NormalParameter
        {
            public NameReference ParameterName;
            public byte CompressionSettings;
            public bool bOverride;
            public Guid ExpressionGUID;
        }

        public Guid BaseMaterialId;
        public StaticSwitchParameter[] StaticSwitchParameters;
        public StaticComponentMaskParameter[] StaticComponentMaskParameters;
        public NormalParameter[] NormalParameters;//ME3

        #region IEquatable

        public bool Equals(StaticParameterSet other)
        {
            if (other is null || other.BaseMaterialId != BaseMaterialId || other.StaticSwitchParameters.Length != StaticSwitchParameters.Length ||
                other.StaticComponentMaskParameters.Length != StaticComponentMaskParameters.Length || other.NormalParameters.Length != NormalParameters.Length)
            {
                return false;
            }
            //bOverride is intentionally left out of the following comparisons
            for (int i = 0; i < StaticSwitchParameters.Length; i++)
            {
                var a = StaticSwitchParameters[i];
                var b = other.StaticSwitchParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.Value != b.Value)
                {
                    return false;
                }
            }
            for (int i = 0; i < StaticComponentMaskParameters.Length; i++)
            {
                var a = StaticComponentMaskParameters[i];
                var b = other.StaticComponentMaskParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.R != b.R || a.G != b.G || a.B != b.B || a.A != b.A)
                {
                    return false;
                }
            }
            for (int i = 0; i < NormalParameters.Length; i++)
            {
                var a = NormalParameters[i];
                var b = other.NormalParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.CompressionSettings != b.CompressionSettings)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StaticParameterSet)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = BaseMaterialId.GetHashCode();
                hashCode = (hashCode * 397) ^ StaticSwitchParameters.GetHashCode();
                hashCode = (hashCode * 397) ^ StaticComponentMaskParameters.GetHashCode();
                hashCode = (hashCode * 397) ^ NormalParameters.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StaticParameterSet left, StaticParameterSet right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StaticParameterSet left, StaticParameterSet right)
        {
            return !Equals(left, right);
        }
        #endregion

        public static explicit operator StaticParameterSet(Guid guid)
        {
            return new StaticParameterSet
            {
                BaseMaterialId = guid,
                StaticSwitchParameters = new StaticSwitchParameter[0],
                StaticComponentMaskParameters = new StaticComponentMaskParameter[0],
                NormalParameters = new NormalParameter[0]
            };
        }

        public List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(StaticSwitchParameters.Select((param, i) => (param.ParameterName, $"{nameof(StaticSwitchParameters)}[{i}].ParameterName")));
            names.AddRange(StaticComponentMaskParameters.Select((param, i) => (param.ParameterName, $"{nameof(StaticComponentMaskParameters)}[{i}].ParameterName")));
            if (game >= MEGame.ME3)
            {
                names.AddRange(NormalParameters.Select((param, i) => (param.ParameterName, $"{nameof(NormalParameters)}[{i}].ParameterName")));
            }

            return names;
        }
    }

    #region MaterialUniformExpressions
    //FMaterialUniformExpressionRealTime
    //FMaterialUniformExpressionTime
    //FMaterialUniformExpressionFractionOfEffectEnabled
    public class MaterialUniformExpression
    {
        public NameReference ExpressionType;

        public virtual void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref ExpressionType);
        }

        public virtual List<(NameReference, string)> GetNames(MEGame game)
        {
            return new List<(NameReference, string)>(0);
        }

        public static MaterialUniformExpression Create(SerializingContainer2 sc)
        {
            NameReference expressionType = sc.ms.ReadNameReference(sc.Pcc);
            sc.ms.Skip(-8);//ExpressionType will be read again during serialization, so back the stream up.
            switch (expressionType.Name)
            {
                case "FMaterialUniformExpressionAbs":
                case "FMaterialUniformExpressionCeil":
                case "FMaterialUniformExpressionFloor":
                case "FMaterialUniformExpressionFrac":
                case "FMaterialUniformExpressionPeriodic":
                case "FMaterialUniformExpressionSquareRoot":
                    return new MaterialUniformExpressionUnaryOp();
                case "FMaterialUniformExpressionAppendVector":
                    return new MaterialUniformExpressionAppendVector();
                case "FMaterialUniformExpressionClamp":
                    return new MaterialUniformExpressionClamp();
                case "FMaterialUniformExpressionConstant":
                    return new MaterialUniformExpressionConstant();
                case "FMaterialUniformExpressionFmod":
                case "FMaterialUniformExpressionMax":
                case "FMaterialUniformExpressionMin":
                    return new MaterialUniformExpressionBinaryOp();
                case "FMaterialUniformExpressionFoldedMath":
                    return new MaterialUniformExpressionFoldedMath();
                case "FMaterialUniformExpressionTime":
                case "FMaterialUniformExpressionRealTime":
                case "FMaterialUniformExpressionFractionOfEffectEnabled":
                    return new MaterialUniformExpression();
                case "FMaterialUniformExpressionScalarParameter":
                    return new MaterialUniformExpressionScalarParameter();
                case "FMaterialUniformExpressionSine":
                    return new MaterialUniformExpressionSine();
                case "FMaterialUniformExpressionTexture":
                case "FMaterialUniformExpressionFlipBookTextureParameter":
                    return new MaterialUniformExpressionTexture();
                case "FMaterialUniformExpressionTextureParameter":
                    return new MaterialUniformExpressionTextureParameter();
                case "FMaterialUniformExpressionVectorParameter":
                    return new MaterialUniformExpressionVectorParameter();
                case "FMaterialUniformExpressionFlipbookParameter":
                    return new MaterialUniformExpressionFlipbookParameter();
                default:
                    throw new ArgumentException(expressionType.Instanced);
            }
        }
    }
    // FMaterialUniformExpressionAbs
    // FMaterialUniformExpressionCeil
    // FMaterialUniformExpressionFloor
    // FMaterialUniformExpressionFrac
    // FMaterialUniformExpressionPeriodic
    // FMaterialUniformExpressionSquareRoot
    public class MaterialUniformExpressionUnaryOp : MaterialUniformExpression
    {
        public MaterialUniformExpression X;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                X = Create(sc);
            }
            X.Serialize(sc);
        }
    }
    //FMaterialUniformExpressionFlipbookParameter
    public class MaterialUniformExpressionFlipbookParameter : MaterialUniformExpression
    {
        public int Index;
        public UIndex TextureIndex;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Index);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionSine
    public class MaterialUniformExpressionSine : MaterialUniformExpressionUnaryOp
    {
        public bool bIsCosine;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref bIsCosine);
        }
    }
    // FMaterialUniformExpressionFmod
    // FMaterialUniformExpressionMax
    // FMaterialUniformExpressionMin
    public class MaterialUniformExpressionBinaryOp : MaterialUniformExpression
    {
        public MaterialUniformExpression A;
        public MaterialUniformExpression B;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                A = Create(sc);
            }
            A.Serialize(sc);
            if (sc.IsLoading)
            {
                B = Create(sc);
            }
            B.Serialize(sc);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            // TODO: IMPROVE TEXT
            var names = new List<(NameReference, string)>();
            names.Add((A.ExpressionType, $"{ExpressionType}.A.ExpressionType"));
            names.AddRange(A.GetNames(game));
            names.Add((B.ExpressionType, $"{ExpressionType}.A.ExpressionType"));
            names.AddRange(B.GetNames(game));
            return names;
        }

    }
    // FMaterialUniformExpressionAppendVector
    public class MaterialUniformExpressionAppendVector : MaterialUniformExpressionBinaryOp
    {
        public uint NumComponentsA;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NumComponentsA);
        }
    }
    //FMaterialUniformExpressionFoldedMath
    public class MaterialUniformExpressionFoldedMath : MaterialUniformExpressionBinaryOp
    {
        public byte Op; //EFoldedMathOperation
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Op);
        }
    }
    // FMaterialUniformExpressionClamp
    public class MaterialUniformExpressionClamp : MaterialUniformExpression
    {
        public MaterialUniformExpression Input;
        public MaterialUniformExpression Min;
        public MaterialUniformExpression Max;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                Input = Create(sc);
            }
            Input.Serialize(sc);
            if (sc.IsLoading)
            {
                Min = Create(sc);
            }
            Min.Serialize(sc);
            if (sc.IsLoading)
            {
                Max = Create(sc);
            }
            Max.Serialize(sc);
        }
    }
    //FMaterialUniformExpressionConstant
    public class MaterialUniformExpressionConstant : MaterialUniformExpression
    {
        public float R;
        public float G;
        public float B;
        public float A;
        public byte ValueType;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref R);
            sc.Serialize(ref G);
            sc.Serialize(ref B);
            sc.Serialize(ref A);
            sc.Serialize(ref ValueType);
        }
    }
    //FMaterialUniformExpressionTexture
    //FMaterialUniformExpressionFlipBookTextureParameter
    public class MaterialUniformExpressionTexture : MaterialUniformExpression
    {
        public UIndex TextureIndex; //UIndex in ME1/2, index into MaterialResource's Uniform2DTextureExpressions in ME3
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionTextureParameter
    public class MaterialUniformExpressionTextureParameter : MaterialUniformExpressionTexture
    {
        public NameReference ParameterName;
        public override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref ExpressionType);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionScalarParameter
    public class MaterialUniformExpressionScalarParameter : MaterialUniformExpression
    {
        public NameReference ParameterName;
        public float DefaultValue;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref DefaultValue);
        }
    }
    //FMaterialUniformExpressionVectorParameter
    public class MaterialUniformExpressionVectorParameter : MaterialUniformExpression
    {
        public NameReference ParameterName;
        public float DefaultR;
        public float DefaultG;
        public float DefaultB;
        public float DefaultA;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref DefaultR);
            sc.Serialize(ref DefaultG);
            sc.Serialize(ref DefaultB);
            sc.Serialize(ref DefaultA);
        }
    }
    #endregion

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref MaterialResource mres)
        {
            if (sc.IsLoading && mres == null)
            {
                mres = new MaterialResource();
            }
            sc.Serialize(ref mres.CompileErrors, SCExt.Serialize);
            sc.Serialize(ref mres.TextureDependencyLengthMap, Serialize, Serialize);
            sc.Serialize(ref mres.MaxTextureDependencyLength);
            sc.Serialize(ref mres.ID);
            sc.Serialize(ref mres.NumUserTexCoords);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mres.UniformExpressionTextures, Serialize);
            }
            else
            {
                sc.Serialize(ref mres.UniformPixelVectorExpressions, Serialize);
                sc.Serialize(ref mres.UniformPixelScalarExpressions, Serialize);
                sc.Serialize(ref mres.Uniform2DTextureExpressions, Serialize);
                sc.Serialize(ref mres.UniformCubeTextureExpressions, Serialize);

                if (sc.IsLoading)
                {
                    mres.UniformExpressionTextures = mres.Uniform2DTextureExpressions.Select(texExpr => texExpr.TextureIndex).ToArray();
                }
            }
            sc.Serialize(ref mres.bUsesSceneColor);
            sc.Serialize(ref mres.bUsesSceneDepth);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mres.bUsesDynamicParameter);
                sc.Serialize(ref mres.bUsesLightmapUVs);
                sc.Serialize(ref mres.bUsesMaterialVertexPositionOffset);
                if (sc.Game == MEGame.ME3)
                {
                    sc.Serialize(ref mres.unkBool1);
                }
            }
            sc.Serialize(ref mres.UsingTransforms);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref mres.Me1MaterialUniformExpressionsList, Serialize);
            }
            else
            {
                sc.Serialize(ref mres.TextureLookups, Serialize);
                if (sc.Game == MEGame.UDK)
                {
                    sc.Serialize(ref mres.udkUnk1);
                    sc.Serialize(ref mres.udkUnk2);
                    sc.Serialize(ref mres.udkUnk3);
                    sc.Serialize(ref mres.udkUnk4);
                }
                else
                {
                    int dummy = 0;
                    sc.Serialize(ref dummy);
                }
            }
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref mres.unk1);
                int tmp = mres.unkCount;
                sc.Serialize(ref tmp);
                mres.unkCount = tmp; //will create mr.unkList of unkCount size
                sc.Serialize(ref mres.unkInt2);
                for (int i = 0; i < mres.unkCount; i++)
                {
                    sc.Serialize(ref mres.unkList[i].Item1);
                    sc.Serialize(ref mres.unkList[i].Item2);
                    sc.Serialize(ref mres.unkList[i].Item3);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialResource.TextureLookup tLookup)
        {
            if (sc.IsLoading)
            {
                tLookup = new MaterialResource.TextureLookup();
            }
            sc.Serialize(ref tLookup.TexCoordIndex);
            sc.Serialize(ref tLookup.TextureIndex);
            sc.Serialize(ref tLookup.UScale);
            if (sc.IsLoading && sc.Game == MEGame.ME1)
            {
                tLookup.VScale = tLookup.UScale;
            }

            if (sc.Game != MEGame.ME1)
            {
                sc.Serialize(ref tLookup.VScale);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialUniformExpression matExp)
        {
            if (sc.IsLoading)
            {
                matExp = MaterialUniformExpression.Create(sc);
            }
            matExp.Serialize(sc);
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialUniformExpressionTexture matExp)
        {
            if (sc.IsLoading)
            {
                matExp = (MaterialUniformExpressionTexture)MaterialUniformExpression.Create(sc);
            }
            matExp.Serialize(sc);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet paramSet)
        {
            if (sc.IsLoading)
            {
                paramSet = new StaticParameterSet();
            }
            sc.Serialize(ref paramSet.BaseMaterialId);
            sc.Serialize(ref paramSet.StaticSwitchParameters, Serialize);
            sc.Serialize(ref paramSet.StaticComponentMaskParameters, Serialize);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref paramSet.NormalParameters, Serialize);
            }
            else if (sc.IsLoading)
            {
                paramSet.NormalParameters = new StaticParameterSet.NormalParameter[0];
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.StaticSwitchParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.StaticSwitchParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.Value);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.StaticComponentMaskParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.StaticComponentMaskParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.R);
            sc.Serialize(ref param.G);
            sc.Serialize(ref param.B);
            sc.Serialize(ref param.A);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.NormalParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.NormalParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.CompressionSettings);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref ME1MaterialUniformExpressionsElement elem)
        {
            if (sc.IsLoading)
            {
                elem = new ME1MaterialUniformExpressionsElement();
            }
            sc.Serialize(ref elem.UniformPixelVectorExpressions, Serialize);
            sc.Serialize(ref elem.UniformPixelScalarExpressions, Serialize);
            sc.Serialize(ref elem.Uniform2DTextureExpressions, Serialize);
            sc.Serialize(ref elem.UniformCubeTextureExpressions, Serialize);
            sc.Serialize(ref elem.unk2);
            sc.Serialize(ref elem.unk3);
            sc.Serialize(ref elem.unk4);
            sc.Serialize(ref elem.unk5);
        }
    }
}
using System;
using System.Reflection;
using HarmonyLib;

namespace HydroReadbackFix
{
    internal static class HydroPatch
    {
        private const string WaterSystemTypeName =
            "Game.Simulation.WaterSystem";

        private const string SurfaceReaderTypeName =
            "Game.Simulation.SurfaceDataReader";

        private const string BaseReaderTypeName =
            "Game.Simulation.BaseDataReader`2";

        private const string Float4TypeName =
            "Unity.Mathematics.float4";

        private const string BrokenFormat =
            "R16G16B16A16_SFloat";

        private const string WorkingFormat =
            "R32G32B32A32_SFloat";

        private static readonly object Sync = new object();

        private static FieldInfo _velocityReaderField;
        private static FieldInfo _graphicsFormatField;

        private static object _lastReader;

        private static bool _reportedNoFixNeeded;
        private static bool _reportedUnsupported;

        internal static bool Install(Harmony harmony)
        {
            Type waterSystem =
                AccessTools.TypeByName(WaterSystemTypeName);

            if (waterSystem == null)
            {
                Mod.Log.Warn(
                    "[HydroReadbackFix] WaterSystem type not found");
                return false;
            }

            MethodInfo initTextures =
                AccessTools.DeclaredMethod(
                    waterSystem,
                    "InitTextures");

            MethodInfo getVelocities =
                AccessTools.DeclaredMethod(
                    waterSystem,
                    "GetVelocitiesSurfaceData");

            if (initTextures == null &&
                getVelocities == null)
            {
                return false;
            }

            if (initTextures != null)
            {
                MethodInfo postfix =
                    AccessTools.DeclaredMethod(
                        typeof(HydroPatch),
                        nameof(InitTexturesPostfix));

                harmony.Patch(
                    initTextures,
                    postfix: new HarmonyMethod(postfix));
            }

            if (getVelocities != null)
            {
                MethodInfo prefix =
                    AccessTools.DeclaredMethod(
                        typeof(HydroPatch),
                        nameof(GetVelocitiesPrefix));

                harmony.Patch(
                    getVelocities,
                    prefix: new HarmonyMethod(prefix));
            }

            return true;
        }

        private static void InitTexturesPostfix(object __instance)
        {
            lock (Sync)
            {
                _lastReader = null;
                _graphicsFormatField = null;
            }

            ValidateAndFix(__instance);
        }

        private static void GetVelocitiesPrefix(object __instance)
        {
            ValidateAndFix(__instance);
        }

        private static void ValidateAndFix(object waterSystem)
        {
            if (waterSystem == null)
                return;

            try
            {
                lock (Sync)
                {
                    if (_velocityReaderField == null)
                    {
                        _velocityReaderField =
                            AccessTools.Field(
                                waterSystem.GetType(),
                                "m_velocitiesReader");

                        if (_velocityReaderField == null)
                        {
                            ReportUnsupportedOnce(
                                "m_velocitiesReader was not found");
                            return;
                        }
                    }

                    object reader =
                        _velocityReaderField.GetValue(waterSystem);

                    if (reader == null)
                        return;

                    if (ReferenceEquals(reader, _lastReader))
                        return;

                    _lastReader = reader;
                    _graphicsFormatField = null;

                    if (reader.GetType().FullName !=
                        SurfaceReaderTypeName)
                    {
                        ReportUnsupportedOnce(
                            $"Unexpected velocity reader type: " +
                            $"{reader.GetType().FullName}");
                        return;
                    }

                    Type stagingType =
                        GetStagingType(reader.GetType());

                    if (stagingType == null)
                    {
                        ReportUnsupportedOnce(
                            "Could not determine CPU staging type");
                        return;
                    }

                    if (stagingType.FullName != Float4TypeName)
                    {
                        ReportNoFixNeededOnce(
                            $"CPU staging type is {stagingType.FullName}; " +
                            "the known float4/RGBA16F mismatch is absent");
                        return;
                    }

                    _graphicsFormatField =
                        FindInstanceField(
                            reader.GetType(),
                            "m_graphicsFormat");

                    if (_graphicsFormatField == null)
                    {
                        ReportUnsupportedOnce(
                            "m_graphicsFormat was not found");
                        return;
                    }

                    object currentValue =
                        _graphicsFormatField.GetValue(reader);

                    if (currentValue == null)
                    {
                        ReportUnsupportedOnce(
                            "m_graphicsFormat returned null");
                        return;
                    }

                    string currentFormat =
                        currentValue.ToString();

                    if (currentFormat == WorkingFormat)
                    {
                        ReportNoFixNeededOnce(
                            $"velocity readback is already {WorkingFormat}");
                        return;
                    }

                    if (currentFormat != BrokenFormat)
                    {
                        ReportNoFixNeededOnce(
                            $"velocity readback format is {currentFormat}, " +
                            $"not the known affected {BrokenFormat}");
                        return;
                    }

                    object workingValue =
                        Enum.Parse(
                            _graphicsFormatField.FieldType,
                            WorkingFormat);

                    _graphicsFormatField.SetValue(
                        reader,
                        workingValue);

                    object verifiedValue =
                        _graphicsFormatField.GetValue(reader);

                    if (verifiedValue == null ||
                        verifiedValue.ToString() != WorkingFormat)
                    {
                        Mod.Log.Error(
                            "[HydroReadbackFix] Attempted compatibility fix " +
                            "but verification failed");
                        return;
                    }

                    Mod.Log.Info(
                        "[HydroReadbackFix] Known 1.6.2 water readback " +
                        "regression detected");

                    Mod.Log.Info(
                        $"[HydroReadbackFix] Velocity CPU readback format: " +
                        $"{BrokenFormat} -> {WorkingFormat}");

                    Mod.Log.Info(
                        "[HydroReadbackFix] Compatibility fix active");
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Error(
                    $"[HydroReadbackFix] Validation/fix failed: {ex}");
            }
        }

        private static Type GetStagingType(Type readerType)
        {
            Type type = readerType;

            while (type != null)
            {
                if (type.IsGenericType)
                {
                    Type genericDefinition =
                        type.GetGenericTypeDefinition();

                    if (genericDefinition.FullName ==
                        BaseReaderTypeName)
                    {
                        Type[] args =
                            type.GetGenericArguments();

                        if (args.Length == 2)
                            return args[1];

                        return null;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        private static FieldInfo FindInstanceField(
            Type type,
            string fieldName)
        {
            while (type != null)
            {
                FieldInfo field =
                    type.GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        private static void ReportNoFixNeededOnce(
            string reason)
        {
            if (_reportedNoFixNeeded)
                return;

            _reportedNoFixNeeded = true;

            Mod.Log.Info(
                $"[HydroReadbackFix] Fix not required: {reason}");
        }

        private static void ReportUnsupportedOnce(
            string reason)
        {
            if (_reportedUnsupported)
                return;

            _reportedUnsupported = true;

            Mod.Log.Warn(
                $"[HydroReadbackFix] Game implementation differs from " +
                $"the known affected version: {reason}. No change made.");
        }
    }
}

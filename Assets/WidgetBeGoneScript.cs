using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public class WidgetBeGoneScript : MonoBehaviour
{
    private ModSettings<Settings> _settings;
    private static WidgetBeGoneScript _instance = null;
    private static bool _patched = false, _patchEnabled = false;
    private static Type _batteryType, _indicatorType, _portType, _serialType;
    private static List<Type> _removedTypes;

    void Start()
    {
        if (_instance != null)
        {
            Debug.Log("[Widget-Be-Gone] Duplicated instance. Self-destructing.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _patchEnabled = true;

        _settings = new ModSettings<Settings>("WidgetBeGone-settings");

        if (!_patched)
            StartCoroutine(ApplyPatch());
    }

    private static IEnumerator ApplyPatch()
    {
        var wgType = AppDomain
                 .CurrentDomain
                 .GetAssemblies()
                 .SelectMany(a => GetLoadableTypes(a))
                 .FirstOrDefault(t => t != null && t.Name.Equals("WidgetGenerator"));

        _batteryType = wgType.Assembly.GetType("BatteryWidget");
        _indicatorType = wgType.Assembly.GetType("IndicatorWidget");
        _portType = wgType.Assembly.GetType("PortWidget");
        _serialType = wgType.Assembly.GetType("SerialNumber");

        var orig = wgType.GetMethod("GenerateWidgets", BindingFlags.Public | BindingFlags.Instance);
        var patch = typeof(WidgetBeGoneScript).GetMethod("GeneratePatch", BindingFlags.NonPublic | BindingFlags.Static);
        // DBML stops the method from running until later, so we need to run before that.
        // Adding the prefix after DBML does ensures that ours runs first.
        // Ditto for Voltage Meter.
        var prefix = new HarmonyMethod(patch, before: new string[] { "samfundev.tweaks.DBML", "voltageMeter.harmony" });

        Harmony harm = new Harmony("net.gdane.WidgetBeGone");
        harm.Patch(orig, prefix: prefix);

        Debug.Log("[Widget-Be-Gone] Widget generation patched. Patching Serial Number Modifier...");
        _patched = true;

        Type snmType = AppDomain
                 .CurrentDomain
                 .GetAssemblies()
                 .SelectMany(a => GetLoadableTypes(a))
                 .FirstOrDefault(t => t != null && t.Name.Equals("SerialNumberModifierAssembly.CommonReflectedTypeInfo"));
        do
        {
            yield return new WaitForSeconds(1f);
            snmType = AppDomain
                 .CurrentDomain
                 .GetAssemblies()
                 .SelectMany(a => GetLoadableTypes(a))
                 .FirstOrDefault(t => t != null && t.FullName.Equals("SerialNumberModifierAssembly.CommonReflectedTypeInfo"));
        }
        while (snmType == null);

        orig = snmType.GetMethod("AddWidgetToBomb", BindingFlags.Public | BindingFlags.Static);
        patch = typeof(WidgetBeGoneScript).GetMethod("ModifierPatch", BindingFlags.NonPublic | BindingFlags.Static);
        prefix = new HarmonyMethod(patch);

        harm.Patch(orig, prefix: prefix);
        Debug.Log("[Widget-Be-Gone] Serial Number Modifier patched.");
    }

    private void OnEnable()
    {
        if (_instance == this)
            _patchEnabled = true;
    }

    private void OnDisable()
    {
        if (_instance == this)
            _patchEnabled = false;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private static IEnumerator DoNothing()
    {
        yield break;
    }

    private static bool ModifierPatch(ref IEnumerator __result)
    {
        if (!_patchEnabled || _instance == null)
            return true;

        ReadSettings();

        if (!_removedTypes.Contains(_serialType))
            return true;

        Debug.Log("[Widget-Be-Gone] Preventing Serial Number Modifier from doing anything for this bomb.");

        __result = DoNothing();
        return false;
    }

    private static void GeneratePatch(IList ___Widgets, IList ___RequiredWidgets)
    {
        if (!_patchEnabled || _instance == null)
        {
            Debug.Log("[Widget-Be-Gone] Mod disabled, not removing widgets.");
            return;
        }

        ReadSettings();

        if (_removedTypes.Count == 0)
        {
            Debug.Log("[Widget-Be-Gone] Removing no widgets because none are configured.");
            return;
        }

        Debug.Log("[Widget-Be-Gone] Removing illegal widget" + (_removedTypes.Count == 1 ? "" : "s") + ": " + _removedTypes.Select(t => t.Name).Join());

        for (int i = ___Widgets.Count - 1; i >= 0; i--)
            if (_removedTypes.Any(t => t.IsAssignableFrom(___Widgets[i].GetType())))
                ___Widgets.RemoveAt(i);

        for (int i = ___RequiredWidgets.Count - 1; i >= 0; i--)
            if (_removedTypes.Any(t => t.IsAssignableFrom(___RequiredWidgets[i].GetType())))
                ___RequiredWidgets.RemoveAt(i);
    }

    private static void ReadSettings()
    {
        var settings = _instance._settings.Read();
        if (settings == null)
        {
            Debug.Log("[Widget-Be-Gone] Settings read failed! Ignoring all widgets.");
            settings = new Settings() { Batteries = true, Indicators = true, Ports = true };
            _instance._settings.Write(settings);
        }

        _removedTypes = new List<Type>();
        if (settings.Batteries)
            _removedTypes.Add(_batteryType);
        if (settings.Indicators)
            _removedTypes.Add(_indicatorType);
        if (settings.Ports)
            _removedTypes.Add(_portType);
        if (settings.SerialNumber)
            _removedTypes.Add(_serialType);
    }

    private static Type[] GetLoadableTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
        catch (Exception)
        {
            return new Type[0];
        }
    }

    private class Settings
    {
        public bool Batteries;
        public bool Indicators;
        public bool Ports;
        public bool SerialNumber;
    }

    private static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[] {
        new Dictionary<string, object>
        {
            { "Filename", "WidgetBeGone-settings.txt" },
            { "Name", "Widget-Be-Gone" },
            {
                "Listings",
                new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "Key", "Batteries" }, { "Text", "Remove Batteries" } },
                    new Dictionary<string, object> { { "Key", "Indicators" }, { "Text", "Remove Indicators" } },
                    new Dictionary<string, object> { { "Key", "Ports" }, { "Text", "Remove Port Plates" } },
                    new Dictionary<string, object> { { "Key", "SerialNumber" }, { "Text", "Remove the Serial Number" } },
                }
            }
        }
    };
}

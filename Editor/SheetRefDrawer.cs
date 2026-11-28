using System;
using System.Collections.Generic;
using System.Linq;
using Kentavr.SheetsToSO.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kentavr.SheetsToSO.Editor
{
    /// <summary>
    /// Draws a <see cref="SheetRefAttribute"/> string field as a dropdown of the ids of its table asset.
    /// The value is never changed by drawing: a value the table does not have is shown as an error and kept.
    /// </summary>
    [CustomPropertyDrawer(typeof(SheetRefAttribute))]
    internal sealed class SheetRefDrawer : PropertyDrawer
    {
        private const string EmptyText = "(пусто)";
        private const string MixedText = "—";

        // Table type + requested path -> found asset and its ids.
        private static readonly Dictionary<(Type, string), Entry> Cache = new Dictionary<(Type, string), Entry>();

        private static GUIStyle missingStyle;

        static SheetRefDrawer()
        {
            EditorApplication.projectChanged += Cache.Clear;
            Undo.undoRedoPerformed += Cache.Clear;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var view = Resolve(property);
            var height = view.FallbackField
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : EditorGUIUtility.singleLineHeight;

            if (view.Message != null)
                height += MessageHeight + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var view = Resolve(property);

            if (view.Message != null)
            {
                var messageRect = EditorGUI.IndentedRect(new Rect(position.x, position.y, position.width, MessageHeight));
                var icon = EditorGUIUtility.IconContent(view.IsError ? "console.erroricon.sml" : "console.warnicon.sml");
                GUI.Label(messageRect, new GUIContent(view.Message, icon.image, view.Message), EditorStyles.helpBox);
                position.yMin += MessageHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            if (view.FallbackField)
            {
                // Inside its own drawer Unity draws the property with the default field.
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            DrawDropdown(position, property, label, view.Entry);
        }

        private static float MessageHeight => EditorGUIUtility.singleLineHeight + 4f;

        private void DrawDropdown(Rect position, SerializedProperty property, GUIContent label, Entry entry)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            var buttonRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var mixed = property.hasMultipleDifferentValues;
            var value = property.stringValue;
            var content = new GUIContent(mixed ? MixedText : string.IsNullOrEmpty(value) ? EmptyText : value);
            var style = EditorStyles.popup;

            if (!mixed && SheetRefChoices.Classify(value, entry.IdSet) == SheetRefState.Missing)
            {
                content.image = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                content.tooltip = $"'{value}': нет строки в {entry.TableName} ({entry.Path})";
                style = MissingStyle;
            }

            var showMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            if (EditorGUI.DropdownButton(buttonRect, content, FocusType.Keyboard, style))
            {
                // Rows may have been edited by hand in the table inspector: always show the current ids.
                entry.ReadIds();
                var targets = property.serializedObject.targetObjects;
                var path = property.propertyPath;
                new IdDropdown(entry.TableName, entry.Ids, id => Write(targets, path, id)).Show(buttonRect);
            }
            EditorGUI.showMixedValue = showMixed;

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        // The dropdown calls back after this OnGUI is gone, so the value is written through a fresh SerializedObject.
        private static void Write(Object[] targets, string path, string id)
        {
            var alive = targets.Where(target => target != null).ToArray();
            if (alive.Length == 0)
                return;

            using (var serializedObject = new SerializedObject(alive))
            {
                var property = serializedObject.FindProperty(path);
                if (property == null || property.propertyType != SerializedPropertyType.String)
                    return;

                property.stringValue = id;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static GUIStyle MissingStyle
        {
            get
            {
                if (missingStyle == null)
                {
                    missingStyle = new GUIStyle(EditorStyles.popup);
                    var red = new Color(0.9f, 0.25f, 0.25f);
                    missingStyle.normal.textColor = red;
                    missingStyle.hover.textColor = red;
                    missingStyle.focused.textColor = red;
                    missingStyle.active.textColor = red;
                }

                return missingStyle;
            }
        }

        private View Resolve(SerializedProperty property)
        {
            var sheetRef = (SheetRefAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.String)
                return View.Fallback("[SheetRef] работает только с полями string, string[] и List<string>");

            var tableType = sheetRef.TableType;
            if (tableType == null || tableType.IsAbstract || tableType.ContainsGenericParameters ||
                SheetAssetImporter.GetRowType(tableType) == null)
            {
                var name = tableType == null ? "null" : tableType.FullName;
                return View.Fallback($"[SheetRef]: тип {name} должен быть неабстрактным наследником SheetTable<>");
            }

            var entry = GetEntry(tableType, sheetRef.Asset);
            if (entry.Pick.Error != null)
                return View.Fallback(entry.Pick.Error);

            return new View { Entry = entry, Message = entry.Pick.Warning };
        }

        private static Entry GetEntry(Type tableType, string requested)
        {
            var key = (tableType, requested ?? string.Empty);
            if (!Cache.TryGetValue(key, out var entry) || entry.IsStale)
            {
                entry = Entry.Load(tableType, requested);
                Cache[key] = entry;
            }

            return entry;
        }

        private struct View
        {
            public Entry Entry;
            public string Message;
            public bool IsError;
            public bool FallbackField;

            public static View Fallback(string error)
            {
                return new View { Message = error, IsError = true, FallbackField = true };
            }
        }

        private sealed class Entry
        {
            private int dirtyCount;

            public SheetRefAssetPick Pick { get; private set; }

            public string TableName { get; private set; }

            public string Path => Pick.Path;

            public Object Table { get; private set; }

            public IReadOnlyList<string> Ids { get; private set; } = Array.Empty<string>();

            public HashSet<string> IdSet { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

            // The asset was deleted, or its rows were changed (e.g. by hand in its inspector) since the ids were read.
            public bool IsStale => Pick.Path != null &&
                (Table == null || EditorUtility.GetDirtyCount(Table) != dirtyCount);

            public static Entry Load(Type tableType, string requested)
            {
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:" + tableType.Name))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    // Exact type only: subclasses are other tables.
                    if (asset != null && asset.GetType() == tableType)
                        paths.Add(assetPath);
                }

                var entry = new Entry
                {
                    TableName = tableType.Name,
                    Pick = SheetRefChoices.PickAsset(paths, requested, tableType.Name),
                };

                if (entry.Pick.Path != null)
                {
                    entry.Table = AssetDatabase.LoadMainAssetAtPath(entry.Pick.Path);
                    entry.ReadIds();
                }

                return entry;
            }

            public void ReadIds()
            {
                if (Table == null)
                    return;

                var rows = (Table as ISheetTableData)?.RowsData;
                var ids = new List<string>();
                if (rows != null)
                {
                    foreach (var row in rows)
                        ids.Add(row is ISheetRow sheetRow ? sheetRow.Id : null);
                }

                Ids = SheetRefChoices.BuildIds(ids);
                IdSet = new HashSet<string>(Ids, StringComparer.Ordinal);
                dirtyCount = EditorUtility.GetDirtyCount(Table);
            }
        }

        private sealed class IdDropdown : AdvancedDropdown
        {
            private readonly string title;
            private readonly IReadOnlyList<string> ids;
            private readonly Action<string> onPick;

            public IdDropdown(string title, IReadOnlyList<string> ids, Action<string> onPick)
                : base(new AdvancedDropdownState())
            {
                this.title = title;
                this.ids = ids;
                this.onPick = onPick;
                minimumSize = new Vector2(200f, 300f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(title);
                root.AddChild(new IdItem(EmptyText, string.Empty));
                foreach (var id in ids)
                    root.AddChild(new IdItem(id, id));

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is IdItem idItem)
                    onPick(idItem.Value);
            }
        }

        private sealed class IdItem : AdvancedDropdownItem
        {
            public IdItem(string name, string value) : base(name)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}

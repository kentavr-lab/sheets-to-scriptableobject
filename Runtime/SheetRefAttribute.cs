using System;
using UnityEngine;

namespace Kentavr.SheetsToSO
{
    /// <summary>
    /// Shows a <c>string</c> field that stores a row id as a dropdown of the ids of a table asset:
    /// <c>[SheetRef(typeof(WeaponsTable))] public string weaponId;</c>.
    /// Works on <c>string</c>, <c>string[]</c> and <c>List&lt;string&gt;</c> fields (each element gets its own dropdown).
    /// An empty value is allowed; a value the table does not have is shown as an error and kept.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SheetRefAttribute : PropertyAttribute
    {
        /// <param name="tableType">Non-abstract subclass of <see cref="SheetTable{TRow}"/> whose asset gives the ids.</param>
        public SheetRefAttribute(Type tableType)
        {
            TableType = tableType;
        }

        /// <summary>Non-abstract subclass of <see cref="SheetTable{TRow}"/> whose asset gives the ids.</summary>
        public Type TableType { get; }

        /// <summary>
        /// Optional project path of the table asset, e.g. <c>"Assets/Data/Weapons.asset"</c>.
        /// Needed when the project has several assets of <see cref="TableType"/>; without it the first by path is used.
        /// </summary>
        public string Asset { get; set; }
    }
}

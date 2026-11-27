namespace Kentavr.SheetsToSO.Core
{
    /// <summary>
    /// A row of a sheet tab that can be looked up by its id.
    /// Implemented by generated row classes and by hand-written ones (schema-from-code mode).
    /// </summary>
    public interface ISheetRow
    {
        /// <summary>Row identifier, unique within the tab. Lookup is case-sensitive.</summary>
        string Id { get; }
    }
}

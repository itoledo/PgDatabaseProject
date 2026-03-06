namespace PgDatabaseProject.Core.Models;

public enum DatabaseObjectType
{
    Schema,
    Extension,
    Sequence,
    Type,
    Table,
    View,
    Function,
    StoredProcedure,
    Trigger,
    Index
}
